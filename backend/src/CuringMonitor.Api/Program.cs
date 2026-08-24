using CuringMonitor.Api.Configuration;
using CuringMonitor.Api.Endpoints;
using CuringMonitor.Api.Realtime;
using CuringMonitor.Api.Services;
using Microsoft.Extensions.Options;

// Offline utility: convert a legacy SCADA press config into this service's layout file.
//   dotnet run -- import-legacy <config_AB.txt> <plant-layout.json> [title]
if (args is ["import-legacy", var legacyPath, var outputPath, ..])
{
    var title = args.Length > 3 ? args[3] : "Curing Press Status";
    File.WriteAllText(outputPath, LegacyConfigImporter.Convert(legacyPath, title));
    Console.WriteLine($"Wrote {outputPath}");
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<PlantOptions>()
    .Bind(builder.Configuration.GetSection(PlantOptions.SectionName))
    .Validate(o => o.PollInterval > TimeSpan.Zero, "Plant:PollInterval must be positive.")
    .Validate(o => o.StaleAfter > o.PollInterval, "Plant:StaleAfter must exceed Plant:PollInterval.")
    .ValidateOnStart();

// The plant definition is static for the life of the process: load it once, fail fast if
// it is missing or malformed rather than starting a display that can never render.
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<PlantOptions>>().Value;
    var path = Path.IsPathRooted(options.LayoutFile)
        ? options.LayoutFile
        : Path.Combine(builder.Environment.ContentRootPath, options.LayoutFile);

    return PlantConfiguration.Load(path, options.Title);
});

builder.Services.AddSingleton<IShiftService, ShiftService>();
builder.Services.AddSingleton<PressStatusEvaluator>();
builder.Services.AddSingleton<PlantStateStore>();

builder.Services.AddSingleton<IPressDataProvider>(sp =>
{
    var options = sp.GetRequiredService<IOptions<PlantOptions>>().Value;

    if (string.Equals(options.Provider, "simulated", StringComparison.OrdinalIgnoreCase))
    {
        return new SimulatedPressDataProvider();
    }

    if (string.Equals(options.Provider, "opc", StringComparison.OrdinalIgnoreCase))
    {
        // Register an IOpcSession for the site's OPC stack (classic DA via interop, or UA)
        // and this provider drives it. See docs/BACKEND.md.
        return ActivatorUtilities.CreateInstance<OpcPressDataProvider>(sp);
    }

    throw new InvalidOperationException(
        $"Unknown Plant:Provider '{options.Provider}'. Expected 'simulated' or 'opc'.");
});

builder.Services.AddHostedService<PlantPollingService>();

builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

const string DisplayCorsPolicy = "display";
builder.Services.AddCors(options => options.AddPolicy(DisplayCorsPolicy, policy => policy
    .WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                 ?? ["http://localhost:5173"])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(DisplayCorsPolicy);

app.MapPlantEndpoints();
app.MapHub<PressStatusHub>("/hubs/press-status");
app.MapGet("/health", (PlantStateStore store, IPressDataProvider provider) => Results.Ok(new
{
    status = store.Current is null ? "starting" : "ok",
    sourceConnected = provider.IsConnected,
    lastSnapshot = store.Current?.Timestamp
}));

app.Run();
