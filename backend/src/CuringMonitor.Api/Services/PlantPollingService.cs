using CuringMonitor.Api.Configuration;
using CuringMonitor.Api.Contracts;
using CuringMonitor.Api.Realtime;
using CuringMonitor.Api.Services.Production;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace CuringMonitor.Api.Services;

/// <summary>
/// Reads the full tag set on a fixed cadence, evaluates it and pushes the result to every
/// connected display. One loop serves all clients: the wall display, however many browsers
/// are open, costs the plant network nothing extra.
/// </summary>
public sealed class PlantPollingService(
    PlantConfigurationProvider plantProvider,
    IPressDataProvider provider,
    PressStatusEvaluator evaluator,
    IShiftService shifts,
    IProductionSource production,
    PlantStateStore store,
    IHubContext<PressStatusHub, IPressStatusClient> hub,
    IOptions<PlantOptions> options,
    ILogger<PlantPollingService> logger) : BackgroundService
{
    private readonly PlantOptions _options = options.Value;

    /// <summary>Last reported health, so a change is logged the moment it happens.</summary>
    private bool? _lastSourceConnected;
    private bool? _lastProductionAvailable;
    private DateTimeOffset _nextHeartbeat = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var plant = plantProvider.Current;
        logger.LogInformation(
            "Polling {TagCount} tags across {AssetCount} boxes every {Interval}.",
            plant.AllTags.Count,
            plant.Assets.Count,
            _options.PollInterval);

        using var timer = new PeriodicTimer(_options.PollInterval);

        do
        {
            try
            {
                await PollOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let one bad cycle take the loop down; the display would freeze on
                // stale colours with no indication that anything is wrong.
                logger.LogError(ex, "Poll cycle failed; retrying on the next tick.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task PollOnceAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        // Taken per cycle: a configuration reload is picked up on the next tick, with no
        // restart and no torn read of a half-applied plant.
        var plant = plantProvider.Current;
        var shift = shifts.Current(now);

        var values = await provider.ReadAsync(plant.AllTags, cancellationToken).ConfigureAwait(false);
        var counts = await production.GetAsync(shift, cancellationToken).ConfigureAwait(false);

        var snapshot = evaluator.Evaluate(
            plant,
            values,
            shift,
            provider.IsConnected,
            counts,
            now);

        store.Publish(snapshot);
        await hub.Clients.All.Snapshot(snapshot).ConfigureAwait(false);

        ReportHealth(snapshot, counts.IsAvailable, now);
    }

    /// <summary>
    /// Says something when health changes, and otherwise once in a while.
    ///
    /// Logging every cycle would bury the changes; logging only changes would make silence
    /// ambiguous — a healthy plant and a dead service look identical. A slow heartbeat
    /// separates the two, and carries enough to see at a glance whether the picture is sane.
    /// </summary>
    private void ReportHealth(PlantSnapshot snapshot, bool productionAvailable, DateTimeOffset now)
    {
        var connected = snapshot.SourceConnected;
        var changed = connected != _lastSourceConnected || productionAvailable != _lastProductionAvailable;

        if (!changed && now < _nextHeartbeat)
        {
            return;
        }

        var totals = snapshot.Totals;

        if (changed && _lastSourceConnected is not null)
        {
            logger.LogWarning(
                "Health changed — process data {Process}, production {Production}.",
                connected ? "connected" : "disconnected",
                productionAvailable ? "available" : "unavailable");
        }

        logger.Log(
            connected && productionAvailable ? LogLevel.Information : LogLevel.Warning,
            "Shift {Shift}: {Running} running, {Stopped} stopped, {Alarm} in alarm, {NoComm} not communicating. " +
            "Production {ProductionTotal} (source {Production}). Process data {Process}.",
            snapshot.Shift,
            totals.Running,
            totals.Stopped,
            totals.Alarm,
            totals.NoCommunication,
            snapshot.Production.Total,
            productionAvailable ? "ok" : "unavailable",
            connected ? "ok" : "down");

        _lastSourceConnected = connected;
        _lastProductionAvailable = productionAvailable;
        _nextHeartbeat = now + _options.HeartbeatInterval;
    }
}
