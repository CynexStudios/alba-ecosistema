using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Alba.IDE.Models;

namespace Alba.IDE.Services;

/// <summary>
/// Sistema de creación unificado para Proyectos, Programas y Archivos independientes
/// en Cibao, Python, HTML, C# y C++, validado con los entornos reales detectados.
/// </summary>
public sealed class ServicioCreacion
{
    private static readonly JsonSerializerOptions JsonOpciones = new() { WriteIndented = true };

    public IReadOnlyList<PlantillaCreacion> ObtenerPlantillas(
        ModalidadCreacion? modalidad = null,
        LenguajeSoportado? lenguaje = null,
        IReadOnlyList<EstadoEntornoLenguaje>? entornosDetectados = null)
    {
        var entornosPorLenguaje = (entornosDetectados ?? [])
            .ToDictionary(e => e.Lenguaje, e => e);

        List<PlantillaCreacion> catalogo =
        [
            // ==================== PROYECTOS ====================
            new PlantillaCreacion
            {
                Id = "proj-cibao-consola",
                Nombre = "Proyecto Cibao Consola",
                Descripcion = "Estructura profesional con principal.cibao, manifiesto xynexproj y README listos para ejecutar.",
                Lenguaje = LenguajeSoportado.Cibao,
                Modalidad = ModalidadCreacion.Proyecto,
                ExtensionArchivoPrincipal = ".cibao",
                ArchivoPrincipalSugerido = "principal.cibao",
                DependenciasRecomendadas = "Motor Cibao 9.0 integrado"
            },
            new PlantillaCreacion
            {
                Id = "proj-cibao-modular",
                Nombre = "Proyecto Cibao Modular",
                Descripcion = "Incluye carpeta de módulos y utilidades iniciales en Cibao 9.0 para aplicaciones organizadas.",
                Lenguaje = LenguajeSoportado.Cibao,
                Modalidad = ModalidadCreacion.Proyecto,
                ExtensionArchivoPrincipal = ".cibao",
                ArchivoPrincipalSugerido = "principal.cibao",
                DependenciasRecomendadas = "Motor Cibao 9.0 integrado"
            },
            new PlantillaCreacion
            {
                Id = "proj-python-app",
                Nombre = "Proyecto Python",
                Descripcion = "Carpeta estructurada con main.py, requirements.txt y documentación inicial.",
                Lenguaje = LenguajeSoportado.Python,
                Modalidad = ModalidadCreacion.Proyecto,
                ExtensionArchivoPrincipal = ".py",
                ArchivoPrincipalSugerido = "main.py",
                DependenciasRecomendadas = "Intérprete Python 3",
                RequiereCompiladorExterno = true
            },
            new PlantillaCreacion
            {
                Id = "proj-python-gui",
                Nombre = "Aplicación Gráfica Python (Tkinter)",
                Descripcion = "Ventana de escritorio real con tema oscuro, botones interactivos y salida en vivo.",
                Lenguaje = LenguajeSoportado.Python,
                Modalidad = ModalidadCreacion.Proyecto,
                ExtensionArchivoPrincipal = ".py",
                ArchivoPrincipalSugerido = "app_gui.py",
                DependenciasRecomendadas = "Python 3 + Tkinter",
                RequiereCompiladorExterno = true
            },
            new PlantillaCreacion
            {
                Id = "proj-html-sitio",
                Nombre = "Proyecto Web HTML / CSS / JS",
                Descripcion = "Sitio moderno con index.html, estilos.css y app.js con Vista Previa Live Server.",
                Lenguaje = LenguajeSoportado.Html,
                Modalidad = ModalidadCreacion.Proyecto,
                ExtensionArchivoPrincipal = ".html",
                ArchivoPrincipalSugerido = "index.html",
                DependenciasRecomendadas = "Navegador Web Integrado + Live Server"
            },
            new PlantillaCreacion
            {
                Id = "proj-csharp-consola",
                Nombre = "Proyecto C# (.NET Consola)",
                Descripcion = "Genera .csproj y Program.cs compatibles con dotnet run.",
                Lenguaje = LenguajeSoportado.CSharp,
                Modalidad = ModalidadCreacion.Proyecto,
                ExtensionArchivoPrincipal = ".cs",
                ArchivoPrincipalSugerido = "Program.cs",
                DependenciasRecomendadas = "SDK de .NET",
                RequiereCompiladorExterno = true
            },
            new PlantillaCreacion
            {
                Id = "proj-csharp-avalonia",
                Nombre = "Aplicación Gráfica C# (Avalonia UI)",
                Descripcion = "Ventana multiplataforma Windows / Linux / macOS en Avalonia 11 lista para compilar.",
                Lenguaje = LenguajeSoportado.CSharp,
                Modalidad = ModalidadCreacion.Proyecto,
                ExtensionArchivoPrincipal = ".cs",
                ArchivoPrincipalSugerido = "MainWindow.axaml.cs",
                DependenciasRecomendadas = "SDK de .NET",
                RequiereCompiladorExterno = true
            },
            new PlantillaCreacion
            {
                Id = "proj-cpp-consola",
                Nombre = "Proyecto C++20",
                Descripcion = "Genera main.cpp y CMakeLists.txt configurados para compilación estándar C++20.",
                Lenguaje = LenguajeSoportado.Cpp,
                Modalidad = ModalidadCreacion.Proyecto,
                ExtensionArchivoPrincipal = ".cpp",
                ArchivoPrincipalSugerido = "main.cpp",
                DependenciasRecomendadas = "g++ o clang++",
                RequiereCompiladorExterno = true
            },

            // ==================== PROGRAMAS ====================
            new PlantillaCreacion
            {
                Id = "prog-cibao-directo",
                Nombre = "Programa en Cibao",
                Descripcion = "Crea un programa ejecutable directo (.cibao) sin estructura pesada de proyecto.",
                Lenguaje = LenguajeSoportado.Cibao,
                Modalidad = ModalidadCreacion.Programa,
                ExtensionArchivoPrincipal = ".cibao",
                ArchivoPrincipalSugerido = "programa.cibao",
                DependenciasRecomendadas = "Motor Cibao v9.0 integrado"
            },
            new PlantillaCreacion
            {
                Id = "prog-python-script",
                Nombre = "Programa Python",
                Descripcion = "Script Python listo para ejecutar en un clic con entrada y salida estándar.",
                Lenguaje = LenguajeSoportado.Python,
                Modalidad = ModalidadCreacion.Programa,
                ExtensionArchivoPrincipal = ".py",
                ArchivoPrincipalSugerido = "programa.py",
                DependenciasRecomendadas = "Intérprete Python 3",
                RequiereCompiladorExterno = true
            },
            new PlantillaCreacion
            {
                Id = "prog-html-pagina",
                Nombre = "Programa Web Autocontenido",
                Descripcion = "Página HTML5 interactiva con estilos y lógica JS embebidos en un solo archivo.",
                Lenguaje = LenguajeSoportado.Html,
                Modalidad = ModalidadCreacion.Programa,
                ExtensionArchivoPrincipal = ".html",
                ArchivoPrincipalSugerido = "pagina.html",
                DependenciasRecomendadas = "HTML5 integrado"
            },
            new PlantillaCreacion
            {
                Id = "prog-csharp-directo",
                Nombre = "Programa C# Ejecutable",
                Descripcion = "Programa C# autocontenido con instrucciones de nivel superior listo para compilar.",
                Lenguaje = LenguajeSoportado.CSharp,
                Modalidad = ModalidadCreacion.Programa,
                ExtensionArchivoPrincipal = ".cs",
                ArchivoPrincipalSugerido = "programa.cs",
                DependenciasRecomendadas = "SDK de .NET",
                RequiereCompiladorExterno = true
            },
            new PlantillaCreacion
            {
                Id = "prog-cpp-directo",
                Nombre = "Programa C++ Ejecutable",
                Descripcion = "Archivo único C++20 listo para compilar y ejecutar desde el editor.",
                Lenguaje = LenguajeSoportado.Cpp,
                Modalidad = ModalidadCreacion.Programa,
                ExtensionArchivoPrincipal = ".cpp",
                ArchivoPrincipalSugerido = "programa.cpp",
                DependenciasRecomendadas = "g++ o clang++",
                RequiereCompiladorExterno = true
            },

            // ==================== ARCHIVOS INDEPENDIENTES ====================
            new PlantillaCreacion
            {
                Id = "file-cibao",
                Nombre = "Archivo Cibao (.cibao)",
                Descripcion = "Archivo independiente de código fuente Cibao para edición y ejecución rápida.",
                Lenguaje = LenguajeSoportado.Cibao,
                Modalidad = ModalidadCreacion.ArchivoIndependiente,
                ExtensionArchivoPrincipal = ".cibao",
                ArchivoPrincipalSugerido = "archivo.cibao",
                DependenciasRecomendadas = "Motor Cibao 9.0 integrado"
            },
            new PlantillaCreacion
            {
                Id = "file-python",
                Nombre = "Archivo Python (.py)",
                Descripcion = "Archivo fuente Python suelto sin carpeta de proyecto.",
                Lenguaje = LenguajeSoportado.Python,
                Modalidad = ModalidadCreacion.ArchivoIndependiente,
                ExtensionArchivoPrincipal = ".py",
                ArchivoPrincipalSugerido = "script.py",
                DependenciasRecomendadas = "Python 3 (para ejecutar)",
                RequiereCompiladorExterno = false
            },
            new PlantillaCreacion
            {
                Id = "file-html",
                Nombre = "Archivo HTML (.html)",
                Descripcion = "Documento HTML5 suelto listo para editar y abrir en el navegador.",
                Lenguaje = LenguajeSoportado.Html,
                Modalidad = ModalidadCreacion.ArchivoIndependiente,
                ExtensionArchivoPrincipal = ".html",
                ArchivoPrincipalSugerido = "index.html",
                DependenciasRecomendadas = "HTML5 integrado"
            },
            new PlantillaCreacion
            {
                Id = "file-js",
                Nombre = "Archivo JavaScript (.js)",
                Descripcion = "Script JavaScript moderno suelto sin carpeta de proyecto.",
                Lenguaje = LenguajeSoportado.JavaScript,
                Modalidad = ModalidadCreacion.ArchivoIndependiente,
                ExtensionArchivoPrincipal = ".js",
                ArchivoPrincipalSugerido = "app.js",
                DependenciasRecomendadas = "Node.js / Web"
            },
            new PlantillaCreacion
            {
                Id = "file-ts",
                Nombre = "Archivo TypeScript (.ts)",
                Descripcion = "Archivo TypeScript tipado independiente sin carpeta obligatoria.",
                Lenguaje = LenguajeSoportado.TypeScript,
                Modalidad = ModalidadCreacion.ArchivoIndependiente,
                ExtensionArchivoPrincipal = ".ts",
                ArchivoPrincipalSugerido = "modulo.ts",
                DependenciasRecomendadas = "TypeScript (tsc / ts-node)"
            },
            new PlantillaCreacion
            {
                Id = "file-json",
                Nombre = "Archivo JSON (.json)",
                Descripcion = "Archivo de configuración o datos JSON con validación nativa.",
                Lenguaje = LenguajeSoportado.Json,
                Modalidad = ModalidadCreacion.ArchivoIndependiente,
                ExtensionArchivoPrincipal = ".json",
                ArchivoPrincipalSugerido = "datos.json",
                DependenciasRecomendadas = "Validador JSON integrado"
            },
            new PlantillaCreacion
            {
                Id = "file-csharp",
                Nombre = "Archivo C# (.cs)",
                Descripcion = "Archivo independiente en C# listo para editar o compilar.",
                Lenguaje = LenguajeSoportado.CSharp,
                Modalidad = ModalidadCreacion.ArchivoIndependiente,
                ExtensionArchivoPrincipal = ".cs",
                ArchivoPrincipalSugerido = "Clase.cs",
                DependenciasRecomendadas = "SDK de .NET (para ejecutar)",
                RequiereCompiladorExterno = false
            },
            new PlantillaCreacion
            {
                Id = "file-cpp",
                Nombre = "Archivo C++ (.cpp)",
                Descripcion = "Archivo independiente en C++20.",
                Lenguaje = LenguajeSoportado.Cpp,
                Modalidad = ModalidadCreacion.ArchivoIndependiente,
                ExtensionArchivoPrincipal = ".cpp",
                ArchivoPrincipalSugerido = "codigo.cpp",
                DependenciasRecomendadas = "g++ o clang++ (para ejecutar)",
                RequiereCompiladorExterno = false
            }
        ];

        foreach (PlantillaCreacion plantilla in catalogo)
        {
            if (plantilla.RequiereCompiladorExterno &&
                entornosPorLenguaje.TryGetValue(plantilla.Lenguaje, out EstadoEntornoLenguaje? env) &&
                !env.Disponible)
            {
                plantilla.DisponibleEnEquipo = false;
                plantilla.MotivoNoDisponible = $"{plantilla.DependenciasRecomendadas} no detectado en el sistema";
            }
            else
            {
                plantilla.DisponibleEnEquipo = true;
                plantilla.MotivoNoDisponible = string.Empty;
            }
        }

        return catalogo
            .Where(p => (!modalidad.HasValue || p.Modalidad == modalidad.Value) &&
                        (!lenguaje.HasValue || p.Lenguaje == lenguaje.Value))
            .ToList();
    }

