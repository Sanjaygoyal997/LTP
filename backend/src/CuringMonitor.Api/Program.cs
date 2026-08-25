using System.Text.Json;
using System.Text.Json.Serialization;
using CuringMonitor.Api.Configuration;
using CuringMonitor.Api.Endpoints;
using CuringMonitor.Api.Realtime;
using CuringMonitor.Api.Screens;
using CuringMonitor.Api.Services;
using CuringMonitor.Api.Services.Opc;
using CuringMonitor.Api.Services.Production;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

// Offline utility: convert a legacy SCADA press config into this service's layout file.
//   dotnet run -- import-legacy <config_AB.txt> <assets.json> [title]
if (args is ["import-legacy", var legacyPath, var outputPath, ..])
{
    var title = args.Length > 3 ? args[3] : "Curing Press Status";
    File.WriteAllText(outputPath, EquipmentConfigReader.ToAssetJson(legacyPath, title));
    Console.WriteLine($"Wrote {outputPath}");
    return;
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<PlantOptions>()
    .Bind(builder.Configuration.GetSection(PlantOptions.SectionName))
    .Validate(o => o.PollInterval > TimeSpan.Zero, "Plant:PollInterval must be positive.")
    .ValidateOnStart();

// The plant definition is loaded at start-up and reloaded whenever the file changes.
// Loading fails fast: a display that can never render is worse than a service that refuses
// to start and says why.
builder.Services.AddSingleton<PlantConfigurationProvider>();

builder.Services.AddSingleton<IShiftService, ShiftService>();
builder.Services.AddSingleton<PressStatusEvaluator>();
builder.Services.AddSingleton<PlantStateStore>();

builder.Services.AddSingleton<IPressDataProvider>(sp =>
{
    var options = sp.GetRequiredService<IOptions<PlantOptions>>().Value;
    var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    // Settings arrive from several files and the environment; naming the winner here turns
    // "my change did nothing" into something the log answers directly.
    log.LogInformation(
        "Environment {Environment}. Process data provider: {Provider}. Production source: {Production}.",
        builder.Environment.EnvironmentName,
        options.Provider,
        options.Production.Provider);

    if (string.Equals(options.Provider, "simulated", StringComparison.OrdinalIgnoreCase))
    {
        return new SimulatedPressDataProvider();
    }

    if (string.Equals(options.Provider, "opc", StringComparison.OrdinalIgnoreCase))
    {
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

// Classic OPC DA over the Automation interface, matching the plant's other services.
// Windows-only, which is inherent to DA rather than a choice of this service.
if (OperatingSystem.IsWindows())
{
    builder.Services.AddSingleton<IOpcSession, ClassicOpcSession>();
}

builder.Services.AddSingleton<IProductionSource>(sp =>
{
    var production = sp.GetRequiredService<IOptions<PlantOptions>>().Value.Production;

    return string.Equals(production.Provider, "sql", StringComparison.OrdinalIgnoreCase)
        ? ActivatorUtilities.CreateInstance<SqlProductionSource>(sp)
        : ActivatorUtilities.CreateInstance<SimulatedProductionSource>(sp);
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

// Serve the built display alongside the API when it has been copied into wwwroot, so a
// deployment is one process on one port rather than a service plus a web server.
if (Directory.Exists(Path.Combine(app.Environment.WebRootPath ?? string.Empty)))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.MapPlantEndpoints();
app.MapHub<PressStatusHub>("/hubs/press-status");
app.MapGet("/health", (PlantStateStore store, IPressDataProvider provider) => Results.Ok(new
{
    status = store.Current is null ? "starting" : "ok",
    sourceConnected = provider.IsConnected,
    lastSnapshot = store.Current?.Timestamp
}));

// Anything not matched by an endpoint is a client-side route, so hand back the display.
if (Directory.Exists(Path.Combine(app.Environment.WebRootPath ?? string.Empty)))
{
    app.MapFallbackToFile("index.html");
}

app.Run();
