using Microsoft.Data.SqlClient;

namespace MuBredaEditor;

public sealed class ConnectForm : Form
{
    private readonly AppConfig _cfg;
    private readonly TextBox _server = new() { Width = 220 };
    private readonly NumericUpDown _port = new() { Minimum = 1, Maximum = 65535, Width = 80 };
    private readonly TextBox _db = new() { Width = 220 };
    private readonly TextBox _user = new() { Width = 220 };
    private readonly TextBox _pass = new() { Width = 220, UseSystemPasswordChar = true };
    private readonly TextBox _images = new() { Width = 320 };
    private readonly TextBox _itemXml = new() { Width = 320, PlaceholderText = "(vacío = el que viene dentro del editor)" };

    public ConnectForm(AppConfig cfg)
    {
        _cfg = cfg;
        Text = "Conexión a la base de datos";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ClientSize = new Size(560, 330);
        Font = new Font("Segoe UI", 9f);

        var t = new TableLayoutPanel { Location = new Point(10, 10), Size = new Size(540, 260), ColumnCount = 3 };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        void Row(string l, Control c, Control? extra = null)
        {
            var r = t.RowCount++;
            t.Controls.Add(new Label { Text = l, AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, r);
            t.Controls.Add(c, 1, r);
            if (extra is not null) t.Controls.Add(extra, 2, r);
        }

        var browseImg = new Button { Text = "…", Width = 30 };
        var browseXml = new Button { Text = "…", Width = 30 };
        Row("Servidor (IP)", _server);
        Row("Puerto", _port);
        Row("Base de datos", _db);
        Row("Usuario", _user);
        Row("Contraseña", _pass);
        Row("Imágenes items", _images, browseImg);
        Row("Item.xml", _itemXml, browseXml);

        var import = new Button { Text = "Importar de MuEditor.ini…", Location = new Point(10, 285), Width = 180 };
        var test = new Button { Text = "Probar", Location = new Point(250, 285), Width = 90 };
        var ok = new Button { Text = "Guardar", Location = new Point(350, 285), Width = 90 };
        var cancel = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Location = new Point(450, 285), Width = 90 };
        CancelButton = cancel;
        Controls.AddRange([t, import, test, ok, cancel]);

        LoadFrom(cfg);

        browseImg.Click += (_, _) =>
        {
            using var d = new FolderBrowserDialog { Description = "Carpeta Item (con subcarpetas 0..15 de imágenes)" };
            if (d.ShowDialog(this) == DialogResult.OK) _images.Text = d.SelectedPath;
        };
        browseXml.Click += (_, _) =>
        {
            using var d = new OpenFileDialog { Filter = "Item.xml|Item.xml|XML|*.xml" };
            if (d.ShowDialog(this) == DialogResult.OK) _itemXml.Text = d.FileName;
        };
        import.Click += (_, _) =>
        {
            using var d = new OpenFileDialog { Filter = "MuEditor.ini|MuEditor.ini|INI|*.ini" };
            if (d.ShowDialog(this) != DialogResult.OK) return;
            var tmp = new AppConfig();
            if (tmp.ImportFromMuEditorIni(d.FileName))
            {
                _server.Text = tmp.Server;
                _port.Value = tmp.Port;
                _db.Text = tmp.Database;
                _user.Text = tmp.User;
                _pass.Text = tmp.Password;
                var itemDir = Path.Combine(Path.GetDirectoryName(d.FileName)!, "Item");
                if (Directory.Exists(itemDir)) _images.Text = itemDir;
            }
            else MessageBox.Show("No encontré los datos de conexión en ese archivo.");
        };
        test.Click += (_, _) => Test(showOk: true);
        ok.Click += (_, _) =>
        {
            if (!Test(showOk: false) &&
                MessageBox.Show("No se pudo conectar. ¿Guardar igual?", "Conexión", MessageBoxButtons.YesNo) != DialogResult.Yes)
                return;
            SaveTo(_cfg);
            _cfg.Save();
            DialogResult = DialogResult.OK;
        };
    }

    private void LoadFrom(AppConfig c)
    {
        _server.Text = c.Server;
        _port.Value = Math.Clamp(c.Port, 1, 65535);
        _db.Text = c.Database;
        _user.Text = c.User;
        _pass.Text = c.Password;
        _images.Text = c.ImagesPath;
        _itemXml.Text = c.ItemXmlPath;
    }

    private void SaveTo(AppConfig c)
    {
        c.Server = _server.Text.Trim();
        c.Port = (int)_port.Value;
        c.Database = _db.Text.Trim();
        c.User = _user.Text.Trim();
        c.Password = _pass.Text;
        c.ImagesPath = _images.Text.Trim();
        c.ItemXmlPath = _itemXml.Text.Trim();
    }

    private bool Test(bool showOk)
    {
        var tmp = new AppConfig();
        SaveTo(tmp);
        Cursor = Cursors.WaitCursor;
        try
        {
            using var c = new SqlConnection(tmp.ConnectionString);
            c.Open();
            using var cmd = new SqlCommand("SELECT COUNT(*) FROM Character", c);
            var n = cmd.ExecuteScalar();
            if (showOk) MessageBox.Show($"Conexión OK. Personajes en la base: {n}.", "Conexión");
            return true;
        }
        catch (Exception ex)
        {
            if (showOk) MessageBox.Show("No se pudo conectar:\n\n" + ex.Message, "Conexión", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
        finally { Cursor = Cursors.Default; }
    }
}
