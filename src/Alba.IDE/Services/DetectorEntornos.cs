using System.Diagnostics;
using Alba.IDE.Models;

namespace Alba.IDE.Services;

/// <summary>
/// Detecta de forma real los SDK, compiladores, intérpretes y herramientas instalados en el equipo del usuario
/// para Cibao 9.0, HTML/CSS, JavaScript (Node.js), TypeScript, Python, C# y C++.
/// </summary>
public sealed class DetectorEntornos
{
    private IReadOnlyList<EstadoEntornoLenguaje>? _cacheLenguajes;
    private readonly Dictionary<string, (bool Disponible, string Version)> _cacheComandos = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<EstadoEntornoLenguaje> ObtenerUltimoEstadoSinBloquear()
    {
        if (_cacheLenguajes is not null)
        {
            return _cacheLenguajes;
        }

        return
        [
            CrearEstadoCibao(),
            CrearEstadoHtml(),
            new EstadoEntornoLenguaje
            {
                Lenguaje = LenguajeSoportado.Python,
                Nombre = "Python",
                ExtensionPrincipal = ".py",
                Disponible = false,
                RequiereSdkExterno = true,
                VersionDetectada = string.Empty,
                ComandoEjecutable = "python"
            },
            new EstadoEntornoLenguaje
            {
                Lenguaje = LenguajeSoportado.JavaScript,
                Nombre = "JavaScript (Node.js)",
                ExtensionPrincipal = ".js",
                Disponible = false,
                RequiereSdkExterno = true,
                VersionDetectada = string.Empty,
                ComandoEjecutable = "node"
            },
            new EstadoEntornoLenguaje
            {
                Lenguaje = LenguajeSoportado.TypeScript,
                Nombre = "TypeScript",
                ExtensionPrincipal = ".ts",
                Disponible = false,
                RequiereSdkExterno = true,
                VersionDetectada = string.Empty,
                ComandoEjecutable = "tsc"
            },
            new EstadoEntornoLenguaje
            {
                Lenguaje = LenguajeSoportado.CSharp,
                Nombre = "C# (.NET)",
                ExtensionPrincipal = ".cs",
                Disponible = false,
                RequiereSdkExterno = true,
                VersionDetectada = string.Empty,
                ComandoEjecutable = "dotnet"
            },
            new EstadoEntornoLenguaje
            {
                Lenguaje = LenguajeSoportado.Cpp,
                Nombre = "C++",
                ExtensionPrincipal = ".cpp",
                Disponible = false,
                RequiereSdkExterno = true,
                VersionDetectada = string.Empty,
                ComandoEjecutable = "g++"
            }
        ];
    }

