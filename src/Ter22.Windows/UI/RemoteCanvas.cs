using System.Drawing.Drawing2D;
using Ter22.Core;
using Ter22.Windows.Sessions;

namespace Ter22.Windows.UI;

internal sealed class RemoteCanvas(ViewerSession session) : Control
{
    private Image? _image;
    private FrameHeader? _frame;
    private long _lastMove;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool ControlEnabled { get; set; } = true;
    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public Action? ExitControl { get; set; }
    public void SetFrame(FrameHeader frame, byte[] jpeg)
    {
        using var stream = new MemoryStream(jpeg, writable: false);
        using var decoded = Image.FromStream(stream, false, true);
        var image = new Bitmap(decoded);
        _image?.Dispose(); _image = image; _frame = frame; Invalidate();
    }
    public void ClearFrame() { _frame = null; _image?.Dispose(); _image = null; Invalidate(); }
    protected override void OnCreateControl()
    {
        SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        TabStop = true; BackColor = Color.FromArgb(20, 25, 32); base.OnCreateControl();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        if (_image is null) { TextRenderer.DrawText(e.Graphics, "Așteaptă imaginea…", Font, ClientRectangle, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); return; }
        double scale = Math.Min((double)Width / _image.Width, (double)Height / _image.Height);
        int w = (int)Math.Round(_image.Width * scale), h = (int)Math.Round(_image.Height * scale);
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
        e.Graphics.DrawImage(_image, new Rectangle((Width - w) / 2, (Height - h) / 2, w, h));
    }
    protected override void OnResize(EventArgs e) { base.OnResize(e); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { session.ReleaseInputs(); base.OnLostFocus(e); }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Environment.TickCount64 - _lastMove < 20) return;
        _lastMove = Environment.TickCount64; MouseInput(InputAction.Move, e);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); Focus(); Capture = true;
        if (ButtonAction(e.Button, false) is { } action) MouseInput(action, e);
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        bool sent = ButtonAction(e.Button, true) is { } action && MouseInput(action, e);
        Capture = false;
        if (!sent) session.ReleaseInputs();
    }
    protected override void OnMouseWheel(MouseEventArgs e)
    { base.OnMouseWheel(e); MouseInput(InputAction.Wheel, e, Math.Clamp(e.Delta, -1200, 1200)); }
    private static InputAction? ButtonAction(MouseButtons button, bool up) => button switch
    {
        MouseButtons.Left => up ? InputAction.LeftUp : InputAction.LeftDown,
        MouseButtons.Right => up ? InputAction.RightUp : InputAction.RightDown,
        MouseButtons.Middle => up ? InputAction.MiddleUp : InputAction.MiddleDown, _ => null
    };
    private bool MouseInput(InputAction action, MouseEventArgs e, int value = 0)
    {
        if (!ControlEnabled || _frame is null || !Focused) return false;
        if (Geometry.TryMap(e.X, e.Y, Width, Height, _frame, out int x, out int y))
        {
            session.Input(new RemoteInput(action, _frame.DisplayId, _frame.Revision, x, y, value));
            return true;
        }
        return false;
    }
    protected override void WndProc(ref Message m)
    {
        const int KeyDown = 0x100, KeyUp = 0x101, SysKeyDown = 0x104, SysKeyUp = 0x105;
        if (m.Msg == 0x87 && ControlEnabled) { m.Result = (IntPtr)0x87; return; } // WM_GETDLGCODE: arrows, tab, all keys and chars.
        if (m.Msg is KeyDown or KeyUp or SysKeyDown or SysKeyUp)
        {
            int key = (int)m.WParam;
            if (key == (int)Keys.F12 && m.Msg is KeyDown or SysKeyDown)
            { session.ReleaseInputs(); ExitControl?.Invoke(); return; }
            if (ControlEnabled && Focused && _frame is not null)
            {
                session.Input(new RemoteInput(m.Msg is KeyDown or SysKeyDown ? InputAction.KeyDown : InputAction.KeyUp,
                    _frame.DisplayId, _frame.Revision, 0, 0, key));
                return;
            }
        }
        if (ControlEnabled && m.Msg is 0x102 or 0x106) return;
        base.WndProc(ref m);
    }
    protected override void Dispose(bool disposing) { if (disposing) _image?.Dispose(); base.Dispose(disposing); }
}
