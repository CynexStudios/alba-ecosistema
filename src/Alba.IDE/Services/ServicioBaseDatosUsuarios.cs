using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Alba.IDE.Models;
using Microsoft.Data.Sqlite;

namespace Alba.IDE.Services;

/// <summary>
/// Base de datos SQLite local embebida para cuentas propias de Xynex Code Editor 9.0,
/// sesiones seguras, protección contra fuerza bruta y bóveda cifrada de credenciales IA del usuario.
/// </summary>
public sealed partial class ServicioBaseDatosUsuarios
{
    private const int MaxIntentosFallidos = 5;
    private static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(5);

    private readonly string _rutaDb;
    private readonly string _rutaClaveMaestra;

    public ServicioBaseDatosUsuarios(string? rutaBaseDatos = null)
    {
        _rutaDb = string.IsNullOrWhiteSpace(rutaBaseDatos)
            ? RutasUsuarioAlba.ObtenerArchivoBaseDatosUsuarios()
            : rutaBaseDatos;

        string dir = Path.GetDirectoryName(_rutaDb) ?? RutasUsuarioAlba.ObtenerDirectorioDatosUsuario();
        Directory.CreateDirectory(dir);
        _rutaClaveMaestra = Path.Combine(dir, "vault9.key");

        InicializarBaseDatos();
    }

    private string CadenaConexion => $"Data Source={_rutaDb};Mode=ReadWriteCreate;Cache=Shared";

