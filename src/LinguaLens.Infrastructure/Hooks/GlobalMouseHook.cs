using System.Runtime.InteropServices;
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
    [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string lpModuleName);

    private const int WH_MOUSE_LL = 14;

    private IntPtr _hookHandle = IntPtr.Zero;
    private HookProc? _hookProc; // keep reference to prevent GC

    public void Start()
    {
        throw new NotImplementedException();
    }

    public void Stop()
    {
        throw new NotImplementedException();
    }

    private IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        Stop();
    }
}
