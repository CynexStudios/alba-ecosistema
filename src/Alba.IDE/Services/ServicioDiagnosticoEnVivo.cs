using Alba.Engine.Diagnostics;
using Alba.Engine.Parser;
using Alba.IDE.Models;

namespace Alba.IDE.Services;

/// <summary>
/// Analiza archivos Alba (.alb) sin modificar el motor del lenguaje para reportar
/// errores léxicos y sintácticos en tiempo real dentro del editor y panel de Problemas.
/// </summary>
public sealed class ServicioDiagnosticoEnVivo
{
    public IReadOnlyList<ProblemaCodigo> AnalizarDocumento(string rutaArchivo, string codigoFuente)
    {
        if (string.IsNullOrWhiteSpace(rutaArchivo) ||
            !Path.GetExtension(rutaArchivo).Equals(".alb", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        string nombre = Path.GetFileName(rutaArchivo);
        var (lexico, sintactico) = AnalizadorSintactico.AnalizarCodigo(codigoFuente ?? string.Empty, nombre);

        var problemas = new List<ProblemaCodigo>();
        foreach (ErrorAlba err in lexico.Errores)
        {
            problemas.Add(Convertir(rutaArchivo, nombre, err));
        }

        foreach (ErrorAlba err in sintactico.Errores)
        {
            problemas.Add(Convertir(rutaArchivo, nombre, err));
        }

        return problemas;
    }

    private static ProblemaCodigo Convertir(string ruta, string nombre, ErrorAlba err) => new()
    {
        RutaArchivo = ruta,
        NombreArchivo = nombre,
        Codigo = err.Codigo,
        Severidad = "Error",
        Linea = Math.Max(1, err.Ubicacion.Linea),
        Columna = Math.Max(1, err.Ubicacion.Columna),
        Longitud = 1,
        Mensaje = err.Mensaje
    };
}
