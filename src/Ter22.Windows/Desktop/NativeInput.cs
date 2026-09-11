using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Ter22.Core;

namespace Ter22.Windows.Desktop;

internal sealed class NativeInput
{
    private readonly HashSet<int> _keys = [];
    private readonly HashSet<InputAction> _buttons = [];
    private const uint Move = 0x0001, Absolute = 0x8000, VirtualDesk = 0x4000;

    public void Apply(RemoteInput input, DisplayLayout layout)
    {
        if (!Enum.IsDefined(input.Action)) throw new InvalidDataException("Acțiune necunoscută.");
        if (input.Revision != layout.Revision || !layout.Displays.Any(d => d.Id == input.DisplayId)) return;
        EnsureInteractiveDesktop();
        if (input.Action is InputAction.KeyDown or InputAction.KeyUp)
        {
            if (input.Value is < 8 or > 254) throw new InvalidDataException("Tastă invalidă.");
            bool up = input.Action == InputAction.KeyUp;
            if (up && !_keys.Contains(input.Value)) return;
            Send(Key(input.Value, up));
            if (up) _keys.Remove(input.Value); else _keys.Add(input.Value);
            return;
        }
        if (!layout.Displays.Any(d => d.Id != "*" && d.Contains(input.X, input.Y))) return;
        var desktop = layout.Displays.Single(d => d.Id == "*");
        var (x, y) = Geometry.Normalize(input.X, input.Y, desktop);
        uint flags = input.Action switch
        {
            InputAction.Move => 0, InputAction.LeftDown => 2, InputAction.LeftUp => 4,
            InputAction.RightDown => 8, InputAction.RightUp => 16,
            InputAction.MiddleDown => 32, InputAction.MiddleUp => 64, InputAction.Wheel => 0x0800,
            _ => throw new InvalidDataException("Acțiune mouse invalidă.")
        };
        if (input.Action == InputAction.Wheel && input.Value is < -1200 or > 1200)
            throw new InvalidDataException("Deplasare rotiță invalidă.");
        InputAction? down = input.Action switch
        {
            InputAction.LeftUp => InputAction.LeftDown, InputAction.RightUp => InputAction.RightDown,
            InputAction.MiddleUp => InputAction.MiddleDown, _ => null
        };
        if (down.HasValue && !_buttons.Contains(down.Value)) return;
        Send(new NativeEvent { Type = 0, Data = new EventUnion { Mouse = new MouseEvent
        {
            X = x, Y = y, Flags = Move | Absolute | VirtualDesk | flags,
            Data = input.Action == InputAction.Wheel ? unchecked((uint)input.Value) : 0
        } } });
        if (down.HasValue) _buttons.Remove(down.Value);
        else if (input.Action is InputAction.LeftDown or InputAction.RightDown or InputAction.MiddleDown) _buttons.Add(input.Action);
    }

    public void ReleaseAll()
    {
        var events = _keys.Select(k => Key(k, true)).ToList();
        foreach (var button in _buttons)
            events.Add(new NativeEvent { Data = new EventUnion { Mouse = new MouseEvent
            { Flags = button == InputAction.LeftDown ? 4u : button == InputAction.RightDown ? 16u : 64u } } });
        if (events.Count > 0) SendInput((uint)events.Count, events.ToArray(), Marshal.SizeOf<NativeEvent>());
        _keys.Clear(); _buttons.Clear();
    }

    private static NativeEvent Key(int key, bool up) => new()
    {
        Type = 1, Data = new EventUnion { Keyboard = new KeyboardEvent
        {
            VirtualKey = (ushort)key,
            Flags = (up ? 2u : 0u) | (key is 33 or 34 or 35 or 36 or 37 or 38 or 39 or 40 or 45 or 46 or 91 or 92 or 111 or 163 or 165 ? 1u : 0u)
        } }
    };
    private static void Send(NativeEvent value)
    {
        if (SendInput(1, [value], Marshal.SizeOf<NativeEvent>()) != 1)
            throw new IOException("Windows a respins controlul. Verifică dacă aplicația țintă rulează ca administrator.");
    }

