namespace MuBredaEditor;

/// <summary>
/// Edits the 5 socket bytes (and the socket bonus stored in byte 10 of socket items).
/// Only offers seeds whose element fits the item and that are not already mounted in another hole.
/// </summary>
public sealed class SocketEditor : UserControl
{
    private enum Kind { NoHole, Empty, Seed, Raw }

    private sealed record Choice(Kind Kind, byte Value, string Text)
    {
        public override string ToString() => Text;
    }

    private sealed record BonusChoice(int Value, string Text)
    {
        public override string ToString() => Text;
    }

    private sealed record FamilyChoice(List<SocketBonus>? Family, string Text)
    {
        public override string ToString() => Text;
    }

    private readonly SocketData _data;
    private readonly NumericUpDown _holes = new() { Minimum = 0, Maximum = SocketData.MaxHoles, Width = 45 };
    private readonly ComboBox _recipe = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox[] _opt = new ComboBox[SocketData.MaxHoles];
    private readonly ComboBox[] _lvl = new ComboBox[SocketData.MaxHoles];
    private readonly ComboBox _bonus = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _force = new() { Text = "Editar igual (el item no figura en SocketItemType.xml)", AutoSize = true };
    private readonly Label _info = new() { AutoSize = false, ForeColor = Color.DimGray };

    private readonly byte[] _state = [0xFF, 0xFF, 0xFF, 0xFF, 0xFF];
    private int _cat;
    private bool _socketItem;
    private int _bonusByte = SocketData.NoHole;
    private int _familyKey = -1;
    private bool _touched;
    private bool _loading;

    public event EventHandler? Changed;

    /// <summary>True when sockets were edited; untouched items keep their original bytes.</summary>
    public bool Touched => _touched;
    public bool Editable => _socketItem || _force.Checked;

