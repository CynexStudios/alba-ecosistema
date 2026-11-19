using System.Text;
using System.Text.Json;
using Alba.IDE.Models;

namespace Alba.IDE.Services;

/// <summary>
/// Persiste configuración del IDE, historial de recientes y recuperación de sesión
/// en el directorio de datos del usuario.
/// </summary>
public sealed class GestorConfiguracion
{
    private static readonly JsonSerializerOptions JsonOpciones = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _rutaConfig;
    private readonly string _rutaRecientes;
    private readonly string _rutaSesion;

    public ConfiguracionIDE Configuracion { get; private set; } = new();
    public List<ElementoReciente> Recientes { get; private set; } = [];
    public SesionIDE Sesion { get; private set; } = new();

    public GestorConfiguracion(string? directorioBase = null)
    {
        if (string.IsNullOrWhiteSpace(directorioBase))
        {
            _rutaConfig = RutasUsuarioAlba.ObtenerArchivoConfiguracion();
            _rutaRecientes = RutasUsuarioAlba.ObtenerArchivoRecientes();
            _rutaSesion = RutasUsuarioAlba.ObtenerArchivoSesion();
        }
        else
        {
            Directory.CreateDirectory(directorioBase);
            _rutaConfig = Path.Combine(directorioBase, "configuracion.json");
            _rutaRecientes = Path.Combine(directorioBase, "recientes.json");
            _rutaSesion = Path.Combine(directorioBase, "sesion.json");
        }

        CargarTodo();
    }

    public void CargarTodo()
    {
        Configuracion = LeerJson<ConfiguracionIDE>(_rutaConfig) ?? new ConfiguracionIDE();
        Recientes = LeerJson<List<ElementoReciente>>(_rutaRecientes) ?? [];
        Sesion = LeerJson<SesionIDE>(_rutaSesion) ?? new SesionIDE();

        // Asegura que siempre existan los atajos predeterminados faltantes
        var defaults = new ConfiguracionIDE().AtajosTeclado;
        foreach (var kv in defaults)
        {
            Configuracion.AtajosTeclado.TryAdd(kv.Key, kv.Value);
        }
    }

    public void GuardarConfiguracion() => EscribirJson(_rutaConfig, Configuracion);

    public void RegistrarReciente(string ruta, bool esProyecto)
    {
        if (string.IsNullOrWhiteSpace(ruta))
        {
            return;
        }

        string rutaNorm = Path.GetFullPath(ruta);
        ElementoReciente? existente = Recientes.FirstOrDefault(r =>
            r.Ruta.Equals(rutaNorm, StringComparison.OrdinalIgnoreCase));

        bool fijado = existente?.Fijado ?? false;
        if (existente is not null)
        {
            Recientes.Remove(existente);
        }

        string nombre = Path.GetFileName(rutaNorm.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(nombre))
        {
            nombre = rutaNorm;
        }

        Recientes.Insert(0, new ElementoReciente
        {
            Ruta = rutaNorm,
            Nombre = nombre,
            EsProyecto = esProyecto,
            UltimoAccesoUtc = DateTime.UtcNow,
            Fijado = fijado
        });

        // Ordena primero fijados y conserva hasta 20 entradas
        Recientes = Recientes
            .OrderByDescending(r => r.Fijado)
            .ThenByDescending(r => r.UltimoAccesoUtc)
            .Take(20)
            .ToList();

        EscribirJson(_rutaRecientes, Recientes);
    }

    public void AlternarFijadoReciente(string ruta)
    {
        ElementoReciente? item = Recientes.FirstOrDefault(r =>
            r.Ruta.Equals(ruta, StringComparison.OrdinalIgnoreCase));
        if (item is null) return;

        item.Fijado = !item.Fijado;
        Recientes = Recientes
            .OrderByDescending(r => r.Fijado)
            .ThenByDescending(r => r.UltimoAccesoUtc)
            .ToList();
        EscribirJson(_rutaRecientes, Recientes);
    }

    public void EliminarReciente(string ruta)
    {
        Recientes.RemoveAll(r => r.Ruta.Equals(ruta, StringComparison.OrdinalIgnoreCase));
        EscribirJson(_rutaRecientes, Recientes);
    }

    public void GuardarSesion(
        string? proyectoActual,
        IEnumerable<string> pestanasAbiertas,
        string? archivoActivo,
        IEnumerable<string>? archivosIndependientes = null)
    {
        Sesion = new SesionIDE
        {
            UltimoProyecto = proyectoActual,
            PestanasAbiertas = pestanasAbiertas.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ArchivosIndependientes = archivosIndependientes is null
                ? []
                : archivosIndependientes.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ArchivoActivo = archivoActivo
        };
        EscribirJson(_rutaSesion, Sesion);
    }

    private static T? LeerJson<T>(string ruta)
    {
        if (!File.Exists(ruta))
        {
            return default;
        }

        try
        {
            string json = File.ReadAllText(ruta, Encoding.UTF8);
            return JsonSerializer.Deserialize<T>(json, JsonOpciones);
        }
        catch
        {
            return default;
        }
    }

    private static void EscribirJson<T>(string ruta, T datos)
    {
        try
        {
            string json = JsonSerializer.Serialize(datos, JsonOpciones);
            File.WriteAllText(ruta, json, Encoding.UTF8);
        }
        catch
        {
            // Evita interrumpir el flujo de la UI por errores puntuales de E/S
        }
    }
}
