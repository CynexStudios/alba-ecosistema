using System.Collections.ObjectModel;
using Alba.Engine.Diagnostics;
using Alba.Engine.Parser;
using Alba.IDE.Models;
using Alba.IDE.Services;
using Avalonia.Threading;

namespace Alba.IDE.ViewModels;

public enum SeccionBarraLateral
{
    Explorador,
    Busqueda,
    Addons,
    Herramientas
}

public enum PestanaPanelInferior
{
    Consola,
    Problemas,
    Terminal,
    Registro
}

/// <summary>
/// ViewModel principal de Alba Studio 2.0.
/// Coordina bienvenida, proyectos, pestañas, diagnóstico en vivo, consola, terminal, temas y actualizaciones.
/// </summary>
public sealed class VentanaPrincipalViewModel : ViewModelBase, IDisposable
{
    private readonly GestorProyectos _gestorProyectos;
    private readonly GestorAddons _gestorAddons;
    private readonly GestorConfiguracion _gestorConfiguracion;
    private readonly GestorTemas _gestorTemas;
    private readonly GestorActualizaciones _gestorActualizaciones;
    private readonly ServicioEjecucion _servicioEjecucion;
    private readonly ServicioDiagnosticoEnVivo _servicioDiagnostico;
    private readonly ServicioTerminal _servicioTerminal;
    private readonly DispatcherTimer _timerAutoguardado;
    private DispatcherTimer? _timerDebounceDiagnostico;
    private DispatcherTimer? _timerNotificacion;

    private string? _rutaProyectoActual;
    private ArchivoAbierto? _archivoActivo;
    private string _estadoBarra = "Alba Studio v2.0.0 listo";
    private string _posicionCursor = "Ln 1, Col 1";
    private bool _estaEjecutando;
    private CancellationTokenSource? _ctsEjecucion;

    private bool _mostrarPantallaInicio = true;
    private bool _mostrarBarraLateral = true;
    private bool _mostrarPanelInferior = true;
    private SeccionBarraLateral _seccionLateral = SeccionBarraLateral.Explorador;
    private PestanaPanelInferior _pestanaInferior = PestanaPanelInferior.Consola;

    private string _terminoBusquedaProyecto = string.Empty;
    private bool _busquedaMayusculas;
    private string _comandoTerminal = string.Empty;
    private string _textoNotificacion = string.Empty;
    private bool _mostrarNotificacion;
    private bool _hayActualizacionDisponible;
    private string _versionDisponibleTexto = string.Empty;
    private InformacionActualizacion? _ultimaInfoActualizacion;

    public ObservableCollection<NodoProyecto> NodosExplorador { get; } = [];
    public ObservableCollection<ArchivoAbierto> PestanasAbiertas { get; } = [];
    public ObservableCollection<ElementoReciente> Recientes { get; } = [];
    public ObservableCollection<ProblemaCodigo> Problemas { get; } = [];
    public ObservableCollection<ResultadoBusquedaProyecto> ResultadosBusqueda { get; } = [];
    public ObservableCollection<EntradaRegistro> RegistroActividad { get; } = [];
    public ObservableCollection<string> LineasTerminal { get; } = [];
    public ObservableCollection<AddonCargado> AddonsInstalados { get; } = [];
    public ConsolaViewModel Consola { get; } = new();

    public GestorAddons GestorAddons => _gestorAddons;
    public GestorConfiguracion GestorConfiguracion => _gestorConfiguracion;
    public GestorTemas GestorTemas => _gestorTemas;
    public GestorActualizaciones GestorActualizaciones => _gestorActualizaciones;
    public ServicioAutocompletado ServicioAutocompletado { get; }
    public ConfiguracionIDE Configuracion => _gestorConfiguracion.Configuracion;

    public string VersionApp => RutasUsuarioAlba.VersionActual;

    public string? RutaProyectoActual
    {
        get => _rutaProyectoActual;
        private set
        {
            if (SetProperty(ref _rutaProyectoActual, value))
            {
                OnPropertyChanged(nameof(NombreProyectoActual));
                OnPropertyChanged(nameof(TituloVentana));
                OnPropertyChanged(nameof(TieneProyectoAbierto));
                ComandoRefrescarExplorador.NotificarCambio();
            }
        }
    }

    public bool TieneProyectoAbierto => !string.IsNullOrWhiteSpace(RutaProyectoActual);

