using WpfApplication = System.Windows.Application;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
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

        _serviceProvider = (ServiceProvider)BuildServices();

        // Apply DB schema
        var db = _serviceProvider.GetRequiredService<LinguaLensDbContext>();
        try
        {
            db.Database.Migrate();
            db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
        }
        catch { /* non-critical on subsequent runs */ }

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

        _trayIconManager.OpenVocabRequested += (_, _) =>
        {
            var vm = _serviceProvider.GetRequiredService<VocabViewModel>();
            var win = new VocabWindow(vm);
            win.Show();
        };

        _trayIconManager.OpenSettingsRequested += (_, _) =>
        {
            var settings = _serviceProvider.GetRequiredService<IAppSettings>();
            var win = new SettingsWindow(settings);
            win.ShowDialog();
        };
    }

    private IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        var settings = AppSettings.Load();
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

        // Language detection
        services.AddSingleton<ILanguageDetector, SimpleLanguageDetector>();

        // Text extraction
        services.AddSingleton<UiaTextExtractor>();
        services.AddSingleton<ClipboardTextExtractor>();
        services.AddSingleton<ITextExtractor, CompositeTextExtractor>();

        // LLM client — selected by provider setting
        if (settings.LlmProvider == "gemini")
        {
            services.AddSingleton<ILlmClient>(_ => new GeminiLlmClient(new HttpClient(), settings.ApiKey));
        }
        else
        {
            var groqHttp = new HttpClient();
            groqHttp.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", settings.ApiKey);
            services.AddSingleton<ILlmClient>(new GroqLlmClient(groqHttp));
        }

        // Export
        services.AddSingleton<CsvVocabExporter>();
        services.AddSingleton<IVocabExporter, AnkiExporter>();

        // Core services
        services.AddSingleton<TranslationOrchestrator>();

        // Infrastructure
        services.AddSingleton<GlobalMouseHook>();

        // App / UI
        services.AddSingleton<OverlayWindow>();
        services.AddSingleton<DebounceController>();
        services.AddSingleton<TrayIconManager>();
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
