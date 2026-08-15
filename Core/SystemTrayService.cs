using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CrosshairFree.Core
{
    public class SystemTrayService : IDisposable
    {
        public const int WM_USER = 0x0400;
        public const int WM_TRAYICON = WM_USER + 101;

        private const uint NIM_ADD = 0x00000000;
        private const uint NIM_MODIFY = 0x00000001;
        private const uint NIM_DELETE = 0x00000002;

        private const uint NIF_MESSAGE = 0x00000001;
        private const uint NIF_ICON = 0x00000002;
        private const uint NIF_TIP = 0x00000004;

        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONUP = 0x0205;

        private IntPtr _hwnd = IntPtr.Zero;
        private IntPtr _hIcon = IntPtr.Zero;
        private bool _isAdded = false;

        public event Action? OnOpenRequested;
        public event Action<int, int>? OnContextMenuRequested;

        public void Initialize(Window window)
        {
            _hwnd = new WindowInteropHelper(window).Handle;
            try
            {
                _hIcon = NativeWin32.ExtractAssociatedIcon(IntPtr.Zero, Environment.ProcessPath ?? "", out _);
            }
            catch { }

            if (_hIcon == IntPtr.Zero)
            {
                _hIcon = LoadIcon(IntPtr.Zero, (IntPtr)32512); // Standard App Icon
            }

            var source = HwndSource.FromHwnd(_hwnd);
            source?.AddHook(WndProc);

            AddTrayIcon();
        }

        private void AddTrayIcon()
        {
            if (_isAdded || _hwnd == IntPtr.Zero) return;

            var nid = new NOTIFYICONDATA();
            nid.cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA));
            nid.hWnd = _hwnd;
            nid.uID = 1;
            nid.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP;
            nid.uCallbackMessage = WM_TRAYICON;
            nid.hIcon = _hIcon;
            nid.szTip = "Crosshair Free — Adurite";

            _isAdded = Shell_NotifyIcon(NIM_ADD, ref nid);
        }

        public void RemoveTrayIcon()
        {
            if (!_isAdded || _hwnd == IntPtr.Zero) return;

            var nid = new NOTIFYICONDATA();
            nid.cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA));
            nid.hWnd = _hwnd;
            nid.uID = 1;

            Shell_NotifyIcon(NIM_DELETE, ref nid);
            _isAdded = false;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_TRAYICON)
            {
                int mouseEvent = lParam.ToInt32();
                if (mouseEvent == WM_LBUTTONUP || mouseEvent == WM_LBUTTONDBLCLK)
                {
                    OnOpenRequested?.Invoke();
                    handled = true;
                }
                else if (mouseEvent == WM_RBUTTONUP)
                {
                    POINT pt;
                    GetCursorPos(out pt);
                    OnContextMenuRequested?.Invoke(pt.x, pt.y);
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }

        public void Dispose()
        {
            RemoveTrayIcon();
            if (_hIcon != IntPtr.Zero)
            {
                DestroyIcon(_hIcon);
                _hIcon = IntPtr.Zero;
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}
