using System.Windows;
using LinguaLens.Core.Interfaces;

namespace LinguaLens.App.Windows;

public partial class SettingsWindow : Window
{
    private readonly IAppSettings _settings;

    public SettingsWindow(IAppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        throw new NotImplementedException();
    }
}
