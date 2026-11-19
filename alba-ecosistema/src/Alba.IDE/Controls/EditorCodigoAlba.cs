using System.Reflection;
using System.Xml;
using Alba.IDE.Models;
using Alba.IDE.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace Alba.IDE.Controls;

/// <summary>
/// Editor de código Alba Studio 2.0 basado en AvaloniaEdit con soporte de temas dinámicos,
/// diagnósticos en vivo, zoom, autocompletado, cierre automático e indentación inteligente.
/// </summary>
public sealed class EditorCodigoAlba : TextEditor
{
    private readonly MarcadorErroresEditor _marcadorErrores = new();
    private IHighlightingDefinition? _resaltadoAlba;
    private CompletionWindow? _ventanaCompletado;
    private ServicioAutocompletado? _servicioAutocompletado;
    private GestorAddons? _gestorAddons;
    private GestorTemas? _gestorTemas;
    private ConfiguracionIDE? _configuracion;
    private string _rutaArchivoActual = string.Empty;

    protected override Type StyleKeyOverride => typeof(TextEditor);

    public event Action<int, int>? CursorCambiado;
    public event Action<double>? TamanoFuenteCambiado;

    public EditorCodigoAlba()
    {
        ShowLineNumbers = true;
        FontFamily = new FontFamily("JetBrains Mono, Cascadia Mono, Consolas, SF Mono, DejaVu Sans Mono, monospace");
        FontSize = 14;
        Background = Brush.Parse("#0B111E");
        Foreground = Brush.Parse("#E2E8F0");
        LineNumbersForeground = Brush.Parse("#64748B");
        Padding = new Thickness(10, 8);

        Options.EnableHyperlinks = false;
        Options.ConvertTabsToSpaces = true;
        Options.IndentationSize = 4;
        Options.HighlightCurrentLine = true;

        _resaltadoAlba = CargarResaltadoEmbebido("Alba.IDE.Resources.alba.xshd");
        TextArea.TextView.BackgroundRenderers.Add(_marcadorErrores);

        TextArea.TextEntering += AlIngresarTexto;
        TextArea.TextEntered += AlTextoIngresado;
        TextArea.KeyDown += AlPresionarTecla;
        TextArea.PointerWheelChanged += AlMoverRueda;
        TextArea.PointerMoved += AlMoverPuntero;
        TextArea.Caret.PositionChanged += (_, _) =>
        {
            CursorCambiado?.Invoke(TextArea.Caret.Line, TextArea.Caret.Column);
        };
    }

    public void ConfigurarServicios(
        ServicioAutocompletado autocompletado,
        GestorAddons gestorAddons,
        GestorTemas? gestorTemas = null,
        ConfiguracionIDE? configuracion = null)
    {
        _servicioAutocompletado = autocompletado;
        _gestorAddons = gestorAddons;
        _gestorTemas = gestorTemas;
        _configuracion = configuracion;

        if (_gestorTemas is not null)
        {
            AplicarTema(_gestorTemas.TemaActual, _configuracion);
        }
    }

