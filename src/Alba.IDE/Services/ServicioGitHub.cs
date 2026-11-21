using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Alba.IDE.Models;

namespace Alba.IDE.Services;

/// <summary>
/// Cliente oficial de la API REST de GitHub y flujo OAuth Device Flow / Token Personal.
/// Nunca almacena tokens dentro de proyectos o registros sin cifrar.
/// </summary>
public sealed class ServicioGitHub : IDisposable
{
    private readonly HttpClient _http;
    private readonly AlmacenCredencialesSeguro _almacen;
    private string? _tokenActivo;

    public CuentaGitHub? CuentaActual { get; private set; }
    public bool EstaConectado => CuentaActual is not null && !string.IsNullOrWhiteSpace(_tokenActivo);

    public event Action<CuentaGitHub?>? SesionCambiada;

    public ServicioGitHub(AlmacenCredencialesSeguro? almacen = null, HttpMessageHandler? handler = null)
    {
        _almacen = almacen ?? new AlmacenCredencialesSeguro();
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.Timeout = TimeSpan.FromSeconds(20);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"Xynex-Code-Editor/{RutasUsuarioAlba.VersionActual}");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public string? ObtenerTokenTemporalParaGit() => _tokenActivo;

    public async Task<bool> RestaurarSesionGuardadaAsync()
    {
        var (token, metodo) = _almacen.LeerTokenGitHub();
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var res = await IniciarSesionConTokenAsync(token, metodo, guardarEnAlmacen: false);
        return res.Exito;
    }

