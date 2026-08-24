using System.Collections.Immutable;
using System.Text.Json;

namespace CuringMonitor.Api.Screens;

/// <summary>Envelope the service validates; everything inside a widget belongs to the client.</summary>
/// <param name="Id">Screen id, taken from the document, defaulting to the file name.</param>
/// <param name="Title">Title shown on the display.</param>
/// <param name="Json">The document verbatim, served as-is.</param>
public sealed record ScreenDefinition(string Id, string Title, string Json);

/// <summary>
/// Loads screen documents from disk and keeps them current.
///
/// The service validates only the envelope — an id, a title and a list of widgets that
/// each name a type. Widget contents are passed through untouched, so adding a widget or a
/// property to one is a front-end change and a config edit, with no matching change here.
/// </summary>
public sealed class ScreenCatalog : IDisposable
{
    private readonly string _directory;
    private readonly ILogger<ScreenCatalog> _logger;
    private readonly FileSystemWatcher? _watcher;
    private readonly object _reloadLock = new();
    private readonly System.Timers.Timer _debounce;

    private ImmutableDictionary<string, ScreenDefinition> _screens =
        ImmutableDictionary<string, ScreenDefinition>.Empty.WithComparers(StringComparer.OrdinalIgnoreCase);

    /// <summary>Raised after a reload changed the catalogue, so displays can pick the edit up.</summary>
    public event Func<Task>? Changed;

    public ScreenCatalog(string directory, bool watch, ILogger<ScreenCatalog> logger)
    {
        _directory = directory;
        _logger = logger;

        // Editors save in bursts — write, rename, touch — so collapse a flurry into one reload.
        _debounce = new System.Timers.Timer(400) { AutoReset = false };
        _debounce.Elapsed += (_, _) => ReloadAndNotify();

        Reload();

        if (!watch || !Directory.Exists(directory))
        {
            return;
        }

        _watcher = new FileSystemWatcher(directory, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Deleted += OnFileEvent;
        _watcher.Renamed += OnFileEvent;
    }

    public IReadOnlyCollection<ScreenDefinition> All => _screens.Values.ToArray();

    public ScreenDefinition? Find(string id) => _screens.TryGetValue(id, out var screen) ? screen : null;

    /// <summary>The screen a client gets when it asks for no particular one.</summary>
    public ScreenDefinition? Default => _screens.Values.OrderBy(s => s.Id, StringComparer.OrdinalIgnoreCase).FirstOrDefault();

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private void ReloadAndNotify()
    {
        if (!Reload())
        {
            return;
        }

        var handler = Changed;
        if (handler is null)
        {
            return;
        }

        // Fire and forget: a failing display must not stop the catalogue from serving edits.
        _ = Task.Run(async () =>
        {
            try
            {
                await handler().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notifying displays of a screen change failed.");
            }
        });
    }

    /// <returns>True when the catalogue actually changed.</returns>
    private bool Reload()
    {
        lock (_reloadLock)
        {
            if (!Directory.Exists(_directory))
            {
                _logger.LogWarning("Screen directory {Directory} does not exist; no screens loaded.", _directory);
                return false;
            }

            var loaded = ImmutableDictionary.CreateBuilder<string, ScreenDefinition>(StringComparer.OrdinalIgnoreCase);

            foreach (var path in Directory.EnumerateFiles(_directory, "*.json"))
            {
                try
                {
                    var screen = ReadScreen(path);
                    if (loaded.ContainsKey(screen.Id))
                    {
                        _logger.LogWarning("Screen id '{Id}' is defined more than once; ignoring {Path}.", screen.Id, path);
                        continue;
                    }

                    loaded[screen.Id] = screen;
                }
                catch (Exception ex) when (ex is JsonException or InvalidOperationException or IOException)
                {
                    // A half-saved or malformed file must not blank the wall: keep serving
                    // whatever loaded cleanly and say which file is bad.
                    _logger.LogError(ex, "Screen {Path} could not be loaded and was skipped.", path);
                }
            }

            var next = loaded.ToImmutable();
            if (next.Count == 0)
            {
                _logger.LogWarning("No screens loaded from {Directory}; keeping the previous catalogue.", _directory);
                return false;
            }

            var unchanged = next.Count == _screens.Count &&
                            next.All(kv => _screens.TryGetValue(kv.Key, out var existing) && existing.Json == kv.Value.Json);
            if (unchanged)
            {
                return false;
            }

            _screens = next;
            _logger.LogInformation("Loaded {Count} screen(s): {Ids}.", next.Count, string.Join(", ", next.Keys));
            return true;
        }
    }

    private static ScreenDefinition ReadScreen(string path)
    {
        var json = File.ReadAllText(path);
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });

        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"{Path.GetFileName(path)}: a screen must be a JSON object.");
        }

        var id = root.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String
            ? idElement.GetString()!
            : Path.GetFileNameWithoutExtension(path);

        var title = root.TryGetProperty("title", out var titleElement) && titleElement.ValueKind == JsonValueKind.String
            ? titleElement.GetString()!
            : id;

        if (!root.TryGetProperty("widgets", out var widgets) || widgets.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"{Path.GetFileName(path)}: 'widgets' must be an array.");
        }

        var index = 0;
        foreach (var widget in widgets.EnumerateArray())
        {
            if (widget.ValueKind != JsonValueKind.Object ||
                !widget.TryGetProperty("type", out var type) ||
                type.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(type.GetString()))
            {
                throw new InvalidOperationException(
                    $"{Path.GetFileName(path)}: widget {index} has no 'type'.");
            }

            index++;
        }

        return new ScreenDefinition(id, title, json);
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce.Dispose();
    }
}
