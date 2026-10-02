using Microsoft.Data.SqlClient;

namespace MuBredaEditor;

public sealed class AppConfig
{
    public string Server { get; set; } = "";
    public int Port { get; set; } = 1433;
    public string Database { get; set; } = "MuOnline";
    public string User { get; set; } = "sa";
    public string Password { get; set; } = "";
    public string ImagesPath { get; set; } = "";
    public string ItemXmlPath { get; set; } = "";

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "MuBredaEditor.ini");
    public static string BackupDir => Path.Combine(AppContext.BaseDirectory, "Backups");

    public bool HasConnection => Server.Length > 0 && User.Length > 0;

    public string ConnectionString => new SqlConnectionStringBuilder
    {
        DataSource = $"{Server},{Port}",
        InitialCatalog = Database,
        UserID = User,
        Password = Password,
        Encrypt = SqlConnectionEncryptOption.Optional,
        TrustServerCertificate = true,
        ConnectTimeout = 10,
    }.ConnectionString;

    public static AppConfig Load()
    {
        var c = new AppConfig();
        if (File.Exists(FilePath))
        {
            var v = ReadIni(FilePath);
            c.Server = v.GetValueOrDefault("server", "");
            c.Port = int.TryParse(v.GetValueOrDefault("port"), out var p) ? p : 1433;
            c.Database = v.GetValueOrDefault("database", "MuOnline");
            c.User = v.GetValueOrDefault("user", "sa");
            c.Password = v.GetValueOrDefault("password", "");
            c.ImagesPath = v.GetValueOrDefault("imagespath", "");
            c.ItemXmlPath = v.GetValueOrDefault("itemxmlpath", "");
        }
        if (c.ImagesPath.Length == 0)
            c.ImagesPath = Path.Combine(AppContext.BaseDirectory, "Item");
        return c;
    }

    public void Save()
    {
        File.WriteAllLines(FilePath,
        [
            "[Conexion]",
            $"Server={Server}",
            $"Port={Port}",
            $"Database={Database}",
            $"User={User}",
            $"Password={Password}",
            "",
            "[Datos]",
            "; Carpeta con las imagenes de items: Item\\<categoria>\\<indice>.png",
            $"ImagesPath={ImagesPath}",
            "; Item.xml del server (vacio = usar el que viene dentro del editor)",
            $"ItemXmlPath={ItemXmlPath}",
        ]);
    }

    public bool ImportFromMuEditorIni(string path)
    {
        var v = ReadIni(path);
        if (!v.ContainsKey("server")) return false;
        Server = v["server"];
        Port = int.TryParse(v.GetValueOrDefault("port"), out var p) ? p : 1433;
        Database = v.GetValueOrDefault("mu_db", "MuOnline");
        User = v.GetValueOrDefault("user", "sa");
        Password = v.GetValueOrDefault("pass", "");
        return true;
    }

    private static Dictionary<string, string> ReadIni(string path)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == ';' || line[0] == '[') continue;
            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            d[line[..eq].Trim()] = line[(eq + 1)..].Trim();
        }
        return d;
    }
}
