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

public enum MetodoInstalacionActualizacion
{
    ManualNavegador = 0,
    WindowsInstalador,
    WindowsPortable,
    LinuxAppImage,
    LinuxArchPacman,
    LinuxDeb,
    LinuxRpm,
    LinuxPortableTarGz
}

public sealed class AssetActualizacion
{
    public string Nombre { get; init; } = string.Empty;
    public string UrlDescarga { get; init; } = string.Empty;
    public long TamanoBytes { get; init; }

    public string TamanoFormateado => TamanoBytes switch
    {
        <= 0 => "Tamaño desconocido",
        < 1024 * 1024 => $"{TamanoBytes / 1024.0:F1} KB",
        _ => $"{TamanoBytes / (1024.0 * 1024.0):F1} MB"
    };
}

public sealed class ProgresoActualizacion
{
    public long BytesRecibidos { get; init; }
    public long TotalBytes { get; init; }
    public double Porcentaje { get; init; }
    public string EstadoTexto { get; init; } = string.Empty;
}

public sealed class ResultadoDescargaActualizacion
{
    public bool Exito { get; init; }
    public string RutaArchivo { get; init; } = string.Empty;
    public bool Sha256Verificado { get; init; }
    public string Mensaje { get; init; } = string.Empty;
}

public sealed class InformacionActualizacion
{
    public bool HayActualizacion { get; init; }
    public string VersionActual { get; init; } = "5.0.0";
    public string VersionDisponible { get; init; } = "5.0.0";
    public string Notas { get; init; } = string.Empty;
    public string UrlDescarga { get; init; } = "https://github.com/CynexStudios/alba-ecosistema/releases";
    public string MensajeEstado { get; init; } = string.Empty;
    public AssetActualizacion? AssetRecomendado { get; init; }
    public string? UrlChecksumsSha256 { get; init; }
    public MetodoInstalacionActualizacion MetodoInstalacion { get; init; } = MetodoInstalacionActualizacion.ManualNavegador;
    public string DescripcionEntorno { get; init; } = string.Empty;

    public bool PuedeInstalarDirectamente =>
        AssetRecomendado is not null && MetodoInstalacion != MetodoInstalacionActualizacion.ManualNavegador;
}