    public (bool Exito, string Mensaje, string? RutaProyecto, string RutaArchivoPrincipal, bool EsProyecto) Crear(SolicitudCreacion solicitud)
    {
        if (string.IsNullOrWhiteSpace(solicitud.CarpetaDestino))
        {
            return (false, "Selecciona una carpeta de destino válida.", null, string.Empty, false);
        }

        Directory.CreateDirectory(solicitud.CarpetaDestino);
        string nombreSeguro = SanitizarNombre(solicitud.Nombre);

        if (solicitud.Modalidad == ModalidadCreacion.ArchivoIndependiente ||
            solicitud.Modalidad == ModalidadCreacion.Programa)
        {
            string ext = solicitud.Plantilla.ExtensionArchivoPrincipal;
            string nombreArchivo = Path.HasExtension(nombreSeguro)
                ? nombreSeguro
                : $"{nombreSeguro}{ext}";
            string rutaArchivo = AsegurarRutaUnicaArchivo(Path.Combine(solicitud.CarpetaDestino, nombreArchivo));
            string contenido = GenerarContenidoArchivoUnico(solicitud.Lenguaje, Path.GetFileNameWithoutExtension(rutaArchivo), solicitud.Modalidad);
            File.WriteAllText(rutaArchivo, contenido, Encoding.UTF8);

            string tipoEtiqueta = solicitud.Modalidad == ModalidadCreacion.Programa ? "Programa" : "Archivo";
            return (true, $"{tipoEtiqueta} '{Path.GetFileName(rutaArchivo)}' creado.", null, rutaArchivo, false);
        }

        string rutaProyecto = AsegurarRutaUnicaDirectorio(Path.Combine(solicitud.CarpetaDestino, nombreSeguro));
        Directory.CreateDirectory(rutaProyecto);

        string rutaPrincipal = CrearEstructuraProyecto(rutaProyecto, nombreSeguro, solicitud);
        if (solicitud.InicializarGit)
        {
            IntentarInicializarGit(rutaProyecto);
        }

        return (true, $"Proyecto '{Path.GetFileName(rutaProyecto)}' creado correctamente.", rutaProyecto, rutaPrincipal, true);
    }

