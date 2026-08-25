using System.Globalization;
using CuringMonitor.Api.Configuration;
using CuringMonitor.Api.Contracts;
using CuringMonitor.Api.Domain;
using CuringMonitor.Api.Services.Production;
using Microsoft.Extensions.Options;

namespace CuringMonitor.Api.Services;

/// <summary>
/// Turns raw tag readings into the snapshot the display renders, reproducing the rules the
/// legacy mimic used: the band shows run, stop or no communication; a fault is a separate
/// flag the display flashes on the box header; and the press totals count bands, so an
/// alarmed press still counts as whatever its band shows.
/// </summary>
public sealed class PressStatusEvaluator(IOptions<PlantOptions> options)
{
    private readonly PlantOptions _options = options.Value;
    private readonly SignalOptions _signals = options.Value.Signals;

    /// <summary>
    /// Values treated as true for the alarm signal. The tag's text is compared rather than
    /// its type, since OPC servers surface booleans variously as 1, -1 or True.
    /// </summary>
    private static readonly HashSet<string> TruthyValues =
        new(["1", "-1", "true"], StringComparer.OrdinalIgnoreCase);

    public PlantSnapshot Evaluate(
        PlantConfiguration plant,
        IReadOnlyDictionary<string, TagValue> tags,
        Shift shift,
        bool sourceConnected,
        ProductionCounts production,
        DateTimeOffset now)
    {
        var assets = new List<AssetSnapshot>(plant.Assets.Count);
        int running = 0, stopped = 0, alarms = 0, noComm = 0;

        foreach (var definition in plant.Assets)
        {
            var asset = EvaluateAsset(definition, tags, production, now);
            assets.Add(asset);

            switch (asset.Status)
            {
                case PressStatus.Running: running++; break;
                case PressStatus.Stopped: stopped++; break;
                default: noComm++; break;
            }

            if (asset.Alarm)
            {
                alarms++;
            }
        }

        return new PlantSnapshot(
            now,
            shift.Name.ToString(),
            shift.ProductionDate,
            sourceConnected,
            new ProductionTotals(
                production.ForShift(ShiftName.A),
                production.ForShift(ShiftName.B),
                production.ForShift(ShiftName.C)),
            new PressTotals(running, stopped, alarms, noComm),
            assets,
            plant.Groups
                .Select(g => new GroupSnapshot(g.Key, g.DisplayLabel, g.Order, g.PanelWidth, g.PanelHeight))
                .ToArray());
    }

    private AssetSnapshot EvaluateAsset(
        AssetDefinition definition,
        IReadOnlyDictionary<string, TagValue> tags,
        ProductionCounts production,
        DateTimeOffset now)
    {
        // Every configured signal is published, whether or not the rules use it, so a screen
        // can bind to anything the plant chose to wire up.
        var signals = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, address) in definition.Signals)
        {
            var reading = Read(address, tags);
            signals[name] = reading.IsGood ? reading.Value : null;
        }

        // Cures come from the MES against this item's work centre, not from a tag.
        definition.Attributes.TryGetValue(SignalNames.WorkCentreAttribute, out var workCentre);
        signals["count"] = production.For(workCentre);

        return new AssetSnapshot(
            definition.Id,
            definition.Kind,
            definition.DisplayLabel,
            definition.DisplayGroup,
            definition.Position,
            BandState(definition, tags),
            IsTrue(Signal(definition, _signals.Alarm, tags)),
            definition.Attributes,
            signals,
            now);
    }

    /// <summary>
    /// The band state, from the communication-check signal alone.
    ///
    /// The run-check signal does double duty: no reading at all means the equipment is not
    /// talking, and its value against a threshold separates running from stopped. Which
    /// signal that is, and the threshold, are both configuration — per item where the file
    /// gives one, otherwise the service-wide default.
    /// </summary>
    private PressStatus BandState(AssetDefinition definition, IReadOnlyDictionary<string, TagValue> tags)
    {
        var reading = Signal(definition, definition.RunSignal ?? _signals.RunCheck, tags);

        if (!reading.IsGood || reading.Value is null || !reading.TryGetDouble(out var value))
        {
            return PressStatus.NoCommunication;
        }

        var threshold = definition.RunThreshold ?? _options.RunThreshold;

        return value >= threshold ? PressStatus.Running : PressStatus.Stopped;
    }

    private static bool IsTrue(TagValue tag) => Text(tag) is { } text && TruthyValues.Contains(text);

    private static string? Text(TagValue tag) =>
        tag.IsGood && tag.Value is not null
            ? Convert.ToString(tag.Value, CultureInfo.InvariantCulture)?.Trim()
            : null;

    private static TagValue Signal(
        AssetDefinition definition,
        string name,
        IReadOnlyDictionary<string, TagValue> tags) =>
        definition.Signals.TryGetValue(name, out var address) ? Read(address, tags) : default;

    private static TagValue Read(string? address, IReadOnlyDictionary<string, TagValue> tags)
    {
        if (string.IsNullOrWhiteSpace(address) || !tags.TryGetValue(address, out var value))
        {
            return default;
        }

        return value;
    }
}
