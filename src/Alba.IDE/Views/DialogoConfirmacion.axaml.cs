using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Alba.IDE.Views;

public partial class DialogoConfirmacion : Window
{
    public DialogoConfirmacion()
    {
        InitializeComponent();
    }

    public DialogoConfirmacion(string tituloVentana, string titulo, string mensaje, string textoConfirmar = "Confirmar") : this()
    {
        Title = tituloVentana;
        TxtTitulo.Text = titulo;
        TxtMensaje.Text = mensaje;
        BtnConfirmar.Content = textoConfirmar;
    }

    private void AlConfirmar(object? sender, RoutedEventArgs e) => Close(true);

    private void AlCancelar(object? sender, RoutedEventArgs e) => Close(false);
}