    public async Task<(bool Exito, string Mensaje, CuentaGitHub? Cuenta)> IniciarSesionConTokenAsync(
        string token,
        MetodoAutenticacionGitHub metodo = MetodoAutenticacionGitHub.TokenPersonal,
        bool guardarEnAlmacen = true)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return (false, "El token de acceso no puede estar vacío.", null);
        }

        string tokenLimpio = token.Trim();

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenLimpio);
            req.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

            using HttpResponseMessage resp = await _http.SendAsync(req);
            string cuerpo = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                return (false, $"GitHub rechazó la autenticación (HTTP {(int)resp.StatusCode}). Verifica el token y sus permisos.", null);
            }

            using JsonDocument doc = JsonDocument.Parse(cuerpo);
            JsonElement root = doc.RootElement;

            string scopes = resp.Headers.TryGetValues("X-OAuth-Scopes", out IEnumerable<string>? vals)
                ? string.Join(", ", vals)
                : "Token detallado";

            var cuenta = new CuentaGitHub
            {
                Login = ObtenerString(root, "login"),
                Nombre = ObtenerString(root, "name"),
                Email = ObtenerString(root, "email"),
                AvatarUrl = ObtenerString(root, "avatar_url"),
                PerfilUrl = ObtenerString(root, "html_url"),
                RepositoriosPublicos = ObtenerInt(root, "public_repos"),
                RepositoriosPrivados = ObtenerInt(root, "total_private_repos"),
                PermisosToken = scopes,
                Metodo = metodo
            };

            _tokenActivo = tokenLimpio;
            CuentaActual = cuenta;
            if (guardarEnAlmacen)
            {
                _almacen.GuardarTokenGitHub(tokenLimpio, metodo);
            }

            SesionCambiada?.Invoke(cuenta);
            return (true, $"Conectado a GitHub como @{cuenta.Login}.", cuenta);
        }
        catch (Exception ex)
        {
            return (false, $"No se pudo conectar con GitHub: {ex.Message}", null);
        }
    }

    public async Task<(bool Exito, string Mensaje, RespuestaDeviceFlowGitHub? Flujo)> IniciarDeviceFlowAsync(string clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return (false, "Debes indicar el Client ID de tu aplicación OAuth de GitHub.", null);
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/device/code");
            req.Headers.Accept.Clear();
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId.Trim(),
                ["scope"] = "repo read:user user:email"
            });

            using HttpResponseMessage resp = await _http.SendAsync(req);
            string json = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                return (false, $"Error al iniciar OAuth Device Flow (HTTP {(int)resp.StatusCode}).", null);
            }

            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            if (root.TryGetProperty("error", out JsonElement err))
            {
                return (false, $"GitHub OAuth error: {err.GetString()}", null);
            }

            var flujo = new RespuestaDeviceFlowGitHub
            {
                DeviceCode = ObtenerString(root, "device_code"),
                UserCode = ObtenerString(root, "user_code"),
                VerificationUri = ObtenerString(root, "verification_uri", "https://github.com/login/device"),
                ExpiresIn = ObtenerInt(root, "expires_in", 900),
                Interval = Math.Max(5, ObtenerInt(root, "interval", 5))
            };

            return (true, "Código de dispositivo generado. Autoriza en el navegador.", flujo);
        }
        catch (Exception ex)
        {
            return (false, $"Error iniciando Device Flow: {ex.Message}", null);
        }
    }

    public async Task<(bool Exito, string Mensaje, CuentaGitHub? Cuenta)> EsperarAutorizacionDeviceFlowAsync(
        string clientId,
        RespuestaDeviceFlowGitHub flujo,
        CancellationToken cancellationToken)
    {
        int intervalo = Math.Max(5, flujo.Interval);
        DateTime limite = DateTime.UtcNow.AddSeconds(Math.Max(60, flujo.ExpiresIn));

        while (DateTime.UtcNow < limite && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(intervalo), cancellationToken);

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token");
            req.Headers.Accept.Clear();
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId.Trim(),
                ["device_code"] = flujo.DeviceCode,
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
            });

            using HttpResponseMessage resp = await _http.SendAsync(req, cancellationToken);
            string json = await resp.Content.ReadAsStringAsync(cancellationToken);
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("access_token", out JsonElement tokElem))
            {
                string? accessToken = tokElem.GetString();
                if (!string.IsNullOrWhiteSpace(accessToken))
                {
                    return await IniciarSesionConTokenAsync(accessToken, MetodoAutenticacionGitHub.OAuthDeviceFlow);
                }
            }

            string? err = root.TryGetProperty("error", out JsonElement errElem) ? errElem.GetString() : null;
            if (err == "slow_down")
            {
                intervalo += 5;
            }
            else if (err is "expired_token" or "access_denied")
            {
                return (false, err == "access_denied" ? "Autorización denegada por el usuario." : "El código OAuth expiró.", null);
            }
        }

        return (false, "Tiempo de espera OAuth cancelado o expirado.", null);
    }

    public async Task<(bool Exito, string Mensaje, IReadOnlyList<RepositorioGitHub> Repositorios)> ListarRepositoriosAsync()
    {
        if (!EstaConectado)
        {
            return (false, "Inicia sesión en GitHub primero.", []);
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/repos?sort=updated&per_page=100");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenActivo);
            req.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

            using HttpResponseMessage resp = await _http.SendAsync(req);
            string json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                return (false, $"No se pudieron consultar los repositorios (HTTP {(int)resp.StatusCode}).", []);
            }

            using JsonDocument doc = JsonDocument.Parse(json);
            var lista = new List<RepositorioGitHub>();
            foreach (JsonElement item in doc.RootElement.EnumerateArray())
            {
                lista.Add(ParsearRepositorio(item));
            }

            return (true, $"{lista.Count} repositorio(s) encontrados.", lista);
        }
        catch (Exception ex)
        {
            return (false, $"Error consultando repositorios: {ex.Message}", []);
        }
    }

    public async Task<(bool Exito, string Mensaje, RepositorioGitHub? Repositorio)> CrearRepositorioAsync(
        string nombre,
        string descripcion,
        bool esPrivado)
    {
        if (!EstaConectado)
        {
            return (false, "Inicia sesión en GitHub primero.", null);
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            return (false, "El nombre del repositorio es obligatorio.", null);
        }

        try
        {
            var payload = new
            {
                name = nombre.Trim(),
                description = descripcion?.Trim() ?? string.Empty,
                @private = esPrivado,
                auto_init = false
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.github.com/user/repos");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenActivo);
            req.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
            req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using HttpResponseMessage resp = await _http.SendAsync(req);
            string json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                return (false, $"GitHub no pudo crear el repositorio (HTTP {(int)resp.StatusCode}): {ExtraerMensajeError(json)}", null);
            }

            using JsonDocument doc = JsonDocument.Parse(json);
            RepositorioGitHub repo = ParsearRepositorio(doc.RootElement);
            return (true, $"Repositorio '{repo.NombreCompleto}' creado en GitHub.", repo);
        }
        catch (Exception ex)
        {
            return (false, $"Error creando repositorio en GitHub: {ex.Message}", null);
        }
    }

    public async Task<(bool Exito, string Mensaje, RepositorioGitHub? Repositorio)> ObtenerRepositorioAsync(
        string propietario,
        string nombreRepo)
    {
        if (!EstaConectado)
        {
            return (false, "Inicia sesión en GitHub primero.", null);
        }

        if (string.IsNullOrWhiteSpace(propietario) || string.IsNullOrWhiteSpace(nombreRepo))
        {
            return (false, "Propietario y nombre del repositorio son obligatorios.", null);
        }

        try
        {
            string url = $"https://api.github.com/repos/{Uri.EscapeDataString(propietario.Trim())}/{Uri.EscapeDataString(nombreRepo.Trim())}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenActivo);
            req.Headers.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

            using HttpResponseMessage resp = await _http.SendAsync(req);
            string json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                return (false, $"No se pudo obtener '{propietario}/{nombreRepo}' (HTTP {(int)resp.StatusCode}): {ExtraerMensajeError(json)}", null);
            }

            using JsonDocument doc = JsonDocument.Parse(json);
            RepositorioGitHub repo = ParsearRepositorio(doc.RootElement);
            return (true, $"Repositorio '{repo.NombreCompleto}' encontrado en GitHub.", repo);
        }
        catch (Exception ex)
        {
            return (false, $"Error consultando repositorio en GitHub: {ex.Message}", null);
        }
    }

    public async Task<(bool Exito, string Mensaje, RepositorioGitHub? Repositorio, bool YaExistia)> CrearOEnlazarRepositorioAsync(
        string nombre,
        string descripcion,
        bool esPrivado)
    {
        var (creadoOk, msgCrear, repoCreado) = await CrearRepositorioAsync(nombre, descripcion, esPrivado);
        if (creadoOk && repoCreado is not null)
        {
            return (true, msgCrear, repoCreado, false);
        }

        string? login = CuentaActual?.Login;
        if (!string.IsNullOrWhiteSpace(login) &&
            (msgCrear.Contains("422", StringComparison.Ordinal) ||
             msgCrear.Contains("already exists", StringComparison.OrdinalIgnoreCase) ||
             msgCrear.Contains("Creation failed", StringComparison.OrdinalIgnoreCase)))
        {
            var (encontradoOk, _, repoExistente) = await ObtenerRepositorioAsync(login, nombre);
            if (encontradoOk && repoExistente is not null)
            {
                return (true, $"Repositorio existente '{repoExistente.NombreCompleto}' enlazado para subir archivos.", repoExistente, true);
            }
        }

        return (false, msgCrear, null, false);
    }

    public async Task<byte[]?> DescargarAvatarAsync(string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(avatarUrl) || !Uri.TryCreate(avatarUrl, UriKind.Absolute, out Uri? uri))
        {
            return null;
        }

        try
        {
            return await _http.GetByteArrayAsync(uri);
        }
        catch
        {
            return null;
        }
    }

    public void CerrarSesion()
    {
        _tokenActivo = null;
        CuentaActual = null;
        _almacen.EliminarTokenGitHub();
        SesionCambiada?.Invoke(null);
    }

    private static RepositorioGitHub ParsearRepositorio(JsonElement item) => new()
    {
        Id = item.TryGetProperty("id", out JsonElement idEl) && idEl.TryGetInt64(out long id) ? id : 0,
        Nombre = ObtenerString(item, "name"),
        NombreCompleto = ObtenerString(item, "full_name"),
        Descripcion = ObtenerString(item, "description"),
        EsPrivado = item.TryGetProperty("private", out JsonElement privEl) && privEl.ValueKind == JsonValueKind.True,
        CloneUrl = ObtenerString(item, "clone_url"),
        HtmlUrl = ObtenerString(item, "html_url"),
        RamaPorDefecto = ObtenerString(item, "default_branch", "main"),
        LenguajePrincipal = ObtenerString(item, "language"),
        ActualizadoEnUtc = item.TryGetProperty("updated_at", out JsonElement dtEl) &&
                           DateTime.TryParse(dtEl.GetString(), out DateTime dt)
            ? dt.ToUniversalTime()
            : DateTime.UtcNow
    };

    private static string ExtraerMensajeError(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("message", out JsonElement msg))
            {
                return msg.GetString() ?? json;
            }
        }
        catch
        {
            // Ignora JSON inválido
        }
        return json;
    }

    private static string ObtenerString(JsonElement el, string prop, string def = "") =>
        el.TryGetProperty(prop, out JsonElement val) && val.ValueKind == JsonValueKind.String
            ? val.GetString() ?? def
            : def;

    private static int ObtenerInt(JsonElement el, string prop, int def = 0) =>
        el.TryGetProperty(prop, out JsonElement val) && val.TryGetInt32(out int n)
            ? n
            : def;

    public void Dispose() => _http.Dispose();
}
