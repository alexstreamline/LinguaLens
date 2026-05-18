using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LinguaLens.Core.Interfaces;
using Microsoft.Win32;
using WpfPanel = System.Windows.Controls.Panel;

namespace LinguaLens.App.Windows;

public partial class SettingsWindow : Window
{
    private readonly IAppSettings _settings;
    private readonly ITokenUsageRepository _usageRepo;
    private bool _keyVisible;

    public SettingsWindow(IAppSettings settings, ITokenUsageRepository usageRepo)
    {
        InitializeComponent();
        _settings = settings;
        _usageRepo = usageRepo;

        LoadValues();
        WireEvents();
        _ = RefreshUsageAsync();
    }

    // ── Loading state into controls ──────────────────────────────────────────

    private void LoadValues()
    {
        // API
        ProviderGroq.IsChecked   = settings.LlmProvider != "gemini";
        ProviderGemini.IsChecked = settings.LlmProvider == "gemini";
        ApiKeyMasked.Password    = settings.ApiKey;

        // Behavior
        DebounceSlider.Value         = settings.DebounceMs;
        DebounceValue.Text           = $"{settings.DebounceMs}ms";
        DetectEnglishToggle.IsChecked = settings.DetectEnglish;
        DetectSpanishToggle.IsChecked = settings.DetectSpanish;
        AutoSaveToggle.IsChecked      = settings.AutoSaveToVocab;

        // Appearance
        ThemeLight.IsChecked = settings.Theme != "dark";
        ThemeDark.IsChecked  = settings.Theme == "dark";

        // System
        HotKeyBox.Text                  = settings.TranslateHotKey;
        StartWithWindowsToggle.IsChecked = settings.StartWithWindows;

        // Usage
        DailyLimitBox.Text         = settings.DailyTokenLimit.ToString();
        WarnAtEightyToggle.IsChecked = settings.WarnAtEightyPercent;
    }

    private IAppSettings settings => _settings;

    // ── Event wiring ─────────────────────────────────────────────────────────

    private void WireEvents()
    {
        // Sidebar — sw between content panels
        TabApi.Checked        += (_, _) => ShowPanel(ApiPanel);
        TabBehavior.Checked   += (_, _) => ShowPanel(BehaviorPanel);
        TabAppearance.Checked += (_, _) => ShowPanel(AppearancePanel);
        TabSystem.Checked     += (_, _) => ShowPanel(SystemPanel);
        TabUsage.Checked      += (_, _) => ShowPanel(UsagePanel);

        // Debounce label sync
        DebounceSlider.ValueChanged += (_, e) =>
            DebounceValue.Text = $"{(int)e.NewValue}ms";

        // Show/hide API key
        ShowKeyBtn.Click += (_, _) => ToggleKeyVisibility();

        // Reset usage counter
        ResetUsageBtn.Click += async (_, _) =>
        {
            await _usageRepo.ResetAsync();
            await RefreshUsageAsync();
        };

        // Close = save and close (no separate Save button by design)
        CloseBtn.Click += (_, _) => { SaveValues(); Close(); };
    }

    private void ShowPanel(WpfPanel target)
    {
        var panels = new WpfPanel[] { ApiPanel, BehaviorPanel, AppearancePanel, SystemPanel, UsagePanel };
        foreach (var p in panels)
            p.Visibility = ReferenceEquals(p, target) ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ToggleKeyVisibility()
    {
        if (_keyVisible)
        {
            ApiKeyMasked.Password = ApiKeyPlain.Text;
            ApiKeyPlain.Visibility   = Visibility.Collapsed;
            ApiKeyMasked.Visibility  = Visibility.Visible;
            ShowKeyBtn.Content = "показать";
            _keyVisible = false;
        }
        else
        {
            ApiKeyPlain.Text = ApiKeyMasked.Password;
            ApiKeyMasked.Visibility = Visibility.Collapsed;
            ApiKeyPlain.Visibility  = Visibility.Visible;
            ShowKeyBtn.Content = "скрыть";
            _keyVisible = true;
        }
    }

    private string GetApiKey()
        => _keyVisible ? ApiKeyPlain.Text : ApiKeyMasked.Password;

    private void OnHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    // ── Save back to IAppSettings ────────────────────────────────────────────

    private void SaveValues()
    {
        _settings.LlmProvider  = ProviderGemini.IsChecked == true ? "gemini" : "groq";
        _settings.ApiKey       = GetApiKey();
        _settings.DebounceMs   = (int)DebounceSlider.Value;
        _settings.TranslateHotKey = HotKeyBox.Text.Trim();
        _settings.DetectEnglish   = DetectEnglishToggle.IsChecked == true;
        _settings.DetectSpanish   = DetectSpanishToggle.IsChecked == true;
        _settings.AutoSaveToVocab = AutoSaveToggle.IsChecked == true;
        _settings.Theme           = ThemeDark.IsChecked == true ? "dark" : "light";
        _settings.StartWithWindows = StartWithWindowsToggle.IsChecked == true;
        if (int.TryParse(DailyLimitBox.Text, out var limit)) _settings.DailyTokenLimit = limit;
        _settings.WarnAtEightyPercent = WarnAtEightyToggle.IsChecked == true;
        _settings.Save();

        ApplyStartWithWindows(_settings.StartWithWindows);
    }

    // ── Usage block ──────────────────────────────────────────────────────────

    private async Task RefreshUsageAsync()
    {
        var today = await _usageRepo.GetTodaySummaryAsync();
        var limit = _settings.DailyTokenLimit;
        var pct = limit > 0 ? (double)today.TotalTokens / limit * 100 : 0;
        UsageBar.Value = pct;
        // Цвет fill меняется в зависимости от уровня — берём из ресурсов.
        var brushKey = pct >= 90 ? "BadBrush" : pct >= 80 ? "AccentBrush" : "OkBrush";
        UsageBar.Foreground = (System.Windows.Media.Brush)FindResource(brushKey);

        var providerLabel = _settings.LlmProvider == "groq" ? "Groq free" : "Gemini";
        var costLabel = today.EstimatedCostUsd == 0 ? "$0.00" : $"${today.EstimatedCostUsd:F4}";
        UsageTokensText.Text = limit > 0
            ? $"{today.TotalTokens:N0} / {limit:N0} токенов"
            : $"{today.TotalTokens:N0} токенов";
        UsageCostText.Text = $"{costLabel} · {providerLabel}";
    }

    private static void ApplyStartWithWindows(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (enabled)
                key?.SetValue("LinguaLens", Process.GetCurrentProcess().MainModule!.FileName!);
            else
                key?.DeleteValue("LinguaLens", throwOnMissingValue: false);
        }
        catch { /* non-critical */ }
    }
}
