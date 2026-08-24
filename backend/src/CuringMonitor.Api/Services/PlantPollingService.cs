using CuringMonitor.Api.Configuration;
using CuringMonitor.Api.Realtime;
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
    PlantStateStore store,
    IHubContext<PressStatusHub, IPressStatusClient> hub,
    IOptions<PlantOptions> options,
    ILogger<PlantPollingService> logger) : BackgroundService
{
    private readonly PlantOptions _options = options.Value;

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
        var values = await provider.ReadAsync(plant.AllTags, cancellationToken).ConfigureAwait(false);

        var snapshot = evaluator.Evaluate(
            plant,
            values,
            shifts.Current(now),
            provider.IsConnected,
            now);

        store.Publish(snapshot);
        await hub.Clients.All.Snapshot(snapshot).ConfigureAwait(false);
    }
}
