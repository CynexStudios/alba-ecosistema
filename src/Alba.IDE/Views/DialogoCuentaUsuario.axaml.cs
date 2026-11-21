using Alba.IDE.Models;
using Alba.IDE.Services;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Alba.IDE.Views;

public partial class DialogoCuentaUsuario : Window
{
    public sealed record ElementoPerfilIAVista(
        int Id,
        string NombrePerfil,
        string EtiquetaModelo,
        string TextoEstado);

    private sealed record OpcionProveedorItem(ProveedorIAUsuario Proveedor, string Titulo)
    {
        public override string ToString() => Titulo;
    }

    private readonly ServicioBaseDatosUsuarios _db;

    public CuentaUsuario? CuentaActual { get; private set; }
    public bool HuboCambiosIA { get; private set; }

    public DialogoCuentaUsuario()
    {
        InitializeComponent();
        _db = new ServicioBaseDatosUsuarios();
        InicializarProveedores();
        RefrescarEstado();
    }

    public DialogoCuentaUsuario(ServicioBaseDatosUsuarios db, bool abrirEnTabIA = false)
    {
        InitializeComponent();
        _db = db ?? new ServicioBaseDatosUsuarios();
        InicializarProveedores();
        RefrescarEstado();
        if (abrirEnTabIA)
        {
            TabsCuenta.SelectedIndex = 2;
        }
    }

    private void InicializarProveedores()
    {
        CmbProveedorIA.ItemsSource = new[]
        {
            new OpcionProveedorItem(ProveedorIAUsuario.OllamaLocal, "Ollama / LM Studio (IA Local Gratuita)"),
            new OpcionProveedorItem(ProveedorIAUsuario.GoogleGemini, "Google Gemini (Tu propia API Key)"),
            new OpcionProveedorItem(ProveedorIAUsuario.OpenAICompatible, "OpenAI / DeepSeek / Groq / OpenRouter"),
            new OpcionProveedorItem(ProveedorIAUsuario.AnthropicClaude, "Anthropic Claude (Tu propia API Key)")
        };
        CmbProveedorIA.SelectedIndex = 0;
        AplicarPlantillaProveedor(ProveedorIAUsuario.OllamaLocal);
    }

    private void RefrescarEstado()
    {
        CuentaActual = _db.ObtenerUsuarioActivo();
        if (CuentaActual is not null)
        {
            TxtInicialesCuenta.Text = CuentaActual.Iniciales;
            TxtTituloCuenta.Text = $"{CuentaActual.NombreMostrar} (@{CuentaActual.NombreUsuario})";
            TxtSubtituloCuenta.Text = $"Sesión autenticada · {CuentaActual.Correo} · SQLite + PBKDF2-SHA512";
            BtnCerrarSesion.IsVisible = true;
        }
        else
        {
            TxtInicialesCuenta.Text = "XY";
            TxtTituloCuenta.Text = "Modo local (sin cuenta iniciada)";
            TxtSubtituloCuenta.Text = "Crea tu cuenta de Xynex o inicia sesión. Tus perfiles de IA también funcionan cifrados localmente.";
            BtnCerrarSesion.IsVisible = false;
        }

        int uid = CuentaActual?.Id ?? 0;
        IReadOnlyList<PerfilIAConectado> perfiles = _db.ListarPerfilesIA(uid);
        ListaPerfilesIA.ItemsSource = perfiles.Select(p =>
        {
            string claveEnmascarada = string.IsNullOrWhiteSpace(p.ApiKeyCifrada)
                ? "Sin API Key (Local)"
                : "API Key cifrada en bóveda";
            return new ElementoPerfilIAVista(
                p.Id,
                p.NombrePerfil,
                $"{p.EtiquetaProveedor} · Modelo: {p.Modelo} · {claveEnmascarada}",
                p.EsActivo ? "Activo ✓" : "Usar");
        }).ToList();
    }

    private void AlCambiarProveedorIA(object? sender, SelectionChangedEventArgs e)
    {
        if (CmbProveedorIA.SelectedItem is OpcionProveedorItem opcion)
        {
            AplicarPlantillaProveedor(opcion.Proveedor);
        }
    }

