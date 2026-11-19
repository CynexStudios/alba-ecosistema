using System.Collections.ObjectModel;
using Alba.IDE.Models;
using Alba.IDE.Services;

namespace Alba.IDE.ViewModels;

/// <summary>
/// ViewModel de la ventana de gestión de complementos (/addons).
/// </summary>
public sealed class AddonsViewModel : ViewModelBase
{
    private readonly GestorAddons _gestorAddons;
    private AddonCargado? _addonSeleccionado;
    private string _mensajeEstado = string.Empty;

    public ObservableCollection<AddonCargado> Addons { get; } = [];

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

    public string MensajeEstado
    {
        get => _mensajeEstado;
        private set => SetProperty(ref _mensajeEstado, value);
    }

    public string DirectorioAddons => _gestorAddons.DirectorioAddons;

    public RelayCommand ComandoRecargar { get; }
    public RelayCommand ComandoAlternarEstado { get; }
    public RelayCommand ComandoDesinstalar { get; }

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

        Recargar();
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
