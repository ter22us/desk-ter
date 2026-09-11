namespace Ter22.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19045))
        {
            MessageBox.Show("Ter22 Remote necesită Windows 10 22H2 (build 19045) sau Windows 11, pe 64 de biți.", "Ter22 Remote");
            return;
        }
        ApplicationConfiguration.Initialize();
        using var instance = new Mutex(true, @"Local\Ter22.Remote", out bool created);
        if (!created)
        {
            MessageBox.Show("Ter22 Remote este deja deschis în această sesiune Windows.", "Ter22 Remote");
            return;
        }
        Application.Run(new MainForm());
        instance.ReleaseMutex();
    }
}
