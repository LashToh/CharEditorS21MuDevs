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
    private readonly NumericUpDown _anc = Num(0, 255);
    private readonly CheckBox _o380 = new() { Text = "Opción 380", AutoSize = true };
    private readonly NumericUpDown _harm = Num(0, 255);
    private readonly TextBox[] _sock = new TextBox[5];
    private readonly NumericUpDown _serial = new() { Minimum = 0, Maximum = uint.MaxValue, Width = 120 };
    private readonly TextBox _hex = new() { Width = 600, Font = new Font("Consolas", 9f) };

    public MuItem? Result { get; private set; }

    private static NumericUpDown Num(int min, int max) => new() { Minimum = min, Maximum = max, Width = 70 };

    public ItemEditForm(ItemDb db, ItemImages images, MuItem? existing)
    {
        _db = db;
        _images = images;
        _item = existing ?? MuItem.Create(0, 0);
        Text = existing is null ? "Agregar item" : "Editar item";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ClientSize = new Size(900, 580);
        Font = new Font("Segoe UI", 9f);

        foreach (var (cat, name) in db.Sections.Where(s => s.Key <= 15).OrderBy(s => s.Key))
            _cat.Items.Add(new CatItem(cat, name));
        for (var i = 0; i <= 7; i++) _opt.Items.Add($"+{i * 4}");

        var left = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Location = new Point(10, 10), Size = new Size(270, 500), WrapContents = false };
        left.Controls.AddRange([new Label { Text = "Categoría", AutoSize = true }, _cat, _filter, _list]);

        var right = new TableLayoutPanel { Location = new Point(295, 10), Size = new Size(595, 480), ColumnCount = 2, AutoSize = false };
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
        Row(right, "Ancient / Set", Flow(_anc, new Label { Text = "5/6 = set A/B +5 · 9/10 = set A/B +10", AutoSize = true, ForeColor = Color.Gray, Padding = new Padding(4, 4, 0, 0) }));
        Row(right, "Otras", Flow(_o380, new Label { Text = "Harmony", AutoSize = true, Padding = new Padding(8, 4, 0, 0) }, _harm));

        var sockBox = new FlowLayoutPanel { AutoSize = true };
        for (var i = 0; i < 5; i++)
        {
            _sock[i] = new TextBox { Width = 36, MaxLength = 2, CharacterCasing = CharacterCasing.Upper, TextAlign = HorizontalAlignment.Center };
            sockBox.Controls.Add(_sock[i]);
        }
        sockBox.Controls.Add(new Label { Text = "hex · FF = sin socket · FE = vacío", AutoSize = true, ForeColor = Color.Gray, Padding = new Padding(4, 4, 0, 0) });
        Row(right, "Sockets", sockBox);
        Row(right, "Serial", _serial);

        var hexLabel = new Label { Text = "Hex (25 bytes):", AutoSize = true, Location = new Point(295, 508) };
        _hex.Location = new Point(395, 505);
        _hex.Width = 400;
        var applyHex = new Button { Text = "Aplicar hex", Location = new Point(800, 503), Width = 90 };

        var ok = new Button { Text = "Aceptar", DialogResult = DialogResult.OK, Location = new Point(700, 540), Width = 90 };
        var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(800, 540), Width = 90 };
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
        foreach (var c in new Control[] { _level, _dur, _anc, _harm, _serial })
            ((NumericUpDown)c).ValueChanged += (_, _) => FromControls();
        foreach (var c in new[] { _skill, _luck, _o380 }.Concat(_exc))
            c.CheckedChanged += (_, _) => FromControls();
        _opt.SelectedIndexChanged += (_, _) => FromControls();
        foreach (var s in _sock) s.TextChanged += (_, _) => FromControls();

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
        _anc.Value = _item.Ancient;
        _o380.Checked = _item.Opt380;
        _harm.Value = _item.Harmony;
        for (var i = 0; i < 5; i++) _sock[i].Text = _item.GetSocket(i).ToString("X2");
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
        _item.Ancient = (int)_anc.Value;
        _item.Opt380 = _o380.Checked;
        _item.Harmony = (int)_harm.Value;
        for (var i = 0; i < 5; i++)
            if (byte.TryParse(_sock[i].Text, System.Globalization.NumberStyles.HexNumber, null, out var b))
                _item.SetSocket(i, b);
        _item.Serial = (uint)_serial.Value;
        _hex.Text = _item.Hex;
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
