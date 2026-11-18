using Alba.IDE.Models;
using Avalonia.Controls;
using Avalonia.Input;

namespace Alba.IDE.Views;

public partial class VentanaPaletaComandos : Window
{
    private readonly List<ComandoPaleta> _todos = [];

    public VentanaPaletaComandos()
    {
        InitializeComponent();
        Opened += (_, _) => TxtFiltro.Focus();
        KeyDown += AlTecla;
    }

    public VentanaPaletaComandos(IEnumerable<ComandoPaleta> comandos, string? placeholder = null) : this()
    {
        _todos.AddRange(comandos);
        if (!string.IsNullOrWhiteSpace(placeholder))
        {
            TxtFiltro.Watermark = placeholder;
        }
        AplicarFiltro(string.Empty);
    }

    private void AlCambiarFiltro(object? sender, TextChangedEventArgs e) =>
        AplicarFiltro(TxtFiltro.Text ?? string.Empty);

    private void AplicarFiltro(string texto)
    {
        IEnumerable<ComandoPaleta> filtrados = string.IsNullOrWhiteSpace(texto)
            ? _todos
            : _todos.Where(c =>
                c.Titulo.Contains(texto, StringComparison.OrdinalIgnoreCase) ||
                c.Categoria.Contains(texto, StringComparison.OrdinalIgnoreCase));

        ListaComandos.ItemsSource = filtrados.ToList();
        if (ListaComandos.ItemCount > 0)
        {
            ListaComandos.SelectedIndex = 0;
        }
    }

    private void AlTecla(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            EjecutarSeleccionado();
        }
        else if (e.Key == Key.Down && ListaComandos.ItemCount > 0)
        {
            ListaComandos.SelectedIndex = Math.Min(ListaComandos.ItemCount - 1, ListaComandos.SelectedIndex + 1);
            e.Handled = true;
        }
        else if (e.Key == Key.Up && ListaComandos.ItemCount > 0)
        {
            ListaComandos.SelectedIndex = Math.Max(0, ListaComandos.SelectedIndex - 1);
            e.Handled = true;
        }
    }

    private void AlDobleClicItem(object? sender, TappedEventArgs e) => EjecutarSeleccionado();

    private void EjecutarSeleccionado()
    {
        if (ListaComandos.SelectedItem is ComandoPaleta cmd)
        {
            Close();
            cmd.Ejecutar();
        }
    }
}
