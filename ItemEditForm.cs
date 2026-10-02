namespace MuBredaEditor;

public sealed class ItemEditForm : Form
{
    private readonly ItemDb _db;
    private readonly ItemImages _images;
    private MuItem _item;
    private bool _loading;

    private readonly ComboBox _cat = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly TextBox _filter = new() { Width = 260, PlaceholderText = "Buscar por nombre…" };
    private readonly ListBox _list = new() { Width = 260, Height = 360, Font = new Font("Consolas", 9f) };
    private readonly PictureBox _pic = new() { Size = new Size(80, 110), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(30, 28, 26) };
    private readonly Label _name = new() { AutoSize = false, Width = 480, Height = 70, Font = new Font("Segoe UI", 10f, FontStyle.Bold), Padding = new Padding(6, 24, 0, 0) };

    private readonly NumericUpDown _level = Num(0, 15);
    private readonly NumericUpDown _dur = Num(0, 255);
    private readonly CheckBox _skill = new() { Text = "Skill", AutoSize = true };
    private readonly CheckBox _luck = new() { Text = "Luck", AutoSize = true };
    private readonly ComboBox _opt = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
    private readonly CheckBox[] _exc = new CheckBox[6];
    private readonly CheckBox _excAll = new() { Text = "Full excelente", AutoSize = true };
    private readonly ComboBox _anc = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 250, DropDownWidth = 420 };
    private readonly ComboBox _stam = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly CheckBox _o380 = new() { Text = "Opción 380", AutoSize = true };
    private readonly NumericUpDown _harm = Num(0, 255);
    private readonly Label _harmLabel = new() { Text = "Harmony", AutoSize = true, Padding = new Padding(8, 4, 0, 0) };
    private readonly SocketEditor _sockEd;
    private readonly NumericUpDown _serial = new() { Minimum = 0, Maximum = uint.MaxValue, Width = 120 };
    private readonly TextBox _hex = new() { Width = 600, Font = new Font("Consolas", 9f) };

    public MuItem? Result { get; private set; }

    private static NumericUpDown Num(int min, int max) => new() { Minimum = min, Maximum = max, Width = 70 };

    public ItemEditForm(ItemDb db, ItemImages images, MuItem? existing)
    {
        _db = db;
        _images = images;
        _item = existing ?? MuItem.Create(0, 0);
        _sockEd = new SocketEditor(db.Sockets, 470);
        Text = existing is null ? "Agregar item" : "Editar item";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ClientSize = new Size(900, 780);
        Font = new Font("Segoe UI", 9f);

        foreach (var (cat, name) in db.Sections.Where(s => s.Key <= 15).OrderBy(s => s.Key))
            _cat.Items.Add(new CatItem(cat, name));
        for (var i = 0; i <= 7; i++) _opt.Items.Add($"+{i * 4}");
        _stam.Items.Add(new StamItem(0, "Sin stamina"));
        _stam.Items.Add(new StamItem(SetData.Stamina5, "+5 stamina"));
        _stam.Items.Add(new StamItem(SetData.Stamina10, "+10 stamina"));

        _list.Height = 580;
        var left = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Location = new Point(10, 10), Size = new Size(270, 680), WrapContents = false };
        left.Controls.AddRange([new Label { Text = "Categoría", AutoSize = true }, _cat, _filter, _list]);

        var right = new TableLayoutPanel { Location = new Point(295, 10), Size = new Size(595, 690), ColumnCount = 2, AutoSize = false };
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var head = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        head.Controls.AddRange([_pic, _name]);
        right.Controls.Add(head, 0, 0);
        right.SetColumnSpan(head, 2);
        right.RowCount = 1;

        Row(right, "Nivel (+)", _level);
        Row(right, "Durabilidad", _dur);
        Row(right, "Opciones", Flow(_skill, _luck, new Label { Text = "Adicional", AutoSize = true, Padding = new Padding(8, 4, 0, 0) }, _opt));

        var excBox = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, MaximumSize = new Size(470, 0) };
        _excAll.Width = 460;
        _excAll.AutoSize = false;
        excBox.Controls.Add(_excAll);
        for (var i = 0; i < 6; i++)
        {
            _exc[i] = new CheckBox { AutoSize = false, Width = 225 };
            excBox.Controls.Add(_exc[i]);
        }
        Row(right, "Excelente", excBox);
        Row(right, "Ancient", Flow(_anc, _stam));
        Row(right, "Otras", Flow(_o380, _harmLabel, _harm));
        Row(right, "Sockets", _sockEd);
        Row(right, "Serial", _serial);

        var hexLabel = new Label { Text = "Hex (25 bytes):", AutoSize = true, Location = new Point(295, 711) };
        _hex.Location = new Point(395, 708);
        _hex.Width = 400;
        var applyHex = new Button { Text = "Aplicar hex", Location = new Point(800, 706), Width = 90 };

        var ok = new Button { Text = "Aceptar", DialogResult = DialogResult.OK, Location = new Point(700, 742), Width = 90 };
        var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(800, 742), Width = 90 };
        AcceptButton = ok;
        CancelButton = cancel;

        Controls.AddRange([left, right, hexLabel, _hex, applyHex, ok, cancel]);

        _cat.SelectedIndexChanged += (_, _) => FillList();
        _filter.TextChanged += (_, _) => FillList();
        _list.SelectedIndexChanged += (_, _) => PickFromList();
        applyHex.Click += (_, _) => ApplyHex();
        _excAll.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            foreach (var c in _exc) c.Checked = _excAll.Checked;
        };
        foreach (var c in new NumericUpDown[] { _level, _dur, _harm, _serial })
            c.ValueChanged += (_, _) => FromControls();
        foreach (var c in new[] { _skill, _luck, _o380 }.Concat(_exc))
            c.CheckedChanged += (_, _) => FromControls();
        _opt.SelectedIndexChanged += (_, _) => FromControls();
        _anc.SelectedIndexChanged += (_, _) => { if (!_loading) OnAncientPick(); };
        _stam.SelectedIndexChanged += (_, _) => FromControls();
        _sockEd.Changed += (_, _) => FromControls();

        ok.Click += (_, _) =>
        {
            FromControls();
            if (_db.Get(_item.Cat, _item.Index) is null &&
                MessageBox.Show("Ese item no está en Item.xml. ¿Guardarlo igual?", "Item desconocido",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                DialogResult = DialogResult.None;
                return;
            }
            Result = _item;
        };

        SelectCategory(_item.Cat);
        ToControls();
    }

    private static FlowLayoutPanel Flow(params Control[] cs)
    {
        var f = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        f.Controls.AddRange(cs);
        return f;
    }

    private static void Row(TableLayoutPanel t, string label, Control c)
    {
        var r = t.RowCount++;
        t.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, r);
        t.Controls.Add(c, 1, r);
    }

    private sealed record CatItem(int Cat, string Name)
    {
        public override string ToString() => $"{Cat} - {Name}";
    }

    private void SelectCategory(int cat)
    {
        for (var i = 0; i < _cat.Items.Count; i++)
            if (((CatItem)_cat.Items[i]!).Cat == cat) { _cat.SelectedIndex = i; return; }
        if (_cat.Items.Count > 0) _cat.SelectedIndex = 0;
    }

    private void FillList()
    {
        if (_cat.SelectedItem is not CatItem ci) return;
        var f = _filter.Text.Trim();
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var d in _db.InSection(ci.Cat))
            if (f.Length == 0 || d.Name.Contains(f, StringComparison.OrdinalIgnoreCase))
                _list.Items.Add(d);
        _list.EndUpdate();
        for (var i = 0; i < _list.Items.Count; i++)
            if (_list.Items[i] is ItemDef d && d.Cat == _item.Cat && d.Index == _item.Index)
            {
                _loading = true;
                _list.SelectedIndex = i;
                _loading = false;
                break;
            }
    }

    private void PickFromList()
    {
        if (_loading || _list.SelectedItem is not ItemDef d) return;
        _item.Cat = d.Cat;
        _item.Index = d.Index;
        _item.Durability = d.Durability > 0 ? d.Durability : 1;
        ToControls();
    }

    private void ToControls()
    {
        _loading = true;
        var def = _db.Get(_item.Cat, _item.Index);
        _name.Text = def is null ? $"Desconocido ({_item.Cat}, {_item.Index})" : $"{def.Name}   [{def.Width}x{def.Height}]";
        _pic.Image = _images.Get(_item.Cat, _item.Index);
        _level.Value = _item.Level;
        _dur.Value = _item.Durability;
        _skill.Checked = _item.Skill;
        _luck.Checked = _item.Luck;
        _opt.SelectedIndex = _item.Option;
        var labels = MuItem.ExcLabels(_item.Cat);
        for (var i = 0; i < 6; i++)
        {
            _exc[i].Text = labels[i];
            _exc[i].Checked = (_item.Exc & (1 << i)) != 0;
        }
        _excAll.Checked = _item.Exc == 0x3F;
        FillAncient();
        _o380.Checked = _item.Opt380;
        _harm.Value = _item.Harmony;
        var socketItem = _db.Sockets.IsSocketItem(_item.Cat, _item.Index);
        _harm.Enabled = !socketItem;
        _harmLabel.Text = socketItem ? "Harmony (usado por el bonus socket)" : "Harmony";
        _sockEd.LoadFrom(_item);
        _serial.Value = _item.Serial;
        _hex.Text = _item.Hex;
        _loading = false;
    }

    private void FromControls()
    {
        if (_loading) return;
        _item.Level = (int)_level.Value;
        _item.Durability = (int)_dur.Value;
        _item.Skill = _skill.Checked;
        _item.Luck = _luck.Checked;
        _item.Option = Math.Max(0, _opt.SelectedIndex);
        var exc = 0;
        for (var i = 0; i < 6; i++) if (_exc[i].Checked) exc |= 1 << i;
        _item.Exc = exc;
        if (_anc.SelectedItem is AncItem anc)
        {
            if (anc.Raw is int raw) _item.Ancient = raw;
            else if (anc.Tier == 0) _item.Ancient = 0;
            else _item.Ancient = anc.Tier | (((StamItem?)_stam.SelectedItem)?.Bits ?? 0);
        }
        _item.Opt380 = _o380.Checked;
        if (!_db.Sockets.IsSocketItem(_item.Cat, _item.Index)) _item.Harmony = (int)_harm.Value;
        _sockEd.ApplyTo(_item);
        _item.Serial = (uint)_serial.Value;
        _hex.Text = _item.Hex;
    }

    private void OnAncientPick()
    {
        _stam.Enabled = _anc.SelectedItem is AncItem a && a.Raw is null && a.Tier > 0;
        FromControls();
    }

    private void FillAncient()
    {
        var ancient = _item.Ancient;
        var tier = ancient & 0x03;
        var stam = ancient & 0x0C;
        var extra = ancient & 0xF0;
        var versions = _db.Sets.VersionsOf(_item.Id).ToList();
        var tierOk = tier == 0 || versions.Any(v => v.Tier == tier);
        var stamOk = stam is 0 or SetData.Stamina5 or SetData.Stamina10;
        var raw = ancient != 0 && (!tierOk || !stamOk || extra != 0);

        _anc.Items.Clear();
        _anc.Items.Add(new AncItem(0, "Sin ancient"));
        foreach (var (opt, t) in versions)
            _anc.Items.Add(new AncItem(t, $"{opt.Name} (tier {t})"));
        if (raw)
            _anc.Items.Add(new AncItem(0, $"Valor crudo {ancient} (se conserva)", ancient));

        var ancIndex = 0;
        for (var i = 0; i < _anc.Items.Count; i++)
        {
            var a = (AncItem)_anc.Items[i]!;
            if (raw && a.Raw == ancient) { ancIndex = i; break; }
            if (!raw && a.Raw is null && a.Tier == tier) { ancIndex = i; break; }
        }
        _anc.SelectedIndex = ancIndex;

        var want = !raw && tier > 0 && stamOk ? stam : 0;
        var stamIndex = 0;
        for (var i = 0; i < _stam.Items.Count; i++)
            if (((StamItem)_stam.Items[i]!).Bits == want) { stamIndex = i; break; }
        _stam.SelectedIndex = stamIndex;
        _stam.Enabled = !raw && tier > 0;
    }

    private sealed record AncItem(int Tier, string Label, int? Raw = null)
    {
        public override string ToString() => Label;
    }

    private sealed record StamItem(int Bits, string Label)
    {
        public override string ToString() => Label;
    }

    private void ApplyHex()
    {
        var it = MuItem.FromHex(_hex.Text);
        if (it is null)
        {
            MessageBox.Show($"Tienen que ser {MuItem.Size} bytes en hexadecimal.", "Hex inválido");
            return;
        }
        _item = it;
        SelectCategory(_item.Cat);
        ToControls();
    }
}
