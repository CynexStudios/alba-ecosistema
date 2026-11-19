namespace Alba.IDE.Models;

public enum CategoriaHerramienta
{
    Todas,
    Lenguajes,
    Compiladores,
    Interpretes,
    Sdk,
    Frameworks,
    Bibliotecas,
    Dependencias,
    Extensiones,
    Depuracion,
    DesarrolloWeb,
    AplicacionesGraficas,
    IntegracionesIA,
    Productividad
}

public sealed class CapacidadesLenguajeTool
{
    public bool SoporteEdicion { get; init; }
    public bool Autocompletado { get; init; }
    public bool CompiladorOInterprete { get; init; }
    public bool Depurador { get; init; }
    public bool FrameworksYBibliotecas { get; init; }
    public bool HerramientasConstruccion { get; init; }

    public string Resumen =>
        $"Edición: {(SoporteEdicion ? "Sí" : "No")} · " +
        $"Completado: {(Autocompletado ? "Sí" : "No")} · " +
        $"Motor: {(CompiladorOInterprete ? "Sí" : "No")} · " +
        $"Depuración: {(Depurador ? "Sí" : "No")} · " +
        $"Bibliotecas: {(FrameworksYBibliotecas ? "Sí" : "No")} · " +
        $"Construcción: {(HerramientasConstruccion ? "Sí" : "No")}";
}

public sealed class ElementoTool
{
    public string Id { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public string Version { get; init; } = "1.0.0";
    public string Autor { get; init; } = "CynexStudios";
    public CategoriaHerramienta Categoria { get; init; } = CategoriaHerramienta.Lenguajes;
    public string CategoriaTexto { get; init; } = "Lenguajes";
    public string Compatibilidad { get; init; } = "Windows · Linux · macOS";
    public string FuenteOficial { get; init; } = string.Empty;
    public string Requisitos { get; init; } = string.Empty;
    public string Dependencias { get; init; } = "Ninguna";
    public string HashVerificacion { get; init; } = "Integridad verificada (paquete local)";
    public string FirmaDigital { get; init; } = "CynexStudios / Fuente oficial";
    public string InstruccionesInstalacion { get; init; } = string.Empty;
    public string PermisosRequeridos { get; init; } = "Lectura del entorno local";
    public string ComandoVerificacion { get; init; } = string.Empty;
    public string? ComandoInstalacionGuiada { get; init; }
    public string? PaqueteAddonIncorporadoId { get; init; }
    public bool Instalado { get; set; }
    public bool Activo { get; set; }
    public bool EsAddonLocal { get; set; }
    public bool EsIntegrado { get; set; }
    public bool TieneActualizacionDisponible { get; set; }
    public string EstadoInstalacion { get; set; } = "Disponible";
    public string QueFaltaTexto { get; set; } = string.Empty;
    public string? RutaAddon { get; set; }
    public CapacidadesLenguajeTool? Capacidades { get; init; }

    public string ResumenCapacidades => Capacidades?.Resumen ?? "Herramienta general del ecosistema";
}