    private static string CrearEstructuraProyecto(string rutaProyecto, string nombreProyecto, SolicitudCreacion solicitud)
    {
        string archivoPrincipal = solicitud.Plantilla.ArchivoPrincipalSugerido;

        var manifiesto = new
        {
            nombre = nombreProyecto,
            version = "9.0.0",
            ide = "Xynex Code Editor",
            lenguaje = solicitud.Lenguaje.ToString(),
            plantilla = solicitud.Plantilla.Id,
            archivoPrincipal
        };

        File.WriteAllText(
            Path.Combine(rutaProyecto, "proyecto.xynexproj.json"),
            JsonSerializer.Serialize(manifiesto, JsonOpciones),
            Encoding.UTF8);

        string rutaPrincipal = Path.Combine(rutaProyecto, archivoPrincipal);

        switch (solicitud.Lenguaje)
        {
            case LenguajeSoportado.Cibao:
                if (solicitud.Plantilla.Id == "proj-cibao-modular")
                {
                    string dirModulos = Path.Combine(rutaProyecto, "modulos");
                    Directory.CreateDirectory(dirModulos);
                    File.WriteAllText(
                        Path.Combine(dirModulos, "utilidades.cibao"),
                        """
                        # Módulo de utilidades Cibao

                        funcion formatearTitulo(texto nombre) -> texto {
                            retornar "=== " + nombre + " ==="
                        }
                        """,
                        Encoding.UTF8);
                }

                File.WriteAllText(
                    rutaPrincipal,
                    $$"""
                    // Proyecto: {{nombreProyecto}}
                    // Lenguaje: Cibao 9.0 · Xynex Code Editor 9.0

                    funcion principal() {
                        mostrar("Bienvenido a {{nombreProyecto}} en Cibao 9.0")
                    }

                    principal()
                    """,
                    Encoding.UTF8);
                break;

            case LenguajeSoportado.Python:
                if (solicitud.Plantilla.Id == "proj-python-gui")
                {
                    File.WriteAllText(
                        rutaPrincipal,
                        $$"""
                        # Aplicación Gráfica Python (Tkinter) - {{nombreProyecto}}
                        # Creado con Xynex Code Editor 9.0
                        import tkinter as tk

                        def main() -> None:
                            ventana = tk.Tk()
                            ventana.title("{{nombreProyecto}} — Xynex GUI")
                            ventana.geometry("460x260")
                            ventana.configure(bg="#0D1320")

                            titulo = tk.Label(
                                ventana,
                                text="{{nombreProyecto}}",
                                font=("Segoe UI", 16, "bold"),
                                bg="#0D1320",
                                fg="#38BDF8"
                            )
                            titulo.pack(pady=(22, 8))

                            entrada = tk.Entry(ventana, font=("Segoe UI", 11), width=28)
                            entrada.insert(0, "Cibao 9.0")
                            entrada.pack(pady=6)

                            estado = tk.Label(ventana, text="Listo", bg="#0D1320", fg="#E2E8F0", font=("Segoe UI", 11))
                            estado.pack(pady=10)

                            def saludar() -> None:
                                texto = entrada.get().strip() or "Mundo"
                                estado.config(text=f"¡Hola desde GUI Python, {texto}!")

                            boton = tk.Button(
                                ventana,
                                text="Probar evento GUI",
                                command=saludar,
                                bg="#38BDF8",
                                fg="#08101D",
                                font=("Segoe UI", 10, "bold"),
                                padx=14,
                                pady=6
                            )
                            boton.pack(pady=6)
                            ventana.mainloop()

                        if __name__ == "__main__":
                            main()
                        """,
                        Encoding.UTF8);
                }
                else
                {
                    File.WriteAllText(
                        rutaPrincipal,
                        $$"""
                        # Proyecto: {{nombreProyecto}}
                        # Creado con Xynex Code Editor 9.0

                        def main() -> None:
                            print("Bienvenido a {{nombreProyecto}} en Python")


                        if __name__ == "__main__":
                            main()
                        """,
                        Encoding.UTF8);
                }
                File.WriteAllText(Path.Combine(rutaProyecto, "requirements.txt"), "# Agrega dependencias de Python aquí\n", Encoding.UTF8);
                break;

            case LenguajeSoportado.Html:
                File.WriteAllText(
                    rutaPrincipal,
                    $$"""
                    <!DOCTYPE html>
                    <html lang="es">
                    <head>
                      <meta charset="UTF-8" />
                      <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                      <title>{{nombreProyecto}}</title>
                      <link rel="stylesheet" href="estilos.css" />
                    </head>
                    <body>
                      <main class="contenedor">
                        <h1>{{nombreProyecto}}</h1>
                        <p>Proyecto web creado con Xynex Code Editor 9.0.</p>
                        <button id="btnAccion" type="button">Probar interacción</button>
                        <p id="estado"></p>
                      </main>
                      <script src="app.js"></script>
                    </body>
                    </html>
                    """,
                    Encoding.UTF8);
                File.WriteAllText(
                    Path.Combine(rutaProyecto, "estilos.css"),
                    """
                    * { box-sizing: border-box; }
                    body {
                      margin: 0;
                      font-family: system-ui, -apple-system, sans-serif;
                      background: #0d1320;
                      color: #e2e8f0;
                      min-height: 100vh;
                      display: grid;
                      place-items: center;
                    }
                    .contenedor {
                      background: #141d2f;
                      border: 1px solid #23304a;
                      border-radius: 10px;
                      padding: 2rem;
                      max-width: 520px;
                    }
                    button {
                      background: #38bdf8;
                      color: #08101d;
                      border: 0;
                      border-radius: 6px;
                      padding: 0.6rem 1rem;
                      font-weight: 600;
                      cursor: pointer;
                    }
                    """,
                    Encoding.UTF8);
                File.WriteAllText(
                    Path.Combine(rutaProyecto, "app.js"),
                    """
                    const boton = document.getElementById("btnAccion");
                    const estado = document.getElementById("estado");
                    let clics = 0;

                    boton?.addEventListener("click", () => {
                      clics += 1;
                      if (estado) estado.textContent = `Interacción activa (${clics})`;
                    });
                    """,
                    Encoding.UTF8);
                break;

            case LenguajeSoportado.CSharp:
                if (solicitud.Plantilla.Id == "proj-csharp-avalonia")
                {
                    File.WriteAllText(
                        Path.Combine(rutaProyecto, $"{nombreProyecto}.csproj"),
                        """
                        <Project Sdk="Microsoft.NET.Sdk">
                          <PropertyGroup>
                            <OutputType>WinExe</OutputType>
                            <TargetFramework>net8.0</TargetFramework>
                            <Nullable>enable</Nullable>
                            <ImplicitUsings>enable</ImplicitUsings>
                            <RollForward>LatestMajor</RollForward>
                          </PropertyGroup>
                          <ItemGroup>
                            <PackageReference Include="Avalonia" Version="11.2.1" />
                            <PackageReference Include="Avalonia.Desktop" Version="11.2.1" />
                            <PackageReference Include="Avalonia.Themes.Fluent" Version="11.2.1" />
                          </ItemGroup>
                        </Project>
                        """,
                        Encoding.UTF8);

                    File.WriteAllText(
                        Path.Combine(rutaProyecto, "Program.cs"),
                        """
                        using Avalonia;

                        namespace AppGui;

                        internal static class Program
                        {
                            [STAThread]
                            public static void Main(string[] args) => BuildAvaloniaApp()
                                .StartWithClassicDesktopLifetime(args);

                            public static AppBuilder BuildAvaloniaApp() =>
                                AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
                        }
                        """,
                        Encoding.UTF8);

                    File.WriteAllText(
                        Path.Combine(rutaProyecto, "App.axaml"),
                        """
                        <Application xmlns="https://github.com/avaloniaui"
                                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                     x:Class="AppGui.App"
                                     RequestedThemeVariant="Dark">
                          <Application.Styles>
                            <FluentTheme />
                          </Application.Styles>
                        </Application>
                        """,
                        Encoding.UTF8);

                    File.WriteAllText(
                        Path.Combine(rutaProyecto, "App.axaml.cs"),
                        """
                        using Avalonia;
                        using Avalonia.Controls.ApplicationLifetimes;
                        using Avalonia.Markup.Xaml;

                        namespace AppGui;

                        public partial class App : Application
                        {
                            public override void Initialize() => AvaloniaXamlLoader.Load(this);

                            public override void OnFrameworkInitializationCompleted()
                            {
                                if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                                {
                                    desktop.MainWindow = new MainWindow();
                                }
                                base.OnFrameworkInitializationCompleted();
                            }
                        }
                        """,
                        Encoding.UTF8);

                    File.WriteAllText(
                        Path.Combine(rutaProyecto, "MainWindow.axaml"),
                        $$"""
                        <Window xmlns="https://github.com/avaloniaui"
                                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                x:Class="AppGui.MainWindow"
                                Title="{{nombreProyecto}} — Avalonia GUI"
                                Width="520"
                                Height="320"
                                Background="#0D1320"
                                Foreground="#E2E8F0">
                          <StackPanel Margin="24" Spacing="12" VerticalAlignment="Center">
                            <TextBlock Text="{{nombreProyecto}}" FontSize="22" FontWeight="Bold" Foreground="#38BDF8" />
                            <TextBlock Text="Aplicación gráfica multiplataforma creada desde Xynex Code Editor 9.0." FontSize="13" />
                            <Button Content="Probar clic" Click="AlHacerClic" HorizontalAlignment="Left" Padding="14,8" />
                            <TextBlock x:Name="TxtContador" Text="Clics: 0" FontSize="13" Foreground="#22C55E" />
                          </StackPanel>
                        </Window>
                        """,
                        Encoding.UTF8);

                    File.WriteAllText(
                        rutaPrincipal,
                        """
                        using Avalonia.Controls;
                        using Avalonia.Interactivity;

                        namespace AppGui;

                        public partial class MainWindow : Window
                        {
                            private int _clics;

                            public MainWindow() => InitializeComponent();

                            private void AlHacerClic(object? sender, RoutedEventArgs e)
                            {
                                _clics++;
                                TxtContador.Text = $"Clics: {_clics}";
                            }
                        }
                        """,
                        Encoding.UTF8);
                }
                else
                {
                    File.WriteAllText(
                        Path.Combine(rutaProyecto, $"{nombreProyecto}.csproj"),
                        """
                        <Project Sdk="Microsoft.NET.Sdk">
                          <PropertyGroup>
                            <OutputType>Exe</OutputType>
                            <TargetFramework>net8.0</TargetFramework>
                            <ImplicitUsings>enable</ImplicitUsings>
                            <Nullable>enable</Nullable>
                            <RollForward>LatestMajor</RollForward>
                          </PropertyGroup>
                        </Project>
                        """,
                        Encoding.UTF8);
                    File.WriteAllText(
                        rutaPrincipal,
                        $$"""
                        // Proyecto: {{nombreProyecto}}
                        Console.WriteLine("Bienvenido a {{nombreProyecto}} en C# (.NET)");
                        """,
                        Encoding.UTF8);
                }
                break;

            case LenguajeSoportado.Cpp:
                File.WriteAllText(
                    Path.Combine(rutaProyecto, "CMakeLists.txt"),
                    $$"""
                    cmake_minimum_required(VERSION 3.16)
                    project({{nombreProyecto}} LANGUAGES CXX)
                    set(CMAKE_CXX_STANDARD 20)
                    set(CMAKE_CXX_STANDARD_REQUIRED ON)
                    add_executable({{nombreProyecto}} main.cpp)
                    """,
                    Encoding.UTF8);
                File.WriteAllText(
                    rutaPrincipal,
                    $$"""
                    #include <iostream>

                    int main() {
                        std::cout << "Bienvenido a {{nombreProyecto}} en C++20\n";
                        return 0;
                    }
                    """,
                    Encoding.UTF8);
                break;
        }

        File.WriteAllText(
            Path.Combine(rutaProyecto, "README.md"),
            $"# {nombreProyecto}\n\nProyecto creado con Xynex Code Editor 9.0.\n",
            Encoding.UTF8);

        return rutaPrincipal;
    }

