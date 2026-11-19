using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Alba.IDE.Models;

namespace Alba.IDE.Services;

/// <summary>
/// Operaciones reales con Git local, detección de secretos antes de publicar,
/// generación de .gitignore y autenticación temporal en memoria para push/pull/clone.
/// </summary>
public sealed class ServicioGit
{
    private static readonly Regex[] PatronesSecretos =
    [
        new(@"ghp_[A-Za-z0-9]{20,}", RegexOptions.Compiled),
        new(@"github_pat_[A-Za-z0-9_]{20,}", RegexOptions.Compiled),
        new(@"AKIA[0-9A-Z]{16}", RegexOptions.Compiled),
        new(@"AIza[0-9A-Za-z\-_]{25,}", RegexOptions.Compiled),
        new(@"sk-[A-Za-z0-9\-_]{20,}", RegexOptions.Compiled),
        new(@"-----BEGIN\s+(?:RSA\s+|EC\s+|OPENSSH\s+)?PRIVATE\s+KEY-----", RegexOptions.Compiled),
        new(@"(?i)(api_key|secret_key|password|access_token)\s*[:=]\s*[""'][^""'\r\n]{8,}[""']", RegexOptions.Compiled)
    ];

    public async Task<bool> EstaGitDisponibleAsync()
    {
        var res = await EjecutarGitAsync(Directory.GetCurrentDirectory(), ["--version"]);
        return res.ExitCode == 0;
    }

    public async Task<EstadoRepositorioGit> ObtenerEstadoAsync(string? rutaProyecto)
    {
        if (!await EstaGitDisponibleAsync())
        {
            return new EstadoRepositorioGit { GitInstalado = false };
        }

        if (string.IsNullOrWhiteSpace(rutaProyecto) || !Directory.Exists(rutaProyecto))
        {
            return new EstadoRepositorioGit { GitInstalado = true, EsRepositorio = false };
        }

        var checkRepo = await EjecutarGitAsync(rutaProyecto, ["rev-parse", "--is-inside-work-tree"]);
        if (checkRepo.ExitCode != 0 || !checkRepo.Stdout.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return new EstadoRepositorioGit { GitInstalado = true, EsRepositorio = false };
        }

        var branchTask = EjecutarGitAsync(rutaProyecto, ["rev-parse", "--abbrev-ref", "HEAD"]);
        var branchesTask = EjecutarGitAsync(rutaProyecto, ["branch", "--format=%(refname:short)"]);
        var statusTask = EjecutarGitAsync(rutaProyecto, ["status", "--porcelain=v1"]);
        var remoteTask = EjecutarGitAsync(rutaProyecto, ["remote", "get-url", "origin"]);
        var logTask = EjecutarGitAsync(rutaProyecto, ["log", "-n", "20", "--pretty=format:%h%x1f%an%x1f%ar%x1f%s"]);

        await Task.WhenAll(branchTask, branchesTask, statusTask, remoteTask, logTask);

        string ramaActual = branchTask.Result.ExitCode == 0 && !string.IsNullOrWhiteSpace(branchTask.Result.Stdout)
            ? branchTask.Result.Stdout.Trim()
            : "main";

        List<string> ramas = branchesTask.Result.Stdout
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(r => r.Trim())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToList();

        List<ArchivoCambioGit> cambios = ParsearCambiosPorcelain(statusTask.Result.Stdout);
        List<CommitHistorialGit> historial = ParsearHistorial(logTask.Result.Stdout);
        string? remoto = remoteTask.Result.ExitCode == 0 ? remoteTask.Result.Stdout.Trim() : null;

        return new EstadoRepositorioGit
        {
            GitInstalado = true,
            EsRepositorio = true,
            RamaActual = ramaActual,
            RemotoOrigin = string.IsNullOrWhiteSpace(remoto) ? null : remoto,
            Ramas = ramas,
            Cambios = cambios,
            Historial = historial
        };
    }

    public async Task<string> ObtenerDiferenciasAsync(string rutaProyecto, ArchivoCambioGit cambio)
    {
        if (cambio.EsNuevoSinRastrear)
        {
            string full = Path.Combine(rutaProyecto, cambio.RutaRelativa);
            if (!File.Exists(full)) return string.Empty;
            string contenido = await File.ReadAllTextAsync(full, Encoding.UTF8);
            if (contenido.Length > 12_000) contenido = contenido[..12_000] + "\n... (vista previa truncada)";
            return $"[Archivo nuevo sin rastrear: {cambio.RutaRelativa}]\n\n{contenido}";
        }

        string[] args = cambio.EstaPreparado
            ? ["diff", "--cached", "--", cambio.RutaRelativa]
            : ["diff", "--", cambio.RutaRelativa];

        var res = await EjecutarGitAsync(rutaProyecto, args);
        return string.IsNullOrWhiteSpace(res.Stdout)
            ? (string.IsNullOrWhiteSpace(res.Stderr) ? "Sin diferencias de texto disponibles." : res.Stderr)
            : res.Stdout;
    }

