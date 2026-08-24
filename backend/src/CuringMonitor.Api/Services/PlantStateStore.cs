using CuringMonitor.Api.Contracts;

namespace CuringMonitor.Api.Services;

/// <summary>
/// Holds the latest snapshot. Readers (HTTP requests, newly connected display clients)
/// take whatever is current; the poller replaces it wholesale, so no reader ever sees a
/// half-updated plant.
/// </summary>
public sealed class PlantStateStore
{
    private volatile PlantSnapshot? _current;

    public PlantSnapshot? Current => _current;

    public void Publish(PlantSnapshot snapshot) => _current = snapshot;
}
