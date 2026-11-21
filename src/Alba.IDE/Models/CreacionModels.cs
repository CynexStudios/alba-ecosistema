namespace Alba.IDE.Models;

public enum ModalidadCreacion
{
    Proyecto,
    Programa,
    ArchivoIndependiente
}

public enum LenguajeSoportado
{
    Cibao,
    Python,
    Html,
    CSharp,
    Cpp,
    JavaScript,
    TypeScript,
    Json
}

public sealed class EstadoEntornoLenguaje
{
    public LenguajeSoportado Lenguaje { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string ExtensionPrincipal { get; init; } = string.Empty;
    public bool Disponible { get; init; }
    public bool RequiereSdkExterno { get; init; }
    public string VersionDetectada { get; init; } = string.Empty;
    public string ComandoEjecutable { get; init; } = string.Empty;
    public string EstadoResumen => Disponible
        ? (string.IsNullOrWhiteSpace(VersionDetectada) ? "Listo (integrado)" : $"Instalado ({VersionDetectada})")
        : "No detectado en el sistema";
}

public sealed class PlantillaCreacion
{
    public string Id { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public LenguajeSoportado Lenguaje { get; init; }
    public ModalidadCreacion Modalidad { get; init; }
    public string ExtensionArchivoPrincipal { get; init; } = ".cibao";
    public string ArchivoPrincipalSugerido { get; init; } = "principal.cibao";
    public string DependenciasRecomendadas { get; init; } = "Ninguna";
    public bool RequiereCompiladorExterno { get; init; }
    public bool DisponibleEnEquipo { get; set; } = true;
    public string MotivoNoDisponible { get; set; } = string.Empty;

    public string EtiquetaEstado => DisponibleEnEquipo
        ? DependenciasRecomendadas
        : $"Requiere: {MotivoNoDisponible}";
}

public sealed class SolicitudCreacion
{
    public ModalidadCreacion Modalidad { get; init; }
    public LenguajeSoportado Lenguaje { get; init; }
    public PlantillaCreacion Plantilla { get; init; } = new();
    public string Nombre { get; init; } = string.Empty;
    public string CarpetaDestino { get; init; } = string.Empty;
    public bool InicializarGit { get; init; }
}
