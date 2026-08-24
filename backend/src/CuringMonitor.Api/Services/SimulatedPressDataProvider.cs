using System.Collections.Concurrent;
using CuringMonitor.Api.Domain;

namespace CuringMonitor.Api.Services;

/// <summary>
/// Drives the whole tag set from a local model of a curing cycle, so the display and the
/// API can be developed and demonstrated without the plant network.
/// </summary>
public sealed class SimulatedPressDataProvider : IPressDataProvider
{
    private static readonly string[] Recipes =
        ["140912_ULTIMA", "13575_XF", "15570_JETSTEEL", "9070_ULTIMA", "12080_XLM"];

    private readonly ConcurrentDictionary<string, PressState> _presses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Random _random = new(20260824);
    private readonly object _tickLock = new();

    public bool IsConnected => true;

    public Task<IReadOnlyDictionary<string, TagValue>> ReadAsync(
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        lock (_tickLock)
        {
            foreach (var press in _presses.Values)
            {
                press.Advance(_random, now);
            }
        }

        var values = new Dictionary<string, TagValue>(tags.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var tag in tags)
        {
            values[tag] = Evaluate(tag, now);
        }

        return Task.FromResult<IReadOnlyDictionary<string, TagValue>>(values);
    }

    /// <summary>
    /// Derives a value from the tag's own address, so the simulator needs no knowledge of
    /// the layout: the press key is the second dot-separated segment of the address.
    /// </summary>
    private TagValue Evaluate(string tag, DateTimeOffset now)
    {
        var segments = tag.Split('.');
        var key = segments.Length > 1 ? segments[1] : tag;
        var state = _presses.GetOrAdd(key, k => PressState.Seed(k, _random, now));

        if (state.Offline)
        {
            return TagValue.Bad(now);
        }

        if (tag.EndsWith("internal_pressure", StringComparison.OrdinalIgnoreCase) ||
            tag.EndsWith("pressure", StringComparison.OrdinalIgnoreCase))
        {
            return new TagValue(state.Pressure, true, now);
        }

        if (tag.EndsWith("Press_Open", StringComparison.OrdinalIgnoreCase))
        {
            return new TagValue(state.IsOpen, true, now);
        }

        if (tag.EndsWith("Press_Fault", StringComparison.OrdinalIgnoreCase))
        {
            return new TagValue(state.HasFault, true, now);
        }

        if (tag.EndsWith("RecipeCode", StringComparison.OrdinalIgnoreCase))
        {
            return new TagValue(state.Recipe, true, now);
        }

        if (tag.EndsWith("FIRST_SHIFT_COUNTER", StringComparison.OrdinalIgnoreCase))
        {
            return new TagValue(state.CounterA, true, now);
        }

        if (tag.EndsWith("SECOND_SHIFT_COUNTER", StringComparison.OrdinalIgnoreCase))
        {
            return new TagValue(state.CounterB, true, now);
        }

        if (tag.EndsWith("THIRD_SHIFT_COUNTER", StringComparison.OrdinalIgnoreCase))
        {
            return new TagValue(state.CounterC, true, now);
        }

        return TagValue.Bad(now);
    }

    private sealed class PressState
    {
        private DateTimeOffset _nextChange;

        public required string Recipe { get; set; }

        public bool IsOpen { get; private set; }

        public bool HasFault { get; private set; }

        public bool Offline { get; private set; }

        public double Pressure { get; private set; }

        public int CounterA { get; private set; }

        public int CounterB { get; private set; }

        public int CounterC { get; private set; }

        public static PressState Seed(string key, Random random, DateTimeOffset now)
        {
            lock (random)
            {
                var state = new PressState
                {
                    Recipe = Recipes[Math.Abs(key.GetHashCode(StringComparison.Ordinal)) % Recipes.Length],
                    CounterA = random.Next(4, 28),
                    CounterB = random.Next(4, 28),
                    CounterC = random.Next(4, 28)
                };
                state.Close(random, now);
                return state;
            }
        }

        public void Advance(Random random, DateTimeOffset now)
        {
            if (now < _nextChange)
            {
                if (!IsOpen && !Offline && !HasFault)
                {
                    // Small wander so the pressure reading looks alive.
                    Pressure = Math.Clamp(Pressure + (random.NextDouble() - 0.5) * 0.2, 14, 20);
                }

                return;
            }

            if (Offline || HasFault)
            {
                Offline = false;
                HasFault = false;
                Close(random, now);
                return;
            }

            if (IsOpen)
            {
                Close(random, now);
                return;
            }

            // A finished cure books a tyre, then the press opens for unload.
            CounterA++;
            IsOpen = true;
            Pressure = 0;
            _nextChange = now.AddSeconds(random.Next(20, 60));

            var roll = random.NextDouble();
            if (roll > 0.97)
            {
                HasFault = true;
            }
            else if (roll > 0.94)
            {
                Offline = true;
            }
        }

        private void Close(Random random, DateTimeOffset now)
        {
            IsOpen = false;
            Pressure = 15 + random.NextDouble() * 4;
            _nextChange = now.AddSeconds(random.Next(90, 240));
        }
    }
}
