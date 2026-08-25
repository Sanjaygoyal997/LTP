using System.Globalization;
using System.Text;
using System.Text.Json;
using CuringMonitor.Api.Domain;

namespace CuringMonitor.Api.Configuration;

/// <summary>
/// Reads the equipment configuration the plant maintains.
///
/// Two layouts are accepted, told apart by the header line.
///
/// <b>Named</b> — column meaning comes from its header, so order is irrelevant, unused
/// columns can simply be deleted, and a new signal is a new column rather than a code
/// change:
/// <code>
/// GroupNo#Name#Title#WorkCentre#Threshold#Signal.pressure#Signal.alarm#Signal.recipe
/// </code>
/// Any column named <c>Signal.x</c> binds signal <c>x</c>; anything unrecognised becomes an
/// attribute, so a screen can show or filter on it without the service knowing about it.
///
/// <b>Legacy</b> — the original fixed 11-column layout, read positionally.
/// </summary>
public static class EquipmentConfigReader
{
    private const string PanelSizeFileName = "trenchSize.txt";
    private const string SignalPrefix = "Signal.";
    private static readonly char[] CandidateDelimiters = ['#', ','];

    /// <summary>Header names understood for the fixed fields, each with its aliases.</summary>
    private static readonly Dictionary<string, string> FieldAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GroupNo"] = "group", ["RowNo"] = "group", ["Trench"] = "group",
        ["Name"] = "name", ["PressName"] = "name",
        ["Title"] = "title", ["PressTitle"] = "title",
        ["WorkCentre"] = "workCentre", ["WorkCenter"] = "workCentre",
        ["WorkCentreId"] = "workCentre", ["WcID"] = "workCentre",
        ["Threshold"] = "threshold", ["RunThreshold"] = "threshold",
        ["RunSignal"] = "runSignal", ["StatusSignal"] = "runSignal"
    };

    public sealed record Result(
        IReadOnlyList<AssetDefinition> Assets,
        IReadOnlyList<GroupDefinition> Groups,
        IReadOnlyList<string> SourceFiles);

    public static Result Read(string path, string groupLabelFormat = "Group {0}")
    {
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        if (lines.Length == 0)
        {
            throw new InvalidOperationException($"{Path.GetFileName(path)} is empty.");
        }

        var delimiter = DetectDelimiter(lines[0], path);
        var header = lines[0].Split(delimiter).Select(h => h.Trim()).ToArray();
        var schema = Schema.From(header, path);

        var assets = new List<AssetDefinition>();
        var counts = new Dictionary<int, int>();
        var groupOrder = new List<int>();
        var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var lineNumber = 2; lineNumber <= lines.Length; lineNumber++)
        {
            var line = lines[lineNumber - 1];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = line.Split(delimiter);
            var item = schema.Read(columns, path, lineNumber);

            if (!groupOrder.Contains(item.Group))
            {
                groupOrder.Add(item.Group);
            }

            var position = counts.GetValueOrDefault(item.Group) + 1;
            counts[item.Group] = position;

            assets.Add(new AssetDefinition
            {
                // The same name legitimately appears in more than one group, so the
                // identifier carries the group; the caption is unaffected.
                Id = UniqueId(usedIds, item.Group, item.Name),
                Kind = AssetKinds.Press,
                Label = item.Title,
                Group = GroupName(item.Group, groupLabelFormat),
                Position = position,
                RunThreshold = item.Threshold,
                RunSignal = item.RunSignal,
                Attributes = item.Attributes,
                Signals = item.Signals
            });
        }

        if (assets.Count == 0)
        {
            throw new InvalidOperationException($"{Path.GetFileName(path)} defines no equipment.");
        }

        var panelPath = Path.Combine(Path.GetDirectoryName(path) ?? ".", PanelSizeFileName);
        var panels = ReadPanels(panelPath);

        var groups = groupOrder
            .Select((groupNo, index) =>
            {
                var hasPanel = panels.TryGetValue(groupNo, out var panel);

                return new GroupDefinition
                {
                    Key = GroupName(groupNo, groupLabelFormat),
                    Label = GroupName(groupNo, groupLabelFormat),
                    Order = index,
                    PanelWidth = hasPanel ? panel.Width : null,
                    PanelHeight = hasPanel ? panel.Height : null
                };
            })
            .ToArray();

        return new Result(assets, groups, [path, panelPath]);
    }

    /// <summary>Exports the same content as an asset file, for sites that would rather edit that.</summary>
    public static string ToAssetJson(string configPath, string title)
    {
        var result = Read(configPath);
        var document = new { title, groups = result.Groups, assets = result.Assets };

        return JsonSerializer.Serialize(document, new JsonSerializerOptions(PlantConfiguration.SerializerOptions)
        {
            WriteIndented = true
        });
    }

    /// <summary>What one line yields, before it becomes an asset.</summary>
    private sealed record Item(
        int Group,
        string Name,
        string Title,
        double? Threshold,
        string? RunSignal,
        Dictionary<string, string> Attributes,
        Dictionary<string, string> Signals);

    /// <summary>
    /// Where each field lives in a line. Built once from the header so every line is read
    /// the same way, rather than re-deciding per row.
    /// </summary>
    private sealed class Schema
    {
        private readonly Dictionary<string, int> _fields = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<(string Signal, int Column)> _signals = [];
        private readonly List<(string Name, int Column)> _attributes = [];
        private readonly bool _legacy;

        public static Schema From(string[] header, string path)
        {
            // The original file's header names its columns PressName, CommunicationCheck and
            // so on, in a fixed order. Recognising it keeps existing files working untouched.
            var legacy = header.Length >= 10 &&
                         header[3].Equals("CommunicationCheck", StringComparison.OrdinalIgnoreCase);

            var schema = new Schema(legacy);
            if (legacy)
            {
                return schema;
            }

            for (var column = 0; column < header.Length; column++)
            {
                var name = header[column];
                if (name.Length == 0)
                {
                    continue;
                }

                if (name.StartsWith(SignalPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    schema._signals.Add((name[SignalPrefix.Length..], column));
                }
                else if (FieldAliases.TryGetValue(name, out var field))
                {
                    schema._fields[field] = column;
                }
                else
                {
                    // Unrecognised columns are carried through as attributes rather than
                    // rejected: a screen can bind to them, and the service need not care.
                    schema._attributes.Add((name, column));
                }
            }

            foreach (var required in new[] { "group", "name" })
            {
                if (!schema._fields.ContainsKey(required))
                {
                    throw new InvalidOperationException(
                        $"{Path.GetFileName(path)}: the header has no '{required}' column. " +
                        "Expected GroupNo and Name, or the original 11-column header.");
                }
            }

            return schema;
        }

        private Schema(bool legacy) => _legacy = legacy;

        public Item Read(string[] columns, string path, int lineNumber)
        {
            return _legacy ? ReadLegacy(columns, path, lineNumber) : ReadNamed(columns, path, lineNumber);
        }

        private Item ReadNamed(string[] columns, string path, int lineNumber)
        {
            var group = Integer(columns, _fields["group"], path, lineNumber, "group number");
            var name = Text(columns, _fields["name"]);
            if (name.Length == 0)
            {
                throw new InvalidOperationException($"{Path.GetFileName(path)} line {lineNumber}: name is blank.");
            }

            var attributes = new Dictionary<string, string>
            {
                ["group"] = group.ToString(CultureInfo.InvariantCulture),
                ["name"] = name
            };

            foreach (var (attribute, column) in _attributes)
            {
                var value = Text(columns, column);
                if (value.Length > 0)
                {
                    attributes[attribute] = value;
                }
            }

            if (_fields.TryGetValue("workCentre", out var wcColumn))
            {
                var workCentre = Text(columns, wcColumn);
                if (workCentre.Length > 0)
                {
                    attributes[SignalNames.WorkCentreAttribute] = workCentre;
                }
            }

            var signals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (signal, column) in _signals)
            {
                var address = Text(columns, column);
                if (address.Length > 0)
                {
                    signals[signal] = address;
                }
            }

            double? threshold = null;
            if (_fields.TryGetValue("threshold", out var thresholdColumn))
            {
                var text = Text(columns, thresholdColumn);
                if (text.Length > 0 &&
                    double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    threshold = value;
                }
            }

            var title = _fields.TryGetValue("title", out var titleColumn) ? Text(columns, titleColumn) : string.Empty;

            string? runSignal = null;
            if (_fields.TryGetValue("runSignal", out var runSignalColumn))
            {
                var named = Text(columns, runSignalColumn);
                runSignal = named.Length > 0 ? named : null;
            }

            return new Item(group, name, title.Length > 0 ? title : name, threshold, runSignal, attributes, signals);
        }

        private static Item ReadLegacy(string[] columns, string path, int lineNumber)
        {
            if (columns.Length < 10)
            {
                throw new InvalidOperationException(
                    $"{Path.GetFileName(path)} line {lineNumber}: expected at least 10 columns, found {columns.Length}.");
            }

            var group = Integer(columns, 0, path, lineNumber, "group number");
            var name = Text(columns, 1);
            if (name.Length == 0)
            {
                throw new InvalidOperationException($"{Path.GetFileName(path)} line {lineNumber}: name is blank.");
            }

            // The original columns carry the same three meanings, so they are stored under
            // the default signal names the rules look for.
            var signals = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Add(signals, SignalNames.RunCheck, columns, 3);
            Add(signals, "open", columns, 4);
            Add(signals, SignalNames.Alarm, columns, 5);
            Add(signals, SignalNames.Recipe, columns, 6);

            var title = Text(columns, 2);

            return new Item(
                group,
                name,
                title.Length > 0 ? title : name,
                null,
                null,
                new Dictionary<string, string>
                {
                    ["group"] = group.ToString(CultureInfo.InvariantCulture),
                    ["name"] = name
                },
                signals);
        }

        private static void Add(Dictionary<string, string> signals, string signal, string[] columns, int column)
        {
            var address = Text(columns, column);
            if (address.Length > 0)
            {
                signals[signal] = address;
            }
        }

        private static string Text(string[] columns, int column) =>
            column >= 0 && column < columns.Length ? columns[column].Trim() : string.Empty;

        private static int Integer(string[] columns, int column, string path, int lineNumber, string what)
        {
            var text = Text(columns, column);
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                throw new InvalidOperationException(
                    $"{Path.GetFileName(path)} line {lineNumber}: '{text}' is not a {what}.");
            }

            return value;
        }
    }

    private static char DetectDelimiter(string header, string path)
    {
        var best = CandidateDelimiters
            .Select(candidate => (Candidate: candidate, Count: header.Split(candidate).Length))
            .OrderByDescending(x => x.Count)
            .First();

        if (best.Count < 3)
        {
            throw new InvalidOperationException(
                $"{Path.GetFileName(path)}: the header line does not split into columns on '#' or ','.");
        }

        return best.Candidate;
    }

    /// <summary>
    /// Reads the panel geometry file: a header line (<c>id,w,h</c>), then one line per group.
    /// The <c>id</c> is the group number, so the two files join on it.
    /// </summary>
    private static IReadOnlyDictionary<int, (int Width, int Height)> ReadPanels(string path)
    {
        var panels = new Dictionary<int, (int, int)>();
        if (!File.Exists(path))
        {
            return panels;
        }

        foreach (var line in File.ReadLines(path, Encoding.UTF8).Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = line.Split(',');
            var id = Number(columns, 0);
            var width = Number(columns, 1);
            var height = Number(columns, 2);

            if (id > 0 && width > 0 && height > 0)
            {
                panels[id] = (width, height);
            }
        }

        return panels;
    }

    private static int Number(string[] columns, int index) =>
        index < columns.Length &&
        int.TryParse(columns[index].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) &&
        value > 0
            ? value
            : 0;

    private static string UniqueId(HashSet<string> used, int groupNo, string name)
    {
        var id = $"{groupNo}/{name}";
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

    private static string GroupName(int groupNo, string format) =>
        string.Format(CultureInfo.InvariantCulture, format, groupNo);
}
