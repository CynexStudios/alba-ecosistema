using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Alba.IDE.Models;

namespace Alba.IDE.Services;

/// <summary>
/// Administra descubrimiento, catálogo en línea vía API, validación, activación, instalación,
/// eliminación y ejecución controlada de complementos, separando addons integrados de addons de usuario.
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

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private const string UrlCatalogoOficialRaw =
        "https://raw.githubusercontent.com/CynexStudios/alba-ecosistema/main/addons/catalogo.json";

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

    public IReadOnlyList<SnippetAddon> ObtenerSnippetsActivosPorExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return [];
        }

        return _addons
            .Where(a => a.Activo && a.EsValido &&
                        a.Manifiesto.Extensiones.Any(ext => ext.Equals(extension, StringComparison.OrdinalIgnoreCase)))
            .SelectMany(a => a.Snippets)
            .ToList();
    }

    public async Task<(IReadOnlyList<AddonCatalogoEnLinea> Catalogo, string Fuente)> ConsultarCatalogoEnLineaAsync(
        CancellationToken ct = default)
    {
        List<AddonCatalogoEnLinea>? lista = null;
        string fuente = "API en línea GitHub (CynexStudios)";

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, UrlCatalogoOficialRaw);
            req.Headers.UserAgent.ParseAdd($"XynexCodeEditor/{RutasUsuarioAlba.VersionActual}");
            using HttpResponseMessage resp = await Http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                string json = await resp.Content.ReadAsStringAsync(ct);
                lista = JsonSerializer.Deserialize<List<AddonCatalogoEnLinea>>(json, JsonOpciones);
            }
        }
        catch
        {
            // Usa catálogo local embebido como respaldo transparente si no hay internet o aún no se publica el branch
        }

        if (lista is null || lista.Count == 0)
        {
            lista = ObtenerCatalogoRespaldoIntegrado();
            fuente = "Catálogo oficial integrado v9.0 (sin conexión requerida)";
        }

        var instalados = new HashSet<string>(_addons.Select(a => a.Manifiesto.Id), StringComparer.OrdinalIgnoreCase);
        foreach (AddonCatalogoEnLinea item in lista)
        {
            item.YaInstalado = instalados.Contains(item.Id);
        }

        return (lista, fuente);
    }

    public async Task<(bool Exito, string Mensaje)> InstalarDesdeCatalogoEnLineaAsync(
        AddonCatalogoEnLinea item,
        CancellationToken ct = default)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Id))
        {
            return (false, "Entrada de catálogo inválida.");
        }

        var manifiesto = new ManifiestoAddon
        {
            Id = item.Id.Trim(),
            Nombre = item.Nombre.Trim(),
            Version = string.IsNullOrWhiteSpace(item.Version) ? "9.0.0" : item.Version.Trim(),
            Descripcion = item.Descripcion,
            Autor = string.IsNullOrWhiteSpace(item.Autor) ? "CynexStudios" : item.Autor,
            Lenguaje = item.Lenguaje,
            Extensiones = item.Extensiones.ToList(),
            Ejecutable = item.Ejecutable,
            Snippets = "snippets/snippets.json"
        };

        List<SnippetAddon> snippets = item.SnippetsIntegrados?.ToList() ?? [];
        if (!string.IsNullOrWhiteSpace(item.UrlSnippets) && ServicioSeguridad.EsUrlHttpSegura(item.UrlSnippets, false))
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, item.UrlSnippets);
                req.Headers.UserAgent.ParseAdd($"XynexCodeEditor/{RutasUsuarioAlba.VersionActual}");
                using HttpResponseMessage resp = await Http.SendAsync(req, ct);
                if (resp.IsSuccessStatusCode)
                {
                    string jsonSnip = await resp.Content.ReadAsStringAsync(ct);
                    List<SnippetAddon>? descargados = JsonSerializer.Deserialize<List<SnippetAddon>>(jsonSnip, JsonOpciones);
                    if (descargados is { Count: > 0 })
                    {
                        snippets = descargados;
                    }
                }
            }
            catch
            {
                // Mantiene snippetsIntegrados como respaldo seguro
            }
        }

        string tempDir = Path.Combine(Path.GetTempPath(), $"xynex_addon_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            string dirSnippets = Path.Combine(tempDir, "snippets");
            Directory.CreateDirectory(dirSnippets);
            File.WriteAllText(
                Path.Combine(dirSnippets, "snippets.json"),
                JsonSerializer.Serialize(snippets, JsonOpciones),
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(tempDir, "manifest.json"),
                JsonSerializer.Serialize(manifiesto, JsonOpciones),
                Encoding.UTF8);

            ResultadoValidacionAddon validacion = ValidadorAddons.Validar(manifiesto, tempDir);
            if (!validacion.EsValido)
            {
                return (false, $"Addon bloqueado por validación de seguridad: {validacion.Mensaje}");
            }

            if (!ValidadorAddons.TryResolverRutaSegura(DirectorioAddons, manifiesto.Id, out string? destino) ||
                string.IsNullOrWhiteSpace(destino))
            {
                return (false, "Identificador de addon inseguro para instalación en disco.");
            }

            if (Directory.Exists(destino))
            {
                Directory.Delete(destino, recursive: true);
            }

            CopiarDirectorio(tempDir, destino);
            _desactivados.Remove(manifiesto.Id);
            GuardarConfiguracion();
            RecargarAddons();
            item.YaInstalado = true;

            return (true, $"Complemento '{manifiesto.Nombre}' v{manifiesto.Version} descargado, validado e instalado.");
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private static List<AddonCatalogoEnLinea> ObtenerCatalogoRespaldoIntegrado() =>
    [
        new AddonCatalogoEnLinea
        {
            Id = "web-html-css-pro",
            Nombre = "HTML5 & CSS3 Studio Pack",
            Version = "9.0.0",
            Descripcion = "Snippets profesionales HTML5/CSS3, tarjetas modernas, Grid y Flexbox listos para Live Preview.",
            Autor = "CynexStudios",
            Lenguaje = "HTML / CSS",
            Extensiones = [".html", ".htm", ".css"],
            Ejecutable = "browser",
            SnippetsIntegrados =
            [
                new SnippetAddon { Prefijo = "html5", Titulo = "Plantilla HTML5 Moderna", Descripcion = "Estructura HTML5 responsiva", Cuerpo = "<!DOCTYPE html>\n<html lang=\"es\">\n<head>\n  <meta charset=\"UTF-8\" />\n  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />\n  <title>Mi Sitio Xynex</title>\n</head>\n<body>\n  <main class=\"card\">\n    <h1>Hola Mundo Web</h1>\n  </main>\n</body>\n</html>" },
                new SnippetAddon { Prefijo = "card", Titulo = "Tarjeta CSS Moderna", Descripcion = "Clase tarjeta oscura", Cuerpo = ".card {\n  background: #161b22;\n  border: 1px solid #30363d;\n  border-radius: 12px;\n  padding: 20px;\n}" }
            ]
        },
        new AddonCatalogoEnLinea
        {
            Id = "js-ts-runtime-pack",
            Nombre = "JavaScript & TypeScript Pro Pack",
            Version = "9.0.0",
            Descripcion = "Plantillas ES2024/TypeScript, fetch async/await e interfaces tipadas para Node.js y tsc.",
            Autor = "CynexStudios",
            Lenguaje = "JavaScript / TypeScript",
            Extensiones = [".js", ".mjs", ".ts"],
            Ejecutable = "node",
            SnippetsIntegrados =
            [
                new SnippetAddon { Prefijo = "fnasync", Titulo = "Fetch Async/Await", Descripcion = "Petición HTTP moderna", Cuerpo = "async function consultarDatos(url) {\n  const res = await fetch(url);\n  return await res.json();\n}" },
                new SnippetAddon { Prefijo = "iface", Titulo = "Interface TypeScript", Descripcion = "Entidad tipada TS", Cuerpo = "interface Usuario {\n  id: number;\n  nombre: string;\n  activo: boolean;\n}" }
            ]
        },
        new AddonCatalogoEnLinea
        {
            Id = "cibao-algoritmos-pro",
            Nombre = "Cibao 9.0 Algoritmos y POO Pack",
            Version = "9.0.0",
            Descripcion = "Snippets avanzados de Cibao 9.0: rangos 1..=10, lambdas flecha, clases seguras y módulos.",
            Autor = "CynexStudios",
            Lenguaje = "Cibao",
            Extensiones = [".cibao", ".alb"],
            Ejecutable = "cibao",
            SnippetsIntegrados =
            [
                new SnippetAddon { Prefijo = "clase9", Titulo = "Clase Cibao 9.0", Descripcion = "Clase completa con constructor", Cuerpo = "clase Servicio {\n    privado nombre: texto\n    constructor(nombre: texto) { este.nombre = nombre }\n    publico funcion ejecutar() { mostrar($\"Servicio {este.nombre} activo\") }\n}" },
                new SnippetAddon { Prefijo = "rango9", Titulo = "Bucle con Rango 9.0", Descripcion = "Iteración 1..=10", Cuerpo = "para i en 1..=10 {\n    mostrar($\"Paso {i}\")\n}" }
            ]
        },
        new AddonCatalogoEnLinea
        {
            Id = "python-data-pack",
            Nombre = "Python 3 Data & Automatización",
            Version = "9.0.0",
            Descripcion = "Dataclasses, scripts limpios y utilidades Python 3.",
            Autor = "CynexStudios",
            Lenguaje = "Python",
            Extensiones = [".py"],
            Ejecutable = "python3",
            SnippetsIntegrados =
            [
                new SnippetAddon { Prefijo = "dclass", Titulo = "Dataclass Python", Descripcion = "Modelo @dataclass", Cuerpo = "from dataclasses import dataclass\n\n@dataclass\nclass Registro:\n    id: int\n    nombre: str" }
            ]
        },
        new AddonCatalogoEnLinea
        {
            Id = "csharp-dotnet8-pack",
            Nombre = "C# .NET 8 Productividad",
            Version = "9.0.0",
            Descripcion = "Records inmutables y estructuras modernas para dotnet.",
            Autor = "CynexStudios",
            Lenguaje = "C#",
            Extensiones = [".cs"],
            Ejecutable = "dotnet",
            SnippetsIntegrados =
            [
                new SnippetAddon { Prefijo = "rec", Titulo = "Record C#", Descripcion = "Modelo compacto C# 12", Cuerpo = "public sealed record Elemento(int Id, string Nombre);" }
            ]
        },
        new AddonCatalogoEnLinea
        {
            Id = "cpp20-modern-pack",
            Nombre = "C++20 Moderno y Estructuras",
            Version = "9.0.0",
            Descripcion = "Esqueletos modernos C++20 con STL segura.",
            Autor = "CynexStudios",
            Lenguaje = "C++",
            Extensiones = [".cpp", ".hpp"],
            Ejecutable = "g++",
            SnippetsIntegrados =
            [
                new SnippetAddon { Prefijo = "maincpp", Titulo = "Main C++20", Descripcion = "Punto de entrada estándar", Cuerpo = "#include <iostream>\nint main() {\n    std::cout << \"C++20 listo\\n\";\n    return 0;\n}" }
            ]
        }
    ];

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
