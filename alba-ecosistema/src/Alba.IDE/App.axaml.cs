using Alba.IDE.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Alba.IDE;

public sealed class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            string? rutaInicial = desktop.Args is { Length: > 0 } ? desktop.Args[0] : null;
            desktop.MainWindow = new VentanaPrincipal(rutaInicial);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
