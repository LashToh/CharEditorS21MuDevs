using System.Drawing.Drawing2D;
using System.Reflection;
using System.Xml.Linq;

namespace MuBredaEditor;

public sealed class ItemDef
{
    public int Cat { get; init; }
    public int Index { get; init; }
    public string Name { get; init; } = "";
    public int Width { get; init; } = 1;
    public int Height { get; init; } = 1;
    public int Durability { get; init; }
    /// <summary>Equipment slot from Item.xml: 0/1 hands, 2-6 armor, 9 pendant, 10/11 rings, -1 none.</summary>
    public int Slot { get; init; } = -1;
    public int Skill { get; init; }
    public bool TwoHand { get; init; }
    public int Level { get; init; }
    public int Id => Cat * 512 + Index;
    public override string ToString() => $"{Index,4}  {Name}";
}

public sealed class ItemDb
{
    public Dictionary<int, string> Sections { get; } = [];
    public Dictionary<int, ItemDef> Items { get; } = [];
    public string Source { get; private set; } = "";
    public SocketData Sockets { get; private set; } = SocketData.Empty;
    public SetData Sets { get; private set; } = SetData.Empty;

    public ItemDef? Get(int cat, int index) => Items.GetValueOrDefault(cat * 512 + index);

    public IEnumerable<ItemDef> InSection(int cat) =>
        Items.Values.Where(i => i.Cat == cat).OrderBy(i => i.Index);

    public static ItemDb Load(string? path)
    {
        var db = new ItemDb();
        XDocument doc;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            doc = XDocument.Load(path);
            db.Source = path;
        }
        else
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("Item.xml")
                          ?? throw new InvalidOperationException("Falta el Item.xml interno.");
            doc = XDocument.Load(s);
            db.Source = "Item.xml interno";
        }

        foreach (var sec in doc.Descendants("Section"))
        {
            var cat = Int(sec, "Index");
            db.Sections[cat] = (string?)sec.Attribute("Name") ?? $"Sección {cat}";
            foreach (var it in sec.Elements("Item"))
            {
                var def = new ItemDef
                {
                    Cat = cat,
                    Index = Int(it, "Index"),
                    Name = (string?)it.Attribute("Name") ?? "",
                    Width = Math.Max(1, Int(it, "Width")),
                    Height = Math.Max(1, Int(it, "Height")),
                    Durability = Int(it, "Durability"),
                    Slot = it.Attribute("Slot") is null ? -1 : Int(it, "Slot"),
                    Skill = Int(it, "Skill"),
                    TwoHand = Int(it, "TwoHand") == 1,
                    Level = Int(it, "Level"),
                };
                db.Items[def.Id] = def;
            }
        }
        var dir = File.Exists(path) ? Path.GetDirectoryName(path) : null;
        db.Sockets = SocketData.Load(dir);
        db.Sets = SetData.Load(dir);
        return db;
    }

    private static int Int(XElement e, string attr) =>
        int.TryParse((string?)e.Attribute(attr), out var v) ? v : 0;
}

public sealed class ItemImages(string root)
{
    private readonly Dictionary<int, Image?> _cache = [];

    public string Root => root;
    public bool Available => Directory.Exists(root);

    public Image? Get(int cat, int index)
    {
        var key = cat * 512 + index;
        if (_cache.TryGetValue(key, out var img)) return img;
        img = null;
        foreach (var ext in new[] { ".png", ".gif", ".jpg", ".bmp" })
        {
            var p = Path.Combine(root, cat.ToString(), index + ext);
            if (!File.Exists(p)) continue;
            try
            {
                using var ms = new MemoryStream(File.ReadAllBytes(p));
                using var tmp = Image.FromStream(ms);
                img = new Bitmap(tmp);
            }
            catch { img = null; }
            break;
        }
        _cache[key] = img;
        return img;
    }

    public static void DrawFit(Graphics g, Image img, Rectangle r)
    {
        var scale = Math.Min((float)r.Width / img.Width, (float)r.Height / img.Height);
        scale = Math.Min(scale, 2f);
        var w = (int)(img.Width * scale);
        var h = (int)(img.Height * scale);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.DrawImage(img, r.X + (r.Width - w) / 2, r.Y + (r.Height - h) / 2, w, h);
    }
}
