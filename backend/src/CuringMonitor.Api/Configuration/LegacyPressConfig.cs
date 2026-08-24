using System.Globalization;
using System.Text;
using System.Text.Json;
using CuringMonitor.Api.Domain;

namespace CuringMonitor.Api.Configuration;

/// <summary>
/// Reads the SCADA press configuration the plant already maintains
/// (<c>config_AB.txt</c> and its siblings) so the existing tag map stays the single
/// source of truth — no conversion step, no second copy to keep in step.
/// </summary>
/// <remarks>
/// One press per line, '#'-separated, with a header line:
/// <code>
/// RowNo#PressName#PressTitle#CommunicationCheck#PressOpen_Close#Alarm#RecipeCode#ProdCountA#ProdCountB#ProdCountC#Flag
/// </code>
/// <c>RowNo</c> is the trench number. Trailing columns beyond ProdCountC are ignored:
/// the legacy file carries a flag the display does not use, and later revisions have
/// added columns before now.
/// </remarks>
public static class LegacyPressConfig
{
    /// <summary>Presses per tile row, matching how the legacy mimic flowed a trench panel.</summary>
    private const int TilesPerRow = 16;

    private const int MinimumColumns = 10;

    public sealed record Result(IReadOnlyList<PressDefinition> Presses, IReadOnlyList<TrenchDefinition> Trenches);

    public static Result Read(string path)
    {
        var presses = new List<PressDefinition>();
        var pressIdsByTrench = new Dictionary<int, List<string>>();
        var trenchOrder = new List<int>();
        var lineNumber = 0;

        foreach (var line in File.ReadLines(path, Encoding.UTF8))
        {
            lineNumber++;

            // Skip the header and any blank separator lines.
            if (lineNumber == 1 || string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = line.Split('#');
            if (columns.Length < MinimumColumns)
            {
                throw new InvalidOperationException(
                    $"{Path.GetFileName(path)} line {lineNumber}: expected at least " +
                    $"{MinimumColumns} '#'-separated columns, found {columns.Length}.");
            }

            if (!int.TryParse(columns[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var trench))
            {
                throw new InvalidOperationException(
                    $"{Path.GetFileName(path)} line {lineNumber}: '{columns[0]}' is not a trench number.");
            }

            var id = columns[1].Trim();
            if (id.Length == 0)
            {
                throw new InvalidOperationException(
                    $"{Path.GetFileName(path)} line {lineNumber}: press name is blank.");
            }

            presses.Add(new PressDefinition
            {
                Id = id,
                Title = Optional(columns[2]) ?? id,
                Trench = trench,
                Tags = new PressTags
                {
                    InternalPressure = columns[3].Trim(),
                    PressOpen = columns[4].Trim(),
                    PressFault = columns[5].Trim(),
                    RecipeCode = Optional(columns[6]),
                    ShiftCounterA = Optional(columns[7]),
                    ShiftCounterB = Optional(columns[8]),
                    ShiftCounterC = Optional(columns[9])
                }
            });

            if (!pressIdsByTrench.TryGetValue(trench, out var members))
            {
                members = [];
                pressIdsByTrench[trench] = members;
                trenchOrder.Add(trench);
            }

            members.Add(id);
        }

        if (presses.Count == 0)
        {
            throw new InvalidOperationException($"{Path.GetFileName(path)} defines no presses.");
        }

        var duplicate = presses
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"{Path.GetFileName(path)}: press '{duplicate.Key}' is defined more than once.");
        }

        // Trenches are drawn highest-first, as the legacy screen did.
        var trenches = trenchOrder
            .OrderByDescending(number => number)
            .Select(number => BuildTrench(number, pressIdsByTrench[number]))
            .ToArray();

        return new Result(presses, trenches);
    }

    /// <summary>Exports the same content as a layout file, for sites that want to edit it directly.</summary>
    public static string ToLayoutJson(string legacyConfigPath, string title)
    {
        var result = Read(legacyConfigPath);

        var document = new
        {
            title,
            trenches = result.Trenches,
            presses = result.Presses
        };

        return JsonSerializer.Serialize(document, new JsonSerializerOptions(PlantConfiguration.SerializerOptions)
        {
            WriteIndented = true
        });
    }

    private static TrenchDefinition BuildTrench(int number, IReadOnlyList<string> pressIds)
    {
        var rows = new List<IReadOnlyList<string>>();
        for (var i = 0; i < pressIds.Count; i += TilesPerRow)
        {
            rows.Add(pressIds.Skip(i).Take(TilesPerRow).ToList());
        }

        // The trench pressure tile closes the last row, as it does on the existing screen.
        var lastRow = rows.Count > 0 ? rows[^1].ToList() : [];
        lastRow.Add($"trench:T {number}");
        if (rows.Count > 0)
        {
            rows[^1] = lastRow;
        }
        else
        {
            rows.Add(lastRow);
        }

        return new TrenchDefinition
        {
            Number = number,
            Label = $"Trench {number}",
            PressureTag = null,
            Rows = rows
        };
    }

    private static string? Optional(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
