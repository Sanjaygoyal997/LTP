using CuringMonitor.Api.Domain;

namespace CuringMonitor.Api.Contracts;

/// <summary>State of the whole plant at one instant — the payload the wall display renders.</summary>
public sealed record PlantSnapshot(
    DateTimeOffset Timestamp,
    ShiftName Shift,
    DateOnly ProductionDate,
    bool SourceConnected,
    ProductionTotals Production,
    PressTotals Totals,
    IReadOnlyList<PressSnapshot> Presses,
    IReadOnlyList<TrenchSnapshot> Trenches);

/// <summary>One press tile.</summary>
public sealed record PressSnapshot(
    string Id,
    string Title,
    int Trench,
    PressStatus Status,
    string? RecipeCode,
    int Count,
    double? Pressure,
    DateTimeOffset UpdatedAt);

/// <summary>Trench header pressure, in kg/cm².</summary>
public sealed record TrenchSnapshot(int Number, string Label, double? Pressure, bool IsHealthy);

/// <summary>Per-shift production counts, summed over every press.</summary>
public sealed record ProductionTotals(int A, int B, int C)
{
    public int Total => A + B + C;
}

/// <summary>Press counts by status, driving the "Total Curing Running / Stop" panel.</summary>
public sealed record PressTotals(int Running, int Stopped, int Alarm, int NoCommunication)
{
    public int Total => Running + Stopped + Alarm + NoCommunication;
}
