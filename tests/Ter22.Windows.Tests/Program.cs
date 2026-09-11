using System.Net;
using System.Net.Sockets;
using Ter22.Core;
using Ter22.Windows;
using Ter22.Windows.Sessions;
using Ter22.Windows.UI;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        int result = 1;
        using var form = new MainForm();
        form.Shown += async (_, _) =>
        {
            try { await RunAsync(form).WaitAsync(TimeSpan.FromSeconds(90)); result = 0; }
            catch (Exception ex) { Console.Error.WriteLine($"FAIL Windows: {ex.GetType().Name}: {ex.Message}"); }
            finally { form.Close(); }
        };
        Application.Run(form);
        return result;
    }

    private static async Task RunAsync(MainForm form)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(75));
        var address = Find<ComboBox>(form, "HostAddress");
        address.SelectedIndex = -1; address.Text = "127.0.0.1";
        var port = Find<NumericUpDown>(form, "HostPort");
        var reservation = new TcpListener(IPAddress.Loopback, 0); reservation.Start();
        port.Value = ((IPEndPoint)reservation.LocalEndpoint).Port; reservation.Stop();
        var hostButton = Find<Button>(form, "StartHost");
        var share = Find<TextBox>(form, "ShareCode");
        hostButton.PerformClick();
        await Until(() => share.Text.Length > 0, timeout.Token);
        var invitation = Invitation.Parse(share.Text) with { Host = "127.0.0.1", AlternateHosts = null };

        Task<ViewerSession> denied = ViewerSession.ConnectAsync(invitation, timeout.Token);
        await Until(() => Application.OpenForms.OfType<ApprovalForm>().Any(), timeout.Token);
        Check(!denied.IsCompleted, "Acces primit înaintea aprobării locale.");
        Find<Button>(Application.OpenForms.OfType<ApprovalForm>().Single(), "Deny").PerformClick();
        try { using var unexpected = await denied; throw new Exception("Refuzul a permis accesul."); }
        catch (ConnectionFailureException ex)
        { Check(ex.Stage == ConnectionStage.Approval && ex.Message.Contains("refuzat", StringComparison.Ordinal), "Refuzul nu a fost afișat explicit."); }
        Console.WriteLine("PASS Windows: cerere reală în interfață, fără acces înaintea aprobării, refuz explicit.");

        Task<ViewerSession> connecting = ViewerSession.ConnectAsync(invitation, timeout.Token);
        await Until(() => Application.OpenForms.OfType<ApprovalForm>().Any(), timeout.Token);
        Check(!connecting.IsCompleted, "A doua cerere a ocolit aprobarea.");
        Find<Button>(Application.OpenForms.OfType<ApprovalForm>().Single(), "Allow").PerformClick();
        using (var viewer = await connecting)
        {
            viewer.Layout.Validate();
            var received = new TaskCompletionSource<(FrameHeader Header, byte[] Jpeg)>(TaskCreationOptions.RunContinuationsAsynchronously);
            Guid view = Guid.NewGuid();
            viewer.SetView(view, viewer.Layout.Displays[0].Id);
            viewer.FrameReceived += (header, jpeg) => received.TrySetResult((header, jpeg));
            viewer.Closed += message => received.TrySetException(new IOException(message));
            viewer.Start();
            try
            {
                var frame = await received.Task.WaitAsync(TimeSpan.FromSeconds(20), timeout.Token);
                Check(frame.Header.ViewId == view, "Imagine pentru altă fereastră.");
                using var buffer = new MemoryStream(frame.Jpeg);
                using var bitmap = Image.FromStream(buffer);
                Check(bitmap.Width == frame.Header.ImageWidth && bitmap.Height == frame.Header.ImageHeight,
                    "Imaginea desktopului nu se decodează la dimensiunile anunțate.");
                Console.WriteLine("PASS Windows: aprobare în interfață, TLS, monitoare și prima imagine GDI primită și decodată.");
            }
            finally { viewer.Stop(); await viewer.Completion; }
        }
        // Stopping access with a pending approval must close the dialog and unblock shutdown.
        Task<ViewerSession> pending = ViewerSession.ConnectAsync(invitation, timeout.Token);
        await Until(() => Application.OpenForms.OfType<ApprovalForm>().Any(), timeout.Token);
        hostButton.PerformClick();
        await Until(() => share.Text.Length == 0 && hostButton.Enabled
            && !Application.OpenForms.OfType<ApprovalForm>().Any(), timeout.Token);
        try { using var unexpected = await pending; throw new Exception("Oprirea accesului a permis conectarea."); }
        catch (IOException) { }
        Console.WriteLine("PASS Windows: oprirea accesului anulează cererea în curs și închide dialogul.");
    }

    private static T Find<T>(Control root, string name) where T : Control =>
        root.Controls.Find(name, true).OfType<T>().Single();
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static async Task Until(Func<bool> predicate, CancellationToken ct)
    { while (!predicate()) await Task.Delay(20, ct); }
}
