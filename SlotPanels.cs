namespace MuBredaEditor;

public sealed class SlotEventArgs(int slot, bool empty, Point screen) : EventArgs
{
    public int Slot { get; } = slot;
    public bool IsEmpty { get; } = empty;
    public Point Screen { get; } = screen;
}

public abstract class SlotPanelBase : Control
{
    protected readonly ItemDb Db;
    protected readonly ItemImages Images;
    protected readonly Func<ItemContainer?> GetContainer;

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public int SelectedSlot { get; set; } = -1;

    public event EventHandler<SlotEventArgs>? SlotClick;
    public event EventHandler<SlotEventArgs>? SlotDoubleClick;
    public event EventHandler<SlotEventArgs>? SlotRightClick;

    protected SlotPanelBase(ItemDb db, ItemImages images, Func<ItemContainer?> getContainer)
    {
        Db = db;
        Images = images;
        GetContainer = getContainer;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(24, 22, 20);
        Font = new Font("Segoe UI", 7.5f);
    }

    protected abstract (int Slot, bool Empty)? HitTest(Point p);

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        var hit = HitTest(e.Location);
        if (hit is null) return;
        var args = new SlotEventArgs(hit.Value.Slot, hit.Value.Empty, PointToScreen(e.Location));
        if (e.Button == MouseButtons.Right) SlotRightClick?.Invoke(this, args);
        else SlotClick?.Invoke(this, args);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (e.Button != MouseButtons.Left) return;
        var hit = HitTest(e.Location);
        if (hit is not null)
            SlotDoubleClick?.Invoke(this, new SlotEventArgs(hit.Value.Slot, hit.Value.Empty, PointToScreen(e.Location)));
    }

    protected void DrawItem(Graphics g, Rectangle r, MuItem it, bool selected)
    {
        var bg = it.Ancient != 0 ? Color.FromArgb(25, 45, 85)
               : it.Exc != 0 ? Color.FromArgb(20, 70, 35)
               : Color.FromArgb(60, 52, 40);
        using (var b = new SolidBrush(bg)) g.FillRectangle(b, r);

        var img = Images.Get(it.Cat, it.Index);
        var def = Db.Get(it.Cat, it.Index);
        var inner = Rectangle.Inflate(r, -2, -2);
        if (img is not null) ItemImages.DrawFit(g, img, inner);
        else TextRenderer.DrawText(g, def?.Name ?? $"{it.Cat},{it.Index}", Font, inner, Color.Gainsboro,
            TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        if (it.Level > 0)
            TextRenderer.DrawText(g, "+" + it.Level, Font, new Point(r.X + 1, r.Y), Color.Gold);

        using var pen = new Pen(selected ? Color.Yellow : Color.FromArgb(140, 120, 80), selected ? 2 : 1);
        g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
    }
}

public sealed class GridPanel : SlotPanelBase
{
    public const int Cell = 32;
    public InvArea Area { get; }

    public GridPanel(InvArea area, ItemDb db, ItemImages images, Func<ItemContainer?> getContainer)
        : base(db, images, getContainer)
    {
        Area = area;
        Size = new Size(area.Cols * Cell + 1, area.Rows * Cell + 1);
        Margin = new Padding(0, 0, 0, 8);
    }

    protected override (int Slot, bool Empty)? HitTest(Point p)
    {
        var cx = p.X / Cell;
        var cy = p.Y / Cell;
        if (cx < 0 || cy < 0 || cx >= Area.Cols || cy >= Area.Rows) return null;
        var c = GetContainer();
        if (c is null) return null;
        var owner = c.Occupancy(Area, Db)[cx, cy];
        return owner >= 0 ? (owner, false) : (Area.Start + cy * Area.Cols + cx, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        using var grid = new Pen(Color.FromArgb(55, 50, 42));
        for (var x = 0; x <= Area.Cols; x++) g.DrawLine(grid, x * Cell, 0, x * Cell, Area.Rows * Cell);
        for (var y = 0; y <= Area.Rows; y++) g.DrawLine(grid, 0, y * Cell, Area.Cols * Cell, y * Cell);

        var c = GetContainer();
        if (c is null) return;
        for (var s = Area.Start; s < Area.End && s < c.Slots; s++)
        {
            var it = c.Get(s);
            if (it is null) continue;
            var (w, h) = ItemContainer.SizeOf(it, Db);
            var cx = (s - Area.Start) % Area.Cols;
            var cy = (s - Area.Start) / Area.Cols;
            w = Math.Min(w, Area.Cols - cx);
            h = Math.Min(h, Area.Rows - cy);
            DrawItem(g, new Rectangle(cx * Cell, cy * Cell, w * Cell + 1, h * Cell + 1), it, s == SelectedSlot);
        }
    }
}

public sealed class EquipPanel : SlotPanelBase
{
    private const int Box = 64;
    private const int Label = 14;
    private const int Gap = 6;

    public EquipPanel(ItemDb db, ItemImages images, Func<ItemContainer?> getContainer)
        : base(db, images, getContainer)
    {
        Size = new Size(3 * (Box + Gap) + Gap, 5 * (Box + Label + Gap) + Gap);
        Margin = new Padding(0, 0, 0, 8);
    }

    private static Rectangle BoxRect(int col, int row) =>
        new(Gap + col * (Box + Gap), Gap + row * (Box + Label + Gap) + Label, Box, Box);

    protected override (int Slot, bool Empty)? HitTest(Point p)
    {
        var c = GetContainer();
        if (c is null) return null;
        foreach (var (slot, _, col, row) in Layouts.Equipment)
            if (BoxRect(col, row).Contains(p) && slot < c.Slots)
                return (slot, c.Get(slot) is null);
        return null;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var c = GetContainer();
        foreach (var (slot, name, col, row) in Layouts.Equipment)
        {
            var r = BoxRect(col, row);
            TextRenderer.DrawText(g, name, Font, new Rectangle(r.X - 4, r.Y - Label, r.Width + 8, Label),
                Color.FromArgb(200, 180, 140), TextFormatFlags.HorizontalCenter);
            var it = c is not null && slot < c.Slots ? c.Get(slot) : null;
            if (it is null)
            {
                using var pen = new Pen(slot == SelectedSlot ? Color.Yellow : Color.FromArgb(70, 62, 50));
                g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
            }
            else DrawItem(g, r, it, slot == SelectedSlot);
        }
    }
}
