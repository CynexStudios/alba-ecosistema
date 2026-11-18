using Alba.IDE.Models;

namespace Alba.IDE.Services;

/// <summary>
/// Permite búsqueda global de archivos por nombre y búsqueda de texto dentro de un proyecto.
/// </summary>
public static class BuscadorProyecto
{
    private static readonly HashSet<string> CarpetasIgnoradas = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".vs", "bin", "obj", "build", "dist", "node_modules", "__pycache__"
    };

    private static readonly HashSet<string> ExtensionesBinarias = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".pdb", ".png", ".jpg", ".jpeg", ".gif", ".ico", ".icns",
        ".zip", ".tar", ".gz", ".7z", ".so", ".dylib", ".a", ".lib"
    };

    public static IReadOnlyList<string> ListarArchivosProyecto(string? carpetaProyecto)
    {
        if (string.IsNullOrWhiteSpace(carpetaProyecto) || !Directory.Exists(carpetaProyecto))
        {
            return [];
        }

        var archivos = new List<string>();
        Recorrer(carpetaProyecto, archivos);
        return archivos;
    }

    public static IReadOnlyList<ResultadoBusquedaProyecto> BuscarTextoEnProyecto(
        string? carpetaProyecto,
        string termino,
        bool distinguirMayusculas,
        int limite = 250)
    {
        if (string.IsNullOrWhiteSpace(carpetaProyecto) ||
            !Directory.Exists(carpetaProyecto) ||
            string.IsNullOrEmpty(termino))
        {
            return [];
        }

        StringComparison comparacion = distinguirMayusculas
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var resultados = new List<ResultadoBusquedaProyecto>();
        string raizNorm = Path.GetFullPath(carpetaProyecto);

        foreach (string archivo in ListarArchivosProyecto(raizNorm))
        {
            if (ExtensionesBinarias.Contains(Path.GetExtension(archivo)))
            {
                continue;
            }

            try
            {
                var info = new FileInfo(archivo);
                if (info.Length > 2 * 1024 * 1024)
                {
                    continue;
                }

                string[] lineas = File.ReadAllLines(archivo);
                string relativa = Path.GetRelativePath(raizNorm, archivo);

                for (int i = 0; i < lineas.Length; i++)
                {
                    int col = lineas[i].IndexOf(termino, comparacion);
                    if (col >= 0)
                    {
                        resultados.Add(new ResultadoBusquedaProyecto
                        {
                            RutaArchivo = archivo,
                            RutaRelativa = relativa,
                            Linea = i + 1,
                            Columna = col + 1,
                            VistaPrevia = lineas[i].Trim()
                        });

                        if (resultados.Count >= limite)
                        {
                            return resultados;
                        }
                    }
                }
            }
            catch
            {
                // Ignora archivos sin permiso de lectura o bloqueados
            }
        }

        return resultados;
    }

    private static void Recorrer(string directorio, List<string> acumulador)
    {
        try
        {
            foreach (string sub in Directory.GetDirectories(directorio))
            {
                string nombre = Path.GetFileName(sub);
                if (nombre.StartsWith('.') || CarpetasIgnoradas.Contains(nombre))
                {
                    continue;
                }
                Recorrer(sub, acumulador);
            }

            foreach (string archivo in Directory.GetFiles(directorio))
            {
                acumulador.Add(archivo);
            }
        }
        catch
        {
            // Ignora directorios inaccesibles
        }
    }
}