    public SocketEditor(SocketData data, int width = 480)
    {
        _data = data;
        Width = width;
        Height = 284;
        var comboW = width - 55 - 6 - 110;

        Controls.Add(new Label { Text = "Huecos", Location = new Point(0, 4), AutoSize = true });
        _holes.Location = new Point(55, 1);
        _recipe.Location = new Point(110, 1);
        _recipe.Width = width - 110;
        Controls.AddRange([_holes, _recipe]);

        for (var i = 0; i < SocketData.MaxHoles; i++)
        {
            var y = 32 + i * 28;
            Controls.Add(new Label { Text = $"Hueco {i + 1}", Location = new Point(0, y + 3), AutoSize = true });
            _opt[i] = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(55, y), Width = comboW };
            _lvl[i] = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(55 + comboW + 6, y), Width = 110 };
            var n = i;
            _opt[i].SelectedIndexChanged += (_, _) => OptionPicked(n);
            _lvl[i].SelectedIndexChanged += (_, _) => LevelPicked(n);
            Controls.AddRange([_opt[i], _lvl[i]]);
        }

        var by = 32 + SocketData.MaxHoles * 28;
        Controls.Add(new Label { Text = "Bonus", Location = new Point(0, by + 3), AutoSize = true });
        _bonus.Location = new Point(55, by);
        _bonus.Width = width - 55;
        _force.Location = new Point(0, by + 30);
        _info.Location = new Point(0, by + 52);
        _info.Size = new Size(width, 64);
        Controls.AddRange([_bonus, _force, _info]);

        _holes.ValueChanged += (_, _) => HolesChanged();
        _recipe.SelectedIndexChanged += (_, _) => RecipePicked();
        _bonus.SelectedIndexChanged += (_, _) =>
        {
            if (_loading || _bonus.SelectedItem is not BonusChoice b) return;
            _bonusByte = b.Value;
            Touch();
        };
        _force.CheckedChanged += (_, _) => { if (!_loading) Rebuild(); };
    }

    /// <summary>Loads the sockets of <paramref name="item"/> (category decides which seeds fit).</summary>
    public void LoadFrom(MuItem item)
    {
        _cat = item.Cat;
        _socketItem = _data.IsSocketItem(item.Cat, item.Index);
        item.Sockets.CopyTo(_state, 0);
        _bonusByte = _socketItem ? item.Harmony : SocketData.NoHole;
        _touched = false;
        _loading = true;
        _force.Checked = false;
        _force.Visible = !_socketItem;
        var last = Array.FindLastIndex(_state, b => b != SocketData.NoHole);
        _holes.Value = last + 1;
        _loading = false;
        _familyKey = _data.MatchingFamily(_cat, _state)?[0].Index ?? -1;
        if (_socketItem) DropInvalidSeeds();
        Rebuild();
    }

    /// <summary>Writes sockets (and the bonus byte for socket items) into <paramref name="item"/>.</summary>
    public void ApplyTo(MuItem item, bool force = false)
    {
        if (!_touched && !force) return;
        var socketItem = _data.IsSocketItem(item.Cat, item.Index);
        if (!socketItem && !_force.Checked && !force) return;
        for (var i = 0; i < SocketData.MaxHoles; i++) item.SetSocket(i, _state[i]);
        if (socketItem) item.Harmony = _bonusByte;
    }

    public bool ForceChecked => _force.Checked;

    /// <summary>Clears all holes (used by the set dialog).</summary>
    public void Reset(int cat, bool socketItem)
    {
        Array.Fill(_state, SocketData.NoHole);
        _bonusByte = SocketData.NoHole;
        _loading = true;
        _holes.Value = 0;
        _force.Checked = false;
        _loading = false;
        Retarget(cat, socketItem);
        _touched = false;
    }

    /// <summary>Switches the target item type keeping the configured holes.</summary>
    public void Retarget(int cat, bool socketItem)
    {
        _cat = cat;
        _socketItem = socketItem;
        _force.Visible = !socketItem;
        if (socketItem) DropInvalidSeeds();
        _familyKey = -1;
        AfterStateChange();
    }

    public string Summary()
    {
        var parts = _state.Where(b => b != SocketData.NoHole).Select(_data.Describe).ToList();
        if (parts.Count == 0) return "sin sockets";
        var bonus = _data.BonusByIndex(_cat, _bonusByte);
        return string.Join(" | ", parts) + (bonus is null ? "" : $" | Bonus: {bonus.Name} +{bonus.Value}");
    }

    private void Touch()
    {
        _touched = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Seeds with the wrong element (e.g. after switching weapon → armor) become empty holes.</summary>
    private void DropInvalidSeeds()
    {
        var seen = new HashSet<int>();
        for (var i = 0; i < SocketData.MaxHoles; i++)
        {
            if (!_data.TryDecode(_state[i], out var o, out _)) continue;
            if (!SocketData.ElementFits(_cat, o.Element) || !seen.Add(o.Index))
            {
                _state[i] = SocketData.EmptyHole;
                _touched = true;
            }
        }
    }

    private HashSet<int> UsedOptions(int exceptHole)
    {
        var used = new HashSet<int>();
        for (var i = 0; i < SocketData.MaxHoles; i++)
            if (i != exceptHole && _data.TryDecode(_state[i], out var o, out _)) used.Add(o.Index);
        return used;
    }

    private void HolesChanged()
    {
        if (_loading) return;
        var n = (int)_holes.Value;
        for (var i = 0; i < SocketData.MaxHoles; i++)
        {
            if (i < n && _state[i] == SocketData.NoHole) _state[i] = SocketData.EmptyHole;
            if (i >= n) _state[i] = SocketData.NoHole;
        }
        AfterStateChange();
    }

    private void OptionPicked(int hole)
    {
        if (_loading || _opt[hole].SelectedItem is not Choice c) return;
        var prevLevel = _data.TryDecode(_state[hole], out _, out var lv) ? lv : SocketData.MaxLevel;
        _state[hole] = c.Kind == Kind.Seed ? SocketData.Encode(c.Value, prevLevel) : c.Value;
        AfterStateChange();
    }

    private void LevelPicked(int hole)
    {
        if (_loading || _lvl[hole].SelectedIndex < 0) return;
        if (!_data.TryDecode(_state[hole], out var o, out _)) return;
        _state[hole] = SocketData.Encode(o.Index, _lvl[hole].SelectedIndex + 1);
        AfterStateChange();
    }

    private void RecipePicked()
    {
        if (_loading || _recipe.SelectedItem is not FamilyChoice { Family: { } family }) return;
        var req = family[0].ReqElements;
        var level = Enumerable.Range(0, SocketData.MaxHoles)
            .Select(i => _data.TryDecode(_state[i], out _, out var l) ? l : 0).DefaultIfEmpty(0).Max();
        if (level == 0) level = SocketData.MaxLevel;
        for (var i = 0; i < req.Length; i++)
        {
            if (req[i] == 0) continue;
            if (_data.TryDecode(_state[i], out var cur, out _) && cur.Element == req[i]) continue;
            var used = UsedOptions(i);
            var pick = _data.OptionsFor(_cat).FirstOrDefault(o => o.Element == req[i] && !used.Contains(o.Index));
            if (pick is not null) _state[i] = SocketData.Encode(pick.Index, level);
        }
        var needed = Array.FindLastIndex(req, e => e != 0) + 1;
        if (_holes.Value < needed)
        {
            _loading = true;
            _holes.Value = needed;
            _loading = false;
        }
        AfterStateChange();
    }

    private void AfterStateChange()
    {
        if (_socketItem)
        {
            var family = _data.MatchingFamily(_cat, _state);
            var key = family?[0].Index ?? -1;
            if (family is null) _bonusByte = SocketData.NoHole;
            else if (key != _familyKey || !family.Any(b => b.Index == _bonusByte))
                _bonusByte = family[_data.SuggestedTier(family, _state) - 1].Index;
            _familyKey = key;
        }
        Rebuild();
        Touch();
    }

    private void Rebuild()
    {
        _loading = true;
        var editable = Editable;
        var holes = (int)_holes.Value;
        _holes.Enabled = editable;

        for (var i = 0; i < SocketData.MaxHoles; i++)
        {
            var combo = _opt[i];
            combo.BeginUpdate();
            combo.Items.Clear();
            var b = _state[i];
            Choice? selected = null;
            if (b == SocketData.NoHole)
                combo.Items.Add(selected = new Choice(Kind.NoHole, SocketData.NoHole, "— sin hueco —"));
            var empty = new Choice(Kind.Empty, SocketData.EmptyHole, "Hueco vacío");
            combo.Items.Add(empty);
            if (b == SocketData.EmptyHole) selected = empty;

            var decoded = editable && _data.TryDecode(b, out var cur, out _) ? cur : null;
            var used = UsedOptions(i);
            foreach (var o in _data.OptionsFor(_cat).Where(o => !used.Contains(o.Index) || o == decoded))
            {
                var c = new Choice(Kind.Seed, (byte)o.Index, o.ToString());
                combo.Items.Add(c);
                if (o == decoded) selected = c;
            }
            if (selected is null)
                combo.Items.Add(selected = new Choice(Kind.Raw, b, $"Valor crudo {b} (se conserva)"));
            combo.SelectedItem = selected;
            combo.EndUpdate();
            combo.Enabled = editable && i < holes;

            var lvl = _lvl[i];
            lvl.Items.Clear();
            if (decoded is not null && _data.TryDecode(b, out _, out var level))
            {
                for (var l = 1; l <= SocketData.MaxLevel; l++) lvl.Items.Add($"Lv{l}  (+{decoded.ValueAt(l)})");
                lvl.SelectedIndex = level - 1;
            }
            lvl.Enabled = editable && i < holes && decoded is not null;
        }

        var families = _data.BonusFamilies(_cat);
        _recipe.Items.Clear();
        _recipe.Items.Add(new FamilyChoice(null, families.Count == 0 ? "Sin combinaciones para este item" : "Cargar combinación…"));
        foreach (var f in families)
            _recipe.Items.Add(new FamilyChoice(f, $"{f[0].Recipe}  →  {f[0].Name}"));
        _recipe.SelectedIndex = 0;
        _recipe.Enabled = editable && families.Count > 0;

        _bonus.Items.Clear();
        var family = _socketItem ? _data.MatchingFamily(_cat, _state) : null;
        if (!_socketItem)
        {
            _bonus.Items.Add(new BonusChoice(SocketData.NoHole, "No aplica (el byte de bonus es Harmony en este item)"));
            _bonus.SelectedIndex = 0;
        }
        else if (family is null)
        {
            _bonus.Items.Add(new BonusChoice(SocketData.NoHole, "Sin bonus (los huecos 1-3 no forman una combinación)"));
            _bonus.SelectedIndex = 0;
        }
        else
        {
            _bonus.Items.Add(new BonusChoice(SocketData.NoHole, "Sin bonus"));
            for (var t = 0; t < family.Count; t++)
                _bonus.Items.Add(new BonusChoice(family[t].Index, $"Nivel {t + 1}: {family[t].Name} +{family[t].Value}"));
            _bonus.SelectedIndex = Math.Max(0, family.FindIndex(x => x.Index == _bonusByte) + 1);
        }
        _bonus.Enabled = editable && family is not null;

        _info.Text = InfoText(families);
        _loading = false;
    }

    private string InfoText(List<List<SocketBonus>> families)
    {
        if (!_socketItem && !_force.Checked)
            return _state.Any(b => b != SocketData.NoHole)
                ? "Este item no es de socket: sus bytes se conservan tal cual (en S21 los sets Mastery los usan para otras opciones)."
                : "Este item no es de socket según SocketItemType.xml.";
        var kind = SocketData.IsWeaponCat(_cat) ? "Arma: sólo Fire, Ice y Lightning." :
            SocketData.IsArmorCat(_cat) ? "Armadura/escudo: sólo Water, Wind y Earth." : "";
        var recipes = families.Count == 0 ? "" :
            " Combinaciones (huecos 1-2-3 en orden): " + string.Join(" · ", families.Select(f => $"{f[0].Recipe} = {f[0].Name}"));
        return $"{kind} Cada opción se puede montar una sola vez por item.{recipes}";
    }
}
