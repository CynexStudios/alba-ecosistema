using System.ComponentModel;
using System.Runtime.CompilerServices;
using AvaloniaEdit.Document;

namespace Alba.IDE.Models;

/// <summary>
/// Representa una pestaña de archivo abierta dentro del editor (en disco o borrador sin carpeta).
/// </summary>
public sealed class ArchivoAbierto : INotifyPropertyChanged
{
    private string _rutaArchivo;
    private string _nombreArchivo;
    private bool _tieneCambiosSinGuardar;
    private string _lenguaje;
    private bool _esBorradorSinRuta;

    public TextDocument Documento { get; }

    public string RutaArchivo
    {
        get => _rutaArchivo;
        set
        {
            if (_rutaArchivo != value)
            {
                _rutaArchivo = value;
                OnPropertyChanged();
                NombreArchivo = Path.GetFileName(value);
            }
        }
    }

    public string NombreArchivo
    {
        get => _nombreArchivo;
        private set
        {
            if (_nombreArchivo != value)
            {
                _nombreArchivo = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TituloPestana));
            }
        }
    }

    public bool TieneCambiosSinGuardar
    {
        get => _tieneCambiosSinGuardar;
        set
        {
            if (_tieneCambiosSinGuardar != value)
            {
                _tieneCambiosSinGuardar = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TituloPestana));
            }
        }
    }

    public bool EsBorradorSinRuta
    {
        get => _esBorradorSinRuta;
        set
        {
            if (_esBorradorSinRuta != value)
            {
                _esBorradorSinRuta = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TituloPestana));
            }
        }
    }

    public string Lenguaje
    {
        get => _lenguaje;
        set
        {
            if (_lenguaje != value)
            {
                _lenguaje = value;
                OnPropertyChanged();
            }
        }
    }

    public string TituloPestana => TieneCambiosSinGuardar ? $"{NombreArchivo} *" : NombreArchivo;

    public ArchivoAbierto(string rutaArchivo, string contenidoInicial, string lenguaje, bool esBorradorSinRuta = false)
    {
        _rutaArchivo = rutaArchivo;
        _nombreArchivo = Path.GetFileName(rutaArchivo);
        _lenguaje = lenguaje;
        _esBorradorSinRuta = esBorradorSinRuta;
        _tieneCambiosSinGuardar = esBorradorSinRuta;
        Documento = new TextDocument(contenidoInicial);
        Documento.Changed += (_, _) => TieneCambiosSinGuardar = true;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? prop = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
}
