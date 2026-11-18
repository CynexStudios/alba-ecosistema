using System.Diagnostics;
using System.Text;

namespace Alba.IDE.Services;

/// <summary>
/// Terminal integrada real conectada al shell del sistema operativo (PowerShell en Windows, bash/zsh en Linux/macOS).
/// </summary>
public sealed class ServicioTerminal : IDisposable
{
    private Process? _proceso;
    private readonly object _bloqueo = new();

    public bool EstaActiva => _proceso is { HasExited: false };

    public event Action<string, bool>? LineaRecibida;

    public void Iniciar(string? directorioTrabajo)
    {
        lock (_bloqueo)
        {
            if (EstaActiva)
            {
                return;
            }

            string cwd = !string.IsNullOrWhiteSpace(directorioTrabajo) && Directory.Exists(directorioTrabajo)
                ? directorioTrabajo
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            (string shell, string args) = OperatingSystem.IsWindows()
                ? ("powershell.exe", "-NoLogo -NoProfile -NoExit -Command -")
                : (File.Exists("/bin/bash") ? "/bin/bash" : "/bin/sh", "");

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

            if (_proceso.Start())
            {
                _proceso.BeginOutputReadLine();
                _proceso.BeginErrorReadLine();
                LineaRecibida?.Invoke($"Terminal iniciada ({Path.GetFileName(shell)}) en: {cwd}", false);
            }
        }
    }

    public void EnviarComando(string comando, string? directorioFallback = null)
    {
        if (string.IsNullOrWhiteSpace(comando))
        {
            return;
        }

        if (!EstaActiva)
        {
            Iniciar(directorioFallback);
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

        string cmd = OperatingSystem.IsWindows()
            ? $"Set-Location \"{carpeta.Replace("\"", "`\"")}\""
            : $"cd \"{carpeta.Replace("\"", "\\\"")}\"";

        EnviarComando(cmd, carpeta);
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
