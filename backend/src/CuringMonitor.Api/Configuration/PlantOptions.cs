namespace CuringMonitor.Api.Configuration;

/// <summary>Runtime behaviour of the polling loop and the status rules.</summary>
public sealed class PlantOptions
{
    public const string SectionName = "Plant";

    /// <summary>Title shown on the wall display.</summary>
    public string Title { get; set; } = "Curing Press Status";

    /// <summary>Path to the layout/tag definition file, absolute or relative to the content root.</summary>
    public string LayoutFile { get; set; } = "plant-layout.json";

    /// <summary>How often the whole tag set is read.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// A press with no good reading for longer than this drops to "no communication".
    /// The legacy screen used ten five-second polls; keep it comfortably above
    /// <see cref="PollInterval"/> so a single missed read does not flip the tile.
    /// </summary>
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Internal pressure at or above which a closed press counts as "pressure ok".</summary>
    public double MinRunningPressure { get; set; } = 1.0;

    /// <summary>Which data provider to use: "simulated" or "opc".</summary>
    public string Provider { get; set; } = "simulated";

    public ShiftOptions Shifts { get; set; } = new();

    public OpcOptions Opc { get; set; } = new();
}

/// <summary>
/// Shift boundaries, as the hour each shift starts. Shift C spans midnight; hours before
/// <see cref="AStartHour"/> are booked against the previous production day.
/// </summary>
public sealed class ShiftOptions
{
    public int AStartHour { get; set; } = 7;

    public int BStartHour { get; set; } = 15;

    public int CStartHour { get; set; } = 23;
}

public sealed class OpcOptions
{
    /// <summary>OPC server identity, e.g. "Kepware.KEPServerEX.V6" or an OPC UA endpoint URL.</summary>
    public string ServerName { get; set; } = "Kepware.KEPServerEX.V6";

    /// <summary>Server-side update rate requested for the subscription group.</summary>
    public TimeSpan UpdateRate { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Reconnect back-off after a dropped session.</summary>
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);
}
