using System.Text.Json;
using System.Text.Json.Serialization;
using CuringMonitor.Api.Configuration;
using CuringMonitor.Api.Endpoints;
using CuringMonitor.Api.Realtime;
using CuringMonitor.Api.Screens;
using CuringMonitor.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

// Offline utility: convert a legacy SCADA press config into this service's layout file.
//   dotnet run -- import-legacy <config_AB.txt> <assets.json> [title]
if (args is ["import-legacy", var legacyPath, var outputPath, ..])
{
    var title = args.Length > 3 ? args[3] : "Curing Press Status";
    File.WriteAllText(outputPath, LegacyPressConfig.ToAssetJson(legacyPath, title));
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

    return PlantConfiguration.Load(path, options.Title, options.GaugeTags);
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
        // The OPC stack itself is a site decision (classic DA via interop, or UA), so the
        // session is registered by the deployment rather than baked in here.
        if (sp.GetService<IOpcSession>() is null)
        {
            throw new InvalidOperationException(
                "Plant:Provider is 'opc' but no IOpcSession is registered. Register the " +
                "site's OPC session implementation, or set Plant:Provider to 'simulated'. " +
                "See docs/ARCHITECTURE.md.");
        }

        return ActivatorUtilities.CreateInstance<OpcPressDataProvider>(sp);
    }

    throw new InvalidOperationException(
        $"Unknown Plant:Provider '{options.Provider}'. Expected 'simulated' or 'opc'.");
});

// Screens are read from disk and watched, so editing one updates every display without a
// restart. Watching is off by default in production, where config arrives by deployment.
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<PlantOptions>>().Value;
    var path = Path.IsPathRooted(options.ScreensDirectory)
        ? options.ScreensDirectory
        : Path.Combine(builder.Environment.ContentRootPath, options.ScreensDirectory);

    return new ScreenCatalog(path, options.WatchScreens, sp.GetRequiredService<ILogger<ScreenCatalog>>());
});

builder.Services.AddHostedService<PlantPollingService>();

// Status reaches the client as "running", never as 3. Minimal APIs and SignalR each
// carry their own serializer options, so both need the converter.
var jsonEnumConverter = new JsonStringEnumConverter(JsonNamingPolicy.CamelCase);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(jsonEnumConverter));

builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
        options.PayloadSerializerOptions.Converters.Add(jsonEnumConverter));

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

// Push config edits to the displays rather than making someone reload every wall panel.
var screens = app.Services.GetRequiredService<ScreenCatalog>();
var hub = app.Services.GetRequiredService<IHubContext<PressStatusHub, IPressStatusClient>>();
screens.Changed += () => hub.Clients.All.ScreensChanged();

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