    private void AplicarPlantillaProveedor(ProveedorIAUsuario prov)
    {
        switch (prov)
        {
            case ProveedorIAUsuario.OllamaLocal:
                TxtNombrePerfilIA.Text = "Ollama Local";
                TxtEndpointIA.Text = "http://localhost:11434";
                TxtModeloIA.Text = "qwen2.5-coder:7b";
                break;
            case ProveedorIAUsuario.GoogleGemini:
                TxtNombrePerfilIA.Text = "Mi Google Gemini";
                TxtEndpointIA.Text = "https://generativelanguage.googleapis.com";
                TxtModeloIA.Text = "gemini-2.5-flash";
                break;
            case ProveedorIAUsuario.OpenAICompatible:
                TxtNombrePerfilIA.Text = "OpenAI / Compatible";
                TxtEndpointIA.Text = "https://api.openai.com/v1/chat/completions";
                TxtModeloIA.Text = "gpt-4o-mini";
                break;
            case ProveedorIAUsuario.AnthropicClaude:
                TxtNombrePerfilIA.Text = "Mi Claude";
                TxtEndpointIA.Text = "https://api.anthropic.com/v1/messages";
                TxtModeloIA.Text = "claude-3-5-sonnet-latest";
                break;
        }
    }

    private void AlIniciarSesion(object? sender, RoutedEventArgs e)
    {
        ResultadoOperacionCuenta res = _db.IniciarSesion(
            TxtLoginUsuario.Text ?? string.Empty,
            TxtLoginContrasena.Text ?? string.Empty);

        TxtMensajeEstado.Text = res.Mensaje;
        if (res.Exito)
        {
            TxtLoginContrasena.Text = string.Empty;
            RefrescarEstado();
            HuboCambiosIA = true;
        }
    }

    private void AlRegistrarCuenta(object? sender, RoutedEventArgs e)
    {
        string p1 = TxtRegPass1.Text ?? string.Empty;
        string p2 = TxtRegPass2.Text ?? string.Empty;
        if (!string.Equals(p1, p2, StringComparison.Ordinal))
        {
            TxtMensajeEstado.Text = "Las contraseñas no coinciden.";
            return;
        }

        ResultadoOperacionCuenta res = _db.RegistrarUsuario(
            TxtRegUsuario.Text ?? string.Empty,
            TxtRegCorreo.Text ?? string.Empty,
            TxtRegNombreMostrar.Text ?? string.Empty,
            p1);

        TxtMensajeEstado.Text = res.Mensaje;
        if (res.Exito)
        {
            TxtRegPass1.Text = string.Empty;
            TxtRegPass2.Text = string.Empty;
            RefrescarEstado();
            TabsCuenta.SelectedIndex = 2;
        }
    }

    private void AlCerrarSesion(object? sender, RoutedEventArgs e)
    {
        _db.CerrarSesion();
        TxtMensajeEstado.Text = "Sesión cerrada correctamente.";
        RefrescarEstado();
        HuboCambiosIA = true;
    }

    private void AlGuardarPerfilIA(object? sender, RoutedEventArgs e)
    {
        if (CmbProveedorIA.SelectedItem is not OpcionProveedorItem opcion)
        {
            return;
        }

        string endpoint = TxtEndpointIA.Text?.Trim() ?? string.Empty;
        string modelo = TxtModeloIA.Text?.Trim() ?? string.Empty;
        string nombre = TxtNombrePerfilIA.Text?.Trim() ?? opcion.Titulo;
        string key = TxtApiKeyIA.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(modelo))
        {
            TxtMensajeEstado.Text = "Completa el Endpoint URL y el nombre del modelo.";
            return;
        }

        if (opcion.Proveedor != ProveedorIAUsuario.OllamaLocal && string.IsNullOrWhiteSpace(key))
        {
            TxtMensajeEstado.Text = "Ingresa tu propia API Key para este proveedor en la nube.";
            return;
        }

        try
        {
            int uid = CuentaActual?.Id ?? 0;
            _db.GuardarPerfilIA(uid, opcion.Proveedor, nombre, endpoint, modelo, key, marcarActivo: true);
            TxtApiKeyIA.Text = string.Empty;
            TxtMensajeEstado.Text = $"Perfil IA '{nombre}' cifrado y activado correctamente.";
            HuboCambiosIA = true;
            RefrescarEstado();
        }
        catch (Exception ex)
        {
            TxtMensajeEstado.Text = ex.Message;
        }
    }

    private void AlActivarPerfilLista(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: int perfilId })
        {
            _db.ActivarPerfilIA(CuentaActual?.Id ?? 0, perfilId);
            HuboCambiosIA = true;
            TxtMensajeEstado.Text = "Perfil de IA activo actualizado.";
            RefrescarEstado();
        }
    }

    private void AlEliminarPerfilLista(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: int perfilId })
        {
            _db.EliminarPerfilIA(CuentaActual?.Id ?? 0, perfilId);
            HuboCambiosIA = true;
            TxtMensajeEstado.Text = "Perfil eliminado de tu bóveda.";
            RefrescarEstado();
        }
    }

    private void AlCerrar(object? sender, RoutedEventArgs e) => Close(true);
}
