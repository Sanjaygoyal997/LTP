using CuringMonitor.Api.Configuration;
using CuringMonitor.Api.Domain;
using Microsoft.Extensions.Options;

namespace CuringMonitor.Api.Services;

/// <summary>
/// Session against the plant's OPC server. Deliberately kept behind this interface: the
/// classic-DA and UA client stacks differ, and only the site build needs to pick one.
/// </summary>
public interface IOpcSession : IDisposable
{
    bool IsConnected { get; }

    Task ConnectAsync(CancellationToken cancellationToken);

    /// <summary>Adds tags to the subscription group; safe to call repeatedly for the same tags.</summary>
    Task SubscribeAsync(IReadOnlyList<string> tags, CancellationToken cancellationToken);

    /// <summary>Returns the latest cached value of each subscribed tag.</summary>
    Task<IReadOnlyDictionary<string, TagValue>> ReadAsync(
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken);
}

/// <summary>
/// Reads press tags from the plant OPC server, keeping the session alive across drops.
/// While the session is down every tag reads bad, which surfaces as "no communication"
/// on the display rather than as stale colours.
/// </summary>
public sealed class OpcPressDataProvider : IPressDataProvider, IDisposable
{
    private readonly IOpcSession _session;
    private readonly OpcOptions _options;
    private readonly ILogger<OpcPressDataProvider> _logger;
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    private DateTimeOffset _nextConnectAttempt = DateTimeOffset.MinValue;

    /// <summary>Last connect failure, so a retry loop reports a repeat briefly.</summary>
    private string? _lastConnectError;
    private int _repeatedFailures;

    /// <summary>Tags currently subscribed, so a configuration reload triggers a resubscribe.</summary>
    private HashSet<string>? _subscribedTags;

    public OpcPressDataProvider(
        IOpcSession session,
        IOptions<PlantOptions> options,
        ILogger<OpcPressDataProvider> logger)
    {
        _session = session;
        _options = options.Value.Opc;
        _logger = logger;
    }

    public bool IsConnected => _session.IsConnected;

    public async Task<IReadOnlyDictionary<string, TagValue>> ReadAsync(
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken)
    {
        if (!await EnsureConnectedAsync(tags, cancellationToken).ConfigureAwait(false))
        {
            return AllBad(tags);
        }

        try
        {
            var values = await _session.ReadAsync(tags, cancellationToken).ConfigureAwait(false);

            // A provider that silently drops tags would leave the display showing the last
            // good colour forever, so fill any gap with an explicitly bad reading.
            if (values.Count == tags.Count)
            {
                return values;
            }

            var now = DateTimeOffset.UtcNow;
            var complete = new Dictionary<string, TagValue>(tags.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var tag in tags)
            {
                complete[tag] = values.TryGetValue(tag, out var value) ? value : TagValue.Bad(now);
            }

            return complete;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "OPC read failed; marking all tags bad until the session recovers.");
            _subscribedTags = null;
            _nextConnectAttempt = DateTimeOffset.UtcNow + _options.ReconnectDelay;
            return AllBad(tags);
        }
    }

    private async Task<bool> EnsureConnectedAsync(IReadOnlyList<string> tags, CancellationToken cancellationToken)
    {
        if (_session.IsConnected && _subscribedTags is not null && _subscribedTags.SetEquals(tags))
        {
            return true;
        }

        if (DateTimeOffset.UtcNow < _nextConnectAttempt)
        {
            return false;
        }

        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_session.IsConnected)
            {
                _logger.LogInformation("Connecting to OPC server {Server}.", _options.ServerName);
                await _session.ConnectAsync(cancellationToken).ConfigureAwait(false);
            }

            if (_subscribedTags is null || !_subscribedTags.SetEquals(tags))
            {
                await _session.SubscribeAsync(tags, cancellationToken).ConfigureAwait(false);
                _subscribedTags = new HashSet<string>(tags, StringComparer.OrdinalIgnoreCase);
                _lastConnectError = null;
                _repeatedFailures = 0;
                _logger.LogInformation("Subscribed to {TagCount} tags.", tags.Count);
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ReportConnectFailure(ex);
            _nextConnectAttempt = DateTimeOffset.UtcNow + _options.ReconnectDelay;
            return false;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <summary>
    /// Logs a connect failure in full the first time, then keeps repeats to one line. A
    /// five-second retry loop that reprints a stack trace forever buries everything else in
    /// the log, including the message that says what finally changed.
    /// </summary>
    private void ReportConnectFailure(Exception ex)
    {
        var message = ex.Message;

        if (message == _lastConnectError)
        {
            _repeatedFailures++;
            _logger.LogWarning(
                "OPC connect still failing after {Count} attempts; retrying in {Delay}.",
                _repeatedFailures,
                _options.ReconnectDelay);
            return;
        }

        _lastConnectError = message;
        _repeatedFailures = 1;
        _logger.LogError(ex, "OPC connect failed; retrying in {Delay}.", _options.ReconnectDelay);
    }

    private static IReadOnlyDictionary<string, TagValue> AllBad(IReadOnlyList<string> tags)
    {
        var now = DateTimeOffset.UtcNow;
        var values = new Dictionary<string, TagValue>(tags.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var tag in tags)
        {
            values[tag] = TagValue.Bad(now);
        }

        return values;
    }

    public void Dispose()
    {
        _connectLock.Dispose();
        _session.Dispose();
    }
}
