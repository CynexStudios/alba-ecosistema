using Alba.IDE.Models;
using Alba.IDE.Services;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Alba.IDE.Views;

public partial class DialogoPublicarGitHub : Window
{
    private readonly ServicioGitHub _servicioGitHub;
    private readonly ServicioGit _servicioGit;
    private readonly string _rutaProyecto;

    public DialogoPublicarGitHub() : this(new ServicioGitHub(), new ServicioGit(), Directory.GetCurrentDirectory())
    {
    }

    public DialogoPublicarGitHub(ServicioGitHub servicioGitHub, ServicioGit servicioGit, string rutaProyecto)
    {
        InitializeComponent();
        _servicioGitHub = servicioGitHub;
        _servicioGit = servicioGit;
        _rutaProyecto = rutaProyecto;

        CuentaGitHub? cuenta = _servicioGitHub.CuentaActual;
        TxtResumenCuenta.Text = cuenta is not null
            ? $"Cuenta conectada: {cuenta.NombreMostrado} · Carpeta: {_rutaProyecto}"
            : "Cuenta GitHub no conectada.";

        string nombreCarpeta = Path.GetFileName(_rutaProyecto.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        TxtNombreRepo.Text = NormalizarNombreRepo(nombreCarpeta);

        EjecutarEscaneo();
    }

    private static string NormalizarNombreRepo(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return "proyecto-cibao";
        var chars = nombre.Trim().Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '-').ToArray();
        string limpio = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(limpio) ? "proyecto-cibao" : limpio.ToLowerInvariant();
    }

    private void EjecutarEscaneo()
    {
        var (archivos, alertas) = ServicioGit.EscanearSecretos(_rutaProyecto);
        TxtTituloArchivos.Text = $"ARCHIVOS A PUBLICAR ({archivos.Count})";
        ListaArchivosPublicar.ItemsSource = archivos.Take(200).ToList();
        ListaAlertasSecretos.ItemsSource = alertas;
        ListaAlertasSecretos.IsVisible = alertas.Count > 0;

        if (alertas.Count > 0)
        {
            TxtEstadoSecretos.Text = $"ATENCIÓN: Se detectaron {alertas.Count} posible(s) secreto(s) o archivo(s) sensible(s). Revisa antes de publicar.";
            TxtEstadoSecretos.Foreground = Brushes.OrangeRed;
        }
        else
        {
            TxtEstadoSecretos.Text = "No se detectaron claves privadas, tokens expuestos ni archivos .env sin proteger.";
            TxtEstadoSecretos.Foreground = Brushes.LightGreen;
        }
    }

    private void AlGenerarGitIgnoreAhora(object? sender, RoutedEventArgs e)
    {
        ServicioGit.GenerarGitIgnore(_rutaProyecto);
        EjecutarEscaneo();
        TxtProgreso.Text = ".gitignore generado/reforzado en el proyecto.";
    }

    private void AlReescanear(object? sender, RoutedEventArgs e) => EjecutarEscaneo();

    private async void AlConfirmarPublicacion(object? sender, RoutedEventArgs e)
    {
        if (!_servicioGitHub.EstaConectado)
        {
            TxtProgreso.Text = "Debes iniciar sesión en GitHub primero.";
            return;
        }

        string nombreRepo = TxtNombreRepo.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(nombreRepo))
        {
            TxtProgreso.Text = "Escribe un nombre válido para el repositorio.";
            return;
        }

        if (!await _servicioGit.EstaGitDisponibleAsync())
        {
            TxtProgreso.Text = "Git CLI no está disponible en el sistema. Instala Git para publicar.";
            return;
        }

