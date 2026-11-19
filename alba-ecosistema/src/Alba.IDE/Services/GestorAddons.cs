using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Alba.IDE.Models;

namespace Alba.IDE.Services;

/// <summary>
/// Administra descubrimiento, validación, activación, instalación, eliminación y ejecución
/// controlada de complementos, separando addons integrados (solo lectura) de addons de usuario.
/// </summary>
public sealed class GestorAddons
{
    private sealed class ConfiguracionAddons
    {
        public List<string> Desactivados { get; set; } = [];
    }

    private static readonly JsonSerializerOptions JsonOpciones = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly List<AddonCargado> _addons = [];
    private readonly HashSet<string> _desactivados = new(StringComparer.OrdinalIgnoreCase);
    private readonly string? _directorioSistema;
    private readonly string _rutaConfiguracion;

    public string DirectorioAddons { get; }
    public IReadOnlyList<AddonCargado> Addons => _addons;

    public GestorAddons(string? directorioAddons = null)
    {
        if (!string.IsNullOrWhiteSpace(directorioAddons))
        {
            // Modo explícito (utilizado también por pruebas unitarias)
            DirectorioAddons = directorioAddons;
            _directorioSistema = null;
            _rutaConfiguracion = Path.Combine(DirectorioAddons, "addons-config.json");
            Directory.CreateDirectory(DirectorioAddons);
        }
        else
        {
            // Modo aplicación instalada: addons de fábrica en directorio de app + addons de usuario en AppData
            _directorioSistema = RutasUsuarioAlba.BuscarDirectorioInstalacion("addons");
            DirectorioAddons = RutasUsuarioAlba.ObtenerDirectorioAddonsUsuario();
            _rutaConfiguracion = RutasUsuarioAlba.ObtenerArchivoConfigAddonsUsuario();
        }

        CargarConfiguracion();
        RecargarAddons();
    }

    public void RecargarAddons()
    {
        _addons.Clear();
        var idsVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Los addons instalados por el usuario tienen prioridad si sobrescriben un id integrado
        CargarDesdeDirectorio(DirectorioAddons, idsVistos);

        if (!string.IsNullOrWhiteSpace(_directorioSistema) &&
            !_directorioSistema.Equals(DirectorioAddons, StringComparison.OrdinalIgnoreCase))
        {
            CargarDesdeDirectorio(_directorioSistema, idsVistos);
        }
    }

    private void CargarDesdeDirectorio(string carpetaRaiz, HashSet<string> idsVistos)
    {
        if (!Directory.Exists(carpetaRaiz))
        {
            return;
        }

        foreach (string subcarpeta in Directory.GetDirectories(carpetaRaiz).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            string rutaManifiesto = Path.Combine(subcarpeta, "manifest.json");
            if (!File.Exists(rutaManifiesto))
            {
                continue;
            }

            try
            {
                string json = File.ReadAllText(rutaManifiesto, Encoding.UTF8);
                ManifiestoAddon? manifiesto = JsonSerializer.Deserialize<ManifiestoAddon>(json, JsonOpciones);
                if (manifiesto is null || !idsVistos.Add(manifiesto.Id))
                {
                    continue;
                }

                ResultadoValidacionAddon validacion = ValidadorAddons.Validar(manifiesto, subcarpeta);
                List<SnippetAddon> snippets = validacion.EsValido
                    ? CargarSnippets(subcarpeta, manifiesto.Snippets)
                    : [];

                bool activo = validacion.EsValido && !_desactivados.Contains(manifiesto.Id);

                _addons.Add(new AddonCargado
                {
                    Manifiesto = manifiesto,
                    RutaCarpeta = subcarpeta,
                    Activo = activo,
                    EsValido = validacion.EsValido,
                    EstadoSeguridad = validacion.Mensaje,
                    RutaSintaxisResuelta = validacion.RutaSintaxisResuelta,
                    RutaScriptResuelta = validacion.RutaScriptResuelta,
                    Snippets = snippets
                });
            }
            catch (Exception ex)
            {
                _addons.Add(new AddonCargado
                {
                    Manifiesto = new ManifiestoAddon
                    {
                        Id = Path.GetFileName(subcarpeta),
                        Nombre = Path.GetFileName(subcarpeta),
                        Version = "0.0.0",
                        Lenguaje = "Desconocido"
                    },
                    RutaCarpeta = subcarpeta,
                    Activo = false,
                    EsValido = false,
                    EstadoSeguridad = $"Error al leer manifest.json: {ex.Message}"
                });
            }
        }
    }

