namespace CuringMonitor.Api.Domain;

public enum ShiftName
{
    A,
    B,
    C
}

/// <summary>
/// A shift together with the production day it belongs to. Shift C spans midnight,
/// so its early hours are still booked against the previous calendar day.
/// </summary>
/// <param name="Name">Shift the timestamp falls in.</param>
/// <param name="ProductionDate">Calendar day production is booked against.</param>
public readonly record struct Shift(ShiftName Name, DateOnly ProductionDate);
