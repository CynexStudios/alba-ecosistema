using System.Net;
using System.Text;
using Alba.IDE.Models;
using Alba.IDE.Services;
using Xunit;

namespace Alba.IDE.Tests;

public sealed class Fase3GitHubYToolsTests : IDisposable
{
    private readonly string _carpetaTemporal;

    public Fase3GitHubYToolsTests()
    {
        _carpetaTemporal = Path.Combine(Path.GetTempPath(), $"xynex_f3_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_carpetaTemporal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_carpetaTemporal))
        {
            Directory.Delete(_carpetaTemporal, recursive: true);
        }
    }

    private sealed class HandlerGitHubSimulado(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }

    [Fact]
    public void DebeCifrarRecuperarYEliminarTokenGitHubFueraDeRepositorios()
    {
        string dirSeguro = Path.Combine(_carpetaTemporal, "almacen_seguro");
        var almacen = new AlmacenCredencialesSeguro(dirSeguro);

        const string tokenOriginal = "ghp_TokenDePruebaSeguro1234567890ABCD";
        almacen.GuardarTokenGitHub(tokenOriginal, MetodoAutenticacionGitHub.TokenPersonal);

        string archivo = Path.Combine(dirSeguro, "github_auth.bin");
        Assert.True(File.Exists(archivo));

        // Asegurar que no está guardado en texto plano
        byte[] bytes = File.ReadAllBytes(archivo);
        Assert.DoesNotContain("ghp_TokenDePruebaSeguro", Encoding.UTF8.GetString(bytes));

        var (leido, metodo) = almacen.LeerTokenGitHub();
        Assert.Equal(tokenOriginal, leido);
        Assert.Equal(MetodoAutenticacionGitHub.TokenPersonal, metodo);

        almacen.EliminarTokenGitHub();
        Assert.False(File.Exists(archivo));
        var (despues, _) = almacen.LeerTokenGitHub();
        Assert.Null(despues);
    }

    [Fact]
    public async Task DebeAutenticarListarYCrearRepositoriosEnServicioGitHub()
    {
        string dirSeguro = Path.Combine(_carpetaTemporal, "almacen_api");
        var almacen = new AlmacenCredencialesSeguro(dirSeguro);

        using var handler = new HandlerGitHubSimulado(req =>
        {
            string ruta = req.RequestUri?.AbsolutePath ?? string.Empty;
            if (req.Method == HttpMethod.Get && ruta == "/user")
            {
                var resp = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        {
                          "login": "CynexStudios",
                          "name": "Jaderson Liriano",
                          "email": "jaderson@example.com",
                          "avatar_url": "https://avatars.githubusercontent.com/u/1?v=4",
                          "html_url": "https://github.com/CynexStudios",
                          "public_repos": 12,
                          "total_private_repos": 3
                        }
                        """, Encoding.UTF8, "application/json")
                };
                resp.Headers.TryAddWithoutValidation("X-OAuth-Scopes", "repo, read:user");
                return resp;
            }

            if (req.Method == HttpMethod.Get && ruta == "/user/repos")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        [
                          {
                            "id": 101,
                            "name": "alba-ecosistema",
                            "full_name": "CynexStudios/alba-ecosistema",
                            "description": "Cibao & Xynex Code Editor",
                            "private": false,
                            "clone_url": "https://github.com/CynexStudios/alba-ecosistema.git",
                            "html_url": "https://github.com/CynexStudios/alba-ecosistema",
                            "default_branch": "main",
                            "language": "C#",
                            "updated_at": "2026-11-19T10:00:00Z"
                          }
                        ]
                        """, Encoding.UTF8, "application/json")
                };
            }

            if (req.Method == HttpMethod.Get && ruta == "/repos/CynexStudios/alba-ecosistema")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        {
                          "id": 101,
                          "name": "alba-ecosistema",
                          "full_name": "CynexStudios/alba-ecosistema",
                          "description": "Cibao & Xynex Code Editor",
                          "private": false,
                          "clone_url": "https://github.com/CynexStudios/alba-ecosistema.git",
                          "html_url": "https://github.com/CynexStudios/alba-ecosistema",
                          "default_branch": "main",
                          "language": "C#",
                          "updated_at": "2026-11-19T10:00:00Z"
                        }
                        """, Encoding.UTF8, "application/json")
                };
            }

            if (req.Method == HttpMethod.Post && ruta == "/user/repos")
            {
                string bodyReq = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult() ?? string.Empty;
                if (bodyReq.Contains("\"alba-ecosistema\"", StringComparison.Ordinal))
                {
                    return new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
                    {
                        Content = new StringContent("""{"message":"Repository creation failed.","errors":[{"message":"name already exists on this account"}]}""", Encoding.UTF8, "application/json")
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.Created)
                {
                    Content = new StringContent("""
                        {
                          "id": 202,
                          "name": "mi-app-cibao",
                          "full_name": "CynexStudios/mi-app-cibao",
                          "description": "Proyecto nuevo",
                          "private": true,
                          "clone_url": "https://github.com/CynexStudios/mi-app-cibao.git",
                          "html_url": "https://github.com/CynexStudios/mi-app-cibao",
                          "default_branch": "main",
                          "language": null,
                          "updated_at": "2026-11-19T12:00:00Z"
                        }
                        """, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var github = new ServicioGitHub(almacen, handler);
        var loginRes = await github.IniciarSesionConTokenAsync("ghp_ValidoPrueba123456789012345");
        Assert.True(loginRes.Exito);
        Assert.True(github.EstaConectado);
        Assert.NotNull(github.CuentaActual);
        Assert.Equal("CynexStudios", github.CuentaActual!.Login);
        Assert.Contains("repo", github.CuentaActual!.PermisosToken);

        var reposRes = await github.ListarRepositoriosAsync();
        Assert.True(reposRes.Exito);
        Assert.Single(reposRes.Repositorios);
        Assert.Equal("alba-ecosistema", reposRes.Repositorios[0].Nombre);

        var crearRes = await github.CrearRepositorioAsync("mi-app-cibao", "Proyecto nuevo", esPrivado: true);
        Assert.True(crearRes.Exito);
        Assert.NotNull(crearRes.Repositorio);
        Assert.True(crearRes.Repositorio!.EsPrivado);
        Assert.Equal("CynexStudios/mi-app-cibao", crearRes.Repositorio!.NombreCompleto);

        var enlazarRes = await github.CrearOEnlazarRepositorioAsync("alba-ecosistema", "Existente", esPrivado: false);
        Assert.True(enlazarRes.Exito);
        Assert.True(enlazarRes.YaExistia);
        Assert.NotNull(enlazarRes.Repositorio);
        Assert.Equal("CynexStudios/alba-ecosistema", enlazarRes.Repositorio!.NombreCompleto);

        github.CerrarSesion();
        Assert.False(github.EstaConectado);
        Assert.Null(github.CuentaActual);
    }

    [Fact]
    public void DebeDetectarSecretosYGenerarGitIgnoreAntesDePublicar()
    {
        string proyecto = Path.Combine(_carpetaTemporal, "proyecto_secretos");
        Directory.CreateDirectory(proyecto);

        File.WriteAllText(Path.Combine(proyecto, ".env"), "SECRET=123456");
        File.WriteAllText(Path.Combine(proyecto, "clave.pem"), "-----BEGIN PRIVATE KEY-----\nABC");
        File.WriteAllText(Path.Combine(proyecto, "principal.cibao"), "variable tok = \"ghp_ABCDEF1234567890ABCDEF123456\"\nimprimir(tok)\n");

        var (archivos, alertas) = ServicioGit.EscanearSecretos(proyecto);
        Assert.Equal(3, archivos.Count);
        Assert.True(alertas.Count >= 3);
        Assert.Contains(alertas, a => a.RutaRelativa == ".env");
        Assert.Contains(alertas, a => a.RutaRelativa == "clave.pem");
        Assert.Contains(alertas, a => a.RutaRelativa == "principal.cibao" && a.NumeroLinea == 1);

        string rutaIgnore = ServicioGit.GenerarGitIgnore(proyecto);
        Assert.True(File.Exists(rutaIgnore));
        string contenidoIgnore = File.ReadAllText(rutaIgnore);
        Assert.Contains(".env", contenidoIgnore);
        Assert.Contains("*.pem", contenidoIgnore);
        Assert.Contains("node_modules/", contenidoIgnore);
    }

    [Fact]
    public async Task DebeEjecutarFlujoGitLocalRealSiGitEstaDisponible()
    {
        var git = new ServicioGit();
        if (!await git.EstaGitDisponibleAsync())
        {
            return;
        }

        string repo = Path.Combine(_carpetaTemporal, "repo_local_real");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "principal.cibao"), "imprimir(\"Hola Git\")\n");

        var init = await git.InicializarRepositorioAsync(repo, "main");
        Assert.True(init.Exito);

        var prep = await git.PrepararTodoAsync(repo);
        Assert.True(prep.Exito);

        var commit = await git.CrearCommitAsync(repo, "Commit inicial prueba", "Tester", "tester@example.com");
        Assert.True(commit.Exito);

        File.WriteAllText(Path.Combine(repo, "principal.cibao"), "imprimir(\"Hola Git v2\")\n");
        EstadoRepositorioGit estado = await git.ObtenerEstadoAsync(repo);
        Assert.True(estado.EsRepositorio);
        Assert.Equal("main", estado.RamaActual);
        Assert.Single(estado.Cambios);
        Assert.Single(estado.Historial);

        string diff = await git.ObtenerDiferenciasAsync(repo, estado.Cambios[0]);
        Assert.Contains("Hola Git v2", diff);

        var rama = await git.CrearRamaAsync(repo, "feature/cibao");
        Assert.True(rama.Exito);
        EstadoRepositorioGit estadoRama = await git.ObtenerEstadoAsync(repo);
        Assert.Equal("feature/cibao", estadoRama.RamaActual);
    }

    [Fact]
    public async Task DebeInstalarPaqueteIntegradoVerificadoYActualizarCapacidadesLenguaje()
    {
        string dirAddons = Path.Combine(_carpetaTemporal, "addons_tools");
        Directory.CreateDirectory(dirAddons);

        var gestorAddons = new GestorAddons(dirAddons);
        var detector = new DetectorEntornos();
        var gestorTools = new GestorTools(gestorAddons, detector);
        var proveedor = new ProveedorLenguajeIde(gestorAddons, detector);

        ElementoTool? goTool = gestorTools.Catalogo.FirstOrDefault(t => t.Id == "tool-go");
        Assert.NotNull(goTool);
        Assert.Equal("pack-go-lang", goTool!.PaqueteAddonIncorporadoId);

        var installRes = await gestorTools.InstalarPaqueteGuiadoAsync(goTool);
        Assert.True(installRes.Exito);
        Assert.Contains("SHA-256", installRes.Mensaje);

        Assert.NotNull(gestorAddons.ObtenerAddonPorExtension(".go"));

        CapacidadesLenguajeTool capGo = await proveedor.EvaluarCapacidadesLenguajeAsync(".go");
        Assert.True(capGo.SoporteEdicion);
        Assert.True(capGo.Autocompletado);

        CapacidadesLenguajeTool capCibao = await proveedor.EvaluarCapacidadesLenguajeAsync(".cibao");
        Assert.True(capCibao.SoporteEdicion);
        Assert.True(capCibao.Autocompletado);
        Assert.True(capCibao.CompiladorOInterprete);
        Assert.True(capCibao.HerramientasConstruccion);
    }

    [Fact]
    public void DebeDetectarDependenciasRealesDeProyecto()
    {
        string dirProy = Path.Combine(_carpetaTemporal, "proyecto_deps");
        Directory.CreateDirectory(dirProy);

        File.WriteAllText(Path.Combine(dirProy, "requirements.txt"), "requests==2.32.0\n");
        File.WriteAllText(Path.Combine(dirProy, "package.json"), "{ \"name\": \"app\" }");
        File.WriteAllText(Path.Combine(dirProy, "App.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var deps = GestorTools.DetectarDependenciasProyecto(dirProy);
        Assert.Equal(3, deps.Count);
        Assert.Contains(deps, d => d.ArchivoManifiesto == "requirements.txt" && d.ComandoInstalacion.Contains("pip"));
        Assert.Contains(deps, d => d.ArchivoManifiesto == "package.json" && d.ComandoInstalacion == "npm install");
        Assert.Contains(deps, d => d.ArchivoManifiesto == "App.csproj" && d.ComandoInstalacion == "dotnet restore");
    }
}
