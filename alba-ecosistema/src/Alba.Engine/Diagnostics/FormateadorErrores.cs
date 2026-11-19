using System.Text;

namespace Alba.Engine.Diagnostics;

/// <summary>
/// Formatea diagnósticos de Alba en español siguiendo el estándar oficial del ecosistema.
/// </summary>
public static class FormateadorErrores
{
    public static string ObtenerEncabezado(TipoError tipo) => tipo switch
    {
        TipoError.Lexico => "ERROR LÉXICO",
        TipoError.Sintactico => "ERROR SINTÁCTICO",
        TipoError.Tipo => "ERROR DE TIPO",
        TipoError.Ejecucion => "ERROR DE EJECUCIÓN",
        _ => "ERROR"
    };

    public static string Formatear(ErrorAlba error)
    {
        var sb = new StringBuilder();
        sb.AppendLine(ObtenerEncabezado(error.Tipo));
        sb.AppendLine();
        sb.AppendLine($"Archivo: {error.Ubicacion.Archivo}");
        sb.AppendLine($"Línea: {error.Ubicacion.Linea}");
        sb.AppendLine($"Columna: {error.Ubicacion.Columna}");
        sb.AppendLine();
        sb.AppendLine($"Descripción: [{error.Codigo}] {error.Mensaje}");

        if (!string.IsNullOrEmpty(error.LineaCodigo))
        {
            sb.AppendLine();
            sb.AppendLine("Código:");
            string prefijo = $"{error.Ubicacion.Linea} | ";
            string lineaLimpia = error.LineaCodigo.Replace("\t", "    ");
            sb.AppendLine($"{prefijo}{lineaLimpia}");

            int desplazamiento = CalcularDesplazamientoVisual(error.LineaCodigo, error.Ubicacion.Columna);
            int ancho = Math.Max(1, error.Ubicacion.Longitud);
            string marcador = new string(' ', prefijo.Length + desplazamiento) + new string('^', ancho);
            sb.AppendLine(marcador);
        }

        return sb.ToString().TrimEnd();
    }

    public static string ExtraerLinea(string codigoFuente, int numeroLinea)
    {
        if (string.IsNullOrEmpty(codigoFuente) || numeroLinea < 1)
        {
            return string.Empty;
        }

        int lineaActual = 1;
        int inicio = 0;

        for (int i = 0; i < codigoFuente.Length; i++)
        {
            if (codigoFuente[i] == '\n')
            {
                if (lineaActual == numeroLinea)
                {
                    int fin = (i > inicio && codigoFuente[i - 1] == '\r') ? i - 1 : i;
                    return codigoFuente.Substring(inicio, fin - inicio);
                }

                lineaActual++;
                inicio = i + 1;
            }
        }

        if (lineaActual == numeroLinea && inicio <= codigoFuente.Length)
        {
            string ultima = codigoFuente.Substring(inicio);
            return ultima.TrimEnd('\r');
        }

        return string.Empty;
    }

    private static int CalcularDesplazamientoVisual(string lineaOriginal, int columna1Based)
    {
        int indiceMax = Math.Min(Math.Max(0, columna1Based - 1), lineaOriginal.Length);
        int visual = 0;
        for (int i = 0; i < indiceMax; i++)
        {
            visual += lineaOriginal[i] == '\t' ? 4 : 1;
        }
        return visual;
    }
}
