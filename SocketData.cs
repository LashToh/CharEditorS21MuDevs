using System.Reflection;
using System.Xml.Linq;

namespace MuBredaEditor;

public sealed record SocketOption(int Index, int Element, string Name, int[] Values)
{
    public int ValueAt(int level) => level >= 1 && level <= Values.Length ? Values[level - 1] : 0;
    public override string ToString() => $"{SocketData.ElementName(Element)} · {Name}";
}

/// <summary>Seed combination bonus: sockets 1-3 must hold seeds of ReqElements (0 = any) in that order.</summary>
public sealed record SocketBonus(int Index, int SubIndex, int MinSection, int MaxSection, int Value, int[] ReqElements, string Name)
{
    public bool AppliesTo(int cat) => cat >= MinSection && cat <= MaxSection;
    public string Recipe => string.Join(" + ", ReqElements.Where(e => e > 0).Select(SocketData.ElementName));
}

public sealed record SocketSpecial(int Index, int Value, int[] ReqCounts, string Name);

/// <summary>
/// Seed sphere data from SocketItemOption.xml / SocketItemType.xml.
/// Socket byte: 0xFF = no hole, 0xFE = empty hole, else option index + (sphere level - 1) * 50.
/// </summary>
public sealed class SocketData
{
    public const byte NoHole = 0xFF;
    public const byte EmptyHole = 0xFE;
    public const int MaxHoles = 5;
    public const int MaxLevel = 5;
    private const int LevelStep = 50;

    private readonly Dictionary<int, SocketOption> _options = [];
    private readonly Dictionary<int, int> _maxSockets = [];

    public IReadOnlyCollection<SocketOption> Options => _options.Values;
    public List<SocketBonus> Bonuses { get; } = [];
    public List<SocketSpecial> Specials { get; } = [];
    public string Source { get; private set; } = "";

    public static readonly SocketData Empty = new();

    public static string ElementName(int e) => e switch
    {
        1 => "Fire", 2 => "Water", 3 => "Ice", 4 => "Wind", 5 => "Lightning", 6 => "Earth", _ => $"Elemento {e}",
    };

    public static bool IsWeaponElement(int e) => e is 1 or 3 or 5;
    public static bool IsWeaponCat(int cat) => cat <= 5;
    public static bool IsArmorCat(int cat) => cat is >= 6 and <= 11;

    /// <summary>Elements allowed on the item: weapons take Fire/Ice/Lightning, armors Water/Wind/Earth.</summary>
    public static bool ElementFits(int cat, int element) =>
        IsWeaponCat(cat) ? IsWeaponElement(element) : !IsArmorCat(cat) || !IsWeaponElement(element);

    public int MaxSockets(int cat, int index) => _maxSockets.GetValueOrDefault(cat * 512 + index);
    public bool IsSocketItem(int cat, int index) => _maxSockets.ContainsKey(cat * 512 + index);

    public SocketOption? Option(int index) => _options.GetValueOrDefault(index);

    public IEnumerable<SocketOption> OptionsFor(int cat) =>
        _options.Values.Where(o => ElementFits(cat, o.Element)).OrderBy(o => o.Element).ThenBy(o => o.Index);

    public static byte Encode(int option, int level) =>
        (byte)(option + (Math.Clamp(level, 1, MaxLevel) - 1) * LevelStep);

    public bool TryDecode(byte b, out SocketOption option, out int level)
    {
        option = null!;
        level = b / LevelStep + 1;
        if (b >= EmptyHole || level > MaxLevel) return false;
        if (_options.GetValueOrDefault(b % LevelStep) is not { } o) return false;
        option = o;
        return true;
    }

    public string Describe(byte b)
    {
        if (b == NoHole) return "sin hueco";
        if (b == EmptyHole) return "vacío";
        return TryDecode(b, out var o, out var lv)
            ? $"{ElementName(o.Element)} Lv{lv}: {o.Name} +{o.ValueAt(lv)}"
            : $"valor crudo {b}";
    }

    /// <summary>Element of each socket (0 for no hole, empty or undecodable).</summary>
    public int[] Elements(IReadOnlyList<byte> sockets) =>
        sockets.Select(b => TryDecode(b, out var o, out _) ? o.Element : 0).ToArray();

    /// <summary>Bonus families for the item: each one lists its tiers (same SubIndex and section range) ordered by Index.</summary>
    public List<List<SocketBonus>> BonusFamilies(int cat) =>
        Bonuses.Where(b => b.AppliesTo(cat))
            .GroupBy(b => (b.SubIndex, b.MinSection, b.MaxSection))
            .Select(g => g.OrderBy(b => b.Index).ToList())
            .ToList();

