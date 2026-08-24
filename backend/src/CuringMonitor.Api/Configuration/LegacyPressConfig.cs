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
/// <c>RowNo</c> is the trench number. Trailing columns are ignored: the legacy file carries
/// a flag the display does not use, and revisions have added columns before now.
/// </remarks>
public static class LegacyPressConfig
{
    private const int MinimumColumns = 10;

    public static IReadOnlyList<AssetDefinition> Read(string path)
    {
        var assets = new List<AssetDefinition>();
        var positionByTrench = new Dictionary<int, int>();
        var trenches = new List<int>();
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

            if (!trenches.Contains(trench))
            {
                trenches.Add(trench);
            }

            var position = positionByTrench.GetValueOrDefault(trench) + 1;
            positionByTrench[trench] = position;

            assets.Add(new AssetDefinition
            {
                Id = id,
                Kind = AssetKinds.Press,
                Label = Optional(columns[2]) ?? id,
                Group = GroupName(trench),
                Position = position,
                Attributes = { ["trench"] = trench.ToString(CultureInfo.InvariantCulture) },
                Signals = Signals(columns)
            });
        }

        if (assets.Count == 0)
        {
            throw new InvalidOperationException($"{Path.GetFileName(path)} defines no presses.");
        }

        // Each trench gets its header-pressure box, as on the existing screen. The tag is
        // supplied from settings; without one the box shows as no-communication.
        foreach (var trench in trenches)
        {
            assets.Add(new AssetDefinition
            {
                Id = $"T{trench}",
                Kind = AssetKinds.Gauge,
                Label = $"T {trench}",
                Group = GroupName(trench),
                // Sorts after every press in the trench without needing to count them.
                Position = int.MaxValue,
                Attributes =
                {
                    ["trench"] = trench.ToString(CultureInfo.InvariantCulture),
                    ["unit"] = "kg/cm²"
                }
            });
        }

        return assets;
    }

    /// <summary>Exports the same content as an asset file, for sites that would rather edit that.</summary>
    public static string ToAssetJson(string legacyConfigPath, string title)
    {
        var document = new { title, assets = Read(legacyConfigPath) };

        return JsonSerializer.Serialize(document, new JsonSerializerOptions(PlantConfiguration.SerializerOptions)
        {
            WriteIndented = true
        });
    }

    private static Dictionary<string, string> Signals(string[] columns)
    {
        var signals = new Dictionary<string, string>
        {
            [SignalNames.Pressure] = columns[3].Trim(),
            [SignalNames.Open] = columns[4].Trim(),
            [SignalNames.Fault] = columns[5].Trim()
        };

        Add(signals, SignalNames.Recipe, columns[6]);
        Add(signals, SignalNames.Counter(ShiftName.A), columns[7]);
        Add(signals, SignalNames.Counter(ShiftName.B), columns[8]);
        Add(signals, SignalNames.Counter(ShiftName.C), columns[9]);

        return signals;
    }

    private static void Add(Dictionary<string, string> signals, string name, string value)
    {
        var tag = Optional(value);
        if (tag is not null)
        {
            signals[name] = tag;
        }
    }

    private static string GroupName(int trench) =>
        $"Trench {trench.ToString(CultureInfo.InvariantCulture)}";

    private static string? Optional(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
