namespace MuBredaEditor;

/// <summary>
/// Gives a set. "Normal" versions are the armor pieces (sections 7-11) of one variant; ancient / legendary
/// versions create exactly the members of that set option (SetItemType.xml), including weapons, shields,
/// rings and pendants, each with its own ancient tier.
/// </summary>
public sealed class SetForm : Form
{
    private static readonly int[] ArmorCats = [7, 8, 9, 10, 11];
    private static readonly Dictionary<int, int> FallbackSlot = new() { [7] = 2, [8] = 3, [9] = 4, [10] = 5, [11] = 6 };

    private readonly ItemDb _db;
    private readonly List<ArmorGroup> _groups;
    private readonly TextBox _filter = new() { Width = 300, PlaceholderText = "Buscar set…" };
    private readonly ListBox _list = new() { Width = 300, Height = 430 };
    private readonly ComboBox _version = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    private readonly NumericUpDown _level = new() { Minimum = 0, Maximum = 15, Value = 15, Width = 60 };
    private readonly CheckBox _luck = new() { Text = "Luck", AutoSize = true, Checked = true };
    private readonly ComboBox _opt = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70 };
    private readonly CheckBox _exc = new() { Text = "Full excelente (6 opciones)", AutoSize = true };
    private readonly ComboBox _stamina = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly Label _optNote = new() { AutoSize = true, ForeColor = Color.DarkGoldenrod, MaximumSize = new Size(400, 0) };
    private readonly RadioButton _equip = new() { Text = "Equiparlo (reemplaza lo que tenga puesto en esos lugares)", AutoSize = true, Checked = true };
    private readonly RadioButton _bag = new() { Text = "Ponerlo en el inventario (primer lugar libre)", AutoSize = true };
    private readonly Label _pieces = new() { AutoSize = true, ForeColor = Color.DimGray, MaximumSize = new Size(400, 0) };
    private readonly CheckBox _withSockets = new() { Text = "Con sockets (se aplican iguales a las piezas de armadura socket)", AutoSize = true, MaximumSize = new Size(450, 0) };
    private readonly SocketEditor _sockEd;

    /// <summary>Created items with their equipment slot (-1 = goes to the bag).</summary>
    public List<(MuItem Item, int Slot)> Items { get; } = [];
    public bool ToEquipment => _equip.Checked;

    private sealed record Variant(int Index, string Suffix, ItemDef[] Pieces);

    private sealed record ArmorGroup(string Name, List<Variant> Variants, List<SetOption> Options)
    {
        public override string ToString()
        {
            var extra = Options.Count == 0 ? "" : $"  · {Options.Count} ancient/legendary";
            return Name + (Variants.Count > 1 ? $"  ({string.Join("/", Variants.Select(v => v.Suffix))})" : "") + extra;
        }
    }

    private sealed record Version(string Label, Variant? Normal, SetOption? Set)
    {
        public override string ToString() => Label;
    }

    public SetForm(ItemDb db)
    {
        _db = db;
        _sockEd = new SocketEditor(db.Sockets, 450);
        Text = "Dar set";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ClientSize = new Size(1230, 520);
        Font = new Font("Segoe UI", 9f);

        _groups = BuildGroups(db);
        for (var i = 0; i <= 7; i++) _opt.Items.Add($"+{i * 4}");
        _opt.SelectedIndex = 7;
        _stamina.Items.AddRange(["Sin stamina extra", "+5 stamina", "+10 stamina"]);
        _stamina.SelectedIndex = 0;

        var left = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Location = new Point(10, 10), Size = new Size(310, 500), WrapContents = false };
        left.Controls.AddRange([_filter, _list]);

        var right = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Location = new Point(335, 10), Size = new Size(415, 460), WrapContents = false, AutoScroll = true };
        right.Controls.AddRange(
        [
            Line("Versión", _version),
            _pieces,
            new Label { Text = " ", AutoSize = true },
            Line("Nivel +", _level),
            Line("Adicional", _opt),
            _optNote,
            _luck, _exc,
            Line("Stamina", _stamina),
            new Label { Text = " ", AutoSize = true },
            _equip, _bag,
        ]);

        var sockets = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Location = new Point(765, 10), Size = new Size(460, 460), WrapContents = false };
        sockets.Controls.AddRange([_withSockets, _sockEd]);
        _sockEd.Enabled = false;

        var ok = new Button { Text = "Dar set", DialogResult = DialogResult.OK, Location = new Point(1030, 482), Width = 90 };
        var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(1130, 482), Width = 90 };
        AcceptButton = ok;
        CancelButton = cancel;
        Controls.AddRange([left, right, sockets, ok, cancel]);

        _sockEd.Reset(8, false);
        _withSockets.CheckedChanged += (_, _) => _sockEd.Enabled = _withSockets.Checked;
        _filter.TextChanged += (_, _) => Fill();
        _list.SelectedIndexChanged += (_, _) => GroupPicked();
        _version.SelectedIndexChanged += (_, _) => VersionPicked();
        ok.Click += (_, _) => Build(ok);
        Fill();
        VersionPicked();
    }

    private static List<ArmorGroup> BuildGroups(ItemDb db)
    {
        var groups = new Dictionary<string, ArmorGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (var armor in db.InSection(8))
        {
            var pieces = ArmorCats.Select(c => db.Get(c, armor.Index)).OfType<ItemDef>().ToArray();
            if (pieces.Length < 3) continue;
            var (name, suffix) = SplitVariant(CleanName(armor.Name));
            if (!groups.TryGetValue(name, out var g)) groups[name] = g = new ArmorGroup(name, [], []);
            g.Variants.Add(new Variant(armor.Index, suffix, pieces));
        }
        foreach (var g in groups.Values)
        {
            g.Variants.Sort((a, b) => string.CompareOrdinal(a.Suffix, b.Suffix));
            var seen = new HashSet<int>();
            foreach (var p in g.Variants.SelectMany(v => v.Pieces))
                foreach (var (o, _) in db.Sets.VersionsOf(p.Id))
                    if (seen.Add(o.Index)) g.Options.Add(o);
            g.Options.Sort((a, b) => a.Index.CompareTo(b.Index));
        }
        return groups.Values.OrderBy(g => g.Name).ToList();
    }

    private void GroupPicked()
    {
        _version.BeginUpdate();
        _version.Items.Clear();
        if (_list.SelectedItem is ArmorGroup g)
        {
            foreach (var v in g.Variants)
                _version.Items.Add(new Version(v.Suffix.Length > 0 ? $"Normal {v.Suffix}" : "Normal", v, null));
            foreach (var o in g.Options)
                _version.Items.Add(new Version(o.ToString(), null, o));
            _version.SelectedIndex = 0;
        }
        _version.EndUpdate();
        VersionPicked();
    }

    private void VersionPicked()
    {
        var v = _version.SelectedItem as Version;
        var legendary = v?.Set?.IsLegendary == true;
        _stamina.Enabled = v?.Set is not null;
        _opt.Enabled = !legendary;
        _optNote.Text = legendary ? "Los items legendary no admiten opción adicional (Jewel of Life): se crean con +0." : "";
        _optNote.Visible = legendary;

        var members = Members(v);
        _pieces.Text = members.Count == 0 ? "" : "Se crean:\n" + string.Join("\n", members.Select(m =>
            $"  · {SlotLabel(m.Def)}: {m.Def.Name}{(_db.Sockets.IsSocketItem(m.Def.Cat, m.Def.Index) ? " (socket)" : "")}"));
        var socket = members.Any(m => SocketData.IsArmorCat(m.Def.Cat) && _db.Sockets.IsSocketItem(m.Def.Cat, m.Def.Index));
        _sockEd.Retarget(8, socket);
    }

    private List<(ItemDef Def, int Tier)> Members(Version? v)
    {
        if (v?.Normal is { } n) return n.Pieces.Select(p => (p, 0)).ToList();
        if (v?.Set is { } s)
            return s.Members
                .Select(m => (Def: _db.Get(m.ItemId / 512, m.ItemId % 512), m.Tier))
                .Where(m => m.Def is not null)
                .Select(m => (m.Def!, m.Tier))
                .OrderBy(m => SlotOrder(m.Item1))
                .ToList();
        return [];
    }

    private void Build(Button ok)
    {
        if (_version.SelectedItem is not Version v)
        {
            MessageBox.Show("Elegí un set de la lista.");
            DialogResult = DialogResult.None;
            return;
        }
        var members = Members(v);
        var socketSet = members.Any(m => SocketData.IsArmorCat(m.Def.Cat) && _db.Sockets.IsSocketItem(m.Def.Cat, m.Def.Index));
        if (_withSockets.Checked && !socketSet && !_sockEd.ForceChecked)
        {
            MessageBox.Show("Ese set no acepta sockets según SocketItemType.xml. Elegí un set con piezas socket " +
                            "o tildá \"Editar igual\" en la sección de sockets.", "Sockets");
            DialogResult = DialogResult.None;
            return;
        }

        var legendary = v.Set?.IsLegendary == true;
        var stamina = _stamina.SelectedIndex switch { 1 => SetData.Stamina5, 2 => SetData.Stamina10, _ => 0 };
        var used = new HashSet<int>();
        Items.Clear();
        foreach (var (d, tier) in members)
        {
            var it = MuItem.Create(d.Cat, d.Index);
            var jewelry = d.Cat == 13;
            it.Level = jewelry ? 0 : (int)_level.Value;
            it.Durability = d.Durability > 0 ? d.Durability : 255;
            it.Luck = !jewelry && _luck.Checked;
            it.Skill = d.Skill > 0 && d.Cat <= 6;
            it.Option = legendary ? 0 : _opt.SelectedIndex;
            it.Exc = _exc.Checked && !jewelry ? 0x3F : 0;
            it.Ancient = tier > 0 ? tier | stamina : 0;
            if (_withSockets.Checked && SocketData.IsArmorCat(d.Cat) && (_db.Sockets.IsSocketItem(d.Cat, d.Index) || _sockEd.ForceChecked))
                _sockEd.ApplyTo(it, force: true);
            Items.Add((it, EquipSlot(d, used)));
        }
    }

    private static int EquipSlot(ItemDef d, HashSet<int> used)
    {
        var s = d.Slot >= 0 ? d.Slot : FallbackSlot.GetValueOrDefault(d.Cat, -1);
        if (s == 10 && used.Contains(10)) s = 11;
        if (s < 0 || !used.Add(s)) return -1;
        if (d.TwoHand && s == 0) used.Add(1);
        return s;
    }

    private static int SlotOrder(ItemDef d) => d.Slot switch { 0 => 0, 1 => 1, >= 2 and <= 6 => d.Slot, 9 => 9, 10 or 11 => 10, _ => 20 };

    private static string SlotLabel(ItemDef d) => d.Slot switch
    {
        0 => "Arma",
        1 => d.Cat == 6 ? "Escudo" : "Mano izq.",
        2 => "Casco",
        3 => "Armadura",
        4 => "Pantalón",
        5 => "Guantes",
        6 => "Botas",
        9 => "Collar",
        10 or 11 => "Anillo",
        _ => "Item",
    };

    private static (string Name, string Suffix) SplitVariant(string n)
    {
        var m = System.Text.RegularExpressions.Regex.Match(n, @"^(.*?)\s*(\[[A-Z]\])$");
        return m.Success ? (m.Groups[1].Value.Trim(), m.Groups[2].Value) : (n, "");
    }

    private static string CleanName(string n)
    {
        foreach (var suffix in new[] { " Armor", " Robe", " Mail" })
        {
            var i = n.IndexOf(suffix, StringComparison.OrdinalIgnoreCase);
            if (i > 0) return (n[..i] + n[(i + suffix.Length)..]).Trim();
        }
        return n;
    }

    private static FlowLayoutPanel Line(string label, Control c)
    {
        var f = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        f.Controls.Add(new Label { Text = label, Width = 90, Padding = new Padding(0, 5, 0, 0) });
        f.Controls.Add(c);
        return f;
    }

    private void Fill()
    {
        var f = _filter.Text.Trim();
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var g in _groups)
            if (f.Length == 0 || g.Name.Contains(f, StringComparison.OrdinalIgnoreCase)
                || g.Options.Any(o => o.Name.Contains(f, StringComparison.OrdinalIgnoreCase)))
                _list.Items.Add(g);
        _list.EndUpdate();
    }
}
