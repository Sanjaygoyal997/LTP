using System.Globalization;
using CuringMonitor.Api.Configuration;
using CuringMonitor.Api.Contracts;
using CuringMonitor.Api.Domain;
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

    /// <summary>
    /// Values the legacy screen treats as true. It compares the tag's text rather than its
    /// type, and OPC servers surface booleans variously as 1, -1 or True.
    /// </summary>
    private static readonly HashSet<string> TruthyValues =
        new(["1", "-1", "true"], StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> FalsyValues =
        new(["0", "false"], StringComparer.OrdinalIgnoreCase);

    public PlantSnapshot Evaluate(
        PlantConfiguration plant,
        IReadOnlyDictionary<string, TagValue> tags,
        Shift shift,
        bool sourceConnected,
        DateTimeOffset now)
    {
        var assets = new List<AssetSnapshot>(plant.Assets.Count);
        int running = 0, stopped = 0, alarms = 0, noComm = 0;
        int productionA = 0, productionB = 0, productionC = 0;

        foreach (var definition in plant.Assets)
        {
            var asset = EvaluateAsset(definition, tags, shift, now);
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

            productionA += CounterValue(definition, ShiftName.A, tags);
            productionB += CounterValue(definition, ShiftName.B, tags);
            productionC += CounterValue(definition, ShiftName.C, tags);
        }

        return new PlantSnapshot(
            now,
            shift.Name.ToString(),
            shift.ProductionDate,
            sourceConnected,
            new ProductionTotals(productionA, productionB, productionC),
            new PressTotals(running, stopped, alarms, noComm),
            assets,
            plant.Groups
                .Select(g => new GroupSnapshot(g.Key, g.DisplayLabel, g.Order, g.PanelWidth, g.PanelHeight))
                .ToArray());
    }

    private AssetSnapshot EvaluateAsset(
        AssetDefinition definition,
        IReadOnlyDictionary<string, TagValue> tags,
        Shift shift,
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

        // The running shift's counter is also published under a stable name, so a screen does
        // not have to know which shift it is.
        if (definition.Signals.ContainsKey(SignalNames.Counter(shift.Name)))
        {
            signals["count"] = signals[SignalNames.Counter(shift.Name)] ?? 0;
        }

        return new AssetSnapshot(
            definition.Id,
            definition.Kind,
            definition.DisplayLabel,
            definition.DisplayGroup,
            definition.Position,
            BandState(definition, tags),
            IsTrue(Signal(definition, SignalNames.Fault, tags)),
            definition.Attributes,
            signals,
            now);
    }

    /// <summary>
    /// The band colour, in the legacy screen's own order of precedence.
    ///
    /// A press that is not reporting its pressure tag at all is grey whatever else is known
    /// about it. Otherwise the press-open signal decides — and anything that is neither
    /// clearly open nor clearly closed is grey rather than guessed at.
    /// </summary>
    private PressStatus BandState(AssetDefinition definition, IReadOnlyDictionary<string, TagValue> tags)
    {
        var pressure = Signal(definition, SignalNames.Pressure, tags);
        if (!pressure.IsGood || pressure.Value is null)
        {
            return PressStatus.NoCommunication;
        }

        if (_options.RunStop.UsesPressure)
        {
            return pressure.TryGetDouble(out var value) && value >= _options.RunStop.Threshold
                ? PressStatus.Running
                : PressStatus.Stopped;
        }

        var open = Signal(definition, SignalNames.Open, tags);
        if (IsTrue(open))
        {
            return PressStatus.Stopped;
        }

        return IsFalse(open) ? PressStatus.Running : PressStatus.NoCommunication;
    }

    private static bool IsTrue(TagValue tag) => Text(tag) is { } text && TruthyValues.Contains(text);

    private static bool IsFalse(TagValue tag) => Text(tag) is { } text && FalsyValues.Contains(text);

    private static string? Text(TagValue tag) =>
        tag.IsGood && tag.Value is not null
            ? Convert.ToString(tag.Value, CultureInfo.InvariantCulture)?.Trim()
            : null;

    private static int CounterValue(
        AssetDefinition definition,
        ShiftName shift,
        IReadOnlyDictionary<string, TagValue> tags) =>
        Signal(definition, SignalNames.Counter(shift), tags).TryGetInt32(out var value) ? value : 0;

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
