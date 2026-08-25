using CuringMonitor.Api.Domain;

namespace CuringMonitor.Api.Services.Production;

/// <summary>
/// Cures booked in the MES, which is where production is recorded — the PLC counters are
/// not used.
/// </summary>
/// <param name="ByWorkCentre">Cures in the current shift, keyed by work centre id.</param>
/// <param name="ByShift">Cures per shift across the whole production day.</param>
/// <param name="IsAvailable">False when the query failed, so the display can say so.</param>
public sealed record ProductionCounts(
    IReadOnlyDictionary<string, int> ByWorkCentre,
    IReadOnlyDictionary<string, int> ByShift,
    bool IsAvailable)
{
    public static readonly ProductionCounts Empty = new(
        new Dictionary<string, int>(),
        new Dictionary<string, int>(),
        false);

    public int For(string? workCentre) =>
        workCentre is not null && ByWorkCentre.TryGetValue(workCentre, out var count) ? count : 0;

    public int ForShift(ShiftName shift) =>
        ByShift.TryGetValue(shift.ToString(), out var count) ? count : 0;
}

public interface IProductionSource
{
    /// <summary>
    /// Counts for the given shift. Implementations cache: production is queried far less
    /// often than tags are polled, because a cure takes minutes and a database is not a PLC.
    /// </summary>
    Task<ProductionCounts> GetAsync(Shift shift, CancellationToken cancellationToken);
}
