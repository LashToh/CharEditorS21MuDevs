namespace MuBredaEditor;

/// <summary>Visual editor for an item blob (character inventory or warehouse).</summary>
public sealed class InventoryPage : UserControl
{
    private readonly ItemDb _db;
    private readonly ItemImages _images;
    private readonly InvArea[] _areas;
    private readonly List<SlotPanelBase> _panels = [];
    private readonly ItemPreview _details;
    private readonly ContextMenuStrip _menu = new();
    private int _menuSlot = -1;
    private bool _menuEmpty;

    public ItemContainer? Box { get; private set; }
    public bool Dirty { get; private set; }
    public Button SaveButton { get; }
    public Button ReloadButton { get; }
    public FlowLayoutPanel Toolbar { get; }

    public InventoryPage(ItemDb db, ItemImages images, InvArea[] areas, bool withEquipment)
    {
        _db = db;
        _images = images;
        _areas = areas;
        Dock = DockStyle.Fill;

        Toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(4) };
        SaveButton = new Button { Text = "Guardar", Width = 110, Height = 28 };
        ReloadButton = new Button { Text = "Recargar", Width = 90, Height = 28 };
        Toolbar.Controls.AddRange([SaveButton, ReloadButton]);

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown,
            WrapContents = true, Padding = new Padding(8), BackColor = Color.FromArgb(36, 33, 30),
        };

        if (withEquipment)
            AddPanel(flow, "Equipado", new EquipPanel(db, images, () => Box));
        foreach (var a in areas)
            AddPanel(flow, a.Name, new GridPanel(a, db, images, () => Box));

        _details = new ItemPreview(images) { Dock = DockStyle.Right, Width = 300 };
        _details.ShowSlot(-1, null, db);

        Controls.Add(flow);
        Controls.Add(_details);
        Controls.Add(Toolbar);

        _menu.Items.Add("Agregar item aquí…", null, (_, _) => AddAt(_menuSlot));
        _menu.Items.Add("Editar…", null, (_, _) => EditAt(_menuSlot));
        _menu.Items.Add("Borrar", null, (_, _) => DeleteAt(_menuSlot));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Copiar hex", null, (_, _) => CopyHex(_menuSlot));
        _menu.Items.Add("Pegar hex aquí", null, (_, _) => PasteHex(_menuSlot));
        _menu.Opening += (_, _) =>
        {
            _menu.Items[0].Enabled = _menuEmpty;
            _menu.Items[1].Enabled = !_menuEmpty;
            _menu.Items[2].Enabled = !_menuEmpty;
            _menu.Items[4].Enabled = !_menuEmpty;
        };
    }

    private void AddPanel(FlowLayoutPanel flow, string title, SlotPanelBase panel)
    {
        var group = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true,
            Margin = new Padding(0, 0, 16, 4),
        };
        group.Controls.Add(new Label
        {
            Text = title, AutoSize = true, ForeColor = Color.FromArgb(220, 190, 120),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold), Margin = new Padding(0, 4, 0, 2),
        });
        group.Controls.Add(panel);
        flow.Controls.Add(group);
        _panels.Add(panel);
        panel.SlotClick += (_, e) => Select(e.Slot);
        panel.SlotDoubleClick += (_, e) => { if (e.IsEmpty) AddAt(e.Slot); else EditAt(e.Slot); };
        panel.SlotRightClick += (_, e) =>
        {
            Select(e.Slot);
            _menuSlot = e.Slot;
            _menuEmpty = e.IsEmpty;
            _menu.Show(e.Screen);
        };
    }

    public void Bind(byte[]? data)
    {
        Box = data is null ? null : new ItemContainer(data);
        Dirty = false;
        Select(-1);
    }

    public void MarkSaved() => Dirty = false;

    private void Changed()
    {
        Dirty = true;
        RefreshAll();
    }

    public void RefreshAll()
    {
        foreach (var p in _panels) p.Invalidate();
    }

    private void Select(int slot)
    {
        foreach (var p in _panels) p.SelectedSlot = slot;
        RefreshAll();
        var it = slot >= 0 ? Box?.Get(slot) : null;
        _details.ShowSlot(slot, it, _db);
    }

    private InvArea? AreaOf(int slot) => _areas.FirstOrDefault(a => a.Contains(slot));

    /// <summary>Equipment slots accept any size; grid slots need room.</summary>
    public bool TryPlace(int slot, MuItem item, int ignoreSlot = -1, bool quiet = false)
    {
        if (Box is null) return false;
        var area = AreaOf(slot);
        if (area is not null)
        {
            var (w, h) = ItemContainer.SizeOf(item, _db);
            if (!Box.Fits(area, _db, slot, w, h, ignoreSlot))
            {
                if (!quiet)
                    MessageBox.Show($"No entra en ese lugar (el item ocupa {w}x{h}).", "Sin espacio",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
        }
        if (ignoreSlot >= 0 && ignoreSlot != slot) Box.Set(ignoreSlot, null);
        Box.Set(slot, item);
        Changed();
        Select(slot);
        return true;
    }

    public int FindFree(MuItem item)
    {
        if (Box is null) return -1;
        var (w, h) = ItemContainer.SizeOf(item, _db);
        return Box.FindFree(_areas, _db, w, h);
    }

    private void AddAt(int slot)
    {
        if (Box is null) return;
        using var f = new ItemEditForm(_db, _images, null);
        if (f.ShowDialog(this) == DialogResult.OK && f.Result is not null)
            TryPlace(slot, f.Result);
    }

    private void EditAt(int slot)
    {
        var it = Box?.Get(slot);
        if (it is null) return;
        using var f = new ItemEditForm(_db, _images, it.Clone());
        if (f.ShowDialog(this) == DialogResult.OK && f.Result is not null)
            TryPlace(slot, f.Result, ignoreSlot: slot);
    }

    private void DeleteAt(int slot)
    {
        if (Box?.Get(slot) is null) return;
        Box.Set(slot, null);
        Changed();
        Select(slot);
    }

    private void CopyHex(int slot)
    {
        var it = Box?.Get(slot);
        if (it is not null) Clipboard.SetText(it.Hex);
    }

    private void PasteHex(int slot)
    {
        if (Box is null || !Clipboard.ContainsText()) return;
        var it = MuItem.FromHex(Clipboard.GetText());
        if (it is null)
        {
            MessageBox.Show($"El portapapeles no tiene un item válido ({MuItem.Size} bytes en hex).", "Pegar hex");
            return;
        }
        TryPlace(slot, it, ignoreSlot: Box.Get(slot) is null ? -1 : slot);
    }
}
