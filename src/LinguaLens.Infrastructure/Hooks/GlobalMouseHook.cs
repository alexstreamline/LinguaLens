using System.Runtime.InteropServices;
using System.Threading.Channels;
using System.Windows;

namespace LinguaLens.Infrastructure.Hooks;

public sealed class GlobalMouseHook : IDisposable
{
    public event EventHandler<Point>? MouseMoved;
    public event EventHandler? SelectionChanged;

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string? lpModuleName);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] private static extern IntPtr DispatchMessage(ref MSG lpMsg);
    [DllImport("user32.dll")] private static extern bool PostThreadMessage(uint idThread, uint msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData, flags, time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int x, y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam, lParam;
        public uint time;
        public POINT pt;
    }

    private const int WH_MOUSE_LL = 14;
    private const int WM_MOUSEMOVE = 0x0200;
    private const int WM_LBUTTONUP = 0x0202;
    private const uint WM_QUIT = 0x0012;

    private IntPtr _hookHandle = IntPtr.Zero;
    private HookProc? _hookProc; // prevent GC
    private uint _hookThreadId;
    private CancellationTokenSource? _cts;

    private readonly Channel<(int msg, int x, int y)> _channel =
        Channel.CreateBounded<(int, int, int)>(new BoundedChannelOptions(64)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

    public void Start()
    {
        _cts = new CancellationTokenSource();

        var processingThread = new Thread(ProcessMessages)
        {
            IsBackground = true,
            Name = "MouseHookProcessor"
        };
        processingThread.Start();

        var hookThread = new Thread(() =>
        {
            _hookThreadId = GetCurrentThreadId();
            _hookProc = LowLevelMouseProc;

            using var process = System.Diagnostics.Process.GetCurrentProcess();
            using var module = process.MainModule!;
            _hookHandle = SetWindowsHookEx(WH_MOUSE_LL, _hookProc, GetModuleHandle(module.ModuleName), 0);

            while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }

            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
        })
        {
            IsBackground = true,
            Name = "MouseHookThread"
        };
        hookThread.TrySetApartmentState(ApartmentState.STA);
        hookThread.Start();
    }

    public void Stop()
    {
        if (_hookThreadId != 0)
        {
            PostThreadMessage(_hookThreadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            _hookThreadId = 0;
        }
        _cts?.Cancel();
        _channel.Writer.TryComplete();
    }

    private IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = (int)wParam;
            if (msg is WM_MOUSEMOVE or WM_LBUTTONUP)
            {
                var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                _channel.Writer.TryWrite((msg, data.pt.x, data.pt.y));
            }
        }
        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private void ProcessMessages()
    {
        var reader = _channel.Reader;
        var ct = _cts!.Token;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var (msg, x, y) = reader.ReadAsync(ct).AsTask().GetAwaiter().GetResult();
                var point = new Point(x, y);
                if (msg == WM_MOUSEMOVE)
                    MouseMoved?.Invoke(this, point);
                else if (msg == WM_LBUTTONUP)
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException) { break; }
            catch { /* ignore individual message errors */ }
        }
    }

    public void Dispose() => Stop();
}