    public async Task<ResultadoOperacionGit> InicializarRepositorioAsync(string rutaProyecto, string ramaInicial = "main")
    {
        var init = await EjecutarGitAsync(rutaProyecto, ["init"]);
        if (init.ExitCode != 0)
        {
            return new ResultadoOperacionGit { Exito = false, Mensaje = init.Stderr.Trim() };
        }

        await EjecutarGitAsync(rutaProyecto, ["branch", "-M", ramaInicial]);
        return new ResultadoOperacionGit { Exito = true, Mensaje = $"Repositorio Git inicializado en rama '{ramaInicial}'." };
    }

    public async Task<ResultadoOperacionGit> PrepararArchivoAsync(string rutaProyecto, string rutaRelativa)
    {
        var res = await EjecutarGitAsync(rutaProyecto, ["add", "--", rutaRelativa]);
        return ConstruirResultado(res, $"Preparado: {rutaRelativa}");
    }

    public async Task<ResultadoOperacionGit> PrepararTodoAsync(string rutaProyecto)
    {
        var res = await EjecutarGitAsync(rutaProyecto, ["add", "-A"]);
        return ConstruirResultado(res, "Todos los cambios fueron preparados.");
    }

    public async Task<ResultadoOperacionGit> QuitarDePreparadosAsync(string rutaProyecto, string rutaRelativa)
    {
        var res = await EjecutarGitAsync(rutaProyecto, ["reset", "HEAD", "--", rutaRelativa]);
        if (res.ExitCode != 0)
        {
            // En repositorios sin commit inicial se usa rm --cached
            res = await EjecutarGitAsync(rutaProyecto, ["rm", "--cached", "--", rutaRelativa]);
        }
        return ConstruirResultado(res, $"Quitado de preparación: {rutaRelativa}");
    }

    public async Task<ResultadoOperacionGit> CrearCommitAsync(
        string rutaProyecto,
        string mensaje,
        string? nombreFallback = null,
        string? emailFallback = null)
    {
        if (string.IsNullOrWhiteSpace(mensaje))
        {
            return new ResultadoOperacionGit { Exito = false, Mensaje = "Escribe un mensaje de commit." };
        }

        var args = new List<string>();
        var userCheck = await EjecutarGitAsync(rutaProyecto, ["config", "user.name"]);
        var emailCheck = await EjecutarGitAsync(rutaProyecto, ["config", "user.email"]);

        if (string.IsNullOrWhiteSpace(userCheck.Stdout))
        {
            args.Add("-c");
            args.Add($"user.name={(string.IsNullOrWhiteSpace(nombreFallback) ? "XynexUser" : nombreFallback)}");
        }
        if (string.IsNullOrWhiteSpace(emailCheck.Stdout))
        {
            args.Add("-c");
            args.Add($"user.email={(string.IsNullOrWhiteSpace(emailFallback) ? "usuario@users.noreply.github.com" : emailFallback)}");
        }

        args.Add("commit");
        args.Add("-m");
        args.Add(mensaje.Trim());

        var res = await EjecutarGitAsync(rutaProyecto, args);
        return ConstruirResultado(res, "Commit creado correctamente.");
    }

    public async Task<ResultadoOperacionGit> CrearRamaAsync(string rutaProyecto, string nombreRama)
    {
        if (string.IsNullOrWhiteSpace(nombreRama))
        {
            return new ResultadoOperacionGit { Exito = false, Mensaje = "El nombre de la rama es obligatorio." };
        }
        var res = await EjecutarGitAsync(rutaProyecto, ["checkout", "-b", nombreRama.Trim()]);
        return ConstruirResultado(res, $"Rama '{nombreRama.Trim()}' creada y activa.");
    }

    public async Task<ResultadoOperacionGit> CambiarRamaAsync(string rutaProyecto, string nombreRama)
    {
        if (string.IsNullOrWhiteSpace(nombreRama))
        {
            return new ResultadoOperacionGit { Exito = false, Mensaje = "Selecciona una rama." };
        }
        var res = await EjecutarGitAsync(rutaProyecto, ["checkout", nombreRama.Trim()]);
        return ConstruirResultado(res, $"Cambiado a rama '{nombreRama.Trim()}'.");
    }

    public async Task<ResultadoOperacionGit> ConfigurarRemotoOriginAsync(string rutaProyecto, string cloneUrl)
    {
        var existe = await EjecutarGitAsync(rutaProyecto, ["remote", "get-url", "origin"]);
        var args = existe.ExitCode == 0
            ? new[] { "remote", "set-url", "origin", cloneUrl }
            : new[] { "remote", "add", "origin", cloneUrl };

        var res = await EjecutarGitAsync(rutaProyecto, args);
        return ConstruirResultado(res, $"Remoto origin apuntando a {cloneUrl}");
    }

