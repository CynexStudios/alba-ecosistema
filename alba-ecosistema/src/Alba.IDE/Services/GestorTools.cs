using System.Diagnostics;
using Alba.IDE.Models;

namespace Alba.IDE.Services;

/// <summary>
/// Administra el ecosistema completo de Tools integrado en Xynex Code Editor:
/// catálogo verificado multi-lenguaje, detección real de entornos, instalación guiada de packs,
/// gestión de dependencias de proyecto y administración de complementos locales.
/// </summary>
public sealed class GestorTools
{
    private readonly GestorAddons _gestorAddons;
    private readonly DetectorEntornos _detectorEntornos;
    private readonly List<ElementoTool> _catalogo = [];

    public IReadOnlyList<ElementoTool> Catalogo => _catalogo;

    public GestorTools(GestorAddons gestorAddons, DetectorEntornos detectorEntornos)
    {
        _gestorAddons = gestorAddons;
        _detectorEntornos = detectorEntornos;
        ConstruirCatalogoInicial();
    }

    public void ConstruirCatalogoInicial()
    {
        var entornos = _detectorEntornos.ObtenerUltimoEstadoSinBloquear();
        PoblarCatalogo(entornos, new Dictionary<string, (bool Disponible, string Version)>(StringComparer.OrdinalIgnoreCase));
    }

    public async Task RefrescarCatalogoAsync(bool forzarRecarga = false)
    {
        _gestorAddons.RecargarAddons();

        var entornosTask = _detectorEntornos.DetectarTodosAsync(forzarRecarga);
        var extendidasTask = _detectorEntornos.DetectarHerramientasExtendidasAsync(forzarRecarga);

        await Task.WhenAll(entornosTask, extendidasTask);
        PoblarCatalogo(await entornosTask, await extendidasTask);
    }

    public IReadOnlyList<ElementoTool> Filtrar(
        string? textoBusqueda,
        CategoriaHerramienta categoria,
        bool soloInstalados)
    {
        IEnumerable<ElementoTool> query = _catalogo;

        if (categoria != CategoriaHerramienta.Todas)
        {
            query = query.Where(t => t.Categoria == categoria);
        }

        if (soloInstalados)
        {
            query = query.Where(t => t.Instalado);
        }

        if (!string.IsNullOrWhiteSpace(textoBusqueda))
        {
            string filtro = textoBusqueda.Trim();
            query = query.Where(t =>
                t.Nombre.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
                t.Descripcion.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
                t.CategoriaTexto.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
                t.Requisitos.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
                t.Dependencias.Contains(filtro, StringComparison.OrdinalIgnoreCase));
        }

        return query.ToList();
    }

    public async Task<(bool Exito, string Mensaje)> InstalarPaqueteGuiadoAsync(ElementoTool tool)
    {
        if (!string.IsNullOrWhiteSpace(tool.PaqueteAddonIncorporadoId))
        {
            var (exito, mensaje, _) = PaquetesIntegradosTools.InstalarPaqueteIncorporado(tool.PaqueteAddonIncorporadoId, _gestorAddons);
            await RefrescarCatalogoAsync(forzarRecarga: false);
            return (exito, mensaje);
        }

        if (!string.IsNullOrWhiteSpace(tool.ComandoInstalacionGuiada))
        {
            return (false, $"Ejecución externa requiere confirmación explícita: {tool.ComandoInstalacionGuiada}");
        }

        return (false, "Esta herramienta no tiene instalador automático disponible; sigue las instrucciones de su fuente oficial.");
    }