    public SocketBonus? BonusByIndex(int cat, int index) =>
        Bonuses.FirstOrDefault(b => b.Index == index && b.AppliesTo(cat));

    public int TierOf(int cat, SocketBonus bonus) =>
        BonusFamilies(cat).FirstOrDefault(f => f.Contains(bonus))?.IndexOf(bonus) + 1 ?? 0;

    public static bool Matches(IReadOnlyList<int> elements, SocketBonus bonus)
    {
        for (var i = 0; i < bonus.ReqElements.Length; i++)
        {
            if (bonus.ReqElements[i] == 0) continue;
            if (i >= elements.Count || elements[i] != bonus.ReqElements[i]) return false;
        }
        return true;
    }

    /// <summary>Bonus family whose element order matches sockets 1-3, or null.</summary>
    public List<SocketBonus>? MatchingFamily(int cat, IReadOnlyList<byte> sockets)
    {
        var elements = Elements(sockets);
        return BonusFamilies(cat).FirstOrDefault(f => Matches(elements, f[0]));
    }

    /// <summary>Suggested tier: lowest sphere level among the sockets the bonus requires.</summary>
    public int SuggestedTier(List<SocketBonus> family, IReadOnlyList<byte> sockets)
    {
        var min = MaxLevel;
        var req = family[0].ReqElements;
        for (var i = 0; i < req.Length && i < sockets.Count; i++)
            if (req[i] != 0 && TryDecode(sockets[i], out _, out var lv)) min = Math.Min(min, lv);
        return Math.Clamp(min, 1, family.Count);
    }

    /// <summary>Stored bonus (byte 10 of socket items) if it is valid for the current sockets.</summary>
    public SocketBonus? StoredBonus(int cat, IReadOnlyList<byte> sockets, int harmonyByte)
    {
        if (harmonyByte == NoHole || BonusByIndex(cat, harmonyByte) is not { } b) return null;
        return MatchingFamily(cat, sockets)?.Contains(b) == true ? b : null;
    }

    public static SocketData Load(string? serverItemDir)
    {
        var data = new SocketData();
        var dir = string.IsNullOrWhiteSpace(serverItemDir) ? null : Path.Combine(serverItemDir, "Socket - Settings");
        var optDoc = LoadDoc(dir, "SocketItemOption.xml", out var src);
        var typeDoc = LoadDoc(dir, "SocketItemType.xml", out _);
        data.Source = src;

        if (optDoc is not null)
        {
            foreach (var o in optDoc.Descendants("SocketItemOptionSettings").Elements("Option"))
            {
                var values = Enumerable.Range(1, 20).Select(i => Int(o, "Value" + i)).TakeWhile(v => v > 0).ToArray();
                var opt = new SocketOption(Int(o, "Index"), Int(o, "Element"), (string?)o.Attribute("Name") ?? "", values);
                data._options[opt.Index] = opt;
            }
            foreach (var o in optDoc.Descendants("SocketBonusSettings").Elements("Option"))
                data.Bonuses.Add(new SocketBonus(Int(o, "Index"), Int(o, "SubIndex"), Int(o, "MinItemSection"), Int(o, "MaxItemSection"),
                    Int(o, "Value"), Enumerable.Range(1, 5).Select(i => Int(o, "ReqOptionType" + i)).ToArray(), (string?)o.Attribute("Name") ?? ""));
            foreach (var o in optDoc.Descendants("SpecialOptionSettings").Elements("Option"))
                data.Specials.Add(new SocketSpecial(Int(o, "Index"), Int(o, "Value"),
                    Enumerable.Range(1, 6).Select(i => Int(o, "ReqOptionTypeCount" + i)).ToArray(), (string?)o.Attribute("Name") ?? ""));
        }
        if (typeDoc is not null)
            foreach (var it in typeDoc.Descendants("Item"))
                data._maxSockets[Int(it, "Section") * 512 + Int(it, "Type")] = Int(it, "MaxSocket");
        return data;
    }

    private static XDocument? LoadDoc(string? dir, string file, out string source)
    {
        if (dir is not null && File.Exists(Path.Combine(dir, file)))
        {
            source = dir;
            return XDocument.Load(Path.Combine(dir, file));
        }
        source = "archivos internos";
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(file);
        return s is null ? null : XDocument.Load(s);
    }

    private static int Int(XElement e, string attr) =>
        int.TryParse((string?)e.Attribute(attr), out var v) ? v : 0;
}
