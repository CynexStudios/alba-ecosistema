using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Alba.IDE.Services;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Alba.IDE.Views;

public partial class VentanaDocumentacion : Window
{
    private sealed record DocumentoItem(string Titulo, string Contenido, string? CodigoEjemploPrincipal)
    {
        public override string ToString() => Titulo;
    }

    private readonly Action<string>? _alProbarCodigo;
    private readonly List<DocumentoItem> _todosLosDocumentos = [];
    private ServidorWebLocal? _servidorDocs;

    public VentanaDocumentacion() : this(null)
    {
    }

    public VentanaDocumentacion(Action<string>? alProbarCodigo)
    {
        InitializeComponent();
        _alProbarCodigo = alProbarCodigo;
        Closed += (_, _) => _servidorDocs?.Dispose();
        CargarDocumentos();
    }

    private void CargarDocumentos()
    {
        _todosLosDocumentos.Clear();

        string? dirDocs = RutasUsuarioAlba.BuscarDirectorioInstalacion("docs");
        if (!string.IsNullOrWhiteSpace(dirDocs) && Directory.Exists(dirDocs))
        {
            foreach (string archivo in Directory.GetFiles(dirDocs, "*.md").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                string nombre = Path.GetFileNameWithoutExtension(archivo).Replace('_', ' ');
                string texto = File.ReadAllText(archivo, Encoding.UTF8);
                _todosLosDocumentos.Add(new DocumentoItem(nombre, texto, ExtraerBloqueCibao(texto)));
            }
        }

        if (_todosLosDocumentos.Count == 0)
        {
            _todosLosDocumentos.Add(new DocumentoItem(
                "01 Referencia Rápida Cibao Code 9.0",
                """
                # Cibao Code 9.0 — Guía Completa Desde Cero

                ## 1. Variables, Constantes e Interpolación
                ```cibao
                constante VERSION: texto = "9.0.0"
                variable usuario: texto = "Creador"
                variable nivel: entero = 1
                nivel++
                mostrar($"Bienvenido {usuario} a Cibao Code {VERSION} (Nivel {nivel})")
                ```

                ## 2. Los 7 Tipos de Datos
                - `entero`: números enteros (`42`, `-7`)
                - `decimal`: números reales (`3.1416`, `9.0`)
                - `texto`: cadenas `"..."` y multilínea `\"\"\"...\"\"\"`
                - `booleano`: `verdadero` / `falso`
                - `lista`: `[10, 20, 30]`
                - `diccionario`: `{ clave: "valor", activo: verdadero }`
                - `nulo`: ausencia segura de valor (combinable con `?.` y `??`)
                """.Replace("\\\"\\\"\\\"", "\"\"\""),
                """
                constante VERSION: texto = "9.0.0"
                variable usuario: texto = "Creador"
                variable nivel: entero = 1
                nivel++
                mostrar($"Bienvenido {usuario} a Cibao Code {VERSION} (Nivel {nivel})")
                """));
        }

        AplicarFiltro(string.Empty);
    }

