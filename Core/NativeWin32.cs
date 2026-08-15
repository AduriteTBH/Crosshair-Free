using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CrosshairFree.Core
{
    public static class NativeWin32
    {
        public const int WS_EX_TRANSPARENT = 0x00000020;
        public const int WS_EX_LAYERED = 0x00080000;
        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const int WS_EX_TOPMOST = 0x00000008;

        public const int GWL_EXSTYLE = -20;
        public const int WH_KEYBOARD_LL = 13;
        
        public const int WM_KEYDOWN = 0x0100;
        public const int WM_SYSKEYDOWN = 0x0104;

        public const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

        public const int SM_CXSCREEN = 0;
        public const int SM_CYSCREEN = 1;

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;

        public const int CC_FULLOPEN = 0x00000002;
        public const int CC_RGBINIT = 0x00000001;

        // DWM Dark Mode Constants
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static LowLevelKeyboardProc? _pinnedHookProc;
        private static IntPtr _currentHookId = IntPtr.Zero;

        [StructLayout(LayoutKind.Sequential)]
        public struct CHOOSECOLOR
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public int rgbResult;
            public IntPtr lpCustColors;
            public int Flags;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public IntPtr lpTemplateName;
        }

        [DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("comdlg32.dll", SetLastError = true)]
        public static extern bool ChooseColor(ref CHOOSECOLOR lpcc);

        private static readonly int[] _customColors = new int[16];

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

        [DllImport("psapi.dll")]
        public static extern int EmptyWorkingSet(IntPtr hwProc);

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr ExtractAssociatedIcon(IntPtr hInst, string lpIconPath, out ushort lpiIcon);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        public static void EnableImmersiveDarkMode(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return;
            try
            {
                int useDarkMode = 1;
                // Standard Windows 10 (20H1+) and Windows 11 native immersive dark mode
                if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int)) != 0)
                {
                    // Fallback for earlier Windows 10 builds (1809 - 1909)
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDarkMode, sizeof(int));
                }
            }
            catch { }
        }

        public static void MakeClickThrough(IntPtr hWnd)
        {
            int initialStyle = GetWindowLong(hWnd, GWL_EXSTYLE);
            SetWindowLong(hWnd, GWL_EXSTYLE, initialStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST);
            
            try
            {
                SetWindowDisplayAffinity(hWnd, WDA_EXCLUDEFROMCAPTURE);
            }
            catch { }

            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        public static void ForceTopmost(IntPtr hWnd)
        {
            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        public static IntPtr StartPassiveKeyboardHook(LowLevelKeyboardProc proc)
        {
            try
            {
                _pinnedHookProc = proc; // Permanently root the delegate in static memory
                if (_currentHookId != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_currentHookId);
                    _currentHookId = IntPtr.Zero;
                }

                IntPtr hMod = IntPtr.Zero;
                try
                {
                    using var curProcess = Process.GetCurrentProcess();
                    using var curModule = curProcess.MainModule;
                    if (curModule != null)
                    {
                        hMod = GetModuleHandle(curModule.ModuleName);
                    }
                }
                catch { }

                _currentHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _pinnedHookProc, hMod, 0);
                return _currentHookId;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        public static void StopPassiveKeyboardHook()
        {
            try
            {
                if (_currentHookId != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_currentHookId);
                    _currentHookId = IntPtr.Zero;
                }
            }
            catch { }
        }

        public static void TrimWorkingSet()
        {
            try
            {
                GC.Collect(1, GCCollectionMode.Optimized, false);
                EmptyWorkingSet(Process.GetCurrentProcess().Handle);
                SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, (IntPtr)(-1), (IntPtr)(-1));
            }
            catch { }
        }
    }
}
