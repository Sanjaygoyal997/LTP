using System.Text.Json;
using System.Text.Json.Serialization;
using CuringMonitor.Api.Domain;

namespace CuringMonitor.Api.Configuration;

/// <summary>
/// Every box the plant has, and the tags behind them. Loaded once at start-up from
/// whichever source the deployment points at, and never mutated afterwards.
/// </summary>
public sealed class PlantConfiguration
{
    private readonly Dictionary<string, AssetDefinition> _assetsById;

    private PlantConfiguration(
        string title,
        IReadOnlyList<AssetDefinition> assets,
        IReadOnlyList<GroupDefinition> groups)
    {
        Title = title;
        Assets = assets;
        Groups = groups;
        _assetsById = assets.ToDictionary(a => a.Id, StringComparer.OrdinalIgnoreCase);
        AllTags = assets
            .SelectMany(a => a.Signals.Values)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string Title { get; }

    public IReadOnlyList<AssetDefinition> Assets { get; }

    public IReadOnlyList<GroupDefinition> Groups { get; }

    /// <summary>Every distinct tag address the poller needs to read.</summary>
    public IReadOnlyList<string> AllTags { get; }

    public AssetDefinition? Find(string assetId) =>
        _assetsById.TryGetValue(assetId, out var asset) ? asset : null;

    /// <summary>
    /// Loads the plant definition. A '.txt' path is read as the plant's existing SCADA
    /// press configuration; anything else is read as an asset file.
    /// </summary>
    /// <param name="gaugeTags">
    /// Group name to gauge tag. The legacy press configuration carries no trench pressure
    /// tag, so it is supplied from settings rather than by editing a file the old system
    /// still reads.
    /// </param>
    /// <param name="tilePitch">
    /// Legacy box width, used to turn a trench panel width from <c>trenchSize.txt</c> into
    /// boxes per row.
    /// </param>
    public static PlantConfiguration Load(
        string path,
        string title,
        IReadOnlyDictionary<string, string>? gaugeTags = null,
        int tilePitch = 46)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Plant configuration '{path}' was not found.", path);
        }

        IReadOnlyList<AssetDefinition> assets;
        IReadOnlyList<GroupDefinition> groups;

        if (Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            var legacy = LegacyPressConfig.Read(path);
            assets = legacy.Assets;
            groups = legacy.Groups;
        }
        else
        {
            var file = ReadAssetFile(path);
            assets = file.Assets;
            groups = file.Groups.Count > 0 ? file.Groups : GroupsFrom(assets);
        }

        var duplicate = assets
            .GroupBy(a => a.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"{Path.GetFileName(path)}: asset '{duplicate.Key}' is defined more than once.");
        }

        return new PlantConfiguration(title, assets, groups);
    }

    /// <summary>Derives groups from the assets when the source file does not declare them.</summary>
    private static IReadOnlyList<GroupDefinition> GroupsFrom(IReadOnlyList<AssetDefinition> assets) =>
        assets
            .Select(a => a.DisplayGroup)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select((key, index) => new GroupDefinition { Key = key, Label = key, Order = index })
            .ToArray();

    private static AssetFile ReadAssetFile(string path)
    {
        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize<AssetFile>(stream, SerializerOptions)
                   ?? throw new InvalidOperationException($"Asset file '{path}' is empty.");

        if (file.Assets.Count == 0)
        {
            throw new InvalidOperationException($"Asset file '{path}' defines no assets.");
        }

        return file;
    }

    internal static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private sealed class AssetFile
    {
        public List<AssetDefinition> Assets { get; init; } = [];

        /// <summary>Optional: without it, groups are derived from the assets in first-seen order.</summary>
        public List<GroupDefinition> Groups { get; init; } = [];
    }
}