    public async Task<ResultadoOperacionGit> PushAsync(
        string rutaProyecto,
        string rama,
        string? tokenGitHub = null,
        bool fijarUpstream = true)
    {
        var args = new List<string>();
        AnadirHeaderAuthTemporal(args, tokenGitHub);
        args.Add("push");
        if (fijarUpstream) args.Add("-u");
        args.Add("origin");
        args.Add(string.IsNullOrWhiteSpace(rama) ? "HEAD" : rama);

        var res = await EjecutarGitAsync(rutaProyecto, args);
        return ConstruirResultado(res, "Cambios enviados a GitHub (push).");
    }

    public async Task<ResultadoOperacionGit> PullAsync(string rutaProyecto, string? tokenGitHub = null)
    {
        var args = new List<string>();
        AnadirHeaderAuthTemporal(args, tokenGitHub);
        args.Add("pull");

        var res = await EjecutarGitAsync(rutaProyecto, args);
        return ConstruirResultado(res, "Actualizaciones descargadas (pull).");
    }

    public async Task<(ResultadoOperacionGit Resultado, string RutaClonada)> ClonarRepositorioAsync(
        string cloneUrl,
        string carpetaPadre,
        string? tokenGitHub = null)
    {
        if (string.IsNullOrWhiteSpace(cloneUrl) || string.IsNullOrWhiteSpace(carpetaPadre))
        {
            return (new ResultadoOperacionGit { Exito = false, Mensaje = "URL y carpeta destino requeridas." }, string.Empty);
        }

        Directory.CreateDirectory(carpetaPadre);
        string nombreRepo = Path.GetFileNameWithoutExtension(cloneUrl.TrimEnd('/').Split('/').LastOrDefault() ?? "repositorio");
        string rutaDestino = Path.Combine(carpetaPadre, nombreRepo);

        var args = new List<string>();
        AnadirHeaderAuthTemporal(args, tokenGitHub);
        args.Add("clone");
        args.Add(cloneUrl.Trim());
        args.Add(rutaDestino);

        var res = await EjecutarGitAsync(carpetaPadre, args, timeoutMs: 60_000);
        return (ConstruirResultado(res, $"Repositorio clonado en {rutaDestino}"), rutaDestino);
    }

