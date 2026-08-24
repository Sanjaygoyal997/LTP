namespace CuringMonitor.Api.Domain;

/// <summary>
/// Static definition of one curing press: which trench it sits in and which process
/// tags describe it. Mirrors one line of the legacy config file.
/// </summary>
public sealed class PressDefinition
{
    public required string Id { get; init; }

    /// <summary>Text drawn in the tile header; defaults to <see cref="Id"/>.</summary>
    public string? Title { get; init; }

    /// <summary>Trench (bay) this press belongs to.</summary>
    public required int Trench { get; init; }

    public required PressTags Tags { get; init; }

    public string DisplayName => Title ?? Id;
}

/// <summary>
/// Process tag addresses for one press. Every tag is optional except the three that
/// drive the tile colour, so a partially instrumented press still renders.
/// </summary>
public sealed class PressTags
{
    /// <summary>Internal pressure — drives "pressure ok" and doubles as the communication check.</summary>
    public required string InternalPressure { get; init; }

    /// <summary>Press open / closed — an open press is curing-stopped.</summary>
    public required string PressOpen { get; init; }

    /// <summary>Press fault — raises the alarm state.</summary>
    public required string PressFault { get; init; }

    /// <summary>Recipe currently loaded; shared across the presses of a group.</summary>
    public string? RecipeCode { get; init; }

    /// <summary>Production counter for shift A.</summary>
    public string? ShiftCounterA { get; init; }

    /// <summary>Production counter for shift B.</summary>
    public string? ShiftCounterB { get; init; }

    /// <summary>Production counter for shift C.</summary>
    public string? ShiftCounterC { get; init; }

    public string? CounterFor(ShiftName shift) => shift switch
    {
        ShiftName.A => ShiftCounterA,
        ShiftName.B => ShiftCounterB,
        ShiftName.C => ShiftCounterC,
        _ => null
    };

    public IEnumerable<string> All()
    {
        yield return InternalPressure;
        yield return PressOpen;
        yield return PressFault;

        foreach (var tag in new[] { RecipeCode, ShiftCounterA, ShiftCounterB, ShiftCounterC })
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                yield return tag;
            }
        }
    }
}
