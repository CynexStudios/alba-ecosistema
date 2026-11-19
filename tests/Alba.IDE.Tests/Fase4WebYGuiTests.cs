using System.Net;
using System.Text;
using Alba.IDE.Models;
using Alba.IDE.Services;
using Xunit;

namespace Alba.IDE.Tests;

public sealed class Fase4WebYGuiTests : IDisposable
{
    private readonly string _carpetaTemporal;

    public Fase4WebYGuiTests()
    {
        _carpetaTemporal = Path.Combine(Path.GetTempPath(), $"xynex_f4_tests_{Guid.NewGuid():N}");
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
    public async Task ServidorWebLocalDebeServirHtmlInyectarBridgeYCapturarLogs()
    {
        string webRoot = Path.Combine(_carpetaTemporal, "webroot");
        Directory.CreateDirectory(webRoot);
        string secreto = Path.Combine(_carpetaTemporal, "secreto.txt");
        await File.WriteAllTextAsync(secreto, "NO_ACCESIBLE");

        string indexHtml = Path.Combine(webRoot, "index.html");
        await File.WriteAllTextAsync(indexHtml, "<!DOCTYPE html><html><body><h1>Xynex Web</h1></body></html>");

        using var servidor = new ServidorWebLocal();
        var logs = new List<MensajeConsolaWeb>();
        servidor.LogRecibido += msg => logs.Add(msg);

        string urlBase = servidor.IniciarOActualizarRaiz(webRoot);
        Assert.True(servidor.EstaActivo);
        Assert.StartsWith("http://127.0.0.1:", urlBase);

        using var http = new HttpClient();

        // 1. HTML servido con Live Reload + puente de consola
        HttpResponseMessage respHtml = await http.GetAsync(servidor.ConstruirUrlParaArchivo(indexHtml));
        Assert.Equal(HttpStatusCode.OK, respHtml.StatusCode);
        string cuerpo = await respHtml.Content.ReadAsStringAsync();
        Assert.Contains("<h1>Xynex Web</h1>", cuerpo);
        Assert.Contains("/__xynex_poll", cuerpo);
        Assert.Contains("/__xynex_log", cuerpo);

        // 2. Poll de versión incremental
        string v1 = await http.GetStringAsync($"{urlBase}__xynex_poll");
        servidor.NotificarCambioArchivo();
        string v2 = await http.GetStringAsync($"{urlBase}__xynex_poll");
        Assert.NotEqual(v1, v2);

        // 3. Captura de errores desde el navegador vía POST /__xynex_log
        using var content = new StringContent("""{"level":"error","message":"ReferenceError: demo is not defined"}""", Encoding.UTF8, "application/json");
        HttpResponseMessage respLog = await http.PostAsync($"{urlBase}__xynex_log", content);
        Assert.Equal(HttpStatusCode.NoContent, respLog.StatusCode);
        Assert.Contains(logs, m => m.Nivel == "error" && m.Origen == "Navegador" && m.Mensaje.Contains("demo is not defined"));

        // 4. Bloqueo de directory traversal fuera de webRoot
        HttpResponseMessage respTraversal = await http.GetAsync($"{urlBase}../secreto.txt");
        Assert.Equal(HttpStatusCode.Forbidden, respTraversal.StatusCode);

        // 5. Detección 404 en consola web
        HttpResponseMessage resp404 = await http.GetAsync($"{urlBase}no-existe.js");
        Assert.Equal(HttpStatusCode.NotFound, resp404.StatusCode);
        Assert.Contains(logs, m => m.Origen == "HTTP 404" && m.Mensaje.Contains("no-existe.js"));
    }

    [Fact]
    public void AnalizadorWebHtmlDebeExtraerDomYDetectarErroresEstructurales()
    {
        string carpeta = Path.Combine(_carpetaTemporal, "sitio");
        Directory.CreateDirectory(carpeta);
        string cssPath = Path.Combine(carpeta, "estilos.css");
        File.WriteAllText(cssPath, "body { background: #101826; color: #f8fafc; } button { background: #22c55e; }");

        string htmlValido = """
            <!DOCTYPE html>
            <html>
            <head>
              <title>Portal Cibao</title>
              <link rel="stylesheet" href="estilos.css" />
            </head>
            <body>
              <h1>Bienvenido</h1>
              <p>Prueba interactiva</p>
              <button>Enviar</button>
              <a href="acerca.html">Acerca de</a>
              <input placeholder="Tu nombre" />
            </body>
            </html>
            """;

        ResultadoAnalisisWeb ok = AnalizadorWebHtml.Analizar(Path.Combine(carpeta, "index.html"), htmlValido);
        Assert.True(ok.EsValido);
        Assert.Equal("Portal Cibao", ok.TituloPagina);
        Assert.Equal("#101826", ok.FondoCssHex);
        Assert.Equal("#22c55e", ok.ColorAcentoCssHex);
        Assert.Contains(ok.Bloques, b => b.Etiqueta == "h1" && b.Texto == "Bienvenido");
        Assert.Contains(ok.Bloques, b => b.Etiqueta == "button" && b.Texto == "Enviar");
        Assert.Contains(ok.Bloques, b => b.Etiqueta == "a" && b.Href == "acerca.html");
        Assert.Contains(ok.Bloques, b => b.Etiqueta == "input" && b.Texto == "Tu nombre");

        string htmlRoto = """
            <html>
            <body>
              <div><p>Sin cerrar</div>
              <div>Extra sin cierre
              <script src="inexistente.js"></script>
              <style>body { color: #fff; </style>
            </body>
            </html>
            """;

        ResultadoAnalisisWeb conErrores = AnalizadorWebHtml.Analizar(Path.Combine(carpeta, "roto.html"), htmlRoto);
        Assert.False(conErrores.EsValido);
        Assert.Contains(conErrores.Errores, e => e.Mensaje.Contains("<div>"));
        Assert.Contains(conErrores.Errores, e => e.Mensaje.Contains("inexistente.js"));
        Assert.Contains(conErrores.Errores, e => e.Mensaje.Contains("CSS embebido"));
    }

    [Fact]
    public void ServicioCreacionDebeGenerarPlantillasGraficasPythonTkinterYAvalonia()
    {
        var servicio = new ServicioCreacion();
        IReadOnlyList<PlantillaCreacion> plantillas = servicio.ObtenerPlantillas(ModalidadCreacion.Proyecto);

        PlantillaCreacion tk = Assert.Single(plantillas, p => p.Id == "proj-python-gui");
        var resTk = servicio.Crear(new SolicitudCreacion
        {
            Modalidad = ModalidadCreacion.Proyecto,
            Lenguaje = LenguajeSoportado.Python,
            Plantilla = tk,
            Nombre = "MiAppTkinter",
            CarpetaDestino = _carpetaTemporal
        });

        Assert.True(resTk.Exito);
        Assert.True(File.Exists(resTk.RutaArchivoPrincipal));
        string codigoTk = File.ReadAllText(resTk.RutaArchivoPrincipal);
        Assert.Contains("tkinter", codigoTk);
        Assert.True(ServicioEjecucion.EsCodigoGrafico(resTk.RutaArchivoPrincipal, codigoTk));

        PlantillaCreacion avalonia = Assert.Single(plantillas, p => p.Id == "proj-csharp-avalonia");
        var resAv = servicio.Crear(new SolicitudCreacion
        {
            Modalidad = ModalidadCreacion.Proyecto,
            Lenguaje = LenguajeSoportado.CSharp,
            Plantilla = avalonia,
            Nombre = "MiAppAvalonia",
            CarpetaDestino = _carpetaTemporal
        });

        Assert.True(resAv.Exito);
        Assert.NotNull(resAv.RutaProyecto);
        Assert.True(File.Exists(Path.Combine(resAv.RutaProyecto!, "MainWindow.axaml")));
        Assert.True(File.Exists(Path.Combine(resAv.RutaProyecto!, "MainWindow.axaml.cs")));
        Assert.True(File.Exists(Path.Combine(resAv.RutaProyecto!, "App.axaml")));
        Assert.True(File.Exists(Path.Combine(resAv.RutaProyecto!, "Program.cs")));
        string csproj = File.ReadAllText(Path.Combine(resAv.RutaProyecto!, "MiAppAvalonia.csproj"));
        Assert.Contains("Avalonia", csproj);
    }

    [Fact]
    public async Task DetectorYGestorToolsDebenExponerCapacidadesWebYGraficasReales()
    {
        var detector = new DetectorEntornos();
        IReadOnlyList<EstadoEntornoGrafico> graficos = await detector.DetectarEntornosGraficosAsync(forzarRecarga: true);

        Assert.Equal(3, graficos.Count);
        Assert.Contains(graficos, g => g.Nombre.Contains("Navegador Web", StringComparison.OrdinalIgnoreCase) && g.Disponible);
        Assert.Contains(graficos, g => g.Nombre.Contains("Tkinter", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(graficos, g => g.Nombre.Contains("Avalonia", StringComparison.OrdinalIgnoreCase));

        var addons = new GestorAddons(Path.Combine(_carpetaTemporal, "addons_test"));
        var gestorTools = new GestorTools(addons, detector);
        await gestorTools.RefrescarCatalogoAsync(true);

        Assert.Contains(gestorTools.Catalogo, t => t.Id == "tool-html-web" && t.Instalado);
        Assert.Contains(gestorTools.Catalogo, t => t.Id == "tool-avalonia-ui" && t.Categoria == CategoriaHerramienta.AplicacionesGraficas);
        Assert.Contains(gestorTools.Catalogo, t => t.Id == "tool-python-gui" && t.Categoria == CategoriaHerramienta.AplicacionesGraficas);
    }
}
