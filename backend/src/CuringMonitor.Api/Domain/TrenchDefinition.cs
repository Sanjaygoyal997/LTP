namespace CuringMonitor.Api.Domain;

/// <summary>
/// A trench (bay) on the shop floor: the tile rows drawn for it and, optionally, the
/// tag carrying its header pressure in kg/cm².
/// </summary>
public sealed class TrenchDefinition
{
    public required int Number { get; init; }

    public string? Label { get; init; }

    /// <summary>Trench header pressure tag, shown as the T-n tile at the end of the trench.</summary>
    public string? PressureTag { get; init; }

    /// <summary>
    /// Tile rows exactly as they should be drawn. Each entry is a press id, or a
    /// <c>trench:</c>-prefixed marker for the trench pressure tile.
    /// </summary>
    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }

    public string DisplayName => Label ?? $"Trench {Number}";
}
