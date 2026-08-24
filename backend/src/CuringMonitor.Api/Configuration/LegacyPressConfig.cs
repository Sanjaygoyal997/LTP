using System.Globalization;
using System.Text;
using System.Text.Json;
using CuringMonitor.Api.Domain;

namespace CuringMonitor.Api.Configuration;

/// <summary>
/// Reads the SCADA press configuration the plant already maintains — <c>config_AB.txt</c>
/// and its companion <c>trenchSize.txt</c> — so the existing tag map stays the single
/// source of truth, with no conversion step and no second copy to keep in step.
/// </summary>
/// <remarks>
/// One box per line, '#'-separated, after a header line:
/// <code>
/// RowNo#PressName#PressTitle#CommunicationCheck#PressOpen_Close#Alarm#RecipeCode#ProdCountA#ProdCountB#ProdCountC#Flag
/// </code>
/// <c>RowNo</c> is the trench. The mimic captions each box with <c>PressTitle</c>, not
/// <c>PressName</c> — the two differ in practice.
/// </remarks>
public static class LegacyPressConfig
{
    private const int MinimumColumns = 10;
    private const string TrenchSizeFileName = "trenchSize.txt";

    public sealed record Result(
        IReadOnlyList<AssetDefinition> Assets,
        IReadOnlyList<GroupDefinition> Groups);

    public static Result Read(string path)
    {
        var assets = new List<AssetDefinition>();
        var counts = new Dictionary<int, int>();
        var trenchOrder = new List<int>();
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lineNumber = 0;

        foreach (var line in File.ReadLines(path, Encoding.UTF8))
        {
            lineNumber++;

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

            var name = columns[1].Trim();
            if (name.Length == 0)
            {
                throw new InvalidOperationException(
                    $"{Path.GetFileName(path)} line {lineNumber}: press name is blank.");
            }

            if (!trenchOrder.Contains(trench))
            {
                trenchOrder.Add(trench);
            }

            var position = counts.GetValueOrDefault(trench) + 1;
            counts[trench] = position;

            assets.Add(new AssetDefinition
            {
                // The same press name legitimately appears in more than one trench, so the
                // identifier has to carry the trench; the caption is unaffected.
                Id = UniqueId(usedIds, trench, name),
                Kind = AssetKinds.Press,
                Label = Optional(columns[2]) ?? name,
                Group = GroupName(trench),
                Position = position,
                Attributes =
                {
                    ["trench"] = trench.ToString(CultureInfo.InvariantCulture),
                    ["pressName"] = name
                },
                Signals = Signals(columns)
            });
        }

        if (assets.Count == 0)
        {
            throw new InvalidOperationException($"{Path.GetFileName(path)} defines no boxes.");
        }

        var panels = ReadTrenchPanels(Path.Combine(Path.GetDirectoryName(path) ?? ".", TrenchSizeFileName));

        // Trenches are drawn in the order the configuration lists them; trenchSize.txt gives
        // one panel per trench in that same order, which is what its id column numbers.
        var groups = trenchOrder
            .Select((trench, index) => new GroupDefinition
            {
                Key = GroupName(trench),
                Label = GroupName(trench),
                Order = index,
                PanelWidth = index < panels.Count ? panels[index].Width : null,
                PanelHeight = index < panels.Count ? panels[index].Height : null
            })
            .ToArray();

        return new Result(assets, groups);
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

    /// <summary>
    /// Reads <c>trenchSize.txt</c>: a header line, then one comma-separated line per trench
    /// in configuration order, with width in column 1 and height in column 2. The legacy
    /// mimic sized each trench panel from this and fitted the boxes into it.
    /// </summary>
    private static IReadOnlyList<(int Width, int Height)> ReadTrenchPanels(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var panels = new List<(int, int)>();

        foreach (var line in File.ReadLines(path, Encoding.UTF8).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = line.Split(',');
            var width = Number(columns, 1);
            var height = Number(columns, 2);

            // Keep positions aligned: the k-th line describes the k-th trench, so an
            // unreadable line must not shift every trench after it.
            panels.Add((width, height));
        }

        return panels;
    }

    private static int Number(string[] columns, int index) =>
        index < columns.Length &&
        int.TryParse(columns[index].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) &&
        value > 0
            ? value
            : 0;

    private static string UniqueId(HashSet<string> used, int trench, string name)
    {
        var id = $"{trench}/{name}";
        if (used.Add(id))
        {
            return id;
        }

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{id}#{suffix}";
            if (used.Add(candidate))
            {
                return candidate;
            }
        }
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
