namespace CuringMonitor.Api.Domain;

/// <summary>
/// A group of equipment — a bay, a line, a trench. Carries the order the plant lists it in
/// and, where the source configuration says so, the panel it is drawn in.
/// </summary>
public sealed class GroupDefinition
{
    public required string Key { get; init; }

    public string? Label { get; init; }

    /// <summary>Position in the source configuration; groups are drawn in this order.</summary>
    public int Order { get; init; }

    /// <summary>
    /// Panel width the legacy screen drew this group in, from its panel geometry file.
    /// Boxes were fitted into the panel rather than wrapped at a fixed count, so the client
    /// derives box size and row width from this and the box count.
    /// </summary>
    public int? PanelWidth { get; init; }

    /// <summary>Panel height, from the same file.</summary>
    public int? PanelHeight { get; init; }

    public string DisplayLabel => Label ?? Key;
}
