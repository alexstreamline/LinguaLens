using System.ComponentModel;
using System.Runtime.CompilerServices;
using LinguaLens.Core.Interfaces;

namespace LinguaLens.Infrastructure.Settings;

/// <summary>
/// Persists settings to %AppData%/LinguaLens/settings.json via System.Text.Json.
/// Implements INotifyPropertyChanged for WPF bindings.
/// </summary>
public class AppSettings : IAppSettings, INotifyPropertyChanged
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LinguaLens", "settings.json");

    private string _llmProvider = "groq";
    private string _apiKey = "";
    private int _debounceMs = 400;
    private string _hotKey = "Alt+Shift+L";
    private bool _detectEnglish = true;
    private bool _detectSpanish = true;
    private bool _autoSaveToVocab = true;
    private string _theme = "light";
    private bool _startWithWindows;

    public string LlmProvider
    {
        get => _llmProvider;
        set { _llmProvider = value; OnPropertyChanged(); }
    }

    public string ApiKey
    {
        get => _apiKey;
        set { _apiKey = value; OnPropertyChanged(); }
    }

    public int DebounceMs
    {
        get => _debounceMs;
        set { _debounceMs = value; OnPropertyChanged(); }
    }

    public string HotKey
    {
        get => _hotKey;
        set { _hotKey = value; OnPropertyChanged(); }
    }

    public bool DetectEnglish
    {
        get => _detectEnglish;
        set { _detectEnglish = value; OnPropertyChanged(); }
    }

    public bool DetectSpanish
    {
        get => _detectSpanish;
        set { _detectSpanish = value; OnPropertyChanged(); }
    }

    public bool AutoSaveToVocab
    {
        get => _autoSaveToVocab;
        set { _autoSaveToVocab = value; OnPropertyChanged(); }
    }

    public string Theme
    {
        get => _theme;
        set { _theme = value; OnPropertyChanged(); }
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set { _startWithWindows = value; OnPropertyChanged(); }
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        var json = System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(SettingsPath, json);
    }

    public static AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(SettingsPath);
            return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