    private static string GenerarContenidoArchivoUnico(
        LenguajeSoportado lenguaje,
        string nombreBase,
        ModalidadCreacion modalidad)
    {
        return lenguaje switch
        {
            LenguajeSoportado.Cibao => modalidad == ModalidadCreacion.Programa
                ? $$"""
                  # Programa Cibao: {{nombreBase}}
                  # Creado con Xynex Code Editor 9.0

                  funcion principal() {
                      imprimir("Ejecutando programa '{{nombreBase}}' en Cibao")
                  }

                  principal()
                  """
                : $$"""
                  # Archivo Cibao: {{nombreBase}}

                  imprimir("Hola desde {{nombreBase}} en Cibao")
                  """,

            LenguajeSoportado.Python => modalidad == ModalidadCreacion.Programa
                ? $$"""
                  # Programa Python: {{nombreBase}}

                  def main() -> None:
                      print("Ejecutando programa '{{nombreBase}}' en Python")


                  if __name__ == "__main__":
                      main()
                  """
                : $$"""
                  # Archivo Python: {{nombreBase}}
                  print("Hola desde {{nombreBase}}")
                  """,

            LenguajeSoportado.Html => $$"""
                <!DOCTYPE html>
                <html lang="es">
                <head>
                  <meta charset="UTF-8" />
                  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                  <title>{{nombreBase}}</title>
                  <style>
                    body { font-family: system-ui, sans-serif; background: #0d1320; color: #e2e8f0; margin: 2rem; }
                  </style>
                </head>
                <body>
                  <h1>{{nombreBase}}</h1>
                  <p>Documento creado con Xynex Code Editor 9.0.</p>
                </body>
                </html>
                """,

            LenguajeSoportado.CSharp => $$"""
                // {{nombreBase}}.cs
                Console.WriteLine("Hola desde {{nombreBase}} en C#");
                """,

            LenguajeSoportado.Cpp => $$"""
                #include <iostream>

                int main() {
                    std::cout << "Hola desde {{nombreBase}} en C++20\n";
                    return 0;
                }
                """,

            LenguajeSoportado.JavaScript => $$"""
                // {{nombreBase}}.js — JavaScript en Xynex Code Editor 9.0
                const saludo = "Hola desde {{nombreBase}} en JavaScript";
                console.log(saludo);
                """,

            LenguajeSoportado.TypeScript => $$"""
                // {{nombreBase}}.ts — TypeScript en Xynex Code Editor 9.0
                interface SaludoConfig {
                  nombre: string;
                  version: number;
                }

                const cfg: SaludoConfig = { nombre: "{{nombreBase}}", version: 9.0 };
                console.log(`Hola desde ${cfg.nombre} v${cfg.version}`);
                """,

            LenguajeSoportado.Json => $$"""
                {
                  "nombre": "{{nombreBase}}",
                  "editor": "Xynex Code Editor",
                  "version": "9.0.0"
                }
                """,

            _ => string.Empty
        };
    }