    public async Task<IReadOnlyList<EstadoEntornoLenguaje>> DetectarTodosAsync(bool forzarRecarga = false)
    {
        if (_cacheLenguajes is not null && !forzarRecarga)
        {
            return _cacheLenguajes;
        }

        Task<(bool Ok, string Ver, string Cmd)> pythonTask = DetectarPrimeroAsync(
            forzarRecarga,
            OperatingSystem.IsWindows()
                ? [("python", "--version"), ("python3", "--version"), ("py", "--version")]
                : [("python3", "--version"), ("python", "--version")]);

        Task<(bool Ok, string Ver, string Cmd)> nodeTask = DetectarPrimeroAsync(
            forzarRecarga,
            [("node", "--version")]);

        Task<(bool Ok, string Ver, string Cmd)> tsTask = DetectarPrimeroAsync(
            forzarRecarga,
            [("tsc", "--version"), ("ts-node", "--version")]);

        Task<(bool Ok, string Ver, string Cmd)> dotnetTask = DetectarPrimeroAsync(
            forzarRecarga,
            [("dotnet", "--version")]);

        Task<(bool Ok, string Ver, string Cmd)> cppTask = DetectarPrimeroAsync(
            forzarRecarga,
            [("g++", "--version"), ("clang++", "--version")]);

        await Task.WhenAll(pythonTask, nodeTask, tsTask, dotnetTask, cppTask);

        var py = await pythonTask;
        var js = await nodeTask;
        var ts = await tsTask;
        var cs = await dotnetTask;
        var cpp = await cppTask;

        _cacheLenguajes =
        [
            CrearEstadoCibao(),
            CrearEstadoHtml(),
            new EstadoEntornoLenguaje
            {
                Lenguaje = LenguajeSoportado.Python,
                Nombre = "Python",
                ExtensionPrincipal = ".py",
                Disponible = py.Ok,
                RequiereSdkExterno = true,
                VersionDetectada = py.Ver,
                ComandoEjecutable = py.Ok ? py.Cmd : "python"
            },
            new EstadoEntornoLenguaje
            {
                Lenguaje = LenguajeSoportado.JavaScript,
                Nombre = "JavaScript (Node.js)",
                ExtensionPrincipal = ".js",
                Disponible = js.Ok,
                RequiereSdkExterno = true,
                VersionDetectada = js.Ok ? $"Node {js.Ver}" : string.Empty,
                ComandoEjecutable = "node"
            },
            new EstadoEntornoLenguaje
            {
                Lenguaje = LenguajeSoportado.TypeScript,
                Nombre = "TypeScript",
                ExtensionPrincipal = ".ts",
                Disponible = ts.Ok || js.Ok,
                RequiereSdkExterno = true,
                VersionDetectada = ts.Ok ? ts.Ver : (js.Ok ? "Node + Diagnóstico nativo" : string.Empty),
                ComandoEjecutable = ts.Ok ? ts.Cmd : "tsc"
            },
            new EstadoEntornoLenguaje
            {
                Lenguaje = LenguajeSoportado.CSharp,
                Nombre = "C# (.NET)",
                ExtensionPrincipal = ".cs",
                Disponible = cs.Ok,
                RequiereSdkExterno = true,
                VersionDetectada = cs.Ok ? $".NET {cs.Ver}" : string.Empty,
                ComandoEjecutable = "dotnet"
            },
            new EstadoEntornoLenguaje
            {
                Lenguaje = LenguajeSoportado.Cpp,
                Nombre = "C++",
                ExtensionPrincipal = ".cpp",
                Disponible = cpp.Ok,
                RequiereSdkExterno = true,
                VersionDetectada = cpp.Ver,
                ComandoEjecutable = cpp.Ok ? cpp.Cmd : "g++"
            }
        ];

        return _cacheLenguajes;
    }

