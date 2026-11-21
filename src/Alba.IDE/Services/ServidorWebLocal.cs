using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Alba.IDE.Models;

namespace Alba.IDE.Services;

/// <summary>
/// Servidor HTTP local de Live Preview para proyectos HTML/CSS/JS.
/// Funciona en 127.0.0.1 en Windows, Linux y macOS sin privilegios especiales,
/// con recarga automática en vivo y puente de captura para consola/errores del navegador.
/// </summary>
public sealed class ServidorWebLocal : IDisposable
{
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;
    private string _carpetaRaiz = string.Empty;
    private long _versionCambio = 1;

    public int Puerto { get; private set; }
    public bool EstaActivo => _listener is not null && Puerto > 0;
    public string CarpetaRaiz => _carpetaRaiz;

    public event Action<MensajeConsolaWeb>? LogRecibido;

    public string IniciarOActualizarRaiz(string carpetaRaiz)
    {
        if (string.IsNullOrWhiteSpace(carpetaRaiz) || !Directory.Exists(carpetaRaiz))
        {
            throw new DirectoryNotFoundException("La carpeta raíz del proyecto web no existe.");
        }

        _carpetaRaiz = Path.GetFullPath(carpetaRaiz);
        if (!EstaActivo)
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Puerto = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _cts = new CancellationTokenSource();
            _ = Task.Run(() => BucleAceptacionAsync(_cts.Token));
        }

