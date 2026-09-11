namespace Ter22.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
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