    public void AplicarTema(TemaIDE tema, ConfiguracionIDE? config)
    {
        _configuracion = config;

        if (Color.TryParse(tema.FondoEditor, out Color bg))
            Background = new SolidColorBrush(bg);
        if (Color.TryParse(tema.TextoPrincipal, out Color fg))
            Foreground = new SolidColorBrush(fg);
        if (Color.TryParse(tema.TextoApagado, out Color ln))
            LineNumbersForeground = new SolidColorBrush(ln);
        if (Color.TryParse(tema.FondoLineaActual, out Color hl))
            TextArea.TextView.CurrentLineBackground = new SolidColorBrush(hl);

        if (config is not null)
        {
            FontSize = Math.Clamp(config.TamanoFuenteEditor, 10, 30);
            Options.IndentationSize = Math.Clamp(config.TamanoTabulacion, 2, 8);
        }

        if (_gestorTemas is not null)
        {
            _resaltadoAlba = _gestorTemas.ConstruirResaltadoAlba(config) ?? _resaltadoAlba;
        }

        if (!string.IsNullOrEmpty(_rutaArchivoActual))
        {
            AplicarResaltadoPorExtension(_rutaArchivoActual);
        }

        TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Background);
    }

    public void CargarArchivo(ArchivoAbierto? archivo)
    {
        if (archivo is null)
        {
            Document = new AvaloniaEdit.Document.TextDocument(string.Empty);
            SyntaxHighlighting = null;
            _rutaArchivoActual = string.Empty;
            IsReadOnly = true;
            ActualizarMarcadoresErrores([]);
            return;
        }

        IsReadOnly = false;
        _rutaArchivoActual = archivo.RutaArchivo;
        Document = archivo.Documento;
        AplicarResaltadoPorExtension(archivo.RutaArchivo);
    }

    public void ActualizarMarcadoresErrores(IEnumerable<ProblemaCodigo> problemas)
    {
        _marcadorErrores.ActualizarProblemas(problemas);
        TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Selection);
    }

    public void IrALineaColumna(int linea, int columna)
    {
        if (Document is null || Document.LineCount == 0)
        {
            return;
        }

        int lineaSegura = Math.Clamp(linea, 1, Document.LineCount);
        var docLinea = Document.GetLineByNumber(lineaSegura);
        int colSegura = Math.Clamp(columna, 1, docLinea.Length + 1);

        TextArea.Caret.Line = lineaSegura;
        TextArea.Caret.Column = colSegura;
        ScrollToLine(lineaSegura);
        Focus();
    }

    public void AplicarResaltadoPorExtension(string rutaArchivo)
    {
        string ext = Path.GetExtension(rutaArchivo).ToLowerInvariant();
        if (ext is ".cibao" or ".alb")
        {
            SyntaxHighlighting = _resaltadoAlba;
            return;
        }

        AddonCargado? addon = _gestorAddons?.ObtenerAddonPorExtension(ext);
        if (addon?.RutaSintaxisResuelta is not null && File.Exists(addon.RutaSintaxisResuelta))
        {
            try
            {
                using Stream stream = File.OpenRead(addon.RutaSintaxisResuelta);
                using var reader = XmlReader.Create(stream);
                SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                return;
            }
            catch
            {
                // Si el XSHD externo falla, intenta resaltado integrado
            }
        }

        string extNormalizada = ext switch
        {
            ".htm" => ".html",
            ".cc" or ".cxx" => ".cpp",
            _ => ext
        };
        SyntaxHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(extNormalizada);
    }

    public bool BuscarSiguiente(string termino, bool distinguirMayusculas)
    {
        if (string.IsNullOrEmpty(termino) || Document is null)
        {
            return false;
        }

        StringComparison comparacion = distinguirMayusculas
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        string texto = Document.Text;
        int inicio = Math.Min(CaretOffset, texto.Length);
        int encontrado = texto.IndexOf(termino, inicio, comparacion);

        if (encontrado < 0 && inicio > 0)
        {
            encontrado = texto.IndexOf(termino, 0, comparacion);
        }

        if (encontrado < 0)
        {
            return false;
        }

        Select(encontrado, termino.Length);
        CaretOffset = encontrado + termino.Length;
        ScrollToLine(Document.GetLineByOffset(encontrado).LineNumber);
        return true;
    }

    public bool ReemplazarActual(string termino, string reemplazo, bool distinguirMayusculas)
    {
        if (string.IsNullOrEmpty(termino) || Document is null)
        {
            return false;
        }

        StringComparison comparacion = distinguirMayusculas
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        if (string.Equals(SelectedText, termino, comparacion))
        {
            Document.Replace(SelectionStart, SelectionLength, reemplazo);
            return BuscarSiguiente(termino, distinguirMayusculas);
        }

        return BuscarSiguiente(termino, distinguirMayusculas);
    }

    public int ReemplazarTodo(string termino, string reemplazo, bool distinguirMayusculas)
    {
        if (string.IsNullOrEmpty(termino) || Document is null)
        {
            return 0;
        }

        StringComparison comparacion = distinguirMayusculas
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        int indice = 0;
        int reemplazos = 0;

        Document.BeginUpdate();
        try
        {
            while ((indice = Document.Text.IndexOf(termino, indice, comparacion)) >= 0)
            {
                Document.Replace(indice, termino.Length, reemplazo);
                indice += reemplazo.Length;
                reemplazos++;
            }
        }
        finally
        {
            Document.EndUpdate();
        }

        return reemplazos;
    }

    private void AlMoverRueda(object? sender, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            return;
        }

        e.Handled = true;
        double nueva = Math.Clamp(FontSize + (e.Delta.Y > 0 ? 1 : -1), 10, 30);
        if (Math.Abs(nueva - FontSize) > 0.1)
        {
            FontSize = nueva;
            TamanoFuenteCambiado?.Invoke(nueva);
        }
    }

    private void AlMoverPuntero(object? sender, PointerEventArgs e)
    {
        Point pos = e.GetPosition(TextArea.TextView);
        TextViewPosition? tvPos = TextArea.TextView.GetPosition(pos + TextArea.TextView.ScrollOffset);
        if (tvPos is null)
        {
            ToolTip.SetTip(TextArea, null);
            return;
        }

        ProblemaCodigo? problema = _marcadorErrores.ObtenerProblemaEnLinea(tvPos.Value.Line);
        ToolTip.SetTip(TextArea, problema is not null ? $"[{problema.Codigo}] {problema.Mensaje}" : null);
    }

    private void AlIngresarTexto(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        if (_ventanaCompletado is not null && !char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '_')
        {
            _ventanaCompletado.CompletionList.RequestInsertion(e);
        }

        if (e.Text.Length == 1 && e.Text[0] is ')' or ']' or '}' or '"' or '\'')
        {
            int offset = TextArea.Caret.Offset;
            if (offset < Document.TextLength && Document.GetCharAt(offset) == e.Text[0])
            {
                TextArea.Caret.Offset++;
                e.Handled = true;
            }
        }
    }

    private void AlTextoIngresado(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        string? cierre = e.Text switch
        {
            "(" => ")",
            "[" => "]",
            "{" => "}",
            "\"" => "\"",
            "'" => "'",
            _ => null
        };

        if (cierre is not null)
        {
            int offset = TextArea.Caret.Offset;
            Document.Insert(offset, cierre);
            TextArea.Caret.Offset = offset;
            return;
        }

        if (e.Text == "\n" || e.Text == "\r")
        {
            AplicarIndentacionAutomatica();
            return;
        }

        if (e.Text == "}")
        {
            DesindentarLlaveCierre();
            return;
        }

        if (char.IsLetter(e.Text[0]) || e.Text[0] == '_')
        {
            string prefijo = ObtenerPrefijoActual();
            if (prefijo.Length >= 2)
            {
                MostrarAutocompletado(prefijo);
            }
        }
    }

    private void AlPresionarTecla(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            e.Handled = true;
            MostrarAutocompletado(ObtenerPrefijoActual());
        }
    }

    private void AplicarIndentacionAutomatica()
    {
        int lineaActualNum = TextArea.Caret.Line;
        if (lineaActualNum <= 1)
        {
            return;
        }

        var lineaAnterior = Document.GetLineByNumber(lineaActualNum - 1);
        string textoAnterior = Document.GetText(lineaAnterior);

        int espacios = 0;
        while (espacios < textoAnterior.Length && textoAnterior[espacios] == ' ')
        {
            espacios++;
        }

        string indentBase = new string(' ', espacios);
        if (textoAnterior.TrimEnd().EndsWith('{'))
        {
            indentBase += new string(' ', Math.Clamp(Options.IndentationSize, 2, 8));
        }

        if (indentBase.Length > 0)
        {
            Document.Insert(TextArea.Caret.Offset, indentBase);
        }
    }

    private void DesindentarLlaveCierre()
    {
        var linea = Document.GetLineByNumber(TextArea.Caret.Line);
        string textoLinea = Document.GetText(linea);
        int tab = Math.Clamp(Options.IndentationSize, 2, 8);
        string sangria = new string(' ', tab);
        if (textoLinea.Trim() == "}" && textoLinea.StartsWith(sangria))
        {
            Document.Remove(linea.Offset, tab);
        }
    }

    private string ObtenerPrefijoActual()
    {
        int offset = TextArea.Caret.Offset;
        int inicio = offset;
        while (inicio > 0)
        {
            char c = Document.GetCharAt(inicio - 1);
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                break;
            }
            inicio--;
        }
        return Document.GetText(inicio, offset - inicio);
    }

    private void MostrarAutocompletado(string prefijo)
    {
        if (_servicioAutocompletado is null || string.IsNullOrEmpty(_rutaArchivoActual))
        {
            return;
        }

        IReadOnlyList<ICompletionData> sugerencias = _servicioAutocompletado.ObtenerSugerencias(_rutaArchivoActual, prefijo);
        if (sugerencias.Count == 0)
        {
            _ventanaCompletado?.Close();
            return;
        }

        _ventanaCompletado = new CompletionWindow(TextArea)
        {
            StartOffset = Math.Max(0, TextArea.Caret.Offset - prefijo.Length)
        };

        foreach (ICompletionData item in sugerencias)
        {
            _ventanaCompletado.CompletionList.CompletionData.Add(item);
        }

        _ventanaCompletado.Closed += (_, _) => _ventanaCompletado = null;
        _ventanaCompletado.Show();
    }

    private static IHighlightingDefinition? CargarResaltadoEmbebido(string recurso)
    {
        Assembly asm = Assembly.GetExecutingAssembly();
        using Stream? stream = asm.GetManifestResourceStream(recurso);
        if (stream is null)
        {
            return null;
        }
        using var reader = XmlReader.Create(stream);
        return HighlightingLoader.Load(reader, HighlightingManager.Instance);
    }
}
