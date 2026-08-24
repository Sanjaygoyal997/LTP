using CuringMonitor.Api.Configuration;
using CuringMonitor.Api.Domain;
using Microsoft.Extensions.Options;

namespace CuringMonitor.Api.Services;

public interface IShiftService
{
    Shift Current(DateTimeOffset at);
}

/// <summary>
/// Resolves a timestamp to a shift and the production day it belongs to.
/// Shift C runs across midnight, so its post-midnight hours stay on the previous day.
/// </summary>
public sealed class ShiftService(IOptions<PlantOptions> options) : IShiftService
{
    private readonly ShiftOptions _shifts = options.Value.Shifts;

    public Shift Current(DateTimeOffset at)
    {
        var hour = at.Hour;

        if (hour >= _shifts.AStartHour && hour < _shifts.BStartHour)
        {
            return new Shift(ShiftName.A, DateOnly.FromDateTime(at.Date));
        }

        if (hour >= _shifts.BStartHour && hour < _shifts.CStartHour)
        {
            return new Shift(ShiftName.B, DateOnly.FromDateTime(at.Date));
        }

        // Shift C: from CStartHour to midnight is today's production day; after midnight,
        // up to AStartHour, it still belongs to yesterday.
        var productionDay = hour >= _shifts.CStartHour ? at.Date : at.Date.AddDays(-1);
        return new Shift(ShiftName.C, DateOnly.FromDateTime(productionDay));
    }
}
