using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace CrosshairFree.Core
{
    public sealed class KeyboardHookService : IDisposable
    {
        private static KeyboardHookService? _instance;
        public static KeyboardHookService Instance => _instance ??= new KeyboardHookService();

        private Thread? _hookThread;
        private uint _hookThreadId;
        private IntPtr _hookId = IntPtr.Zero;
        private NativeWin32.LowLevelKeyboardProc? _hookProc;
        private readonly ManualResetEvent _readyEvent = new ManualResetEvent(false);

        public event Action<int>? KeyDown;

        public void Start()
        {
            if (_hookThread != null && _hookThread.IsAlive) return;

            _readyEvent.Reset();
            _hookThread = new Thread(HookThreadStart)
            {
                IsBackground = true,
                Name = "CrosshairFree_KeyboardHook_Thread",
                Priority = ThreadPriority.Highest
            };
            _hookThread.Start();

            _readyEvent.WaitOne(2000);
        }

        private void HookThreadStart()
        {
            _hookThreadId = GetCurrentThreadId();
            _hookProc = HookCallback;

            IntPtr hMod = IntPtr.Zero;
            try
            {
                using var curProcess = Process.GetCurrentProcess();
                using var curModule = curProcess.MainModule;
                if (curModule != null)
                {
                    hMod = NativeWin32.GetModuleHandle(curModule.ModuleName);
                }
            }
            catch { }

            _hookId = NativeWin32.SetWindowsHookEx(NativeWin32.WH_KEYBOARD_LL, _hookProc, hMod, 0);
            _readyEvent.Set();

            // Native Win32 Message Pump: guarantees WH_KEYBOARD_LL never times out or drops in Windows
            MSG msg;
            while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            if (_hookId != IntPtr.Zero)
            {
                NativeWin32.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)NativeWin32.WM_KEYDOWN || wParam == (IntPtr)NativeWin32.WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                try
                {
                    KeyDown?.Invoke(vkCode);
                }
                catch { }
            }

            return NativeWin32.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Stop()
        {
            try
            {
                if (_hookThreadId != 0)
                {
                    PostThreadMessage(_hookThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
                    _hookThreadId = 0;
                }
            }
            catch { }
        }

        public void Dispose()
        {
            Stop();
            _readyEvent.Dispose();
        }

        private const uint WM_QUIT = 0x0012;

        [StructLayout(LayoutKind.Sequential)]
        private struct MSG
        {
            public IntPtr hwnd;
            public uint message;
            public IntPtr wParam;
            public IntPtr lParam;
            public uint time;
            public int pt_x;
            public int pt_y;
        }

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern sbyte GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        private static extern bool TranslateMessage([In] ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern IntPtr DispatchMessage([In] ref MSG lpMsg);

        [DllImport("user32.dll")]
        private static extern bool PostThreadMessage(uint idThread, uint Msg, IntPtr wParam, IntPtr lParam);
    }
}
