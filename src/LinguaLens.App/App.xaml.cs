using WpfApplication = System.Windows.Application;
using WinFormsApp = System.Windows.Forms.Application;
using System.Windows;
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
using Microsoft.EntityFrameworkCore;

namespace LinguaLens.App;

public partial class App : WpfApplication
{
    private ServiceProvider? _serviceProvider;
    private GlobalMouseHook? _mouseHook;
    private TrayIconManager? _trayIconManager;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        throw new NotImplementedException();
    }

    private IServiceProvider BuildServices()
    {
        throw new NotImplementedException();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mouseHook?.Dispose();
        _trayIconManager?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
