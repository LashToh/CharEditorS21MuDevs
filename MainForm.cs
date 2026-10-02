using System.Data;

namespace MuBredaEditor;

public sealed class MainForm : Form
{
    private readonly AppConfig _cfg;
    private Db _db;
    private ItemDb _items;
    private ItemImages _images;

    private readonly TextBox _search = new() { Dock = DockStyle.Fill, PlaceholderText = "Personaje o cuenta (vacío = todos)" };
    private readonly ListView _results = new()
    {
        Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false,
    };
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly StatusStrip _status = new();
    private readonly ToolStripStatusLabel _statusText = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };

    private string? _account;
    private string? _character;

    private readonly Dictionary<string, Control> _acc = [];
    private readonly Dictionary<string, Control> _chr = [];
    private InventoryPage _inv = null!;
    private InventoryPage _ware = null!;
    private readonly NumericUpDown _wareMoney = new() { Maximum = int.MaxValue, Width = 120, ThousandsSeparator = true };
    private readonly Label _accTitle = new() { AutoSize = true, Font = new Font("Segoe UI", 12f, FontStyle.Bold) };
    private readonly Label _chrTitle = new() { AutoSize = true, Font = new Font("Segoe UI", 12f, FontStyle.Bold) };
    private readonly Label _classLabel = new() { AutoSize = true, ForeColor = Color.Gray, Padding = new Padding(6, 6, 0, 0) };
    private bool _syncClass;

    public MainForm(AppConfig cfg)
    {
        _cfg = cfg;
        _db = new Db(cfg.ConnectionString);
        _items = ItemDb.Load(cfg.ItemXmlPath);
        _images = new ItemImages(cfg.ImagesPath);

        Text = "MuBreda Editor S21";
        Size = new Size(1360, 860);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9f);

        var menu = new MenuStrip();
        var file = new ToolStripMenuItem("Archivo");
        file.DropDownItems.Add("Conexión y carpetas…", null, (_, _) => EditConnection());
        file.DropDownItems.Add("Abrir carpeta de backups", null, (_, _) =>
        {
            Directory.CreateDirectory(AppConfig.BackupDir);
            System.Diagnostics.Process.Start("explorer.exe", AppConfig.BackupDir);
        });
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add("Salir", null, (_, _) => Close());
        menu.Items.Add(file);
        MainMenuStrip = menu;

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 400, FixedPanel = FixedPanel.Panel1 };
        var searchBar = new TableLayoutPanel { Dock = DockStyle.Top, Height = 34, ColumnCount = 2, Padding = new Padding(4) };
        searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        var btnSearch = new Button { Text = "Buscar", Dock = DockStyle.Fill };
        searchBar.Controls.Add(_search, 0, 0);
        searchBar.Controls.Add(btnSearch, 1, 0);
        _results.Columns.Add("Personaje", 95);
        _results.Columns.Add("Cuenta", 85);
        _results.Columns.Add("Clase", 175);
        _results.Columns.Add("Nivel", 45);
        _results.Columns.Add("Resets", 50);
        split.Panel1.Controls.Add(_results);
        split.Panel1.Controls.Add(searchBar);
        split.Panel2.Controls.Add(_tabs);

        _tabs.TabPages.Add(BuildAccountTab());
        _tabs.TabPages.Add(BuildCharacterTab());
        _tabs.TabPages.Add(BuildInventoryTab());
        _tabs.TabPages.Add(BuildWarehouseTab());

        _status.Items.Add(_statusText);
        Controls.Add(split);
        Controls.Add(menu);
        Controls.Add(_status);

        btnSearch.Click += (_, _) => Search();
        _search.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Search(); } };
        _results.SelectedIndexChanged += (_, _) => OnResultSelected();
        FormClosing += (_, e) => { if (!ConfirmDiscard()) e.Cancel = true; };
        Shown += (_, _) => { UpdateStatus(); Search(); };
    }

    // ───────────────────────── helpers ─────────────────────────

    private void UpdateStatus(string? msg = null)
    {
        var img = _images.Available ? "imágenes OK" : "SIN imágenes (Archivo > Conexión)";
        _statusText.Text = msg ?? $"Servidor {_cfg.Server}:{_cfg.Port} / {_cfg.Database}   ·   {_items.Items.Count} items ({_items.Source})   ·   {img}";
    }

    private static NumericUpDown Num(long max = int.MaxValue, long min = 0) =>
        new() { Minimum = min, Maximum = max, Width = 130, ThousandsSeparator = true };

    private static TableLayoutPanel FieldTable()
    {
        var t = new TableLayoutPanel { AutoSize = true, ColumnCount = 4, Padding = new Padding(0, 8, 0, 0) };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        return t;
    }

    private static void Field(TableLayoutPanel t, Dictionary<string, Control> map, string key, string label, Control c, int col)
    {
        var row = col == 0 ? t.RowCount++ : t.RowCount - 1;
        t.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, col * 2, row);
        t.Controls.Add(c, col * 2 + 1, row);
        map[key] = c;
    }

    private static int I(DataRow r, string col) => r.Table.Columns.Contains(col) && r[col] is not DBNull ? Convert.ToInt32(r[col]) : 0;
    private static string S(DataRow r, string col) => r.Table.Columns.Contains(col) && r[col] is not DBNull ? Convert.ToString(r[col])!.Trim() : "";
    private decimal V(Dictionary<string, Control> m, string k) => ((NumericUpDown)m[k]).Value;
    private static void SetNum(Dictionary<string, Control> m, string k, long v)
    {
        var n = (NumericUpDown)m[k];
        n.Value = Math.Clamp(v, (long)n.Minimum, (long)n.Maximum);
    }

    private bool EnsureOffline()
    {
        if (_account is null) return false;
        if (!_db.IsAccountOnline(_account)) return true;
        MessageBox.Show($"La cuenta {_account} está ONLINE.\n\nDesconectala antes de guardar: si no, el GameServer pisa los cambios al salir.",
            "Cuenta conectada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return false;
    }

    private static void Backup(string kind, string key, byte[]? data)
    {
        if (data is null) return;
        Directory.CreateDirectory(AppConfig.BackupDir);
        File.WriteAllBytes(Path.Combine(AppConfig.BackupDir, $"{kind}_{key}_{DateTime.Now:yyyyMMdd_HHmmss}.bin"), data);
    }

    private bool ConfirmDiscard()
    {
        if (!_inv.Dirty && !_ware.Dirty) return true;
        return MessageBox.Show("Hay cambios de inventario o baúl sin guardar. ¿Descartarlos?", "Cambios sin guardar",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
    }

    private void EditConnection()
    {
        using var f = new ConnectForm(_cfg);
        if (f.ShowDialog(this) != DialogResult.OK) return;
        _db = new Db(_cfg.ConnectionString);
        _items = ItemDb.Load(_cfg.ItemXmlPath);
        _images = new ItemImages(_cfg.ImagesPath);
        MessageBox.Show("Configuración guardada. Si cambiaste imágenes o Item.xml, reiniciá el editor para aplicarlos en los inventarios.");
        UpdateStatus();
    }

    // ───────────────────────── search ─────────────────────────

    private void Search()
    {
        if (!ConfirmDiscard()) return;
        var q = "%" + _search.Text.Trim() + "%";
        try
        {
            Cursor = Cursors.WaitCursor;
            var chars = _db.Query(@"SELECT TOP 500 Name, AccountID, Class, cLevel, ResetCount FROM Character
                                    WHERE Name LIKE @p0 OR AccountID LIKE @p0 ORDER BY AccountID, Name", q);
            var lonely = _db.Query(@"SELECT TOP 100 memb___id FROM MEMB_INFO m WHERE memb___id LIKE @p0
                                     AND NOT EXISTS (SELECT 1 FROM Character c WHERE c.AccountID = m.memb___id) ORDER BY memb___id", q);
            _results.BeginUpdate();
            _results.Items.Clear();
            foreach (DataRow r in chars.Rows)
            {
                var li = new ListViewItem([S(r, "Name"), S(r, "AccountID"), GameData.ClassName(I(r, "Class")),
                    I(r, "cLevel").ToString(), I(r, "ResetCount").ToString()]) { Tag = (S(r, "AccountID"), S(r, "Name")) };
                _results.Items.Add(li);
            }
            foreach (DataRow r in lonely.Rows)
            {
                var acc = S(r, "memb___id");
                _results.Items.Add(new ListViewItem(["(sin pjs)", acc, "", "", ""]) { Tag = (acc, (string?)null), ForeColor = Color.Gray });
            }
            _results.EndUpdate();
            UpdateStatus($"{chars.Rows.Count} personajes, {lonely.Rows.Count} cuentas sin personajes.");
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error buscando:\n\n" + ex.Message, "Base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { Cursor = Cursors.Default; }
    }

    private void OnResultSelected()
    {
        if (_results.SelectedItems.Count == 0) return;
        var (acc, chr) = ((string, string?))_results.SelectedItems[0].Tag!;
        if (acc == _account && chr == _character) return;
        if (!ConfirmDiscard()) return;
        _account = acc;
        _character = chr;
        try
        {
            Cursor = Cursors.WaitCursor;
            LoadAccount();
            LoadCharacter();
            LoadWarehouse();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error cargando:\n\n" + ex.Message, "Base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally { Cursor = Cursors.Default; }
    }

    // ───────────────────────── account ─────────────────────────

    private TabPage BuildAccountTab()
    {
        var page = new TabPage("Cuenta") { Padding = new Padding(12), AutoScroll = true };
        var t = FieldTable();
        Field(t, _acc, "pwd", "Contraseña", new TextBox { Width = 130, MaxLength = 10 }, 0);
        Field(t, _acc, "mail", "Email", new TextBox { Width = 240, MaxLength = 50 }, 1);
        Field(t, _acc, "level", "VIP (AccountLevel)", Num(255), 0);
        Field(t, _acc, "expire", "VIP vence", new DateTimePicker { Width = 240, Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy HH:mm", ShowCheckBox = true }, 1);
        Field(t, _acc, "blocked", "Bloqueada", new CheckBox { Text = "Cuenta baneada (bloc_code = 1)", AutoSize = true }, 0);
        Field(t, _acc, "online", "Estado", new Label { AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 1);
        Field(t, _acc, "wcc", "WCoin (C)", Num(), 0);
        Field(t, _acc, "wcp", "WCoin (P)", Num(), 1);
        Field(t, _acc, "gob", "Goblin Points", Num(), 0);

        var save = new Button { Text = "Guardar cuenta", Width = 140, Height = 30 };
        save.Click += (_, _) => SaveAccount();
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        flow.Controls.AddRange([_accTitle, t, save]);
        page.Controls.Add(flow);
        return page;
    }

    private void LoadAccount()
    {
        _accTitle.Text = $"Cuenta: {_account}";
        var r = _db.Row("SELECT memb__pwd, mail_addr, AccountLevel, AccountExpireDate, bloc_code FROM MEMB_INFO WHERE memb___id=@p0", _account);
        var cash = _db.Row("SELECT WCoinC, WCoinP, GoblinPoint FROM CashShopData WHERE AccountID=@p0", _account);
        ((TextBox)_acc["pwd"]).Text = r is null ? "" : S(r, "memb__pwd");
        ((TextBox)_acc["mail"]).Text = r is null ? "" : S(r, "mail_addr");
        SetNum(_acc, "level", r is null ? 0 : I(r, "AccountLevel"));
        var dtp = (DateTimePicker)_acc["expire"];
        if (r is not null && r["AccountExpireDate"] is DateTime d) { dtp.Value = d; dtp.Checked = true; }
        else { dtp.Value = DateTime.Now; dtp.Checked = false; }
        ((CheckBox)_acc["blocked"]).Checked = r is not null && S(r, "bloc_code") == "1";
        var online = _db.IsAccountOnline(_account!);
        var lbl = (Label)_acc["online"];
        lbl.Text = online ? "ONLINE (no guardes cambios hasta que salga)" : "Offline";
        lbl.ForeColor = online ? Color.Firebrick : Color.ForestGreen;
        SetNum(_acc, "wcc", cash is null ? 0 : I(cash, "WCoinC"));
        SetNum(_acc, "wcp", cash is null ? 0 : I(cash, "WCoinP"));
        SetNum(_acc, "gob", cash is null ? 0 : I(cash, "GoblinPoint"));
    }

    private void SaveAccount()
    {
        if (_account is null || !EnsureOffline()) return;
        var pwd = ((TextBox)_acc["pwd"]).Text;
        if (pwd.Length is < 4 or > 10)
        {
            MessageBox.Show("La contraseña tiene que tener entre 4 y 10 caracteres.");
            return;
        }
        var dtp = (DateTimePicker)_acc["expire"];
        _db.Transaction(exec =>
        {
            exec(@"UPDATE MEMB_INFO SET memb__pwd=@p0, mail_addr=@p1, AccountLevel=@p2, AccountExpireDate=@p3, bloc_code=@p4
                   WHERE memb___id=@p5",
                [pwd, ((TextBox)_acc["mail"]).Text.Trim(), (int)V(_acc, "level"), dtp.Checked ? dtp.Value : null,
                 ((CheckBox)_acc["blocked"]).Checked ? "1" : "0", _account]);
            var n = exec("UPDATE CashShopData SET WCoinC=@p0, WCoinP=@p1, GoblinPoint=@p2 WHERE AccountID=@p3",
                [(int)V(_acc, "wcc"), (int)V(_acc, "wcp"), (int)V(_acc, "gob"), _account]);
            if (n == 0)
                exec("INSERT INTO CashShopData (AccountID, WCoinC, WCoinP, GoblinPoint) VALUES (@p0, @p1, @p2, @p3)",
                    [_account, (int)V(_acc, "wcc"), (int)V(_acc, "wcp"), (int)V(_acc, "gob")]);
        });
        UpdateStatus($"Cuenta {_account} guardada.");
    }

    // ───────────────────────── character ─────────────────────────

    private TabPage BuildCharacterTab()
    {
        var page = new TabPage("Personaje") { Padding = new Padding(12), AutoScroll = true };
        var race = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        race.Items.AddRange(GameData.Races.Select((n, i) => (object)$"{i} - {n}").ToArray());
        var evo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300, DropDownWidth = 360 };
        var classBox = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        classBox.Controls.AddRange(
        [
            new Label { Text = "Clase", AutoSize = true, Padding = new Padding(0, 6, 6, 0) },
            race,
            new Label { Text = "Evolución", AutoSize = true, Padding = new Padding(8, 6, 4, 0) },
            evo, _classLabel,
        ]);
        _chr["race"] = race;
        _chr["evo"] = evo;
        race.SelectedIndexChanged += (_, _) =>
        {
            if (_syncClass) return;
            var prev = evo.SelectedItem as EvoItem;
            var raceIx = Math.Max(0, race.SelectedIndex);
            int bits;
            if (prev?.Stage is int stage)
            {
                var step = GameData.Evolutions(raceIx).FirstOrDefault(s => s.Stage == stage);
                bits = step.Stage == stage ? step.Code : 0;
            }
            else bits = prev?.Bits ?? 0;
            SelectEvolution(bits);
        };
        evo.SelectedIndexChanged += (_, _) => { if (!_syncClass) UpdateClassLabel(); };
        race.SelectedIndex = 0;

        var map = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 180 };
        map.Items.AddRange(GameData.Maps.OrderBy(m => m.Key).Select(m => (object)$"{m.Key} - {m.Value}").ToArray());

        var t = FieldTable();
        var classRow = t.RowCount++;
        t.Controls.Add(classBox, 0, classRow);
        t.SetColumnSpan(classBox, 4);

        Field(t, _chr, "level", "Nivel", Num(2000), 0);
        Field(t, _chr, "points", "Puntos libres", Num(), 1);
        Field(t, _chr, "str", "Fuerza", Num(), 0);
        Field(t, _chr, "agi", "Agilidad", Num(), 1);
        Field(t, _chr, "vit", "Vitalidad", Num(), 0);
        Field(t, _chr, "ene", "Energía", Num(), 1);
        Field(t, _chr, "cmd", "Comando", Num(), 0);
        Field(t, _chr, "zen", "Zen", Num(), 1);
        Field(t, _chr, "ruud", "Ruud", Num(), 0);
        Field(t, _chr, "resets", "Resets", Num(), 1);
        Field(t, _chr, "mresets", "Master resets", Num(), 0);
        Field(t, _chr, "mlevel", "Master level", Num(2000), 1);
        Field(t, _chr, "mpoints", "Master puntos", Num(), 0);
        Field(t, _chr, "map", "Mapa", map, 1);
        Field(t, _chr, "x", "Posición X", Num(255), 0);
        Field(t, _chr, "y", "Posición Y", Num(255), 1);
        Field(t, _chr, "pklevel", "PK level", Num(255), 0);
        Field(t, _chr, "pkcount", "PK count", Num(), 1);
        Field(t, _chr, "ctl", "CtlCode", Num(255), 0);
        t.Controls.Add(new Label
        {
            Text = "0 = normal · 1 = bloqueado · GM suele ser 8 o 32", AutoSize = true, ForeColor = Color.Gray,
            Padding = new Padding(0, 6, 0, 0),
        }, 2, t.RowCount - 1);
        t.SetColumnSpan(t.GetControlFromPosition(2, t.RowCount - 1)!, 2);

        var tools = new FlowLayoutPanel { AutoSize = true };
        var save = new Button { Text = "Guardar personaje", Width = 150, Height = 30 };
        var toLoren = new Button { Text = "Mover a Lorencia", Width = 130, Height = 30 };
        var resetStats = new Button { Text = "Devolver stats a puntos", Width = 170, Height = 30 };
        tools.Controls.AddRange([save, toLoren, resetStats]);
        save.Click += (_, _) => SaveCharacter();
        toLoren.Click += (_, _) => { map.Text = "0 - Lorencia"; SetNum(_chr, "x", 130); SetNum(_chr, "y", 130); };
        resetStats.Click += (_, _) => ResetStats();

        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        flow.Controls.AddRange([_chrTitle, t, tools]);
        page.Controls.Add(flow);
        return page;
    }

    private void ResetStats()
    {
        if (_character is null) return;
        var race = Math.Max(0, ((ComboBox)_chr["race"]).SelectedIndex);
        var def = _db.Row("SELECT Strength, Dexterity, Vitality, Energy, Leadership FROM DefaultClassType WHERE Class=@p0", race * 16);
        if (def is null) { MessageBox.Show("No encontré los stats base de esa clase en DefaultClassType."); return; }
        var extra = (long)(V(_chr, "str") - I(def, "Strength")) + (long)(V(_chr, "agi") - I(def, "Dexterity")) +
                    (long)(V(_chr, "vit") - I(def, "Vitality")) + (long)(V(_chr, "ene") - I(def, "Energy")) +
                    (long)(V(_chr, "cmd") - I(def, "Leadership"));
        if (extra <= 0) return;
        SetNum(_chr, "str", I(def, "Strength"));
        SetNum(_chr, "agi", I(def, "Dexterity"));
        SetNum(_chr, "vit", I(def, "Vitality"));
        SetNum(_chr, "ene", I(def, "Energy"));
        SetNum(_chr, "cmd", I(def, "Leadership"));
        SetNum(_chr, "points", (long)V(_chr, "points") + extra);
        UpdateStatus($"Se devolvieron {extra:N0} puntos (falta Guardar personaje).");
    }

    private void LoadCharacter()
    {
        _tabs.TabPages[1].Enabled = _tabs.TabPages[2].Enabled = _character is not null;
        if (_character is null)
        {
            _chrTitle.Text = "Esta cuenta no tiene personajes";
            _inv.Bind(null);
            return;
        }
        var r = _db.Row("SELECT * FROM Character WHERE Name=@p0", _character)
                ?? throw new InvalidOperationException($"No existe el personaje {_character}.");
        var m = _db.Row("SELECT MasterLevel, MasterPoint FROM MasterSkillTree WHERE Name=@p0", _character);

        var cls = I(r, "Class");
        _chrTitle.Text = $"{_character}  ·  {GameData.ClassName(cls)}";
        _syncClass = true;
        ((ComboBox)_chr["race"]).SelectedIndex = Math.Clamp(cls >> 4, 0, GameData.Races.Length - 1);
        _syncClass = false;
        SelectEvolution(cls & 0x0F);
        SetNum(_chr, "level", I(r, "cLevel"));
        SetNum(_chr, "points", I(r, "LevelUpPoint"));
        SetNum(_chr, "str", I(r, "Strength"));
        SetNum(_chr, "agi", I(r, "Dexterity"));
        SetNum(_chr, "vit", I(r, "Vitality"));
        SetNum(_chr, "ene", I(r, "Energy"));
        SetNum(_chr, "cmd", I(r, "Leadership"));
        SetNum(_chr, "zen", I(r, "Money"));
        SetNum(_chr, "ruud", I(r, "RuudMoney"));
        SetNum(_chr, "resets", I(r, "ResetCount"));
        SetNum(_chr, "mresets", I(r, "MasterResetCount"));
        SetNum(_chr, "mlevel", m is null ? 0 : I(m, "MasterLevel"));
        SetNum(_chr, "mpoints", m is null ? 0 : I(m, "MasterPoint"));
        _chr["mlevel"].Enabled = _chr["mpoints"].Enabled = m is not null;
        var map = I(r, "MapNumber");
        ((ComboBox)_chr["map"]).Text = $"{map} - {GameData.MapName(map)}";
        SetNum(_chr, "x", I(r, "MapPosX"));
        SetNum(_chr, "y", I(r, "MapPosY"));
        SetNum(_chr, "pklevel", I(r, "PkLevel"));
        SetNum(_chr, "pkcount", I(r, "PkCount"));
        SetNum(_chr, "ctl", I(r, "CtlCode"));

        _inv.Bind(r["Inventory"] as byte[]);
        _tabs.TabPages[2].Text = $"Inventario ({_character})";
    }

    private void SaveCharacter()
    {
        if (_character is null || !EnsureOffline()) return;
        var mapText = ((ComboBox)_chr["map"]).Text;
        if (!int.TryParse(mapText.Split('-')[0].Trim(), out var map))
        {
            MessageBox.Show("Mapa inválido: escribí el número de mapa.");
            return;
        }
        var cls = Math.Max(0, ((ComboBox)_chr["race"]).SelectedIndex) * 16 +
                  (((ComboBox)_chr["evo"]).SelectedItem is EvoItem evo ? evo.Bits : 0);
        _db.Transaction(exec =>
        {
            exec(@"UPDATE Character SET Class=@p0, cLevel=@p1, LevelUpPoint=@p2, Strength=@p3, Dexterity=@p4, Vitality=@p5,
                     Energy=@p6, Leadership=@p7, Money=@p8, RuudMoney=@p9, ResetCount=@p10, MasterResetCount=@p11,
                     MapNumber=@p12, MapPosX=@p13, MapPosY=@p14, PkLevel=@p15, PkCount=@p16, CtlCode=@p17
                   WHERE Name=@p18",
                [cls, (int)V(_chr, "level"), (int)V(_chr, "points"), (int)V(_chr, "str"), (int)V(_chr, "agi"),
                 (int)V(_chr, "vit"), (int)V(_chr, "ene"), (int)V(_chr, "cmd"), (int)V(_chr, "zen"), (int)V(_chr, "ruud"),
                 (int)V(_chr, "resets"), (int)V(_chr, "mresets"), map, (int)V(_chr, "x"), (int)V(_chr, "y"),
                 (int)V(_chr, "pklevel"), (int)V(_chr, "pkcount"), (int)V(_chr, "ctl"), _character]);
            if (_chr["mlevel"].Enabled)
                exec("UPDATE MasterSkillTree SET MasterLevel=@p0, MasterPoint=@p1 WHERE Name=@p2",
                    [(int)V(_chr, "mlevel"), (int)V(_chr, "mpoints"), _character]);
        });
        _chrTitle.Text = $"{_character}  ·  {GameData.ClassName(cls)}";
        UpdateStatus($"Personaje {_character} guardado.");
    }

    // ───────────────────────── inventory / warehouse ─────────────────────────

    private TabPage BuildInventoryTab()
    {
        var page = new TabPage("Inventario");
        _inv = new InventoryPage(_items, _images, Layouts.Character, withEquipment: true);
        _inv.SaveButton.Text = "Guardar inventario";
        _inv.SaveButton.Width = 140;
        var giveSet = new Button { Text = "Dar set…", Width = 100, Height = 28 };
        _inv.Toolbar.Controls.Add(giveSet);
        _inv.SaveButton.Click += (_, _) => SaveInventory();
        _inv.ReloadButton.Click += (_, _) => { if (ConfirmDiscard()) LoadCharacter(); };
        giveSet.Click += (_, _) => GiveSet();
        page.Controls.Add(_inv);
        return page;
    }

    private void GiveSet()
    {
        if (_inv.Box is null) return;
        using var f = new SetForm(_items);
        if (f.ShowDialog(this) != DialogResult.OK || f.Items.Count == 0) return;
        var placed = 0;
        foreach (var (it, equipSlot) in f.Items)
        {
            var equip = f.ToEquipment && equipSlot >= 0;
            var slot = equip ? equipSlot : _inv.FindFree(it);
            if (slot >= 0 && _inv.TryPlace(slot, it, ignoreSlot: equip ? slot : -1, quiet: true)) placed++;
        }
        var msg = $"Set agregado: {placed} de {f.Items.Count} piezas.";
        if (placed < f.Items.Count) msg += " No hubo lugar para el resto.";
        MessageBox.Show(msg + "\n\nAcordate de tocar \"Guardar inventario\".", "Dar set");
    }

    private void SaveInventory()
    {
        if (_character is null || _inv.Box is null || !EnsureOffline()) return;
        var current = _db.Scalar("SELECT Inventory FROM Character WHERE Name=@p0", _character) as byte[];
        if (current is not null && current.Length != _inv.Box.Data.Length)
        {
            MessageBox.Show("El tamaño del inventario en la base cambió. Recargá antes de guardar.");
            return;
        }
        Backup("inventario", _character, current);
        _db.Exec("UPDATE Character SET Inventory=@p0 WHERE Name=@p1", _inv.Box.Data, _character);
        _inv.MarkSaved();
        UpdateStatus($"Inventario de {_character} guardado (backup en la carpeta Backups).");
    }

    private TabPage BuildWarehouseTab()
    {
        var page = new TabPage("Baúl");
        _ware = new InventoryPage(_items, _images, Layouts.Warehouse, withEquipment: false);
        _ware.SaveButton.Text = "Guardar baúl";
        _ware.Toolbar.Controls.Add(new Label { Text = "Zen del baúl", AutoSize = true, Padding = new Padding(12, 7, 0, 0) });
        _ware.Toolbar.Controls.Add(_wareMoney);
        _ware.SaveButton.Click += (_, _) => SaveWarehouse();
        _ware.ReloadButton.Click += (_, _) => { if (ConfirmDiscard()) LoadWarehouse(); };
        page.Controls.Add(_ware);
        return page;
    }

    private void LoadWarehouse()
    {
        var r = _db.Row("SELECT Items, Money FROM warehouse WHERE AccountID=@p0", _account);
        _ware.Bind(r?["Items"] as byte[]);
        _wareMoney.Value = r is null ? 0 : Math.Clamp(I(r, "Money"), 0, int.MaxValue);
        _tabs.TabPages[3].Text = r is null ? "Baúl (no creado)" : $"Baúl ({_account})";
    }

    private void SaveWarehouse()
    {
        if (_account is null || _ware.Box is null)
        {
            MessageBox.Show("Esta cuenta todavía no tiene baúl: se crea la primera vez que lo abre en el juego.");
            return;
        }
        if (!EnsureOffline()) return;
        var current = _db.Scalar("SELECT Items FROM warehouse WHERE AccountID=@p0", _account) as byte[];
        Backup("baul", _account, current);
        _db.Exec("UPDATE warehouse SET Items=@p0, Money=@p1 WHERE AccountID=@p2", _ware.Box.Data, (int)_wareMoney.Value, _account);
        _ware.MarkSaved();
        UpdateStatus($"Baúl de {_account} guardado (backup en la carpeta Backups).");
    }

    private void SelectEvolution(int bits)
    {
        var race = Math.Max(0, ((ComboBox)_chr["race"]).SelectedIndex);
        var evo = (ComboBox)_chr["evo"];
        var steps = GameData.Evolutions(race);
        _syncClass = true;
        evo.BeginUpdate();
        evo.Items.Clear();
        foreach (var s in steps)
            evo.Items.Add(new EvoItem(s.Code, s.Stage, s.Label));
        if (!steps.Any(s => s.Code == bits))
            evo.Items.Add(new EvoItem(bits, null, $"código {bits} (desconocido)"));
        for (var i = 0; i < evo.Items.Count; i++)
            if (((EvoItem)evo.Items[i]!).Bits == bits) { evo.SelectedIndex = i; break; }
        evo.EndUpdate();
        _syncClass = false;
        UpdateClassLabel();
    }

    private void UpdateClassLabel()
    {
        var race = Math.Max(0, ((ComboBox)_chr["race"]).SelectedIndex);
        var bits = ((ComboBox)_chr["evo"]).SelectedItem is EvoItem e ? e.Bits : 0;
        _classLabel.Text = $"Class = {race * 16 + bits}";
    }

    private sealed class EvoItem(int bits, int? stage, string text)
    {
        public int Bits { get; } = bits;
        public int? Stage { get; } = stage;
        public override string ToString() => text;
    }
}
