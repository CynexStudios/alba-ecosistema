using System.Diagnostics;
using Alba.IDE.Models;
using Alba.IDE.Services;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Alba.IDE.Views;

public partial class DialogoLoginGitHub : Window
{
    private readonly ServicioGitHub _servicioGitHub;
    private CancellationTokenSource? _ctsPolling;
    private string _verificationUri = "https://github.com/login/device";

    public DialogoLoginGitHub() : this(new ServicioGitHub())
    {
    }

    public DialogoLoginGitHub(ServicioGitHub servicioGitHub)
    {
        InitializeComponent();
        _servicioGitHub = servicioGitHub;
        Closing += (_, _) => _ctsPolling?.Cancel();
    }

    private void AlAbrirPaginaTokens(object? sender, RoutedEventArgs e)
    {
        AbrirNavegador("https://github.com/settings/tokens/new?scopes=repo,read:user,user:email&description=Xynex%20Code%20Editor%209.0");
    }

    private async void AlConectarConPat(object? sender, RoutedEventArgs e)
    {
        string token = TxtTokenPat.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(token))
        {
            TxtMensajeEstado.Text = "Introduce un token personal de GitHub.";
            return;
        }

        BtnConectarPat.IsEnabled = false;
        TxtMensajeEstado.Text = "Verificando credenciales con api.github.com...";

        try
        {
            var (exito, mensaje, cuenta) = await _servicioGitHub.IniciarSesionConTokenAsync(
                token,
                MetodoAutenticacionGitHub.TokenPersonal,
                guardarEnAlmacen: true);

            TxtMensajeEstado.Text = mensaje;
            if (exito && cuenta is not null)
            {
                Close(cuenta);
            }
        }
        finally
        {
            BtnConectarPat.IsEnabled = true;
        }
    }

    private async void AlIniciarDeviceFlow(object? sender, RoutedEventArgs e)
    {
        string clientId = TxtClientIdOAuth.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(clientId))
        {
            TxtMensajeEstado.Text = "Debes indicar el Client ID de tu aplicación OAuth.";
            return;
        }

        BtnIniciarDeviceFlow.IsEnabled = false;
        TxtMensajeEstado.Text = "Solicitando código de dispositivo a GitHub...";

        try
        {
            var (exito, mensaje, flujo) = await _servicioGitHub.IniciarDeviceFlowAsync(clientId);
            TxtMensajeEstado.Text = mensaje;

            if (!exito || flujo is null)
            {
                return;
            }

            _verificationUri = flujo.VerificationUri;
            TxtCodigoUsuario.Text = flujo.UserCode;
            PanelCodigoDeviceFlow.IsVisible = true;
            TxtEstadoPolling.Text = "Esperando autorización en el navegador (caduca en " + Math.Max(1, flujo.ExpiresIn / 60) + " min)...";

            AbrirNavegador(_verificationUri);

            _ctsPolling?.Cancel();
            _ctsPolling = new CancellationTokenSource();

            var authRes = await _servicioGitHub.EsperarAutorizacionDeviceFlowAsync(clientId, flujo, _ctsPolling.Token);
            TxtEstadoPolling.Text = authRes.Mensaje;
            TxtMensajeEstado.Text = authRes.Mensaje;

            if (authRes.Exito && authRes.Cuenta is not null)
            {
                Close(authRes.Cuenta);
            }
        }
        catch (OperationCanceledException)
        {
            TxtMensajeEstado.Text = "Flujo OAuth cancelado.";
        }
        finally
        {
            BtnIniciarDeviceFlow.IsEnabled = true;
        }
    }

    private async void AlCopiarCodigoDevice(object? sender, RoutedEventArgs e)
    {
        if (Clipboard is not null && !string.IsNullOrWhiteSpace(TxtCodigoUsuario.Text))
        {
            await Clipboard.SetTextAsync(TxtCodigoUsuario.Text);
            TxtMensajeEstado.Text = "Código copiado al portapapeles.";
        }
    }

    private void AlAbrirUrlDeviceFlow(object? sender, RoutedEventArgs e)
    {
        AbrirNavegador(_verificationUri);
    }

    private void AlCancelar(object? sender, RoutedEventArgs e)
    {
        _ctsPolling?.Cancel();
        Close(null);
    }

    private static void AbrirNavegador(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Ignora errores si el sistema no tiene navegador predeterminado
        }
    }
}
