using System.Runtime.InteropServices;

namespace ResetExplode;

/// <summary>Ícone e menu de contexto da área de notificação do Windows.</summary>
internal sealed class TrayIcon : IDisposable
{
    private const uint NimAdd = 0x00000000, NimDelete = 0x00000002;
    private const uint NifMessage = 0x00000001, NifIcon = 0x00000002, NifTip = 0x00000004;
    private const int WmApp = 0x8000, WmNull = 0x0000, WmContextMenu = 0x007B;
    private const int WmRightButtonUp = 0x0205, WmLeftButtonDoubleClick = 0x0203;
    private const uint MfString = 0x00000000, MfSeparator = 0x00000800;
    private const uint TpmRightButton = 0x0002, TpmReturnCommand = 0x0100;
    private const uint IdOpen = 1, IdExit = 2;
    private const int CallbackId = WmApp + 1;

    private readonly Action _open;
    private readonly Action _exit;
    private IntPtr _window, _icon, _smallIcon;
    private bool _ownsIcon, _added, _disposed;

    public int CallbackMessage => CallbackId;

    public TrayIcon(Action open, Action exit)
    {
        _open = open;
        _exit = exit;
        uint count = ExtractIconEx(Environment.ProcessPath ?? "", 0, out _icon, out _smallIcon, 1);
        if (count > 0 && _icon != IntPtr.Zero)
            _ownsIcon = true;
        else
            _icon = LoadIcon(IntPtr.Zero, new IntPtr(32512)); // IDI_APPLICATION

        if (_smallIcon != IntPtr.Zero && _smallIcon != _icon)
            DestroyIcon(_smallIcon);
        _smallIcon = IntPtr.Zero;
    }

    public void Attach(IntPtr window) => _window = window;

    public void Show()
    {
        if (_disposed || _added || _window == IntPtr.Zero) return;
        var data = CreateData();
        if (Shell_NotifyIcon(NimAdd, ref data)) _added = true;
    }

    public void Hide()
    {
        if (!_added) return;
        var data = CreateData();
        Shell_NotifyIcon(NimDelete, ref data);
        _added = false;
    }

    public void HandleCallback(int message)
    {
        if (message == WmLeftButtonDoubleClick)
            _open();
        else if (message == WmRightButtonUp || message == WmContextMenu)
            ShowContextMenu();
    }

    private void ShowContextMenu()
    {
        IntPtr menu = CreatePopupMenu();
        if (menu == IntPtr.Zero) return;
        AppendMenu(menu, MfString, new UIntPtr(IdOpen), "Abrir RESET EXPLODE");
        AppendMenu(menu, MfSeparator, UIntPtr.Zero, null);
        AppendMenu(menu, MfString, new UIntPtr(IdExit), "Sair");

        GetCursorPos(out var point);
        SetForegroundWindow(_window);
        uint selected = TrackPopupMenu(menu, TpmRightButton | TpmReturnCommand,
                                       point.X, point.Y, 0, _window, IntPtr.Zero);
        PostMessage(_window, WmNull, IntPtr.Zero, IntPtr.Zero);
        DestroyMenu(menu);

        if (selected == IdOpen) _open();
        else if (selected == IdExit) _exit();
    }

    private NotifyIconData CreateData() => new()
    {
        cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
        hWnd = _window,
        uID = 1,
        uFlags = NifMessage | NifIcon | NifTip,
        uCallbackMessage = CallbackId,
        hIcon = _icon,
        szTip = "RESET EXPLODE",
        szInfo = "",
        szInfoTitle = ""
    };

    public void Dispose()
    {
        if (_disposed) return;
        Hide();
        if (_ownsIcon && _icon != IntPtr.Zero) DestroyIcon(_icon);
        _icon = IntPtr.Zero;
        _disposed = true;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState;
        public uint dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint ExtractIconEx(string file, int index, out IntPtr large, out IntPtr small, uint count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadIcon(IntPtr instance, IntPtr iconName);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool AppendMenu(IntPtr menu, uint flags, UIntPtr item, string? text);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")]
    private static extern uint TrackPopupMenu(IntPtr menu, uint flags, int x, int y, int reserved, IntPtr window, IntPtr rect);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool DestroyMenu(IntPtr menu);
}
