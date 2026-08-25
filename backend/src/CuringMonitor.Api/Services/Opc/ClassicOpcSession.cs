using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using CuringMonitor.Api.Configuration;
using CuringMonitor.Api.Domain;
using Microsoft.Extensions.Options;
using OPCAutomation;

namespace CuringMonitor.Api.Services.Opc;

/// <summary>
/// Classic OPC DA session over the OPC Automation 2.0 interface — the same route the
/// plant's existing services take (<c>SmartLogic.SmartOPC</c> in the BodyPly service wraps
/// this identically): connect to the server by ProgID, add every tag to one group, then
/// read that group's cache on a cadence.
/// </summary>
/// <remarks>
/// COM apartment rules make the underlying objects unsafe to call concurrently, so every
/// call here is serialised. Requires the OPC Core Components on the host, and Windows.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class ClassicOpcSession : IOpcSession
{
    /// <summary>OPC DA quality is good when the top two bits are set (0xC0).</summary>
    private const int QualityMask = 0xC0;
    private const int QualityGood = 0xC0;

    /// <summary>REGDB_E_CLASSNOTREG — the automation wrapper is missing, or is the wrong bitness.</summary>
    private const int ClassNotRegistered = unchecked((int)0x80040154);

    private readonly OpcOptions _options;
    private readonly ILogger<ClassicOpcSession> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Tag address to the server handle it was added under.</summary>
    private readonly Dictionary<string, int> _serverHandles = new(StringComparer.OrdinalIgnoreCase);

    private OPCServer? _server;
    private OPCGroup? _group;

    public ClassicOpcSession(IOptions<PlantOptions> options, ILogger<ClassicOpcSession> logger)
    {
        _options = options.Value.Opc;
        _logger = logger;
    }

    /// <summary>
    /// Creates the automation wrapper, turning the one failure everybody hits into an
    /// explanation. OPCDAAuto.dll is an in-process COM server, so it has to match the
    /// bitness of this process — a 64-bit build cannot load a 32-bit registration, and it
    /// reports that as "class not registered" rather than as a bitness problem.
    /// </summary>
    private static OPCServer CreateServer()
    {
        try
        {
            return new OPCServer();
        }
        catch (COMException ex) when (ex.HResult == ClassNotRegistered)
        {
            throw new InvalidOperationException(
                "The OPC DA automation wrapper (OPCDAAuto.dll) is not available to this " +
                $"process, which is running as {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}. " +
                "Either the OPC Core Components are not installed, or the wrapper is " +
                "registered for the other bitness. The usual fix is to build this service " +
                "as x86, since OPCDAAuto.dll is normally registered 32-bit only. " +
                "See docs/OPC-INTERFACE.md.",
                ex);
        }
    }

    public bool IsConnected
    {
        get
        {
            try
            {
                return _server is not null && _server.ServerState == (int)OPCServerState.OPCRunning;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A dead COM object throws rather than reporting a state.
                return false;
            }
        }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Teardown();

            _logger.LogInformation("Connecting to OPC server {Server}.", _options.ServerName);

            _server = CreateServer();
            _server.Connect(_options.ServerName, string.IsNullOrWhiteSpace(_options.Node) ? null! : _options.Node);

            _group = _server.OPCGroups.Add(_options.GroupName);
            _group.UpdateRate = (int)_options.UpdateRate.TotalMilliseconds;
            _group.IsActive = true;

            // Values are pulled from the group cache on our own cadence, so no callback
            // subscription is needed — one fewer COM apartment problem to have.
            _group.IsSubscribed = false;
            _group.DeadBand = 0;

            _serverHandles.Clear();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SubscribeAsync(IReadOnlyList<string> tags, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_group is null)
            {
                throw new InvalidOperationException("Connect before subscribing.");
            }

            _serverHandles.Clear();

            // Added in batches: a plant-sized tag list in one call is a long COM round trip,
            // and a batch that fails takes only its own tags down with it.
            foreach (var batch in tags.Chunk(_options.AddItemsBatchSize))
            {
                AddBatch(batch);
            }

            var rejected = tags.Count - _serverHandles.Count;
            if (rejected > 0)
            {
                // Almost always a typo in the equipment configuration. Those tags simply read
                // bad, so their boxes show as no communication rather than stopping the rest.
                _logger.LogWarning(
                    "{Rejected} of {Total} tags were rejected by the server and will read bad.",
                    rejected,
                    tags.Count);
            }

            _logger.LogInformation("Subscribed to {Count} tags in group {Group}.", _serverHandles.Count, _options.GroupName);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyDictionary<string, TagValue>> ReadAsync(
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var values = new Dictionary<string, TagValue>(tags.Count, StringComparer.OrdinalIgnoreCase);

            // Anything the server never accepted is bad by definition; seeding them here
            // means every requested tag has an entry whatever the read returns.
            foreach (var tag in tags)
            {
                values[tag] = TagValue.Bad(now);
            }

            if (_group is null || _serverHandles.Count == 0)
            {
                return values;
            }

            var known = tags.Where(_serverHandles.ContainsKey).ToArray();
            foreach (var batch in known.Chunk(_options.ReadBatchSize))
            {
                ReadBatch(batch, values, now);
            }

            return values;
        }
        finally
        {
            _gate.Release();
        }
    }

    private void AddBatch(IReadOnlyList<string> batch)
    {
        // The Automation interface uses 1-based arrays throughout; a 0-based array is the
        // classic way to get an unhelpful E_INVALIDARG out of this API.
        var itemIds = OneBased<string>(batch.Count);
        var clientHandles = OneBased<int>(batch.Count);

        for (var i = 0; i < batch.Count; i++)
        {
            itemIds.SetValue(batch[i], i + 1);
            clientHandles.SetValue(i + 1, i + 1);
        }

        _group!.OPCItems.AddItems(
            batch.Count,
            ref itemIds,
            ref clientHandles,
            out var serverHandles,
            out var errors,
            Type.Missing,
            Type.Missing);

        for (var i = 0; i < batch.Count; i++)
        {
            var error = Convert.ToInt32(errors.GetValue(i + 1));
            if (error != 0)
            {
                _logger.LogDebug("Server rejected tag {Tag} (0x{Error:X8}).", batch[i], error);
                continue;
            }

            _serverHandles[batch[i]] = Convert.ToInt32(serverHandles.GetValue(i + 1));
        }
    }

    private void ReadBatch(IReadOnlyList<string> batch, Dictionary<string, TagValue> values, DateTimeOffset now)
    {
        var handles = OneBased<int>(batch.Count);
        for (var i = 0; i < batch.Count; i++)
        {
            handles.SetValue(_serverHandles[batch[i]], i + 1);
        }

        _group!.SyncRead(
            (short)OPCDataSource.OPCCache,
            batch.Count,
            ref handles,
            out var readValues,
            out var errors,
            out var qualities,
            out var timestamps);

        var qualityArray = qualities as Array;
        var timestampArray = timestamps as Array;

        for (var i = 0; i < batch.Count; i++)
        {
            var index = i + 1;
            if (Convert.ToInt32(errors.GetValue(index)) != 0)
            {
                continue;
            }

            var quality = qualityArray is null ? QualityGood : Convert.ToInt32(qualityArray.GetValue(index));
            var isGood = (quality & QualityMask) == QualityGood;

            var timestamp = now;
            if (timestampArray?.GetValue(index) is DateTime stamp)
            {
                // The server reports UTC; treating it as local would drift the age of every
                // reading by the site's offset.
                timestamp = new DateTimeOffset(DateTime.SpecifyKind(stamp, DateTimeKind.Utc));
            }

            values[batch[i]] = new TagValue(readValues.GetValue(index), isGood, timestamp);
        }
    }

    private static Array OneBased<T>(int length) =>
        Array.CreateInstance(typeof(T), [length], [1]);

    private void Teardown()
    {
        try
        {
            if (_group is not null && _server is not null)
            {
                _server.OPCGroups.RemoveAll();
            }

            _server?.Disconnect();
        }
        catch (Exception ex)
        {
            // Tearing down a session that is already gone is expected during a reconnect.
            _logger.LogDebug(ex, "Ignoring error while closing the previous OPC session.");
        }
        finally
        {
            _group = null;
            _server = null;
            _serverHandles.Clear();
        }
    }

    public void Dispose()
    {
        _gate.Wait();
        try
        {
            Teardown();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}