    public void CambiarEstado(string idAddon, bool activar)
    {
        AddonCargado? addon = _addons.FirstOrDefault(a =>
            a.Manifiesto.Id.Equals(idAddon, StringComparison.OrdinalIgnoreCase));

        if (addon is null || !addon.EsValido)
        {
            return;
        }

        if (activar)
        {
            _desactivados.Remove(addon.Manifiesto.Id);
            addon.Activo = true;
        }
        else
        {
            _desactivados.Add(addon.Manifiesto.Id);
            addon.Activo = false;
        }

        GuardarConfiguracion();
    }

    public (bool Exito, string Mensaje) InstalarDesdeCarpeta(string carpetaOrigen)
    {
        if (string.IsNullOrWhiteSpace(carpetaOrigen) || !Directory.Exists(carpetaOrigen))
        {
            return (false, "La carpeta de origen no existe.");
        }

        string rutaManifiesto = Path.Combine(carpetaOrigen, "manifest.json");
        if (!File.Exists(rutaManifiesto))
        {
            return (false, "La carpeta seleccionada no contiene un archivo 'manifest.json'.");
        }

        ManifiestoAddon? manifiesto;
        try
        {
            manifiesto = JsonSerializer.Deserialize<ManifiestoAddon>(
                File.ReadAllText(rutaManifiesto, Encoding.UTF8), JsonOpciones);
        }
        catch (Exception ex)
        {
            return (false, $"El manifiesto JSON es inválido: {ex.Message}");
        }

        if (manifiesto is null)
        {
            return (false, "No se pudo interpretar manifest.json.");
        }

        ResultadoValidacionAddon validacion = ValidadorAddons.Validar(manifiesto, carpetaOrigen);
        if (!validacion.EsValido)
        {
            return (false, $"Addon rechazado por seguridad: {validacion.Mensaje}");
        }

        string destino = Path.Combine(DirectorioAddons, manifiesto.Id);
        if (Directory.Exists(destino))
        {
            Directory.Delete(destino, recursive: true);
        }

        CopiarDirectorio(carpetaOrigen, destino);
        _desactivados.Remove(manifiesto.Id);
        GuardarConfiguracion();
        RecargarAddons();

        return (true, $"Complemento '{manifiesto.Nombre}' v{manifiesto.Version} instalado correctamente.");
    }

    public (bool Exito, string Mensaje) DesinstalarAddon(string idAddon)
    {
        AddonCargado? addon = _addons.FirstOrDefault(a =>
            a.Manifiesto.Id.Equals(idAddon, StringComparison.OrdinalIgnoreCase));

        if (addon is null)
        {
            return (false, $"No se encontró el complemento '{idAddon}'.");
        }

        if (!string.IsNullOrWhiteSpace(_directorioSistema) &&
            addon.RutaCarpeta.StartsWith(_directorioSistema, StringComparison.OrdinalIgnoreCase))
        {
            return (false, $"'{addon.Manifiesto.Nombre}' es un complemento integrado del sistema. Puedes desactivarlo si no deseas usarlo.");
        }

        try
        {
            if (Directory.Exists(addon.RutaCarpeta))
            {
                Directory.Delete(addon.RutaCarpeta, recursive: true);
            }
            _desactivados.Remove(addon.Manifiesto.Id);
            GuardarConfiguracion();
            RecargarAddons();
            return (true, $"Complemento '{addon.Manifiesto.Nombre}' eliminado.");
        }
        catch (Exception ex)
        {
            return (false, $"No se pudo eliminar el complemento: {ex.Message}");
        }
    }

