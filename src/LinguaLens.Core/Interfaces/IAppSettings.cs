namespace LinguaLens.Core.Interfaces;

public interface IAppSettings
{
    string LlmProvider { get; set; }    // "groq" | "gemini"
    string ApiKey { get; set; }
    int DebounceMs { get; set; }         // default: 400
    string HotKey { get; set; }          // default: "Alt+Shift+L"  (toggle on/off)
    string TranslateHotKey { get; set; } // default: "Ctrl+~" (translate on demand)
    bool DetectEnglish { get; set; }     // default: true
    bool DetectSpanish { get; set; }     // default: true
    bool AutoSaveToVocab { get; set; }   // default: true
    string Theme { get; set; }           // "light" | "dark"
    bool StartWithWindows { get; set; }
    int DailyTokenLimit { get; set; }       // default: 100_000
    bool WarnAtEightyPercent { get; set; }  // default: true
    void Save();
}
