namespace CuringMonitor.Api.Configuration;

/// <summary>Runtime behaviour of the polling loop and the status rules.</summary>
public sealed class PlantOptions
{
    public const string SectionName = "Plant";

    /// <summary>Title shown on the wall display.</summary>
    public string Title { get; set; } = "Curing Press Status";

    /// <summary>
    /// Plant definition to load, absolute or relative to the content root. A '.txt' file is
    /// read as the plant's existing SCADA press configuration (config_AB.txt and friends);
    /// anything else is read as a layout file.
    /// </summary>
    public string LayoutFile { get; set; } = "plant-layout.json";

    /// <summary>
    /// Trench number to header-pressure tag. The legacy press configuration carries no
    /// trench pressure tag, so it is supplied here rather than by editing a file the plant
    /// still maintains for the old system. Trenches left out show as no-communication.
    /// </summary>
    public Dictionary<int, string> TrenchPressureTags { get; set; } = [];

    /// <summary>Directory holding the screen documents, absolute or relative to the content root.</summary>
    public string ScreensDirectory { get; set; } = "screens";

    /// <summary>
    /// Watch the screen directory and push changes to connected displays. On by default in
    /// development, where editing a screen and seeing it land is the point.
    /// </summary>
    public bool WatchScreens { get; set; }

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
