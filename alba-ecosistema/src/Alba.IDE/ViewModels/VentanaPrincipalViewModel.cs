using System.Collections.ObjectModel;
using Alba.IDE.Models;
using Alba.IDE.Services;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Alba.IDE.ViewModels;

public enum SeccionBarraLateral
{
    Explorador,
    Busqueda,
    GitHub,
    Tools,
    Herramientas
}

public enum PestanaPanelInferior
{
    Consola,
    Problemas,
    Terminal,
    Registro
}

public sealed record CategoriaToolOpcion(CategoriaHerramienta Categoria, string Nombre)
{
    public override string ToString() => Nombre;
}

public sealed record ModalidadTerminalOpcion(ModalidadTerminal Modalidad, string Nombre)
{
    public override string ToString() => Nombre;
}

public sealed record DependenciaProyectoItem(string Nombre, string ArchivoManifiesto, string ComandoInstalacion);

/// <summary>
/// ViewModel principal de Xynex Code Editor 5.0 (lenguaje Cibao y ecosistema multi-lenguaje).
/// Coordina bienvenida, creación tripartita, proyectos y archivos independientes, diagnóstico en vivo,
/// consola, multi-terminal, Centro de Tools integrado, temas y actualizaciones.
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
    private readonly DetectorEntornos _detectorEntornos;
    private readonly ServicioCreacion _servicioCreacion;
    private readonly GestorTools _gestorTools;
    private readonly GestorTerminales _gestorTerminales;
    private readonly AlmacenCredencialesSeguro _almacenCredenciales;
    private readonly ServicioGitHub _servicioGitHub;
    private readonly ServicioGit _servicioGit;
    private readonly ProveedorLenguajeIde _proveedorLenguajeIde;
    private readonly ServidorWebLocal _servidorWebLocal;
    private readonly DispatcherTimer _timerAutoguardado;
    private DispatcherTimer? _timerDebounceDiagnostico;
    private DispatcherTimer? _timerNotificacion;

    private string? _rutaProyectoActual;
    private ArchivoAbierto? _archivoActivo;
    private string _estadoBarra = $"Xynex Code Editor v{RutasUsuarioAlba.VersionActual} listo";
    private string _posicionCursor = "Ln 1, Col 1";
    private bool _estaEjecutando;
    private CancellationTokenSource? _ctsEjecucion;

    private bool _mostrarPantallaInicio = true;
    private bool _mostrarBarraLateral = true;
    private bool _mostrarPanelInferior = true;
    private bool _mostrarCentroTools;
    private bool _mostrarVistaPreviaWeb;
    private string _rutaHtmlVistaPrevia = string.Empty;
    private string _urlVistaPreviaWeb = string.Empty;
    private SeccionBarraLateral _seccionLateral = SeccionBarraLateral.Explorador;
    private PestanaPanelInferior _pestanaInferior = PestanaPanelInferior.Consola;

    private string _terminoBusquedaProyecto = string.Empty;
    private bool _busquedaMayusculas;
    private string _comandoTerminal = string.Empty;
    private ModalidadTerminalOpcion _modalidadTerminalSeleccionada;
    private SesionTerminalTab? _sesionTerminalActiva;

    private string _filtroTextoTools = string.Empty;
    private CategoriaToolOpcion _categoriaToolSeleccionada;
    private bool _soloToolsInstalados;
    private ElementoTool? _toolSeleccionado;

    private CuentaGitHub? _cuentaGitHubActual;
    private Bitmap? _avatarGitHub;
    private EstadoRepositorioGit _estadoGit = new();
    private string _ramaGitSeleccionada = string.Empty;
    private bool _actualizandoRamaGit;
    private string _mensajeCommitGit = string.Empty;
    private string _filtroReposGitHub = string.Empty;
    private string _urlClonacionManual = string.Empty;
    private string _estadoOperacionGitTexto = string.Empty;
    private bool _estaOperandoGit;
    private readonly List<RepositorioGitHub> _todosRepositoriosGitHub = [];

    private string _textoNotificacion = string.Empty;
    private bool _mostrarNotificacion;
    private bool _hayActualizacionDisponible;
    private string _versionDisponibleTexto = string.Empty;
    private InformacionActualizacion? _ultimaInfoActualizacion;
    private string? _rutaPaqueteActualizacionListo;

    public ObservableCollection<NodoProyecto> NodosExplorador { get; } = [];
    public ObservableCollection<ArchivoAbierto> PestanasAbiertas { get; } = [];
    public ObservableCollection<ArchivoAbierto> ArchivosIndependientes { get; } = [];
    public ObservableCollection<ElementoReciente> Recientes { get; } = [];
    public ObservableCollection<ElementoReciente> ProyectosRecientes { get; } = [];
    public ObservableCollection<ElementoReciente> ArchivosRecientes { get; } = [];
    public ObservableCollection<EstadoEntornoLenguaje> EntornosDetectados { get; } = [];
    public ObservableCollection<EstadoEntornoGrafico> EntornosGraficosDetectados { get; } = [];
    public ObservableCollection<ProblemaCodigo> Problemas { get; } = [];
    public ObservableCollection<ResultadoBusquedaProyecto> ResultadosBusqueda { get; } = [];
    public ObservableCollection<EntradaRegistro> RegistroActividad { get; } = [];
    public ObservableCollection<ElementoTool> ToolsFiltrados { get; } = [];
    public ObservableCollection<DependenciaProyectoItem> DependenciasProyectoDetectadas { get; } = [];
    public ObservableCollection<RepositorioGitHub> RepositoriosGitHub { get; } = [];
    public ObservableCollection<ArchivoCambioGit> CambiosGit { get; } = [];
    public ObservableCollection<CommitHistorialGit> HistorialCommitsGit { get; } = [];
    public ObservableCollection<string> RamasGit { get; } = [];
    public ObservableCollection<string> ComandosRecomendadosTerminal { get; } = [];
    public ObservableCollection<AddonCargado> AddonsInstalados { get; } = [];
    public ConsolaViewModel Consola { get; } = new();

    public IReadOnlyList<CategoriaToolOpcion> CategoriasTools { get; } =
    [
        new(CategoriaHerramienta.Todas, "Todas"),
        new(CategoriaHerramienta.Lenguajes, "Lenguajes"),
        new(CategoriaHerramienta.Compiladores, "Compiladores"),
        new(CategoriaHerramienta.Interpretes, "Intérpretes"),
        new(CategoriaHerramienta.Sdk, "SDKs"),
        new(CategoriaHerramienta.Frameworks, "Frameworks"),
        new(CategoriaHerramienta.Bibliotecas, "Bibliotecas"),
        new(CategoriaHerramienta.Dependencias, "Dependencias"),
        new(CategoriaHerramienta.Extensiones, "Extensiones"),
        new(CategoriaHerramienta.Depuracion, "Depuración"),
        new(CategoriaHerramienta.DesarrolloWeb, "Desarrollo Web"),
        new(CategoriaHerramienta.AplicacionesGraficas, "Apps Gráficas"),
        new(CategoriaHerramienta.IntegracionesIA, "Integraciones IA"),
        new(CategoriaHerramienta.Productividad, "Productividad")
    ];

    public IReadOnlyList<ModalidadTerminalOpcion> ModalidadesTerminalDisponibles { get; }

    public ObservableCollection<SesionTerminalTab> SesionesTerminal => _gestorTerminales.Sesiones;

    public GestorAddons GestorAddons => _gestorAddons;
    public GestorConfiguracion GestorConfiguracion => _gestorConfiguracion;
    public GestorTemas GestorTemas => _gestorTemas;
    public GestorActualizaciones GestorActualizaciones => _gestorActualizaciones;
    public DetectorEntornos DetectorEntornos => _detectorEntornos;
    public ServicioCreacion ServicioCreacion => _servicioCreacion;
    public GestorTools GestorTools => _gestorTools;
    public ServicioGitHub ServicioGitHub => _servicioGitHub;
    public ServicioGit ServicioGit => _servicioGit;
    public ProveedorLenguajeIde ProveedorLenguajeIde => _proveedorLenguajeIde;
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
    public bool TieneArchivosIndependientes => ArchivosIndependientes.Count > 0;

    public string NombreProyectoActual => string.IsNullOrWhiteSpace(RutaProyectoActual)
        ? (ArchivoActivo is not null ? "Archivos independientes" : "Sin proyecto")
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
                    MostrarCentroTools = false;
                }

                OnPropertyChanged(nameof(TituloVentana));
                OnPropertyChanged(nameof(RutaRelativaActiva));
                OnPropertyChanged(nameof(NombreProyectoActual));
                NotificarComandos();
                ActualizarComandosRecomendadosTerminal();
                EjecutarDiagnosticoEnVivo();
                if (MostrarVistaPreviaWeb &&
                    _archivoActivo is not null &&
                    ServicioEjecucion.EsArchivoWeb(_archivoActivo.RutaArchivo))
                {
                    AbrirVistaPreviaWeb(_archivoActivo.RutaArchivo);
                }
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
            return $"Independiente > {ArchivoActivo.RutaArchivo}";
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

    public bool MostrarCentroTools
    {
        get => _mostrarCentroTools;
        set
        {
            if (SetProperty(ref _mostrarCentroTools, value))
            {
                OnPropertyChanged(nameof(MostrarAreaEditor));
            }
        }
    }

    public bool MostrarAreaEditor => !MostrarCentroTools;

    public bool MostrarVistaPreviaWeb
    {
        get => _mostrarVistaPreviaWeb;
        set => SetProperty(ref _mostrarVistaPreviaWeb, value);
    }

    public string UrlVistaPreviaWeb
    {
        get => _urlVistaPreviaWeb;
        private set => SetProperty(ref _urlVistaPreviaWeb, value);
    }

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
                OnPropertyChanged(nameof(EsGitHubActivo));
                OnPropertyChanged(nameof(EsToolsActivo));
                OnPropertyChanged(nameof(EsHerramientasActivo));
            }
        }
    }

    public bool EsExploradorActivo => SeccionLateral == SeccionBarraLateral.Explorador;
    public bool EsBusquedaActiva => SeccionLateral == SeccionBarraLateral.Busqueda;
    public bool EsGitHubActivo => SeccionLateral == SeccionBarraLateral.GitHub;
    public bool EsToolsActivo => SeccionLateral == SeccionBarraLateral.Tools;
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

    public SesionTerminalTab? SesionTerminalActiva
    {
        get => _sesionTerminalActiva;
        set
        {
            if (SetProperty(ref _sesionTerminalActiva, value))
            {
                _gestorTerminales.SeleccionarSesion(value);
                OnPropertyChanged(nameof(LineasTerminalActiva));
                OnPropertyChanged(nameof(DirectorioTerminalActiva));
            }
        }
    }

    public ObservableCollection<LineaTerminalItem>? LineasTerminalActiva => SesionTerminalActiva?.Lineas;

    public string DirectorioTerminalActiva => SesionTerminalActiva?.DirectorioTrabajo ?? Directory.GetCurrentDirectory();

    public ModalidadTerminalOpcion ModalidadTerminalSeleccionada
    {
        get => _modalidadTerminalSeleccionada;
        set => SetProperty(ref _modalidadTerminalSeleccionada, value);
    }

    public string FiltroTextoTools
    {
        get => _filtroTextoTools;
        set
        {
            if (SetProperty(ref _filtroTextoTools, value))
            {
                AplicarFiltroTools();
            }
        }
    }

    public CategoriaToolOpcion CategoriaToolSeleccionada
    {
        get => _categoriaToolSeleccionada;
        set
        {
            if (SetProperty(ref _categoriaToolSeleccionada, value))
            {
                AplicarFiltroTools();
            }
        }
    }

    public bool SoloToolsInstalados
    {
        get => _soloToolsInstalados;
        set
        {
            if (SetProperty(ref _soloToolsInstalados, value))
            {
                AplicarFiltroTools();
            }
        }
    }

    public ElementoTool? ToolSeleccionado
    {
        get => _toolSeleccionado;
        set
        {
            if (SetProperty(ref _toolSeleccionado, value))
            {
                OnPropertyChanged(nameof(ToolSeleccionadoEsAddon));
                OnPropertyChanged(nameof(ToolSeleccionadoTextoBotonActivar));
                OnPropertyChanged(nameof(ToolSeleccionadoTieneQueFalta));
                OnPropertyChanged(nameof(ToolSeleccionadoDesinstalable));
                OnPropertyChanged(nameof(ToolSeleccionadoTienePaqueteIntegrado));
                OnPropertyChanged(nameof(ToolSeleccionadoTextoInstalar));
                OnPropertyChanged(nameof(ToolSeleccionadoTieneCmdGuiado));
            }
        }
    }

    public bool ToolSeleccionadoEsAddon => ToolSeleccionado?.EsAddonLocal == true;
    public bool ToolSeleccionadoTieneQueFalta => !string.IsNullOrWhiteSpace(ToolSeleccionado?.QueFaltaTexto);
    public bool ToolSeleccionadoDesinstalable => ToolSeleccionado is { EsAddonLocal: true };
    public bool ToolSeleccionadoTienePaqueteIntegrado => !string.IsNullOrWhiteSpace(ToolSeleccionado?.PaqueteAddonIncorporadoId);
    public bool ToolSeleccionadoTieneCmdGuiado => string.IsNullOrWhiteSpace(ToolSeleccionado?.PaqueteAddonIncorporadoId) && !string.IsNullOrWhiteSpace(ToolSeleccionado?.ComandoInstalacionGuiada);
    public string ToolSeleccionadoTextoInstalar => ToolSeleccionado?.Instalado == true ? "Reinstalar paquete integrado" : "Instalar paquete integrado";
    public string ToolSeleccionadoTextoBotonActivar => ToolSeleccionado is { Activo: true } ? "Desactivar complemento" : "Activar complemento";
    public bool TieneDependenciasProyecto => DependenciasProyectoDetectadas.Count > 0;

    public CuentaGitHub? CuentaGitHubActual
    {
        get => _cuentaGitHubActual;
        private set
        {
            if (SetProperty(ref _cuentaGitHubActual, value))
            {
                OnPropertyChanged(nameof(EstaConectadoGitHub));
                OnPropertyChanged(nameof(NoEstaConectadoGitHub));
                OnPropertyChanged(nameof(ResumenCuentaGitHub));
                OnPropertyChanged(nameof(PermisosCuentaGitHub));
            }
        }
    }

    public bool EstaConectadoGitHub => CuentaGitHubActual is not null;
    public bool NoEstaConectadoGitHub => CuentaGitHubActual is null;
    public string ResumenCuentaGitHub => CuentaGitHubActual is null
        ? "Sin cuenta conectada"
        : CuentaGitHubActual.NombreMostrado;
    public string PermisosCuentaGitHub => CuentaGitHubActual is null
        ? string.Empty
        : $"{CuentaGitHubActual.RepositoriosPublicos} públicos · {CuentaGitHubActual.RepositoriosPrivados} privados · {CuentaGitHubActual.PermisosToken}";

    public Bitmap? AvatarGitHub
    {
        get => _avatarGitHub;
        private set => SetProperty(ref _avatarGitHub, value);
    }

    public EstadoRepositorioGit EstadoGit
    {
        get => _estadoGit;
        private set
        {
            if (SetProperty(ref _estadoGit, value))
            {
                OnPropertyChanged(nameof(GitNoInstalado));
                OnPropertyChanged(nameof(NoEsRepositorioGit));
                OnPropertyChanged(nameof(EsRepositorioGit));
                OnPropertyChanged(nameof(TextoRemotoOrigin));
            }
        }
    }

    public bool GitNoInstalado => !EstadoGit.GitInstalado;
    public bool NoEsRepositorioGit => EstadoGit.GitInstalado && !EstadoGit.EsRepositorio;
    public bool EsRepositorioGit => EstadoGit.GitInstalado && EstadoGit.EsRepositorio;
    public string TextoRemotoOrigin => string.IsNullOrWhiteSpace(EstadoGit.RemotoOrigin)
        ? "Sin remoto origin configurado"
        : EstadoGit.RemotoOrigin;

    public string RamaGitSeleccionada
    {
        get => _ramaGitSeleccionada;
        set
        {
            if (SetProperty(ref _ramaGitSeleccionada, value) && !_actualizandoRamaGit && !string.IsNullOrWhiteSpace(value))
            {
                _ = CambiarRamaGitAsync(value);
            }
        }
    }

    public string MensajeCommitGit
    {
        get => _mensajeCommitGit;
        set => SetProperty(ref _mensajeCommitGit, value);
    }

    public string FiltroReposGitHub
    {
        get => _filtroReposGitHub;
        set
        {
            if (SetProperty(ref _filtroReposGitHub, value))
            {
                AplicarFiltroReposGitHub();
            }
        }
    }

    public string UrlClonacionManual
    {
        get => _urlClonacionManual;
        set => SetProperty(ref _urlClonacionManual, value);
    }

    public string EstadoOperacionGitTexto
    {
        get => _estadoOperacionGitTexto;
        set => SetProperty(ref _estadoOperacionGitTexto, value);
    }

    public bool EstaOperandoGit
    {
        get => _estaOperandoGit;
        private set => SetProperty(ref _estaOperandoGit, value);
    }

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
    public string? RutaPaqueteActualizacionListo => _rutaPaqueteActualizacionListo;
    public event Action<InformacionActualizacion>? ActualizacionListaParaInstalar;

    public int TotalProblemas => Problemas.Count;
    public bool SinProyectosRecientes => ProyectosRecientes.Count == 0;
    public bool SinArchivosRecientes => ArchivosRecientes.Count == 0;

    public string TituloVentana => ArchivoActivo is not null
        ? $"{ArchivoActivo.TituloPestana} — {NombreProyectoActual} — Xynex Code Editor v{VersionApp}"
        : (TieneProyectoAbierto ? $"{NombreProyectoActual} — Xynex Code Editor v{VersionApp}" : $"Xynex Code Editor v{VersionApp}");

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
    public RelayCommand ComandoDetenerTerminal { get; }
    public RelayCommand ComandoNuevaTerminal { get; }
    public RelayCommand ComandoAlternarToolAddon { get; }

    public event Action<ProblemaCodigo>? SolicitarIrAProblema;
    public event Action<string, string, string?>? SolicitarActualizarVistaWeb;
    public event Action<MensajeConsolaWeb>? LogWebRecibido;
    public Func<bool>? ConsultarAutoRecargaWeb { get; set; }

    public VentanaPrincipalViewModel()
    {
        _gestorProyectos = new GestorProyectos();
        _gestorAddons = new GestorAddons();
        _gestorConfiguracion = new GestorConfiguracion();
        _gestorTemas = new GestorTemas();
        _gestorActualizaciones = new GestorActualizaciones();
        _detectorEntornos = new DetectorEntornos();
        _servicioCreacion = new ServicioCreacion();
        _gestorTools = new GestorTools(_gestorAddons, _detectorEntornos);
        _gestorTerminales = new GestorTerminales();
        _almacenCredenciales = new AlmacenCredencialesSeguro();
        _servicioGitHub = new ServicioGitHub(_almacenCredenciales);
        _servicioGit = new ServicioGit();
        _proveedorLenguajeIde = new ProveedorLenguajeIde(_gestorAddons, _detectorEntornos);
        _servidorWebLocal = new ServidorWebLocal();
        _servicioEjecucion = new ServicioEjecucion(_gestorAddons);
        _servicioDiagnostico = new ServicioDiagnosticoEnVivo();
        ServicioAutocompletado = new ServicioAutocompletado(_gestorAddons);

        _servicioEjecucion.SolicitarVistaWeb += rutaHtml =>
        {
            Dispatcher.UIThread.Post(() => AbrirVistaPreviaWeb(rutaHtml));
        };

        _servidorWebLocal.LogRecibido += msg =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                LogWebRecibido?.Invoke(msg);
                RegistrarActividad($"Web/{msg.Nivel}", msg.Mensaje);
            });
        };

        _servicioGitHub.SesionCambiada += cuenta =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                CuentaGitHubActual = cuenta;
                _ = ActualizarAvatarGitHubAsync(cuenta);
                if (cuenta is not null)
                {
                    _ = CargarRepositoriosGitHubAsync();
                }
                else
                {
                    _todosRepositoriosGitHub.Clear();
                    RepositoriosGitHub.Clear();
                }
            });
        };

        ModalidadesTerminalDisponibles = OperatingSystem.IsWindows()
            ? [
                new(ModalidadTerminal.IntegradaIde, "IDE Integrada"),
                new(ModalidadTerminal.PowerShellWindows, "PowerShell"),
                new(ModalidadTerminal.CmdWindows, "CMD")
              ]
            : [
                new(ModalidadTerminal.IntegradaIde, "IDE Integrada"),
                new(ModalidadTerminal.BashSistema, "Bash"),
                new(ModalidadTerminal.ZshSistema, "Zsh")
              ];

        _modalidadTerminalSeleccionada = ModalidadesTerminalDisponibles[0];
        _categoriaToolSeleccionada = CategoriasTools[0];

        _gestorTerminales.SesionActivaCambiada += tab =>
        {
            _sesionTerminalActiva = tab;
            OnPropertyChanged(nameof(SesionTerminalActiva));
            OnPropertyChanged(nameof(LineasTerminalActiva));
            OnPropertyChanged(nameof(DirectorioTerminalActiva));
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
        ComandoLimpiarTerminal = new RelayCommand(_ => LimpiarTerminalActiva());
        ComandoDetenerTerminal = new RelayCommand(_ => DetenerTerminalActiva());
        ComandoNuevaTerminal = new RelayCommand(_ => CrearNuevaTerminal(ModalidadTerminalSeleccionada.Modalidad));
        ComandoAlternarToolAddon = new RelayCommand(_ => AlternarEstadoToolSeleccionado());

        _mostrarBarraLateral = Configuracion.MostrarPanelLateral;
        _mostrarPanelInferior = Configuracion.MostrarPanelInferior;

        _gestorTemas.AplicarTema(Configuracion.TemaId, Configuracion);
        RecargarListaAddons();
        SincronizarRecientes();
        SincronizarEntornosSinBloquear();
        AplicarFiltroTools();
        ActualizarComandosRecomendadosTerminal();

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

        RegistrarActividad("Inicio", $"Xynex Code Editor v{VersionApp} iniciado (Motor Cibao activo).");
        _ = RefrescarEntornosYToolsAsync();
        _ = RestaurarSesionGitHubAsync();
        _ = RefrescarGitAsync();
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
                AbrirArchivo(rutaInicial);
                return;
            }
        }

        if (Configuracion.RestaurarUltimaSesion)
        {
            bool restauroAlgo = false;

            if (!string.IsNullOrWhiteSpace(_gestorConfiguracion.Sesion.UltimoProyecto) &&
                Directory.Exists(_gestorConfiguracion.Sesion.UltimoProyecto))
            {
                AbrirCarpetaProyecto(_gestorConfiguracion.Sesion.UltimoProyecto, restaurarPestanas: true);
                restauroAlgo = true;
            }
            else if (_gestorConfiguracion.Sesion.ArchivosIndependientes.Count > 0)
            {
                foreach (string ruta in _gestorConfiguracion.Sesion.ArchivosIndependientes)
                {
                    if (File.Exists(ruta))
                    {
                        AbrirArchivo(ruta, registrarReciente: false);
                        restauroAlgo = true;
                    }
                }
            }

            if (restauroAlgo)
            {
                return;
            }
        }

        MostrarPantallaInicio = true;
    }

    public (bool Exito, string Mensaje) EjecutarCreacion(SolicitudCreacion solicitud)
    {
        var resultado = _servicioCreacion.Crear(solicitud);
        if (!resultado.Exito)
        {
            MostrarToast(resultado.Mensaje);
            return (false, resultado.Mensaje);
        }

        if (resultado.EsProyecto && !string.IsNullOrWhiteSpace(resultado.RutaProyecto))
        {
            AbrirCarpetaProyecto(resultado.RutaProyecto);
            if (!string.IsNullOrWhiteSpace(resultado.RutaArchivoPrincipal))
            {
                AbrirArchivo(resultado.RutaArchivoPrincipal);
            }
            RegistrarActividad("Creación", $"Proyecto '{Path.GetFileName(resultado.RutaProyecto)}' creado.");
        }
        else if (!string.IsNullOrWhiteSpace(resultado.RutaArchivoPrincipal))
        {
            AbrirArchivo(resultado.RutaArchivoPrincipal);
            RegistrarActividad("Creación", resultado.Mensaje);
        }

        MostrarCentroTools = false;
        MostrarToast(resultado.Mensaje);
        return (true, resultado.Mensaje);
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
        MostrarCentroTools = false;
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
            string? principal = new[] { "principal.cibao", "principal.alb", "main.py", "index.html", "Program.cs", "main.cpp" }
                .Select(n => Path.Combine(RutaProyectoActual, n))
                .FirstOrDefault(File.Exists);
            if (!string.IsNullOrWhiteSpace(principal))
            {
                AbrirArchivo(principal);
            }
        }

        _gestorTerminales.CambiarDirectorioSesiones(RutaProyectoActual);

        RefrescarDependenciasProyecto();
        _ = RefrescarGitAsync();

        GuardarEstadoSesion();
        EstadoBarra = $"Proyecto abierto: {NombreProyectoActual}";
        RegistrarActividad("Proyecto", $"Carpeta abierta: {RutaProyectoActual}");
    }

    public void RefrescarExplorador()
    {
        if (string.IsNullOrWhiteSpace(RutaProyectoActual))
        {
            NodosExplorador.Clear();
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
        MostrarCentroTools = false;
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

        if (!EstaDentroDeProyectoActual(rutaFull))
        {
            ArchivosIndependientes.Add(nuevo);
            OnPropertyChanged(nameof(TieneArchivosIndependientes));
        }

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
        if (MostrarVistaPreviaWeb)
        {
            RefrescarVistaPreviaWeb(sincronizarDisco: false);
        }
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
            if (MostrarVistaPreviaWeb)
            {
                RefrescarVistaPreviaWeb(sincronizarDisco: false);
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
        if (ArchivosIndependientes.Remove(pestana))
        {
            OnPropertyChanged(nameof(TieneArchivosIndependientes));
        }

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
            ArchivosIndependientes.Remove(tab);
        }
        OnPropertyChanged(nameof(TieneArchivosIndependientes));
        ArchivoActivo = conservar;
        GuardarEstadoSesion();
        NotificarComandos();
    }

    public void CerrarTodasPestanas()
    {
        PestanasAbiertas.Clear();
        ArchivosIndependientes.Clear();
        OnPropertyChanged(nameof(TieneArchivosIndependientes));
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
            if (!ServicioDiagnosticoEnVivo.EsArchivoCibao(archivo))
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
        RegistrarActividad("Verificación", $"Proyecto verificado ({archivos} archivo(s) Cibao): {Problemas.Count} problema(s).");
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
                EstadoBarra = "Xynex Code Editor listo";
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
        if (seccion == SeccionBarraLateral.Tools)
        {
            AbrirCentroTools();
            return;
        }

        MostrarCentroTools = false;
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

    public void AbrirCentroTools()
    {
        MostrarPantallaInicio = false;
        SeccionLateral = SeccionBarraLateral.Tools;
        MostrarCentroTools = true;
        AplicarFiltroTools();
    }

    public void CerrarCentroTools()
    {
        MostrarCentroTools = false;
        if (SeccionLateral == SeccionBarraLateral.Tools)
        {
            SeccionLateral = SeccionBarraLateral.Explorador;
        }
    }

    public void SeleccionarPestanaInferior(PestanaPanelInferior pestana)
    {
        PestanaInferior = pestana;
        MostrarPanelInferior = true;
        if (pestana == PestanaPanelInferior.Terminal && SesionesTerminal.Count == 0)
        {
            CrearNuevaTerminal(ModalidadTerminalSeleccionada.Modalidad);
        }
    }

    public void CrearNuevaTerminal(ModalidadTerminal? modalidad = null)
    {
        ModalidadTerminal mod = modalidad ?? ModalidadTerminalSeleccionada.Modalidad;
        string? cwd = RutaProyectoActual ?? Path.GetDirectoryName(ArchivoActivo?.RutaArchivo);
        SesionTerminalTab nueva = _gestorTerminales.CrearSesion(mod, cwd);
        SesionTerminalActiva = nueva;
        PestanaInferior = PestanaPanelInferior.Terminal;
        MostrarPanelInferior = true;
    }

    public void CerrarTerminal(SesionTerminalTab? tab)
    {
        _gestorTerminales.CerrarSesion(tab);
        OnPropertyChanged(nameof(LineasTerminalActiva));
        OnPropertyChanged(nameof(DirectorioTerminalActiva));
    }

    public void DetenerTerminalActiva()
    {
        _gestorTerminales.DetenerSesionActiva();
    }

    public void LimpiarTerminalActiva()
    {
        _gestorTerminales.LimpiarSesionActiva();
    }

    public string? NavegarHistorialTerminal(bool haciaAtras) =>
        _gestorTerminales.NavegarHistorial(haciaAtras);

    public void EjecutarComandoRecomendadoTerminal(string comando)
    {
        if (string.IsNullOrWhiteSpace(comando)) return;
        ComandoTerminal = comando;
        EnviarComandoTerminal();
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
        string? cwd = RutaProyectoActual ?? Path.GetDirectoryName(ArchivoActivo?.RutaArchivo);
        _gestorTerminales.EnviarComando(cmd, cwd);
        OnPropertyChanged(nameof(LineasTerminalActiva));
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

    public async Task RefrescarEntornosYToolsAsync(bool forzar = false)
    {
        IReadOnlyList<EstadoEntornoLenguaje> entornos = await _detectorEntornos.DetectarTodosAsync(forzar);
        IReadOnlyList<EstadoEntornoGrafico> graficos = await _detectorEntornos.DetectarEntornosGraficosAsync(forzar);
        await _gestorTools.RefrescarCatalogoAsync(forzar);

        Dispatcher.UIThread.Post(() =>
        {
            EntornosDetectados.Clear();
            foreach (EstadoEntornoLenguaje env in entornos)
            {
                EntornosDetectados.Add(env);
            }

            EntornosGraficosDetectados.Clear();
            foreach (EstadoEntornoGrafico g in graficos)
            {
                EntornosGraficosDetectados.Add(g);
            }

            RecargarListaAddons();
            AplicarFiltroTools();
        });
    }

    public void AplicarFiltroTools()
    {
        string? idSeleccionado = ToolSeleccionado?.Id;
        IReadOnlyList<ElementoTool> filtrados = _gestorTools.Filtrar(
            FiltroTextoTools,
            CategoriaToolSeleccionada.Categoria,
            SoloToolsInstalados);

        ToolsFiltrados.Clear();
        foreach (ElementoTool tool in filtrados)
        {
            ToolsFiltrados.Add(tool);
        }

        ToolSeleccionado = (!string.IsNullOrWhiteSpace(idSeleccionado)
            ? ToolsFiltrados.FirstOrDefault(t => t.Id.Equals(idSeleccionado, StringComparison.OrdinalIgnoreCase))
            : null) ?? ToolsFiltrados.FirstOrDefault();
    }

    public async Task<(bool Exito, string Mensaje)> InstalarAddonEnToolsAsync(string carpeta)
    {
        var res = _gestorTools.InstalarAddonDesdeCarpeta(carpeta);
        await RefrescarEntornosYToolsAsync(forzar: false);
        MostrarToast(res.Mensaje);
        return res;
    }

    public void AlternarEstadoToolSeleccionado()
    {
        if (ToolSeleccionado is null || !ToolSeleccionado.EsAddonLocal) return;

        bool nuevoEstado = !ToolSeleccionado.Activo;
        _gestorTools.CambiarEstadoAddon(ToolSeleccionado.Id, nuevoEstado);
        _ = RefrescarEntornosYToolsAsync(forzar: false);
        MostrarToast(nuevoEstado
            ? $"Complemento '{ToolSeleccionado.Nombre}' activado."
            : $"Complemento '{ToolSeleccionado.Nombre}' desactivado.");
    }

    public async Task<(bool Exito, string Mensaje)> DesinstalarToolSeleccionadoAsync()
    {
        if (ToolSeleccionado is null || !ToolSeleccionado.EsAddonLocal)
        {
            return (false, "Solo se pueden desinstalar complementos agregados por el usuario.");
        }

        var res = _gestorTools.DesinstalarAddon(ToolSeleccionado.Id);
        await RefrescarEntornosYToolsAsync(forzar: false);
        MostrarToast(res.Mensaje);
        return res;
    }

    public async Task<(bool Exito, string Mensaje)> InstalarPaqueteToolSeleccionadoAsync()
    {
        if (ToolSeleccionado is null)
        {
            return (false, "Selecciona una herramienta primero.");
        }

        var res = await _gestorTools.InstalarPaqueteGuiadoAsync(ToolSeleccionado);
        await RefrescarEntornosYToolsAsync(forzar: false);
        RegistrarActividad("Tools", res.Mensaje);
        MostrarToast(res.Mensaje);
        return res;
    }

    public void RefrescarDependenciasProyecto()
    {
        DependenciasProyectoDetectadas.Clear();
        foreach (var dep in GestorTools.DetectarDependenciasProyecto(RutaProyectoActual))
        {
            DependenciasProyectoDetectadas.Add(new DependenciaProyectoItem(dep.Nombre, dep.ArchivoManifiesto, dep.ComandoInstalacion));
        }
        OnPropertyChanged(nameof(TieneDependenciasProyecto));
    }

    public async Task<(bool Exito, string Salida)> InstalarDependenciaProyectoAsync(DependenciaProyectoItem item)
    {
        if (string.IsNullOrWhiteSpace(RutaProyectoActual))
        {
            return (false, "Abre un proyecto antes de instalar dependencias.");
        }

        MostrarToast($"Instalando dependencias ({item.ArchivoManifiesto})...");
        RegistrarActividad("Dependencias", $"Ejecutando: {item.ComandoInstalacion}");
        var res = await GestorTools.EjecutarComandoDependenciasAsync(RutaProyectoActual, item.ComandoInstalacion);
        RegistrarActividad("Dependencias", res.Exito ? $"Instalación completada ({item.Nombre})." : res.Salida);
        MostrarToast(res.Exito ? $"Dependencias {item.Nombre} instaladas." : "Error instalando dependencias. Revisa el Registro.");
        return res;
    }

    public async Task RestaurarSesionGitHubAsync()
    {
        if (await _servicioGitHub.RestaurarSesionGuardadaAsync())
        {
            RegistrarActividad("GitHub", $"Sesión restaurada como @{_servicioGitHub.CuentaActual?.Login}.");
        }
    }

    private async Task ActualizarAvatarGitHubAsync(CuentaGitHub? cuenta)
    {
        if (cuenta is null || string.IsNullOrWhiteSpace(cuenta.AvatarUrl))
        {
            AvatarGitHub = null;
            return;
        }

        byte[]? bytes = await _servicioGitHub.DescargarAvatarAsync(cuenta.AvatarUrl);
        if (bytes is null || bytes.Length == 0)
        {
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                using var ms = new MemoryStream(bytes);
                AvatarGitHub = new Bitmap(ms);
            }
            catch
            {
                AvatarGitHub = null;
            }
        });
    }

    public async Task CargarRepositoriosGitHubAsync()
    {
        if (!_servicioGitHub.EstaConectado) return;
        var (exito, mensaje, repos) = await _servicioGitHub.ListarRepositoriosAsync();
        Dispatcher.UIThread.Post(() =>
        {
            _todosRepositoriosGitHub.Clear();
            _todosRepositoriosGitHub.AddRange(repos);
            AplicarFiltroReposGitHub();
            EstadoOperacionGitTexto = mensaje;
        });
    }

    private void AplicarFiltroReposGitHub()
    {
        RepositoriosGitHub.Clear();
        IEnumerable<RepositorioGitHub> query = _todosRepositoriosGitHub;
        if (!string.IsNullOrWhiteSpace(FiltroReposGitHub))
        {
            string f = FiltroReposGitHub.Trim();
            query = query.Where(r =>
                r.Nombre.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                r.Descripcion.Contains(f, StringComparison.OrdinalIgnoreCase));
        }
        foreach (RepositorioGitHub r in query)
        {
            RepositoriosGitHub.Add(r);
        }
    }

    public void CerrarSesionGitHub()
    {
        _servicioGitHub.CerrarSesion();
        AvatarGitHub = null;
        RegistrarActividad("GitHub", "Sesión de GitHub cerrada y credenciales locales eliminadas.");
        MostrarToast("Sesión de GitHub cerrada.");
    }

    public async Task RefrescarGitAsync()
    {
        EstadoRepositorioGit estado = await _servicioGit.ObtenerEstadoAsync(RutaProyectoActual);
        Dispatcher.UIThread.Post(() =>
        {
            EstadoGit = estado;
            _actualizandoRamaGit = true;
            try
            {
                RamasGit.Clear();
                foreach (string r in estado.Ramas)
                {
                    RamasGit.Add(r);
                }
                RamaGitSeleccionada = estado.RamaActual;
            }
            finally
            {
                _actualizandoRamaGit = false;
            }

            CambiosGit.Clear();
            foreach (ArchivoCambioGit c in estado.Cambios)
            {
                CambiosGit.Add(c);
            }

            HistorialCommitsGit.Clear();
            foreach (CommitHistorialGit h in estado.Historial)
            {
                HistorialCommitsGit.Add(h);
            }
        });
    }

    public async Task InicializarGitEnProyectoAsync()
    {
        if (string.IsNullOrWhiteSpace(RutaProyectoActual))
        {
            MostrarToast("Abre una carpeta de proyecto primero.");
            return;
        }

        EstaOperandoGit = true;
        try
        {
            var res = await _servicioGit.InicializarRepositorioAsync(RutaProyectoActual);
            EstadoOperacionGitTexto = res.Mensaje;
            RegistrarActividad("Git", res.Mensaje);
            MostrarToast(res.Mensaje);
            await RefrescarGitAsync();
        }
        finally
        {
            EstaOperandoGit = false;
        }
    }

    public async Task PrepararCambioGitAsync(ArchivoCambioGit cambio)
    {
        if (string.IsNullOrWhiteSpace(RutaProyectoActual)) return;
        var res = await _servicioGit.PrepararArchivoAsync(RutaProyectoActual, cambio.RutaRelativa);
        EstadoOperacionGitTexto = res.Mensaje;
        await RefrescarGitAsync();
    }

    public async Task PrepararTodoGitAsync()
    {
        if (string.IsNullOrWhiteSpace(RutaProyectoActual)) return;
        var res = await _servicioGit.PrepararTodoAsync(RutaProyectoActual);
        EstadoOperacionGitTexto = res.Mensaje;
        await RefrescarGitAsync();
    }

    public async Task QuitarPreparadoGitAsync(ArchivoCambioGit cambio)
    {
        if (string.IsNullOrWhiteSpace(RutaProyectoActual)) return;
        var res = await _servicioGit.QuitarDePreparadosAsync(RutaProyectoActual, cambio.RutaRelativa);
        EstadoOperacionGitTexto = res.Mensaje;
        await RefrescarGitAsync();
    }

    public Task<string> ObtenerDiffCambioGitAsync(ArchivoCambioGit cambio) =>
        string.IsNullOrWhiteSpace(RutaProyectoActual)
            ? Task.FromResult(string.Empty)
            : _servicioGit.ObtenerDiferenciasAsync(RutaProyectoActual, cambio);

    public async Task CrearCommitGitAsync()
    {
        if (string.IsNullOrWhiteSpace(RutaProyectoActual)) return;
        if (string.IsNullOrWhiteSpace(MensajeCommitGit))
        {
            MostrarToast("Escribe un mensaje de commit.");
            return;
        }

        EstaOperandoGit = true;
        try
        {
            if (CambiosGit.Count > 0 && CambiosGit.All(c => !c.EstaPreparado))
            {
                await _servicioGit.PrepararTodoAsync(RutaProyectoActual);
            }

            string? email = !string.IsNullOrWhiteSpace(CuentaGitHubActual?.Email)
                ? CuentaGitHubActual.Email
                : (CuentaGitHubActual is not null ? $"{CuentaGitHubActual.Login}@users.noreply.github.com" : null);

            var res = await _servicioGit.CrearCommitAsync(
                RutaProyectoActual,
                MensajeCommitGit,
                CuentaGitHubActual?.Nombre ?? CuentaGitHubActual?.Login,
                email);

            EstadoOperacionGitTexto = res.Mensaje;
            if (res.Exito)
            {
                MensajeCommitGit = string.Empty;
            }
            RegistrarActividad("Git", res.Mensaje);
            MostrarToast(res.Mensaje);
            await RefrescarGitAsync();
        }
        finally
        {
            EstaOperandoGit = false;
        }
    }

    public async Task CrearRamaGitAsync(string nombreRama)
    {
        if (string.IsNullOrWhiteSpace(RutaProyectoActual)) return;
        var res = await _servicioGit.CrearRamaAsync(RutaProyectoActual, nombreRama);
        EstadoOperacionGitTexto = res.Mensaje;
        RegistrarActividad("Git", res.Mensaje);
        MostrarToast(res.Mensaje);
        await RefrescarGitAsync();
    }

    public async Task CambiarRamaGitAsync(string rama)
    {
        if (string.IsNullOrWhiteSpace(RutaProyectoActual)) return;
        var res = await _servicioGit.CambiarRamaAsync(RutaProyectoActual, rama);
        EstadoOperacionGitTexto = res.Mensaje;
        RegistrarActividad("Git", res.Mensaje);
        MostrarToast(res.Mensaje);
        await RefrescarGitAsync();
    }

    public async Task PushGitAsync()
    {
        if (string.IsNullOrWhiteSpace(RutaProyectoActual)) return;
        EstaOperandoGit = true;
        try
        {
            string rama = string.IsNullOrWhiteSpace(EstadoGit.RamaActual) ? "main" : EstadoGit.RamaActual;
            var res = await _servicioGit.PushAsync(
                RutaProyectoActual,
                rama,
                _servicioGitHub.ObtenerTokenTemporalParaGit());
            EstadoOperacionGitTexto = res.Mensaje;
            RegistrarActividad("Git", res.Mensaje);
            MostrarToast(res.Mensaje);
            await RefrescarGitAsync();
        }
        finally
        {
            EstaOperandoGit = false;
        }
    }

    public async Task PullGitAsync()
    {
        if (string.IsNullOrWhiteSpace(RutaProyectoActual)) return;
        EstaOperandoGit = true;
        try
        {
            var res = await _servicioGit.PullAsync(
                RutaProyectoActual,
                _servicioGitHub.ObtenerTokenTemporalParaGit());
            EstadoOperacionGitTexto = res.Mensaje;
            RegistrarActividad("Git", res.Mensaje);
            MostrarToast(res.Mensaje);
            RefrescarExplorador();
            await RefrescarGitAsync();
        }
        finally
        {
            EstaOperandoGit = false;
        }
    }

    public async Task<(bool Exito, string Mensaje)> ClonarYAbrirRepositorioAsync(string cloneUrl, string carpetaDestino)
    {
        EstaOperandoGit = true;
        EstadoOperacionGitTexto = $"Clonando {cloneUrl}...";
        try
        {
            var (res, rutaClonada) = await _servicioGit.ClonarRepositorioAsync(
                cloneUrl,
                carpetaDestino,
                _servicioGitHub.ObtenerTokenTemporalParaGit());

            EstadoOperacionGitTexto = res.Mensaje;
            RegistrarActividad("GitHub", res.Mensaje);
            MostrarToast(res.Mensaje);

            if (res.Exito && Directory.Exists(rutaClonada))
            {
                Dispatcher.UIThread.Post(() => AbrirCarpetaProyecto(rutaClonada));
            }

            return (res.Exito, res.Mensaje);
        }
        finally
        {
            EstaOperandoGit = false;
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

        if (info.HayActualizacion &&
            info.PuedeInstalarDirectamente &&
            Configuracion.DescargarActualizacionesAutomaticamente)
        {
            ResultadoDescargaActualizacion descarga = await _gestorActualizaciones.DescargarYVerificarAsync(info);
            if (descarga.Exito)
            {
                _rutaPaqueteActualizacionListo = descarga.RutaArchivo;
            }
        }

        Dispatcher.UIThread.Post(() =>
        {
            HayActualizacionDisponible = info.HayActualizacion;
            VersionDisponibleTexto = info.HayActualizacion
                ? (!string.IsNullOrWhiteSpace(_rutaPaqueteActualizacionListo)
                    ? $"⬇ Instalar v{info.VersionDisponible}"
                    : $"⬇ Actualizar a v{info.VersionDisponible}")
                : string.Empty;

            RegistrarActividad("Actualizaciones", info.MensajeEstado);
            if (!silencioso || info.HayActualizacion)
            {
                MostrarToast(info.MensajeEstado);
            }

            if (info.HayActualizacion)
            {
                ActualizacionListaParaInstalar?.Invoke(info);
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
        if (ext == ".cibao") return "Cibao";
        if (ext == ".alb") return "Cibao (.alb)";
        if (ext == ".py") return "Python";
        if (ext is ".html" or ".htm") return "HTML";
        if (ext == ".cs") return "C#";
        if (ext is ".cpp" or ".cc" or ".cxx" or ".h" or ".hpp") return "C++";
        if (ext == ".css") return "CSS";
        if (ext is ".js" or ".mjs") return "JavaScript";
        if (ext is ".ts" or ".tsx") return "TypeScript";
        if (ext == ".go") return "Go";
        if (ext == ".rs") return "Rust";
        if (ext == ".java") return "Java";
        if (ext == ".php") return "PHP";
        if (ext == ".rb") return "Ruby";
        if (ext == ".json") return "JSON";
        if (ext == ".md") return "Markdown";

        AddonCargado? addon = _gestorAddons.ObtenerAddonPorExtension(ext);
        return addon is not null ? addon.Manifiesto.Lenguaje : "Texto plano";
    }

    public void SincronizarRecientes()
    {
        Recientes.Clear();
        ProyectosRecientes.Clear();
        ArchivosRecientes.Clear();

        foreach (ElementoReciente r in _gestorConfiguracion.Recientes)
        {
            Recientes.Add(r);
            if (r.EsProyecto)
            {
                ProyectosRecientes.Add(r);
            }
            else
            {
                ArchivosRecientes.Add(r);
            }
        }

        OnPropertyChanged(nameof(SinProyectosRecientes));
        OnPropertyChanged(nameof(SinArchivosRecientes));
    }

    public void GuardarEstadoSesion()
    {
        _gestorConfiguracion.GuardarSesion(
            RutaProyectoActual,
            PestanasAbiertas.Select(p => p.RutaArchivo),
            ArchivoActivo?.RutaArchivo,
            ArchivosIndependientes.Select(a => a.RutaArchivo));
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

    private void SincronizarEntornosSinBloquear()
    {
        EntornosDetectados.Clear();
        foreach (EstadoEntornoLenguaje env in _detectorEntornos.ObtenerUltimoEstadoSinBloquear())
        {
            EntornosDetectados.Add(env);
        }
    }

    private void ActualizarComandosRecomendadosTerminal()
    {
        ComandosRecomendadosTerminal.Clear();
        foreach (string cmd in GestorTerminales.ObtenerComandosRecomendados(ArchivoActivo?.RutaArchivo))
        {
            ComandosRecomendadosTerminal.Add(cmd);
        }
    }

    private bool EstaDentroDeProyectoActual(string rutaArchivo)
    {
        if (string.IsNullOrWhiteSpace(RutaProyectoActual)) return false;
        string raiz = Path.GetFullPath(RutaProyectoActual).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                      + Path.DirectorySeparatorChar;
        return Path.GetFullPath(rutaArchivo).StartsWith(raiz, StringComparison.OrdinalIgnoreCase);
    }

    private void AlCambiarDocumentoActivo(object? sender, EventArgs e)
    {
        _timerDebounceDiagnostico?.Stop();
        _timerDebounceDiagnostico = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
        _timerDebounceDiagnostico.Tick += (_, _) =>
        {
            _timerDebounceDiagnostico.Stop();
            EjecutarDiagnosticoEnVivo();
            if (MostrarVistaPreviaWeb && (ConsultarAutoRecargaWeb?.Invoke() ?? true))
            {
                RefrescarVistaPreviaWeb(sincronizarDisco: false);
            }
        };
        _timerDebounceDiagnostico.Start();
    }

    public void AbrirVistaPreviaWeb(string? rutaHtmlSolicitada = null)
    {
        string? objetivo = ResolverArchivoHtmlVistaPrevia(rutaHtmlSolicitada);
        if (string.IsNullOrWhiteSpace(objetivo) || !File.Exists(objetivo))
        {
            MostrarToast("Abre o crea un archivo .html para activar la vista previa Web integrada.");
            return;
        }

        try
        {
            string raiz = !string.IsNullOrWhiteSpace(RutaProyectoActual) &&
                          objetivo.StartsWith(RutaProyectoActual, StringComparison.OrdinalIgnoreCase)
                ? RutaProyectoActual
                : (Path.GetDirectoryName(objetivo) ?? Directory.GetCurrentDirectory());

            _servidorWebLocal.IniciarOActualizarRaiz(raiz);
            _rutaHtmlVistaPrevia = objetivo;
            UrlVistaPreviaWeb = _servidorWebLocal.ConstruirUrlParaArchivo(objetivo);

            MostrarPantallaInicio = false;
            MostrarCentroTools = false;
            MostrarVistaPreviaWeb = true;
            RefrescarVistaPreviaWeb(sincronizarDisco: true);
            RegistrarActividad("Web", $"Vista previa Live Server activa: {UrlVistaPreviaWeb}");
        }
        catch (Exception ex)
        {
            MostrarToast($"No se pudo iniciar la vista previa Web: {ex.Message}");
        }
    }

    public void RefrescarVistaPreviaWeb(bool sincronizarDisco = true)
    {
        if (string.IsNullOrWhiteSpace(_rutaHtmlVistaPrevia) || !File.Exists(_rutaHtmlVistaPrevia))
        {
            string? fallback = ResolverArchivoHtmlVistaPrevia(null);
            if (string.IsNullOrWhiteSpace(fallback)) return;
            _rutaHtmlVistaPrevia = fallback;
        }

        string html;
        if (ArchivoActivo is not null &&
            ArchivoActivo.RutaArchivo.Equals(_rutaHtmlVistaPrevia, StringComparison.OrdinalIgnoreCase))
        {
            html = ArchivoActivo.Documento.Text;
            if (sincronizarDisco)
            {
                _gestorProyectos.GuardarArchivo(_rutaHtmlVistaPrevia, html);
                ArchivoActivo.TieneCambiosSinGuardar = false;
                OnPropertyChanged(nameof(TituloVentana));
            }
        }
        else
        {
            if (sincronizarDisco &&
                ArchivoActivo is not null &&
                Path.GetExtension(ArchivoActivo.RutaArchivo).ToLowerInvariant() is ".css" or ".js" or ".mjs")
            {
                _gestorProyectos.GuardarArchivo(ArchivoActivo.RutaArchivo, ArchivoActivo.Documento.Text);
                ArchivoActivo.TieneCambiosSinGuardar = false;
                OnPropertyChanged(nameof(TituloVentana));
            }
            html = File.ReadAllText(_rutaHtmlVistaPrevia);
        }

        _servidorWebLocal.NotificarCambioArchivo();
        UrlVistaPreviaWeb = _servidorWebLocal.ConstruirUrlParaArchivo(_rutaHtmlVistaPrevia);
        SolicitarActualizarVistaWeb?.Invoke(_rutaHtmlVistaPrevia, html, UrlVistaPreviaWeb);
    }

    public void CerrarVistaPreviaWeb() => MostrarVistaPreviaWeb = false;

    public void AbrirWebEnNavegadorExterno()
    {
        if (string.IsNullOrWhiteSpace(UrlVistaPreviaWeb))
        {
            AbrirVistaPreviaWeb();
        }
        if (string.IsNullOrWhiteSpace(UrlVistaPreviaWeb)) return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(UrlVistaPreviaWeb)
            {
                UseShellExecute = true
            });
            RegistrarActividad("Web", $"Abierto en navegador del sistema: {UrlVistaPreviaWeb}");
        }
        catch (Exception ex)
        {
            MostrarToast($"No se pudo abrir el navegador externo: {ex.Message}");
        }
    }

    private string? ResolverArchivoHtmlVistaPrevia(string? rutaExplicita)
    {
        if (!string.IsNullOrWhiteSpace(rutaExplicita) && File.Exists(rutaExplicita))
        {
            return Path.GetFullPath(rutaExplicita);
        }

        if (ArchivoActivo is not null &&
            Path.GetExtension(ArchivoActivo.RutaArchivo).ToLowerInvariant() is ".html" or ".htm")
        {
            return ArchivoActivo.RutaArchivo;
        }

        if (!string.IsNullOrWhiteSpace(_rutaHtmlVistaPrevia) && File.Exists(_rutaHtmlVistaPrevia))
        {
            return _rutaHtmlVistaPrevia;
        }

        if (!string.IsNullOrWhiteSpace(RutaProyectoActual))
        {
            string idx = Path.Combine(RutaProyectoActual, "index.html");
            if (File.Exists(idx)) return idx;
            return BuscadorProyecto.ListarArchivosProyecto(RutaProyectoActual)
                .FirstOrDefault(f => f.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".htm", StringComparison.OrdinalIgnoreCase));
        }

        return null;
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
        _gestorTerminales.Dispose();
        _servicioGitHub.Dispose();
        _servidorWebLocal.Dispose();
    }
}
