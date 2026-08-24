using CuringMonitor.Api.Domain;

namespace CuringMonitor.Api.Contracts;

/// <summary>
/// State of the whole plant at one instant — the payload the display renders.
/// </summary>
/// <param name="Shift">Shift letter: "A", "B" or "C".</param>
public sealed record PlantSnapshot(
    DateTimeOffset Timestamp,
    string Shift,
    DateOnly ProductionDate,
    bool SourceConnected,
    ProductionTotals Production,
    PressTotals Totals,
    IReadOnlyList<AssetSnapshot> Assets,
    IReadOnlyList<GroupSnapshot> Groups);

/// <summary>
/// A group of boxes. The client draws groups in <paramref name="Order"/> and, when
/// <paramref name="Wrap"/> is set, wraps that group's boxes at that many per row.
/// </summary>
public sealed record GroupSnapshot(string Key, string Label, int Order, int? Wrap);

/// <summary>
/// One box. The client draws it entirely from this: which group it belongs to and where it
/// sits, what it is called, its state, and every signal value it carries.
/// </summary>
public sealed record AssetSnapshot(
    string Id,
    string Kind,
    string Label,
    string Group,
    int Position,
    PressStatus Status,
    IReadOnlyDictionary<string, string> Attributes,
    IReadOnlyDictionary<string, object?> Signals,
    DateTimeOffset UpdatedAt);

/// <summary>Per-shift production counts, summed over every press.</summary>
public sealed record ProductionTotals(int A, int B, int C)
{
    public int Total => A + B + C;
}

/// <summary>Press counts by status, driving the running/stopped panel.</summary>
public sealed record PressTotals(int Running, int Stopped, int Alarm, int NoCommunication)
{
    public int Total => Running + Stopped + Alarm + NoCommunication;
}
