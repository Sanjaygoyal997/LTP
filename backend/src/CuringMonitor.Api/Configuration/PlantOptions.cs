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
    /// anything else is read as an asset file.
    /// </summary>
    public string LayoutFile { get; set; } = "config_AB.txt";

    /// <summary>
    /// Group name to gauge tag, for gauge boxes whose source file carries no tag — the
    /// legacy press configuration has none. Supplying it here avoids editing a file the
    /// plant still maintains for the old system. Gauges left out show as no-communication.
    /// </summary>
    public Dictionary<string, string> GaugeTags { get; set; } = [];

    /// <summary>
    /// Width one box occupied on the legacy screen, in pixels. Only used to convert a trench
    /// panel width from <c>trenchSize.txt</c> into boxes per row; the mimic drew 40px buttons
    /// with a 3px margin either side.
    /// </summary>
    public int LegacyTilePitch { get; set; } = 46;

    /// <summary>Directory holding the screen documents, absolute or relative to the content root.</summary>
    public string ScreensDirectory { get; set; } = "screens";

    /// <summary>
    /// Watch the screen directory and push changes to connected displays, so saving an edit
    /// re-renders every wall panel.
    /// </summary>
    public bool WatchScreens { get; set; } = true;

    /// <summary>
    /// Watch the plant configuration file and reload it in place. On by default: presses
    /// are commissioned, renamed and moved, and that should reach the display without
    /// anyone restarting a service.
    /// </summary>
    public bool WatchConfiguration { get; set; } = true;

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