    private void AplicarFiltro(string filtro)
    {
        string q = (filtro ?? string.Empty).Trim();
        var filtrados = string.IsNullOrWhiteSpace(q)
            ? _todosLosDocumentos
            : _todosLosDocumentos.Where(d =>
                d.Titulo.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                d.Contenido.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

        ListaDocumentos.ItemsSource = filtrados;
        if (filtrados.Count > 0)
        {
            ListaDocumentos.SelectedIndex = 0;
        }
    }

    private void AlFiltrarDocumentos(object? sender, TextChangedEventArgs e)
    {
        AplicarFiltro(TxtBuscarDoc.Text ?? string.Empty);
    }

    private void AlSeleccionarDocumento(object? sender, SelectionChangedEventArgs e)
    {
        if (ListaDocumentos.SelectedItem is DocumentoItem doc)
        {
            TxtTituloDoc.Text = doc.Titulo;
            TxtContenidoDoc.Text = doc.Contenido;
        }
    }

    private void AlProbarEjemploActual(object? sender, RoutedEventArgs e)
    {
        if (_alProbarCodigo is null) return;
        string codigo = (ListaDocumentos.SelectedItem as DocumentoItem)?.CodigoEjemploPrincipal
            ?? """
               // Ejemplo rápido en Cibao Code 9.0
               variable lenguajes = ["Cibao 9.0", "Python", "HTML", "TypeScript"]
               para item en lenguajes {
                   mostrar($"Soporte nativo: {item}")
               }
               """;

        _alProbarCodigo.Invoke(codigo);
        Close();
    }

    private void AlAbrirManualHtmlInteractivo(object? sender, RoutedEventArgs e)
    {
        string? dirDocs = RutasUsuarioAlba.BuscarDirectorioInstalacion("docs");
        string? rutaHtml = !string.IsNullOrWhiteSpace(dirDocs)
            ? Path.Combine(dirDocs, "html", "documentacion_cibao.html")
            : null;

        if (string.IsNullOrWhiteSpace(rutaHtml) || !File.Exists(rutaHtml))
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "xynex_docs_html");
            Directory.CreateDirectory(tempDir);
            rutaHtml = Path.Combine(tempDir, "documentacion_cibao.html");
            File.WriteAllText(rutaHtml, GenerarHtmlRespaldo(_todosLosDocumentos), Encoding.UTF8);
        }

        string carpetaRaiz = Path.GetDirectoryName(rutaHtml)!;
        _servidorDocs ??= new ServidorWebLocal();
        _servidorDocs.IniciarOActualizarRaiz(carpetaRaiz);
        string url = _servidorDocs.ConstruirUrlParaArchivo(rutaHtml);

        try
        {
            string? chromium = BuscarNavegadorApp();
            if (!string.IsNullOrWhiteSpace(chromium))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = chromium,
                    Arguments = $"--app=\"{url}\"",
                    UseShellExecute = false
                });
                return;
            }

            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Ignora si no existe visor HTML en entorno headless
        }
    }

    private static string? ExtraerBloqueCibao(string markdown)
    {
        Match m = Regex.Match(markdown, @"```(?:cibao|alba)\s*\r?\n([\s\S]*?)\r?\n```", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    private static string? BuscarNavegadorApp()
    {
        string[] candidatos = OperatingSystem.IsWindows()
            ? [
                @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Google\Chrome\Application\chrome.exe"
              ]
            : ["google-chrome", "chromium", "chromium-browser", "brave-browser", "microsoft-edge"];

        foreach (string c in candidatos)
        {
            if (File.Exists(c)) return c;
            if (!c.Contains(Path.DirectorySeparatorChar))
            {
                string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                foreach (string dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
                {
                    string full = Path.Combine(dir.Trim(), c);
                    if (File.Exists(full)) return full;
                }
            }
        }
        return null;
    }

    private static string GenerarHtmlRespaldo(List<DocumentoItem> docs)
    {
        var sb = new StringBuilder();
        sb.Append("""
            <!DOCTYPE html><html lang="es"><head><meta charset="utf-8">
            <title>Documentación Cibao Code 9.0 — Xynex Code Editor</title>
            <style>
            body{background:#1e1e1e;color:#d4d4d4;font-family:system-ui,sans-serif;max-width:960px;margin:32px auto;padding:0 20px;line-height:1.6}
            h1,h2{color:#4fc1ff} pre{background:#111;border:1px solid #333;padding:14px;border-radius:8px;overflow:auto;color:#ce9178}
            section{background:#252526;border:1px solid #3c3c3c;border-radius:10px;padding:18px;margin-bottom:20px}
            </style></head><body><h1>Cibao Code 9.0 &amp; Xynex Code Editor</h1>
            """);
        foreach (var d in docs)
        {
            sb.Append($"<section><h2>{System.Net.WebUtility.HtmlEncode(d.Titulo)}</h2><pre>{System.Net.WebUtility.HtmlEncode(d.Contenido)}</pre></section>");
        }
        sb.Append("</body></html>");
        return sb.ToString();
    }
}
