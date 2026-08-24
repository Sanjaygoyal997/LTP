using System.Text;
using System.Text.Json;
using CuringMonitor.Api.Domain;

namespace CuringMonitor.Api.Configuration;

/// <summary>
/// Converts the legacy SCADA press configuration (<c>config_AB.txt</c> and friends) into
/// the layout file this service reads, so the shop floor's existing tag map stays the
/// single source of truth.
/// </summary>
/// <remarks>
/// The legacy format is one press per line, '#'-separated:
/// <code>
/// RowNo#PressName#PressTitle#CommunicationCheck#PressOpen_Close#Alarm#RecipeCode#ProdCountA#ProdCountB#ProdCountC#Flag
/// </code>
/// <c>RowNo</c> is the trench number. The first line is a header and is skipped.
/// </remarks>
public static class LegacyConfigImporter
{
    private const int TilesPerRow = 16;

    public static string Convert(string legacyConfigPath, string title)
    {
        var presses = new List<PressDefinition>();
        var order = new List<(int Trench, string PressId)>();

        foreach (var line in File.ReadLines(legacyConfigPath, Encoding.UTF8).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = line.Split('#');
            if (columns.Length < 10)
            {
                throw new InvalidOperationException($"Malformed press line: {line}");
            }

            if (!int.TryParse(columns[0], out var trench))
            {
                throw new InvalidOperationException($"Unreadable trench number in line: {line}");
            }

            var id = columns[1].Trim();
            presses.Add(new PressDefinition
            {
                Id = id,
                Title = string.IsNullOrWhiteSpace(columns[2]) ? id : columns[2].Trim(),
                Trench = trench,
                Tags = new PressTags
                {
                    InternalPressure = columns[3].Trim(),
                    PressOpen = columns[4].Trim(),
                    PressFault = columns[5].Trim(),
                    RecipeCode = Nullable(columns[6]),
                    ShiftCounterA = Nullable(columns[7]),
                    ShiftCounterB = Nullable(columns[8]),
                    ShiftCounterC = Nullable(columns[9])
                }
            });

            order.Add((trench, id));
        }

        var trenches = order
            .GroupBy(entry => entry.Trench)
            .OrderByDescending(group => group.Key)
            .Select(group => BuildTrench(group.Key, group.Select(entry => entry.PressId).ToList()))
            .ToList();

        var document = new
        {
            title,
            trenches,
            presses
        };

        return JsonSerializer.Serialize(document, new JsonSerializerOptions(PlantConfiguration.SerializerOptions)
        {
            WriteIndented = true
        });
    }

    /// <summary>
    /// Wraps a trench's presses into fixed-width rows and appends the trench pressure tile,
    /// matching how the legacy mimic flowed tiles inside each trench panel.
    /// </summary>
    private static object BuildTrench(int number, IReadOnlyList<string> pressIds)
    {
        var rows = new List<List<string>>();
        for (var i = 0; i < pressIds.Count; i += TilesPerRow)
        {
            rows.Add(pressIds.Skip(i).Take(TilesPerRow).ToList());
        }

        if (rows.Count == 0)
        {
            rows.Add([]);
        }

        rows[^1].Add($"trench:T {number}");

        return new
        {
            number,
            label = $"Trench {number}",
            pressureTag = $"TRENCH.T{number}.pressure",
            rows
        };
    }

    private static string? Nullable(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
