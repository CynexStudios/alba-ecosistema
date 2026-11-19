using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Alba.IDE.Models;

namespace Alba.IDE.Services;

/// <summary>
/// Comprueba lanzamientos oficiales en GitHub Releases, selecciona el paquete nativo
/// para el sistema operativo actual (Windows, Arch/CachyOS, Debian/Ubuntu, Fedora, AppImage o portable),
/// descarga con verificación SHA-256 y ejecuta la actualización integrada con reinicio automático.
/// </summary>
public sealed class GestorActualizaciones
{
    private static readonly HttpClient Http = CrearCliente(TimeSpan.FromSeconds(10));
    private static readonly HttpClient HttpDescarga = CrearCliente(TimeSpan.FromMinutes(15));

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

            List<AssetActualizacion> assets = ExtraerAssets(doc.RootElement);
            string? urlSha256 = assets.FirstOrDefault(a =>
                a.Nombre.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))?.UrlDescarga;

            MetodoInstalacionActualizacion metodo = DetectarMetodoInstalacionActual();
            AssetActualizacion? assetRecomendado = SeleccionarAssetParaMetodo(assets, metodo);
            if (assetRecomendado is null && metodo != MetodoInstalacionActualizacion.ManualNavegador)
            {
                metodo = AjustarMetodoSegunAssetsDisponibles(assets, metodo);
                assetRecomendado = SeleccionarAssetParaMetodo(assets, metodo);
            }

            bool hayNueva = EsVersionMayor(tag, RutasUsuarioAlba.VersionActual);
            return new InformacionActualizacion
            {
                HayActualizacion = hayNueva,
                VersionActual = RutasUsuarioAlba.VersionActual,
                VersionDisponible = tag,
                Notas = notas,
                UrlDescarga = htmlUrl,
                AssetRecomendado = assetRecomendado,
                UrlChecksumsSha256 = urlSha256,
                MetodoInstalacion = assetRecomendado is not null ? metodo : MetodoInstalacionActualizacion.ManualNavegador,
                DescripcionEntorno = DescribirMetodoInstalacion(metodo),
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
                MensajeEstado = "No se pudo comprobar actualizaciones en este momento (modo sin conexión)."
            };
        }
    }

    public async Task<ResultadoDescargaActualizacion> DescargarYVerificarAsync(
        InformacionActualizacion info,
        IProgress<ProgresoActualizacion>? progreso = null,
        CancellationToken cancellationToken = default)
    {
        if (info.AssetRecomendado is null || string.IsNullOrWhiteSpace(info.AssetRecomendado.UrlDescarga))
        {
            return new ResultadoDescargaActualizacion
            {
                Exito = false,
                Mensaje = "No se encontró un paquete compatible en la release para descargar automáticamente."
            };
        }

        AssetActualizacion asset = info.AssetRecomendado;
        string carpetaDestino = Path.Combine(
            RutasUsuarioAlba.ObtenerDirectorioDatosUsuario(),
            "updates",
            info.VersionDisponible);
        Directory.CreateDirectory(carpetaDestino);
        string rutaDestino = Path.Combine(carpetaDestino, asset.Nombre);

        try
        {
            Dictionary<string, string>? checksums = null;
            if (!string.IsNullOrWhiteSpace(info.UrlChecksumsSha256))
            {
                progreso?.Report(new ProgresoActualizacion
                {
                    Porcentaje = 0,
                    EstadoTexto = "Obteniendo firmas SHA-256 del lanzamiento..."
                });
                checksums = await ObtenerChecksumsRemotosAsync(info.UrlChecksumsSha256, cancellationToken);
            }

            string? hashEsperado = checksums is not null && checksums.TryGetValue(asset.Nombre, out string? h)
                ? h
                : null;

            // Si el archivo ya estaba descargado y coincide su checksum (o tamaño si no hay checksum), reutilizarlo
            if (File.Exists(rutaDestino))
            {
                if (!string.IsNullOrWhiteSpace(hashEsperado))
                {
                    string hashLocal = await Task.Run(() => CalcularSha256Archivo(rutaDestino), cancellationToken);
                    if (hashLocal.Equals(hashEsperado, StringComparison.OrdinalIgnoreCase))
                    {
                        progreso?.Report(new ProgresoActualizacion
                        {
                            BytesRecibidos = new FileInfo(rutaDestino).Length,
                            TotalBytes = new FileInfo(rutaDestino).Length,
                            Porcentaje = 100,
                            EstadoTexto = "Paquete ya descargado y verificado."
                        });
                        return new ResultadoDescargaActualizacion
                        {
                            Exito = true,
                            RutaArchivo = rutaDestino,
                            Sha256Verificado = true,
                            Mensaje = $"Paquete {asset.Nombre} listo y verificado."
                        };
                    }
                }
                File.Delete(rutaDestino);
            }

            string rutaParcial = rutaDestino + ".part";
            if (File.Exists(rutaParcial))
            {
                File.Delete(rutaParcial);
            }

            using HttpResponseMessage respuesta = await HttpDescarga.GetAsync(
                asset.UrlDescarga,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            respuesta.EnsureSuccessStatusCode();

            long totalBytes = respuesta.Content.Headers.ContentLength ?? asset.TamanoBytes;
            long recibidos = 0;

            await using (Stream flujoRemoto = await respuesta.Content.ReadAsStreamAsync(cancellationToken))
            await using (FileStream flujoArchivo = new(rutaParcial, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
            {
                byte[] buffer = new byte[81920];
                int leidos;
                while ((leidos = await flujoRemoto.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await flujoArchivo.WriteAsync(buffer.AsMemory(0, leidos), cancellationToken);
                    recibidos += leidos;

                    double pct = totalBytes > 0 ? Math.Min(100.0, recibidos * 100.0 / totalBytes) : 0;
                    string mbRec = $"{recibidos / (1024.0 * 1024.0):F1} MB";
                    string mbTot = totalBytes > 0 ? $" / {totalBytes / (1024.0 * 1024.0):F1} MB" : string.Empty;
                    progreso?.Report(new ProgresoActualizacion
                    {
                        BytesRecibidos = recibidos,
                        TotalBytes = totalBytes,
                        Porcentaje = pct,
                        EstadoTexto = $"Descargando {asset.Nombre}: {mbRec}{mbTot} ({pct:F0}%)"
                    });
                }
            }

            File.Move(rutaParcial, rutaDestino, overwrite: true);

            bool verificado = false;
            if (!string.IsNullOrWhiteSpace(hashEsperado))
            {
                progreso?.Report(new ProgresoActualizacion
                {
                    BytesRecibidos = recibidos,
                    TotalBytes = totalBytes,
                    Porcentaje = 100,
                    EstadoTexto = "Verificando integridad SHA-256..."
                });
                string hashDescargado = await Task.Run(() => CalcularSha256Archivo(rutaDestino), cancellationToken);
                if (!hashDescargado.Equals(hashEsperado, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(rutaDestino);
                    return new ResultadoDescargaActualizacion
                    {
                        Exito = false,
                        Mensaje = "La verificación SHA-256 falló. El archivo descargado podría estar corrupto."
                    };
                }
                verificado = true;
            }

            return new ResultadoDescargaActualizacion
            {
                Exito = true,
                RutaArchivo = rutaDestino,
                Sha256Verificado = verificado,
                Mensaje = verificado
                    ? $"Paquete {asset.Nombre} descargado y verificado por SHA-256."
                    : $"Paquete {asset.Nombre} descargado correctamente."
            };
        }
        catch (OperationCanceledException)
        {
            return new ResultadoDescargaActualizacion
            {
                Exito = false,
                Mensaje = "Descarga cancelada por el usuario."
            };
        }
        catch (Exception ex)
        {
            return new ResultadoDescargaActualizacion
            {
                Exito = false,
                Mensaje = $"Error al descargar la actualización: {ex.Message}"
            };
        }
    }

    public (bool Exito, string Mensaje) IniciarInstalacionYReiniciar(string rutaPaquete, InformacionActualizacion info)
    {
        if (!File.Exists(rutaPaquete))
        {
            return (false, "No se encontró el archivo de actualización descargado.");
        }

        int pidActual = Environment.ProcessId;
        string? exeActual = Environment.ProcessPath;
        string baseDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string raizApp = ObtenerRaizAplicacionDesdeBaseDir(baseDir);

        try
        {
            if (OperatingSystem.IsWindows())
            {
                string scriptPs1 = Path.Combine(Path.GetTempPath(), $"xynex-updater-{Guid.NewGuid():N}.ps1");
                string exeReinicio = !string.IsNullOrWhiteSpace(exeActual) && exeActual.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? exeActual
                    : Path.Combine(raizApp, "ide", "Alba.IDE.exe");

                string contenidoPs1 = info.MetodoInstalacion == MetodoInstalacionActualizacion.WindowsPortable
                    ? GenerarScriptWindowsPortable(pidActual, rutaPaquete, raizApp, exeReinicio)
                    : GenerarScriptWindowsInstalador(pidActual, rutaPaquete, exeReinicio);

                File.WriteAllText(scriptPs1, contenidoPs1, Encoding.UTF8);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPs1}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                return (true, "Actualizador iniciado. Xynex Code Editor se cerrará y volverá a abrirse actualizado.");
            }

            if (OperatingSystem.IsLinux())
            {
                string scriptSh = Path.Combine(Path.GetTempPath(), $"xynex-updater-{Guid.NewGuid():N}.sh");
                string appImageActual = Environment.GetEnvironmentVariable("APPIMAGE") ?? string.Empty;
                string contenidoSh = GenerarScriptLinux(
                    pidActual,
                    rutaPaquete,
                    info.MetodoInstalacion,
                    raizApp,
                    appImageActual,
                    exeActual ?? "/usr/bin/xynex");

                File.WriteAllText(scriptSh, contenidoSh, new UTF8Encoding(false));
                File.SetUnixFileMode(scriptSh,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"\"{scriptSh}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                return (true, "Instalador iniciado en segundo plano. Al finalizar se relanzará Xynex Code Editor.");
            }

            return (false, "Actualización automática no soportada en esta plataforma.");
        }
        catch (Exception ex)
        {
            return (false, $"No se pudo iniciar el instalador integrado: {ex.Message}");
        }
    }

    public static MetodoInstalacionActualizacion DetectarMetodoInstalacionActual()
    {
        string baseDir = AppContext.BaseDirectory;
        bool esWindows = OperatingSystem.IsWindows();
        bool esLinux = OperatingSystem.IsLinux();
        string? appImage = Environment.GetEnvironmentVariable("APPIMAGE");

        bool tieneUninstallerWindows = esWindows &&
            (File.Exists(Path.Combine(ObtenerRaizAplicacionDesdeBaseDir(baseDir), "unins000.exe")) ||
             baseDir.Contains("Program Files", StringComparison.OrdinalIgnoreCase) ||
             baseDir.Contains("Xynex Code Editor", StringComparison.OrdinalIgnoreCase));

        bool tienePacman = esLinux && (File.Exists("/etc/arch-release") || File.Exists("/usr/bin/pacman"));
        bool tieneDpkg = esLinux && (File.Exists("/etc/debian_version") || File.Exists("/usr/bin/dpkg"));
        bool tieneRpm = esLinux && File.Exists("/usr/bin/rpm");

        return DetectarMetodoInstalacion(
            esWindows,
            esLinux,
            baseDir,
            tieneUninstallerWindows,
            appImage,
            tienePacman,
            tieneDpkg,
            tieneRpm);
    }

    public static MetodoInstalacionActualizacion DetectarMetodoInstalacion(
        bool esWindows,
        bool esLinux,
        string directorioBase,
        bool tieneDesinstaladorWindows,
        string? variableAppImage,
        bool tienePacman,
        bool tieneDpkg,
        bool tieneRpm)
    {
        if (esWindows)
        {
            return tieneDesinstaladorWindows
                ? MetodoInstalacionActualizacion.WindowsInstalador
                : MetodoInstalacionActualizacion.WindowsPortable;
        }

        if (esLinux)
        {
            if (!string.IsNullOrWhiteSpace(variableAppImage))
            {
                return MetodoInstalacionActualizacion.LinuxAppImage;
            }

            bool instalacionSistema = directorioBase.StartsWith("/opt/xynex-code-editor", StringComparison.Ordinal) ||
                                      directorioBase.StartsWith("/usr/", StringComparison.Ordinal);

            if (instalacionSistema)
            {
                if (tienePacman) return MetodoInstalacionActualizacion.LinuxArchPacman;
                if (tieneDpkg) return MetodoInstalacionActualizacion.LinuxDeb;
                if (tieneRpm) return MetodoInstalacionActualizacion.LinuxRpm;
            }

            // En Arch/CachyOS o distros Linux si no se corre desde /opt ni AppImage, actualizar portable o paquete si está disponible
            return MetodoInstalacionActualizacion.LinuxPortableTarGz;
        }

        return MetodoInstalacionActualizacion.ManualNavegador;
    }

    public static AssetActualizacion? SeleccionarAssetParaMetodo(
        IReadOnlyList<AssetActualizacion> assets,
        MetodoInstalacionActualizacion metodo)
    {
        if (assets.Count == 0) return null;

        return metodo switch
        {
            MetodoInstalacionActualizacion.WindowsInstalador =>
                assets.FirstOrDefault(a => a.Nombre.EndsWith("-win-x64-setup.exe", StringComparison.OrdinalIgnoreCase)) ??
                assets.FirstOrDefault(a => a.Nombre.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)),

            MetodoInstalacionActualizacion.WindowsPortable =>
                assets.FirstOrDefault(a => a.Nombre.EndsWith("-win-x64-portable.zip", StringComparison.OrdinalIgnoreCase)) ??
                assets.FirstOrDefault(a => a.Nombre.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                                           a.Nombre.Contains("win", StringComparison.OrdinalIgnoreCase)),

            MetodoInstalacionActualizacion.LinuxAppImage =>
                assets.FirstOrDefault(a => a.Nombre.EndsWith(".AppImage", StringComparison.OrdinalIgnoreCase)),

            MetodoInstalacionActualizacion.LinuxArchPacman =>
                assets.FirstOrDefault(a => a.Nombre.EndsWith(".pkg.tar.zst", StringComparison.OrdinalIgnoreCase)),

            MetodoInstalacionActualizacion.LinuxDeb =>
                assets.FirstOrDefault(a => a.Nombre.EndsWith(".deb", StringComparison.OrdinalIgnoreCase)),

            MetodoInstalacionActualizacion.LinuxRpm =>
                assets.FirstOrDefault(a => a.Nombre.EndsWith(".rpm", StringComparison.OrdinalIgnoreCase)),

            MetodoInstalacionActualizacion.LinuxPortableTarGz =>
                assets.FirstOrDefault(a => a.Nombre.EndsWith("-linux-x64-portable.tar.gz", StringComparison.OrdinalIgnoreCase)) ??
                assets.FirstOrDefault(a => a.Nombre.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) &&
                                           a.Nombre.Contains("linux", StringComparison.OrdinalIgnoreCase)),

            _ => null
        };
    }

    private static MetodoInstalacionActualizacion AjustarMetodoSegunAssetsDisponibles(
        IReadOnlyList<AssetActualizacion> assets,
        MetodoInstalacionActualizacion original)
    {
        if (original == MetodoInstalacionActualizacion.WindowsPortable &&
            SeleccionarAssetParaMetodo(assets, MetodoInstalacionActualizacion.WindowsInstalador) is not null)
        {
            return MetodoInstalacionActualizacion.WindowsInstalador;
        }

        if (original is MetodoInstalacionActualizacion.LinuxArchPacman
            or MetodoInstalacionActualizacion.LinuxDeb
            or MetodoInstalacionActualizacion.LinuxRpm
            or MetodoInstalacionActualizacion.LinuxAppImage)
        {
            if (SeleccionarAssetParaMetodo(assets, MetodoInstalacionActualizacion.LinuxPortableTarGz) is not null)
            {
                return MetodoInstalacionActualizacion.LinuxPortableTarGz;
            }
        }

        return MetodoInstalacionActualizacion.ManualNavegador;
    }

    public static string DescribirMetodoInstalacion(MetodoInstalacionActualizacion metodo) => metodo switch
    {
        MetodoInstalacionActualizacion.WindowsInstalador => "Windows x64 (Instalador silencioso .exe)",
        MetodoInstalacionActualizacion.WindowsPortable => "Windows x64 (Actualización portable .zip)",
        MetodoInstalacionActualizacion.LinuxAppImage => "Linux AppImage (Reemplazo directo en sitio)",
        MetodoInstalacionActualizacion.LinuxArchPacman => "Arch Linux / CachyOS (Paquete nativo pacman .pkg.tar.zst)",
        MetodoInstalacionActualizacion.LinuxDeb => "Debian / Ubuntu / Mint (Paquete .deb)",
        MetodoInstalacionActualizacion.LinuxRpm => "Fedora / openSUSE (Paquete .rpm)",
        MetodoInstalacionActualizacion.LinuxPortableTarGz => "Linux x64 (Archivo portable .tar.gz)",
        _ => "Descarga manual desde GitHub Releases"
    };

    public static Dictionary<string, string> ParsearChecksumsSha256(string contenido)
    {
        var mapa = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using StringReader lector = new(contenido);
        string? linea;
        while ((linea = lector.ReadLine()) is not null)
        {
            string limpia = linea.Trim();
            if (string.IsNullOrWhiteSpace(limpia)) continue;

            int idxEspacio = limpia.IndexOfAny([' ', '\t']);
            if (idxEspacio <= 0) continue;

            string hash = limpia[..idxEspacio].Trim().ToLowerInvariant();
            string nombre = limpia[(idxEspacio + 1)..].Trim().TrimStart('*');
            nombre = Path.GetFileName(nombre);
            if (hash.Length == 64 && !string.IsNullOrWhiteSpace(nombre))
            {
                mapa[nombre] = hash;
            }
        }
        return mapa;
    }

    public static string CalcularSha256Archivo(string rutaArchivo)
    {
        using FileStream fs = File.OpenRead(rutaArchivo);
        byte[] hash = SHA256.HashData(fs);
        return Convert.ToHexString(hash).ToLowerInvariant();
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

    private static List<AssetActualizacion> ExtraerAssets(JsonElement raiz)
    {
        var lista = new List<AssetActualizacion>();
        if (!raiz.TryGetProperty("assets", out var assetsEl) || assetsEl.ValueKind != JsonValueKind.Array)
        {
            return lista;
        }

        foreach (JsonElement item in assetsEl.EnumerateArray())
        {
            string nombre = item.TryGetProperty("name", out var n) ? (n.GetString() ?? string.Empty) : string.Empty;
            string url = item.TryGetProperty("browser_download_url", out var u) ? (u.GetString() ?? string.Empty) : string.Empty;
            long size = item.TryGetProperty("size", out var s) && s.TryGetInt64(out long bytes) ? bytes : 0;

            if (!string.IsNullOrWhiteSpace(nombre) && !string.IsNullOrWhiteSpace(url))
            {
                lista.Add(new AssetActualizacion
                {
                    Nombre = nombre,
                    UrlDescarga = url,
                    TamanoBytes = size
                });
            }
        }
        return lista;
    }

    private static async Task<Dictionary<string, string>?> ObtenerChecksumsRemotosAsync(
        string urlSha256,
        CancellationToken cancellationToken)
    {
        try
        {
            string texto = await Http.GetStringAsync(urlSha256, cancellationToken);
            return ParsearChecksumsSha256(texto);
        }
        catch
        {
            return null;
        }
    }

    private static string ObtenerRaizAplicacionDesdeBaseDir(string baseDir)
    {
        string limpio = baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string nombreCarpeta = Path.GetFileName(limpio);
        if (nombreCarpeta.Equals("ide", StringComparison.OrdinalIgnoreCase))
        {
            return Directory.GetParent(limpio)?.FullName ?? limpio;
        }
        return limpio;
    }

    private static string GenerarScriptWindowsInstalador(int pid, string setupExe, string exeReinicio)
    {
        string sExe = EscaparPs(setupExe);
        string sLaunch = EscaparPs(exeReinicio);
        return $$"""
$ErrorActionPreference = 'SilentlyContinue'
$targetPid = {{pid}}
for ($i = 0; $i -lt 60; $i++) {
    if (-not (Get-Process -Id $targetPid -ErrorAction SilentlyContinue)) { break }
    Start-Sleep -Milliseconds 250
}
$proc = Start-Process -FilePath '{{sExe}}' -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CLOSEAPPLICATIONS' -Wait -PassThru
if (Test-Path '{{sLaunch}}') {
    Start-Process -FilePath '{{sLaunch}}'
}
Remove-Item -LiteralPath $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue
""";
    }

    private static string GenerarScriptWindowsPortable(int pid, string zipPath, string raizApp, string exeReinicio)
    {
        string sZip = EscaparPs(zipPath);
        string sRaiz = EscaparPs(raizApp);
        string sLaunch = EscaparPs(exeReinicio);
        return $$"""
$ErrorActionPreference = 'Stop'
$targetPid = {{pid}}
for ($i = 0; $i -lt 60; $i++) {
    if (-not (Get-Process -Id $targetPid -ErrorAction SilentlyContinue)) { break }
    Start-Sleep -Milliseconds 250
}
Expand-Archive -LiteralPath '{{sZip}}' -DestinationPath '{{sRaiz}}' -Force
if (Test-Path '{{sLaunch}}') {
    Start-Process -FilePath '{{sLaunch}}'
}
Remove-Item -LiteralPath $MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue
""";
    }

    private static string GenerarScriptLinux(
        int pid,
        string rutaPaquete,
        MetodoInstalacionActualizacion metodo,
        string raizApp,
        string appImageActual,
        string exeActual)
    {
        string qPkg = EscaparSh(rutaPaquete);
        string qRaiz = EscaparSh(raizApp);
        string qAppImg = EscaparSh(appImageActual);
        string qExe = EscaparSh(exeActual);

        string bloqueInstalacion = metodo switch
        {
            MetodoInstalacionActualizacion.LinuxAppImage => $$"""
chmod +x "{{qPkg}}"
mv -f "{{qPkg}}" "{{qAppImg}}"
chmod +x "{{qAppImg}}"
nohup "{{qAppImg}}" >/dev/null 2>&1 &
""",
            MetodoInstalacionActualizacion.LinuxArchPacman => $$"""
if command -v pkexec >/dev/null 2>&1; then
  pkexec pacman -U --noconfirm "{{qPkg}}"
else
  for term in konsole gnome-terminal kitty alacritty xfce4-terminal xterm; do
    if command -v "$term" >/dev/null 2>&1; then
      "$term" -e sh -c 'sudo pacman -U --noconfirm "'"{{qPkg}}"'"; echo "Presiona Enter..."; read _'
      break
    fi
  done
fi
if [ -x /usr/bin/xynex ]; then
  nohup /usr/bin/xynex >/dev/null 2>&1 &
else
  nohup "{{qExe}}" >/dev/null 2>&1 &
fi
""",
            MetodoInstalacionActualizacion.LinuxDeb => $$"""
if command -v pkexec >/dev/null 2>&1; then
  pkexec apt-get install -y "{{qPkg}}" || pkexec dpkg -i "{{qPkg}}"
fi
if [ -x /usr/bin/xynex ]; then
  nohup /usr/bin/xynex >/dev/null 2>&1 &
else
  nohup "{{qExe}}" >/dev/null 2>&1 &
fi
""",
            MetodoInstalacionActualizacion.LinuxRpm => $$"""
if command -v pkexec >/dev/null 2>&1; then
  pkexec dnf install -y "{{qPkg}}" || pkexec rpm -Uvh "{{qPkg}}"
fi
if [ -x /usr/bin/xynex ]; then
  nohup /usr/bin/xynex >/dev/null 2>&1 &
else
  nohup "{{qExe}}" >/dev/null 2>&1 &
fi
""",
            _ => $$"""
tar -xzf "{{qPkg}}" -C "{{qRaiz}}"
[ -f "{{qRaiz}}/ide/Alba.IDE" ] && chmod +x "{{qRaiz}}/ide/Alba.IDE"
[ -f "{{qRaiz}}/cli/alba" ] && chmod +x "{{qRaiz}}/cli/alba"
if [ -x "{{qRaiz}}/ide/Alba.IDE" ]; then
  nohup "{{qRaiz}}/ide/Alba.IDE" >/dev/null 2>&1 &
else
  nohup "{{qExe}}" >/dev/null 2>&1 &
fi
"""
        };

        return $$"""
#!/usr/bin/env bash
set +e
TARGET_PID={{pid}}
for _ in $(seq 1 60); do
  if ! kill -0 "$TARGET_PID" 2>/dev/null; then
    break
  fi
  sleep 0.25
done
{{bloqueInstalacion}}
rm -f "$0"
""";
    }

    private static string EscaparPs(string valor) => valor.Replace("'", "''");
    private static string EscaparSh(string valor) => valor.Replace("\"", "\\\"");

    private static string LimpiarSemVer(string v)
    {
        int guion = v.IndexOf('-');
        return guion >= 0 ? v[..guion] : v;
    }

    private static HttpClient CrearCliente(TimeSpan timeout)
    {
        var client = new HttpClient { Timeout = timeout };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("XynexCodeEditor", RutasUsuarioAlba.VersionActual));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }
}
