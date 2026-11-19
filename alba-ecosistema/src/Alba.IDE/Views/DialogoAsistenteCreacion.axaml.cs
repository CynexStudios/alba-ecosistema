using Alba.IDE.Models;
using Alba.IDE.Services;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Alba.IDE.Views;

public partial class DialogoAsistenteCreacion : Window
{
    private readonly DetectorEntornos _detector;
    private readonly ServicioCreacion _servicioCreacion;
    private ModalidadCreacion _modalidadActual;
    private IReadOnlyList<EstadoEntornoLenguaje> _entornos = [];

    public DialogoAsistenteCreacion()
        : this(ModalidadCreacion.Proyecto, new DetectorEntornos(), new ServicioCreacion(), null)
    {
    }

    public DialogoAsistenteCreacion(
        ModalidadCreacion modalidadInicial,
        DetectorEntornos detector,
        ServicioCreacion servicioCreacion,
        string? carpetaInicial)
    {
        InitializeComponent();
        _detector = detector;
        _servicioCreacion = servicioCreacion;
        _modalidadActual = modalidadInicial;

        TxtCarpetaDestino.Text = !string.IsNullOrWhiteSpace(carpetaInicial) && Directory.Exists(carpetaInicial)
            ? carpetaInicial
            : RutasUsuarioAlba.ObtenerCarpetaProyectosPorDefecto();

        _entornos = _detector.ObtenerUltimoEstadoSinBloquear();
        PoblarLenguajes();
        ActualizarUiModalidad();

        _ = CargarEntornosRealesAsync();
    }

    private async Task CargarEntornosRealesAsync(bool forzar = false)
    {
        _entornos = await _detector.DetectarTodosAsync(forzar);
        LenguajeSoportado? seleccionado = (ListaLenguajes.SelectedItem as EstadoEntornoLenguaje)?.Lenguaje;
        PoblarLenguajes(seleccionado);
    }

    private void PoblarLenguajes(LenguajeSoportado? mantenerSeleccion = null)
    {
        ListaLenguajes.ItemsSource = _entornos.ToList();
        EstadoEntornoLenguaje? objetivo = mantenerSeleccion.HasValue
            ? _entornos.FirstOrDefault(e => e.Lenguaje == mantenerSeleccion.Value)
            : _entornos.FirstOrDefault();
        ListaLenguajes.SelectedItem = objetivo ?? _entornos.FirstOrDefault();
    }

    private void ActualizarUiModalidad()
    {
        BtnModProyecto.Opacity = _modalidadActual == ModalidadCreacion.Proyecto ? 1.0 : 0.68;
        BtnModPrograma.Opacity = _modalidadActual == ModalidadCreacion.Programa ? 1.0 : 0.68;
        BtnModArchivo.Opacity = _modalidadActual == ModalidadCreacion.ArchivoIndependiente ? 1.0 : 0.68;

        ChkInicializarGit.IsVisible = _modalidadActual == ModalidadCreacion.Proyecto;
        TxtTituloPlantillas.Text = _modalidadActual switch
        {
            ModalidadCreacion.Proyecto => "PLANTILLAS DE PROYECTO",
            ModalidadCreacion.Programa => "PLANTILLAS DE PROGRAMA EJECUTABLE",
            ModalidadCreacion.ArchivoIndependiente => "ARCHIVO INDEPENDIENTE",
            _ => "PLANTILLAS"
        };

        TxtNombre.Text = _modalidadActual switch
        {
            ModalidadCreacion.Proyecto => "MiProyectoCibao",
            ModalidadCreacion.Programa => "mi_programa",
            ModalidadCreacion.ArchivoIndependiente => "archivo",
            _ => "nuevo"
        };

        RefrescarPlantillas();
    }

    private void RefrescarPlantillas()
    {
        LenguajeSoportado lenguaje = (ListaLenguajes.SelectedItem as EstadoEntornoLenguaje)?.Lenguaje ?? LenguajeSoportado.Cibao;
        IReadOnlyList<PlantillaCreacion> plantillas = _servicioCreacion.ObtenerPlantillas(_modalidadActual, lenguaje, _entornos);

        ListaPlantillas.ItemsSource = plantillas.ToList();
        ListaPlantillas.SelectedItem = plantillas.FirstOrDefault(p => p.DisponibleEnEquipo) ?? plantillas.FirstOrDefault();
        ActualizarEstadoSeleccion();
    }

