using System.Diagnostics;
using System.Text;
using Alba.Engine.Diagnostics;
using Alba.Engine.Parser;
using Alba.Engine.Runtime;
using Alba.IDE.Models;

namespace Alba.IDE.Services;

/// <summary>
/// Coordina la ejecución directa de Cibao (.cibao / .alb), complementos (/addons)
/// y entornos reales detectados (Python, C#, C++ y HTML).
/// </summary>
public sealed class ServicioEjecucion
{
    private readonly GestorAddons _gestorAddons;

    public event Action<string>? SolicitarVistaWeb;

    public ServicioEjecucion(GestorAddons gestorAddons)
    {
        _gestorAddons = gestorAddons;
    }

    public static bool EsArchivoWeb(string rutaArchivo) =>
        Path.GetExtension(rutaArchivo).ToLowerInvariant() is ".html" or ".htm";

    public static bool EsCodigoGrafico(string rutaArchivo, string codigoFuente)
    {
        string ext = Path.GetExtension(rutaArchivo).ToLowerInvariant();
        if (ext == ".py")
        {
            return codigoFuente.Contains("tkinter", StringComparison.OrdinalIgnoreCase) ||
                   codigoFuente.Contains("PyQt", StringComparison.OrdinalIgnoreCase) ||
                   codigoFuente.Contains("PySide", StringComparison.OrdinalIgnoreCase) ||
                   codigoFuente.Contains("pygame", StringComparison.OrdinalIgnoreCase);
        }
        if (ext is ".cs" or ".axaml")
        {
            return codigoFuente.Contains("Avalonia", StringComparison.OrdinalIgnoreCase) ||
                   codigoFuente.Contains("System.Windows", StringComparison.OrdinalIgnoreCase) ||
                   codigoFuente.Contains("Window", StringComparison.Ordinal);
        }
        return false;
    }

    public async Task<bool> EjecutarArchivoAsync(
        string rutaArchivo,
        string codigoFuente,
        ConsolaIdeIO consolaAlba,
        Action<TipoMensajeConsola, string> registrarMensaje,
        CancellationToken cancellationToken)
    {
        string extension = Path.GetExtension(rutaArchivo).ToLowerInvariant();

        if (extension is ".cibao" or ".alb")
        {
            return await EjecutarCibaoAsync(codigoFuente, rutaArchivo, consolaAlba, registrarMensaje, cancellationToken);
        }

        AddonCargado? addon = _gestorAddons.ObtenerAddonPorExtension(extension);
        if (addon is not null)
        {
            registrarMensaje(
                TipoMensajeConsola.Sistema,
                $"Ejecutando mediante complemento '{addon.Manifiesto.Nombre}' ({addon.Manifiesto.Lenguaje})...");

            int codigoSalida = await _gestorAddons.EjecutarConAddonAsync(
                addon,
                rutaArchivo,
                (linea, esError) => registrarMensaje(esError ? TipoMensajeConsola.Error : TipoMensajeConsola.Salida, linea),
                cancellationToken);

            return codigoSalida == 0;
        }

        if (EsCodigoGrafico(rutaArchivo, codigoFuente) &&
            OperatingSystem.IsLinux() &&
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY")) &&
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            registrarMensaje(
                TipoMensajeConsola.Advertencia,
                "Se detectó una aplicación gráfica, pero no hay servidor gráfico activo (DISPLAY / WAYLAND_DISPLAY) en esta sesión Linux.");
        }

        return extension switch
        {
            ".py" => await EjecutarPythonDirectoAsync(rutaArchivo, registrarMensaje, cancellationToken),
            ".cs" => await EjecutarCSharpAsync(rutaArchivo, registrarMensaje, cancellationToken),
            ".cpp" or ".cc" or ".cxx" => await EjecutarCppAsync(rutaArchivo, registrarMensaje, cancellationToken),
            ".html" or ".htm" => AbrirHtmlEnVistaIntegrada(rutaArchivo, registrarMensaje),
            ".js" or ".mjs" => await EjecutarConRuntimeSimpleAsync("node", [rutaArchivo], rutaArchivo, "Node.js", registrarMensaje, cancellationToken),
            ".ts" => await EjecutarTypeScriptAsync(rutaArchivo, registrarMensaje, cancellationToken),
            ".go" => await EjecutarConRuntimeSimpleAsync("go", ["run", rutaArchivo], rutaArchivo, "Go", registrarMensaje, cancellationToken),
            ".java" => await EjecutarConRuntimeSimpleAsync("java", [rutaArchivo], rutaArchivo, "Java (JDK 11+)", registrarMensaje, cancellationToken),
            ".php" => await EjecutarConRuntimeSimpleAsync("php", [rutaArchivo], rutaArchivo, "PHP", registrarMensaje, cancellationToken),
            ".rb" => await EjecutarConRuntimeSimpleAsync("ruby", [rutaArchivo], rutaArchivo, "Ruby", registrarMensaje, cancellationToken),
            ".rs" => await EjecutarRustAsync(rutaArchivo, registrarMensaje, cancellationToken),
            _ => ReportarNoSoportado(extension, registrarMensaje)
        };
    }

