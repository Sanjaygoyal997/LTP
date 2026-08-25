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

    /// <summary>Connect-failure state, so a retry loop reports a change rather than a repeat.</summary>
    private string? _lastConnectError;
    private int _failedAttempts;
    private DateTimeOffset _failingSince;
    private DateTimeOffset _nextFailureLog;

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
                _logger.LogInformation("Subscribed to {TagCount} tags.", tags.Count);

                // Recovery is the other half of the transition: without it the log shows a
                // fault starting and never ending.
                if (_lastConnectError is not null)
                {
                    _logger.LogInformation(
                        "OPC recovered after {Attempts} failed attempts over {Duration:hh\\:mm\\:ss}.",
                        _failedAttempts,
                        DateTimeOffset.UtcNow - _failingSince);
                }

                _lastConnectError = null;
                _failedAttempts = 0;
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
    /// Reports a connect failure once, then backs off.
    ///
    /// A five-second retry loop that reprints a stack trace forever buries the one line that
    /// matters — the one saying what finally changed. A new failure is logged in full; the
    /// same failure repeating is reported at widening intervals, so a fault that lasts all
    /// weekend leaves a handful of lines rather than fifty thousand.
    /// </summary>
    private void ReportConnectFailure(Exception ex)
    {
        var now = DateTimeOffset.UtcNow;
        var message = ex.Message;

        if (message != _lastConnectError)
        {
            _lastConnectError = message;
            _failedAttempts = 1;
            _failingSince = now;
            _nextFailureLog = now + FailureLogIntervals[0];
            _logger.LogError(ex, "OPC connect failed; retrying every {Delay}.", _options.ReconnectDelay);
            return;
        }

        _failedAttempts++;
        if (now < _nextFailureLog)
        {
            return;
        }

        _logger.LogWarning(
            "OPC still unreachable: {Attempts} attempts over {Duration:hh\\:mm\\:ss}. Last error: {Error}",
            _failedAttempts,
            now - _failingSince,
            message);

        _nextFailureLog = now + NextInterval(now - _failingSince);
    }

    /// <summary>How long to stay quiet between repeats of the same failure.</summary>
    private static readonly TimeSpan[] FailureLogIntervals =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1)
    ];

    private static TimeSpan NextInterval(TimeSpan failingFor) => failingFor switch
    {
        { TotalMinutes: < 5 } => FailureLogIntervals[1],
        { TotalMinutes: < 15 } => FailureLogIntervals[2],
        _ => FailureLogIntervals[3]
    };

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