    public static void EnsureInteractiveDesktop()
    {
        IntPtr input = OpenInputDesktop(0, false, 1);
        if (input == IntPtr.Zero) throw new IOException("Desktopul este blocat sau Windows afișează un ecran securizat.");
        try
        {
            if (DesktopName(input) != DesktopName(GetThreadDesktop(GetCurrentThreadId())))
                throw new IOException("Sesiunea s-a oprit deoarece desktopul Windows s-a schimbat.");
        }
        finally { CloseDesktop(input); }
    }
    private static string DesktopName(IntPtr desktop)
    {
        var result = new StringBuilder(256);
        if (!GetUserObjectInformation(desktop, 2, result, result.Capacity * sizeof(char), out _))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return result.ToString();
    }

    public static void CaptureRegion(Graphics target, DisplayInfo display, int originX, int originY)
    {
        IntPtr screen = GetDC(IntPtr.Zero);
        if (screen == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        IntPtr destination = IntPtr.Zero;
        try
        {
            destination = target.GetHdc();
            if (!BitBlt(destination, display.X - originX, display.Y - originY, display.Width, display.Height,
                screen, display.X, display.Y, 0x00CC0020u | 0x40000000u))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            if (destination != IntPtr.Zero) target.ReleaseHdc(destination);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    public static void DrawCursor(Graphics graphics, int originX, int originY)
    {
        var cursor = new CursorInfo { Size = Marshal.SizeOf<CursorInfo>() };
        if (!GetCursorInfo(ref cursor) || cursor.Flags != 1) return;
        IntPtr copy = CopyIcon(cursor.Cursor);
        if (copy == IntPtr.Zero) return;
        IconInfo icon = default;
        try
        {
            if (!GetIconInfo(copy, out icon)) return;
            IntPtr dc = graphics.GetHdc();
            try { DrawIconEx(dc, cursor.X - (int)icon.HotX - originX, cursor.Y - (int)icon.HotY - originY, copy, 0, 0, 0, IntPtr.Zero, 3); }
            finally { graphics.ReleaseHdc(dc); }
        }
        finally
        {
            if (icon.Mask != IntPtr.Zero) DeleteObject(icon.Mask);
            if (icon.Color != IntPtr.Zero) DeleteObject(icon.Color);
            DestroyIcon(copy);
        }
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeEvent { public uint Type; public EventUnion Data; }
    [StructLayout(LayoutKind.Explicit)] private struct EventUnion
    {
        [FieldOffset(0)] public MouseEvent Mouse;
        [FieldOffset(0)] public KeyboardEvent Keyboard;
    }
    [StructLayout(LayoutKind.Sequential)] private struct MouseEvent
    { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardEvent
    { public ushort VirtualKey, Scan; public uint Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct CursorInfo
    { public int Size; public uint Flags; public IntPtr Cursor; public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct IconInfo
    { [MarshalAs(UnmanagedType.Bool)] public bool IsIcon; public uint HotX, HotY; public IntPtr Mask, Color; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, NativeEvent[] inputs, int size);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr OpenInputDesktop(uint flags, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint access);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll")] private static extern IntPtr GetThreadDesktop(uint id);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetUserObjectInformation(IntPtr handle, int index, StringBuilder info, int length, out int needed);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BitBlt(IntPtr target, int x, int y, int width, int height, IntPtr source, int sx, int sy, uint operation);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetCursorInfo(ref CursorInfo info);
    [DllImport("user32.dll")] private static extern IntPtr CopyIcon(IntPtr icon);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetIconInfo(IntPtr icon, out IconInfo info);
    [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool DrawIconEx(IntPtr dc, int x, int y, IntPtr icon, int w, int h, uint step, IntPtr brush, uint flags);
}
