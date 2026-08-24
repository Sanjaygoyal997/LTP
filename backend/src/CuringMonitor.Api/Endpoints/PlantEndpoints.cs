using CuringMonitor.Api.Configuration;
using CuringMonitor.Api.Contracts;
using CuringMonitor.Api.Screens;
using CuringMonitor.Api.Services;

namespace CuringMonitor.Api.Endpoints;

public static class PlantEndpoints
{
    public static IEndpointRouteBuilder MapPlantEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Plant");

        group.MapGet("/snapshot", (PlantStateStore store) =>
                store.Current is { } snapshot
                    ? Results.Ok(snapshot)
                    : Results.StatusCode(StatusCodes.Status503ServiceUnavailable))
            .WithName("GetSnapshot")
            .WithSummary("Latest state of every box. 503 until the first poll completes.")
            .Produces<PlantSnapshot>()
            .Produces(StatusCodes.Status503ServiceUnavailable);

        group.MapGet("/assets/{id}", (string id, PlantStateStore store) =>
            {
                var asset = store.Current?.Assets
                    .FirstOrDefault(a => string.Equals(a.Id, id, StringComparison.OrdinalIgnoreCase));

                return asset is null ? Results.NotFound() : Results.Ok(asset);
            })
            .WithName("GetAsset")
            .WithSummary("State of a single box.")
            .Produces<AssetSnapshot>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/screens", (ScreenCatalog catalogue) => Results.Ok(
                catalogue.All
                    .Select(s => new { s.Id, s.Title })
                    .OrderBy(s => s.Id)))
            .WithName("ListScreens")
            .WithSummary("Screens this service can serve.");

        group.MapGet("/screens/{id}", (string id, ScreenCatalog catalogue) =>
            {
                // "default" lets a wall panel be pointed at the service without naming a
                // screen, so renaming the screen does not mean re-configuring the panel.
                var screen = string.Equals(id, "default", StringComparison.OrdinalIgnoreCase)
                    ? catalogue.Default
                    : catalogue.Find(id);

                // Served verbatim: the document is the contract, and re-serialising it here
                // would quietly drop any property the service does not know about.
                return screen is null
                    ? Results.NotFound()
                    : Results.Content(screen.Json, "application/json");
            })
            .WithName("GetScreen")
            .WithSummary("One screen document, exactly as it is on disk.")
            .Produces(StatusCodes.Status200OK, contentType: "application/json")
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
