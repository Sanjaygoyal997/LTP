using CuringMonitor.Api.Contracts;
using CuringMonitor.Api.Services;
using Microsoft.AspNetCore.SignalR;

namespace CuringMonitor.Api.Realtime;

/// <summary>Methods the server calls on a connected display.</summary>
public interface IPressStatusClient
{
    Task Snapshot(PlantSnapshot snapshot);
}

/// <summary>
/// Live channel for the wall display. A client that has just connected gets the current
/// snapshot immediately, so the screen never sits blank waiting for the next poll.
/// </summary>
public sealed class PressStatusHub(PlantStateStore store) : Hub<IPressStatusClient>
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync().ConfigureAwait(false);

        if (store.Current is { } snapshot)
        {
            await Clients.Caller.Snapshot(snapshot).ConfigureAwait(false);
        }
    }
}
