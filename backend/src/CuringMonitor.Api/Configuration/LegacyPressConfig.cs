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

    /// <summary>Companion file giving each trench's panel size, if the site ships one.</summary>
    private const string TrenchSizeFileName = "trenchSize.txt";

    public sealed record Result(
        IReadOnlyList<AssetDefinition> Assets,
        IReadOnlyList<GroupDefinition> Groups);

    /// <param name="tilePitch">
    /// Width one box occupied on the legacy screen, used to turn a trench panel width from
    /// <c>trenchSize.txt</c> into boxes per row. The mimic drew 40px buttons with a 3px
    /// margin either side.
    /// </param>
    public static Result Read(string path, int tilePitch = 46)
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

        // Trench panel widths, if the site ships the companion file. The legacy screen never
        // stated a boxes-per-row figure: it sized each trench panel in pixels and let the
        // buttons wrap, so the width has to be converted back into a count.
        var panelWidths = ReadTrenchWidths(Path.Combine(Path.GetDirectoryName(path) ?? ".", TrenchSizeFileName));

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

        // Groups are drawn in the order the configuration lists them, not in name order:
        // the plant's own sequence is the one operators know.
        var groups = trenches
            .Select((trench, index) => new GroupDefinition
            {
                Key = GroupName(trench),
                Label = GroupName(trench),
                Order = index,
                Wrap = WrapFor(panelWidths, index, tilePitch)
            })
            .ToArray();

        return new Result(assets, groups);
    }

    /// <summary>
    /// Reads <c>trenchSize.txt</c>: a header line, then one comma-separated line per trench
    /// in the same order the presses are listed, with width in column 1 and height in
    /// column 2. Missing or unreadable, the screen decides its own row width instead.
    /// </summary>
    private static IReadOnlyList<int> ReadTrenchWidths(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var widths = new List<int>();

        foreach (var line in File.ReadLines(path, Encoding.UTF8).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = line.Split(',');
            if (columns.Length > 1 &&
                int.TryParse(columns[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var width) &&
                width > 0)
            {
                widths.Add(width);
            }
            else
            {
                // Keep the positions aligned: the k-th line describes the k-th trench, so a
                // bad line must not shift every trench after it.
                widths.Add(0);
            }
        }

        return widths;
    }

    private static int? WrapFor(IReadOnlyList<int> panelWidths, int index, int tilePitch)
    {
        if (index >= panelWidths.Count || panelWidths[index] <= 0 || tilePitch <= 0)
        {
            return null;
        }

        return Math.Max(1, panelWidths[index] / tilePitch);
    }

    /// <summary>Exports the same content as an asset file, for sites that would rather edit that.</summary>
    public static string ToAssetJson(string legacyConfigPath, string title)
    {
        var result = Read(legacyConfigPath);
        var document = new { title, groups = result.Groups, assets = result.Assets };

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
