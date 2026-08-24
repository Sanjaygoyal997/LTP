namespace CuringMonitor.Api.Contracts;

/// <summary>
/// Floor layout, served once at start-up so the client can build the tile grid before
/// the first snapshot arrives.
/// </summary>
public sealed record PlantLayout(string Title, IReadOnlyList<TrenchLayout> Trenches);

public sealed record TrenchLayout(int Number, string Label, IReadOnlyList<IReadOnlyList<LayoutCell>> Rows);

/// <summary>
/// One grid position. <see cref="Kind"/> is "press" for a curing press, "trench" for a
/// trench pressure tile, or "gap" for a deliberate blank.
/// </summary>
public sealed record LayoutCell(string Kind, string Id, string Label);
