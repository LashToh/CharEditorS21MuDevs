using System.Reflection;
using System.Xml;

namespace MuBredaEditor;

/// <summary>Ancient / legendary set from SetItemOption.xml with its members from SetItemType.xml.</summary>
public sealed class SetOption(int index, string name)
{
    public int Index { get; } = index;
    public string Name { get; } = name;
    public List<(int ItemId, int Tier)> Members { get; } = [];
    public bool IsLegendary => Name.StartsWith("Legendary", StringComparison.OrdinalIgnoreCase);
    public string Kind => IsLegendary ? "legendary" : "ancient";
    public override string ToString() => $"{Name} ({Kind})";
}

/// <summary>
/// Set data. The ancient byte of an item holds the tier in bits 0-1 (1..3 = OptionIndex0..2 of the item
/// in SetItemType.xml) and the extra stamina in bits 2-3 (4 = +5, 8 = +10).
/// </summary>
public sealed class SetData
{
    public const int Stamina5 = 4;
    public const int Stamina10 = 8;

    private readonly Dictionary<int, int[]> _tiers = [];
    private readonly Dictionary<int, SetOption> _options = [];

    public string Source { get; private set; } = "";
    public static readonly SetData Empty = new();

    public int[] TiersOf(int itemId) => _tiers.GetValueOrDefault(itemId) ?? [0, 0, 0];
    public SetOption? Option(int index) => _options.GetValueOrDefault(index);

    /// <summary>Set versions the item belongs to, with the tier that selects each one.</summary>
    public IEnumerable<(SetOption Option, int Tier)> VersionsOf(int itemId)
    {
        var t = TiersOf(itemId);
        for (var i = 0; i < t.Length; i++)
            if (t[i] > 0 && Option(t[i]) is { } o) yield return (o, i + 1);
    }

    public SetOption? OptionOf(MuItem item)
    {
        var tier = item.Ancient & 0x03;
        if (tier == 0) return null;
        var t = TiersOf(item.Id);
        return t[tier - 1] > 0 ? Option(t[tier - 1]) : null;
    }

    public static int StaminaOf(int ancientByte) => (ancientByte & 0x0C) switch { Stamina5 => 5, Stamina10 => 10, _ => 0 };

    public static SetData Load(string? serverItemDir)
    {
        var data = new SetData();
        var dir = string.IsNullOrWhiteSpace(serverItemDir) ? null : Path.Combine(serverItemDir, "SetItem - Settings");
        using (var r = Open(dir, "SetItemOption.xml", out var src))
        {
            data.Source = src;
            while (r?.ReadToFollowing("Option") == true)
                if (int.TryParse(r.GetAttribute("Index"), out var idx))
                    data._options[idx] = new SetOption(idx, r.GetAttribute("Name") ?? "");
        }
        using (var r = Open(dir, "SetItemType.xml", out _))
        {
            while (r?.ReadToFollowing("ItemOptionList") == true)
            {
                if (!int.TryParse(r.GetAttribute("Index"), out var id)) continue;
                var t = new int[3];
                for (var i = 0; i < 3; i++) int.TryParse(r.GetAttribute($"OptionIndex{i}"), out t[i]);
                data._tiers[id] = t;
                for (var i = 0; i < 3; i++)
                    if (t[i] > 0 && data._options.TryGetValue(t[i], out var o)) o.Members.Add((id, i + 1));
            }
        }
        return data;
    }

    private static XmlReader? Open(string? dir, string file, out string source)
    {
        var settings = new XmlReaderSettings { IgnoreComments = true, DtdProcessing = DtdProcessing.Ignore };
        if (dir is not null && File.Exists(Path.Combine(dir, file)))
        {
            source = dir;
            return XmlReader.Create(Path.Combine(dir, file), settings);
        }
        source = "archivos internos";
        var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(file);
        return s is null ? null : XmlReader.Create(s, settings);
    }
}
