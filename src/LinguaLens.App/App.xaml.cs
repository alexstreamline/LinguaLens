using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LinguaLens.Core.Interfaces;
using LinguaLens.Core.Services;
using LinguaLens.Infrastructure.Data;
using LinguaLens.Infrastructure.Export;
using LinguaLens.Infrastructure.Hooks;
using LinguaLens.Infrastructure.Language;
using LinguaLens.Infrastructure.Llm;
using LinguaLens.Infrastructure.Settings;
using LinguaLens.Infrastructure.TextExtraction;
using LinguaLens.App.Tray;
using LinguaLens.App.Overlay;
using LinguaLens.App.ViewModels;
using LinguaLens.App.Windows;
using WpfApplication = System.Windows.Application;

namespace LinguaLens.App;

public partial class App : WpfApplication
{
    private ServiceProvider? _serviceProvider;
    private GlobalMouseHook? _mouseHook;
    private TrayIconManager? _trayIconManager;
    private DebounceController? _debounceController;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _serviceProvider = ConfigureServices();

        // Apply EF migrations and enable WAL
        var db = _serviceProvider.GetRequiredService<LinguaLensDbContext>();
        db.Database.Migrate();
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");

        var settings = _serviceProvider.GetRequiredService<IAppSettings>();

        // Apply initial theme
        ApplyTheme(settings.Theme);

        // Subscribe to theme changes
        if (settings is System.ComponentModel.INotifyPropertyChanged notifySettings)
        {
            notifySettings.PropertyChanged += (_, pe) =>
            {
                if (pe.PropertyName == nameof(IAppSettings.Theme))
                    ApplyTheme(settings.Theme);
            };
        }

        // Start mouse hook on dedicated STA thread
        _mouseHook = _serviceProvider.GetRequiredService<GlobalMouseHook>();
        _mouseHook.Start();

        // Wire up debounce controller
        _debounceController = _serviceProvider.GetRequiredService<DebounceController>();

        // Init tray icon (must be on UI thread)
        _trayIconManager = _serviceProvider.GetRequiredService<TrayIconManager>();

        bool isEnabled = true;
        _trayIconManager.ToggleRequested += (_, _) =>
        {
            isEnabled = !isEnabled;
            _trayIconManager.SetEnabled(isEnabled);
            if (!isEnabled) _serviceProvider.GetRequiredService<OverlayWindow>().HideOverlay();
        };

        _trayIconManager.TranslateRequested += (_, _) =>
        {
            if (isEnabled)
                _ = _debounceController.TriggerManualAsync();
        };

        _trayIconManager.OpenVocabRequested += (_, _) =>
        {
            var vm = _serviceProvider.GetRequiredService<VocabViewModel>();
            var win = new VocabWindow(vm);
            win.Show();
        };

        void OpenSettings()
        {
            var s = _serviceProvider.GetRequiredService<IAppSettings>();
            var usageRepo = _serviceProvider.GetRequiredService<ITokenUsageRepository>();
            var win = new SettingsWindow(s, usageRepo);
            win.ShowDialog();
        }

        _trayIconManager.OpenSettingsRequested += (_, _) => OpenSettings();

        // Wire OverlayWindow.OpenSettingsRequested
        var overlay = _serviceProvider.GetRequiredService<OverlayWindow>();
        overlay.OpenSettingsRequested += (_, _) => OpenSettings();
    }

    private static void ApplyTheme(string theme)
    {
        var uri = new Uri($"pack://application:,,,/Themes/{(theme == "dark" ? "Dark" : "Light")}Theme.xaml");
        var dict = new ResourceDictionary { Source = uri };
        var merged = Current.Resources.MergedDictionaries;
        if (merged.Count > 0) merged[0] = dict;
        else merged.Insert(0, dict);
    }

    private static ServiceProvider ConfigureServices()
    {
        var settings = AppSettings.Load();
        var services = new ServiceCollection();

        // Settings
        services.AddSingleton<IAppSettings>(settings);

        // Logging
        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LinguaLens", "logs", "app.log");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        services.AddLogging(b => b
            .SetMinimumLevel(LogLevel.Warning)
            .AddFile(logPath));

        // Database
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LinguaLens", "lingualens.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        services.AddSingleton<LinguaLensDbContext>(_ =>
        {
            var opts = new DbContextOptionsBuilder<LinguaLensDbContext>()
                .UseSqlite($"Data Source={dbPath};")
                .Options;
            return new LinguaLensDbContext(opts);
        });

        // Repositories & cache
        services.AddSingleton<ITranslationCache, SqliteTranslationCache>();
        services.AddSingleton<IVocabRepository, SqliteVocabRepository>();
        services.AddSingleton<ITokenUsageRepository, SqliteTokenUsageRepository>();

        // Language detection
        services.AddSingleton<ILanguageDetector, SimpleLanguageDetector>();

        // Text extraction
        services.AddSingleton<UiaTextExtractor>();
        services.AddSingleton<ClipboardTextExtractor>();
        services.AddSingleton<ITextExtractor, CompositeTextExtractor>();

        // LLM clients via IHttpClientFactory with ApiKeyHandler for Groq
        services.AddTransient<ApiKeyHandler>();
        services.AddHttpClient<GroqLlmClient>()
            .AddHttpMessageHandler<ApiKeyHandler>();
        services.AddHttpClient<GeminiLlmClient>();

        // LlmClientFactory selects provider at call time
        services.AddSingleton<ILlmClientFactory>(sp => new LlmClientFactory(
            sp.GetRequiredService<IAppSettings>(),
            sp.GetRequiredService<GroqLlmClient>(),
            sp.GetRequiredService<GeminiLlmClient>()));

        // Export
        services.AddSingleton<CsvVocabExporter>();
        services.AddSingleton<IVocabExporter, AnkiExporter>();

        // Core services
        services.AddSingleton<TranslationOrchestrator>();

        // Infrastructure
        services.AddSingleton<GlobalMouseHook>();

        // App / UI
        services.AddSingleton<OverlayWindow>(sp => new OverlayWindow(
            sp.GetRequiredService<IVocabRepository>(),
            sp.GetRequiredService<IAppSettings>()));
        services.AddSingleton<DebounceController>();
        services.AddSingleton<TrayIconManager>(sp => new TrayIconManager(
            sp.GetRequiredService<IAppSettings>(),
            sp.GetRequiredService<ITokenUsageRepository>()));
        services.AddTransient<VocabViewModel>();

        return services.BuildServiceProvider();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _debounceController?.Dispose();
        _mouseHook?.Dispose();
        _trayIconManager?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
