namespace Alba.IDE.Models;

public enum MetodoAutenticacionGitHub
{
    TokenPersonal,
    OAuthDeviceFlow
}

public sealed class CuentaGitHub
{
    public string Login { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string AvatarUrl { get; init; } = string.Empty;
    public string PerfilUrl { get; init; } = string.Empty;
    public int RepositoriosPublicos { get; init; }
    public int RepositoriosPrivados { get; init; }
    public string PermisosToken { get; init; } = string.Empty;
    public MetodoAutenticacionGitHub Metodo { get; init; }

    public string NombreMostrado => string.IsNullOrWhiteSpace(Nombre) ? Login : $"{Nombre} (@{Login})";
}

public sealed class RespuestaDeviceFlowGitHub
{
    public string DeviceCode { get; init; } = string.Empty;
    public string UserCode { get; init; } = string.Empty;
    public string VerificationUri { get; init; } = "https://github.com/login/device";
    public int ExpiresIn { get; init; } = 900;
    public int Interval { get; init; } = 5;
}

public sealed class RepositorioGitHub
{
    public long Id { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string NombreCompleto { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public bool EsPrivado { get; init; }
    public string CloneUrl { get; init; } = string.Empty;
    public string HtmlUrl { get; init; } = string.Empty;
    public string RamaPorDefecto { get; init; } = "main";
    public string LenguajePrincipal { get; init; } = string.Empty;
    public DateTime ActualizadoEnUtc { get; init; }

    public string EtiquetaVisibilidad => EsPrivado ? "Privado" : "Público";
    public string Resumen =>
        string.IsNullOrWhiteSpace(LenguajePrincipal)
            ? $"{EtiquetaVisibilidad} · rama {RamaPorDefecto}"
            : $"{EtiquetaVisibilidad} · {LenguajePrincipal} · rama {RamaPorDefecto}";
}

public sealed class ArchivoCambioGit
{
    public string RutaRelativa { get; init; } = string.Empty;
    public string CodigoEstado { get; init; } = "M";
    public bool EstaPreparado { get; init; }
    public bool EsNuevoSinRastrear { get; init; }

    public string EtiquetaEstado => EsNuevoSinRastrear
        ? "Nuevo"
        : (EstaPreparado ? $"Preparado ({CodigoEstado})" : $"Modificado ({CodigoEstado})");
}

public sealed class CommitHistorialGit
{
    public string HashCorto { get; init; } = string.Empty;
    public string Autor { get; init; } = string.Empty;
    public string FechaRelativa { get; init; } = string.Empty;
    public string Mensaje { get; init; } = string.Empty;

    public string Resumen => $"{HashCorto} · {Mensaje} ({Autor}, {FechaRelativa})";
}

public sealed class EstadoRepositorioGit
{
    public bool GitInstalado { get; init; }
    public bool EsRepositorio { get; init; }
    public string RamaActual { get; init; } = string.Empty;
    public string? RemotoOrigin { get; init; }
    public IReadOnlyList<string> Ramas { get; init; } = [];
    public IReadOnlyList<ArchivoCambioGit> Cambios { get; init; } = [];
    public IReadOnlyList<CommitHistorialGit> Historial { get; init; } = [];
}

public sealed class HallazgoSecretoGit
{
    public string RutaRelativa { get; init; } = string.Empty;
    public string Motivo { get; init; } = string.Empty;
    public int? NumeroLinea { get; init; }

    public string Resumen => NumeroLinea.HasValue
        ? $"{RutaRelativa} (línea {NumeroLinea}): {Motivo}"
        : $"{RutaRelativa}: {Motivo}";
}

public sealed class SolicitudPublicacionGitHub
{
    public string RutaProyecto { get; init; } = string.Empty;
    public string NombreRepositorio { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public bool EsPrivado { get; init; }
    public bool GenerarGitIgnore { get; init; } = true;
    public string MensajeCommitInicial { get; init; } = "Publicación inicial desde Xynex Code Editor 9.0";
}

public sealed class ResultadoOperacionGit
{
    public bool Exito { get; init; }
    public string Mensaje { get; init; } = string.Empty;
    public string Salida { get; init; } = string.Empty;
}