    public static string GenerarPlantillaRapidaPorExtension(string extension, string nombreBase)
    {
        string ext = (extension ?? ".cibao").ToLowerInvariant();
        return ext switch
        {
            ".cibao" or ".alb" => $"// Archivo rápido Cibao 9.0: {nombreBase}\nmostrar(\"¡Hola desde {nombreBase} en Cibao 9.0!\")\n",
            ".py" => $"# Archivo rápido Python: {nombreBase}\nprint(\"¡Hola desde {nombreBase} en Python!\")\n",
            ".html" or ".htm" => GenerarContenidoArchivoUnico(LenguajeSoportado.Html, nombreBase, ModalidadCreacion.ArchivoIndependiente),
            ".css" => $"/* Hoja de estilos: {nombreBase} */\nbody {{\n  background: #0f172a;\n  color: #e2e8f0;\n  font-family: system-ui, sans-serif;\n}}\n",
            ".js" or ".mjs" => GenerarContenidoArchivoUnico(LenguajeSoportado.JavaScript, nombreBase, ModalidadCreacion.ArchivoIndependiente),
            ".ts" => GenerarContenidoArchivoUnico(LenguajeSoportado.TypeScript, nombreBase, ModalidadCreacion.ArchivoIndependiente),
            ".json" => GenerarContenidoArchivoUnico(LenguajeSoportado.Json, nombreBase, ModalidadCreacion.ArchivoIndependiente),
            ".cs" => GenerarContenidoArchivoUnico(LenguajeSoportado.CSharp, nombreBase, ModalidadCreacion.ArchivoIndependiente),
            ".cpp" => GenerarContenidoArchivoUnico(LenguajeSoportado.Cpp, nombreBase, ModalidadCreacion.ArchivoIndependiente),
            _ => string.Empty
        };
    }