    private void ActualizarEstadoSeleccion()
    {
        if (ListaPlantillas.SelectedItem is not PlantillaCreacion plantilla)
        {
            BtnCrearConfirmar.IsEnabled = false;
            TxtAdvertenciaSdk.Text = string.Empty;
            TxtResumenCreacion.Text = "Selecciona una plantilla para continuar.";
            return;
        }

        BtnCrearConfirmar.IsEnabled = plantilla.DisponibleEnEquipo;
        TxtAdvertenciaSdk.Text = plantilla.DisponibleEnEquipo
            ? string.Empty
            : $"SDK requerido no detectado: {plantilla.MotivoNoDisponible}";

        string destino = TxtCarpetaDestino.Text?.Trim() ?? string.Empty;
        string nombre = TxtNombre.Text?.Trim() ?? "nuevo";
        TxtResumenCreacion.Text = plantilla.DisponibleEnEquipo
            ? $"Se creará {plantilla.Nombre} ({plantilla.ExtensionArchivoPrincipal}) en {Path.Combine(destino, nombre)}"
            : $"No disponible en este equipo hasta instalar: {plantilla.DependenciasRecomendadas}";
    }

    private void AlSeleccionarModalidadProyecto(object? sender, RoutedEventArgs e)
    {
        _modalidadActual = ModalidadCreacion.Proyecto;
        ActualizarUiModalidad();
    }

    private void AlSeleccionarModalidadPrograma(object? sender, RoutedEventArgs e)
    {
        _modalidadActual = ModalidadCreacion.Programa;
        ActualizarUiModalidad();
    }

    private void AlSeleccionarModalidadArchivo(object? sender, RoutedEventArgs e)
    {
        _modalidadActual = ModalidadCreacion.ArchivoIndependiente;
        ActualizarUiModalidad();
    }

    private void AlSeleccionarLenguaje(object? sender, SelectionChangedEventArgs e)
    {
        RefrescarPlantillas();
    }

    private void AlSeleccionarPlantilla(object? sender, SelectionChangedEventArgs e)
    {
        ActualizarEstadoSeleccion();
    }

    private void AlCambiarTextoResumen(object? sender, TextChangedEventArgs e)
    {
        ActualizarEstadoSeleccion();
    }

    private async void AlReescanearEntornos(object? sender, RoutedEventArgs e)
    {
        TxtResumenCreacion.Text = "Escaneando compiladores e intérpretes del sistema...";
        await CargarEntornosRealesAsync(forzar: true);
        RefrescarPlantillas();
    }

    private async void AlExaminarCarpeta(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFolder> carpetas = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Seleccionar carpeta de destino",
                AllowMultiple = false
            });

        if (carpetas.Count > 0)
        {
            string? ruta = carpetas[0].TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(ruta))
            {
                TxtCarpetaDestino.Text = ruta;
                ActualizarEstadoSeleccion();
            }
        }
    }

    private void AlConfirmarCreacion(object? sender, RoutedEventArgs e)
    {
        if (ListaPlantillas.SelectedItem is not PlantillaCreacion plantilla || !plantilla.DisponibleEnEquipo)
        {
            return;
        }

        string nombre = string.IsNullOrWhiteSpace(TxtNombre.Text) ? "Nuevo" : TxtNombre.Text.Trim();
        string carpeta = string.IsNullOrWhiteSpace(TxtCarpetaDestino.Text)
            ? RutasUsuarioAlba.ObtenerCarpetaProyectosPorDefecto()
            : TxtCarpetaDestino.Text.Trim();

        var solicitud = new SolicitudCreacion
        {
            Modalidad = _modalidadActual,
            Lenguaje = plantilla.Lenguaje,
            Plantilla = plantilla,
            Nombre = nombre,
            CarpetaDestino = carpeta,
            InicializarGit = _modalidadActual == ModalidadCreacion.Proyecto && ChkInicializarGit.IsChecked == true
        };

        Close(solicitud);
    }

    private void AlCancelar(object? sender, RoutedEventArgs e) => Close(null);
}