        BtnPublicarConfirmar.IsEnabled = false;
        try
        {
            if (ChkGenerarGitIgnore.IsChecked == true)
            {
                TxtProgreso.Text = "1/4 Generando .gitignore seguro...";
                ServicioGit.GenerarGitIgnore(_rutaProyecto);
            }

            TxtProgreso.Text = $"2/4 Creando o enlazando repositorio '{nombreRepo}' en GitHub...";
            bool esPrivado = RadPrivado.IsChecked == true;
            var (creadoOk, msgCrear, repo, yaExistia) = await _servicioGitHub.CrearOEnlazarRepositorioAsync(
                nombreRepo,
                TxtDescripcionRepo.Text ?? string.Empty,
                esPrivado);

            if (!creadoOk || repo is null)
            {
                TxtProgreso.Text = msgCrear;
                return;
            }

            if (yaExistia)
            {
                TxtProgreso.Text = $"2/4 Enlazado a repositorio existente '{repo.NombreCompleto}'...";
            }

            TxtProgreso.Text = "3/4 Preparando Git local y creando commit...";
            EstadoRepositorioGit estado = await _servicioGit.ObtenerEstadoAsync(_rutaProyecto);
            if (!estado.EsRepositorio)
            {
                var initRes = await _servicioGit.InicializarRepositorioAsync(_rutaProyecto, "main");
                if (!initRes.Exito)
                {
                    TxtProgreso.Text = initRes.Mensaje;
                    return;
                }
            }

            var prepRes = await _servicioGit.PrepararTodoAsync(_rutaProyecto);
            if (!prepRes.Exito)
            {
                TxtProgreso.Text = $"No se pudieron preparar los archivos: {prepRes.Mensaje}";
                return;
            }

            string commitMsg = string.IsNullOrWhiteSpace(TxtCommitInicial.Text)
                ? "Publicación desde Xynex Code Editor 9.0"
                : TxtCommitInicial.Text.Trim();

            CuentaGitHub? cuenta = _servicioGitHub.CuentaActual;
            string? email = !string.IsNullOrWhiteSpace(cuenta?.Email)
                ? cuenta.Email
                : (cuenta is not null ? $"{cuenta.Login}@users.noreply.github.com" : null);

            ResultadoOperacionGit commitRes = await _servicioGit.CrearCommitAsync(
                _rutaProyecto,
                commitMsg,
                cuenta?.Nombre ?? cuenta?.Login,
                email);

            if (!commitRes.Exito)
            {
                bool sinCambiosPendientes = commitRes.Salida.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase) ||
                                            commitRes.Salida.Contains("nothing added to commit", StringComparison.OrdinalIgnoreCase);
                EstadoRepositorioGit estadoTrasCommit = await _servicioGit.ObtenerEstadoAsync(_rutaProyecto);
                if (!(sinCambiosPendientes && estadoTrasCommit.Historial.Count > 0))
                {
                    TxtProgreso.Text = $"No se pudo crear el commit: {commitRes.Mensaje}";
                    return;
                }
            }

            TxtProgreso.Text = "4/4 Configurando origin y subiendo archivos a GitHub...";
            ResultadoOperacionGit remoteRes = await _servicioGit.ConfigurarRemotoOriginAsync(_rutaProyecto, repo.CloneUrl);
            if (!remoteRes.Exito)
            {
                TxtProgreso.Text = $"No se pudo configurar el remoto origin: {remoteRes.Mensaje}";
                return;
            }

            EstadoRepositorioGit estadoFinal = await _servicioGit.ObtenerEstadoAsync(_rutaProyecto);
            string rama = string.IsNullOrWhiteSpace(estadoFinal.RamaActual) || estadoFinal.RamaActual == "HEAD"
                ? "main"
                : estadoFinal.RamaActual;
            var pushRes = await _servicioGit.PushAsync(
                _rutaProyecto,
                rama,
                _servicioGitHub.ObtenerTokenTemporalParaGit(),
                fijarUpstream: true);

            if (!pushRes.Exito)
            {
                TxtProgreso.Text = $"Repositorio listo, pero falló push: {pushRes.Mensaje}";
                return;
            }

            Close(repo);
        }
        finally
        {
            BtnPublicarConfirmar.IsEnabled = true;
        }
    }

    private void AlCancelar(object? sender, RoutedEventArgs e) => Close(null);
}
