using Alba.IDE.Models;
using Alba.IDE.Services;
using Xunit;

namespace Alba.IDE.Tests;

public sealed class AddonsYProyectosTests : IDisposable
{
    private readonly string _carpetaTemporal;

    public AddonsYProyectosTests()
    {
        _carpetaTemporal = Path.Combine(Path.GetTempPath(), $"alba_tests_{Guid.NewGuid():N}");
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
    public void DebeRechazarPathTraversalYEjecutablesNoAutorizadosEnAddon()
    {
        string addonDir = Path.Combine(_carpetaTemporal, "malicioso");
        Directory.CreateDirectory(addonDir);

        var manifiestoTraversal = new ManifiestoAddon
        {
            Id = "malicioso",
            Nombre = "Addon Inseguro",
            Version = "1.0.0",
            Lenguaje = "Python",
            Extensiones = [".py"],
            Ejecutable = "python3",
            Sintaxis = "../../etc/passwd"
        };

        ResultadoValidacionAddon resTraversal = ValidadorAddons.Validar(manifiestoTraversal, addonDir);
        Assert.False(resTraversal.EsValido);

        var manifiestoShell = new ManifiestoAddon
        {
            Id = "malicioso",
            Nombre = "Addon Shell",
            Version = "1.0.0",
            Lenguaje = "Bash",
            Extensiones = [".sh"],
            Ejecutable = "bash -c"
        };

        ResultadoValidacionAddon resShell = ValidadorAddons.Validar(manifiestoShell, addonDir);
        Assert.False(resShell.EsValido);
    }

    [Fact]
    public void DebeInstalarActivarDesactivarYDesinstalarAddonValido()
    {
        string origen = Path.Combine(_carpetaTemporal, "origen_addon");
        string addonsRoot = Path.Combine(_carpetaTemporal, "addons_ide");
        Directory.CreateDirectory(Path.Combine(origen, "syntax"));
        Directory.CreateDirectory(addonsRoot);

        File.WriteAllText(Path.Combine(origen, "syntax", "demo.xshd"), "<SyntaxDefinition />");
        File.WriteAllText(Path.Combine(origen, "manifest.json"), """
            {
              "id": "demo-lang",
              "nombre": "Demo Lang",
              "version": "1.0.0",
              "descripcion": "Prueba",
              "autor": "Alba",
              "lenguaje": "Demo",
              "extensiones": [".demo"],
              "ejecutable": "python3",
              "sintaxis": "syntax/demo.xshd"
            }
            """);

        var gestor = new GestorAddons(addonsRoot);
        var (instalado, _) = gestor.InstalarDesdeCarpeta(origen);

        Assert.True(instalado);
        Assert.NotNull(gestor.ObtenerAddonPorExtension(".demo"));

        gestor.CambiarEstado("demo-lang", activar: false);
        Assert.Null(gestor.ObtenerAddonPorExtension(".demo"));

        // Verifica persistencia al recargar
        var gestorRecargado = new GestorAddons(addonsRoot);
        Assert.Null(gestorRecargado.ObtenerAddonPorExtension(".demo"));

        var (desinstalado, _) = gestorRecargado.DesinstalarAddon("demo-lang");
        Assert.True(desinstalado);
        Assert.Empty(gestorRecargado.Addons);
    }

    [Fact]
    public void DebeCrearProyectoYCargarArbolDeArchivos()
    {
        var gestor = new GestorProyectos();
        var (rutaProyecto, rutaPrincipal) = gestor.CrearProyecto(_carpetaTemporal, "MiApp");

        Assert.True(Directory.Exists(rutaProyecto));
        Assert.True(File.Exists(rutaPrincipal));
        Assert.True(File.Exists(Path.Combine(rutaProyecto, "proyecto.albaproj.json")));

        NodoProyecto arbol = gestor.CargarArbolProyecto(rutaProyecto);
        Assert.Equal("MiApp", arbol.Nombre);
        Assert.Contains(arbol.Hijos, h => h.Nombre == "principal.alb" && !h.EsCarpeta);
    }

    [Fact]
    public async Task DebePermitirLecturaInteractivaAsincronaEnConsolaIdeIO()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var salidas = new List<string>();
        ConsolaIdeIO? consola = null;

        consola = new ConsolaIdeIO(
            s => salidas.Add(s),
            s => salidas.Add(s),
            indicador =>
            {
                salidas.Add(indicador ?? string.Empty);
                Task.Run(() => consola!.EnviarEntrada("Lucía"));
            },
            cts.Token);

        string? leido = await Task.Run(() => consola.LeerLinea("Nombre: "), cts.Token);

        Assert.Equal("Lucía", leido);
        Assert.Contains("Nombre: ", salidas);
    }
}
