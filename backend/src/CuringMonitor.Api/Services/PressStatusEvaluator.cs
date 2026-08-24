using CuringMonitor.Api.Configuration;
using CuringMonitor.Api.Contracts;
using CuringMonitor.Api.Domain;
using Microsoft.Extensions.Options;

namespace CuringMonitor.Api.Services;

/// <summary>
/// Turns a set of raw tag readings into the snapshot the display renders: one status,
/// recipe and production count per press, plus the plant-wide totals.
/// </summary>
public sealed class PressStatusEvaluator(IOptions<PlantOptions> options)
{
    private readonly PlantOptions _options = options.Value;

    /// <summary>Last time each press produced a good reading, used for the stale check.</summary>
    private readonly Dictionary<string, DateTimeOffset> _lastGoodReading = new(StringComparer.OrdinalIgnoreCase);

    public PlantSnapshot Evaluate(
        PlantConfiguration plant,
        IReadOnlyDictionary<string, TagValue> tags,
        Shift shift,
        bool sourceConnected,
        DateTimeOffset now)
    {
        var presses = new List<PressSnapshot>(plant.Presses.Count);
        int running = 0, stopped = 0, alarm = 0, noComm = 0;
        int productionA = 0, productionB = 0, productionC = 0;

        foreach (var definition in plant.Presses)
        {
            var press = EvaluatePress(definition, tags, shift, now);
            presses.Add(press);

            switch (press.Status)
            {
                case PressStatus.Running: running++; break;
                case PressStatus.Stopped: stopped++; break;
                case PressStatus.Alarm: alarm++; break;
                default: noComm++; break;
            }

            productionA += CounterValue(definition.Tags.ShiftCounterA, tags);
            productionB += CounterValue(definition.Tags.ShiftCounterB, tags);
            productionC += CounterValue(definition.Tags.ShiftCounterC, tags);
        }

        var trenches = plant.Trenches
            .Select(trench => EvaluateTrench(trench, tags))
            .ToArray();

        return new PlantSnapshot(
            now,
            shift.Name,
            shift.ProductionDate,
            sourceConnected,
            new ProductionTotals(productionA, productionB, productionC),
            new PressTotals(running, stopped, alarm, noComm),
            presses,
            trenches);
    }

    private PressSnapshot EvaluatePress(
        PressDefinition definition,
        IReadOnlyDictionary<string, TagValue> tags,
        Shift shift,
        DateTimeOffset now)
    {
        var pressure = Read(definition.Tags.InternalPressure, tags);
        var open = Read(definition.Tags.PressOpen, tags);
        var fault = Read(definition.Tags.PressFault, tags);

        var hasPressure = pressure.TryGetDouble(out var pressureValue);
        var communicating = hasPressure || open.IsGood || fault.IsGood;

        if (communicating)
        {
            _lastGoodReading[definition.Id] = now;
        }

        var status = ResolveStatus(definition.Id, communicating, hasPressure, pressureValue, open, fault, now);

        return new PressSnapshot(
            definition.Id,
            definition.DisplayName,
            definition.Trench,
            status,
            Read(definition.Tags.RecipeCode, tags).AsString(),
            CounterValue(definition.Tags.CounterFor(shift.Name), tags),
            hasPressure ? Math.Round(pressureValue, 1) : null,
            _lastGoodReading.TryGetValue(definition.Id, out var last) ? last : now);
    }

    /// <summary>
    /// Status precedence matches the legacy mimic: a press that is not talking is grey
    /// whatever its last values said, a fault outranks the open/closed state, and pressure
    /// only decides between run and stop once the press is known to be closed.
    /// </summary>
    private PressStatus ResolveStatus(
        string pressId,
        bool communicating,
        bool hasPressure,
        double pressureValue,
        TagValue open,
        TagValue fault,
        DateTimeOffset now)
    {
        if (!communicating && IsStale(pressId, now))
        {
            return PressStatus.NoCommunication;
        }

        if (fault.TryGetBoolean(out var faulted) && faulted)
        {
            return PressStatus.Alarm;
        }

        if (open.TryGetBoolean(out var isOpen) && isOpen)
        {
            return PressStatus.Stopped;
        }

        if (!hasPressure)
        {
            return PressStatus.Stopped;
        }

        return pressureValue >= _options.MinRunningPressure
            ? PressStatus.Running
            : PressStatus.Stopped;
    }

    private bool IsStale(string pressId, DateTimeOffset now) =>
        !_lastGoodReading.TryGetValue(pressId, out var last) || now - last > _options.StaleAfter;

    private static TrenchSnapshot EvaluateTrench(
        TrenchDefinition trench,
        IReadOnlyDictionary<string, TagValue> tags)
    {
        var reading = Read(trench.PressureTag, tags);
        var hasValue = reading.TryGetDouble(out var pressure);

        return new TrenchSnapshot(
            trench.Number,
            trench.DisplayName,
            hasValue ? Math.Round(pressure, 1) : null,
            hasValue);
    }

    private static int CounterValue(string? tag, IReadOnlyDictionary<string, TagValue> tags) =>
        Read(tag, tags).TryGetInt32(out var value) ? value : 0;

    private static TagValue Read(string? tag, IReadOnlyDictionary<string, TagValue> tags)
    {
        if (string.IsNullOrWhiteSpace(tag) || !tags.TryGetValue(tag, out var value))
        {
            return default;
        }

        return value;
    }
}
