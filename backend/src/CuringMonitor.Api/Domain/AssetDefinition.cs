namespace CuringMonitor.Api.Domain;

/// <summary>
/// One box on the screen. Everything about it is data: what it is called, which group it
/// belongs to, where it sits in that group, whatever extra attributes the plant wants to
/// carry, and which process tag feeds each of its signals.
/// </summary>
public sealed class AssetDefinition
{
    /// <summary>Stable identifier, unique across the plant.</summary>
    public required string Id { get; init; }

    /// <summary>
    /// What kind of box this is. "press" is evaluated against the curing state rules;
    /// "gauge" simply shows its <c>value</c> signal.
    /// </summary>
    public string Kind { get; init; } = AssetKinds.Press;

    /// <summary>Text on the box. Defaults to the id, but nothing requires it to match.</summary>
    public string? Label { get; init; }

    /// <summary>Equipment group the box is drawn in — a bay, a line, a trench.</summary>
    public string? Group { get; init; }

    /// <summary>Order within the group.</summary>
    public int Position { get; init; }

    /// <summary>
    /// Free-form metadata carried straight through to the client, so a screen can label or
    /// filter boxes by anything the plant records — mould size, capacity, curing line.
    /// </summary>
    public Dictionary<string, string> Attributes { get; init; } = [];

    /// <summary>
    /// Signal name to process tag address. Names are the plant's own vocabulary; the status
    /// rules look for the well-known ones in <see cref="SignalNames"/> and anything else is
    /// simply published for a screen to bind to.
    /// </summary>
    public Dictionary<string, string> Signals { get; init; } = [];

    /// <summary>
    /// Value the running signal must reach for this item to count as running. Null uses the
    /// service-wide default, so only equipment that differs needs a figure in the file.
    /// </summary>
    public double? RunThreshold { get; init; }

    public string DisplayLabel => Label ?? Id;

    public string DisplayGroup => Group ?? "Plant";
}

public static class AssetKinds
{
    public const string Press = "press";
    public const string Gauge = "gauge";
}

/// <summary>Signal names the status rules understand. Any other name is published as-is.</summary>
public static class SignalNames
{
    public const string Pressure = "pressure";
    public const string Open = "open";
    public const string Fault = "fault";
    public const string Recipe = "recipe";
    public const string Value = "value";

    /// <summary>
    /// Work centre this item reports production against, used to join it to the MES
    /// production table. Carried as an attribute rather than a signal — it is identity, not
    /// a live value.
    /// </summary>
    public const string WorkCentreAttribute = "workCentre";

}
