namespace Alba.IDE.Models;

public sealed class ProblemaCodigo
{
    public string RutaArchivo { get; set; } = string.Empty;
    public string NombreArchivo { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Severidad { get; set; } = "Error";
    public int Linea { get; set; }
    public int Columna { get; set; }
    public int Longitud { get; set; } = 1;
    public string Mensaje { get; set; } = string.Empty;

    public string UbicacionTexto => $"{NombreArchivo} (Ln {Linea}, Col {Columna})";
}

public sealed class EntradaRegistro
{
    public DateTime MarcaTiempo { get; set; } = DateTime.Now;
    public string Categoria { get; set; } = "Sistema";
    public string Mensaje { get; set; } = string.Empty;

    public string HoraTexto => MarcaTiempo.ToString("HH:mm:ss");
}

public sealed class ResultadoBusquedaProyecto
{
    public string RutaArchivo { get; set; } = string.Empty;
    public string RutaRelativa { get; set; } = string.Empty;
    public int Linea { get; set; }
    public int Columna { get; set; }
    public string VistaPrevia { get; set; } = string.Empty;

    public string Etiqueta => $"{RutaRelativa}:{Linea}:{Columna}";
}

public sealed class ComandoPaleta
{
    public string Id { get; init; } = string.Empty;
    public string Titulo { get; init; } = string.Empty;
    public string Categoria { get; init; } = string.Empty;
    public string Atajo { get; init; } = string.Empty;
    public required Action Ejecutar { get; init; }
}

public sealed class InformacionActualizacion
{
    public bool HayActualizacion { get; init; }
    public string VersionActual { get; init; } = "5.0.0";
    public string VersionDisponible { get; init; } = "5.0.0";
    public string Notas { get; init; } = string.Empty;
    public string UrlDescarga { get; init; } = "https://github.com/CynexStudios/alba-ecosistema/releases";
    public string MensajeEstado { get; init; } = string.Empty;
}
