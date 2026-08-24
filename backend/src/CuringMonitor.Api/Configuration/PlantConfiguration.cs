using System.Text.Json;
using System.Text.Json.Serialization;
using CuringMonitor.Api.Contracts;
using CuringMonitor.Api.Domain;

namespace CuringMonitor.Api.Configuration;

/// <summary>
/// The plant's static definition: which presses exist, which tags describe them and how
/// the tiles are laid out. Loaded once at start-up and never mutated afterwards.
/// </summary>
public sealed class PlantConfiguration
{
    private readonly Dictionary<string, PressDefinition> _pressesById;

    private PlantConfiguration(
        string title,
        IReadOnlyList<PressDefinition> presses,
        IReadOnlyList<TrenchDefinition> trenches)
    {
        Title = title;
        Presses = presses;
        Trenches = trenches;
        _pressesById = presses.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
        var trenchTags = trenches
            .Select(t => t.PressureTag)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag!);

        AllTags = presses
            .SelectMany(p => p.Tags.All())
            .Concat(trenchTags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string Title { get; }

    public IReadOnlyList<PressDefinition> Presses { get; }

    public IReadOnlyList<TrenchDefinition> Trenches { get; }

    /// <summary>Every distinct tag address the poller needs to read.</summary>
    public IReadOnlyList<string> AllTags { get; }

    public PressDefinition? Find(string pressId) =>
        _pressesById.TryGetValue(pressId, out var press) ? press : null;

    /// <summary>
    /// Loads the plant definition. A '.txt' path is read as the plant's existing SCADA
    /// press configuration; anything else is read as a layout file.
    /// </summary>
    /// <param name="trenchPressureTags">
    /// Optional trench number to pressure tag map. The legacy config carries no trench
    /// pressure tag, so it is supplied from application settings instead of being edited
    /// into a file the plant maintains for the old system.
    /// </param>
    public static PlantConfiguration Load(
        string path,
        string title,
        IReadOnlyDictionary<int, string>? trenchPressureTags = null)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Plant configuration '{path}' was not found.", path);
        }

        var (presses, trenches) = Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase)
            ? ReadLegacy(path)
            : ReadLayoutFile(path);

        if (trenchPressureTags is { Count: > 0 })
        {
            trenches = trenches
                .Select(trench => trenchPressureTags.TryGetValue(trench.Number, out var tag)
                    ? WithPressureTag(trench, tag)
                    : trench)
                .ToArray();
        }

        return new PlantConfiguration(title, presses, trenches);
    }

    private static (IReadOnlyList<PressDefinition> Presses, IReadOnlyList<TrenchDefinition> Trenches) ReadLegacy(string path)
    {
        var result = LegacyPressConfig.Read(path);
        return (result.Presses, result.Trenches);
    }

    private static (IReadOnlyList<PressDefinition> Presses, IReadOnlyList<TrenchDefinition> Trenches) ReadLayoutFile(string path)
    {
        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize<PlantFile>(stream, SerializerOptions)
                   ?? throw new InvalidOperationException($"Layout file '{path}' is empty.");

        if (file.Presses.Count == 0)
        {
            throw new InvalidOperationException($"Layout file '{path}' defines no presses.");
        }

        var duplicate = file.Presses
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Press '{duplicate.Key}' is defined more than once.");
        }

        return (file.Presses, file.Trenches);
    }

    private static TrenchDefinition WithPressureTag(TrenchDefinition trench, string tag) => new()
    {
        Number = trench.Number,
        Label = trench.Label,
        PressureTag = tag,
        Rows = trench.Rows
    };

    /// <summary>Projects the definition into the layout the client draws.</summary>
    public PlantLayout ToLayout()
    {
        var trenches = Trenches
            .Select(trench => new TrenchLayout(
                trench.Number,
                trench.DisplayName,
                trench.Rows.Select(row => row.Select(cell => ToCell(trench, cell)).ToArray()).ToArray()))
            .ToArray();

        return new PlantLayout(Title, trenches);
    }

    private LayoutCell ToCell(TrenchDefinition trench, string cell)
    {
        if (cell.StartsWith("trench:", StringComparison.OrdinalIgnoreCase))
        {
            var label = cell["trench:".Length..];
            return new LayoutCell("trench", trench.Number.ToString(), label);
        }

        if (string.IsNullOrWhiteSpace(cell) || cell == "-")
        {
            return new LayoutCell("gap", string.Empty, string.Empty);
        }

        var press = Find(cell);
        return press is null
            ? new LayoutCell("gap", string.Empty, string.Empty)
            : new LayoutCell("press", press.Id, press.DisplayName);
    }

    internal static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed class PlantFile
    {
        public List<TrenchDefinition> Trenches { get; init; } = [];

        public List<PressDefinition> Presses { get; init; } = [];
    }
}
