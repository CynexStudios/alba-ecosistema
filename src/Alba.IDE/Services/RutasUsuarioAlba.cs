namespace Alba.IDE.Services;

/// <summary>
/// Separa de forma estricta los directorios de solo lectura de la aplicación
/// respecto a la configuración, addons y proyectos del usuario.
/// </summary>
public static class RutasUsuarioAlba
{
    public const string VersionActual = "2.0.0";
    public const string RepositorioGitHub = "CynexStudios/alba-ecosistema";

    public static string ObtenerDirectorioDatosUsuario()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        string dir = Path.Combine(appData, "AlbaStudio");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string ObtenerArchivoConfiguracion() =>
        Path.Combine(ObtenerDirectorioDatosUsuario(), "configuracion.json");

    public static string ObtenerArchivoRecientes() =>
        Path.Combine(ObtenerDirectorioDatosUsuario(), "recientes.json");

    public static string ObtenerArchivoSesion() =>
        Path.Combine(ObtenerDirectorioDatosUsuario(), "sesion.json");

    public static string ObtenerDirectorioAddonsUsuario()
    {
        string dir = Path.Combine(ObtenerDirectorioDatosUsuario(), "addons");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string ObtenerArchivoConfigAddonsUsuario() =>
        Path.Combine(ObtenerDirectorioDatosUsuario(), "addons-config.json");

    public static string ObtenerCarpetaProyectosPorDefecto()
    {
        string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string baseDir = !string.IsNullOrWhiteSpace(docs)
            ? docs
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        string carpeta = Path.Combine(baseDir, "ProyectosAlba");
        Directory.CreateDirectory(carpeta);
        return carpeta;
    }

    public static string? BuscarDirectorioInstalacion(string subcarpeta)
    {
        DirectoryInfo? actual = new(AppContext.BaseDirectory);
        for (int i = 0; i < 6 && actual is not null; i++)
        {
            string candidato = Path.Combine(actual.FullName, subcarpeta);
            if (Directory.Exists(candidato))
            {
                return candidato;
            }

            string enResources = Path.Combine(actual.FullName, "Resources", subcarpeta);
            if (Directory.Exists(enResources))
            {
                return enResources;
            }

            actual = actual.Parent;
        }

        string enTrabajo = Path.Combine(Directory.GetCurrentDirectory(), subcarpeta);
        return Directory.Exists(enTrabajo) ? enTrabajo : null;
    }
}
