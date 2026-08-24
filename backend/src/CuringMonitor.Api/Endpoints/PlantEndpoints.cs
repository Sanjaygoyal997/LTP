using CuringMonitor.Api.Configuration;
using CuringMonitor.Api.Contracts;
using CuringMonitor.Api.Services;

namespace CuringMonitor.Api.Endpoints;

public static class PlantEndpoints
{
    public static IEndpointRouteBuilder MapPlantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Plant");

        group.MapGet("/layout", (PlantConfiguration plant) => Results.Ok(plant.ToLayout()))
            .WithName("GetLayout")
            .WithSummary("Tile grid: trenches, rows and the cell in each position.");

        group.MapGet("/snapshot", (PlantStateStore store) =>
                store.Current is { } snapshot
                    ? Results.Ok(snapshot)
                    : Results.StatusCode(StatusCodes.Status503ServiceUnavailable))
            .WithName("GetSnapshot")
            .WithSummary("Latest state of every press. 503 until the first poll completes.")
            .Produces<PlantSnapshot>()
            .Produces(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/presses/{id}", (string id, PlantStateStore store) =>
            {
                var press = store.Current?.Presses
                    .FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

                return press is null ? Results.NotFound() : Results.Ok(press);
            })
            .WithName("GetPress")
            .WithSummary("State of a single press.")
            .Produces<PressSnapshot>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
