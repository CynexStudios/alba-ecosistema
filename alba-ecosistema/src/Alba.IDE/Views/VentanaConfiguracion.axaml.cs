using Alba.IDE.Models;
using Alba.IDE.Services;
using Alba.IDE.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Alba.IDE.Views;

public partial class VentanaConfiguracion : Window
{
    private readonly VentanaPrincipalViewModel? _vm;
    private string _urlReleases = $"https://github.com/{RutasUsuarioAlba.RepositorioGitHub}/releases";

    public VentanaConfiguracion()
    {
        InitializeComponent();
    }

    public VentanaConfiguracion(VentanaPrincipalViewModel vm) : this()
    {
        _vm = vm;
        CargarValoresActuales();
    }

    private void CargarValoresActuales()
    {
        if (_vm is null) return;

        var temas = _vm.GestorTemas.TemasDisponibles.ToList();
        CmbTemas.ItemsSource = temas;
        CmbTemas.SelectedItem = temas.FirstOrDefault(t =>
            t.Id.Equals(_vm.Configuracion.TemaId, StringComparison.OrdinalIgnoreCase)) ?? temas.FirstOrDefault();

        ChkSintaxisPersonalizada.IsChecked = _vm.Configuracion.UsarSintaxisPersonalizada;
        ColoresSintaxisConfig syn = _vm.Configuracion.SintaxisPersonalizada;
        TxtColComentario.Text = syn.Comentario;
        TxtColCadena.Text = syn.Cadena;
        TxtColNumero.Text = syn.Numero;
        TxtColControl.Text = syn.PalabraControl;
        TxtColDeclaracion.Text = syn.PalabraDeclaracion;
        TxtColTipo.Text = syn.TipoDato;
        TxtColFuncion.Text = syn.FuncionIntegrada;
        TxtColOperador.Text = syn.OperadorLogico;

        NumFuente.Value = (decimal)_vm.Configuracion.TamanoFuenteEditor;
        NumTab.Value = _vm.Configuracion.TamanoTabulacion;
        ChkAutoguardado.IsChecked = _vm.Configuracion.GuardadoAutomatico;
        NumIntervaloGuardado.Value = _vm.Configuracion.IntervaloGuardadoSegundos;
        ChkRestaurarSesion.IsChecked = _vm.Configuracion.RestaurarUltimaSesion;
        ChkComprobarActualizaciones.IsChecked = _vm.Configuracion.ComprobarActualizacionesAlIniciar;

        var atajos = _vm.Configuracion.AtajosTeclado;
        TxtAtajoGuardar.Text = atajos.GetValueOrDefault("Guardar", "Ctrl+S");
        TxtAtajoEjecutar.Text = atajos.GetValueOrDefault("Ejecutar", "F5");
        TxtAtajoVerificar.Text = atajos.GetValueOrDefault("Verificar", "Ctrl+Shift+B");
        TxtAtajoPaleta.Text = atajos.GetValueOrDefault("PaletaComandos", "Ctrl+Shift+P");
        TxtAtajoAbrirRapido.Text = atajos.GetValueOrDefault("AbrirRapido", "Ctrl+P");
        TxtAtajoBuscarArchivo.Text = atajos.GetValueOrDefault("BuscarArchivo", "Ctrl+F");
        TxtAtajoBuscarProyecto.Text = atajos.GetValueOrDefault("BuscarProyecto", "Ctrl+Shift+F");
        TxtAtajoSidebar.Text = atajos.GetValueOrDefault("AlternarBarraLateral", "Ctrl+B");
        TxtAtajoPanelInferior.Text = atajos.GetValueOrDefault("AlternarPanelInferior", "Ctrl+J");

        TxtRutaDatosUsuario.Text = $"Directorio persistente de usuario: {RutasUsuarioAlba.ObtenerDirectorioDatosUsuario()}";
        if (_vm.UltimaInfoActualizacion is { } info)
        {
            TxtEstadoActualizacion.Text = info.MensajeEstado;
            TxtNotasActualizacion.Text = info.Notas;
            _urlReleases = info.UrlDescarga;
        }
    }

    private void AlCambiarTemaCombo(object? sender, SelectionChangedEventArgs e)
    {
        if (_vm is null || CmbTemas.SelectedItem is not TemaIDE tema) return;
        _vm.Configuracion.TemaId = tema.Id;
        _vm.AplicarTemaYConfiguracion();
    }

    private void AlCopiarColoresTema(object? sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        ColoresSintaxisConfig syn = _vm.GestorTemas.TemaActual.Sintaxis;
        TxtColComentario.Text = syn.Comentario;
        TxtColCadena.Text = syn.Cadena;
        TxtColNumero.Text = syn.Numero;
        TxtColControl.Text = syn.PalabraControl;
        TxtColDeclaracion.Text = syn.PalabraDeclaracion;
        TxtColTipo.Text = syn.TipoDato;
        TxtColFuncion.Text = syn.FuncionIntegrada;
        TxtColOperador.Text = syn.OperadorLogico;
    }

