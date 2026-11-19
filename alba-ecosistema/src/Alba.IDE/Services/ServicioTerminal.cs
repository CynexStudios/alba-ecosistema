using System.Diagnostics;
using System.Text;
using Alba.IDE.Models;

namespace Alba.IDE.Services;

/// <summary>
/// Terminal integrada real conectada al shell del sistema operativo (PowerShell/CMD en Windows, Bash/Zsh en Linux/macOS)
/// o modo integrado Xynex.
/// </summary>
public sealed class ServicioTerminal : IDisposable
{
    private Process? _proceso;
    private readonly object _bloqueo = new();

    public bool EstaActiva => _proceso is { HasExited: false };
    public ModalidadTerminal ModalidadActual { get; private set; } = ModalidadTerminal.IntegradaIde;

    public event Action<string, bool>? LineaRecibida;

    public void Iniciar(string? directorioTrabajo, ModalidadTerminal modalidad = ModalidadTerminal.IntegradaIde)
    {
        lock (_bloqueo)
        {
            if (EstaActiva)
            {
                return;
            }

            ModalidadActual = modalidad;

            string cwd = !string.IsNullOrWhiteSpace(directorioTrabajo) && Directory.Exists(directorioTrabajo)
                ? directorioTrabajo
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            (string shell, string args) = ResolverShell(modalidad);

            var psi = new ProcessStartInfo
            {
                FileName = shell,
                Arguments = args,
                WorkingDirectory = cwd,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            _proceso = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _proceso.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    LineaRecibida?.Invoke(e.Data, false);
                }
            };
            _proceso.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                {
                    LineaRecibida?.Invoke(e.Data, true);
                }
            };

            try
            {
                if (_proceso.Start())
                {
                    _proceso.BeginOutputReadLine();
                    _proceso.BeginErrorReadLine();
                    LineaRecibida?.Invoke(
                        $"Sesión iniciada [{ObtenerNombreModalidad(modalidad)} · {Path.GetFileName(shell)}] en: {cwd}",
                        false);
                }
            }
            catch (Exception ex)
            {
                LineaRecibida?.Invoke($"No se pudo iniciar '{shell}': {ex.Message}", true);
            }
        }
    }

    public void EnviarComando(
        string comando,
        string? directorioFallback = null,
        ModalidadTerminal modalidadFallback = ModalidadTerminal.IntegradaIde)
    {
        if (string.IsNullOrWhiteSpace(comando))
        {
            return;
        }

        if (!EstaActiva)
        {
            Iniciar(directorioFallback, modalidadFallback);
        }

        lock (_bloqueo)
        {
            if (_proceso is { HasExited: false })
            {
                _proceso.StandardInput.WriteLine(comando);
                _proceso.StandardInput.Flush();
            }
        }
    }

    public void CambiarDirectorio(string carpeta)
    {
        if (!EstaActiva || !Directory.Exists(carpeta))
        {
            return;
        }

        string cmd = ModalidadActual == ModalidadTerminal.CmdWindows
            ? $"cd /d \"{carpeta}\""
            : (OperatingSystem.IsWindows() && ModalidadActual != ModalidadTerminal.BashSistema
                ? $"Set-Location \"{carpeta.Replace("\"", "`\"")}\""
                : $"cd \"{carpeta.Replace("\"", "\\\"")}\"");

        EnviarComando(cmd, carpeta, ModalidadActual);
    }

    public void Detener()
    {
        Dispose();
        LineaRecibida?.Invoke("Proceso de terminal detenido.", false);
    }

    public static string ObtenerNombreModalidad(ModalidadTerminal modalidad) => modalidad switch
    {
        ModalidadTerminal.IntegradaIde => "IDE Integrada",
        ModalidadTerminal.PowerShellWindows => "PowerShell",
        ModalidadTerminal.CmdWindows => "CMD",
        ModalidadTerminal.BashSistema => "Bash",
        ModalidadTerminal.ZshSistema => "Zsh",
        _ => "Terminal"
    };

    private static (string Shell, string Args) ResolverShell(ModalidadTerminal modalidad)
    {
        if (OperatingSystem.IsWindows())
        {
            return modalidad switch
            {
                ModalidadTerminal.CmdWindows => ("cmd.exe", "/K"),
                ModalidadTerminal.BashSistema when File.Exists(@"C:\Program Files\Git\bin\bash.exe") =>
                    (@"C:\Program Files\Git\bin\bash.exe", ""),
                _ => ("powershell.exe", "-NoLogo -NoProfile -NoExit -Command -")
            };
        }

        return modalidad switch
        {
            ModalidadTerminal.ZshSistema when File.Exists("/bin/zsh") => ("/bin/zsh", ""),
            ModalidadTerminal.ZshSistema when File.Exists("/usr/bin/zsh") => ("/usr/bin/zsh", ""),
            _ => (File.Exists("/bin/bash") ? "/bin/bash" : "/bin/sh", "")
        };
    }

    public void Dispose()
    {
        lock (_bloqueo)
        {
            if (_proceso is not null)
            {
                try
                {
                    if (!_proceso.HasExited)
                    {
                        _proceso.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // Ignora si el proceso ya finalizó
                }
                _proceso.Dispose();
                _proceso = null;
            }
        }
    }
}