    public static IReadOnlyList<(string Nombre, string ArchivoManifiesto, string ComandoInstalacion)> DetectarDependenciasProyecto(string? rutaProyecto)
    {
        var lista = new List<(string, string, string)>();
        if (string.IsNullOrWhiteSpace(rutaProyecto) || !Directory.Exists(rutaProyecto))
        {
            return lista;
        }

        string reqPy = Path.Combine(rutaProyecto, "requirements.txt");
        if (File.Exists(reqPy))
        {
            lista.Add(("Python (pip)", "requirements.txt", "python -m pip install -r requirements.txt"));
        }

        string? csproj = Directory.GetFiles(rutaProyecto, "*.csproj").FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(csproj))
        {
            lista.Add((".NET NuGet", Path.GetFileName(csproj), "dotnet restore"));
        }

        string pkgJson = Path.Combine(rutaProyecto, "package.json");
        if (File.Exists(pkgJson))
        {
            lista.Add(("Node.js (npm)", "package.json", "npm install"));
        }

        return lista;
    }

    public static async Task<(bool Exito, string Salida)> EjecutarComandoDependenciasAsync(string rutaProyecto, string comando)
    {
        string shell = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        string argFlag = OperatingSystem.IsWindows() ? "/C" : "-c";

        var psi = new ProcessStartInfo
        {
            FileName = shell,
            WorkingDirectory = rutaProyecto,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add(argFlag);
        psi.ArgumentList.Add(comando);

        try
        {
            using var proc = new Process { StartInfo = psi };
            if (!proc.Start()) return (false, "No se pudo iniciar el gestor de dependencias.");
            string stdout = await proc.StandardOutput.ReadToEndAsync();
            string stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();
            string combinado = string.Join(Environment.NewLine, new[] { stdout.Trim(), stderr.Trim() }.Where(s => !string.IsNullOrWhiteSpace(s)));
            return (proc.ExitCode == 0, string.IsNullOrWhiteSpace(combinado) ? "Operación completada." : combinado);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public (bool Exito, string Mensaje) InstalarAddonDesdeCarpeta(string carpeta) =>
        _gestorAddons.InstalarDesdeCarpeta(carpeta);

    public void CambiarEstadoAddon(string idAddon, bool activar) =>
        _gestorAddons.CambiarEstado(idAddon, activar);

    public (bool Exito, string Mensaje) DesinstalarAddon(string idAddon) =>
        _gestorAddons.DesinstalarAddon(idAddon);

    private bool EstaAddonActivo(string id) =>
        _gestorAddons.Addons.Any(a => a.Manifiesto.Id.Equals(id, StringComparison.OrdinalIgnoreCase) && a.Activo && a.EsValido);

    private void PoblarCatalogo(
        IReadOnlyList<EstadoEntornoLenguaje> entornos,
        Dictionary<string, (bool Disponible, string Version)> ext)
    {
        _catalogo.Clear();
        bool Has(string k) => ext.TryGetValue(k, out var v) && v.Disponible;
        string Ver(string k, string def) => ext.TryGetValue(k, out var v) && v.Disponible && !string.IsNullOrWhiteSpace(v.Version) ? v.Version : def;

        // 1. Lenguaje Cibao + Biblioteca Estándar + Plantillas
        _catalogo.Add(new ElementoTool
        {
            Id = "cibao-engine",
            Nombre = "Cibao Programming Language Motor v5.0",
            Descripcion = "Intérprete oficial, analizador léxico/sintáctico AST y consola interactiva para .cibao y .alb.",
            Version = RutasUsuarioAlba.VersionActual,
            Autor = "Jaderson Liriano (CynexStudios)",
            Categoria = CategoriaHerramienta.Lenguajes,
            CategoriaTexto = "Lenguajes",
            FuenteOficial = "Integrado en Xynex Code Editor",
            Requisitos = "Ninguno",
            PermisosRequeridos = "Lectura/ejecución en espacio de trabajo",
            Instalado = true,
            Activo = true,
            EsIntegrado = true,
            EstadoInstalacion = "Integrado y activo",
            Capacidades = new CapacidadesLenguajeTool
            {
                SoporteEdicion = true, Autocompletado = true, CompiladorOInterprete = true,
                Depurador = false, FrameworksYBibliotecas = true, HerramientasConstruccion = true
            }
        });

        _catalogo.Add(new ElementoTool
        {
            Id = "cibao-stdlib",
            Nombre = "Biblioteca Estándar Cibao",
            Descripcion = "Funciones integradas de consola, conversión, listas, cadenas y control de flujo.",
            Version = RutasUsuarioAlba.VersionActual,
            Autor = "CynexStudios",
            Categoria = CategoriaHerramienta.Bibliotecas,
            CategoriaTexto = "Bibliotecas",
            FuenteOficial = "Integrado",
            Requisitos = "Motor Cibao",
            Instalado = true,
            Activo = true,
            EsIntegrado = true,
            EstadoInstalacion = "Integrada"
        });

        // 2. Python + Pack oficial
        EstadoEntornoLenguaje? py = entornos.FirstOrDefault(e => e.Lenguaje == LenguajeSoportado.Python);
        bool pyOk = py?.Disponible ?? false;
        bool pyPackOk = EstaAddonActivo("pack-python-xynex");
        _catalogo.Add(new ElementoTool
        {
            Id = "tool-python",
            Nombre = "Python 3 + Paquete Xynex",
            Descripcion = "Intérprete Python 3, pip, resaltado sintáctico y snippets integrados para .py.",
            Version = py?.VersionDetectada ?? "3.x",
            Autor = "Python Software Foundation / CynexStudios",
            Categoria = CategoriaHerramienta.Interpretes,
            CategoriaTexto = "Intérpretes",
            FuenteOficial = "https://www.python.org/downloads/",
            Requisitos = "python o python3 en PATH",
            Dependencias = "pip (para requirements.txt)",
            PermisosRequeridos = "Ejecución de intérprete Python y escritura en carpeta de addons",
            ComandoVerificacion = "python --version",
            ComandoInstalacionGuiada = OperatingSystem.IsWindows() ? "winget install -e --id Python.Python.3.12" : null,
            PaqueteAddonIncorporadoId = "pack-python-xynex",
            Instalado = pyOk && pyPackOk,
            Activo = pyOk,
            EstadoInstalacion = pyOk
                ? (pyPackOk ? $"Completo ({py!.VersionDetectada})" : $"Motor listo ({py!.VersionDetectada}) · Falta Pack Xynex")
                : "Intérprete no detectado",
            QueFaltaTexto = !pyOk
                ? "Instala Python 3 en el sistema y activa el Paquete Xynex aquí con 1 clic."
                : (!pyPackOk ? "Pulsa 'Instalar paquete integrado' para activar resaltado XSHD y snippets Python." : string.Empty),
            Capacidades = new CapacidadesLenguajeTool
            {
                SoporteEdicion = pyPackOk, Autocompletado = pyPackOk, CompiladorOInterprete = pyOk,
                Depurador = false, FrameworksYBibliotecas = Has("pip") || pyOk, HerramientasConstruccion = pyOk
            }
        });

        // 3. C# / .NET SDK
        EstadoEntornoLenguaje? cs = entornos.FirstOrDefault(e => e.Lenguaje == LenguajeSoportado.CSharp);
        bool csOk = cs?.Disponible ?? false;
        _catalogo.Add(new ElementoTool
        {
            Id = "tool-dotnet-sdk",
            Nombre = ".NET SDK (C#)",
            Descripcion = "Compilación, ejecución y gestión de NuGet para proyectos .csproj y archivos .cs.",
            Version = cs?.VersionDetectada ?? "8.0 / 10.0",
            Autor = "Microsoft",
            Categoria = CategoriaHerramienta.Sdk,
            CategoriaTexto = "SDK",
            FuenteOficial = "https://dotnet.microsoft.com/download",
            Requisitos = "dotnet CLI en PATH",
            Dependencias = "NuGet (incluido en dotnet)",
            PermisosRequeridos = "Compilación y ejecución de ensamblados .NET",
            ComandoVerificacion = "dotnet --version",
            ComandoInstalacionGuiada = OperatingSystem.IsWindows() ? "winget install -e --id Microsoft.DotNet.SDK.8" : null,
            Instalado = csOk,
            Activo = csOk,
            EstadoInstalacion = csOk ? $"Detectado ({cs!.VersionDetectada})" : "No detectado",
            QueFaltaTexto = csOk ? string.Empty : "Instalar .NET SDK 8.0 o superior.",
            Capacidades = new CapacidadesLenguajeTool
            {
                SoporteEdicion = true, Autocompletado = false, CompiladorOInterprete = csOk,
                Depurador = false, FrameworksYBibliotecas = csOk, HerramientasConstruccion = csOk
            }
        });

        // 4. C / C++20 + CMake + GDB
        EstadoEntornoLenguaje? cpp = entornos.FirstOrDefault(e => e.Lenguaje == LenguajeSoportado.Cpp);
        bool cppOk = cpp?.Disponible ?? false;
        bool cmakeOk = Has("cmake");
        bool gdbOk = Has("gdb");
        _catalogo.Add(new ElementoTool
        {
            Id = "tool-cpp-compiler",
            Nombre = "C / C++20 Toolchain (GCC / Clang)",
            Descripcion = "Compilación y ejecución nativa C/C++20 con soporte para CMake y depuración GDB.",
            Version = cpp?.VersionDetectada ?? "C++20",
            Autor = "GNU / LLVM",
            Categoria = CategoriaHerramienta.Compiladores,
            CategoriaTexto = "Compiladores",
            FuenteOficial = "https://gcc.gnu.org / https://clang.llvm.org",
            Requisitos = "g++ o clang++ en PATH",
            Dependencias = "CMake / GDB opcionales",
            ComandoVerificacion = "g++ --version",
            Instalado = cppOk,
            Activo = cppOk,
            EstadoInstalacion = cppOk ? $"Detectado ({cpp!.ComandoEjecutable})" : "No detectado",
            QueFaltaTexto = cppOk ? string.Empty : "Instalar GCC (MinGW-w64) o Clang y añadirlo al PATH.",
            Capacidades = new CapacidadesLenguajeTool
            {
                SoporteEdicion = true, Autocompletado = false, CompiladorOInterprete = cppOk,
                Depurador = gdbOk, FrameworksYBibliotecas = cppOk, HerramientasConstruccion = cmakeOk || cppOk
            }
        });

        // 5. Lenguajes adicionales con packs integrados (Web/TS, Java, Go, Rust, PHP, Ruby)
        AnadirLenguajeExtendido("tool-ts-web", "TypeScript / JavaScript", "Desarrollo Web, autocompletado y ejecución Node.js.",
            Has("tsc") || Has("node"), Ver("node", "Node / TS"), "node / tsc", "pack-web-ts",
            CategoriaHerramienta.DesarrolloWeb, "Desarrollo Web", "https://www.typescriptlang.org/");

        AnadirLenguajeExtendido("tool-java", "Java (JDK)", "Edición, snippets OOP y ejecución directa de clases .java.",
            Has("java"), Ver("java", "JDK 17+"), "java", "pack-java-lang",
            CategoriaHerramienta.Lenguajes, "Lenguajes", "https://adoptium.net/");

        AnadirLenguajeExtendido("tool-go", "Go (Golang)", "Compilación rápida, snippets y ejecución con go run.",
            Has("go"), Ver("go", "Go 1.22+"), "go", "pack-go-lang",
            CategoriaHerramienta.Lenguajes, "Lenguajes", "https://go.dev/dl/");

        AnadirLenguajeExtendido("tool-rust", "Rust (rustc / Cargo)", "Edición, compilación segura y binarios nativos Rust.",
            Has("rustc"), Ver("rustc", "Rustc"), "rustc", "pack-rust-lang",
            CategoriaHerramienta.Compiladores, "Compiladores", "https://rustup.rs/");

        AnadirLenguajeExtendido("tool-php", "PHP CLI", "Desarrollo backend rápido con PHP y resaltado integrado.",
            Has("php"), Ver("php", "PHP 8.x"), "php", "pack-php-lang",
            CategoriaHerramienta.Interpretes, "Intérpretes", "https://www.php.net/downloads.php");

        AnadirLenguajeExtendido("tool-ruby", "Ruby", "Scripting, automatización y ejecución con Ruby.",
            Has("ruby"), Ver("ruby", "Ruby 3.x"), "ruby", "pack-ruby-lang",
            CategoriaHerramienta.Interpretes, "Intérpretes", "https://www.ruby-lang.org/");

        // 6. Infraestructura, Dependencias, Web, Gráficos, Depuración, Productividad, IA
        _catalogo.Add(new ElementoTool
        {
            Id = "tool-deps-manager",
            Nombre = "Gestor de Dependencias de Proyecto",
            Descripcion = "Detecta automáticamente requirements.txt (pip), *.csproj (NuGet) y package.json (npm) para instalar dependencias del proyecto.",
            Version = "5.0.0",
            Autor = "CynexStudios",
            Categoria = CategoriaHerramienta.Dependencias,
            CategoriaTexto = "Dependencias",
            FuenteOficial = "Integrado en Xynex Code Editor",
            Requisitos = "Proyecto con archivo de dependencias + pip / dotnet / npm",
            PermisosRequeridos = "Descarga de paquetes dentro del proyecto tras confirmación",
            Instalado = true,
            Activo = true,
            EsIntegrado = true,
            EstadoInstalacion = "Integrado"
        });

        _catalogo.Add(new ElementoTool
        {
            Id = "tool-html-web",
            Nombre = "Navegador Web Integrado + Live Server HTML5",
            Descripcion = "Vista previa responsiva en vivo dentro del IDE (Escritorio/Tablet/Móvil), recarga automática, Live Server HTTP y captura de consola/errores Web.",
            Version = "HTML5 / Live Server",
            Autor = "CynexStudios",
            Categoria = CategoriaHerramienta.DesarrolloWeb,
            CategoriaTexto = "Desarrollo Web",
            FuenteOficial = "Integrado en Xynex Code Editor",
            Requisitos = "Ninguno (servidor HTTP local en 127.0.0.1)",
            PermisosRequeridos = "Puerto loopback local 127.0.0.1",
            Instalado = true,
            Activo = true,
            EsIntegrado = true,
            EstadoInstalacion = "Integrado y activo"
        });

        _catalogo.Add(new ElementoTool
        {
            Id = "tool-avalonia-ui",
            Nombre = "Avalonia UI Framework (Apps Gráficas .NET)",
            Descripcion = "Motor multiplataforma de ventanas nativas para Windows, Linux y macOS con plantilla ejecutable integrada.",
            Version = "11.2.1",
            Autor = "Avalonia Team",
            Categoria = CategoriaHerramienta.AplicacionesGraficas,
            CategoriaTexto = "Aplicaciones Gráficas",
            FuenteOficial = "https://avaloniaui.net/",
            Requisitos = ".NET SDK 8.0+ para compilar proyectos nuevos",
            Instalado = csOk,
            Activo = true,
            EsIntegrado = true,
            EstadoInstalacion = csOk ? "Listo para proyectos GUI C#" : "Integrado en IDE · Falta .NET SDK para compilar apps nuevas"
        });

        _catalogo.Add(new ElementoTool
        {
            Id = "tool-python-gui",
            Nombre = "Python Tkinter (Aplicaciones Gráficas)",
            Descripcion = "Creación y ejecución de ventanas de escritorio interactivas en Python.",
            Version = py?.VersionDetectada ?? "Tkinter",
            Autor = "Python Software Foundation",
            Categoria = CategoriaHerramienta.AplicacionesGraficas,
            CategoriaTexto = "Aplicaciones Gráficas",
            FuenteOficial = "https://docs.python.org/3/library/tkinter.html",
            Requisitos = "Python 3 + sesión gráfica del sistema operativo",
            Instalado = pyOk,
            Activo = pyOk,
            EstadoInstalacion = pyOk ? "Listo con Python" : "Requiere Python 3",
            QueFaltaTexto = pyOk ? string.Empty : "Instala Python 3 con soporte Tcl/Tk para ejecutar ventanas Tkinter."
        });

        _catalogo.Add(new ElementoTool
        {
            Id = "tool-xynex-templates",
            Nombre = "Plantillas Tripartitas Xynex 5.0",
            Descripcion = "Creación guiada de Proyectos, Programas directos y Archivos independientes.",
            Version = RutasUsuarioAlba.VersionActual,
            Autor = "CynexStudios",
            Categoria = CategoriaHerramienta.Frameworks,
            CategoriaTexto = "Frameworks",
            FuenteOficial = "Integrado",
            Requisitos = "Ninguno",
            Instalado = true,
            Activo = true,
            EsIntegrado = true,
            EstadoInstalacion = "Integrado"
        });

        bool gitOk = Has("git");
        _catalogo.Add(new ElementoTool
        {
            Id = "tool-git",
            Nombre = "Git CLI + Conector GitHub",
            Descripcion = "Control de versiones visual, commits, ramas, diferencias, clonación y publicación directa en GitHub.",
            Version = gitOk ? Ver("git", "2.x") : "2.x",
            Autor = "Git SCM / CynexStudios",
            Categoria = CategoriaHerramienta.Productividad,
            CategoriaTexto = "Productividad",
            FuenteOficial = "https://git-scm.com/",
            Requisitos = "git en PATH + cuenta GitHub opcional",
            ComandoVerificacion = "git --version",
            ComandoInstalacionGuiada = OperatingSystem.IsWindows() ? "winget install -e --id Git.Git" : null,
            Instalado = gitOk,
            Activo = gitOk,
            EstadoInstalacion = gitOk ? $"Detectado ({Ver("git", "git")})" : "Git CLI no detectado",
            QueFaltaTexto = gitOk ? string.Empty : "Instala Git para usar commits, ramas, clonación y publicación local."
        });

        _catalogo.Add(new ElementoTool
        {
            Id = "tool-gdb",
            Nombre = "Depurador Nativo GDB",
            Descripcion = "Depuración de binarios C y C++ compilados con símbolos de depuración.",
            Version = gdbOk ? Ver("gdb", "GDB") : "GDB",
            Autor = "GNU Project",
            Categoria = CategoriaHerramienta.Depuracion,
            CategoriaTexto = "Depuración",
            FuenteOficial = "https://sourceware.org/gdb/",
            Requisitos = "gdb en PATH",
            ComandoVerificacion = "gdb --version",
            Instalado = gdbOk,
            Activo = gdbOk,
            EstadoInstalacion = gdbOk ? $"Detectado ({Ver("gdb", "gdb")})" : "No detectado",
            QueFaltaTexto = gdbOk ? string.Empty : "Instalar GDB si deseas depurar binarios nativos C/C++."
        });

        _catalogo.Add(new ElementoTool
        {
            Id = "tool-ia-hub",
            Nombre = "Hub Multi-Proveedor de IA (Fase 5)",
            Descripcion = "Arquitectura preparada para conectar claves API del usuario con OpenAI, Gemini, Anthropic u Ollama.",
            Version = "Arquitectura 5.0",
            Autor = "CynexStudios",
            Categoria = CategoriaHerramienta.IntegracionesIA,
            CategoriaTexto = "Integraciones de IA",
            FuenteOficial = "Programado para Fase 5",
            Requisitos = "Disponible en Fase 5 con credenciales propias del usuario",
            Instalado = false,
            Activo = false,
            EstadoInstalacion = "Próxima fase (Fase 5)",
            QueFaltaTexto = "El panel de IA y Agentes se activará en la Fase 5."
        });

        // 7. Addons instalados localmente
        foreach (AddonCargado addon in _gestorAddons.Addons)
        {
            _catalogo.Add(new ElementoTool
            {
                Id = addon.Manifiesto.Id,
                Nombre = addon.Manifiesto.Nombre,
                Descripcion = string.IsNullOrWhiteSpace(addon.Manifiesto.Descripcion)
                    ? $"Complemento de lenguaje {addon.Manifiesto.Lenguaje} ({addon.ExtensionesTexto})."
                    : addon.Manifiesto.Descripcion,
                Version = addon.Manifiesto.Version,
                Autor = string.IsNullOrWhiteSpace(addon.Manifiesto.Autor) ? "Comunidad" : addon.Manifiesto.Autor,
                Categoria = CategoriaHerramienta.Extensiones,
                CategoriaTexto = $"Extensión · {addon.Manifiesto.Lenguaje}",
                FuenteOficial = addon.RutaCarpeta,
                Requisitos = $"Ejecutable: {addon.Manifiesto.Ejecutable} · Extensiones: {addon.ExtensionesTexto}",
                PermisosRequeridos = $"Ejecutar '{addon.Manifiesto.Ejecutable}' sobre archivos del proyecto",
                ComandoVerificacion = addon.Manifiesto.Ejecutable,
                Instalado = addon.EsValido,
                Activo = addon.Activo,
                EsAddonLocal = true,
                EstadoInstalacion = addon.EsValido
                    ? (addon.Activo ? "Activo" : "Desactivado")
                    : $"Bloqueado: {addon.EstadoSeguridad}",
                QueFaltaTexto = addon.EsValido ? string.Empty : addon.EstadoSeguridad,
                RutaAddon = addon.RutaCarpeta
            });
        }
    }

    private void AnadirLenguajeExtendido(
        string id,
        string nombre,
        string descripcion,
        bool runtimeOk,
        string version,
        string reqCmd,
        string packId,
        CategoriaHerramienta categoria,
        string catTexto,
        string fuenteOficial)
    {
        bool packOk = EstaAddonActivo(packId);
        _catalogo.Add(new ElementoTool
        {
            Id = id,
            Nombre = nombre,
            Descripcion = descripcion,
            Version = version,
            Autor = "CynexStudios / Oficial",
            Categoria = categoria,
            CategoriaTexto = catTexto,
            FuenteOficial = fuenteOficial,
            Requisitos = $"Comando '{reqCmd}' en PATH",
            PermisosRequeridos = "Instalación de paquete XSHD/snippets en addons de usuario",
            ComandoVerificacion = $"{reqCmd} --version",
            PaqueteAddonIncorporadoId = packId,
            Instalado = runtimeOk && packOk,
            Activo = runtimeOk || packOk,
            EstadoInstalacion = runtimeOk && packOk
                ? $"Completo ({version})"
                : (runtimeOk ? $"Runtime detectado ({version}) · Instalar pack IDE" : (packOk ? "Solo edición (falta runtime OS)" : "Disponible para instalar")),
            QueFaltaTexto = !runtimeOk && !packOk
                ? $"Instala '{reqCmd}' en tu sistema y pulsa 'Instalar paquete integrado'."
                : (!runtimeOk ? $"Falta el runtime '{reqCmd}' en tu sistema para ejecutar programas."
                    : (!packOk ? "Pulsa 'Instalar paquete integrado' para activar resaltado y snippets en el editor." : string.Empty)),
            Capacidades = new CapacidadesLenguajeTool
            {
                SoporteEdicion = packOk,
                Autocompletado = packOk,
                CompiladorOInterprete = runtimeOk,
                Depurador = false,
                FrameworksYBibliotecas = runtimeOk,
                HerramientasConstruccion = runtimeOk
            }
        });
    }
}
