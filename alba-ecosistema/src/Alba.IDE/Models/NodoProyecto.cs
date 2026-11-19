using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Alba.IDE.Models;

/// <summary>
/// Nodo de archivo o carpeta dentro del Explorador de Proyectos lateral.
/// </summary>
public sealed class NodoProyecto : INotifyPropertyChanged
{
    private bool _estaExpandido;

    public string Nombre { get; }
    public string RutaCompleta { get; }
    public bool EsCarpeta { get; }
    public ObservableCollection<NodoProyecto> Hijos { get; } = [];

    public bool EstaExpandido
    {
        get => _estaExpandido;
        set
        {
            if (_estaExpandido != value)
            {
                _estaExpandido = value;
                OnPropertyChanged();
            }
        }
    }

    public string IconoTexto => EsCarpeta ? "DIR" : Path.GetExtension(Nombre).ToLowerInvariant() switch
    {
        ".cibao" => "CIB",
        ".alb" => "ALB",
        ".py" => "PY",
        ".cs" => "C#",
        ".cpp" or ".cc" or ".cxx" => "C++",
        ".h" or ".hpp" => "H",
        ".html" or ".htm" => "HTML",
        ".css" => "CSS",
        ".js" => "JS",
        ".json" => "JSON",
        ".md" => "MD",
        _ => "TXT"
    };

    public NodoProyecto(string nombre, string rutaCompleta, bool esCarpeta, bool expandido = false)
    {
        Nombre = nombre;
        RutaCompleta = rutaCompleta;
        EsCarpeta = esCarpeta;
        _estaExpandido = expandido;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? prop = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}
