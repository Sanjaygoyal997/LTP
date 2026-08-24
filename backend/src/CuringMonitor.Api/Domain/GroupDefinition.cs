namespace CuringMonitor.Api.Domain;

/// <summary>
/// A group of boxes — a trench, a bay, a line. Carries the order the plant lists it in and,
/// where the source configuration says so, how many boxes fit across one row of it.
/// </summary>
public sealed class GroupDefinition
{
    public required string Key { get; init; }

    public string? Label { get; init; }

    /// <summary>Position in the source configuration; groups are drawn in this order.</summary>
    public int Order { get; init; }

    /// <summary>
    /// Panel width the legacy screen drew this trench in, from <c>trenchSize.txt</c>.
    /// Boxes were fitted into the panel rather than wrapped at a fixed count, so the client
    /// derives box size and row width from this and the box count.
    /// </summary>
    public int? PanelWidth { get; init; }

    /// <summary>Panel height, from the same file.</summary>
    public int? PanelHeight { get; init; }

    public string DisplayLabel => Label ?? Key;
}