    public async Task<Dictionary<string, (bool Disponible, string Version)>> DetectarHerramientasExtendidasAsync(bool forzarRecarga = false)
    {
        string[] herramientas = ["git", "node", "npm", "tsc", "cmake", "gdb", "java", "go", "rustc", "php", "ruby", "pip"];
        var tareas = herramientas.ToDictionary(
            h => h,
            h => DetectarComandoAsync(h, h == "java" ? "-version" : "--version", forzarRecarga));

        await Task.WhenAll(tareas.Values);
        var salida = new Dictionary<string, (bool Disponible, string Version)>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in tareas)
        {
            salida[kv.Key] = await kv.Value;
        }
        return salida;
    }

    public async Task<IReadOnlyList<EstadoEntornoGrafico>> DetectarEntornosGraficosAsync(bool forzarRecarga = false)
    {
        string plataforma = OperatingSystem.IsWindows()
            ? "Windows"
            : (OperatingSystem.IsMacOS() ? "macOS" : "Linux");

        bool sesionGraficaOk = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ||
                               !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")) ||
                               !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

        var pyTkTask = OperatingSystem.IsWindows()
            ? DetectarPrimeroAsync(forzarRecarga, ("python", "-c \"import tkinter\""), ("python3", "-c \"import tkinter\""))
            : DetectarPrimeroAsync(forzarRecarga, ("python3", "-c \"import tkinter\""), ("python", "-c \"import tkinter\""));

        var dotnetTask = DetectarComandoAsync("dotnet", "--version", forzarRecarga);

        await Task.WhenAll(pyTkTask, dotnetTask);
        var pyTk = await pyTkTask;
        var dotnet = await dotnetTask;

        return
        [
            new EstadoEntornoGrafico
            {
                Nombre = "Navegador Web Integrado + Live Server",
                Plataforma = plataforma,
                Disponible = true,
                Detalle = "HTML5 / CSS3 / JS listo en todas las plataformas"
            },
            new EstadoEntornoGrafico
            {
                Nombre = "Python Tkinter GUI",
                Plataforma = plataforma,
                Disponible = pyTk.Ok && sesionGraficaOk,
                Detalle = !pyTk.Ok
                    ? "Módulo tkinter no instalado en Python"
                    : (sesionGraficaOk ? $"Listo ({pyTk.Cmd})" : "Tkinter instalado, pero falta sesión gráfica (DISPLAY)")
            },
            new EstadoEntornoGrafico
            {
                Nombre = "C# .NET GUI (Avalonia / Desktop)",
                Plataforma = plataforma,
                Disponible = dotnet.Disponible && sesionGraficaOk,
                Detalle = !dotnet.Disponible
                    ? "Requiere .NET SDK 8.0+"
                    : (sesionGraficaOk ? $"Listo (.NET {dotnet.Version})" : $"SDK .NET {dotnet.Version} listo, requiere entorno gráfico")
            }
        ];
    }

    public async Task<(bool Disponible, string Version)> DetectarComandoAsync(
        string comando,
        string argumentos = "--version",
        bool forzarRecarga = false)
    {
        string clave = $"{comando} {argumentos}";
        if (!forzarRecarga && _cacheComandos.TryGetValue(clave, out var guardado))
        {
            return guardado;
        }

        var resultado = await EjecutarSondaAsync(comando, argumentos);
        _cacheComandos[clave] = resultado;
        return resultado;
    }

    private async Task<(bool Ok, string Ver, string Cmd)> DetectarPrimeroAsync(
        bool forzarRecarga,
        params (string Cmd, string Args)[] candidatos)
    {
        foreach (var (cmd, args) in candidatos)
        {
            var (ok, ver) = await DetectarComandoAsync(cmd, args, forzarRecarga);
            if (ok)
            {
                return (true, ver, cmd);
            }
        }

        return (false, string.Empty, string.Empty);
    }

    private static EstadoEntornoLenguaje CrearEstadoCibao() => new()
    {
        Lenguaje = LenguajeSoportado.Cibao,
        Nombre = "Cibao",
        ExtensionPrincipal = ".cibao",
        Disponible = true,
        RequiereSdkExterno = false,
        VersionDetectada = $"{RutasUsuarioAlba.VersionActual} (integrado)",
        ComandoEjecutable = "Alba.Engine"
    };

    private static EstadoEntornoLenguaje CrearEstadoHtml() => new()
    {
        Lenguaje = LenguajeSoportado.Html,
        Nombre = "HTML / CSS / Web",
        ExtensionPrincipal = ".html",
        Disponible = true,
        RequiereSdkExterno = false,
        VersionDetectada = "HTML5 / CSS3 nativo",
        ComandoEjecutable = "browser"
    };

    private static async Task<(bool Disponible, string Version)> EjecutarSondaAsync(string comando, string argumentos)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(2500));
            var psi = new ProcessStartInfo
            {
                FileName = comando,
                Arguments = argumentos,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proceso = new Process { StartInfo = psi };
            if (!proceso.Start())
            {
                return (false, string.Empty);
            }

            Task<string> outTask = proceso.StandardOutput.ReadToEndAsync(cts.Token);
            Task<string> errTask = proceso.StandardError.ReadToEndAsync(cts.Token);
            await proceso.WaitForExitAsync(cts.Token);

            if (proceso.ExitCode != 0)
            {
                return (false, string.Empty);
            }

            string texto = (await outTask).Trim();
            if (string.IsNullOrWhiteSpace(texto))
            {
                texto = (await errTask).Trim();
            }

            string primeraLinea = texto
                .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.Trim() ?? string.Empty;

            if (primeraLinea.Length > 48)
            {
                primeraLinea = primeraLinea[..48] + "...";
            }

            return (true, primeraLinea);
        }
        catch
        {
            return (false, string.Empty);
        }
    }
}
