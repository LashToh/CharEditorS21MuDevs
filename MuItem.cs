namespace MuBredaEditor;

/// <summary>
/// 25-byte DB item. Bytes 0-10 follow the classic MU layout; 11-15 are sockets.
/// Bytes 16-24 are not decoded and are preserved as-is.
/// </summary>
public sealed class MuItem
{
    public const int Size = 25;
    public byte[] Raw { get; }

    public MuItem(byte[] raw)
    {
        if (raw.Length != Size) throw new ArgumentException($"El item debe tener {Size} bytes.");
        Raw = raw;
    }

    public static MuItem Create(int cat, int index)
    {
        var r = new byte[Size];
        for (var i = 11; i <= 18; i++) r[i] = 0xFF;
        return new MuItem(r) { Cat = cat, Index = index };
    }

    public static bool IsEmptyBytes(ReadOnlySpan<byte> b)
    {
        foreach (var x in b) if (x != 0xFF) goto notAllFF;
        return true;
    notAllFF:
        return b[0] == 0xFF && (b[7] & 0x80) != 0 && (b[9] & 0xF0) == 0xF0;
    }

    public MuItem Clone() => new((byte[])Raw.Clone());

    public int Cat
    {
        get => Raw[9] >> 4;
        set => Raw[9] = (byte)((Raw[9] & 0x0F) | ((value & 0x0F) << 4));
    }

    public int Index
    {
        get => Raw[0] | ((Raw[7] & 0x80) << 1);
        set
        {
            Raw[0] = (byte)(value & 0xFF);
            Raw[7] = (byte)((Raw[7] & 0x7F) | ((value & 0x100) >> 1));
        }
    }

    public int Id => Cat * 512 + Index;

    public int Level
    {
        get => (Raw[1] >> 3) & 0x0F;
        set => Raw[1] = (byte)((Raw[1] & 0x87) | ((value & 0x0F) << 3));
    }

    public bool Skill
    {
        get => (Raw[1] & 0x80) != 0;
        set => Raw[1] = (byte)(value ? Raw[1] | 0x80 : Raw[1] & 0x7F);
    }

    public bool Luck
    {
        get => (Raw[1] & 0x04) != 0;
        set => Raw[1] = (byte)(value ? Raw[1] | 0x04 : Raw[1] & ~0x04);
    }

    /// <summary>Additional option 0-7 (+0 .. +28).</summary>
    public int Option
    {
        get => (Raw[1] & 0x03) | ((Raw[7] & 0x40) >> 4);
        set
        {
            Raw[1] = (byte)((Raw[1] & 0xFC) | (value & 0x03));
            Raw[7] = (byte)((Raw[7] & 0xBF) | ((value & 0x04) << 4));
        }
    }

    public int Durability
    {
        get => Raw[2];
        set => Raw[2] = (byte)Math.Clamp(value, 0, 255);
    }

    public uint Serial
    {
        get => (uint)(Raw[3] << 24 | Raw[4] << 16 | Raw[5] << 8 | Raw[6]);
        set
        {
            Raw[3] = (byte)(value >> 24);
            Raw[4] = (byte)(value >> 16);
            Raw[5] = (byte)(value >> 8);
            Raw[6] = (byte)value;
        }
    }

    public int Exc
    {
        get => Raw[7] & 0x3F;
        set => Raw[7] = (byte)((Raw[7] & 0xC0) | (value & 0x3F));
    }

    public int Ancient
    {
        get => Raw[8];
        set => Raw[8] = (byte)value;
    }

    public bool Opt380
    {
        get => (Raw[9] & 0x08) != 0;
        set => Raw[9] = (byte)(value ? Raw[9] | 0x08 : Raw[9] & ~0x08);
    }

    public int Harmony
    {
        get => Raw[10];
        set => Raw[10] = (byte)value;
    }

    public byte GetSocket(int i) => Raw[11 + i];
    public void SetSocket(int i, byte v) => Raw[11 + i] = v;

    public string Hex => string.Join(" ", Raw.Select(b => b.ToString("X2")));

    public static MuItem? FromHex(string text)
    {
        var clean = new string(text.Where(Uri.IsHexDigit).ToArray());
        if (clean.Length != Size * 2) return null;
        return new MuItem(Convert.FromHexString(clean));
    }

    public static readonly string[] ExcWeapon =
    [
        "Mana al matar (mana/8)", "Vida al matar (vida/8)", "Velocidad de ataque +7",
        "Daño +2%", "Daño +nivel/20", "Daño excelente +10%",
    ];

    public static readonly string[] ExcArmor =
    [
        "Zen +30%", "Tasa de defensa +10%", "Refleja daño 5%",
        "Reduce daño 4%", "Mana máx. +4%", "Vida máx. +4%",
    ];

    public static string[] ExcLabels(int cat) => cat is >= 6 and <= 11 ? ExcArmor : ExcWeapon;

    public string Describe(ItemDef? def)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{def?.Name ?? "(item desconocido)"} +{Level}");
        sb.AppendLine($"Categoría {Cat}, índice {Index}  (ID MuDevs {Id})");
        sb.AppendLine($"Durabilidad {Durability}   Serial {Serial}");
        var opts = new List<string>();
        if (Skill) opts.Add("Skill");
        if (Luck) opts.Add("Luck");
        if (Option > 0) opts.Add($"Opción +{Option * 4}");
        if (Opt380) opts.Add("Opción 380");
        if (opts.Count > 0) sb.AppendLine(string.Join(", ", opts));
        if (Exc != 0)
        {
            var labels = ExcLabels(Cat);
            sb.AppendLine("Excelente: " + string.Join(", ", Enumerable.Range(0, 6).Where(b => (Exc & (1 << b)) != 0).Select(b => labels[b])));
        }
        if (Ancient != 0) sb.AppendLine($"Ancient/Set: {Ancient}");
        if (Harmony != 0) sb.AppendLine($"Harmony: tipo {Harmony >> 4}, nivel {Harmony & 0x0F}");
        var sockets = Enumerable.Range(0, 5).Select(GetSocket).ToArray();
        if (sockets.Any(s => s != 0xFF))
            sb.AppendLine("Sockets: " + string.Join(" ", sockets.Select(s => s == 0xFF ? "--" : s == 0xFE ? "vacío" : s.ToString())));
        sb.AppendLine();
        sb.AppendLine("Hex:");
        sb.Append(Hex);
        return sb.ToString();
    }
}
