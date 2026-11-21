using Alba.IDE.Models;
using Alba.IDE.Services;
using Xunit;

namespace Alba.IDE.Tests;

public sealed class ActualizadorTests : IDisposable
{
    private readonly string _carpetaTemporal;

    public ActualizadorTests()
    {
        _carpetaTemporal = Path.Combine(Path.GetTempPath(), $"xynex_updater_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_carpetaTemporal);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_carpetaTemporal))
            {
                Directory.Delete(_carpetaTemporal, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void DebeDetectarMetodoDeInstalacionPorSistemaOperativo()
    {
        Assert.Equal(
            MetodoInstalacionActualizacion.WindowsInstalador,
            GestorActualizaciones.DetectarMetodoInstalacion(true, false, @"C:\Program Files\Xynex Code Editor\ide", true, null, false, false, false));

        Assert.Equal(
            MetodoInstalacionActualizacion.WindowsPortable,
            GestorActualizaciones.DetectarMetodoInstalacion(true, false, @"D:\Portable\Xynex\ide", false, null, false, false, false));

        Assert.Equal(
            MetodoInstalacionActualizacion.LinuxAppImage,
            GestorActualizaciones.DetectarMetodoInstalacion(false, true, "/tmp/.mount_Xynex/usr/lib", false, "/home/user/Xynex.AppImage", true, false, false));

        Assert.Equal(
            MetodoInstalacionActualizacion.LinuxArchPacman,
            GestorActualizaciones.DetectarMetodoInstalacion(false, true, "/opt/xynex-code-editor/ide", false, null, true, false, false));

        Assert.Equal(
            MetodoInstalacionActualizacion.LinuxDeb,
            GestorActualizaciones.DetectarMetodoInstalacion(false, true, "/opt/xynex-code-editor/ide", false, null, false, true, false));

        Assert.Equal(
            MetodoInstalacionActualizacion.LinuxRpm,
            GestorActualizaciones.DetectarMetodoInstalacion(false, true, "/opt/xynex-code-editor/ide", false, null, false, false, true));

        Assert.Equal(
            MetodoInstalacionActualizacion.LinuxPortableTarGz,
            GestorActualizaciones.DetectarMetodoInstalacion(false, true, "/home/user/xynex/ide", false, null, true, false, false));
    }

    [Fact]
    public void DebeSeleccionarAssetNativoCorrectoIncluyendoArchYCachyOS()
    {
        var assets = new List<AssetActualizacion>
        {
            new() { Nombre = "XynexCodeEditor-5.1.0-win-x64-setup.exe", UrlDescarga = "https://example.test/win.exe", TamanoBytes = 75_000_000 },
            new() { Nombre = "XynexCodeEditor-5.1.0-win-x64-portable.zip", UrlDescarga = "https://example.test/win.zip", TamanoBytes = 82_000_000 },
            new() { Nombre = "XynexCodeEditor-5.1.0-arch-cachyos-x86_64.pkg.tar.zst", UrlDescarga = "https://example.test/arch.pkg.tar.zst", TamanoBytes = 64_000_000 },
            new() { Nombre = "XynexCodeEditor-5.1.0-linux-x64.deb", UrlDescarga = "https://example.test/lin.deb", TamanoBytes = 66_000_000 },
            new() { Nombre = "XynexCodeEditor-5.1.0-linux-x64.rpm", UrlDescarga = "https://example.test/lin.rpm", TamanoBytes = 67_000_000 },
            new() { Nombre = "XynexCodeEditor-5.1.0-linux-x64.AppImage", UrlDescarga = "https://example.test/lin.AppImage", TamanoBytes = 70_000_000 },
            new() { Nombre = "XynexCodeEditor-5.1.0-linux-x64-portable.tar.gz", UrlDescarga = "https://example.test/lin.tar.gz", TamanoBytes = 65_000_000 }
        };

        Assert.Equal("XynexCodeEditor-5.1.0-win-x64-setup.exe",
            GestorActualizaciones.SeleccionarAssetParaMetodo(assets, MetodoInstalacionActualizacion.WindowsInstalador)?.Nombre);
        Assert.Equal("XynexCodeEditor-5.1.0-win-x64-portable.zip",
            GestorActualizaciones.SeleccionarAssetParaMetodo(assets, MetodoInstalacionActualizacion.WindowsPortable)?.Nombre);
        Assert.Equal("XynexCodeEditor-5.1.0-arch-cachyos-x86_64.pkg.tar.zst",
            GestorActualizaciones.SeleccionarAssetParaMetodo(assets, MetodoInstalacionActualizacion.LinuxArchPacman)?.Nombre);
        Assert.Equal("XynexCodeEditor-5.1.0-linux-x64.deb",
            GestorActualizaciones.SeleccionarAssetParaMetodo(assets, MetodoInstalacionActualizacion.LinuxDeb)?.Nombre);
        Assert.Equal("XynexCodeEditor-5.1.0-linux-x64.rpm",
            GestorActualizaciones.SeleccionarAssetParaMetodo(assets, MetodoInstalacionActualizacion.LinuxRpm)?.Nombre);
        Assert.Equal("XynexCodeEditor-5.1.0-linux-x64.AppImage",
            GestorActualizaciones.SeleccionarAssetParaMetodo(assets, MetodoInstalacionActualizacion.LinuxAppImage)?.Nombre);
        Assert.Equal("XynexCodeEditor-5.1.0-linux-x64-portable.tar.gz",
            GestorActualizaciones.SeleccionarAssetParaMetodo(assets, MetodoInstalacionActualizacion.LinuxPortableTarGz)?.Nombre);
    }

    [Fact]
    public void DebeParsearChecksumsYVerificarSha256DeArchivo()
    {
        string archivo = Path.Combine(_carpetaTemporal, "XynexCodeEditor-5.1.0-arch-cachyos-x86_64.pkg.tar.zst");
        File.WriteAllText(archivo, "contenido de prueba para verificar sha256 en xynex");
        string hashReal = GestorActualizaciones.CalcularSha256Archivo(archivo);

        string tabla = $"{hashReal}  XynexCodeEditor-5.1.0-arch-cachyos-x86_64.pkg.tar.zst\n" +
                       "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef  otro.exe\n";

        Dictionary<string, string> mapa = GestorActualizaciones.ParsearChecksumsSha256(tabla);
        Assert.True(mapa.TryGetValue("XynexCodeEditor-5.1.0-arch-cachyos-x86_64.pkg.tar.zst", out string? encontrado));
        Assert.Equal(hashReal, encontrado);
        Assert.True(GestorActualizaciones.EsVersionMayor("5.1.0", "5.0.0"));
        Assert.False(GestorActualizaciones.EsVersionMayor("5.0.0", "5.0.0"));
    }
}
