namespace MuBredaEditor;

/// <summary>Gives a full armor set: the same item index in sections 7-11 (helm, armor, pants, gloves, boots).</summary>
public sealed class SetForm : Form
{
    private static readonly (int Cat, int EquipSlot)[] Pieces = [(7, 2), (8, 3), (9, 4), (10, 5), (11, 6)];

    private readonly ItemDb _db;
    private readonly List<ArmorSet> _sets;
    private readonly TextBox _filter = new() { Width = 300, PlaceholderText = "Buscar set…" };
    private readonly ListBox _list = new() { Width = 300, Height = 380 };
    private readonly NumericUpDown _level = new() { Minimum = 0, Maximum = 15, Value = 15, Width = 60 };
    private readonly CheckBox _luck = new() { Text = "Luck", AutoSize = true, Checked = true };
    private readonly ComboBox _opt = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70 };
    private readonly CheckBox _exc = new() { Text = "Full excelente (6 opciones)", AutoSize = true };
    private readonly NumericUpDown _anc = new() { Minimum = 0, Maximum = 255, Width = 60 };
    private readonly RadioButton _equip = new() { Text = "Equiparlo (reemplaza casco, armadura, pantalón, guantes y botas)", AutoSize = true, Checked = true };
    private readonly RadioButton _bag = new() { Text = "Ponerlo en el inventario (primer lugar libre)", AutoSize = true };
    private readonly Label _pieces = new() { AutoSize = true, ForeColor = Color.Gray };

    public List<MuItem> Items { get; } = [];
    public bool ToEquipment => _equip.Checked;

    private sealed record ArmorSet(int Index, string Name, ItemDef[] Pieces)
    {
        public override string ToString() => $"{Name}  ({Pieces.Length} piezas)";
    }

    public SetForm(ItemDb db)
    {
        _db = db;
        Text = "Dar set";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ClientSize = new Size(760, 470);
        Font = new Font("Segoe UI", 9f);

        _sets = db.InSection(8)
            .Select(a => new ArmorSet(a.Index, CleanName(a.Name),
                Pieces.Select(p => db.Get(p.Cat, a.Index)).Where(d => d is not null).Cast<ItemDef>().ToArray()))
            .Where(s => s.Pieces.Length >= 3)
            .OrderBy(s => s.Name)
            .ToList();
        for (var i = 0; i <= 7; i++) _opt.Items.Add($"+{i * 4}");
        _opt.SelectedIndex = 7;

        var left = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Location = new Point(10, 10), Size = new Size(310, 450), WrapContents = false };
        left.Controls.AddRange([_filter, _list]);

        var right = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, Location = new Point(335, 10), Size = new Size(415, 400), WrapContents = false };
        right.Controls.AddRange(
        [
            Line("Nivel +", _level),
            Line("Adicional", _opt),
            _luck, _exc,
            Line("Ancient / Set", _anc),
            new Label { Text = "5/6 = set A/B +5 · 9/10 = set A/B +10 (sólo si el set tiene versión ancient)", AutoSize = true, ForeColor = Color.Gray, MaximumSize = new Size(400, 0) },
            new Label { Text = " ", AutoSize = true },
            _equip, _bag,
            new Label { Text = " ", AutoSize = true },
            _pieces,
        ]);

        var ok = new Button { Text = "Dar set", DialogResult = DialogResult.OK, Location = new Point(560, 430), Width = 90 };
        var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(660, 430), Width = 90 };
        AcceptButton = ok;
        CancelButton = cancel;
        Controls.AddRange([left, right, ok, cancel]);

        _filter.TextChanged += (_, _) => Fill();
        _list.SelectedIndexChanged += (_, _) =>
            _pieces.Text = _list.SelectedItem is ArmorSet s ? "Piezas: " + string.Join(", ", s.Pieces.Select(p => p.Name)) : "";
        ok.Click += (_, _) =>
        {
            if (_list.SelectedItem is not ArmorSet s)
            {
                MessageBox.Show("Elegí un set de la lista.");
                DialogResult = DialogResult.None;
                return;
            }
            Items.Clear();
            foreach (var d in s.Pieces)
            {
                var it = MuItem.Create(d.Cat, d.Index);
                it.Level = (int)_level.Value;
                it.Durability = d.Durability > 0 ? d.Durability : 255;
                it.Luck = _luck.Checked;
                it.Option = _opt.SelectedIndex;
                it.Exc = _exc.Checked ? 0x3F : 0;
                it.Ancient = (int)_anc.Value;
                Items.Add(it);
            }
        };
        Fill();
    }

    public static int EquipSlotFor(int cat) => Pieces.First(p => p.Cat == cat).EquipSlot;

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
        foreach (var s in _sets)
            if (f.Length == 0 || s.Name.Contains(f, StringComparison.OrdinalIgnoreCase))
                _list.Items.Add(s);
        _list.EndUpdate();
    }
}
