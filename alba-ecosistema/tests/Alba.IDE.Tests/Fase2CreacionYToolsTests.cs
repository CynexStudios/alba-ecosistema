using Alba.IDE.Models;
using Alba.IDE.Services;
using Xunit;

namespace Alba.IDE.Tests;

public sealed class Fase2CreacionYToolsTests : IDisposable
{
    private readonly string _carpetaTemporal;

    public Fase2CreacionYToolsTests()
    {
        _carpetaTemporal = Path.Combine(Path.GetTempPath(), $"xynex_f2_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_carpetaTemporal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_carpetaTemporal))
        {
            Directory.Delete(_carpetaTemporal, recursive: true);
        }
    }

    [Fact]
    public void DebeCrearProyectoProgramaYArchivoIndependienteEnCibaoYHtml()
    {
        var servicio = new ServicioCreacion();
        var detector = new DetectorEntornos();
        var entornos = detector.ObtenerUltimoEstadoSinBloquear();

        PlantillaCreacion plantillaProyectoCibao = servicio
            .ObtenerPlantillas(ModalidadCreacion.Proyecto, LenguajeSoportado.Cibao, entornos)
            .First();

        var resProyecto = servicio.Crear(new SolicitudCreacion
        {
            Modalidad = ModalidadCreacion.Proyecto,
            Lenguaje = LenguajeSoportado.Cibao,
            Plantilla = plantillaProyectoCibao,
            Nombre = "MiAppCibao",
            CarpetaDestino = _carpetaTemporal,
            InicializarGit = false
        });

        Assert.True(resProyecto.Exito);
        Assert.True(resProyecto.EsProyecto);
        Assert.NotNull(resProyecto.RutaProyecto);
        Assert.True(File.Exists(Path.Combine(resProyecto.RutaProyecto!, "proyecto.xynexproj.json")));
        Assert.True(File.Exists(Path.Combine(resProyecto.RutaProyecto!, "principal.cibao")));

        PlantillaCreacion plantillaPrograma = servicio
            .ObtenerPlantillas(ModalidadCreacion.Programa, LenguajeSoportado.Cibao, entornos)
            .First();

        var resPrograma = servicio.Crear(new SolicitudCreacion
        {
            Modalidad = ModalidadCreacion.Programa,
            Lenguaje = LenguajeSoportado.Cibao,
            Plantilla = plantillaPrograma,
            Nombre = "hola_directo",
            CarpetaDestino = _carpetaTemporal
        });

        Assert.True(resPrograma.Exito);
        Assert.False(resPrograma.EsProyecto);
        Assert.EndsWith(".cibao", resPrograma.RutaArchivoPrincipal, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(resPrograma.RutaArchivoPrincipal));

        PlantillaCreacion plantillaArchivoHtml = servicio
            .ObtenerPlantillas(ModalidadCreacion.ArchivoIndependiente, LenguajeSoportado.Html, entornos)
            .First();

        var resArchivo = servicio.Crear(new SolicitudCreacion
        {
            Modalidad = ModalidadCreacion.ArchivoIndependiente,
            Lenguaje = LenguajeSoportado.Html,
            Plantilla = plantillaArchivoHtml,
            Nombre = "landing",
            CarpetaDestino = _carpetaTemporal
        });

        Assert.True(resArchivo.Exito);
        Assert.False(resArchivo.EsProyecto);
        Assert.EndsWith(".html", resArchivo.RutaArchivoPrincipal, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<!DOCTYPE html>", File.ReadAllText(resArchivo.RutaArchivoPrincipal));
    }

    [Fact]
    public void DebeDeshabilitarPlantillasCuyoSdkNoEsteDetectado()
    {
        var servicio = new ServicioCreacion();
        IReadOnlyList<EstadoEntornoLenguaje> entornosSimulados =
        [
            new EstadoEntornoLenguaje
            {
                Lenguaje = LenguajeSoportado.Cpp,
                Nombre = "C++",
                ExtensionPrincipal = ".cpp",
                Disponible = false,
                RequiereSdkExterno = true
            }
        ];

        var plantillasCpp = servicio.ObtenerPlantillas(ModalidadCreacion.Proyecto, LenguajeSoportado.Cpp, entornosSimulados);
        Assert.NotEmpty(plantillasCpp);
        Assert.All(plantillasCpp, p =>
        {
            Assert.False(p.DisponibleEnEquipo);
            Assert.False(string.IsNullOrWhiteSpace(p.MotivoNoDisponible));
        });
    }

    [Fact]
    public async Task DebeDetectarCibaoYHtmlIntegradosYSoportarSondaSegura()
    {
        var detector = new DetectorEntornos();
        var entornos = await detector.DetectarTodosAsync();

        EstadoEntornoLenguaje? cibao = entornos.FirstOrDefault(e => e.Lenguaje == LenguajeSoportado.Cibao);
        EstadoEntornoLenguaje? html = entornos.FirstOrDefault(e => e.Lenguaje == LenguajeSoportado.Html);

        Assert.NotNull(cibao);
        Assert.True(cibao!.Disponible);
        Assert.Equal(".cibao", cibao.ExtensionPrincipal);

        Assert.NotNull(html);
        Assert.True(html!.Disponible);

        var (inexistenteOk, _) = await detector.DetectarComandoAsync("comando_inexistente_xynex_999", "--version");
        Assert.False(inexistenteOk);
    }

    [Fact]
    public void DebeFiltrarCatalogoDeToolsPorCategoriaTextoEInstalacion()
    {
        string dirAddons = Path.Combine(_carpetaTemporal, "addons");
        Directory.CreateDirectory(dirAddons);

        var gestorAddons = new GestorAddons(dirAddons);
        var detector = new DetectorEntornos();
        var gestorTools = new GestorTools(gestorAddons, detector);

        var lenguajes = gestorTools.Filtrar(null, CategoriaHerramienta.Lenguajes, soloInstalados: true);
        Assert.Contains(lenguajes, t => t.Id == "cibao-engine" && t.Instalado);

        var busqueda = gestorTools.Filtrar("Cibao", CategoriaHerramienta.Todas, soloInstalados: false);
        Assert.NotEmpty(busqueda);
    }

    [Fact]
    public void DebeAnalizarArchivosCibaoConExtensionNuevaYLegacy()
    {
        var diag = new ServicioDiagnosticoEnVivo();

        Assert.True(ServicioDiagnosticoEnVivo.EsArchivoCibao("demo.cibao"));
        Assert.True(ServicioDiagnosticoEnVivo.EsArchivoCibao("legacy.alb"));
        Assert.False(ServicioDiagnosticoEnVivo.EsArchivoCibao("script.py"));

        var conErrorCibao = diag.AnalizarDocumento("prueba.cibao", "funcion mal() {\n  imprimir(\"sin cerrar\"\n");
        Assert.NotEmpty(conErrorCibao);

        var validoCibao = diag.AnalizarDocumento("ok.cibao", "variable saludo = \"Cibao 5.0\"\nimprimir(saludo)\n");
        Assert.Empty(validoCibao);
    }

    [Fact]
    public void DebeAdministrarMultiplesSesionesTerminalEHistorial()
    {
        using var terminales = new GestorTerminales();
        SesionTerminalTab s1 = terminales.CrearSesion(ModalidadTerminal.IntegradaIde, _carpetaTemporal);
        SesionTerminalTab s2 = terminales.CrearSesion(
            OperatingSystem.IsWindows() ? ModalidadTerminal.PowerShellWindows : ModalidadTerminal.BashSistema,
            _carpetaTemporal);

        Assert.Equal(2, terminales.Sesiones.Count);
        Assert.Equal(s2, terminales.SesionActiva);

        s2.HistorialComandos.Add("echo uno");
        s2.HistorialComandos.Add("echo dos");
        s2.IndiceHistorial = 2;

        Assert.Equal("echo dos", terminales.NavegarHistorial(haciaAtras: true));
        Assert.Equal("echo uno", terminales.NavegarHistorial(haciaAtras: true));
        Assert.Equal("echo dos", terminales.NavegarHistorial(haciaAtras: false));

        terminales.CerrarSesion(s2);
        Assert.Single(terminales.Sesiones);
        Assert.Equal(s1, terminales.SesionActiva);
    }
}
