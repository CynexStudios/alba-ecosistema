using System.Text;
using System.Text.Json;
using Alba.IDE.Models;

namespace Alba.IDE.Services;

/// <summary>
/// Administra la creación, apertura y manipulación de proyectos y archivos en disco.
/// </summary>
public sealed class GestorProyectos
{
    private static readonly JsonSerializerOptions JsonOpciones = new() { WriteIndented = true };

    public (string RutaProyecto, string RutaPrincipal) CrearProyecto(string carpetaPadre, string nombreProyecto)
    {
        string nombreSeguro = string.Join("_", nombreProyecto.Trim().Split(Path.GetInvalidFileNameChars()));
        if (string.IsNullOrWhiteSpace(nombreSeguro))
        {
            nombreSeguro = "MiProyectoAlba";
        }

        string rutaProyecto = Path.Combine(carpetaPadre, nombreSeguro);
        Directory.CreateDirectory(rutaProyecto);

        var metadatos = new
        {
            nombre = nombreSeguro,
            version = "9.0.0",
            lenguaje = "Cibao",
            archivoPrincipal = "principal.alb"
        };

        File.WriteAllText(
            Path.Combine(rutaProyecto, "proyecto.albaproj.json"),
            JsonSerializer.Serialize(metadatos, JsonOpciones),
            Encoding.UTF8);

        string rutaPrincipal = Path.Combine(rutaProyecto, "principal.alb");
        if (!File.Exists(rutaPrincipal))
        {
            string plantilla = $$"""
                // Proyecto: {{nombreSeguro}}
                // Creado con Xynex Code Editor v9.0.0 — Lenguaje Cibao

                funcion principal() {
                    mostrar($"¡Bienvenido a {{nombreSeguro}} en Cibao 9.0!")
                }

                principal()
                """;
            File.WriteAllText(rutaPrincipal, plantilla, Encoding.UTF8);
        }

        return (rutaProyecto, rutaPrincipal);
    }

    public NodoProyecto CargarArbolProyecto(string rutaRaiz)
    {
        string nombre = Path.GetFileName(rutaRaiz.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(nombre))
        {
            nombre = rutaRaiz;
        }

        var raiz = new NodoProyecto(nombre, rutaRaiz, esCarpeta: true, expandido: true);
        PoblarHijos(raiz);
        return raiz;
    }

    public string CrearArchivo(string carpetaDestino, string nombreArchivo)
    {
        string ruta = Path.Combine(carpetaDestino, nombreArchivo.Trim());
        if (!File.Exists(ruta))
        {
            File.WriteAllText(ruta, string.Empty, Encoding.UTF8);
        }
        return ruta;
    }

    public string CrearCarpeta(string carpetaDestino, string nombreCarpeta)
    {
        string ruta = Path.Combine(carpetaDestino, nombreCarpeta.Trim());
        Directory.CreateDirectory(ruta);
        return ruta;
    }

    public string LeerArchivo(string ruta) => File.ReadAllText(ruta, Encoding.UTF8);

    public void GuardarArchivo(string ruta, string contenido) => File.WriteAllText(ruta, contenido, Encoding.UTF8);

    public string RenombrarElemento(string rutaActual, string nuevoNombre, bool esCarpeta)
    {
        string? padre = Path.GetDirectoryName(rutaActual);
        if (string.IsNullOrWhiteSpace(padre))
        {
            return rutaActual;
        }

        string destino = Path.Combine(padre, nuevoNombre.Trim());
        if (destino.Equals(rutaActual, StringComparison.OrdinalIgnoreCase))
        {
            return rutaActual;
        }

        if (esCarpeta)
        {
            if (Directory.Exists(rutaActual) && !Directory.Exists(destino))
            {
                Directory.Move(rutaActual, destino);
            }
        }
        else
        {
            if (File.Exists(rutaActual) && !File.Exists(destino))
            {
                File.Move(rutaActual, destino);
            }
        }
        return destino;
    }

    public string? DuplicarArchivo(string rutaOrigen)
    {
        if (!File.Exists(rutaOrigen))
        {
            return null;
        }

        string dir = Path.GetDirectoryName(rutaOrigen) ?? string.Empty;
        string nombreSinExt = Path.GetFileNameWithoutExtension(rutaOrigen);
        string ext = Path.GetExtension(rutaOrigen);
        string candidato = Path.Combine(dir, $"{nombreSinExt}_copia{ext}");
        int idx = 2;
        while (File.Exists(candidato))
        {
            candidato = Path.Combine(dir, $"{nombreSinExt}_copia{idx}{ext}");
            idx++;
        }

        File.Copy(rutaOrigen, candidato);
        return candidato;
    }

    public void EliminarElemento(string ruta, bool esCarpeta)
    {
        if (esCarpeta)
        {
            if (Directory.Exists(ruta)) Directory.Delete(ruta, recursive: true);
        }
        else
        {
            if (File.Exists(ruta)) File.Delete(ruta);
        }
    }

    private static void PoblarHijos(NodoProyecto nodoCarpeta)
    {
        if (!Directory.Exists(nodoCarpeta.RutaCompleta))
        {
            return;
        }

        try
        {
            foreach (string dir in Directory.GetDirectories(nodoCarpeta.RutaCompleta)
                         .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase))
            {
                string nombreDir = Path.GetFileName(dir);
                if (nombreDir.StartsWith('.') || nombreDir is "bin" or "obj" or "build" or "__pycache__")
                {
                    continue;
                }

                var hijo = new NodoProyecto(nombreDir, dir, esCarpeta: true);
                PoblarHijos(hijo);
                nodoCarpeta.Hijos.Add(hijo);
            }

            foreach (string archivo in Directory.GetFiles(nodoCarpeta.RutaCompleta)
                         .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase))
            {
                nodoCarpeta.Hijos.Add(new NodoProyecto(Path.GetFileName(archivo), archivo, esCarpeta: false));
            }
        }
        catch
        {
            // Ignora carpetas sin permisos de lectura del sistema operativo
        }
    }
}
