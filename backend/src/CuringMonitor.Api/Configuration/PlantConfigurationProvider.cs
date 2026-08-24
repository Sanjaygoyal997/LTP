using Microsoft.Extensions.Options;

namespace CuringMonitor.Api.Configuration;

/// <summary>
/// Holds the current plant definition and keeps it in step with the file it came from.
///
/// The plant configuration is a file the plant maintains — presses are commissioned,
/// renamed and moved between trenches — so an edit has to reach the wall display without
/// anyone restarting a service. Readers take <see cref="Current"/> per cycle; a reload
/// swaps it wholesale, so no reader ever sees a half-applied plant.
/// </summary>
public sealed class PlantConfigurationProvider : IDisposable
{
    private readonly string _path;
    private readonly PlantOptions _options;
    private readonly ILogger<PlantConfigurationProvider> _logger;
    private readonly FileSystemWatcher? _watcher;
    private readonly System.Timers.Timer _debounce;
    private readonly object _reloadLock = new();

    private volatile PlantConfiguration _current;

    public PlantConfigurationProvider(
        IOptions<PlantOptions> options,
        IHostEnvironment environment,
        ILogger<PlantConfigurationProvider> logger)
    {
        _options = options.Value;
        _logger = logger;

        _path = Path.IsPathRooted(_options.LayoutFile)
            ? _options.LayoutFile
            : Path.Combine(environment.ContentRootPath, _options.LayoutFile);

        // Fail fast on the first load: a display that can never render is worse than a
        // service that refuses to start and says why.
        _current = Load();

        // Editors and file copies save in bursts, so collapse a flurry into one reload.
        _debounce = new System.Timers.Timer(500) { AutoReset = false };
        _debounce.Elapsed += (_, _) => Reload();

        if (!_options.WatchConfiguration)
        {
            return;
        }

        var directory = Path.GetDirectoryName(_path);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            _logger.LogWarning("Cannot watch {Path}: its directory does not exist.", _path);
            return;
        }

        _watcher = new FileSystemWatcher(directory, Path.GetFileName(_path))
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Renamed += OnFileEvent;

        _logger.LogInformation("Watching {Path} for changes.", _path);
    }

    /// <summary>The definition in force right now.</summary>
    public PlantConfiguration Current => _current;

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private void Reload()
    {
        lock (_reloadLock)
        {
            PlantConfiguration next;
            try
            {
                next = Load();
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or FormatException)
            {
                // A half-written or malformed file must not blank the wall: keep serving the
                // definition already in force and say which file is bad.
                _logger.LogError(ex, "Reloading {Path} failed; keeping the previous configuration.", _path);
                return;
            }

            var previous = _current;
            _current = next;

            _logger.LogInformation(
                "Reloaded {Path}: {AssetCount} boxes ({Delta:+#;-#;0}), {TagCount} tags.",
                _path,
                next.Assets.Count,
                next.Assets.Count - previous.Assets.Count,
                next.AllTags.Count);
        }
    }

    private PlantConfiguration Load() =>
        PlantConfiguration.Load(_path, _options.Title);

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce.Dispose();
    }
}
