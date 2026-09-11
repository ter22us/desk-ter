using Ter22.Core;
using Ter22.Windows.Sessions;

namespace Ter22.Windows.UI;

internal sealed class ViewerForm : Form
{
    private sealed record Pending(FrameHeader Header, byte[] Jpeg);
    private readonly ViewerSession _session;
    private readonly Guid _id = Guid.NewGuid();
    private readonly RemoteCanvas _canvas;
    private readonly ComboBox _displays = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 290 };
    private readonly CheckBox _control = new() { Text = "Control mouse/tastatură", Checked = true, AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
    private readonly System.Windows.Forms.Timer _paint = new() { Interval = 33 };
    private Pending? _pending;
    private bool _fullScreen;
    private Rectangle _restoreBounds;
    private FormWindowState _restoreState;

    public ViewerForm(ViewerSession session, string displayId)
    {
        _session = session;
        Text = "Ter22 Remote — desktop la distanță";
        Width = 1200; Height = 800; MinimumSize = new Size(720, 450); StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        _canvas = new RemoteCanvas(session) { Dock = DockStyle.Fill };
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8), WrapContents = true };
        var windows = new Button { Text = "Ferestre pentru monitoare", AutoSize = true };
        var full = new Button { Text = "Ecran complet", AutoSize = true };
        var stop = new Button { Text = "Deconectare", AutoSize = true };
        bar.Controls.AddRange([_displays, _control, windows, full, stop]);
        Controls.Add(_canvas); Controls.Add(bar);
        var status = new Label { Text = "Clic pe imagine pentru control. F12 eliberează tastatura și mouse-ul. Combinațiile rezervate de Windows rămân locale.",
            Dock = DockStyle.Bottom, Height = 32, Padding = new Padding(8, 6, 0, 0), AutoEllipsis = true };
        Controls.Add(status);
        _displays.SelectedIndexChanged += (_, _) =>
        {
            if (_displays.SelectedItem is not DisplayInfo selected) return;
            session.ReleaseInputs(); _canvas.ClearFrame(); Interlocked.Exchange(ref _pending, null);
            session.SetView(_id, selected.Id); Text = $"Ter22 Remote — {selected.Name}";
        };
        _control.CheckedChanged += (_, _) => { _canvas.ControlEnabled = _control.Checked; session.ReleaseInputs(); };
        _canvas.ExitControl = () => _control.Checked = false;
        windows.Click += (_, _) =>
        {
            foreach (var display in session.Layout.Displays.Where(d => d.Id != "*"))
                if (!session.HasView(display.Id)) new ViewerForm(session, display.Id).Show();
        };
        full.Click += (_, _) => ToggleFullScreen(); stop.Click += (_, _) => session.Stop();
        _paint.Tick += (_, _) =>
        {
            var latest = Interlocked.Exchange(ref _pending, null);
            if (latest is null) return;
            if (_displays.SelectedItem is not DisplayInfo selected || selected.Id != latest.Header.DisplayId
                || latest.Header.Revision != session.Layout.Revision) return;
            try { _canvas.SetFrame(latest.Header, latest.Jpeg); }
            catch (Exception ex) when (ex is ArgumentException or System.Runtime.InteropServices.ExternalException)
            { session.Stop(); }
        };
        session.FrameReceived += ReceiveFrame; session.LayoutChanged += ChangeLayout; session.Closed += SessionClosed;
        SetLayout(session.Layout, displayId); _paint.Start();
    }
    private void ReceiveFrame(FrameHeader header, byte[] jpeg)
    { if (header.ViewId == _id) Interlocked.Exchange(ref _pending, new Pending(header, jpeg)); }
    private void ChangeLayout(DisplayLayout layout) => Post(() =>
        SetLayout(layout, (_displays.SelectedItem as DisplayInfo)?.Id ?? layout.Displays[0].Id));
    private void SetLayout(DisplayLayout layout, string selected)
    {
        _displays.DataSource = layout.Displays;
        _displays.SelectedItem = layout.Displays.FirstOrDefault(d => d.Id == selected) ?? layout.Displays[0];
    }
    private void SessionClosed(string _) => Post(Close);
    private void Post(Action action)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(action); } catch (InvalidOperationException) { }
    }
    private void ToggleFullScreen()
    {
        if (!_fullScreen)
        {
            _restoreBounds = Bounds; _restoreState = WindowState; WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.None; Bounds = Screen.FromControl(this).Bounds;
        }
        else { FormBorderStyle = FormBorderStyle.Sizable; Bounds = _restoreBounds; WindowState = _restoreState; }
        _fullScreen = !_fullScreen;
    }
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _paint.Stop(); _paint.Dispose();
        _session.FrameReceived -= ReceiveFrame; _session.LayoutChanged -= ChangeLayout; _session.Closed -= SessionClosed;
        Interlocked.Exchange(ref _pending, null); _session.RemoveView(_id); base.OnFormClosed(e);
    }
}
