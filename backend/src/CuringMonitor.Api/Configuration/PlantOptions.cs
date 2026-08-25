using CuringMonitor.Api.Domain;

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

    /// <summary>Which data provider to use: "simulated" or "opc".</summary>
    public string Provider { get; set; } = "simulated";

    public ShiftOptions Shifts { get; set; } = new();

    /// <summary>
    /// Value the communication-check signal must reach for equipment to count as running,
    /// where the configuration gives no per-item threshold.
    /// </summary>
    public double RunThreshold { get; set; } = 1.0;

    /// <summary>
    /// How often a healthy service says so. Health changes are logged as they happen; this
    /// is the heartbeat in between, so silence in the log means the service is gone rather
    /// than merely quiet.
    /// </summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How an equipment group is labelled, given the group number from the configuration.
    /// Sites call these bays, lines or trenches; the screen should use the plant's word.
    /// </summary>
    public string GroupLabelFormat { get; set; } = "Group {0}";

    public OpcOptions Opc { get; set; } = new();

    public ProductionOptions Production { get; set; } = new();

    public SignalOptions Signals { get; set; } = new();
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

/// <summary>
/// Where cures are counted. Production is booked in the MES, not read from the PLC, so it
/// comes from the database rather than from a tag.
/// </summary>
/// <summary>
/// Which signal each rule reads. The names are the plant's own — this only says which of
/// them carries which meaning, so equipment judged on temperature or weight rather than
/// pressure needs a setting rather than a code change.
/// </summary>
public sealed class SignalOptions
{
    /// <summary>Signal that decides communication and run/stop. Overridable per item.</summary>
    public string RunCheck { get; set; } = SignalNames.RunCheck;

    /// <summary>Signal that raises the alarm flag.</summary>
    public string Alarm { get; set; } = SignalNames.Alarm;

    /// <summary>Signal shown on the box's middle line.</summary>
    public string Recipe { get; set; } = SignalNames.Recipe;
}

public sealed class ProductionOptions
{
    /// <summary>"sql" or "simulated".</summary>
    public string Provider { get; set; } = "simulated";

    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// How often production is re-queried. Far slower than the tag poll: a cure takes
    /// minutes, and this is a database rather than a PLC.
    /// </summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Process the equipment belongs to in the work-centre master. Curing is 2.</summary>
    public int ProcessId { get; set; } = 2;

    /// <summary>
    /// Asset attribute the first column of <see cref="ByEquipmentQuery"/> is matched against.
    /// The default joins on the equipment name, which the configuration already carries, so
    /// no work-centre id has to be maintained twice.
    /// </summary>
    public string MatchAttribute { get; set; } = "name";

    /// <summary>
    /// Cures in the current shift per item. Must return (key, count), where key matches
    /// <see cref="MatchAttribute"/> — the master table resolves the production table's
    /// work-centre id to the equipment name.
    /// </summary>
    public string ByEquipmentQuery { get; set; } =
        "SELECT m.name, SUM(p.quantity) " +
        "FROM dbo.CuringProduction p " +
        "INNER JOIN dbo.wcMaster m ON m.iD = p.wcID " +
        "WHERE p.dtandTime >= @from AND m.processID = @processId " +
        "GROUP BY m.name";

    /// <summary>Cures per shift across the production day. Must return (shift, count).</summary>
    public string ByShiftQuery { get; set; } =
        "SELECT p.shift, SUM(p.quantity) " +
        "FROM dbo.CuringProduction p " +
        "INNER JOIN dbo.wcMaster m ON m.iD = p.wcID " +
        "WHERE p.dtandTime >= @from AND m.processID = @processId " +
        "GROUP BY p.shift";
}

public sealed class OpcOptions
{
    /// <summary>Server ProgID, as the plant's other services use it.</summary>
    public string ServerName { get; set; } = "Kepware.KEPServerEX.V6";

    /// <summary>Machine hosting the server; empty for the local machine.</summary>
    public string Node { get; set; } = string.Empty;

    /// <summary>Name of the OPC group this service creates.</summary>
    public string GroupName { get; set; } = "CuringMonitor";

    /// <summary>Server-side update rate requested for the group.</summary>
    public TimeSpan UpdateRate { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Reconnect back-off after a dropped session.</summary>
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Tags added per AddItems call. One call for a plant-sized list is a long round trip.</summary>
    public int AddItemsBatchSize { get; set; } = 500;

    /// <summary>Tags read per SyncRead call.</summary>
    public int ReadBatchSize { get; set; } = 500;
}
