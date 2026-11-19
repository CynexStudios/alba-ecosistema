using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Alba.IDE.Views;

public partial class DialogoEntradaTexto : Window
{
    public DialogoEntradaTexto()
    {
        InitializeComponent();
    }

    public DialogoEntradaTexto(string titulo, string etiqueta, string valorInicial = "")
        : this()
    {
        Title = titulo;
        TxtEtiqueta.Text = etiqueta;
        TxtValor.Text = valorInicial;
        Opened += (_, _) =>
        {
            TxtValor.Focus();
            TxtValor.SelectAll();
        };
    }

    private void AlAceptar(object? sender, RoutedEventArgs e)
    {
        Close(TxtValor.Text?.Trim());
    }

    private void AlCancelar(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