        Interlocked.Increment(ref _versionCambio);
        return $"http://127.0.0.1:{Puerto}/";
    }

    public string ConstruirUrlParaArchivo(string rutaArchivo)
    {
        string rutaFull = Path.GetFullPath(rutaArchivo);
        string carpeta = Path.GetDirectoryName(rutaFull) ?? Directory.GetCurrentDirectory();
        if (string.IsNullOrWhiteSpace(_carpetaRaiz) ||
            !rutaFull.StartsWith(_carpetaRaiz, StringComparison.OrdinalIgnoreCase))
        {
            IniciarOActualizarRaiz(carpeta);
        }

        string rel = Path.GetRelativePath(_carpetaRaiz, rutaFull).Replace('\\', '/').TrimStart('/');
        return $"http://127.0.0.1:{Puerto}/{Uri.EscapeDataString(rel).Replace("%2F", "/")}";
    }

    public void NotificarCambioArchivo() => Interlocked.Increment(ref _versionCambio);

    private async Task BucleAceptacionAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener is not null)
        {
            try
            {
                TcpClient cliente = await _listener.AcceptTcpClientAsync(token);
                _ = Task.Run(() => AtenderClienteAsync(cliente, token), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                if (token.IsCancellationRequested) break;
            }
        }
    }

    private async Task AtenderClienteAsync(TcpClient cliente, CancellationToken token)
    {
        using (cliente)
        await using (NetworkStream stream = cliente.GetStream())
        {
            try
            {
                var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                string? lineaSolicitud = await reader.ReadLineAsync(token);
                if (string.IsNullOrWhiteSpace(lineaSolicitud)) return;

                string[] partes = lineaSolicitud.Split(' ');
                if (partes.Length < 2) return;

                string metodo = partes[0].ToUpperInvariant();
                string urlObjetivo = partes[1];
                int contentLength = 0;

                while (true)
                {
                    string? header = await reader.ReadLineAsync(token);
                    if (string.IsNullOrEmpty(header)) break;
                    if (header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase) &&
                        int.TryParse(header["Content-Length:".Length..].Trim(), out int len))
                    {
                        contentLength = len;
                    }
                }

                string pathPuro = urlObjetivo.Split('?')[0];

                if (pathPuro == "/__xynex_poll")
                {
                    string ver = Interlocked.Read(ref _versionCambio).ToString();
                    await EnviarRespuestaAsync(stream, "200 OK", "text/plain; charset=utf-8", Encoding.UTF8.GetBytes(ver));
                    return;
                }

                if (pathPuro == "/__xynex_log" && metodo == "POST")
                {
                    char[] buf = new char[Math.Clamp(contentLength, 0, 16_384)];
                    int leidos = contentLength > 0 ? await reader.ReadBlockAsync(buf, 0, buf.Length) : 0;
                    ProcesarLogBrowser(new string(buf, 0, leidos));
                    await EnviarRespuestaAsync(stream, "204 No Content", "text/plain", []);
                    return;
                }

                string rel = Uri.UnescapeDataString(pathPuro.TrimStart('/'));
                if (string.IsNullOrWhiteSpace(rel))
                {
                    rel = "index.html";
                }

                string rutaDestino = Path.GetFullPath(Path.Combine(_carpetaRaiz, rel));
                string raizConSep = _carpetaRaiz.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (!rutaDestino.Equals(_carpetaRaiz, StringComparison.OrdinalIgnoreCase) &&
                    !rutaDestino.StartsWith(raizConSep, StringComparison.OrdinalIgnoreCase))
                {
                    await EnviarRespuestaAsync(stream, "403 Forbidden", "text/plain; charset=utf-8", "Acceso denegado"u8.ToArray());
                    return;
                }

                if (!File.Exists(rutaDestino))
                {
                    LogRecibido?.Invoke(new MensajeConsolaWeb
                    {
                        Nivel = "error",
                        Origen = "HTTP 404",
                        Mensaje = $"Archivo no encontrado: /{rel}"
                    });
                    await EnviarRespuestaAsync(stream, "404 Not Found", "text/plain; charset=utf-8", Encoding.UTF8.GetBytes($"404 - No encontrado: {rel}"));
                    return;
                }

                string ext = Path.GetExtension(rutaDestino).ToLowerInvariant();
                string mime = ObtenerMimeType(ext);
                byte[] contenido = await File.ReadAllBytesAsync(rutaDestino, token);

                if (ext is ".html" or ".htm")
                {
                    string html = Encoding.UTF8.GetString(contenido);
                    html = InyectarBridgeLiveReload(html, Interlocked.Read(ref _versionCambio));
                    contenido = Encoding.UTF8.GetBytes(html);
                }

                await EnviarRespuestaAsync(stream, "200 OK", mime, contenido);
            }
            catch
            {
                // Ignora desconexiones abruptas del navegador
            }
        }
    }

    private void ProcesarLogBrowser(string jsonBody)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(jsonBody);
            string nivel = doc.RootElement.TryGetProperty("level", out JsonElement lv) ? lv.GetString() ?? "log" : "log";
            string msg = doc.RootElement.TryGetProperty("message", out JsonElement ms) ? ms.GetString() ?? string.Empty : string.Empty;
            if (!string.IsNullOrWhiteSpace(msg))
            {
                LogRecibido?.Invoke(new MensajeConsolaWeb
                {
                    Nivel = nivel,
                    Origen = "Navegador",
                    Mensaje = msg
                });
            }
        }
        catch
        {
            // Ignora payloads malformados
        }
    }

    private static string InyectarBridgeLiveReload(string html, long versionActual)
    {
        string script = $$"""
            <script>
            (function(){
              let v = "{{versionActual}}";
              function enviar(level, args){
                try {
                  navigator.sendBeacon ? navigator.sendBeacon('/__xynex_log', JSON.stringify({ level, message: Array.from(args).map(String).join(' ') }))
                  : fetch('/__xynex_log', { method:'POST', body: JSON.stringify({ level, message: Array.from(args).map(String).join(' ') }) });
                } catch {}
              }
              const origErr = console.error, origWarn = console.warn;
              console.error = function(){ enviar('error', arguments); return origErr.apply(console, arguments); };
              console.warn = function(){ enviar('warn', arguments); return origWarn.apply(console, arguments); };
              window.addEventListener('error', e => enviar('error', [e.message + (e.filename ? ' (' + e.filename + ':' + e.lineno + ')' : '')]));
              window.addEventListener('unhandledrejection', e => enviar('error', ['Promesa rechazada: ' + e.reason]));
              setInterval(async () => {
                try {
                  const r = await fetch('/__xynex_poll', { cache: 'no-store' });
                  const nv = await r.text();
                  if (nv && nv !== v) location.reload();
                } catch {}
              }, 1000);
            })();
            </script>
            """;

        int idx = html.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? html.Insert(idx, script) : html + script;
    }

    private static async Task EnviarRespuestaAsync(NetworkStream stream, string estado, string contentType, byte[] cuerpo)
    {
        string headers = $"HTTP/1.1 {estado}\r\nContent-Type: {contentType}\r\nContent-Length: {cuerpo.Length}\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n";
        byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
        await stream.WriteAsync(headerBytes);
        if (cuerpo.Length > 0)
        {
            await stream.WriteAsync(cuerpo);
        }
        await stream.FlushAsync();
    }

    private static string ObtenerMimeType(string ext) => ext switch
    {
        ".html" or ".htm" => "text/html; charset=utf-8",
        ".css" => "text/css; charset=utf-8",
        ".js" or ".mjs" => "application/javascript; charset=utf-8",
        ".json" => "application/json; charset=utf-8",
        ".svg" => "image/svg+xml",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        ".ico" => "image/x-icon",
        _ => "application/octet-stream"
    };

    public void Detener()
    {
        try { _cts?.Cancel(); } catch { }
        try { _listener?.Stop(); } catch { }
        _listener = null;
        Puerto = 0;
    }

    public void Dispose()
    {
        Detener();
        _cts?.Dispose();
    }
}
