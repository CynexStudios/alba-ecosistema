using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Alba.IDE.Views;

public partial class DialogoDiffGit : Window
{
    public DialogoDiffGit()
    {
        InitializeComponent();
    }

    public DialogoDiffGit(string rutaRelativa, string subtitulo, string contenidoDiff) : this()
    {
        TxtRutaArchivo.Text = $"Diferencias: {rutaRelativa}";
        TxtSubtitulo.Text = subtitulo;
        TxtDiffContenido.Text = contenidoDiff;
    }

    private async void AlCopiar(object? sender, RoutedEventArgs e)
    {
        if (Clipboard is not null && !string.IsNullOrWhiteSpace(TxtDiffContenido.Text))
        {
            await Clipboard.SetTextAsync(TxtDiffContenido.Text);
        }
    }

    private void AlCerrar(object? sender, RoutedEventArgs e) => Close();
}
