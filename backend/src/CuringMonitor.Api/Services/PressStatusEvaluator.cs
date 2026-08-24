using CuringMonitor.Api.Configuration;
using CuringMonitor.Api.Contracts;
using CuringMonitor.Api.Domain;
using Microsoft.Extensions.Options;

namespace CuringMonitor.Api.Services;

/// <summary>
/// Turns raw tag readings into the snapshot the display renders: a state and a set of
/// signal values per box, plus the plant-wide totals.
/// </summary>
public sealed class PressStatusEvaluator(IOptions<PlantOptions> options)
{
    private readonly PlantOptions _options = options.Value;

    /// <summary>Last time each asset produced a good reading, used for the stale check.</summary>
    private readonly Dictionary<string, DateTimeOffset> _lastGoodReading = new(StringComparer.OrdinalIgnoreCase);

    public PlantSnapshot Evaluate(
        PlantConfiguration plant,
        IReadOnlyDictionary<string, TagValue> tags,
        Shift shift,
        bool sourceConnected,
        DateTimeOffset now)
    {
        var assets = new List<AssetSnapshot>(plant.Assets.Count);
        int running = 0, stopped = 0, alarm = 0, noComm = 0;
        int productionA = 0, productionB = 0, productionC = 0;

        foreach (var definition in plant.Assets)
        {
            var asset = EvaluateAsset(definition, tags, shift, now);
            assets.Add(asset);

            // Only presses count towards the press totals; a gauge is not a machine.
            if (!definition.Kind.Equals(AssetKinds.Press, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            switch (asset.Status)
            {
                case PressStatus.Running: running++; break;
                case PressStatus.Stopped: stopped++; break;
                case PressStatus.Alarm: alarm++; break;
                default: noComm++; break;
            }

            productionA += CounterValue(definition, ShiftName.A, tags);
            productionB += CounterValue(definition, ShiftName.B, tags);
            productionC += CounterValue(definition, ShiftName.C, tags);
        }

        // A reload can remove boxes; drop their staleness entries so the map tracks the
        // plant rather than everything the service has ever seen.
        if (_lastGoodReading.Count > plant.Assets.Count)
        {
            var live = plant.Assets.Select(a => a.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var id in _lastGoodReading.Keys.Where(id => !live.Contains(id)).ToArray())
            {
                _lastGoodReading.Remove(id);
            }
        }

        return new PlantSnapshot(
            now,
            shift.Name.ToString(),
            shift.ProductionDate,
            sourceConnected,
            new ProductionTotals(productionA, productionB, productionC),
            new PressTotals(running, stopped, alarm, noComm),
            assets);
    }

    private AssetSnapshot EvaluateAsset(
        AssetDefinition definition,
        IReadOnlyDictionary<string, TagValue> tags,
        Shift shift,
        DateTimeOffset now)
    {
        // Every configured signal is published, whether or not the status rules use it, so a
        // screen can bind to anything the plant chose to wire up.
        var signals = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var communicating = false;

        foreach (var (name, address) in definition.Signals)
        {
            var reading = Read(address, tags);
            signals[name] = reading.IsGood ? reading.Value : null;
            communicating |= reading.IsGood;
        }

        // The current shift's counter is also published under a stable name, so a screen
        // does not have to know which shift is running.
        if (definition.Signals.ContainsKey(SignalNames.Counter(shift.Name)))
        {
            signals["count"] = signals[SignalNames.Counter(shift.Name)] ?? 0;
        }

        if (communicating)
        {
            _lastGoodReading[definition.Id] = now;
        }

        var status = definition.Kind.Equals(AssetKinds.Press, StringComparison.OrdinalIgnoreCase)
            ? PressState(definition, tags, communicating, now)
            : GaugeState(definition, communicating, now);

        return new AssetSnapshot(
            definition.Id,
            definition.Kind,
            definition.DisplayLabel,
            definition.DisplayGroup,
            definition.Position,
            status,
            definition.Attributes,
            signals,
            _lastGoodReading.TryGetValue(definition.Id, out var last) ? last : now);
    }

    /// <summary>
    /// Status precedence matches the mimic this replaces: a press that is not talking is
    /// grey whatever its last values said, a fault outranks the open/closed state, and
    /// pressure only decides between run and stop once the press is known to be closed.
    /// </summary>
    private PressStatus PressState(
        AssetDefinition definition,
        IReadOnlyDictionary<string, TagValue> tags,
        bool communicating,
        DateTimeOffset now)
    {
        if (!communicating && IsStale(definition.Id, now))
        {
            return PressStatus.NoCommunication;
        }

        if (Signal(definition, SignalNames.Fault, tags).TryGetBoolean(out var faulted) && faulted)
        {
            return PressStatus.Alarm;
        }

        if (Signal(definition, SignalNames.Open, tags).TryGetBoolean(out var isOpen) && isOpen)
        {
            return PressStatus.Stopped;
        }

        if (!Signal(definition, SignalNames.Pressure, tags).TryGetDouble(out var pressure))
        {
            return PressStatus.Stopped;
        }

        return pressure >= _options.MinRunningPressure
            ? PressStatus.Running
            : PressStatus.Stopped;
    }

    private PressStatus GaugeState(AssetDefinition definition, bool communicating, DateTimeOffset now) =>
        communicating || !IsStale(definition.Id, now)
            ? PressStatus.Running
            : PressStatus.NoCommunication;

    private bool IsStale(string assetId, DateTimeOffset now) =>
        !_lastGoodReading.TryGetValue(assetId, out var last) || now - last > _options.StaleAfter;

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
