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
    /// Boxes per row for this group, when the plant configuration specifies a width.
    /// Null leaves the decision to the screen.
    /// </summary>
    public int? Wrap { get; init; }

    public string DisplayLabel => Label ?? Key;
}
