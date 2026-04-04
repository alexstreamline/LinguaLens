using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Interop;
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

    private const int HotkeyId = 9001;

    private NotifyIcon? _notifyIcon;
    private HwndSource? _hwndSource;
    private bool _isEnabled = true;
    private Icon? _iconActive;
    private Icon? _iconPaused;

    public event EventHandler? ToggleRequested;
    public event EventHandler? OpenVocabRequested;
    public event EventHandler? OpenSettingsRequested;

    public TrayIconManager(IAppSettings settings)
    {
        _iconActive = CreateDotIcon(Color.FromArgb(34, 197, 94));   // green
        _iconPaused = CreateDotIcon(Color.FromArgb(156, 163, 175)); // gray

        var menu = new ContextMenuStrip();
        var toggleItem = new ToolStripMenuItem("Выключить") { Name = "Toggle" };
        var vocabItem = new ToolStripMenuItem("Словарь");
        var settingsItem = new ToolStripMenuItem("Настройки");
        var exitItem = new ToolStripMenuItem("Выход");

        toggleItem.Click += (_, _) => ToggleRequested?.Invoke(this, EventArgs.Empty);
        vocabItem.Click += (_, _) => OpenVocabRequested?.Invoke(this, EventArgs.Empty);
        settingsItem.Click += (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
        exitItem.Click += (_, _) => System.Windows.Application.Current.Shutdown();

        menu.Items.AddRange([toggleItem, new ToolStripSeparator(), vocabItem, settingsItem, new ToolStripSeparator(), exitItem]);

        _notifyIcon = new NotifyIcon
        {
            Icon = _iconActive,
            Text = "LinguaLens",
            ContextMenuStrip = menu,
            Visible = true
        };

        RegisterHotkey(settings.HotKey);
    }

    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
        if (_notifyIcon is null) return;

        _notifyIcon.Icon = enabled ? _iconActive : _iconPaused;
        _notifyIcon.Text = enabled ? "LinguaLens — активен" : "LinguaLens — отключён";

        var menu = _notifyIcon.ContextMenuStrip;
        if (menu?.Items["Toggle"] is ToolStripMenuItem item)
            item.Text = enabled ? "Выключить" : "Включить";
    }

    private void RegisterHotkey(string hotKeyStr)
    {
        try
        {
            var parameters = new HwndSourceParameters("LinguaLensHotkey")
            {
                Width = 0, Height = 0,
                WindowStyle = 0x800000 // WS_BORDER minimal
            };
            _hwndSource = new HwndSource(parameters);
            _hwndSource.AddHook(WndProc);

            var (modifiers, vk) = ParseHotKey(hotKeyStr);
            if (vk != 0)
                RegisterHotKey(_hwndSource.Handle, HotkeyId, modifiers, vk);
        }
        catch { /* hotkey registration is non-critical */ }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0312 && wParam.ToInt32() == HotkeyId) // WM_HOTKEY
        {
            ToggleRequested?.Invoke(this, EventArgs.Empty);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private static (uint modifiers, uint vk) ParseHotKey(string hotKey)
    {
        uint modifiers = 0;
        uint vk = 0;
        foreach (var part in hotKey.Split('+'))
        {
            switch (part.Trim().ToUpperInvariant())
            {
                case "ALT":   modifiers |= 0x0001; break;
                case "CTRL":
                case "CONTROL": modifiers |= 0x0002; break;
                case "SHIFT": modifiers |= 0x0004; break;
                case "WIN":   modifiers |= 0x0008; break;
                default:
                    var p = part.Trim();
                    if (p.Length == 1) vk = (uint)char.ToUpperInvariant(p[0]);
                    break;
            }
        }
        return (modifiers, vk);
    }

    private static Icon CreateDotIcon(Color color)
    {
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, 1, 1, 13, 13);
        }
        var handle = bmp.GetHicon();
        return Icon.FromHandle(handle);
    }

    public void Dispose()
    {
        if (_hwndSource is not null)
        {
            UnregisterHotKey(_hwndSource.Handle, HotkeyId);
            _hwndSource.Dispose();
        }
        _notifyIcon?.Dispose();
        _iconActive?.Dispose();
        _iconPaused?.Dispose();
    }
}
