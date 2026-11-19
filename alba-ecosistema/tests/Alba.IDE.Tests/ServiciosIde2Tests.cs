using Alba.IDE.Services;
using Xunit;

namespace Alba.IDE.Tests;

public sealed class ServiciosIde2Tests : IDisposable
{
    private readonly string _carpetaTemporal;

    public ServiciosIde2Tests()
    {
        _carpetaTemporal = Path.Combine(Path.GetTempPath(), $"alba_ide2_tests_{Guid.NewGuid():N}");
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
    public void DebePersistirConfiguracionRecientesYSesion()
    {
        var cfg1 = new GestorConfiguracion(_carpetaTemporal);
        cfg1.Configuracion.TemaId = "alba-amanecer";
        cfg1.Configuracion.GuardadoAutomatico = true;
        cfg1.Configuracion.TamanoFuenteEditor = 16;
        cfg1.GuardarConfiguracion();

        string proyectoDemo = Path.Combine(_carpetaTemporal, "Demo");
        Directory.CreateDirectory(proyectoDemo);
        string archivoDemo = Path.Combine(proyectoDemo, "principal.alb");
        File.WriteAllText(archivoDemo, "imprimir(\"Hola\")");

        cfg1.RegistrarReciente(proyectoDemo, esProyecto: true);
        cfg1.GuardarSesion(proyectoDemo, [archivoDemo], archivoDemo);

        var cfg2 = new GestorConfiguracion(_carpetaTemporal);
        Assert.Equal("alba-amanecer", cfg2.Configuracion.TemaId);
        Assert.True(cfg2.Configuracion.GuardadoAutomatico);
        Assert.Equal(16, cfg2.Configuracion.TamanoFuenteEditor);
        Assert.Single(cfg2.Recientes);
        Assert.Equal(proyectoDemo, cfg2.Sesion.UltimoProyecto);
        Assert.Contains(archivoDemo, cfg2.Sesion.PestanasAbiertas);
    }

    [Fact]
    public void DebeDetectarErroresEnVivoConAlbaEngineSinModificarMotor()
    {
        var diagnostico = new ServicioDiagnosticoEnVivo();
        var problemas = diagnostico.AnalizarDocumento("prueba.alb", "funcion sinCerrar() {\n  imprimir(\"hola\"\n");
        Assert.NotEmpty(problemas);

        var limpio = diagnostico.AnalizarDocumento("ok.alb", "variable x = 10\nimprimir(x)\n");
        Assert.Empty(limpio);
    }

    [Fact]
    public void DebeBuscarTextoEnArchivosDelProyecto()
    {
        var gestor = new GestorProyectos();
        var (rutaProyecto, _) = gestor.CrearProyecto(_carpetaTemporal, "Buscable");
        File.WriteAllText(Path.Combine(rutaProyecto, "util.alb"), "funcion calcularTotal() {\n  retornar 42\n}");

        var resultados = BuscadorProyecto.BuscarTextoEnProyecto(rutaProyecto, "calcularTotal", distinguirMayusculas: true);
        Assert.Single(resultados);
        Assert.Equal(1, resultados[0].Linea);
    }

    [Fact]
    public void DebeCargarLosTresTemasEmbebidosYCompararVersiones()
    {
        var temas = new GestorTemas();
        Assert.True(temas.TemasDisponibles.Count >= 3);
        Assert.NotNull(temas.ObtenerTema("alba-crepusculo"));
        Assert.NotNull(temas.ObtenerTema("alba-medianoche"));
        Assert.NotNull(temas.ObtenerTema("alba-amanecer"));

        Assert.True(GestorActualizaciones.EsVersionMayor("2.1.0", "2.0.0"));
        Assert.False(GestorActualizaciones.EsVersionMayor("2.0.0", "2.0.0"));
    }
}
