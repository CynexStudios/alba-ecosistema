using System.Collections.ObjectModel;
using Alba.IDE.Models;
using Alba.IDE.Services;

namespace Alba.IDE.ViewModels;

/// <summary>
/// ViewModel de la ventana de gestión de complementos (/addons + catálogo API en línea).
/// </summary>
public sealed class AddonsViewModel : ViewModelBase
{
    private readonly GestorAddons _gestorAddons;
    private AddonCargado? _addonSeleccionado;
    private AddonCatalogoEnLinea? _addonCatalogoSeleccionado;
    private string _mensajeEstado = string.Empty;
    private string _filtroCatalogo = string.Empty;
    private bool _ocupado;
    private List<AddonCatalogoEnLinea> _catalogoCompleto = [];

    public ObservableCollection<AddonCargado> Addons { get; } = [];
    public ObservableCollection<AddonCatalogoEnLinea> CatalogoEnLinea { get; } = [];

    public AddonCargado? AddonSeleccionado
    {
        get => _addonSeleccionado;
        set
        {
            if (SetProperty(ref _addonSeleccionado, value))
            {
                ComandoAlternarEstado.NotificarCambio();
                ComandoDesinstalar.NotificarCambio();
            }
        }
    }

    public AddonCatalogoEnLinea? AddonCatalogoSeleccionado
    {
        get => _addonCatalogoSeleccionado;
        set
        {
            if (SetProperty(ref _addonCatalogoSeleccionado, value))
            {
                ComandoInstalarEnLinea.NotificarCambio();
            }
        }
    }

    public string FiltroCatalogo
    {
        get => _filtroCatalogo;
        set
        {
            if (SetProperty(ref _filtroCatalogo, value))
            {
                AplicarFiltroCatalogo();
            }
        }
    }

    public bool Ocupado
    {
        get => _ocupado;
        private set
        {
            if (SetProperty(ref _ocupado, value))
            {
                ComandoConsultarEnLinea.NotificarCambio();
                ComandoInstalarEnLinea.NotificarCambio();
            }
        }
    }

    public string MensajeEstado
    {
        get => _mensajeEstado;
        private set => SetProperty(ref _mensajeEstado, value);
    }

    public string DirectorioAddons => _gestorAddons.DirectorioAddons;

    public RelayCommand ComandoRecargar { get; }
    public RelayCommand ComandoAlternarEstado { get; }
    public RelayCommand ComandoDesinstalar { get; }
    public RelayCommand ComandoConsultarEnLinea { get; }
    public RelayCommand ComandoInstalarEnLinea { get; }

    public AddonsViewModel(GestorAddons gestorAddons)
    {
        _gestorAddons = gestorAddons;
        ComandoRecargar = new RelayCommand(_ => Recargar());
        ComandoAlternarEstado = new RelayCommand(
            _ => AlternarEstadoSeleccionado(),
            _ => AddonSeleccionado is not null && AddonSeleccionado.EsValido);
        ComandoDesinstalar = new RelayCommand(
            _ => DesinstalarSeleccionado(),
            _ => AddonSeleccionado is not null);
        ComandoConsultarEnLinea = new RelayCommand(
            _ => _ = CargarCatalogoEnLineaAsync(),
            _ => !Ocupado);
        ComandoInstalarEnLinea = new RelayCommand(
            _ => _ = InstalarSeleccionadoEnLineaAsync(),
            _ => !Ocupado && AddonCatalogoSeleccionado is not null);

        Recargar();
        _ = CargarCatalogoEnLineaAsync();
    }

    public void Recargar()
    {
        _gestorAddons.RecargarAddons();
        Addons.Clear();
        foreach (AddonCargado addon in _gestorAddons.Addons)
        {
            Addons.Add(addon);
        }
        AddonSeleccionado = Addons.FirstOrDefault();
        MensajeEstado = $"Se detectaron {Addons.Count} complemento(s) en {DirectorioAddons}.";
    }

    public async Task CargarCatalogoEnLineaAsync()
    {
        Ocupado = true;
        MensajeEstado = "Consultando catálogo de complementos en línea vía API...";
        try
        {
            var (catalogo, fuente) = await _gestorAddons.ConsultarCatalogoEnLineaAsync();
            _catalogoCompleto = catalogo.ToList();
            AplicarFiltroCatalogo();
            MensajeEstado = $"{_catalogoCompleto.Count} complemento(s) listos para descarga en 1 clic ({fuente}).";
        }
        finally
        {
            Ocupado = false;
        }
    }

    private void AplicarFiltroCatalogo()
    {
        string q = (FiltroCatalogo ?? string.Empty).Trim();
        IEnumerable<AddonCatalogoEnLinea> filtrados = string.IsNullOrWhiteSpace(q)
            ? _catalogoCompleto
            : _catalogoCompleto.Where(a =>
                a.Nombre.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.Lenguaje.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.Descripcion.Contains(q, StringComparison.OrdinalIgnoreCase));

        CatalogoEnLinea.Clear();
        foreach (AddonCatalogoEnLinea item in filtrados)
        {
            CatalogoEnLinea.Add(item);
        }
        AddonCatalogoSeleccionado ??= CatalogoEnLinea.FirstOrDefault();
    }

    private async Task InstalarSeleccionadoEnLineaAsync()
    {
        if (AddonCatalogoSeleccionado is null)
        {
            return;
        }

        Ocupado = true;
        MensajeEstado = $"Descargando e instalando '{AddonCatalogoSeleccionado.Nombre}'...";
        try
        {
            var (exito, mensaje) = await _gestorAddons.InstalarDesdeCatalogoEnLineaAsync(AddonCatalogoSeleccionado);
            Recargar();
            AplicarFiltroCatalogo();
            MensajeEstado = exito ? mensaje : $"Error: {mensaje}";
        }
        finally
        {
            Ocupado = false;
        }
    }

    public void InstalarDesdeCarpeta(string carpeta)
    {
        var (exito, mensaje) = _gestorAddons.InstalarDesdeCarpeta(carpeta);
        MensajeEstado = mensaje;
        if (exito)
        {
            Recargar();
        }
    }

    private void AlternarEstadoSeleccionado()
    {
        if (AddonSeleccionado is null)
        {
            return;
        }
        bool nuevoEstado = !AddonSeleccionado.Activo;
        _gestorAddons.CambiarEstado(AddonSeleccionado.Manifiesto.Id, nuevoEstado);
        string nombre = AddonSeleccionado.Manifiesto.Nombre;
        Recargar();
        MensajeEstado = nuevoEstado
            ? $"Complemento '{nombre}' activado."
            : $"Complemento '{nombre}' desactivado.";
    }

    private void DesinstalarSeleccionado()
    {
        if (AddonSeleccionado is null)
        {
            return;
        }
        var (_, mensaje) = _gestorAddons.DesinstalarAddon(AddonSeleccionado.Manifiesto.Id);
        Recargar();
        MensajeEstado = mensaje;
    }
}
