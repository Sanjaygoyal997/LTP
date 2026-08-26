using System.Data;
using CuringMonitor.Api.Configuration;
using CuringMonitor.Api.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace CuringMonitor.Api.Services.Production;

/// <summary>
/// Counts cures from the MES production table.
///
/// Refreshed on its own slower cadence and cached: a cure takes minutes, so querying it at
/// the tag poll rate would load the database for nothing. A failed query leaves the last
/// good counts in place and marks them unavailable rather than showing zeros, which on a
/// wall display would read as "nothing has been produced".
/// </summary>
public sealed class SqlProductionSource(
    IOptions<PlantOptions> options,
    IShiftService shifts,
    ILogger<SqlProductionSource> logger) : IProductionSource
{
    private readonly ProductionOptions _options = options.Value.Production;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private ProductionCounts _cached = ProductionCounts.Empty;
    private DateTimeOffset _refreshedAt = DateTimeOffset.MinValue;

    public async Task<ProductionCounts> GetAsync(Shift shift, CancellationToken cancellationToken)
    {
        if (DateTimeOffset.UtcNow - _refreshedAt < _options.RefreshInterval)
        {
            return _cached;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Another caller may have refreshed while this one waited.
            if (DateTimeOffset.UtcNow - _refreshedAt < _options.RefreshInterval)
            {
                return _cached;
            }

            var shiftStart = shifts.StartOf(shift);
            var dayStart = shifts.StartOfProductionDay(shift.ProductionDate);

            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // Per item, the running shift only: this is what each box shows.
            var byEquipment = await QueryAsync(connection, _options.ByEquipmentQuery, shiftStart, cancellationToken)
                .ConfigureAwait(false);

            // Per shift, the whole production day: this fills the three shift boxes, so it
            // has to reach back past the running shift's start.
            var byShift = await QueryAsync(connection, _options.ByShiftQuery, dayStart, cancellationToken)
                .ConfigureAwait(false);

            _cached = new ProductionCounts(byEquipment, NormaliseShiftKeys(byShift), true);
            _refreshedAt = DateTimeOffset.UtcNow;

            return _cached;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Production query failed; keeping the last counts.");

            // Hold the previous numbers but flag them, so a stale figure is not mistaken for
            // a live one.
            _cached = _cached with { IsAvailable = false };
            _refreshedAt = DateTimeOffset.UtcNow;

            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Rewrites the shift keys the query returned into A, B and C, so the three shift boxes
    /// find their figures whether the MES records the shift as a letter or as a number.
    /// </summary>
    private Dictionary<string, int> NormaliseShiftKeys(Dictionary<string, int> counts)
    {
        var mapped = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in counts)
        {
            var name = _options.ShiftKeys.TryGetValue(key, out var mappedKey) ? mappedKey : key;
            mapped[name] = mapped.GetValueOrDefault(name) + value;
        }

        return mapped;
    }

    private async Task<Dictionary<string, int>> QueryAsync(
        SqlConnection connection,
        string sql,
        DateTimeOffset from,
        CancellationToken cancellationToken)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@from", SqlDbType.DateTime2).Value = from.LocalDateTime;

        // Only bound when the site configured it: a query rewritten to select its equipment
        // some other way should not have to carry a parameter it never mentions.
        if (_options.ProcessId is { } processId)
        {
            command.Parameters.Add("@processId", SqlDbType.Int).Value = processId;
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (await reader.IsDBNullAsync(0, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            var key = reader.GetValue(0).ToString()!.Trim();
            var value = await reader.IsDBNullAsync(1, cancellationToken).ConfigureAwait(false)
                ? 0
                : Convert.ToInt32(reader.GetValue(1));

            counts[key] = value;
        }

        return counts;
    }
}
