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

    private PlantConfiguration(string title, IReadOnlyList<AssetDefinition> assets)
    {
        Title = title;
        Assets = assets;
        _assetsById = assets.ToDictionary(a => a.Id, StringComparer.OrdinalIgnoreCase);
        AllTags = assets
            .SelectMany(a => a.Signals.Values)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public string Title { get; }

    public IReadOnlyList<AssetDefinition> Assets { get; }

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
    public static PlantConfiguration Load(
        string path,
        string title,
        IReadOnlyDictionary<string, string>? gaugeTags = null)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Plant configuration '{path}' was not found.", path);
        }

        var assets = Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase)
            ? LegacyPressConfig.Read(path)
            : ReadAssetFile(path);

        if (gaugeTags is { Count: > 0 })
        {
            assets = assets.Select(asset => ApplyGaugeTag(asset, gaugeTags)).ToArray();
        }

        var duplicate = assets
            .GroupBy(a => a.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"{Path.GetFileName(path)}: asset '{duplicate.Key}' is defined more than once.");
        }

        return new PlantConfiguration(title, assets);
    }

    private static IReadOnlyList<AssetDefinition> ReadAssetFile(string path)
    {
        using var stream = File.OpenRead(path);
        var file = JsonSerializer.Deserialize<AssetFile>(stream, SerializerOptions)
                   ?? throw new InvalidOperationException($"Asset file '{path}' is empty.");

        if (file.Assets.Count == 0)
        {
            throw new InvalidOperationException($"Asset file '{path}' defines no assets.");
        }

        return file.Assets;
    }

    private static AssetDefinition ApplyGaugeTag(
        AssetDefinition asset,
        IReadOnlyDictionary<string, string> gaugeTags)
    {
        if (!asset.Kind.Equals(AssetKinds.Gauge, StringComparison.OrdinalIgnoreCase) ||
            asset.Signals.ContainsKey(SignalNames.Value) ||
            !gaugeTags.TryGetValue(asset.DisplayGroup, out var tag))
        {
            return asset;
        }

        var signals = new Dictionary<string, string>(asset.Signals) { [SignalNames.Value] = tag };

        return new AssetDefinition
        {
            Id = asset.Id,
            Kind = asset.Kind,
            Label = asset.Label,
            Group = asset.Group,
            Position = asset.Position,
            Attributes = asset.Attributes,
            Signals = signals
        };
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
    }
}
