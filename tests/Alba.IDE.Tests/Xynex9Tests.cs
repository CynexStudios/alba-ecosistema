using Alba.IDE.Models;
using Alba.IDE.Services;
using Xunit;

namespace Alba.IDE.Tests;

public sealed class Xynex9Tests : IDisposable
{
    private readonly string _carpetaTemporal;

    public Xynex9Tests()
    {
        _carpetaTemporal = Path.Combine(Path.GetTempPath(), $"xynex9_tests_{Guid.NewGuid():N}");
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
    public void Seguridad_DebeHashearVerificarCifrarYValidarRutasYUrls()
    {
        var (hash, sal) = ServicioSeguridad.GenerarHashContrasena("ClaveSuperSegura90!");
        Assert.True(ServicioSeguridad.VerificarContrasena("ClaveSuperSegura90!", hash, sal));
        Assert.False(ServicioSeguridad.VerificarContrasena("ClaveIncorrecta", hash, sal));

        string rutaClave = Path.Combine(_carpetaTemporal, "vault_test.key");
        string secreto = "sk-xynex-secret-token-9000";
        string cifrado = ServicioSeguridad.CifrarSecretoTexto(secreto, rutaClave);
        Assert.NotEqual(secreto, cifrado);
        Assert.Equal(secreto, ServicioSeguridad.DescifrarSecretoTexto(cifrado, rutaClave));

        Assert.True(ServicioSeguridad.EsUrlHttpSegura("https://api.openai.com/v1/chat/completions"));
        Assert.True(ServicioSeguridad.EsUrlHttpSegura("http://localhost:11434"));
        Assert.True(ServicioSeguridad.EsUrlHttpSegura("http://127.0.0.1:11434"));
        Assert.False(ServicioSeguridad.EsUrlHttpSegura("http://sitio-inseguro.test/api"));
        Assert.False(ServicioSeguridad.EsUrlHttpSegura("file:///etc/passwd"));

        string baseDir = Path.Combine(_carpetaTemporal, "addons");
        Directory.CreateDirectory(baseDir);
        Assert.True(ServicioSeguridad.EsRutaDentroDeBase(baseDir, Path.Combine(baseDir, "python", "manifest.json")));
        Assert.False(ServicioSeguridad.EsRutaDentroDeBase(baseDir, Path.Combine(baseDir, "..", "secrets.txt")));
    }

    [Fact]
    public void BaseDatosUsuarios_DebeRegistrarIniciarSesionBloquearYGuardarPerfilesIACifrados()
    {
        string dbPath = Path.Combine(_carpetaTemporal, "xynex_accounts_test.db");
        var db = new ServicioBaseDatosUsuarios(dbPath);

        ResultadoOperacionCuenta reg = db.RegistrarUsuario("cynex_dev", "dev@cynex.test", "Cynex Dev", "Password90!");
        Assert.True(reg.Exito);
        Assert.NotNull(reg.Cuenta);
        Assert.Equal("cynex_dev", db.ObtenerUsuarioActivo()?.NombreUsuario);

        ResultadoOperacionCuenta dupUser = db.RegistrarUsuario("cynex_dev", "otro@cynex.test", "Otro", "Password90!");
        Assert.False(dupUser.Exito);

        ResultadoOperacionCuenta dupMail = db.RegistrarUsuario("otro_user", "dev@cynex.test", "Otro", "Password90!");
        Assert.False(dupMail.Exito);

        db.CerrarSesion();
        Assert.Null(db.ObtenerUsuarioActivo());

        for (int i = 0; i < 4; i++)
        {
            ResultadoOperacionCuenta fail = db.IniciarSesion("cynex_dev", "WrongPass123!");
            Assert.False(fail.Exito);
        }

        ResultadoOperacionCuenta bloqueo = db.IniciarSesion("cynex_dev", "WrongPass123!");
        Assert.False(bloqueo.Exito);
        Assert.Contains("bloqueada", bloqueo.Mensaje, StringComparison.OrdinalIgnoreCase);

        // Un segundo usuario comprueba inicio exitoso y gestión de perfiles IA cifrados
        ResultadoOperacionCuenta reg2 = db.RegistrarUsuario("ana_cibao", "ana@cynex.test", "Ana", "AnaClaveSegura9!");
        Assert.True(reg2.Exito);
        int uid = reg2.Cuenta!.Id;

        PerfilIAConectado perfil1 = db.GuardarPerfilIA(
            uid, ProveedorIAUsuario.OllamaLocal, "Ollama Local", "http://localhost:11434", "qwen2.5-coder", null, marcarActivo: true);
        PerfilIAConectado perfil2 = db.GuardarPerfilIA(
            uid, ProveedorIAUsuario.GoogleGemini, "Mi Gemini", "https://generativelanguage.googleapis.com", "gemini-2.5-flash", "AIzaTestKey12345", marcarActivo: false);

        IReadOnlyList<PerfilIAConectado> perfiles = db.ListarPerfilesIA(uid);
        Assert.Equal(2, perfiles.Count);
        Assert.True(perfiles.Single(p => p.Id == perfil1.Id).EsActivo);
        Assert.Equal("AIzaTestKey12345", db.DescifrarApiKeyPerfil(perfiles.Single(p => p.Id == perfil2.Id)));

        db.ActivarPerfilIA(uid, perfil2.Id);
        Assert.True(db.ListarPerfilesIA(uid).Single(p => p.Id == perfil2.Id).EsActivo);

        db.EliminarPerfilIA(uid, perfil1.Id);
        Assert.Single(db.ListarPerfilesIA(uid));
    }

    [Fact]
    public void ValidadorAddons_DebePrevenirPathTraversal()
    {
        string addonDir = Path.Combine(_carpetaTemporal, "mi-addon");
        Directory.CreateDirectory(addonDir);

        Assert.True(ValidadorAddons.TryResolverRutaSegura(addonDir, "snippets/snippets.json", out string? segura));
        Assert.NotNull(segura);

        Assert.False(ValidadorAddons.TryResolverRutaSegura(addonDir, "../otro/archivo.json", out _));
        Assert.False(ValidadorAddons.TryResolverRutaSegura(addonDir, "/etc/passwd", out _));
    }

    [Fact]
    public void AgenteIA_DebeConstruirRequestsYExtraerCodigoDeTodosLosProveedores()
    {
        var contexto = new ContextoCodigoIA("prueba.cibao", "Cibao", "mostrar('hola')", []);
        string msg = ServicioAgenteIA.ConstruirMensajeConContexto("Explica este código", contexto);

        var ollama = new PerfilIAConectado { Proveedor = ProveedorIAUsuario.OllamaLocal, EndpointUrl = "http://localhost:11434", Modelo = "llama3.1" };
        using var reqOllama = ServicioAgenteIA.ConstruirHttpRequest(ollama, "", [], msg);
        Assert.Equal("http://localhost:11434/api/chat", reqOllama.RequestUri?.ToString());

        var gemini = new PerfilIAConectado { Proveedor = ProveedorIAUsuario.GoogleGemini, EndpointUrl = "https://generativelanguage.googleapis.com", Modelo = "gemini-2.5-flash" };
        using var reqGemini = ServicioAgenteIA.ConstruirHttpRequest(gemini, "key-gemini", [], msg);
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent", reqGemini.RequestUri?.ToString());
        Assert.True(reqGemini.Headers.Contains("x-goog-api-key"));

        var openai = new PerfilIAConectado { Proveedor = ProveedorIAUsuario.OpenAICompatible, EndpointUrl = "https://api.openai.com", Modelo = "gpt-4o-mini" };
        using var reqOpenai = ServicioAgenteIA.ConstruirHttpRequest(openai, "sk-test", [], msg);
        Assert.Equal("https://api.openai.com/v1/chat/completions", reqOpenai.RequestUri?.ToString());
        Assert.Equal("Bearer", reqOpenai.Headers.Authorization?.Scheme);

        var claude = new PerfilIAConectado { Proveedor = ProveedorIAUsuario.AnthropicClaude, EndpointUrl = "https://api.anthropic.com/v1/messages", Modelo = "claude-3-5-sonnet" };
        using var reqClaude = ServicioAgenteIA.ConstruirHttpRequest(claude, "sk-ant-test", [], msg);
        Assert.True(reqClaude.Headers.Contains("x-api-key"));

        string? codigo = ServicioAgenteIA.ExtraerPrimerBloqueCodigo("Aquí tienes:\n```cibao\nmostrar(\"Hola\")\n```\nListo.");
        Assert.Equal("mostrar(\"Hola\")", codigo);

        Assert.Equal("hola gemini", ServicioAgenteIA.ExtraerTextoRespuesta(
            ProveedorIAUsuario.GoogleGemini,
            """{"candidates":[{"content":{"parts":[{"text":"hola gemini"}]}}]}"""));
    }

    [Fact]
    public void TutorCibao_DebeIncluir8LeccionesYValidarSalidas()
    {
        var tutor = new TutorCibaoService();
        Assert.Equal(8, tutor.Lecciones.Count);
        Assert.All(tutor.Lecciones, l =>
        {
            Assert.False(string.IsNullOrWhiteSpace(l.Titulo));
            Assert.False(string.IsNullOrWhiteSpace(l.CodigoEjemplo));
            Assert.False(string.IsNullOrWhiteSpace(l.TextoEsperadoEnSalida));
        });

        Assert.True(tutor.VerificarSalidaLeccion(tutor.Lecciones[0], "¡Hola Mundo desde Xynex Code Editor 9.0!"));
        Assert.False(tutor.VerificarSalidaLeccion(tutor.Lecciones[0], "otra salida"));
        Assert.Contains("variable", tutor.ResponderPreguntaRapida("como declaro variables", 0), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DiagnosticoEnVivo_DebeDetectarErroresNativosPorLenguaje()
    {
        var diag = new ServicioDiagnosticoEnVivo();

        var htmlErrs = diag.AnalizarDocumento("index.html", "<html><body><div><span>hola</div></body></html>");
        Assert.NotEmpty(htmlErrs);
        Assert.Contains(htmlErrs, e => e.Codigo.StartsWith("HTML-", StringComparison.Ordinal));

        var cssErrs = diag.AnalizarDocumento("estilos.css", "body {\n  color: red\n}");
        Assert.Contains(cssErrs, e => e.Codigo == "CSS-W01");

        var pyErrs = diag.AnalizarDocumento("app.py", "def saludar()\n    print('hola')");
        Assert.Contains(pyErrs, e => e.Codigo == "PY-E02");

        var jsonErrs = diag.AnalizarDocumento("config.json", "{ \"clave\": }");
        Assert.Contains(jsonErrs, e => e.Codigo == "JSON-001");

        var tsErrs = diag.AnalizarDocumento("app.ts", "function test() { console.log('ok');");
        Assert.NotEmpty(tsErrs);

        var cibaoErrs = diag.AnalizarDocumento("main.cibao", "si verdadero { mostrar(\"sin cerrar\"");
        Assert.NotEmpty(cibaoErrs);
    }

    [Fact]
    public void GestorAddonsYCreacionRapida_DebenInstalarDesdeCatalogoYGenerarPlantillasSinCarpeta()
    {
        string addonsDir = Path.Combine(_carpetaTemporal, "addons_user");
        var gestor = new GestorAddons(addonsDir);

        var item = new AddonCatalogoEnLinea
        {
            Id = "pack-web-demo",
            Nombre = "Pack Web Demo",
            Version = "9.0.0",
            Descripcion = "Snippets HTML",
            Autor = "CynexStudios",
            Lenguaje = "HTML",
            Extensiones = [".html"],
            Ejecutable = "browser",
            SnippetsIntegrados =
            [
                new SnippetAddon { Prefijo = "demo", Titulo = "Demo", Descripcion = "Demo tag", Cuerpo = "<div>Demo</div>" }
            ]
        };

        var res = gestor.InstalarDesdeCatalogoEnLineaAsync(item).GetAwaiter().GetResult();
        Assert.True(res.Exito);
        Assert.NotEmpty(gestor.ObtenerSnippetsActivosPorExtension(".html"));

        Assert.Contains("Cibao 9.0", ServicioCreacion.GenerarPlantillaRapidaPorExtension(".cibao", "Borrador"));
        Assert.Contains("print(", ServicioCreacion.GenerarPlantillaRapidaPorExtension(".py", "Borrador"));
        Assert.Contains("<!DOCTYPE html>", ServicioCreacion.GenerarPlantillaRapidaPorExtension(".html", "Borrador"));
        Assert.Contains("interface", ServicioCreacion.GenerarPlantillaRapidaPorExtension(".ts", "Borrador"));
        Assert.Contains("\"version\": \"9.0.0\"", ServicioCreacion.GenerarPlantillaRapidaPorExtension(".json", "Borrador"));
    }
}