    public static (IReadOnlyList<string> ArchivosPublicables, IReadOnlyList<HallazgoSecretoGit> Alertas) EscanearSecretos(string rutaProyecto)
    {
        var archivos = new List<string>();
        var alertas = new List<HallazgoSecretoGit>();

        if (!Directory.Exists(rutaProyecto))
        {
            return (archivos, alertas);
        }

        string raizNorm = Path.GetFullPath(rutaProyecto);
        foreach (string archivo in Directory.EnumerateFiles(raizNorm, "*", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(raizNorm, archivo).Replace('\\', '/');
            if (rel.StartsWith(".git/", StringComparison.OrdinalIgnoreCase) ||
                rel.StartsWith("bin/", StringComparison.OrdinalIgnoreCase) ||
                rel.StartsWith("obj/", StringComparison.OrdinalIgnoreCase) ||
                rel.StartsWith("node_modules/", StringComparison.OrdinalIgnoreCase) ||
                rel.StartsWith("__pycache__/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            archivos.Add(rel);

            string nombre = Path.GetFileName(archivo).ToLowerInvariant();
            string ext = Path.GetExtension(archivo).ToLowerInvariant();

            if (nombre == ".env" || nombre.StartsWith(".env.", StringComparison.Ordinal) ||
                ext is ".pem" or ".key" or ".pfx" or ".p12" ||
                nombre is "id_rsa" or "id_ed25519" or "credentials.json" or "secrets.json")
            {
                alertas.Add(new HallazgoSecretoGit
                {
                    RutaRelativa = rel,
                    Motivo = "Archivo sensible o de credenciales detectado (recomendado ignorar con .gitignore)."
                });
                continue;
            }

            if (new FileInfo(archivo).Length > 512_000)
            {
                continue;
            }

            try
            {
                string[] lineas = File.ReadAllLines(archivo, Encoding.UTF8);
                for (int i = 0; i < lineas.Length; i++)
                {
                    string linea = lineas[i];
                    foreach (Regex patron in PatronesSecretos)
                    {
                        if (patron.IsMatch(linea))
                        {
                            alertas.Add(new HallazgoSecretoGit
                            {
                                RutaRelativa = rel,
                                NumeroLinea = i + 1,
                                Motivo = "Posible token, clave privada o contraseña detectada en el código."
                            });
                            break;
                        }
                    }
                }
            }
            catch
            {
                // Ignora archivos binarios o no legibles como UTF-8
            }
        }

        return (archivos, alertas);
    }

    public static string GenerarGitIgnore(string rutaProyecto)
    {
        string ruta = Path.Combine(rutaProyecto, ".gitignore");
        const string plantilla = """
            # Generado por Xynex Code Editor 5.0
            .env
            .env.*
            *.pem
            *.key
            *.pfx
            *.p12
            secrets.json
            credentials.json

            # Compilación y dependencias
            bin/
            obj/
            build/
            __pycache__/
            *.pyc
            .venv/
            node_modules/
            .vs/
            .idea/
            """;

        if (!File.Exists(ruta))
        {
            File.WriteAllText(ruta, plantilla + Environment.NewLine, Encoding.UTF8);
            return ruta;
        }

        string actual = File.ReadAllText(ruta, Encoding.UTF8);
        if (!actual.Contains(".env", StringComparison.Ordinal))
        {
            File.AppendAllText(ruta, Environment.NewLine + ".env" + Environment.NewLine + ".env.*" + Environment.NewLine, Encoding.UTF8);
        }
        return ruta;
    }

    private static void AnadirHeaderAuthTemporal(List<string> args, string? tokenGitHub)
    {
        if (string.IsNullOrWhiteSpace(tokenGitHub))
        {
            return;
        }

        string credenciales = Convert.ToBase64String(Encoding.UTF8.GetBytes($"x-access-token:{tokenGitHub.Trim()}"));
        args.Add("-c");
        args.Add($"http.https://github.com/.extraheader=AUTHORIZATION: basic {credenciales}");
    }

    private static List<ArchivoCambioGit> ParsearCambiosPorcelain(string salida)
    {
        var lista = new List<ArchivoCambioGit>();
        foreach (string linea in salida.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (linea.Length < 4) continue;
            char x = linea[0];
            char y = linea[1];
            string ruta = linea[3..].Trim().Trim('"');

            if (x == '?' && y == '?')
            {
                lista.Add(new ArchivoCambioGit
                {
                    RutaRelativa = ruta,
                    CodigoEstado = "?",
                    EstaPreparado = false,
                    EsNuevoSinRastrear = true
                });
                continue;
            }

            if (x != ' ')
            {
                lista.Add(new ArchivoCambioGit
                {
                    RutaRelativa = ruta,
                    CodigoEstado = x.ToString(),
                    EstaPreparado = true
                });
            }
            if (y != ' ')
            {
                lista.Add(new ArchivoCambioGit
                {
                    RutaRelativa = ruta,
                    CodigoEstado = y.ToString(),
                    EstaPreparado = false
                });
            }
        }
        return lista;
    }

    private static List<CommitHistorialGit> ParsearHistorial(string salida)
    {
        var lista = new List<CommitHistorialGit>();
        foreach (string linea in salida.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string[] partes = linea.Split('\x1f');
            if (partes.Length < 4) continue;
            lista.Add(new CommitHistorialGit
            {
                HashCorto = partes[0],
                Autor = partes[1],
                FechaRelativa = partes[2],
                Mensaje = partes[3]
            });
        }
        return lista;
    }

    private static ResultadoOperacionGit ConstruirResultado((int ExitCode, string Stdout, string Stderr) res, string mensajeExito)
    {
        bool ok = res.ExitCode == 0;
        string salida = string.Join(Environment.NewLine, new[] { res.Stdout.Trim(), res.Stderr.Trim() }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return new ResultadoOperacionGit
        {
            Exito = ok,
            Mensaje = ok ? mensajeExito : (string.IsNullOrWhiteSpace(salida) ? "La operación Git finalizó con error." : salida),
            Salida = salida
        };
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> EjecutarGitAsync(
        string workingDirectory,
        IEnumerable<string> argumentos,
        int timeoutMs = 20_000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = Directory.Exists(workingDirectory) ? workingDirectory : Directory.GetCurrentDirectory(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
        psi.Environment["LC_ALL"] = "C.UTF-8";

        foreach (string arg in argumentos)
        {
            psi.ArgumentList.Add(arg);
        }

        try
        {
            using var proceso = new Process { StartInfo = psi };
            if (!proceso.Start())
            {
                return (-1, string.Empty, "No se pudo iniciar git.");
            }

            using var cts = new CancellationTokenSource(timeoutMs);
            Task<string> outTask = proceso.StandardOutput.ReadToEndAsync(cts.Token);
            Task<string> errTask = proceso.StandardError.ReadToEndAsync(cts.Token);
            await proceso.WaitForExitAsync(cts.Token);

            return (proceso.ExitCode, await outTask, await errTask);
        }
        catch (Exception ex)
        {
            return (-1, string.Empty, ex.Message);
        }
    }
}
