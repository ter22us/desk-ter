namespace Ter22.Windows;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19045))
        {
            MessageBox.Show("Ter22 Remote necesită Windows 10 22H2 (build 19045) sau Windows 11, pe 64 de biți.", "Ter22 Remote");
            return 1;
        }
        ApplicationConfiguration.Initialize();
        if (args.Length > 0)
        {
            if (args.Length != 2 || args[0] != "--configure-firewall"
                || !int.TryParse(args[1], out int port) || port is < 1024 or > 65535) return 1;
            try
            {
                string? warning = FirewallAccess.Configure(port);
                if (warning is null) return 0;
                MessageBox.Show(warning, "Ter22 Remote — Windows Firewall", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return 2;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                MessageBox.Show("Regula Windows Firewall nu a putut fi configurată. " + ex.Message,
                    "Ter22 Remote — Windows Firewall", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }
        using var instance = new Mutex(true, @"Local\Ter22.Remote", out bool created);
        if (!created)
        {
            MessageBox.Show("Ter22 Remote este deja deschis în această sesiune Windows.", "Ter22 Remote");
            return 1;
        }
        Application.Run(new MainForm());
        instance.ReleaseMutex();
        return 0;
    }
}
