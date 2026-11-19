using System.Collections.ObjectModel;
using Alba.IDE.Models;
using Avalonia.Threading;

namespace Alba.IDE.Services;

/// <summary>
/// Administra múltiples sesiones de terminal (pestañas) con modalidades reales del sistema,
/// historial por sesión y colores diferenciados por tipo de salida.
/// </summary>
public sealed class GestorTerminales : IDisposable
{
    private readonly Dictionary<string, ServicioTerminal> _servicios = [];
    private int _contador = 1;

    public ObservableCollection<SesionTerminalTab> Sesiones { get; } = [];
    public SesionTerminalTab? SesionActiva { get; private set; }

    public event Action<SesionTerminalTab?>? SesionActivaCambiada;

    public SesionTerminalTab CrearSesion(ModalidadTerminal modalidad, string? directorioTrabajo = null)
    {
        int numero = _contador++;
        string tituloBase = ServicioTerminal.ObtenerNombreModalidad(modalidad);
        string cwd = !string.IsNullOrWhiteSpace(directorioTrabajo) && Directory.Exists(directorioTrabajo)
            ? directorioTrabajo
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var tab = new SesionTerminalTab
        {
            Modalidad = modalidad,
            Titulo = $"{tituloBase} #{numero}",
            DirectorioTrabajo = cwd,
            Activa = false
        };

        var servicio = new ServicioTerminal();
        servicio.LineaRecibida += (linea, esError) =>
        {
            AgregarLineaASesion(tab, linea, esError ? "#F87171" : "#E2E8F0");
        };

        _servicios[tab.Id] = servicio;
        Sesiones.Add(tab);
        SeleccionarSesion(tab);
        servicio.Iniciar(cwd, modalidad);
        return tab;
    }

    public void SeleccionarSesion(SesionTerminalTab? tab)
    {
        foreach (SesionTerminalTab item in Sesiones)
        {
            item.Activa = ReferenceEquals(item, tab);
        }

        SesionActiva = tab;
        SesionActivaCambiada?.Invoke(tab);
    }

    public void CerrarSesion(SesionTerminalTab? tab)
    {
        if (tab is null) return;

        if (_servicios.TryGetValue(tab.Id, out ServicioTerminal? servicio))
        {
            servicio.Dispose();
            _servicios.Remove(tab.Id);
        }

        int idx = Sesiones.IndexOf(tab);
        if (idx >= 0)
        {
            Sesiones.RemoveAt(idx);
        }

        if (ReferenceEquals(SesionActiva, tab))
        {
            SesionTerminalTab? siguiente = Sesiones.Count > 0
                ? Sesiones[Math.Min(idx, Sesiones.Count - 1)]
                : null;
            SeleccionarSesion(siguiente);
        }
    }

    public void EnviarComando(string comando, string? directorioFallback = null)
    {
        if (string.IsNullOrWhiteSpace(comando)) return;

        SesionTerminalTab tab = SesionActiva ?? CrearSesion(ModalidadTerminal.IntegradaIde, directorioFallback);
        tab.HistorialComandos.Add(comando);
        tab.IndiceHistorial = tab.HistorialComandos.Count;

        AgregarLineaASesion(tab, $"> {comando}", "#38BDF8");

        if (_servicios.TryGetValue(tab.Id, out ServicioTerminal? servicio))
        {
            servicio.EnviarComando(comando, tab.DirectorioTrabajo, tab.Modalidad);
        }
    }

    public string? NavegarHistorial(bool haciaAtras)
    {
        if (SesionActiva is null || SesionActiva.HistorialComandos.Count == 0)
        {
            return null;
        }

        if (haciaAtras)
        {
            SesionActiva.IndiceHistorial = Math.Max(0, SesionActiva.IndiceHistorial - 1);
            return SesionActiva.HistorialComandos[SesionActiva.IndiceHistorial];
        }

        SesionActiva.IndiceHistorial = Math.Min(SesionActiva.HistorialComandos.Count, SesionActiva.IndiceHistorial + 1);
        return SesionActiva.IndiceHistorial >= SesionActiva.HistorialComandos.Count
            ? string.Empty
            : SesionActiva.HistorialComandos[SesionActiva.IndiceHistorial];
    }

    public void CambiarDirectorioSesiones(string carpeta)
    {
        if (!Directory.Exists(carpeta)) return;

        foreach (SesionTerminalTab tab in Sesiones)
        {
            tab.DirectorioTrabajo = carpeta;
            if (_servicios.TryGetValue(tab.Id, out ServicioTerminal? servicio) && servicio.EstaActiva)
            {
                servicio.CambiarDirectorio(carpeta);
            }
        }
    }

    public void DetenerSesionActiva()
    {
        if (SesionActiva is null) return;
        if (_servicios.TryGetValue(SesionActiva.Id, out ServicioTerminal? servicio))
        {
            servicio.Detener();
        }
    }

    public void LimpiarSesionActiva()
    {
        SesionActiva?.Lineas.Clear();
    }

    public static IReadOnlyList<string> ObtenerComandosRecomendados(string? rutaArchivo)
    {
        string ext = Path.GetExtension(rutaArchivo ?? string.Empty).ToLowerInvariant();
        return ext switch
        {
            ".cibao" or ".alb" => ["dotnet --version", "dir", "ls"],
            ".py" => ["python --version", "pip list", "python -m pytest"],
            ".cs" => ["dotnet --version", "dotnet build", "dotnet run"],
            ".cpp" or ".cc" => ["g++ --version", "cmake --version"],
            _ => ["git status", "dotnet --version", "python --version"]
        };
    }

    private static void AgregarLineaASesion(SesionTerminalTab tab, string texto, string colorHex)
    {
        void Insertar()
        {
            tab.Lineas.Add(new LineaTerminalItem
            {
                Texto = texto,
                ColorHex = colorHex
            });
            if (tab.Lineas.Count > 600)
            {
                tab.Lineas.RemoveAt(0);
            }
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Insertar();
        }
        else
        {
            Dispatcher.UIThread.Post(Insertar);
        }
    }

    public void Dispose()
    {
        foreach (ServicioTerminal servicio in _servicios.Values)
        {
            servicio.Dispose();
        }
        _servicios.Clear();
    }
}
