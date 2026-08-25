using CuringMonitor.Api.Domain;

namespace CuringMonitor.Api.Services.Production;

/// <summary>
/// Plausible production for development, so the display can be worked on without the MES.
/// Counts climb through the shift rather than jumping about, which is what makes a wrong
/// total obvious when the real source is connected.
/// </summary>
public sealed class SimulatedProductionSource(
    IShiftService shifts,
    Configuration.PlantConfigurationProvider plant) : IProductionSource
{
    private readonly Dictionary<string, int> _seeds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Random _random = new(20260825);

    private IEnumerable<string> _names =>
        plant.Current.Assets
            .Select(a => a.Attributes.GetValueOrDefault("name") ?? a.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase);

    public Task<ProductionCounts> GetAsync(Shift shift, CancellationToken cancellationToken)
    {
        var elapsed = DateTimeOffset.UtcNow - shifts.StartOf(shift);
        var progress = Math.Clamp(elapsed.TotalHours / 8.0, 0, 1);

        // Keyed the way the real query is: by equipment name.
        var byEquipment = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        lock (_seeds)
        {
            foreach (var name in _names)
            {
                if (!_seeds.TryGetValue(name, out var rate))
                {
                    rate = _random.Next(18, 32);
                    _seeds[name] = rate;
                }

                byEquipment[name] = (int)Math.Round(rate * progress);
            }
        }

        var current = byEquipment.Values.Sum();
        var byShift = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [ShiftName.A.ToString()] = shift.Name == ShiftName.A ? current : 1900,
            [ShiftName.B.ToString()] = shift.Name == ShiftName.B ? current : 1850,
            [ShiftName.C.ToString()] = shift.Name == ShiftName.C ? current : 0
        };

        return Task.FromResult(new ProductionCounts(byEquipment, byShift, true));
    }
}
