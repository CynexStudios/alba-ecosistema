using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Xml;
using Alba.IDE.Models;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace Alba.IDE.Services;

/// <summary>
/// Administra los temas visuales del IDE, actualización dinámica de recursos Avalonia
/// y compilación en tiempo real del resaltado de sintaxis de Alba.
/// </summary>
public sealed class GestorTemas
{
    private static readonly JsonSerializerOptions JsonOpciones = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly List<TemaIDE> _temas = [];
    public IReadOnlyList<TemaIDE> TemasDisponibles => _temas;
    public TemaIDE TemaActual { get; private set; } = new();

    public event Action<TemaIDE>? TemaCambiado;

    public GestorTemas()
    {
        CargarTemasEmbebidos();
        if (_temas.Count == 0)
        {
            _temas.Add(new TemaIDE());
        }
        TemaActual = _temas[0];
    }

    public TemaIDE? ObtenerTema(string id) =>
        _temas.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public void AplicarTema(string temaId, ConfiguracionIDE? config = null)
    {
        TemaIDE tema = ObtenerTema(temaId) ?? _temas[0];
        TemaActual = tema;

        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = tema.EsOscuro ? ThemeVariant.Dark : ThemeVariant.Light;
            ActualizarRecursoBrush(app, "FondoBaseBrush", tema.FondoBase);
            ActualizarRecursoBrush(app, "FondoSuperficieBrush", tema.FondoSuperficie);
            ActualizarRecursoBrush(app, "FondoPanelBrush", tema.FondoPanel);
            ActualizarRecursoBrush(app, "FondoBarraBrush", tema.FondoBarra);
            ActualizarRecursoBrush(app, "FondoEditorBrush", tema.FondoEditor);
            ActualizarRecursoBrush(app, "FondoLineaActualBrush", tema.FondoLineaActual);
            ActualizarRecursoBrush(app, "FondoHoverBrush", tema.FondoHover);
            ActualizarRecursoBrush(app, "BordeSutilBrush", tema.BordeSutil);
            ActualizarRecursoBrush(app, "TextoPrincipalBrush", tema.TextoPrincipal);
            ActualizarRecursoBrush(app, "TextoSecundarioBrush", tema.TextoSecundario);
            ActualizarRecursoBrush(app, "TextoApagadoBrush", tema.TextoApagado);
            ActualizarRecursoBrush(app, "AcentoPrimarioBrush", tema.AcentoPrimario);
            ActualizarRecursoBrush(app, "AcentoSecundarioBrush", tema.AcentoSecundario);
            ActualizarRecursoBrush(app, "ExitoColorBrush", tema.ExitoColor);
            ActualizarRecursoBrush(app, "AdvertenciaColorBrush", tema.AdvertenciaColor);
            ActualizarRecursoBrush(app, "ErrorColorBrush", tema.ErrorColor);
        }

        if (config is not null)
        {
            config.TemaId = tema.Id;
        }

        TemaCambiado?.Invoke(tema);
    }

    public IHighlightingDefinition? ConstruirResaltadoAlba(ConfiguracionIDE? config)
    {
        ColoresSintaxisConfig colores = config is { UsarSintaxisPersonalizada: true }
            ? config.SintaxisPersonalizada
            : TemaActual.Sintaxis;

        Assembly asm = Assembly.GetExecutingAssembly();
        using Stream? stream = asm.GetManifestResourceStream("Alba.IDE.Resources.alba.xshd");
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        string xml = reader.ReadToEnd();

        // Sustituye los colores definidos en alba.xshd por los del tema o personalización del usuario
        xml = ReemplazarColorXshd(xml, "Comentario", colores.Comentario);
        xml = ReemplazarColorXshd(xml, "Cadena", colores.Cadena);
        xml = ReemplazarColorXshd(xml, "Numero", colores.Numero);
        xml = ReemplazarColorXshd(xml, "PalabraControl", colores.PalabraControl);
        xml = ReemplazarColorXshd(xml, "PalabraDeclaracion", colores.PalabraDeclaracion);
        xml = ReemplazarColorXshd(xml, "TipoDato", colores.TipoDato);
        xml = ReemplazarColorXshd(xml, "ValorEspecial", colores.ValorEspecial);
        xml = ReemplazarColorXshd(xml, "FuncionIntegrada", colores.FuncionIntegrada);
        xml = ReemplazarColorXshd(xml, "OperadorLogico", colores.OperadorLogico);
        xml = ReemplazarColorXshd(xml, "Delimitador", colores.Delimitador);

        using var stringReader = new StringReader(xml);
        using var xmlReader = XmlReader.Create(stringReader);
        return HighlightingLoader.Load(xmlReader, HighlightingManager.Instance);
    }

    private void CargarTemasEmbebidos()
    {
        Assembly asm = Assembly.GetExecutingAssembly();
        string[] ordenPreferido =
        [
            "Alba.IDE.Assets.themes.alba-crepusculo.json",
            "Alba.IDE.Assets.themes.alba-medianoche.json",
            "Alba.IDE.Assets.themes.alba-amanecer.json"
        ];

        foreach (string recurso in ordenPreferido)
        {
            using Stream? stream = asm.GetManifestResourceStream(recurso);
            if (stream is null) continue;
            try
            {
                TemaIDE? tema = JsonSerializer.Deserialize<TemaIDE>(stream, JsonOpciones);
                if (tema is not null)
                {
                    _temas.Add(tema);
                }
            }
            catch
            {
                // Ignora tema malformado sin detener el arranque
            }
        }
    }

    private static void ActualizarRecursoBrush(Application app, string clave, string hex)
    {
        if (Color.TryParse(hex, out Color color))
        {
            app.Resources[clave] = new SolidColorBrush(color);
        }
    }

    private static string ReemplazarColorXshd(string xml, string nombre, string colorHex)
    {
        if (!Color.TryParse(colorHex, out _))
        {
            return xml;
        }

        string marcador = $"<Color name=\"{nombre}\" foreground=\"";
        int idx = xml.IndexOf(marcador, StringComparison.Ordinal);
        if (idx < 0) return xml;

        int inicioValor = idx + marcador.Length;
        int finValor = xml.IndexOf('"', inicioValor);
        if (finValor < 0) return xml;

        return string.Concat(xml.AsSpan(0, inicioValor), colorHex, xml.AsSpan(finValor));
    }
}