    private static void IntentarInicializarGit(string rutaProyecto)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = "init",
                WorkingDirectory = rutaProyecto,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(2000);
        }
        catch
        {
            // Ignora si Git no está instalado en el equipo
        }
    }

    private static string SanitizarNombre(string nombre)
    {
        string limpio = string.Join("_", (nombre ?? string.Empty).Trim().Split(Path.GetInvalidFileNameChars()));
        return string.IsNullOrWhiteSpace(limpio) ? "NuevoElemento" : limpio;
    }

    private static string AsegurarRutaUnicaArchivo(string ruta)
    {
        if (!File.Exists(ruta)) return ruta;
        string dir = Path.GetDirectoryName(ruta) ?? string.Empty;
        string baseName = Path.GetFileNameWithoutExtension(ruta);
        string ext = Path.GetExtension(ruta);
        int i = 2;
        while (File.Exists(Path.Combine(dir, $"{baseName}_{i}{ext}")))
        {
            i++;
        }
        return Path.Combine(dir, $"{baseName}_{i}{ext}");
    }

    private static string AsegurarRutaUnicaDirectorio(string ruta)
    {
        if (!Directory.Exists(ruta)) return ruta;
        int i = 2;
        while (Directory.Exists($"{ruta}_{i}"))
        {
            i++;
        }
        return $"{ruta}_{i}";
    }
}
