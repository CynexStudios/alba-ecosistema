using Alba.IDE.Models;
using Alba.IDE.Services;
using Alba.IDE.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Alba.IDE.Views;

public partial class VentanaActualizacion : Window
{
    private readonly VentanaPrincipalViewModel? _vm;
    private readonly Action? _solicitarCierreApp;
    private CancellationTokenSource? _ctsDescarga;
    private InformacionActualizacion? _info;
    private string? _rutaPaqueteListo;
    private bool _descargando;

    public VentanaActualizacion()
    {
        InitializeComponent();
        Closing += (_, _) => _ctsDescarga?.Cancel();
    }

    public VentanaActualizacion(
        VentanaPrincipalViewModel vm,
        InformacionActualizacion? infoInicial = null,
        string? rutaPaquetePreDescargado = null,
        Action? solicitarCierreApp = null) : this()
    {
        _vm = vm;
        _info = infoInicial ?? vm.UltimaInfoActualizacion;
        _rutaPaqueteListo = rutaPaquetePreDescargado;
        _solicitarCierreApp = solicitarCierreApp;

        if (_info is not null)
        {
            AplicarInformacion(_info);
        }
        else
        {
            _ = RefrescarEstadoAsync();
        }
    }

    private void AplicarInformacion(InformacionActualizacion info)
    {
        _info = info;
        TxtVersiones.Text = info.HayActualizacion
            ? $"v{info.VersionActual}  →  v{info.VersionDisponible}"
            : $"v{info.VersionActual}";

        TxtTituloEstado.Text = info.HayActualizacion
            ? $"Actualización disponible: v{info.VersionDisponible}"
            : "Xynex Code Editor está actualizado";

        TxtEntorno.Text = string.IsNullOrWhiteSpace(info.DescripcionEntorno)
            ? "Entorno detectado automáticamente"
            : $"Método de actualización detectado: {info.DescripcionEntorno}";

        TxtPaquete.Text = info.AssetRecomendado is not null
            ? $"Paquete: {info.AssetRecomendado.Nombre} ({info.AssetRecomendado.TamanoFormateado})"
            : "No se detectó paquete instalable directo en esta release (usa 'Ver en GitHub').";

        TxtNotas.Text = string.IsNullOrWhiteSpace(info.Notas)
            ? info.MensajeEstado
            : info.Notas;

        if (!string.IsNullOrWhiteSpace(_rutaPaqueteListo) && File.Exists(_rutaPaqueteListo))
        {
            BarraProgreso.Value = 100;
            TxtProgreso.Text = "Paquete descargado y verificado. Listo para instalar y reiniciar.";
            BtnActualizar.Content = "Instalar y reiniciar ahora";
            BtnActualizar.IsEnabled = info.HayActualizacion && info.PuedeInstalarDirectamente;
        }
        else
        {
            BarraProgreso.Value = 0;
            TxtProgreso.Text = info.MensajeEstado;
            BtnActualizar.Content = "Descargar e instalar";
            BtnActualizar.IsEnabled = info.HayActualizacion && info.PuedeInstalarDirectamente;
        }
    }

    private async Task RefrescarEstadoAsync()
    {
        if (_vm is null) return;

        BtnComprobar.IsEnabled = false;
        BtnActualizar.IsEnabled = false;
        BarraProgreso.IsIndeterminate = true;
        TxtProgreso.Text = "Consultando GitHub Releases...";

        await _vm.ComprobarActualizacionesAsync(silencioso: true);
        BarraProgreso.IsIndeterminate = false;
        BtnComprobar.IsEnabled = true;

        if (_vm.UltimaInfoActualizacion is { } nueva)
        {
            _rutaPaqueteListo = _vm.RutaPaqueteActualizacionListo;
            AplicarInformacion(nueva);
        }
    }

    private async void AlComprobar(object? sender, RoutedEventArgs e)
    {
        if (_descargando) return;
        await RefrescarEstadoAsync();
    }

    private async void AlDescargarEInstalar(object? sender, RoutedEventArgs e)
    {
        if (_vm is null || _info is null) return;

        if (_descargando)
        {
            _ctsDescarga?.Cancel();
            return;
        }

        if (!string.IsNullOrWhiteSpace(_rutaPaqueteListo) && File.Exists(_rutaPaqueteListo))
        {
            EjecutarInstalacion(_rutaPaqueteListo, _info);
            return;
        }

        _descargando = true;
        _ctsDescarga = new CancellationTokenSource();
        BtnComprobar.IsEnabled = false;
        BtnActualizar.Content = "Cancelar descarga";

        var progreso = new Progress<ProgresoActualizacion>(p =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                BarraProgreso.IsIndeterminate = p.TotalBytes <= 0 && p.Porcentaje <= 0;
                BarraProgreso.Value = p.Porcentaje;
                TxtProgreso.Text = p.EstadoTexto;
            });
        });

        ResultadoDescargaActualizacion res = await _vm.GestorActualizaciones.DescargarYVerificarAsync(
            _info,
            progreso,
            _ctsDescarga.Token);

        _descargando = false;
        BarraProgreso.IsIndeterminate = false;
        BtnComprobar.IsEnabled = true;

        if (!res.Exito)
        {
            TxtProgreso.Text = res.Mensaje;
            BtnActualizar.Content = "Reintentar descarga";
            BtnActualizar.IsEnabled = _info.PuedeInstalarDirectamente;
            return;
        }

        _rutaPaqueteListo = res.RutaArchivo;
        BarraProgreso.Value = 100;
        TxtProgreso.Text = res.Mensaje;
        EjecutarInstalacion(res.RutaArchivo, _info);
    }

    private void EjecutarInstalacion(string rutaPaquete, InformacionActualizacion info)
    {
        if (_vm is null) return;

        var (exito, mensaje) = _vm.GestorActualizaciones.IniciarInstalacionYReiniciar(rutaPaquete, info);
        TxtProgreso.Text = mensaje;
        if (!exito)
        {
            BtnActualizar.Content = "Reintentar instalación";
            BtnActualizar.IsEnabled = true;
            return;
        }

        _vm.RegistrarActividad("Actualizaciones", mensaje);
        Close(true);
        _solicitarCierreApp?.Invoke();
    }

    private async void AlAbrirGitHub(object? sender, RoutedEventArgs e)
    {
        string url = _info?.UrlDescarga ?? $"https://github.com/{RutasUsuarioAlba.RepositorioGitHub}/releases";
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            await Launcher.LaunchUriAsync(uri);
        }
    }

    private void AlCerrar(object? sender, RoutedEventArgs e)
    {
        _ctsDescarga?.Cancel();
        Close(false);
    }
}