    private static async Task<bool> EjecutarTypeScriptAsync(
        string rutaArchivo,
        Action<TipoMensajeConsola, string> registrarMensaje,
        CancellationToken cancellationToken)
    {
        string cwd = Path.GetDirectoryName(rutaArchivo) ?? Directory.GetCurrentDirectory();
        var (tsNodeOk, exitTsNode) = await EjecutarProcesoStreamingAsync("ts-node", [rutaArchivo], cwd, registrarMensaje, cancellationToken);
        if (tsNodeOk)
        {
            return exitTsNode == 0;
        }

        string tempDir = Path.Combine(Path.GetTempPath(), $"xynex_ts_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            registrarMensaje(TipoMensajeConsola.Sistema, "Compilando TypeScript con tsc...");
            var (tscOk, exitTsc) = await EjecutarProcesoStreamingAsync(
                "tsc",
                [rutaArchivo, "--outDir", tempDir, "--target", "ES2022", "--module", "commonjs"],
                cwd,
                registrarMensaje,
                cancellationToken);

            if (!tscOk)
            {
                registrarMensaje(TipoMensajeConsola.Error, "No se detectó 'ts-node' ni 'tsc' (TypeScript) en el PATH del sistema.");
                return false;
            }
            if (exitTsc != 0)
            {
                return false;
            }

            string jsGenerado = Path.Combine(tempDir, Path.GetFileNameWithoutExtension(rutaArchivo) + ".js");
            if (!File.Exists(jsGenerado))
            {
                return false;
            }

            var (nodeOk, exitNode) = await EjecutarProcesoStreamingAsync("node", [jsGenerado], cwd, registrarMensaje, cancellationToken);
            if (!nodeOk)
            {
                registrarMensaje(TipoMensajeConsola.Error, "TypeScript compiló bien, pero falta Node.js ('node') para ejecutarlo.");
                return false;
            }
            return exitNode == 0;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private static async Task<bool> EjecutarConRuntimeSimpleAsync(
        string ejecutable,
        string[] argumentos,
        string rutaArchivo,
        string nombreEntorno,
        Action<TipoMensajeConsola, string> registrarMensaje,
        CancellationToken cancellationToken)
    {
        string cwd = Path.GetDirectoryName(rutaArchivo) ?? Directory.GetCurrentDirectory();
        var (iniciado, exitCode) = await EjecutarProcesoStreamingAsync(ejecutable, argumentos, cwd, registrarMensaje, cancellationToken);
        if (!iniciado)
        {
            registrarMensaje(TipoMensajeConsola.Error, $"No se detectó '{ejecutable}' ({nombreEntorno}) en el PATH del sistema.");
            return false;
        }
        return exitCode == 0;
    }

    private static async Task<bool> EjecutarRustAsync(
        string rutaArchivo,
        Action<TipoMensajeConsola, string> registrarMensaje,
        CancellationToken cancellationToken)
    {
        string cwd = Path.GetDirectoryName(rutaArchivo) ?? Directory.GetCurrentDirectory();
        string tempDir = Path.Combine(Path.GetTempPath(), $"xynex_rs_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string exeSalida = Path.Combine(tempDir, OperatingSystem.IsWindows() ? "programa.exe" : "programa");
        try
        {
            var (iniciado, exitComp) = await EjecutarProcesoStreamingAsync("rustc", [rutaArchivo, "-o", exeSalida], cwd, registrarMensaje, cancellationToken);
            if (!iniciado)
            {
                registrarMensaje(TipoMensajeConsola.Error, "No se detectó 'rustc' (Rust) en el PATH del sistema.");
                return false;
            }
            if (exitComp != 0 || !File.Exists(exeSalida)) return false;
            var (_, exitRun) = await EjecutarProcesoStreamingAsync(exeSalida, [], cwd, registrarMensaje, cancellationToken);
            return exitRun == 0;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private static Task<bool> EjecutarCibaoAsync(
        string codigoFuente,
        string rutaArchivo,
        ConsolaIdeIO consolaAlba,
        Action<TipoMensajeConsola, string> registrarMensaje,
        CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            string nombreArchivo = Path.GetFileName(rutaArchivo);
            var (lexico, sintactico) = AnalizadorSintactico.AnalizarCodigo(codigoFuente, nombreArchivo);

            if (lexico.TieneErrores || sintactico.TieneErrores)
            {
                foreach (ErrorAlba err in lexico.Errores)
                {
                    registrarMensaje(TipoMensajeConsola.Error, FormateadorErrores.Formatear(err));
                }
                foreach (ErrorAlba err in sintactico.Errores)
                {
                    registrarMensaje(TipoMensajeConsola.Error, FormateadorErrores.Formatear(err));
                }
                return false;
            }

            var interprete = new Interprete(consolaAlba, codigoFuente, rutaArchivo);
            try
            {
                interprete.Ejecutar(sintactico.Programa);
                return true;
            }
            catch (OperationCanceledException)
            {
                registrarMensaje(TipoMensajeConsola.Advertencia, "Ejecución detenida por el usuario.");
                return false;
            }
            catch (ExcepcionAlba ex)
            {
                registrarMensaje(TipoMensajeConsola.Error, FormateadorErrores.Formatear(ex.Error));
                return false;
            }
            catch (ExcepcionLanzadaUsuario exUsuario)
            {
                string linea = FormateadorErrores.ExtraerLinea(codigoFuente, exUsuario.Ubicacion.Linea);
                var error = new ErrorAlba(
                    "ALB-E090",
                    TipoError.Ejecucion,
                    exUsuario.Ubicacion,
                    $"Excepción no capturada: {exUsuario.ValorLanzado.ATextoParaImprimir()}",
                    linea);
                registrarMensaje(TipoMensajeConsola.Error, FormateadorErrores.Formatear(error));
                return false;
            }
        }, cancellationToken);
    }

    private static async Task<bool> EjecutarPythonDirectoAsync(
        string rutaArchivo,
        Action<TipoMensajeConsola, string> registrarMensaje,
        CancellationToken cancellationToken)
    {
        string[] candidatos = OperatingSystem.IsWindows()
            ? ["python", "python3", "py"]
            : ["python3", "python"];

        string cwd = Path.GetDirectoryName(rutaArchivo) ?? Directory.GetCurrentDirectory();
        foreach (string exe in candidatos)
        {
            var (iniciado, exitCode) = await EjecutarProcesoStreamingAsync(
                exe,
                ["-u", rutaArchivo],
                cwd,
                registrarMensaje,
                cancellationToken);

            if (iniciado)
            {
                return exitCode == 0;
            }
        }

        registrarMensaje(
            TipoMensajeConsola.Error,
            "No se detectó Python (python3/python/py) en el PATH del sistema.");
        return false;
    }

    private static async Task<bool> EjecutarCSharpAsync(
        string rutaArchivo,
        Action<TipoMensajeConsola, string> registrarMensaje,
        CancellationToken cancellationToken)
    {
        string? carpeta = Path.GetDirectoryName(rutaArchivo);
        string? csproj = !string.IsNullOrWhiteSpace(carpeta)
            ? Directory.GetFiles(carpeta, "*.csproj").FirstOrDefault()
            : null;

        if (!string.IsNullOrWhiteSpace(csproj))
        {
            registrarMensaje(TipoMensajeConsola.Sistema, $"Compilando y ejecutando proyecto .NET ({Path.GetFileName(csproj)})...");
            var (iniciado, exitCode) = await EjecutarProcesoStreamingAsync(
                "dotnet",
                ["run", "--project", csproj],
                carpeta!,
                registrarMensaje,
                cancellationToken);

            if (!iniciado)
            {
                registrarMensaje(TipoMensajeConsola.Error, "No se detectó el SDK de .NET ('dotnet') en el sistema.");
                return false;
            }
            return exitCode == 0;
        }

        // Archivo .cs independiente: crea un proyecto temporal limpio para compilar y ejecutar con dotnet
        string tempDir = Path.Combine(Path.GetTempPath(), $"xynex_cs_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            string csprojTemp = Path.Combine(tempDir, "ProgramaCSharp.csproj");
            File.WriteAllText(csprojTemp, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net8.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <RollForward>LatestMajor</RollForward>
                  </PropertyGroup>
                </Project>
                """, Encoding.UTF8);
            File.Copy(rutaArchivo, Path.Combine(tempDir, "Program.cs"), overwrite: true);

            registrarMensaje(TipoMensajeConsola.Sistema, "Compilando archivo C# independiente con .NET SDK...");
            var (iniciado, exitCode) = await EjecutarProcesoStreamingAsync(
                "dotnet",
                ["run", "--project", csprojTemp],
                carpeta ?? tempDir,
                registrarMensaje,
                cancellationToken);

            if (!iniciado)
            {
                registrarMensaje(TipoMensajeConsola.Error, "No se detectó el SDK de .NET ('dotnet') en el sistema.");
                return false;
            }
            return exitCode == 0;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private static async Task<bool> EjecutarCppAsync(
        string rutaArchivo,
        Action<TipoMensajeConsola, string> registrarMensaje,
        CancellationToken cancellationToken)
    {
        string carpeta = Path.GetDirectoryName(rutaArchivo) ?? Directory.GetCurrentDirectory();
        string tempDir = Path.Combine(Path.GetTempPath(), $"xynex_cpp_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        string exeSalida = Path.Combine(tempDir, OperatingSystem.IsWindows() ? "programa.exe" : "programa");

        try
        {
            bool compilado = false;
            int codigoCompilacion = -1;
            foreach (string compilador in new[] { "g++", "clang++" })
            {
                registrarMensaje(TipoMensajeConsola.Sistema, $"Compilando C++ con {compilador} (-std=c++20)...");
                var (iniciado, exitCode) = await EjecutarProcesoStreamingAsync(
                    compilador,
                    ["-std=c++20", "-Wall", rutaArchivo, "-o", exeSalida],
                    carpeta,
                    registrarMensaje,
                    cancellationToken);

                if (iniciado)
                {
                    compilado = true;
                    codigoCompilacion = exitCode;
                    break;
                }
            }

            if (!compilado)
            {
                registrarMensaje(TipoMensajeConsola.Error, "No se detectó ningún compilador C++ (g++ o clang++) en el PATH.");
                return false;
            }

            if (codigoCompilacion != 0 || !File.Exists(exeSalida))
            {
                return false;
            }

            registrarMensaje(TipoMensajeConsola.Sistema, "Ejecutando binario C++ compilado...");
            var (_, exitRun) = await EjecutarProcesoStreamingAsync(
                exeSalida,
                [],
                carpeta,
                registrarMensaje,
                cancellationToken);
            return exitRun == 0;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private bool AbrirHtmlEnVistaIntegrada(
        string rutaArchivo,
        Action<TipoMensajeConsola, string> registrarMensaje)
    {
        if (SolicitarVistaWeb is not null)
        {
            SolicitarVistaWeb.Invoke(rutaArchivo);
            registrarMensaje(
                TipoMensajeConsola.Sistema,
                $"Vista previa Web integrada y Live Server iniciados para: {Path.GetFileName(rutaArchivo)}");
            return true;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = rutaArchivo,
                UseShellExecute = true
            });
            registrarMensaje(
                TipoMensajeConsola.Sistema,
                $"Archivo HTML abierto en el navegador predeterminado: {rutaArchivo}");
            return true;
        }
        catch (Exception ex)
        {
            registrarMensaje(TipoMensajeConsola.Error, $"No se pudo abrir el archivo HTML: {ex.Message}");
            return false;
        }
    }

    private static bool ReportarNoSoportado(string extension, Action<TipoMensajeConsola, string> registrarMensaje)
    {
        registrarMensaje(
            TipoMensajeConsola.Advertencia,
            $"No hay ningún motor o complemento activo para ejecutar archivos '{extension}'.");
        return false;
    }

    private static async Task<(bool Iniciado, int ExitCode)> EjecutarProcesoStreamingAsync(
        string fileName,
        IEnumerable<string> argumentos,
        string workingDirectory,
        Action<TipoMensajeConsola, string> registrarMensaje,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = Directory.Exists(workingDirectory) ? workingDirectory : Directory.GetCurrentDirectory(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (string arg in argumentos)
        {
            psi.ArgumentList.Add(arg);
        }

        try
        {
            using var proceso = new Process { StartInfo = psi };
            proceso.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null) registrarMensaje(TipoMensajeConsola.Salida, e.Data);
            };
            proceso.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null) registrarMensaje(TipoMensajeConsola.Error, e.Data);
            };

            if (!proceso.Start())
            {
                return (false, -1);
            }

            proceso.BeginOutputReadLine();
            proceso.BeginErrorReadLine();

            try
            {
                await proceso.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                try { if (!proceso.HasExited) proceso.Kill(entireProcessTree: true); } catch { }
                registrarMensaje(TipoMensajeConsola.Advertencia, "Ejecución cancelada por el usuario.");
                return (true, 130);
            }

            return (true, proceso.ExitCode);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return (false, -1);
        }
    }
}
