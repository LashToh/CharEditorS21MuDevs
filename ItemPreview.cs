namespace MuBredaEditor;

/// <summary>Game-style item tooltip: colored name, large icon, blue options, sockets and a small hex dump.</summary>
public sealed class ItemPreview : UserControl
{
    private static readonly Color NameNormal = Color.FromArgb(255, 244, 214);
    private static readonly Color NameExc = Color.FromArgb(30, 230, 80);
    private static readonly Color NameAncient = Color.FromArgb(255, 196, 40);
    private static readonly Color NameLegendary = Color.FromArgb(255, 132, 48);
    private static readonly Color OptionBlue = Color.FromArgb(126, 178, 255);
    private static readonly Color SocketViolet = Color.FromArgb(176, 146, 255);
    private static readonly Color Muted = Color.FromArgb(164, 156, 144);
    private static readonly Color HexColor = Color.FromArgb(124, 116, 106);
    private static readonly Color Border = Color.FromArgb(176, 140, 64);

    private readonly ItemImages _images;
    private readonly Font _titleFont = new("Segoe UI", 11f, FontStyle.Bold);
    private readonly Font _bodyFont = new("Segoe UI", 9f);
    private readonly Font _hexFont = new("Consolas", 8f);
    private readonly List<Tip> _tips = [];
    private Image? _image;
    private int _contentHeight;
    private bool _measuring;

    private readonly record struct Tip(string Text, Color Color, Font Font, bool Center, int GapAfter, bool Image);

    public ItemPreview(ItemImages images)
    {
        _images = images;
        Width = 300;
        BackColor = Color.FromArgb(24, 20, 18);
        ForeColor = NameNormal;
        AutoScroll = true;
        DoubleBuffered = true;
        Resize += (_, _) =>
        {
            UpdateScroll();
            Invalidate();
        };
    }

    public void ShowSlot(int slot, MuItem? item, ItemDb db)
    {
        _tips.Clear();
        _image = null;
        if (item is null)
        {
            var msg = slot >= 0
                ? $"Casillero {slot} vacío."
                : "Click en un item para ver el detalle.\nDoble click para editar.\nClick derecho para más opciones.";
            foreach (var line in msg.Split('\n'))
                _tips.Add(new Tip(line, Muted, _bodyFont, false, 4, false));
        }
        else
            Build(slot, item, db);

        AutoScrollPosition = Point.Empty;
        UpdateScroll();
        Invalidate();
    }

    private void Build(int slot, MuItem item, ItemDb db)
    {
        var def = db.Get(item.Cat, item.Index);
        var set = db.Sets.OptionOf(item);
        var sockets = db.Sockets;
        var socketItem = sockets.IsSocketItem(item.Cat, item.Index);
        var nameColor = set?.IsLegendary == true ? NameLegendary : set is not null ? NameAncient : item.Exc != 0 ? NameExc : NameNormal;

        _tips.Add(new Tip($"Casillero {slot}", Muted, _hexFont, false, 6, false));
        var name = def?.Name ?? "(item desconocido)";
        if (set is not null) name = $"{set.Name} {name}";
        _tips.Add(new Tip($"{name} +{item.Level}", nameColor, _titleFont, true, 8, false));

        _image = _images.Get(item.Cat, item.Index);
        if (_image is null)
            _tips.Add(new Tip("(sin imagen)", Muted, _bodyFont, true, 8, false));
        else
            _tips.Add(new Tip("", Color.Empty, _bodyFont, true, 10, true));

        _tips.Add(new Tip($"Durabilidad {item.Durability}    Serial {item.Serial}", Muted, _bodyFont, false, 8, false));

        if (item.Skill) _tips.Add(Opt("Skill"));
        if (item.Luck) _tips.Add(Opt("Luck"));
        if (item.Option > 0) _tips.Add(Opt($"Opción adicional +{item.Option * 4}"));
        if (item.Opt380) _tips.Add(Opt("Opción 380"));
        if (item.Exc != 0)
        {
            var labels = MuItem.ExcLabels(item.Cat);
            for (var i = 0; i < 6; i++)
                if ((item.Exc & (1 << i)) != 0) _tips.Add(Opt(labels[i]));
        }
        if (!socketItem && item.Harmony != 0)
            _tips.Add(Opt($"Harmony: tipo {item.Harmony >> 4}, nivel {item.Harmony & 0x0F}"));

        var stam = SetData.StaminaOf(item.Ancient);
        if (set is not null && stam > 0)
            _tips.Add(new Tip($"Stamina +{stam}", nameColor, _bodyFont, false, 4, false));
        else if (set is null && item.Ancient != 0)
            _tips.Add(new Tip($"Ancient byte {item.Ancient}", NameAncient, _bodyFont, false, 4, false));

        var raw = item.Sockets;
        if (raw.Any(s => s != 0xFF))
        {
            _tips.Add(new Tip("Sockets", Muted, _bodyFont, false, 2, false));
            for (var i = 0; i < raw.Length; i++)
                if (raw[i] != 0xFF)
                    _tips.Add(new Tip($"{i + 1}. {sockets.Describe(raw[i])}", SocketViolet, _bodyFont, false, 2, false));
        }
        if (socketItem && item.Harmony != 0xFF)
        {
            var bonus = sockets.StoredBonus(item.Cat, raw, item.Harmony);
            var text = bonus is not null
                ? $"Bonus socket: {bonus.Name} +{bonus.Value} (nivel {sockets.TierOf(item.Cat, bonus)})"
                : item.Harmony != 0 ? $"Bonus socket: valor {item.Harmony} (no corresponde)" : null;
            if (text is not null)
                _tips.Add(new Tip(text, SocketViolet, _bodyFont, false, 4, false));
        }

        if (_tips.Count > 0)
        {
            var last = _tips[^1];
            _tips[^1] = last with { GapAfter = last.GapAfter + 10 };
        }
        _tips.Add(new Tip(item.Hex, HexColor, _hexFont, false, 4, false));
    }

