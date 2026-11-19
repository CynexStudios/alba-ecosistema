using System.Collections.ObjectModel;
using Alba.IDE.ViewModels;

namespace Alba.IDE.Models;

public enum ModalidadTerminal
{
    IntegradaIde,
    PowerShellWindows,
    CmdWindows,
    BashSistema,
    ZshSistema
}

public sealed class LineaTerminalItem
{
    public string Texto { get; init; } = string.Empty;
    public string ColorHex { get; init; } = "#E2E8F0";
}

public sealed class SesionTerminalTab : ViewModelBase
{
    private string _titulo = "Terminal";
    private string _directorioTrabajo = string.Empty;
    private string _comandoActual = string.Empty;
    private bool _activa;

    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];
    public ModalidadTerminal Modalidad { get; init; } = ModalidadTerminal.IntegradaIde;

    public string Titulo
    {
        get => _titulo;
        set => SetProperty(ref _titulo, value);
    }

    public string DirectorioTrabajo
    {
        get => _directorioTrabajo;
        set => SetProperty(ref _directorioTrabajo, value);
    }

    public string ComandoActual
    {
        get => _comandoActual;
        set => SetProperty(ref _comandoActual, value);
    }

    public bool Activa
    {
        get => _activa;
        set => SetProperty(ref _activa, value);
    }

    public ObservableCollection<LineaTerminalItem> Lineas { get; } = [];
    public List<string> HistorialComandos { get; } = [];
    public int IndiceHistorial { get; set; } = -1;
}