    public void InicializarBaseDatos()
    {
        using var conn = new SqliteConnection(CadenaConexion);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA foreign_keys=ON;

            CREATE TABLE IF NOT EXISTS usuarios (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                nombre_usuario TEXT NOT NULL UNIQUE COLLATE NOCASE,
                correo TEXT NOT NULL UNIQUE COLLATE NOCASE,
                nombre_mostrar TEXT NOT NULL,
                hash_contrasena TEXT NOT NULL,
                sal_contrasena TEXT NOT NULL,
                intentos_fallidos INTEGER NOT NULL DEFAULT 0,
                bloqueado_hasta_utc TEXT NULL,
                creado_utc TEXT NOT NULL,
                ultimo_acceso_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS sesiones (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                usuario_id INTEGER NOT NULL,
                token_sesion TEXT NOT NULL,
                actualizado_utc TEXT NOT NULL,
                FOREIGN KEY (usuario_id) REFERENCES usuarios(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS perfiles_ia (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                usuario_id INTEGER NOT NULL DEFAULT 0,
                proveedor TEXT NOT NULL,
                nombre_perfil TEXT NOT NULL,
                endpoint_url TEXT NOT NULL,
                modelo TEXT NOT NULL,
                api_key_cifrada TEXT NOT NULL,
                es_activo INTEGER NOT NULL DEFAULT 0,
                actualizado_utc TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public ResultadoOperacionCuenta RegistrarUsuario(
        string nombreUsuario,
        string correo,
        string nombreMostrar,
        string contrasena)
    {
        string user = (nombreUsuario ?? string.Empty).Trim();
        string mail = (correo ?? string.Empty).Trim();
        string display = string.IsNullOrWhiteSpace(nombreMostrar) ? user : nombreMostrar.Trim();

        if (user.Length < 3 || !RegexUsuario().IsMatch(user))
        {
            return new ResultadoOperacionCuenta
            {
                Exito = false,
                Mensaje = "El nombre de usuario debe tener entre 3 y 32 caracteres (letras, números, '_' o '-')."
            };
        }

        if (!mail.Contains('@') || !mail.Contains('.'))
        {
            return new ResultadoOperacionCuenta
            {
                Exito = false,
                Mensaje = "Ingresa un correo electrónico válido."
            };
        }

        if (string.IsNullOrEmpty(contrasena) || contrasena.Length < 8)
        {
            return new ResultadoOperacionCuenta
            {
                Exito = false,
                Mensaje = "Por seguridad, la contraseña debe tener al menos 8 caracteres."
            };
        }

        try
        {
            var (hash, sal) = ServicioSeguridad.GenerarHashContrasena(contrasena);
            string ahora = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

            using var conn = new SqliteConnection(CadenaConexion);
            conn.Open();

            using (var check = conn.CreateCommand())
            {
                check.CommandText = "SELECT COUNT(1) FROM usuarios WHERE nombre_usuario = @u OR correo = @c;";
                check.Parameters.AddWithValue("@u", user);
                check.Parameters.AddWithValue("@c", mail);
                long existe = Convert.ToInt64(check.ExecuteScalar() ?? 0);
                if (existe > 0)
                {
                    return new ResultadoOperacionCuenta
                    {
                        Exito = false,
                        Mensaje = "Ya existe una cuenta registrada con ese usuario o correo."
                    };
                }
            }

            long nuevoId;
            using (var ins = conn.CreateCommand())
            {
                ins.CommandText = """
                    INSERT INTO usuarios (nombre_usuario, correo, nombre_mostrar, hash_contrasena, sal_contrasena, creado_utc, ultimo_acceso_utc)
                    VALUES (@u, @c, @m, @h, @s, @now, @now);
                    SELECT last_insert_rowid();
                    """;
                ins.Parameters.AddWithValue("@u", user);
                ins.Parameters.AddWithValue("@c", mail);
                ins.Parameters.AddWithValue("@m", display);
                ins.Parameters.AddWithValue("@h", hash);
                ins.Parameters.AddWithValue("@s", sal);
                ins.Parameters.AddWithValue("@now", ahora);
                nuevoId = Convert.ToInt64(ins.ExecuteScalar() ?? 0);
            }

            GuardarSesionActiva(conn, (int)nuevoId);

            return new ResultadoOperacionCuenta
            {
                Exito = true,
                Mensaje = $"Cuenta '{user}' creada y sesión iniciada de forma segura.",
                Cuenta = new CuentaUsuario
                {
                    Id = (int)nuevoId,
                    NombreUsuario = user,
                    Correo = mail,
                    NombreMostrar = display,
                    CreadoUtc = DateTime.UtcNow,
                    UltimoAccesoUtc = DateTime.UtcNow
                }
            };
        }
        catch (Exception ex)
        {
            return new ResultadoOperacionCuenta
            {
                Exito = false,
                Mensaje = $"No se pudo registrar la cuenta: {ex.Message}"
            };
        }
    }

    public ResultadoOperacionCuenta IniciarSesion(string usuarioOCorreo, string contrasena)
    {
        string identificador = (usuarioOCorreo ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(identificador) || string.IsNullOrEmpty(contrasena))
        {
            return new ResultadoOperacionCuenta { Exito = false, Mensaje = "Completa usuario/correo y contraseña." };
        }

        try
        {
            using var conn = new SqliteConnection(CadenaConexion);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT id, nombre_usuario, correo, nombre_mostrar, hash_contrasena, sal_contrasena,
                       intentos_fallidos, bloqueado_hasta_utc, creado_utc, ultimo_acceso_utc
                FROM usuarios
                WHERE nombre_usuario = @id OR correo = @id
                LIMIT 1;
                """;
            cmd.Parameters.AddWithValue("@id", identificador);

            using SqliteDataReader reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                return new ResultadoOperacionCuenta { Exito = false, Mensaje = "Credenciales inválidas." };
            }

            int id = reader.GetInt32(0);
            string nombreUsuario = reader.GetString(1);
            string correo = reader.GetString(2);
            string nombreMostrar = reader.GetString(3);
            string hash = reader.GetString(4);
            string sal = reader.GetString(5);
            int intentos = reader.GetInt32(6);
            string? bloqueadoRaw = reader.IsDBNull(7) ? null : reader.GetString(7);
            DateTime creadoUtc = DateTime.Parse(reader.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            reader.Close();

            if (!string.IsNullOrWhiteSpace(bloqueadoRaw) &&
                DateTime.TryParse(bloqueadoRaw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime bloqueadoHasta) &&
                bloqueadoHasta > DateTime.UtcNow)
            {
                int minutosRestantes = (int)Math.Ceiling((bloqueadoHasta - DateTime.UtcNow).TotalMinutes);
                return new ResultadoOperacionCuenta
                {
                    Exito = false,
                    Mensaje = $"Cuenta protegida temporalmente por múltiples intentos fallidos. Espera {minutosRestantes} min."
                };
            }

            if (!ServicioSeguridad.VerificarContrasena(contrasena, hash, sal))
            {
                int nuevosIntentos = intentos + 1;
                string? nuevoBloqueo = nuevosIntentos >= MaxIntentosFallidos
                    ? DateTime.UtcNow.Add(DuracionBloqueo).ToString("O", CultureInfo.InvariantCulture)
                    : null;

                using var updFail = conn.CreateCommand();
                updFail.CommandText = "UPDATE usuarios SET intentos_fallidos = @n, bloqueado_hasta_utc = @b WHERE id = @id;";
                updFail.Parameters.AddWithValue("@n", nuevosIntentos >= MaxIntentosFallidos ? 0 : nuevosIntentos);
                updFail.Parameters.AddWithValue("@b", (object?)nuevoBloqueo ?? DBNull.Value);
                updFail.Parameters.AddWithValue("@id", id);
                updFail.ExecuteNonQuery();

                return new ResultadoOperacionCuenta
                {
                    Exito = false,
                    Mensaje = nuevosIntentos >= MaxIntentosFallidos
                        ? "Demasiados intentos fallidos. Cuenta bloqueada 5 minutos por seguridad."
                        : "Credenciales inválidas."
                };
            }

            string ahora = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            using (var updOk = conn.CreateCommand())
            {
                updOk.CommandText = "UPDATE usuarios SET intentos_fallidos = 0, bloqueado_hasta_utc = NULL, ultimo_acceso_utc = @now WHERE id = @id;";
                updOk.Parameters.AddWithValue("@now", ahora);
                updOk.Parameters.AddWithValue("@id", id);
                updOk.ExecuteNonQuery();
            }

            GuardarSesionActiva(conn, id);

            return new ResultadoOperacionCuenta
            {
                Exito = true,
                Mensaje = $"Bienvenido de nuevo, {nombreMostrar}.",
                Cuenta = new CuentaUsuario
                {
                    Id = id,
                    NombreUsuario = nombreUsuario,
                    Correo = correo,
                    NombreMostrar = nombreMostrar,
                    CreadoUtc = creadoUtc,
                    UltimoAccesoUtc = DateTime.UtcNow
                }
            };
        }
        catch (Exception ex)
        {
            return new ResultadoOperacionCuenta { Exito = false, Mensaje = $"Error al iniciar sesión: {ex.Message}" };
        }
    }

    public CuentaUsuario? ObtenerUsuarioActivo()
    {
        try
        {
            using var conn = new SqliteConnection(CadenaConexion);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT u.id, u.nombre_usuario, u.correo, u.nombre_mostrar, u.creado_utc, u.ultimo_acceso_utc
                FROM sesiones s
                INNER JOIN usuarios u ON u.id = s.usuario_id
                WHERE s.id = 1
                LIMIT 1;
                """;
            using SqliteDataReader r = cmd.ExecuteReader();
            if (!r.Read())
            {
                return null;
            }

            return new CuentaUsuario
            {
                Id = r.GetInt32(0),
                NombreUsuario = r.GetString(1),
                Correo = r.GetString(2),
                NombreMostrar = r.GetString(3),
                CreadoUtc = DateTime.Parse(r.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                UltimoAccesoUtc = DateTime.Parse(r.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
            };
        }
        catch
        {
            return null;
        }
    }

    public void CerrarSesion()
    {
        try
        {
            using var conn = new SqliteConnection(CadenaConexion);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM sesiones WHERE id = 1;";
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // Ignora errores de cierre de sesión
        }
    }

    public PerfilIAConectado GuardarPerfilIA(
        int usuarioId,
        ProveedorIAUsuario proveedor,
        string nombrePerfil,
        string endpointUrl,
        string modelo,
        string? apiKeyPlana,
        bool marcarActivo = true)
    {
        string urlLimpia = endpointUrl.Trim();
        if (!ServicioSeguridad.EsUrlHttpSegura(urlLimpia, permitirHttpLocalhost: true))
        {
            throw new ArgumentException("La URL del proveedor IA debe usar HTTPS o http://localhost / http://127.0.0.1.");
        }

        string apiKeyCifrada = string.IsNullOrWhiteSpace(apiKeyPlana)
            ? string.Empty
            : ServicioSeguridad.CifrarSecretoTexto(apiKeyPlana.Trim(), _rutaClaveMaestra);

        string ahora = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        using var conn = new SqliteConnection(CadenaConexion);
        conn.Open();

        if (marcarActivo)
        {
            using var reset = conn.CreateCommand();
            reset.CommandText = "UPDATE perfiles_ia SET es_activo = 0 WHERE usuario_id = @uid;";
            reset.Parameters.AddWithValue("@uid", usuarioId);
            reset.ExecuteNonQuery();
        }

        long nuevoId;
        using (var ins = conn.CreateCommand())
        {
            ins.CommandText = """
                INSERT INTO perfiles_ia (usuario_id, proveedor, nombre_perfil, endpoint_url, modelo, api_key_cifrada, es_activo, actualizado_utc)
                VALUES (@uid, @prov, @nom, @url, @mod, @key, @act, @now);
                SELECT last_insert_rowid();
                """;
            ins.Parameters.AddWithValue("@uid", usuarioId);
            ins.Parameters.AddWithValue("@prov", proveedor.ToString());
            ins.Parameters.AddWithValue("@nom", string.IsNullOrWhiteSpace(nombrePerfil) ? proveedor.ToString() : nombrePerfil.Trim());
            ins.Parameters.AddWithValue("@url", urlLimpia);
            ins.Parameters.AddWithValue("@mod", modelo.Trim());
            ins.Parameters.AddWithValue("@key", apiKeyCifrada);
            ins.Parameters.AddWithValue("@act", marcarActivo ? 1 : 0);
            ins.Parameters.AddWithValue("@now", ahora);
            nuevoId = Convert.ToInt64(ins.ExecuteScalar() ?? 0);
        }

        return new PerfilIAConectado
        {
            Id = (int)nuevoId,
            UsuarioId = usuarioId,
            Proveedor = proveedor,
            NombrePerfil = nombrePerfil.Trim(),
            EndpointUrl = urlLimpia,
            Modelo = modelo.Trim(),
            ApiKeyCifrada = apiKeyCifrada,
            EsActivo = marcarActivo,
            ActualizadoUtc = DateTime.UtcNow
        };
    }

    public IReadOnlyList<PerfilIAConectado> ListarPerfilesIA(int usuarioId)
    {
        var lista = new List<PerfilIAConectado>();
        try
        {
            using var conn = new SqliteConnection(CadenaConexion);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT id, usuario_id, proveedor, nombre_perfil, endpoint_url, modelo, api_key_cifrada, es_activo, actualizado_utc
                FROM perfiles_ia
                WHERE usuario_id = @uid
                ORDER BY es_activo DESC, id DESC;
                """;
            cmd.Parameters.AddWithValue("@uid", usuarioId);

            using SqliteDataReader r = cmd.ExecuteReader();
            while (r.Read())
            {
                if (!Enum.TryParse(r.GetString(2), out ProveedorIAUsuario prov))
                {
                    prov = ProveedorIAUsuario.OpenAICompatible;
                }

                lista.Add(new PerfilIAConectado
                {
                    Id = r.GetInt32(0),
                    UsuarioId = r.GetInt32(1),
                    Proveedor = prov,
                    NombrePerfil = r.GetString(3),
                    EndpointUrl = r.GetString(4),
                    Modelo = r.GetString(5),
                    ApiKeyCifrada = r.GetString(6),
                    EsActivo = r.GetInt32(7) == 1,
                    ActualizadoUtc = DateTime.Parse(r.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                });
            }
        }
        catch
        {
            // Retorna lista vacía si aún no hay perfiles
        }

        return lista;
    }

    public void ActivarPerfilIA(int usuarioId, int perfilId)
    {
        using var conn = new SqliteConnection(CadenaConexion);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE perfiles_ia SET es_activo = CASE WHEN id = @pid THEN 1 ELSE 0 END
            WHERE usuario_id = @uid;
            """;
        cmd.Parameters.AddWithValue("@uid", usuarioId);
        cmd.Parameters.AddWithValue("@pid", perfilId);
        cmd.ExecuteNonQuery();
    }

    public void EliminarPerfilIA(int usuarioId, int perfilId)
    {
        using var conn = new SqliteConnection(CadenaConexion);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM perfiles_ia WHERE usuario_id = @uid AND id = @pid;";
        cmd.Parameters.AddWithValue("@uid", usuarioId);
        cmd.Parameters.AddWithValue("@pid", perfilId);
        cmd.ExecuteNonQuery();
    }

    public string DescifrarApiKeyPerfil(PerfilIAConectado perfil) =>
        ServicioSeguridad.DescifrarSecretoTexto(perfil.ApiKeyCifrada, _rutaClaveMaestra);

    private static void GuardarSesionActiva(SqliteConnection conn, int usuarioId)
    {
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        string ahora = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO sesiones (id, usuario_id, token_sesion, actualizado_utc)
            VALUES (1, @uid, @tok, @now)
            ON CONFLICT(id) DO UPDATE SET
                usuario_id = excluded.usuario_id,
                token_sesion = excluded.token_sesion,
                actualizado_utc = excluded.actualizado_utc;
            """;
        cmd.Parameters.AddWithValue("@uid", usuarioId);
        cmd.Parameters.AddWithValue("@tok", token);
        cmd.Parameters.AddWithValue("@now", ahora);
        cmd.ExecuteNonQuery();
    }

    [GeneratedRegex(@"^[a-zA-Z0-9_-]{3,32}$")]
    private static partial Regex RegexUsuario();
}
