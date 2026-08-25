using Microsoft.Extensions.Options;

namespace CuringMonitor.Api.Configuration;

/// <summary>
/// Holds the current plant definition and keeps it in step with the file it came from.
///
/// The plant configuration is a file the plant maintains — presses are commissioned,
/// renamed and moved between groups — so an edit has to reach the wall display without
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

    /// <summary>File names a change should trigger a reload for, from the last load.</summary>
    private volatile HashSet<string> _watchedNames = new(StringComparer.OrdinalIgnoreCase);

    public PlantConfigurationProvider(
        IOptions<PlantOptions> options,
        IHostEnvironment environment,
        ILogger<PlantConfigurationProvider> logger)
    {
        _options = options.Value;
        _logger = logger;

        _path = ContentPaths.Resolve(_options.LayoutFile, environment.ContentRootPath);

        // Fail fast on the first load: a display that can never render is worse than a
        // service that refuses to start and says why.
        _current = Load();
        _watchedNames = NamesOf(_current);

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

        // Watch the directory rather than one file name: the definition is assembled from
        // the equipment configuration *and* its companion panel-geometry file, and a
        // companion that appears for the first time has to be noticed too.
        _watcher = new FileSystemWatcher(directory)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Renamed += OnFileEvent;
        _watcher.Deleted += OnFileEvent;

        _logger.LogInformation(
            "Watching {Directory} for changes to {Files}.",
            directory,
            string.Join(", ", _watchedNames));
    }

    /// <summary>The definition in force right now.</summary>
    public PlantConfiguration Current => _current;

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        if (e.Name is null || !_watchedNames.Contains(e.Name))
        {
            return;
        }

        _debounce.Stop();
        _debounce.Start();
    }

    /// <summary>
    /// File names to react to. Taken from what the last load actually read, so adding a
    /// companion file to the loader does not mean remembering to add it here too.
    /// </summary>
    private static HashSet<string> NamesOf(PlantConfiguration configuration) =>
        configuration.SourceFiles
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;

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
            _watchedNames = NamesOf(next);

            _logger.LogInformation(
                "Reloaded {Path}: {AssetCount} boxes ({Delta:+#;-#;0}), {TagCount} tags.",
                _path,
                next.Assets.Count,
                next.Assets.Count - previous.Assets.Count,
                next.AllTags.Count);
        }
    }

    private PlantConfiguration Load() =>
        PlantConfiguration.Load(_path, _options.Title, _options.GroupLabelFormat);

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce.Dispose();
    }
}
