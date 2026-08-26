using CuringMonitor.Api.Configuration;
using CuringMonitor.Api.Domain;
using Microsoft.Extensions.Options;

namespace CuringMonitor.Api.Services;

public interface IShiftService
{
    Shift Current(DateTimeOffset at);

    /// <summary>When the given shift began — the lower bound for its production.</summary>
    DateTimeOffset StartOf(Shift shift);

    /// <summary>When the production day began, i.e. the start of that day's shift A.</summary>
    DateTimeOffset StartOfProductionDay(DateOnly productionDate);
}

/// <summary>
/// Resolves a timestamp to a shift and the production day it belongs to.
/// Shift C runs across midnight, so its post-midnight hours stay on the previous day.
/// </summary>
public sealed class ShiftService(IOptions<PlantOptions> options) : IShiftService
{
    private readonly ShiftOptions _shifts = options.Value.Shifts;

    public DateTimeOffset StartOf(Shift shift)
    {
        var hour = shift.Name switch
        {
            ShiftName.A => _shifts.AStartHour,
            ShiftName.B => _shifts.BStartHour,
            _ => _shifts.CStartHour
        };

        var day = shift.ProductionDate.ToDateTime(TimeOnly.MinValue).AddHours(hour);

        return new DateTimeOffset(day, TimeZoneInfo.Local.GetUtcOffset(day));
    }

    public DateTimeOffset StartOfProductionDay(DateOnly productionDate)
    {
        var day = productionDate.ToDateTime(TimeOnly.MinValue).AddHours(_shifts.AStartHour);

        return new DateTimeOffset(day, TimeZoneInfo.Local.GetUtcOffset(day));
    }

    public Shift Current(DateTimeOffset at)
    {
        // The start hours are wall-clock hours at the plant, and callers pass an instant —
        // UtcNow from the poller. Comparing that instant's UTC hour against them puts the
        // shift out by the machine's offset, so convert first: at 16:00 in UTC+05:30 the
        // untranslated hour is 10, which reads as shift A well into shift B.
        var local = at.ToLocalTime();
        var hour = local.Hour;

        if (hour >= _shifts.AStartHour && hour < _shifts.BStartHour)
        {
            return new Shift(ShiftName.A, DateOnly.FromDateTime(local.Date));
        }

        if (hour >= _shifts.BStartHour && hour < _shifts.CStartHour)
        {
            return new Shift(ShiftName.B, DateOnly.FromDateTime(local.Date));
        }

        // Shift C: from CStartHour to midnight is today's production day; after midnight,
        // up to AStartHour, it still belongs to yesterday.
        var productionDay = hour >= _shifts.CStartHour ? local.Date : local.Date.AddDays(-1);
        return new Shift(ShiftName.C, DateOnly.FromDateTime(productionDay));
    }
}
