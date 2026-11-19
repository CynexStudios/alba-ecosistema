namespace Alba.IDE.Models;

public sealed class ElementoReciente
{
    public string Ruta { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool EsProyecto { get; set; }
    public DateTime UltimoAccesoUtc { get; set; } = DateTime.UtcNow;
    public bool Fijado { get; set; }

    public string EtiquetaTipo => EsProyecto ? "PROYECTO" : "ARCHIVO";
    public string FechaTexto => UltimoAccesoUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
}

public sealed class SesionIDE
{
    public string? UltimoProyecto { get; set; }
    public List<string> PestanasAbiertas { get; set; } = [];
    public List<string> ArchivosIndependientes { get; set; } = [];
    public string? ArchivoActivo { get; set; }
}