    private Tip Opt(string text) => new(text, OptionBlue, _bodyFont, false, 3, false);

    private int TextWidth(bool withBar)
    {
        var bar = withBar ? SystemInformation.VerticalScrollBarWidth : 0;
        return Math.Max(80, ClientSize.Width - 28 - bar);
    }

    private (int W, int H) ImageSize(int maxW)
    {
        if (_image is null) return (0, 0);
        var scale = Math.Min(maxW / (float)_image.Width, 168f / _image.Height);
        scale = Math.Min(scale, 3f);
        return (Math.Max(1, (int)(_image.Width * scale)), Math.Max(1, (int)(_image.Height * scale)));
    }

    private int Measure(bool withBar)
    {
        var y = 12;
        var w = TextWidth(withBar);
        foreach (var t in _tips)
        {
            if (t.Image)
            {
                y += ImageSize(w).H + t.GapAfter;
                continue;
            }
            var size = TextRenderer.MeasureText(t.Text, t.Font, new Size(w, int.MaxValue), Flags(t.Center));
            y += Math.Max(size.Height, t.Font.Height) + t.GapAfter;
        }
        return y + 10;
    }

    private void UpdateScroll()
    {
        if (_measuring) return;
        _measuring = true;
        _contentHeight = Measure(false);
        if (_contentHeight > ClientSize.Height)
            _contentHeight = Measure(true);
        AutoScrollMinSize = new Size(0, _contentHeight);
        _measuring = false;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        var g = e.Graphics;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        var y = 12 + AutoScrollPosition.Y;
        var w = TextWidth(_contentHeight > ClientSize.Height);
        foreach (var t in _tips)
        {
            if (t.Image && _image is not null)
            {
                var (iw, ih) = ImageSize(w);
                g.DrawImage(_image, 14 + (w - iw) / 2, y, iw, ih);
                y += ih + t.GapAfter;
                continue;
            }
            var flags = Flags(t.Center);
            var size = TextRenderer.MeasureText(g, t.Text, t.Font, new Size(w, int.MaxValue), flags);
            var h = Math.Max(size.Height, t.Font.Height);
            TextRenderer.DrawText(g, t.Text, t.Font, new Rectangle(14, y, w, h), t.Color, flags);
            y += h + t.GapAfter;
        }
        using var pen = new Pen(Border);
        g.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
    }

    private static TextFormatFlags Flags(bool center) =>
        TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
        (center ? TextFormatFlags.HorizontalCenter : 0);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _titleFont.Dispose();
            _bodyFont.Dispose();
            _hexFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
