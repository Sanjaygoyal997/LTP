namespace CuringMonitor.Api.Domain;

/// <summary>
/// Status of a single curing press, in the order the wall display's legend lists them.
/// </summary>
public enum PressStatus
{
    /// <summary>The press is not reporting: bad tag quality, or no fresh value within the stale window.</summary>
    NoCommunication,

    /// <summary>Curing run / pressure ok.</summary>
    Running,

    /// <summary>Press open — curing stopped.</summary>
    Stopped,

    /// <summary>The press is reporting a fault.</summary>
    Alarm
}