    private async void AlComprobarActualizaciones(object? sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        TxtEstadoActualizacion.Text = "Comprobando lanzamientos oficiales...";
        await _vm.ComprobarActualizacionesAsync(silencioso: true);
        if (_vm.UltimaInfoActualizacion is { } info)
        {
            TxtEstadoActualizacion.Text = info.MensajeEstado;
            TxtNotasActualizacion.Text = info.Notas;
            _urlReleases = info.UrlDescarga;
        }
    }

    private async void AlAbrirReleases(object? sender, RoutedEventArgs e)
    {
        if (Uri.TryCreate(_urlReleases, UriKind.Absolute, out Uri? uri))
        {
            await Launcher.LaunchUriAsync(uri);
        }
    }

    private void AlGuardar(object? sender, RoutedEventArgs e)
    {
        if (_vm is null)
        {
            Close(false);
            return;
        }

        ConfiguracionIDE cfg = _vm.Configuracion;
        if (CmbTemas.SelectedItem is TemaIDE tema)
        {
            cfg.TemaId = tema.Id;
        }

        cfg.UsarSintaxisPersonalizada = ChkSintaxisPersonalizada.IsChecked == true;
        cfg.SintaxisPersonalizada.Comentario = TxtColComentario.Text?.Trim() ?? cfg.SintaxisPersonalizada.Comentario;
        cfg.SintaxisPersonalizada.Cadena = TxtColCadena.Text?.Trim() ?? cfg.SintaxisPersonalizada.Cadena;
        cfg.SintaxisPersonalizada.Numero = TxtColNumero.Text?.Trim() ?? cfg.SintaxisPersonalizada.Numero;
        cfg.SintaxisPersonalizada.PalabraControl = TxtColControl.Text?.Trim() ?? cfg.SintaxisPersonalizada.PalabraControl;
        cfg.SintaxisPersonalizada.PalabraDeclaracion = TxtColDeclaracion.Text?.Trim() ?? cfg.SintaxisPersonalizada.PalabraDeclaracion;
        cfg.SintaxisPersonalizada.TipoDato = TxtColTipo.Text?.Trim() ?? cfg.SintaxisPersonalizada.TipoDato;
        cfg.SintaxisPersonalizada.FuncionIntegrada = TxtColFuncion.Text?.Trim() ?? cfg.SintaxisPersonalizada.FuncionIntegrada;
        cfg.SintaxisPersonalizada.OperadorLogico = TxtColOperador.Text?.Trim() ?? cfg.SintaxisPersonalizada.OperadorLogico;

        cfg.TamanoFuenteEditor = (double)(NumFuente.Value ?? 14);
        cfg.TamanoTabulacion = (int)(NumTab.Value ?? 4);
        cfg.GuardadoAutomatico = ChkAutoguardado.IsChecked == true;
        cfg.IntervaloGuardadoSegundos = (int)(NumIntervaloGuardado.Value ?? 15);
        cfg.RestaurarUltimaSesion = ChkRestaurarSesion.IsChecked == true;
        cfg.ComprobarActualizacionesAlIniciar = ChkComprobarActualizaciones.IsChecked == true;

        cfg.AtajosTeclado["Guardar"] = TxtAtajoGuardar.Text?.Trim() ?? "Ctrl+S";
        cfg.AtajosTeclado["Ejecutar"] = TxtAtajoEjecutar.Text?.Trim() ?? "F5";
        cfg.AtajosTeclado["Verificar"] = TxtAtajoVerificar.Text?.Trim() ?? "Ctrl+Shift+B";
        cfg.AtajosTeclado["PaletaComandos"] = TxtAtajoPaleta.Text?.Trim() ?? "Ctrl+Shift+P";
        cfg.AtajosTeclado["AbrirRapido"] = TxtAtajoAbrirRapido.Text?.Trim() ?? "Ctrl+P";
        cfg.AtajosTeclado["BuscarArchivo"] = TxtAtajoBuscarArchivo.Text?.Trim() ?? "Ctrl+F";
        cfg.AtajosTeclado["BuscarProyecto"] = TxtAtajoBuscarProyecto.Text?.Trim() ?? "Ctrl+Shift+F";
        cfg.AtajosTeclado["AlternarBarraLateral"] = TxtAtajoSidebar.Text?.Trim() ?? "Ctrl+B";
        cfg.AtajosTeclado["AlternarPanelInferior"] = TxtAtajoPanelInferior.Text?.Trim() ?? "Ctrl+J";

        _vm.AplicarTemaYConfiguracion();
        Close(true);
    }

    private void AlCancelar(object? sender, RoutedEventArgs e) => Close(false);
}
