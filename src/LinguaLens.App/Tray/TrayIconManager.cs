using System.Runtime.InteropServices;
using System.Windows.Forms;
using LinguaLens.Core.Interfaces;

namespace LinguaLens.App.Tray;

/// <summary>
/// Manages the system tray icon using System.Windows.Forms.NotifyIcon.
/// Context menu: Toggle (Enabled/Disabled), Словарь, Настройки, Выход.
/// Icon: green = active, grey = paused.
/// Registers global hotkey via RegisterHotKey Win32 API.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private NotifyIcon? _notifyIcon;
    private bool _isEnabled = true;

    public event EventHandler? ToggleRequested;
    public event EventHandler? OpenVocabRequested;
    public event EventHandler? OpenSettingsRequested;

    public TrayIconManager(IAppSettings settings)
    {
        throw new NotImplementedException();
    }

    public void SetEnabled(bool enabled)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        _notifyIcon?.Dispose();
    }
}
