using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Ter22.Core;
using Ter22.Windows.Desktop;
using Ter22.Windows.Sessions;
using Ter22.Windows.UI;

namespace Ter22.Windows;

internal sealed class MainForm : Form
{
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly TextBox _address = new() { Width = 260 };
    private readonly NumericUpDown _port = new() { Minimum = 1024, Maximum = 65535, Value = 45990, Width = 100 };
    private readonly TextBox _relayCode = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    private readonly CheckBox _approval = new() { Text = "Solicită acceptare locală pentru fiecare conexiune", Checked = true, AutoSize = true };
    private readonly Button _hostButton = new() { Text = "Pornește accesul", AutoSize = true };
    private readonly TextBox _share = new() { ReadOnly = true, Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    private readonly Button _copy = new() { Text = "Copiază codul privat", AutoSize = true, Enabled = false };
    private readonly TextBox _connection = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    private readonly Button _connect = new() { Text = "Conectează-te", AutoSize = true };
    private readonly TextBox _status = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly CancellationTokenSource _closing = new();
    private CancellationTokenSource? _hostStop;
    private Task? _hostTask;
    private ViewerSession? _viewer;
    private bool _allowClose;
    private bool _connecting;
    private bool _starting;

    public MainForm()
    {
        Text = "Ter22 Remote"; ClientSize = new Size(920, 740); MinimumSize = new Size(740, 680);
        StartPosition = FormStartPosition.CenterScreen; AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(245, 247, 250);
        _mode.Items.AddRange(["Direct — LAN / VPN", "Internet — releu propriu"]); _mode.SelectedIndex = 0;
        _address.Text = LocalAddress();
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 7 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 300));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 106));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        var title = new Label { Text = "Ter22 Remote\nCalculatoarele tale. Spațiul tău de lucru.", Dock = DockStyle.Fill, Font = new Font("Segoe UI", 14) };
        root.Controls.Add(title, 0, 0);
        var host = new GroupBox { Text = "Permite accesul la acest calculator", Dock = DockStyle.Fill, Padding = new Padding(12) };
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 7 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 205)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 6; i++) table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.Controls.Add(LabelFor("Tip conexiune"), 0, 0); table.Controls.Add(_mode, 1, 0);
        table.Controls.Add(LabelFor("Adresa acestui PC (LAN/VPN)"), 0, 1);
        var addressBar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        addressBar.Controls.AddRange([_address, _port]); table.Controls.Add(addressBar, 1, 1);
        table.Controls.Add(LabelFor("Cod de configurare releu"), 0, 2); table.Controls.Add(_relayCode, 1, 2);
        table.Controls.Add(_approval, 1, 3); table.Controls.Add(_hostButton, 0, 4);
        table.Controls.Add(new Label { Text = "Accesul funcționează cât timp aplicația este deschisă și desktopul Windows este deblocat.", AutoSize = true }, 1, 4);
        table.Controls.Add(_copy, 0, 5); table.Controls.Add(_share, 1, 5);
        table.Controls.Add(new Label { Text = "Codul permite acces la ecrane, mouse și tastatură. Îl introduci numai pe calculatoarele tale. Oprirea accesului îl invalidează.", Dock = DockStyle.Fill }, 1, 6);
        host.Controls.Add(table); root.Controls.Add(host, 0, 1);
        var viewer = new GroupBox { Text = "Lucrează pe alt calculator", Dock = DockStyle.Fill, Padding = new Padding(12) };
        var connectTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        connectTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); connectTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 155));
        connectTable.Controls.Add(new Label { Text = "Lipește codul privat generat pe calculatorul controlat:", Dock = DockStyle.Fill }, 0, 0);
        connectTable.Controls.Add(_connection, 0, 1); connectTable.Controls.Add(_connect, 1, 1);
        viewer.Controls.Add(connectTable); root.Controls.Add(viewer, 0, 3);
        root.Controls.Add(new Label { Text = "Monitoare: selectare individuală, toate ecranele sau câte o fereastră pentru fiecare.\nControlul ferestrelor UAC și accesul înainte de autentificare nu sunt incluse în versiunea 0.1.", Dock = DockStyle.Fill }, 0, 4);
        root.Controls.Add(_status, 0, 5);
        root.Controls.Add(new Label { Text = "v0.1 · Windows x64 · F12 eliberează controlul în fereastra desktopului la distanță", Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0) }, 0, 6);
        Controls.Add(root);
        _mode.SelectedIndexChanged += (_, _) => UpdateMode(); UpdateMode();
        _hostButton.Click += async (_, _) => { if (_hostStop is null) await StartHostAsync(); else await StopHostAsync(); };
        _copy.Click += (_, _) =>
        {
            try { if (_share.Text.Length > 0) Clipboard.SetText(_share.Text); }
            catch (System.Runtime.InteropServices.ExternalException) { Log("Clipboard ocupat. Încearcă din nou."); }
        };
        _connect.Click += async (_, _) => await ConnectAsync();
        FormClosing += ClosingAsync;
    }
    private static Label LabelFor(string text) => new() { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private void UpdateMode()
    {
        bool idle = _hostStop is null, relay = _mode.SelectedIndex == 1;
        _mode.Enabled = idle; _address.Enabled = idle && !relay; _port.Enabled = idle && !relay;
        _relayCode.Enabled = idle && relay; _approval.Enabled = idle;
    }
    private async Task StartHostAsync()
    {
        _starting = true;
        HostSession? host = null;
        try
        {
            RelayAddress? relay = _mode.SelectedIndex == 1 ? RelayAddress.Parse(_relayCode.Text) : null;
            string address = _address.Text.Trim(); int port = (int)_port.Value;
            if (relay is null) Codes.ValidateHost(address, port);
            _hostButton.Enabled = false;
            bool requireApproval = _approval.Checked;
            host = await Task.Run(() => new HostSession(new ScreenCapture())
            {
                Status = Log, Approve = requireApproval ? RequestApprovalAsync : null
            });
            _hostStop = CancellationTokenSource.CreateLinkedTokenSource(_closing.Token);
            var token = _hostStop.Token;
            _share.Text = host.Invitation(address, port, relay).ToCode(); _copy.Enabled = true;
            _hostButton.Text = "Oprește accesul"; _hostButton.Enabled = true; UpdateMode();
            var activeHost = host;
            _hostTask = Task.Run(() => activeHost.RunAsync(port, relay, token));
            try { await _hostTask; }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            { if (!token.IsCancellationRequested) Log(HostSession.SafeMessage(ex)); }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Log(HostSession.SafeMessage(ex)); }
        finally
        {
            host?.Dispose(); _hostStop?.Dispose(); _hostStop = null; _hostTask = null;
            _share.Clear(); _copy.Enabled = false; _hostButton.Text = "Pornește accesul";
            _hostButton.Enabled = !_closing.IsCancellationRequested; UpdateMode();
            _starting = false;
        }
    }
    private async Task StopHostAsync()
    {
        _hostButton.Enabled = false; _hostStop?.Cancel(); _share.Clear(); _copy.Enabled = false;
        if (_hostTask is not null)
        {
            try { await _hostTask; } catch (Exception ex) when (ex is not OutOfMemoryException) { }
        }
        Log("Acces oprit; codul privat a fost invalidat.");
    }
    private Task<bool> RequestApprovalAsync(CancellationToken ct)
    {
        var result = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (ct.IsCancellationRequested || IsDisposed || !IsHandleCreated) return Task.FromResult(false);
        Post(async () =>
        {
            using var dialog = new ApprovalForm();
            using var registration = ct.Register(() =>
            {
                result.TrySetResult(false);
                Post(() => { if (!dialog.IsDisposed) dialog.Close(); });
            });
            if (ct.IsCancellationRequested) { result.TrySetResult(false); return; }
            dialog.Show(this);
            result.TrySetResult(await dialog.Result.Task);
        });
        return result.Task.WaitAsync(ct);
    }
    private async Task ConnectAsync()
    {
        if (_viewer is not null) { Log("Închide sesiunea curentă înainte de o nouă conexiune."); return; }
        _connect.Enabled = false; _connecting = true;
        try
        {
            var invitation = Invitation.Parse(_connection.Text);
            Log("Conectare securizată; așteaptă eventuala acceptare pe celălalt calculator.");
            var session = await ViewerSession.ConnectAsync(invitation, _closing.Token);
            if (_closing.IsCancellationRequested) { session.Dispose(); return; }
            _viewer = session;
            new ViewerForm(session, session.Layout.Displays[0].Id).Show();
            session.Closed += message => Post(() =>
            {
                Log(message); if (ReferenceEquals(_viewer, session)) _viewer = null;
                session.Dispose(); _connect.Enabled = !_closing.IsCancellationRequested;
            });
            session.Start(); Log("Conectat. Selectează monitorul în fereastra desktopului.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { Log(HostSession.SafeMessage(ex)); _connect.Enabled = true; }
        finally { _connecting = false; }
    }
    private async void ClosingAsync(object? sender, FormClosingEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        if (_closing.IsCancellationRequested) return;
        Enabled = false; _closing.Cancel(); _viewer?.Stop();
        await StopHostAsync();
        if (_viewer is { } viewer) await viewer.Completion;
        while (_connecting || _starting) await Task.Delay(25);
        _allowClose = true; Close();
    }
    private void Log(string message) => Post(() =>
    {
        var lines = _status.Lines.TakeLast(10).ToList();
        lines.Add($"{DateTime.Now:HH:mm:ss}  {message}"); _status.Lines = lines.ToArray();
        _status.SelectionStart = _status.TextLength; _status.ScrollToCaret();
    });
    private void Post(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(action); } catch (InvalidOperationException) { }
    }
    private static string LocalAddress()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses).Select(a => a.Address)
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))?.ToString() ?? "127.0.0.1";
        }
        catch (NetworkInformationException) { return "127.0.0.1"; }
    }
}
