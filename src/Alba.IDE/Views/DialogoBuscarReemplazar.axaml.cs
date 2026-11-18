using Alba.IDE.Controls;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Alba.IDE.Views;

public partial class DialogoBuscarReemplazar : Window
{
    private readonly EditorCodigoAlba? _editor;

    public DialogoBuscarReemplazar()
    {
        InitializeComponent();
    }

    public DialogoBuscarReemplazar(EditorCodigoAlba editor) : this()
    {
        _editor = editor;
        Opened += (_, _) => TxtBuscar.Focus();
    }

    private void AlBuscarSiguiente(object? sender, RoutedEventArgs e)
    {
        if (_editor is null) return;
        bool encontrado = _editor.BuscarSiguiente(TxtBuscar.Text ?? string.Empty, ChkMayusculas.IsChecked == true);
        TxtResultado.Text = encontrado ? "Coincidencia encontrada." : "No se encontraron coincidencias.";
    }

    private void AlReemplazar(object? sender, RoutedEventArgs e)
    {
        if (_editor is null) return;
        bool ok = _editor.ReemplazarActual(
            TxtBuscar.Text ?? string.Empty,
            TxtReemplazar.Text ?? string.Empty,
            ChkMayusculas.IsChecked == true);
        TxtResultado.Text = ok ? "Reemplazado y avanzada búsqueda." : "No hay coincidencia activa.";
    }

    private void AlReemplazarTodo(object? sender, RoutedEventArgs e)
    {
        if (_editor is null) return;
        int total = _editor.ReemplazarTodo(
            TxtBuscar.Text ?? string.Empty,
            TxtReemplazar.Text ?? string.Empty,
            ChkMayusculas.IsChecked == true);
        TxtResultado.Text = $"Se reemplazaron {total} coincidencia(s).";
    }
}