    public string NombreProyectoActual => string.IsNullOrWhiteSpace(RutaProyectoActual)
        ? "Sin proyecto"
        : Path.GetFileName(RutaProyectoActual.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    public ArchivoAbierto? ArchivoActivo
    {
        get => _archivoActivo;
        set
        {
            if (ReferenceEquals(_archivoActivo, value))
            {
                return;
            }

            if (_archivoActivo is not null)
            {
                _archivoActivo.Documento.Changed -= AlCambiarDocumentoActivo;
            }

            if (SetProperty(ref _archivoActivo, value))
            {
                if (_archivoActivo is not null)
                {
                    _archivoActivo.Documento.Changed += AlCambiarDocumentoActivo;
                }

                OnPropertyChanged(nameof(TituloVentana));
                OnPropertyChanged(nameof(RutaRelativaActiva));
                NotificarComandos();
                EjecutarDiagnosticoEnVivo();
            }
        }
    }

    public string RutaRelativaActiva
    {
        get
        {
            if (ArchivoActivo is null) return "Ningún archivo abierto";
            if (!string.IsNullOrWhiteSpace(RutaProyectoActual) &&
                ArchivoActivo.RutaArchivo.StartsWith(RutaProyectoActual, StringComparison.OrdinalIgnoreCase))
            {
                return $"{NombreProyectoActual} > {Path.GetRelativePath(RutaProyectoActual, ArchivoActivo.RutaArchivo)}";
            }
            return ArchivoActivo.RutaArchivo;
        }
    }

    public bool MostrarPantallaInicio
    {
        get => _mostrarPantallaInicio;
        set
        {
            if (SetProperty(ref _mostrarPantallaInicio, value))
            {
                OnPropertyChanged(nameof(MostrarEspacioTrabajo));
            }
        }
    }

    public bool MostrarEspacioTrabajo => !MostrarPantallaInicio;

    public bool MostrarBarraLateral
    {
        get => _mostrarBarraLateral;
        set
        {
            if (SetProperty(ref _mostrarBarraLateral, value))
            {
                Configuracion.MostrarPanelLateral = value;
                _gestorConfiguracion.GuardarConfiguracion();
            }
        }
    }

    public bool MostrarPanelInferior
    {
        get => _mostrarPanelInferior;
        set
        {
            if (SetProperty(ref _mostrarPanelInferior, value))
            {
                Configuracion.MostrarPanelInferior = value;
                _gestorConfiguracion.GuardarConfiguracion();
            }
        }
    }

    public SeccionBarraLateral SeccionLateral
    {
        get => _seccionLateral;
        set
        {
            if (SetProperty(ref _seccionLateral, value))
            {
                OnPropertyChanged(nameof(EsExploradorActivo));
                OnPropertyChanged(nameof(EsBusquedaActiva));
                OnPropertyChanged(nameof(EsAddonsActivo));
                OnPropertyChanged(nameof(EsHerramientasActivo));
            }
        }
    }

    public bool EsExploradorActivo => SeccionLateral == SeccionBarraLateral.Explorador;
    public bool EsBusquedaActiva => SeccionLateral == SeccionBarraLateral.Busqueda;
    public bool EsAddonsActivo => SeccionLateral == SeccionBarraLateral.Addons;
    public bool EsHerramientasActivo => SeccionLateral == SeccionBarraLateral.Herramientas;

    public PestanaPanelInferior PestanaInferior
    {
        get => _pestanaInferior;
        set
        {
            if (SetProperty(ref _pestanaInferior, value))
            {
                OnPropertyChanged(nameof(EsConsolaActiva));
                OnPropertyChanged(nameof(EsProblemasActiva));
                OnPropertyChanged(nameof(EsTerminalActiva));
                OnPropertyChanged(nameof(EsRegistroActiva));
            }
        }
    }

    public bool EsConsolaActiva => PestanaInferior == PestanaPanelInferior.Consola;
    public bool EsProblemasActiva => PestanaInferior == PestanaPanelInferior.Problemas;
    public bool EsTerminalActiva => PestanaInferior == PestanaPanelInferior.Terminal;
    public bool EsRegistroActiva => PestanaInferior == PestanaPanelInferior.Registro;

    public string EstadoBarra
    {
        get => _estadoBarra;
        set => SetProperty(ref _estadoBarra, value);
    }

    public string PosicionCursor
    {
        get => _posicionCursor;
        set => SetProperty(ref _posicionCursor, value);
    }

    public string NombreTemaActual => _gestorTemas.TemaActual.Nombre;

    public bool EstaEjecutando
    {
        get => _estaEjecutando;
        private set
        {
            if (SetProperty(ref _estaEjecutando, value))
            {
                NotificarComandos();
            }
        }
    }

    public string TerminoBusquedaProyecto
    {
        get => _terminoBusquedaProyecto;
        set => SetProperty(ref _terminoBusquedaProyecto, value);
    }

    public bool BusquedaMayusculas
    {
        get => _busquedaMayusculas;
        set => SetProperty(ref _busquedaMayusculas, value);
    }

    public string ComandoTerminal
    {
        get => _comandoTerminal;
        set => SetProperty(ref _comandoTerminal, value);
    }

    public string TextoNotificacion
    {
        get => _textoNotificacion;
        set => SetProperty(ref _textoNotificacion, value);
    }

    public bool MostrarNotificacion
    {
        get => _mostrarNotificacion;
        set => SetProperty(ref _mostrarNotificacion, value);
    }

    public bool HayActualizacionDisponible
    {
        get => _hayActualizacionDisponible;
        private set => SetProperty(ref _hayActualizacionDisponible, value);
    }

    public string VersionDisponibleTexto
    {
        get => _versionDisponibleTexto;
        private set => SetProperty(ref _versionDisponibleTexto, value);
    }

    public InformacionActualizacion? UltimaInfoActualizacion => _ultimaInfoActualizacion;

    public int TotalProblemas => Problemas.Count;

    public string TituloVentana => ArchivoActivo is not null
        ? $"{ArchivoActivo.TituloPestana} — {NombreProyectoActual} — Alba Studio v{VersionApp}"
        : (TieneProyectoAbierto ? $"{NombreProyectoActual} — Alba Studio v{VersionApp}" : $"Alba Studio v{VersionApp}");

    public RelayCommand ComandoGuardar { get; }
    public RelayCommand ComandoGuardarTodo { get; }
    public RelayCommand ComandoEjecutar { get; }
    public RelayCommand ComandoVerificar { get; }
    public RelayCommand ComandoDetener { get; }
    public RelayCommand ComandoCerrarPestana { get; }
    public RelayCommand ComandoRefrescarExplorador { get; }
    public RelayCommand ComandoBuscarEnProyecto { get; }
    public RelayCommand ComandoEnviarTerminal { get; }
    public RelayCommand ComandoLimpiarTerminal { get; }

    public event Action<ProblemaCodigo>? SolicitarIrAProblema;

    public VentanaPrincipalViewModel()
    {
        _gestorProyectos = new GestorProyectos();
        _gestorAddons = new GestorAddons();
        _gestorConfiguracion = new GestorConfiguracion();
        _gestorTemas = new GestorTemas();
        _gestorActualizaciones = new GestorActualizaciones();
        _servicioEjecucion = new ServicioEjecucion(_gestorAddons);
        _servicioDiagnostico = new ServicioDiagnosticoEnVivo();
        _servicioTerminal = new ServicioTerminal();
        ServicioAutocompletado = new ServicioAutocompletado(_gestorAddons);

        _servicioTerminal.LineaRecibida += (linea, esError) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                LineasTerminal.Add(esError ? $"[ERR] {linea}" : linea);
                if (LineasTerminal.Count > 500)
                {
                    LineasTerminal.RemoveAt(0);
                }
            });
        };

        ComandoGuardar = new RelayCommand(_ => GuardarArchivoActivo(), _ => ArchivoActivo is not null);
        ComandoGuardarTodo = new RelayCommand(_ => GuardarTodos(), _ => PestanasAbiertas.Count > 0);
        ComandoEjecutar = new RelayCommand(_ => _ = EjecutarActivoAsync(), _ => !EstaEjecutando && ArchivoActivo is not null);
        ComandoVerificar = new RelayCommand(_ => VerificarArchivoActivo(), _ => ArchivoActivo is not null);
        ComandoDetener = new RelayCommand(_ => DetenerEjecucion(), _ => EstaEjecutando);
        ComandoCerrarPestana = new RelayCommand(p => CerrarPestana(p as ArchivoAbierto ?? ArchivoActivo));
        ComandoRefrescarExplorador = new RelayCommand(_ => RefrescarExplorador(), _ => TieneProyectoAbierto);
        ComandoBuscarEnProyecto = new RelayCommand(_ => EjecutarBusquedaEnProyecto());
        ComandoEnviarTerminal = new RelayCommand(_ => EnviarComandoTerminal());
        ComandoLimpiarTerminal = new RelayCommand(_ => LineasTerminal.Clear());

        _mostrarBarraLateral = Configuracion.MostrarPanelLateral;
        _mostrarPanelInferior = Configuracion.MostrarPanelInferior;

        _gestorTemas.AplicarTema(Configuracion.TemaId, Configuracion);
        RecargarListaAddons();
        SincronizarRecientes();

        _timerAutoguardado = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(Math.Clamp(Configuracion.IntervaloGuardadoSegundos, 5, 300))
        };
        _timerAutoguardado.Tick += (_, _) =>
        {
            if (Configuracion.GuardadoAutomatico && PestanasAbiertas.Any(p => p.TieneCambiosSinGuardar))
            {
                GuardarTodos(silencioso: true);
            }
        };
        _timerAutoguardado.Start();

        RegistrarActividad("Inicio", $"Alba Studio v{VersionApp} iniciado correctamente.");
    }

    public void InicializarSegunArgumento(string? rutaInicial)
    {
        if (!string.IsNullOrWhiteSpace(rutaInicial))
        {
            if (Directory.Exists(rutaInicial))
            {
                AbrirCarpetaProyecto(rutaInicial);
                return;
            }
            if (File.Exists(rutaInicial))
            {
                string? dir = Path.GetDirectoryName(rutaInicial);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    AbrirCarpetaProyecto(dir);
                }
                AbrirArchivo(rutaInicial);
                return;
            }
        }

        if (Configuracion.RestaurarUltimaSesion &&
            !string.IsNullOrWhiteSpace(_gestorConfiguracion.Sesion.UltimoProyecto) &&
            Directory.Exists(_gestorConfiguracion.Sesion.UltimoProyecto))
        {
            AbrirCarpetaProyecto(_gestorConfiguracion.Sesion.UltimoProyecto, restaurarPestanas: true);
        }
        else
        {
            MostrarPantallaInicio = true;
        }
    }

    public void CrearNuevoProyecto(string carpetaPadre, string nombreProyecto)
    {
        var (rutaProyecto, rutaPrincipal) = _gestorProyectos.CrearProyecto(carpetaPadre, nombreProyecto);
        AbrirCarpetaProyecto(rutaProyecto);
        AbrirArchivo(rutaPrincipal);
        RegistrarActividad("Proyecto", $"Proyecto '{nombreProyecto}' creado en {rutaProyecto}.");
        MostrarToast($"Proyecto '{nombreProyecto}' creado.");
    }

    public void AbrirCarpetaProyecto(string rutaCarpeta, bool restaurarPestanas = false)
    {
        if (!Directory.Exists(rutaCarpeta))
        {
            return;
        }

        RutaProyectoActual = Path.GetFullPath(rutaCarpeta);
        MostrarPantallaInicio = false;
        RefrescarExplorador();

        _gestorConfiguracion.RegistrarReciente(RutaProyectoActual, esProyecto: true);
        SincronizarRecientes();

        if (restaurarPestanas)
        {
            foreach (string ruta in _gestorConfiguracion.Sesion.PestanasAbiertas)
            {
                if (File.Exists(ruta))
                {
                    AbrirArchivo(ruta, registrarReciente: false);
                }
            }
            if (!string.IsNullOrWhiteSpace(_gestorConfiguracion.Sesion.ArchivoActivo))
            {
                ArchivoAbierto? activa = PestanasAbiertas.FirstOrDefault(p =>
                    p.RutaArchivo.Equals(_gestorConfiguracion.Sesion.ArchivoActivo, StringComparison.OrdinalIgnoreCase));
                if (activa is not null) ArchivoActivo = activa;
            }
        }
        else if (PestanasAbiertas.Count == 0)
        {
            string principal = Path.Combine(RutaProyectoActual, "principal.alb");
            if (File.Exists(principal))
            {
                AbrirArchivo(principal);
            }
        }

        if (_servicioTerminal.EstaActiva)
        {
            _servicioTerminal.CambiarDirectorio(RutaProyectoActual);
        }

        GuardarEstadoSesion();
        EstadoBarra = $"Proyecto abierto: {NombreProyectoActual}";
        RegistrarActividad("Proyecto", $"Carpeta abierta: {RutaProyectoActual}");
    }

    public void RefrescarExplorador()
    {
        if (string.IsNullOrWhiteSpace(RutaProyectoActual))
        {
            return;
        }

        NodosExplorador.Clear();
        NodosExplorador.Add(_gestorProyectos.CargarArbolProyecto(RutaProyectoActual));
    }

    public void CrearArchivoEnProyecto(string carpetaDestino, string nombreArchivo)
    {
        string ruta = _gestorProyectos.CrearArchivo(carpetaDestino, nombreArchivo);
        RefrescarExplorador();
        AbrirArchivo(ruta);
        RegistrarActividad("Archivo", $"Creado: {Path.GetFileName(ruta)}");
    }

    public void CrearCarpetaEnProyecto(string carpetaDestino, string nombreCarpeta)
    {
        _gestorProyectos.CrearCarpeta(carpetaDestino, nombreCarpeta);
        RefrescarExplorador();
        RegistrarActividad("Carpeta", $"Carpeta creada: {nombreCarpeta}");
    }

    public void RenombrarNodo(NodoProyecto nodo, string nuevoNombre)
    {
        string rutaAnterior = nodo.RutaCompleta;
        string nuevaRuta = _gestorProyectos.RenombrarElemento(rutaAnterior, nuevoNombre, nodo.EsCarpeta);
        if (nuevaRuta.Equals(rutaAnterior, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!nodo.EsCarpeta)
        {
            ArchivoAbierto? tab = PestanasAbiertas.FirstOrDefault(p =>
                p.RutaArchivo.Equals(rutaAnterior, StringComparison.OrdinalIgnoreCase));
            if (tab is not null)
            {
                tab.RutaArchivo = nuevaRuta;
                OnPropertyChanged(nameof(TituloVentana));
                OnPropertyChanged(nameof(RutaRelativaActiva));
            }
        }

        RefrescarExplorador();
        GuardarEstadoSesion();
        RegistrarActividad("Archivo", $"Renombrado a {Path.GetFileName(nuevaRuta)}");
    }

    public void DuplicarArchivoNodo(NodoProyecto nodo)
    {
        if (nodo.EsCarpeta) return;
        string? copia = _gestorProyectos.DuplicarArchivo(nodo.RutaCompleta);
        if (copia is null) return;
        RefrescarExplorador();
        AbrirArchivo(copia);
        RegistrarActividad("Archivo", $"Duplicado: {Path.GetFileName(copia)}");
    }

    public void EliminarNodo(NodoProyecto nodo)
    {
        _gestorProyectos.EliminarElemento(nodo.RutaCompleta, nodo.EsCarpeta);

        if (!nodo.EsCarpeta)
        {
            ArchivoAbierto? abierta = PestanasAbiertas.FirstOrDefault(p =>
                p.RutaArchivo.Equals(nodo.RutaCompleta, StringComparison.OrdinalIgnoreCase));
            if (abierta is not null)
            {
                CerrarPestana(abierta);
            }
        }

        RefrescarExplorador();
        RegistrarActividad("Archivo", $"Eliminado: {nodo.Nombre}");
    }

    public void AbrirArchivo(string rutaArchivo, bool registrarReciente = true)
    {
        if (!File.Exists(rutaArchivo))
        {
            return;
        }

        MostrarPantallaInicio = false;
        string rutaFull = Path.GetFullPath(rutaArchivo);
        ArchivoAbierto? existente = PestanasAbiertas.FirstOrDefault(p =>
            p.RutaArchivo.Equals(rutaFull, StringComparison.OrdinalIgnoreCase));

        if (existente is not null)
        {
            ArchivoActivo = existente;
            return;
        }

        string contenido = _gestorProyectos.LeerArchivo(rutaFull);
        string lenguaje = DetectarNombreLenguaje(rutaFull);
        var nuevo = new ArchivoAbierto(rutaFull, contenido, lenguaje);
        PestanasAbiertas.Add(nuevo);
        ArchivoActivo = nuevo;

        if (registrarReciente)
        {
            _gestorConfiguracion.RegistrarReciente(rutaFull, esProyecto: false);
            SincronizarRecientes();
        }

        GuardarEstadoSesion();
        EstadoBarra = $"Abierto: {nuevo.NombreArchivo} ({lenguaje})";
    }

    public void GuardarArchivoActivo()
    {
        if (ArchivoActivo is null)
        {
            return;
        }

        _gestorProyectos.GuardarArchivo(ArchivoActivo.RutaArchivo, ArchivoActivo.Documento.Text);
        ArchivoActivo.TieneCambiosSinGuardar = false;
        OnPropertyChanged(nameof(TituloVentana));
        EstadoBarra = $"Guardado: {ArchivoActivo.NombreArchivo}";
        RegistrarActividad("Guardado", $"{ArchivoActivo.NombreArchivo} guardado en disco.");
        MostrarToast($"Guardado: {ArchivoActivo.NombreArchivo}");
    }

    public void GuardarTodos(bool silencioso = false)
    {
        int guardados = 0;
        foreach (ArchivoAbierto tab in PestanasAbiertas)
        {
            if (tab.TieneCambiosSinGuardar)
            {
                _gestorProyectos.GuardarArchivo(tab.RutaArchivo, tab.Documento.Text);
                tab.TieneCambiosSinGuardar = false;
                guardados++;
            }
        }

        OnPropertyChanged(nameof(TituloVentana));
        if (guardados > 0)
        {
            EstadoBarra = silencioso ? "Autoguardado completado." : "Todos los archivos han sido guardados.";
            RegistrarActividad("Guardado", $"{guardados} archivo(s) guardado(s).");
            if (!silencioso)
            {
                MostrarToast("Todos los archivos guardados.");
            }
        }
    }

    public void CerrarPestana(ArchivoAbierto? pestana)
    {
        if (pestana is null)
        {
            return;
        }

        int idx = PestanasAbiertas.IndexOf(pestana);
        if (idx < 0)
        {
            return;
        }

        PestanasAbiertas.RemoveAt(idx);
        if (ReferenceEquals(ArchivoActivo, pestana))
        {
            ArchivoActivo = PestanasAbiertas.Count > 0
                ? PestanasAbiertas[Math.Min(idx, PestanasAbiertas.Count - 1)]
                : null;
        }
        GuardarEstadoSesion();
        NotificarComandos();
    }

    public void CerrarOtrasPestanas(ArchivoAbierto? conservar)
    {
        if (conservar is null) return;
        foreach (var tab in PestanasAbiertas.Where(t => !ReferenceEquals(t, conservar)).ToList())
        {
            PestanasAbiertas.Remove(tab);
        }
        ArchivoActivo = conservar;
        GuardarEstadoSesion();
        NotificarComandos();
    }

    public void CerrarTodasPestanas()
    {
        PestanasAbiertas.Clear();
        ArchivoActivo = null;
        GuardarEstadoSesion();
        NotificarComandos();
    }

    public void VerificarArchivoActivo()
    {
        if (ArchivoActivo is null) return;
        EjecutarDiagnosticoEnVivo();
        PestanaInferior = PestanaPanelInferior.Problemas;
        MostrarPanelInferior = true;
        MostrarToast(Problemas.Count == 0
            ? "Sin errores de sintaxis en el archivo actual."
            : $"Se detectaron {Problemas.Count} problema(s).");
    }

    public void VerificarTodoElProyecto()
    {
        if (!TieneProyectoAbierto) return;

        Problemas.Clear();
        int archivos = 0;
        foreach (string archivo in BuscadorProyecto.ListarArchivosProyecto(RutaProyectoActual))
        {
            if (!Path.GetExtension(archivo).Equals(".alb", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            archivos++;
            string codigo = File.ReadAllText(archivo);
            foreach (ProblemaCodigo p in _servicioDiagnostico.AnalizarDocumento(archivo, codigo))
            {
                Problemas.Add(p);
            }
        }

        OnPropertyChanged(nameof(TotalProblemas));
        PestanaInferior = PestanaPanelInferior.Problemas;
        MostrarPanelInferior = true;
        RegistrarActividad("Verificación", $"Proyecto verificado ({archivos} archivo(s) .alb): {Problemas.Count} problema(s).");
        MostrarToast($"Verificación completa: {Problemas.Count} problema(s).");
    }

    public async Task EjecutarActivoAsync()
    {
        if (ArchivoActivo is null || EstaEjecutando)
        {
            return;
        }

        GuardarArchivoActivo();
        string ruta = ArchivoActivo.RutaArchivo;
        string codigo = ArchivoActivo.Documento.Text;

        PestanaInferior = PestanaPanelInferior.Consola;
        MostrarPanelInferior = true;
        Consola.Limpiar();
        Consola.AgregarMensaje(TipoMensajeConsola.Sistema, $"Ejecutando {Path.GetFileName(ruta)}...");

        EstaEjecutando = true;
        EstadoBarra = $"Ejecutando {Path.GetFileName(ruta)}...";
        RegistrarActividad("Ejecución", $"Iniciada ejecución de {Path.GetFileName(ruta)}.");
        _ctsEjecucion = new CancellationTokenSource();

        ConsolaIdeIO? consolaIo = null;
        try
        {
            consolaIo = new ConsolaIdeIO(
                texto => Consola.AgregarMensaje(TipoMensajeConsola.Salida, texto),
                texto => Consola.AgregarMensaje(TipoMensajeConsola.Salida, texto),
                indicador => Consola.SolicitarEntrada(indicador, entrada => consolaIo?.EnviarEntrada(entrada)),
                _ctsEjecucion.Token);

            bool exito = await _servicioEjecucion.EjecutarArchivoAsync(
                ruta,
                codigo,
                consolaIo,
                (tipo, msg) => Consola.AgregarMensaje(tipo, msg),
                _ctsEjecucion.Token);

            Consola.AgregarMensaje(
                exito ? TipoMensajeConsola.Sistema : TipoMensajeConsola.Advertencia,
                exito ? "Programa finalizado correctamente." : "La ejecución finalizó con incidencias.");
        }
        finally
        {
            Consola.CancelarSolicitudEntrada();
            consolaIo?.Dispose();
            _ctsEjecucion.Dispose();
            _ctsEjecucion = null;

            Dispatcher.UIThread.Post(() =>
            {
                EstaEjecutando = false;
                EstadoBarra = "Alba Studio listo";
            });
        }
    }

    public void DetenerEjecucion()
    {
        _ctsEjecucion?.Cancel();
        Consola.CancelarSolicitudEntrada();
        RegistrarActividad("Ejecución", "Ejecución detenida por el usuario.");
    }

    public void SeleccionarSeccionLateral(SeccionBarraLateral seccion)
    {
        if (SeccionLateral == seccion && MostrarBarraLateral)
        {
            MostrarBarraLateral = false;
        }
        else
        {
            SeccionLateral = seccion;
            MostrarBarraLateral = true;
        }
    }

    public void SeleccionarPestanaInferior(PestanaPanelInferior pestana)
    {
        PestanaInferior = pestana;
        MostrarPanelInferior = true;
        if (pestana == PestanaPanelInferior.Terminal && !_servicioTerminal.EstaActiva)
        {
            _servicioTerminal.Iniciar(RutaProyectoActual);
        }
    }

    public void EjecutarBusquedaEnProyecto()
    {
        ResultadosBusqueda.Clear();
        if (!TieneProyectoAbierto || string.IsNullOrWhiteSpace(TerminoBusquedaProyecto))
        {
            return;
        }

        IReadOnlyList<ResultadoBusquedaProyecto> encontrados = BuscadorProyecto.BuscarTextoEnProyecto(
            RutaProyectoActual,
            TerminoBusquedaProyecto,
            BusquedaMayusculas);

        foreach (ResultadoBusquedaProyecto item in encontrados)
        {
            ResultadosBusqueda.Add(item);
        }

        RegistrarActividad("Búsqueda", $"'{TerminoBusquedaProyecto}': {ResultadosBusqueda.Count} coincidencia(s).");
    }

    public void EnviarComandoTerminal()
    {
        if (string.IsNullOrWhiteSpace(ComandoTerminal))
        {
            return;
        }

        string cmd = ComandoTerminal;
        ComandoTerminal = string.Empty;
        LineasTerminal.Add($"> {cmd}");
        _servicioTerminal.EnviarComando(cmd, RutaProyectoActual);
    }

    public void RecargarListaAddons()
    {
        _gestorAddons.RecargarAddons();
        AddonsInstalados.Clear();
        foreach (AddonCargado addon in _gestorAddons.Addons)
        {
            AddonsInstalados.Add(addon);
        }
    }

    public void AplicarTemaYConfiguracion()
    {
        _gestorTemas.AplicarTema(Configuracion.TemaId, Configuracion);
        _timerAutoguardado.Interval = TimeSpan.FromSeconds(Math.Clamp(Configuracion.IntervaloGuardadoSegundos, 5, 300));
        _gestorConfiguracion.GuardarConfiguracion();
        OnPropertyChanged(nameof(NombreTemaActual));
        RegistrarActividad("Apariencia", $"Tema aplicado: {NombreTemaActual}");
    }

    public void AlternarTemaRapido()
    {
        var lista = _gestorTemas.TemasDisponibles;
        if (lista.Count == 0) return;

        int actual = -1;
        for (int i = 0; i < lista.Count; i++)
        {
            if (lista[i].Id.Equals(_gestorTemas.TemaActual.Id, StringComparison.OrdinalIgnoreCase))
            {
                actual = i;
                break;
            }
        }

        TemaIDE siguiente = lista[(actual + 1) % lista.Count];
        Configuracion.TemaId = siguiente.Id;
        AplicarTemaYConfiguracion();
        MostrarToast($"Tema cambiado a: {siguiente.Nombre}");
    }

    public async Task ComprobarActualizacionesAsync(bool silencioso = false)
    {
        InformacionActualizacion info = await _gestorActualizaciones.ComprobarAsync();
        _ultimaInfoActualizacion = info;

        Dispatcher.UIThread.Post(() =>
        {
            HayActualizacionDisponible = info.HayActualizacion;
            VersionDisponibleTexto = info.HayActualizacion ? $"Disponible v{info.VersionDisponible}" : string.Empty;
            RegistrarActividad("Actualizaciones", info.MensajeEstado);
            if (!silencioso || info.HayActualizacion)
            {
                MostrarToast(info.MensajeEstado);
            }
        });
    }

    public void IrAProblema(ProblemaCodigo problema)
    {
        SolicitarIrAProblema?.Invoke(problema);
    }

    public string DetectarNombreLenguaje(string rutaArchivo)
    {
        string ext = Path.GetExtension(rutaArchivo).ToLowerInvariant();
        if (ext == ".alb") return "Alba";

        AddonCargado? addon = _gestorAddons.ObtenerAddonPorExtension(ext);
        return addon is not null ? addon.Manifiesto.Lenguaje : "Texto plano";
    }

    public void SincronizarRecientes()
    {
        Recientes.Clear();
        foreach (ElementoReciente r in _gestorConfiguracion.Recientes)
        {
            Recientes.Add(r);
        }
    }

    public void GuardarEstadoSesion()
    {
        _gestorConfiguracion.GuardarSesion(
            RutaProyectoActual,
            PestanasAbiertas.Select(p => p.RutaArchivo),
            ArchivoActivo?.RutaArchivo);
    }

    public void RegistrarActividad(string categoria, string mensaje)
    {
        Dispatcher.UIThread.Post(() =>
        {
            RegistroActividad.Insert(0, new EntradaRegistro
            {
                MarcaTiempo = DateTime.Now,
                Categoria = categoria,
                Mensaje = mensaje
            });
            while (RegistroActividad.Count > 150)
            {
                RegistroActividad.RemoveAt(RegistroActividad.Count - 1);
            }
        });
    }

    public void MostrarToast(string mensaje)
    {
        Dispatcher.UIThread.Post(() =>
        {
            TextoNotificacion = mensaje;
            MostrarNotificacion = true;

            _timerNotificacion?.Stop();
            _timerNotificacion = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.2) };
            _timerNotificacion.Tick += (_, _) =>
            {
                _timerNotificacion.Stop();
                MostrarNotificacion = false;
            };
            _timerNotificacion.Start();
        });
    }

    private void AlCambiarDocumentoActivo(object? sender, EventArgs e)
    {
        _timerDebounceDiagnostico?.Stop();
        _timerDebounceDiagnostico = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
        _timerDebounceDiagnostico.Tick += (_, _) =>
        {
            _timerDebounceDiagnostico.Stop();
            EjecutarDiagnosticoEnVivo();
        };
        _timerDebounceDiagnostico.Start();
    }

    public void EjecutarDiagnosticoEnVivo()
    {
        Problemas.Clear();
        if (ArchivoActivo is not null)
        {
            IReadOnlyList<ProblemaCodigo> lista = _servicioDiagnostico.AnalizarDocumento(
                ArchivoActivo.RutaArchivo,
                ArchivoActivo.Documento.Text);
            foreach (ProblemaCodigo p in lista)
            {
                Problemas.Add(p);
            }
        }
        OnPropertyChanged(nameof(TotalProblemas));
    }

    private void NotificarComandos()
    {
        ComandoGuardar.NotificarCambio();
        ComandoGuardarTodo.NotificarCambio();
        ComandoEjecutar.NotificarCambio();
        ComandoVerificar.NotificarCambio();
        ComandoDetener.NotificarCambio();
    }

    public void Dispose()
    {
        _timerAutoguardado.Stop();
        _timerDebounceDiagnostico?.Stop();
        _timerNotificacion?.Stop();
        _servicioTerminal.Dispose();
    }
}
