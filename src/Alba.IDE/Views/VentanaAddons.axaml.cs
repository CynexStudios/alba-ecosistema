using Alba.IDE.Services;
using Alba.IDE.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Alba.IDE.Views;

public partial class VentanaAddons : Window
{
    private readonly AddonsViewModel? _vm;

    public VentanaAddons()
    {
        InitializeComponent();
    }

    public VentanaAddons(GestorAddons gestorAddons) : this()
    {
        _vm = new AddonsViewModel(gestorAddons);
        DataContext = _vm;
    }

    private async void AlInstalarCarpeta(object? sender, RoutedEventArgs e)
    {
        if (_vm is null) return;

        IReadOnlyList<IStorageFolder> carpetas = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Seleccionar carpeta de complemento (con manifest.json)",
                AllowMultiple = false
            });

        if (carpetas.Count > 0)
        {
            string? ruta = carpetas[0].TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(ruta))
            {
                _vm.InstalarDesdeCarpeta(ruta);
            }
        }
    }
}
