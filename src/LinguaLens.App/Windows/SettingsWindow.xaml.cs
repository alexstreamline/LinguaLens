using System.Diagnostics;
using System.Windows;
using LinguaLens.Core.Interfaces;
using Microsoft.Win32;

namespace LinguaLens.App.Windows;

public partial class SettingsWindow : Window
{
    private readonly IAppSettings _settings;

    public SettingsWindow(IAppSettings settings)
    {
        InitializeComponent();
        _settings = settings;

        // Load current values into controls
        ProviderCombo.SelectedIndex = settings.LlmProvider == "gemini" ? 1 : 0;
        ApiKeyBox.Password = settings.ApiKey;
        DebounceSlider.Value = settings.DebounceMs;
        DebounceValue.Text = settings.DebounceMs.ToString();
        HotKeyBox.Text = settings.HotKey;
        DetectEnglishCheck.IsChecked = settings.DetectEnglish;
        DetectSpanishCheck.IsChecked = settings.DetectSpanish;
        AutoSaveCheck.IsChecked = settings.AutoSaveToVocab;
        LightTheme.IsChecked = settings.Theme == "light";
        DarkTheme.IsChecked = settings.Theme == "dark";
        StartWithWindowsCheck.IsChecked = settings.StartWithWindows;

        DebounceSlider.ValueChanged += (_, e) =>
            DebounceValue.Text = ((int)e.NewValue).ToString();

        ShowKeyButton.Click += (_, _) =>
        {
            if (ShowKeyButton.Tag as string == "visible")
            {
                ApiKeyBox.Visibility = Visibility.Visible;
                ShowKeyButton.Tag = "hidden";
            }
            else
            {
                ApiKeyBox.Visibility = Visibility.Collapsed;
                ShowKeyButton.Tag = "visible";
            }
        };

        SaveButton.Click += (_, _) => Save();
    }

    private void Save()
    {
        _settings.LlmProvider = (ProviderCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString() ?? "groq";
        _settings.ApiKey = ApiKeyBox.Password;
        _settings.DebounceMs = (int)DebounceSlider.Value;
        _settings.HotKey = HotKeyBox.Text.Trim();
        _settings.DetectEnglish = DetectEnglishCheck.IsChecked == true;
        _settings.DetectSpanish = DetectSpanishCheck.IsChecked == true;
        _settings.AutoSaveToVocab = AutoSaveCheck.IsChecked == true;
        _settings.Theme = DarkTheme.IsChecked == true ? "dark" : "light";
        _settings.StartWithWindows = StartWithWindowsCheck.IsChecked == true;
        _settings.Save();

        ApplyStartWithWindows(_settings.StartWithWindows);
        Close();
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
