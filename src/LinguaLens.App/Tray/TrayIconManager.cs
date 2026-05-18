using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Interop;
using LinguaLens.Core.Interfaces;

namespace LinguaLens.App.Tray;

/// <summary>
/// Manages the system tray icon using System.Windows.Forms.NotifyIcon.
/// Context menu: Token usage (info), Toggle (Enabled/Disabled), Словарь, Настройки, Выход.
/// Icon: green = active, grey = paused.
/// Registers global hotkey via RegisterHotKey Win32 API.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int HotkeyId = 9001;
    private const int TranslateHotkeyId = 9002;

    private NotifyIcon? _notifyIcon;
    private HwndSource? _hwndSource;
    private bool _isEnabled = true;
    private Icon? _iconActive;
    private Icon? _iconPaused;
    private ToolStripMenuItem? _usageMenuItem;
    private readonly ITokenUsageRepository? _usageRepo;
    private readonly IAppSettings? _settings;

    public event EventHandler? ToggleRequested;
    public event EventHandler? TranslateRequested;
    public event EventHandler? OpenVocabRequested;
    public event EventHandler? OpenSettingsRequested;

    public TrayIconManager(IAppSettings settings, ITokenUsageRepository? usageRepo = null)
    {
        _usageRepo = usageRepo;
        _settings = settings;

        _iconActive = CreateDotIcon(Color.FromArgb(34, 197, 94));   // green
        _iconPaused = CreateDotIcon(Color.FromArgb(156, 163, 175)); // gray

        var menu = new ContextMenuStrip();

        _usageMenuItem = new ToolStripMenuItem("Токены сегодня: —") { Enabled = false };
        var toggleItem = new ToolStripMenuItem("Выключить") { Name = "Toggle" };
        var vocabItem = new ToolStripMenuItem("Словарь");
        var settingsItem = new ToolStripMenuItem("Настройки");
        var exitItem = new ToolStripMenuItem("Выход");

        toggleItem.Click += (_, _) => ToggleRequested?.Invoke(this, EventArgs.Empty);
        vocabItem.Click += (_, _) => OpenVocabRequested?.Invoke(this, EventArgs.Empty);
        settingsItem.Click += (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
        exitItem.Click += (_, _) => System.Windows.Application.Current.Shutdown();

        menu.Items.AddRange([_usageMenuItem, new ToolStripSeparator(), toggleItem,
            new ToolStripSeparator(), vocabItem, settingsItem, new ToolStripSeparator(), exitItem]);

        menu.Opening += async (_, _) => await UpdateTokenMenuItemAsync();

        _notifyIcon = new NotifyIcon
        {
            Icon = _iconActive,
            Text = "LinguaLens",
            ContextMenuStrip = menu,
            Visible = true
        };

        RegisterHotkey(settings.HotKey, HotkeyId);
        RegisterHotkey(settings.TranslateHotKey, TranslateHotkeyId);
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

    public void ShowWarning(string title, string message)
    {
        _notifyIcon?.ShowBalloonTip(5000, title, message, ToolTipIcon.Warning);
    }

    private async Task UpdateTokenMenuItemAsync()
    {
        if (_usageMenuItem is null || _usageRepo is null || _settings is null) return;
        try
        {
            var summary = await _usageRepo.GetTodaySummaryAsync();
            var limit = _settings.DailyTokenLimit;
            var pct = limit > 0 ? (double)summary.TotalTokens / limit * 100 : 0;
            var text = limit > 0
                ? $"Токены сегодня: {summary.TotalTokens:N0} ({pct:F0}% от {limit:N0})"
                : $"Токены сегодня: {summary.TotalTokens:N0}";
            _usageMenuItem.Text = text;
        }
        catch
        {
            _usageMenuItem.Text = "Токены сегодня: —";
        }
    }

    private void RegisterHotkey(string hotKeyStr, int id)
    {
        try
        {
            if (_hwndSource is null)
            {
                var parameters = new HwndSourceParameters("LinguaLensHotkey")
                {
                    Width = 0, Height = 0,
                    WindowStyle = 0,
                    ParentWindow = new IntPtr(-3)
                };
                _hwndSource = new HwndSource(parameters);
                _hwndSource.AddHook(WndProc);
            }

            var (modifiers, vk) = ParseHotKey(hotKeyStr);
            if (vk != 0)
                RegisterHotKey(_hwndSource.Handle, id, modifiers, vk);
        }
        catch { /* hotkey registration is non-critical */ }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0312) // WM_HOTKEY
        {
            var id = wParam.ToInt32();
            if (id == HotkeyId)
            {
                ToggleRequested?.Invoke(this, EventArgs.Empty);
                handled = true;
            }
            else if (id == TranslateHotkeyId)
            {
                TranslateRequested?.Invoke(this, EventArgs.Empty);
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    private static (uint modifiers, uint vk) ParseHotKey(string hotKey)
    {
        uint modifiers = 0;
        uint vk = 0;
        foreach (var part in hotKey.Split('+'))
        {
            var token = part.Trim();
            switch (token.ToUpperInvariant())
            {
                case "ALT":   modifiers |= 0x0001; break;
                case "CTRL":
                case "CONTROL": modifiers |= 0x0002; break;
                case "SHIFT": modifiers |= 0x0004; break;
                case "WIN":   modifiers |= 0x0008; break;
                case "~":
                case "`":
                case "TILDE":
                case "BACKTICK": vk = 0xC0; break; // VK_OEM_3
                case "SPACE":    vk = 0x20; break; // VK_SPACE
                default:
                    if (token.Length == 1) vk = (uint)char.ToUpperInvariant(token[0]);
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
            UnregisterHotKey(_hwndSource.Handle, TranslateHotkeyId);
            _hwndSource.Dispose();
        }
        _notifyIcon?.Dispose();
        _iconActive?.Dispose();
        _iconPaused?.Dispose();
    }
}
