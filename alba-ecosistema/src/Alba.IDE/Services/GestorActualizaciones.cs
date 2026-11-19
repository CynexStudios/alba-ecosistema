using System.Net.Http.Headers;
using System.Text.Json;
using Alba.IDE.Models;

namespace Alba.IDE.Services;

/// <summary>
/// Comprueba de forma transparente las versiones publicadas en GitHub Releases
/// sin realizar descargas ni instalaciones silenciosas.
/// </summary>
public sealed class GestorActualizaciones
{
    private static readonly HttpClient Http = CrearCliente();

    public async Task<InformacionActualizacion> ComprobarAsync(CancellationToken cancellationToken = default)
    {
        string urlReleases = $"https://github.com/{RutasUsuarioAlba.RepositorioGitHub}/releases";
        string urlApi = $"https://api.github.com/repos/{RutasUsuarioAlba.RepositorioGitHub}/releases/latest";

        try
        {
            using HttpResponseMessage respuesta = await Http.GetAsync(urlApi, cancellationToken);
            if (!respuesta.IsSuccessStatusCode)
            {
                return new InformacionActualizacion
                {
                    HayActualizacion = false,
                    VersionActual = RutasUsuarioAlba.VersionActual,
                    VersionDisponible = RutasUsuarioAlba.VersionActual,
                    UrlDescarga = urlReleases,
                    MensajeEstado = $"Xynex Code Editor v{RutasUsuarioAlba.VersionActual} (sin lanzamientos más recientes disponibles en línea)."
                };
            }

            using Stream stream = await respuesta.Content.ReadAsStreamAsync(cancellationToken);
            using JsonDocument doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            string tag = doc.RootElement.TryGetProperty("tag_name", out var tagEl)
                ? (tagEl.GetString() ?? RutasUsuarioAlba.VersionActual).TrimStart('v', 'V')
                : RutasUsuarioAlba.VersionActual;

            string notas = doc.RootElement.TryGetProperty("body", out var bodyEl)
                ? (bodyEl.GetString() ?? string.Empty)
                : string.Empty;

            string htmlUrl = doc.RootElement.TryGetProperty("html_url", out var urlEl)
                ? (urlEl.GetString() ?? urlReleases)
                : urlReleases;

            bool hayNueva = EsVersionMayor(tag, RutasUsuarioAlba.VersionActual);
            return new InformacionActualizacion
            {
                HayActualizacion = hayNueva,
                VersionActual = RutasUsuarioAlba.VersionActual,
                VersionDisponible = tag,
                Notas = notas,
                UrlDescarga = htmlUrl,
                MensajeEstado = hayNueva
                    ? $"Nueva versión disponible: v{tag} (actual: v{RutasUsuarioAlba.VersionActual})."
                    : $"Estás usando la versión más reciente (v{RutasUsuarioAlba.VersionActual})."
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new InformacionActualizacion
            {
                HayActualizacion = false,
                VersionActual = RutasUsuarioAlba.VersionActual,
                VersionDisponible = RutasUsuarioAlba.VersionActual,
                UrlDescarga = urlReleases,
                MensajeEstado = $"No se pudo comprobar actualizaciones en este momento (modo sin conexión)."
            };
        }
    }

    public static bool EsVersionMayor(string candidata, string actual)
    {
        if (Version.TryParse(LimpiarSemVer(candidata), out var vCand) &&
            Version.TryParse(LimpiarSemVer(actual), out var vAct))
        {
            return vCand > vAct;
        }
        return false;
    }

    private static string LimpiarSemVer(string v)
    {
        int guion = v.IndexOf('-');
        return guion >= 0 ? v[..guion] : v;
    }

    private static HttpClient CrearCliente()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("XynexCodeEditor", RutasUsuarioAlba.VersionActual));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}
