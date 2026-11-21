using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Alba.IDE.Services;

/// <summary>
/// Capa de seguridad de alta resistencia de Xynex Code Editor 9.0:
/// - Hash de contraseñas PBKDF2-HMAC-SHA512 (310,000 iteraciones + sal aleatoria de 128 bits)
/// - Cifrado de bóveda para claves IA / tokens (DPAPI en Windows, AES-256-GCM en Linux/macOS)
/// - Validación estricta contra Path Traversal e inyección de esquemas de URL
/// </summary>
public static class ServicioSeguridad
{
    private const int IteracionesPbkdf2 = 310_000;
    private const int TamanoSalBytes = 16;
    private const int TamanoHashBytes = 64;

    public static (string HashBase64, string SalBase64) GenerarHashContrasena(string contrasena)
    {
        if (string.IsNullOrEmpty(contrasena) || contrasena.Length < 6)
        {
            throw new ArgumentException("La contraseña debe tener al menos 6 caracteres.");
        }

        byte[] sal = RandomNumberGenerator.GetBytes(TamanoSalBytes);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(contrasena),
            sal,
            IteracionesPbkdf2,
            HashAlgorithmName.SHA512,
            TamanoHashBytes);

        return (Convert.ToBase64String(hash), Convert.ToBase64String(sal));
    }

    public static bool VerificarContrasena(string contrasena, string hashBase64, string salBase64)
    {
        if (string.IsNullOrEmpty(contrasena) ||
            string.IsNullOrWhiteSpace(hashBase64) ||
            string.IsNullOrWhiteSpace(salBase64))
        {
            return false;
        }

        try
        {
            byte[] sal = Convert.FromBase64String(salBase64);
            byte[] esperado = Convert.FromBase64String(hashBase64);
            byte[] calculado = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(contrasena),
                sal,
                IteracionesPbkdf2,
                HashAlgorithmName.SHA512,
                esperado.Length);

            return CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }
        catch
        {
            return false;
        }
    }

    public static string CifrarSecretoTexto(string textoPlano, string? rutaClaveMaestra = null)
    {
        if (string.IsNullOrEmpty(textoPlano))
        {
            return string.Empty;
        }

        byte[] datos = Encoding.UTF8.GetBytes(textoPlano);
        byte[] paquete;

        if (OperatingSystem.IsWindows())
        {
            paquete = ProtegerDpapiWindows(datos);
        }
        else
        {
            byte[] clave = ObtenerOCrearClaveAesLocal(rutaClaveMaestra);
            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            byte[] tag = new byte[16];
            byte[] cifrado = new byte[datos.Length];

            using var aes = new AesGcm(clave, 16);
            aes.Encrypt(nonce, datos, cifrado, tag);

            paquete = new byte[1 + 12 + 16 + cifrado.Length];
            paquete[0] = 9; // Versión bóveda Xynex 9.0
            Buffer.BlockCopy(nonce, 0, paquete, 1, 12);
            Buffer.BlockCopy(tag, 0, paquete, 13, 16);
            Buffer.BlockCopy(cifrado, 0, paquete, 29, cifrado.Length);
        }

        return Convert.ToBase64String(paquete);
    }

    public static string DescifrarSecretoTexto(string? paqueteBase64, string? rutaClaveMaestra = null)
    {
        if (string.IsNullOrWhiteSpace(paqueteBase64))
        {
            return string.Empty;
        }

        try
        {
            byte[] paquete = Convert.FromBase64String(paqueteBase64);
            if (OperatingSystem.IsWindows())
            {
                return Encoding.UTF8.GetString(DesprotegerDpapiWindows(paquete));
            }

            if (paquete.Length < 29 || paquete[0] != 9)
            {
                return string.Empty;
            }

            byte[] clave = ObtenerOCrearClaveAesLocal(rutaClaveMaestra);
            byte[] nonce = paquete[1..13];
            byte[] tag = paquete[13..29];
            byte[] cifrado = paquete[29..];
            byte[] plano = new byte[cifrado.Length];

            using var aes = new AesGcm(clave, 16);
            aes.Decrypt(nonce, cifrado, tag, plano);
            return Encoding.UTF8.GetString(plano);
        }
        catch
        {
            return string.Empty;
        }
    }

    public static bool EsRutaDentroDeBase(string directorioBase, string rutaObjetivo)
    {
        if (string.IsNullOrWhiteSpace(directorioBase) || string.IsNullOrWhiteSpace(rutaObjetivo))
        {
            return false;
        }

        string baseNorm = Path.GetFullPath(directorioBase).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string objNorm = Path.GetFullPath(rutaObjetivo);

        StringComparison cmp = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(objNorm, baseNorm, cmp) ||
               objNorm.StartsWith(baseNorm + Path.DirectorySeparatorChar, cmp);
    }

    public static bool EsUrlHttpSegura(string? url, bool permitirHttpLocalhost = true)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(uri.Host);
        }

        if (permitirHttpLocalhost &&
            string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                   uri.Host.Equals("127.0.0.1", StringComparison.Ordinal);
        }

        return false;
    }

    public static string EnmascararClave(string? clave)
    {
        if (string.IsNullOrWhiteSpace(clave))
        {
            return "(sin clave)";
        }
        string t = clave.Trim();
        if (t.Length <= 8)
        {
            return new string('•', t.Length);
        }
        return $"{t[..4]}••••{t[^4..]}";
    }

    private static byte[] ObtenerOCrearClaveAesLocal(string? rutaPersonalizada)
    {
        string ruta = string.IsNullOrWhiteSpace(rutaPersonalizada)
            ? Path.Combine(RutasUsuarioAlba.ObtenerDirectorioDatosUsuario(), "vault9.key")
            : rutaPersonalizada;

        string? dir = Path.GetDirectoryName(ruta);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        if (File.Exists(ruta))
        {
            byte[] existente = File.ReadAllBytes(ruta);
            if (existente.Length == 32)
            {
                return existente;
            }
        }

        byte[] nuevaClave = RandomNumberGenerator.GetBytes(32);
        File.WriteAllBytes(ruta, nuevaClave);
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(ruta, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch
            {
                // Ignora si el sistema de archivos no soporta POSIX
            }
        }
        return nuevaClave;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DataBlob
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref DataBlob pDataIn,
        string? szDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        out DataBlob pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(
        ref DataBlob pDataIn,
        IntPtr ppszDataDescr,
        IntPtr pOptionalEntropy,
        IntPtr pvReserved,
        IntPtr pPromptStruct,
        int dwFlags,
        out DataBlob pDataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    private static byte[] ProtegerDpapiWindows(byte[] datos)
    {
        var entrada = new DataBlob();
        var salida = new DataBlob();
        try
        {
            entrada.cbData = datos.Length;
            entrada.pbData = Marshal.AllocHGlobal(datos.Length);
            Marshal.Copy(datos, 0, entrada.pbData, datos.Length);

            if (!CryptProtectData(ref entrada, "XynexVault9", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out salida))
            {
                throw new CryptographicException(Marshal.GetLastWin32Error());
            }

            byte[] res = new byte[salida.cbData];
            Marshal.Copy(salida.pbData, res, 0, salida.cbData);
            return res;
        }
        finally
        {
            if (entrada.pbData != IntPtr.Zero) Marshal.FreeHGlobal(entrada.pbData);
            if (salida.pbData != IntPtr.Zero) LocalFree(salida.pbData);
        }
    }

    private static byte[] DesprotegerDpapiWindows(byte[] datos)
    {
        var entrada = new DataBlob();
        var salida = new DataBlob();
        try
        {
            entrada.cbData = datos.Length;
            entrada.pbData = Marshal.AllocHGlobal(datos.Length);
            Marshal.Copy(datos, 0, entrada.pbData, datos.Length);

            if (!CryptUnprotectData(ref entrada, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out salida))
            {
                throw new CryptographicException(Marshal.GetLastWin32Error());
            }

            byte[] res = new byte[salida.cbData];
            Marshal.Copy(salida.pbData, res, 0, salida.cbData);
            return res;
        }
        finally
        {
            if (entrada.pbData != IntPtr.Zero) Marshal.FreeHGlobal(entrada.pbData);
            if (salida.pbData != IntPtr.Zero) LocalFree(salida.pbData);
        }
    }
}
