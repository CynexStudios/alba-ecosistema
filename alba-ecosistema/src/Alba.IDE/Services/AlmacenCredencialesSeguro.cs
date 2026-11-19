using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Alba.IDE.Models;

namespace Alba.IDE.Services;

/// <summary>
/// Almacena de forma cifrada credenciales de GitHub fuera de los proyectos y repositorios.
/// Usa DPAPI nativo de Windows en Windows y AES-256-GCM con permisos restringidos en Linux/macOS.
/// </summary>
public sealed class AlmacenCredencialesSeguro
{
    private sealed class DatosCredencialGitHub
    {
        public string Token { get; set; } = string.Empty;
        public MetodoAutenticacionGitHub Metodo { get; set; }
        public DateTime GuardadoUtc { get; set; } = DateTime.UtcNow;
    }

    private readonly string _rutaArchivoSeguro;

    public AlmacenCredencialesSeguro(string? directorioBase = null)
    {
        string carpeta = string.IsNullOrWhiteSpace(directorioBase)
            ? RutasUsuarioAlba.ObtenerDirectorioDatosUsuario()
            : directorioBase;
        Directory.CreateDirectory(carpeta);
        _rutaArchivoSeguro = Path.Combine(carpeta, "github_auth.bin");
    }

    public void GuardarTokenGitHub(string token, MetodoAutenticacionGitHub metodo)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var datos = new DatosCredencialGitHub
        {
            Token = token.Trim(),
            Metodo = metodo,
            GuardadoUtc = DateTime.UtcNow
        };

        byte[] plano = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(datos));
        byte[] protegido = ProtegerBytes(plano);
        File.WriteAllBytes(_rutaArchivoSeguro, protegido);

        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(_rutaArchivoSeguro, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch
            {
                // Ignora si el sistema de archivos no soporta modos POSIX
            }
        }
    }

    public (string? Token, MetodoAutenticacionGitHub Metodo) LeerTokenGitHub()
    {
        if (!File.Exists(_rutaArchivoSeguro))
        {
            return (null, MetodoAutenticacionGitHub.TokenPersonal);
        }

        try
        {
            byte[] protegido = File.ReadAllBytes(_rutaArchivoSeguro);
            byte[] plano = DesprotegerBytes(protegido);
            var datos = JsonSerializer.Deserialize<DatosCredencialGitHub>(Encoding.UTF8.GetString(plano));
            return string.IsNullOrWhiteSpace(datos?.Token)
                ? (null, MetodoAutenticacionGitHub.TokenPersonal)
                : (datos.Token, datos.Metodo);
        }
        catch
        {
            return (null, MetodoAutenticacionGitHub.TokenPersonal);
        }
    }

    public void EliminarTokenGitHub()
    {
        try
        {
            if (File.Exists(_rutaArchivoSeguro))
            {
                File.Delete(_rutaArchivoSeguro);
            }
        }
        catch
        {
            // Ignora errores de eliminación
        }
    }

    private static byte[] ProtegerBytes(byte[] datos)
    {
        if (OperatingSystem.IsWindows())
        {
            return ProtegerDpapiWindows(datos);
        }

        byte[] clave = DerivarClaveLocal();
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] tag = new byte[16];
        byte[] cifrado = new byte[datos.Length];

        using var aes = new AesGcm(clave, 16);
        aes.Encrypt(nonce, datos, cifrado, tag);

        byte[] salida = new byte[1 + 12 + 16 + cifrado.Length];
        salida[0] = 2; // Formato AES-GCM
        Buffer.BlockCopy(nonce, 0, salida, 1, 12);
        Buffer.BlockCopy(tag, 0, salida, 13, 16);
        Buffer.BlockCopy(cifrado, 0, salida, 29, cifrado.Length);
        return salida;
    }

    private static byte[] DesprotegerBytes(byte[] paquete)
    {
        if (OperatingSystem.IsWindows())
        {
            return DesprotegerDpapiWindows(paquete);
        }

        if (paquete.Length < 29 || paquete[0] != 2)
        {
            throw new CryptographicException("Formato de credencial inválido.");
        }

        byte[] clave = DerivarClaveLocal();
        byte[] nonce = paquete[1..13];
        byte[] tag = paquete[13..29];
        byte[] cifrado = paquete[29..];
        byte[] plano = new byte[cifrado.Length];

        using var aes = new AesGcm(clave, 16);
        aes.Decrypt(nonce, cifrado, tag, plano);
        return plano;
    }

    private static byte[] DerivarClaveLocal()
    {
        string semilla = $"{Environment.UserName}|{Environment.MachineName}|XynexCodeEditor5.0|CynexStudios";
        byte[] sal = Encoding.UTF8.GetBytes("Xynex-GitHub-Creds-Salt-v5");
        return Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(semilla), sal, 100_000, HashAlgorithmName.SHA256, 32);
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

            if (!CryptProtectData(ref entrada, "XynexGitHubAuth", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out salida))
            {
                throw new CryptographicException(Marshal.GetLastWin32Error());
            }

            byte[] resultado = new byte[salida.cbData];
            Marshal.Copy(salida.pbData, resultado, 0, salida.cbData);
            return resultado;
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

            byte[] resultado = new byte[salida.cbData];
            Marshal.Copy(salida.pbData, resultado, 0, salida.cbData);
            return resultado;
        }
        finally
        {
            if (entrada.pbData != IntPtr.Zero) Marshal.FreeHGlobal(entrada.pbData);
            if (salida.pbData != IntPtr.Zero) LocalFree(salida.pbData);
        }
    }
}
