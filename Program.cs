namespace MuBredaEditor;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
            MessageBox.Show(e.Exception.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

        var cfg = AppConfig.Load();
        if (!cfg.HasConnection)
        {
            using var f = new ConnectForm(cfg);
            if (f.ShowDialog() != DialogResult.OK) return;
        }
        Application.Run(new MainForm(cfg));
    }
}
