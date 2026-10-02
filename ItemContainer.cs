namespace MuBredaEditor;

public sealed record InvArea(string Name, int Start, int Cols, int Rows)
{
    public int End => Start + Cols * Rows;
    public bool Contains(int slot) => slot >= Start && slot < End;
}

public static class Layouts
{
    public static readonly InvArea[] Character =
    [
        new("Inventario", 12, 8, 8),
        new("Inventario extra 1", 76, 8, 4),
        new("Inventario extra 2", 108, 8, 4),
        new("Inventario extra 3", 140, 8, 4),
        new("Inventario extra 4", 172, 8, 4),
        new("Tienda personal", 204, 8, 4),
    ];

    public static readonly InvArea[] Warehouse =
    [
        new("Baúl 1", 0, 8, 15),
        new("Baúl 2 (extendido)", 120, 8, 15),
    ];

    /// <summary>Equipment slot, label, column and row in the 3x5 equipment panel.</summary>
    public static readonly (int Slot, string Name, int Col, int Row)[] Equipment =
    [
        (8, "Mascota", 0, 0), (2, "Casco", 1, 0), (7, "Alas", 2, 0),
        (0, "Arma der.", 0, 1), (3, "Armadura", 1, 1), (1, "Arma izq.", 2, 1),
        (5, "Guantes", 0, 2), (4, "Pantalón", 1, 2), (6, "Botas", 2, 2),
        (10, "Anillo 1", 0, 3), (9, "Collar", 1, 3), (11, "Anillo 2", 2, 3),
        (236, "Pentagrama", 0, 4), (237, "Arete 1", 1, 4), (238, "Arete 2", 2, 4),
    ];
}

public sealed class ItemContainer
{
    public byte[] Data { get; }
    public int Slots => Data.Length / MuItem.Size;

    public ItemContainer(byte[] data) => Data = (byte[])data.Clone();

    public MuItem? Get(int slot)
    {
        if (slot < 0 || slot >= Slots) return null;
        var span = Data.AsSpan(slot * MuItem.Size, MuItem.Size);
        return MuItem.IsEmptyBytes(span) ? null : new MuItem(span.ToArray());
    }

    public void Set(int slot, MuItem? item)
    {
        var dst = Data.AsSpan(slot * MuItem.Size, MuItem.Size);
        if (item is null) dst.Fill(0xFF);
        else item.Raw.CopyTo(dst);
    }

    public static (int W, int H) SizeOf(MuItem it, ItemDb db)
    {
        var d = db.Get(it.Cat, it.Index);
        return d is null ? (1, 1) : (d.Width, d.Height);
    }

    /// <summary>Owner slot for each cell of the area, -1 = free.</summary>
    public int[,] Occupancy(InvArea a, ItemDb db, int ignoreSlot = -1)
    {
        var occ = new int[a.Cols, a.Rows];
        for (var x = 0; x < a.Cols; x++)
        for (var y = 0; y < a.Rows; y++)
            occ[x, y] = -1;

        for (var s = a.Start; s < a.End && s < Slots; s++)
        {
            if (s == ignoreSlot) continue;
            var it = Get(s);
            if (it is null) continue;
            var (w, h) = SizeOf(it, db);
            var cx = (s - a.Start) % a.Cols;
            var cy = (s - a.Start) / a.Cols;
            for (var x = cx; x < cx + w && x < a.Cols; x++)
            for (var y = cy; y < cy + h && y < a.Rows; y++)
                occ[x, y] = s;
        }
        return occ;
    }

    public bool Fits(InvArea a, ItemDb db, int slot, int w, int h, int ignoreSlot = -1)
    {
        if (!a.Contains(slot)) return false;
        var cx = (slot - a.Start) % a.Cols;
        var cy = (slot - a.Start) / a.Cols;
        if (cx + w > a.Cols || cy + h > a.Rows) return false;
        var occ = Occupancy(a, db, ignoreSlot);
        for (var x = cx; x < cx + w; x++)
        for (var y = cy; y < cy + h; y++)
            if (occ[x, y] != -1) return false;
        return true;
    }

    public int FindFree(IEnumerable<InvArea> areas, ItemDb db, int w, int h)
    {
        foreach (var a in areas)
            for (var s = a.Start; s < a.End && s < Slots; s++)
                if (Fits(a, db, s, w, h)) return s;
        return -1;
    }
}