    public AddonCargado? ObtenerAddonPorExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        return _addons.FirstOrDefault(a =>
            a.Activo &&
            a.EsValido &&
            a.Manifiesto.Extensiones.Any(ext => ext.Equals(extension, StringComparison.OrdinalIgnoreCase)));
    }

    public async Task<int> EjecutarConAddonAsync(
        AddonCargado addon,
        string rutaArchivo,
        Action<string, bool> alRecibirLinea,
        CancellationToken cancellationToken)
    {
        if (!addon.EsValido || !addon.Activo)
        {
            alRecibirLinea("El complemento no está activo o no superó la validación de seguridad.", true);
            return 1;
        }

        if (!ValidadorAddons.EsEjecutableSeguro(addon.Manifiesto.Ejecutable))
        {
            alRecibirLinea("Comando de complemento bloqueado por política de seguridad.", true);
            return 1;
        }

        List<string> candidatos = ObtenerEjecutablesCandidatos(addon.Manifiesto.Ejecutable);

        foreach (string ejecutable in candidatos)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ejecutable,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = Path.GetDirectoryName(rutaArchivo) ?? Directory.GetCurrentDirectory()
            };

            if (!string.IsNullOrWhiteSpace(addon.RutaScriptResuelta))
            {
                psi.ArgumentList.Add(addon.RutaScriptResuelta);
                psi.ArgumentList.Add("ejecutar");
            }
            psi.ArgumentList.Add(rutaArchivo);

            try
            {
                using var proceso = new Process { StartInfo = psi };
                proceso.OutputDataReceived += (_, e) =>
                {
                    if (e.Data is not null) alRecibirLinea(e.Data, false);
                };
                proceso.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data is not null) alRecibirLinea(e.Data, true);
                };

                if (!proceso.Start())
                {
                    continue;
                }

                proceso.BeginOutputReadLine();
                proceso.BeginErrorReadLine();

                await proceso.WaitForExitAsync(cancellationToken);
                return proceso.ExitCode;
            }
            catch (OperationCanceledException)
            {
                alRecibirLinea("Ejecución cancelada por el usuario.", true);
                return 130;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Prueba el siguiente candidato (ej: python3 -> python en Windows)
            }
        }

        alRecibirLinea(
            $"No se encontró el intérprete '{addon.Manifiesto.Ejecutable}' en el PATH del sistema.",
            true);
        return 127;
    }

    private static List<string> ObtenerEjecutablesCandidatos(string ejecutable)
    {
        if (OperatingSystem.IsWindows() && ejecutable.Equals("python3", StringComparison.OrdinalIgnoreCase))
        {
            return ["python", "python3", "py"];
        }
        return [ejecutable];
    }

    private static List<SnippetAddon> CargarSnippets(string carpetaAddon, string? rutaRelativa)
    {
        if (string.IsNullOrWhiteSpace(rutaRelativa) ||
            !ValidadorAddons.TryResolverRutaSegura(carpetaAddon, rutaRelativa, out string? ruta) ||
            !File.Exists(ruta))
        {
            return [];
        }

        try
        {
            string json = File.ReadAllText(ruta, Encoding.UTF8);
            return JsonSerializer.Deserialize<List<SnippetAddon>>(json, JsonOpciones) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void CargarConfiguracion()
    {
        _desactivados.Clear();
        if (!File.Exists(_rutaConfiguracion))
        {
            return;
        }

        try
        {
            string json = File.ReadAllText(_rutaConfiguracion, Encoding.UTF8);
            ConfiguracionAddons? cfg = JsonSerializer.Deserialize<ConfiguracionAddons>(json, JsonOpciones);
            if (cfg?.Desactivados is not null)
            {
                foreach (string id in cfg.Desactivados)
                {
                    _desactivados.Add(id);
                }
            }
        }
        catch
        {
            // Si la configuración está corrupta, se inicia con todos los addons válidos activos
        }
    }

    private void GuardarConfiguracion()
    {
        var cfg = new ConfiguracionAddons
        {
            Desactivados = _desactivados.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()
        };
        File.WriteAllText(_rutaConfiguracion, JsonSerializer.Serialize(cfg, JsonOpciones), Encoding.UTF8);
    }

    private static void CopiarDirectorio(string origen, string destino)
    {
        Directory.CreateDirectory(destino);
        foreach (string archivo in Directory.GetFiles(origen))
        {
            File.Copy(archivo, Path.Combine(destino, Path.GetFileName(archivo)), overwrite: true);
        }
        foreach (string sub in Directory.GetDirectories(origen))
        {
            CopiarDirectorio(sub, Path.Combine(destino, Path.GetFileName(sub)));
        }
    }
}
