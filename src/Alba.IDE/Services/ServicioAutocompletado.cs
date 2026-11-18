using Alba.Engine.Lexer;
using Alba.IDE.Models;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace Alba.IDE.Services;

public sealed class ElementoAutocompletado : ICompletionData
{
    public IImage? Image => null;
    public string Text { get; }
    public object Content => $"{Text}  ({Categoria})";
    public object Description { get; }
    public double Priority => 0;
    public string Categoria { get; }
    private readonly string _textoInsertar;

    public ElementoAutocompletado(string text, string categoria, string descripcion, string? textoInsertar = null)
    {
        Text = text;
        Categoria = categoria;
        Description = descripcion;
        _textoInsertar = textoInsertar ?? text;
    }

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, _textoInsertar);
    }
}

/// <summary>
/// Genera sugerencias de autocompletado para Alba (.alb) y lenguajes de complementos activos.
/// </summary>
public sealed class ServicioAutocompletado
{
    private readonly GestorAddons _gestorAddons;

    private static readonly (string Nombre, string Categoria, string Descripcion, string? Insercion)[] FuncionesAlba =
    [
        ("imprimir", "E/S", "Imprime valores en la consola", "imprimir()"),
        ("leer", "E/S", "Lee una línea desde la consola", "leer(\"\")"),
        ("rango", "Estándar", "Genera una lista numérica secuencial", "rango(0, 10)"),
        ("longitud", "Estándar", "Devuelve la longitud de una lista o texto", "longitud()"),
        ("tipo_de", "Estándar", "Devuelve el nombre del tipo del valor", "tipo_de()"),
        ("convertir_entero", "Conversión", "Convierte texto/decimal a entero", "convertir_entero()"),
        ("convertir_decimal", "Conversión", "Convierte texto/entero a decimal", "convertir_decimal()"),
        ("convertir_texto", "Conversión", "Convierte cualquier valor a texto", "convertir_texto()"),
        ("agregar", "Listas", "Agrega un elemento al final de una lista", "agregar(, )"),
        ("eliminar", "Listas", "Elimina y devuelve el elemento en un índice", "eliminar(, )")
    ];

    public ServicioAutocompletado(GestorAddons gestorAddons)
    {
        _gestorAddons = gestorAddons;
    }

    public IReadOnlyList<ICompletionData> ObtenerSugerencias(string rutaArchivo, string prefijo)
    {
        var resultados = new List<ICompletionData>();
        string ext = Path.GetExtension(rutaArchivo).ToLowerInvariant();

        if (ext == ".alb")
        {
            foreach (string kw in PalabrasReservadas.ObtenerTodas().OrderBy(k => k))
            {
                if (string.IsNullOrEmpty(prefijo) || kw.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
                {
                    resultados.Add(new ElementoAutocompletado(kw, "Palabra clave", $"Palabra reservada de Alba: '{kw}'"));
                }
            }

            foreach (var fn in FuncionesAlba)
            {
                if (string.IsNullOrEmpty(prefijo) || fn.Nombre.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
                {
                    resultados.Add(new ElementoAutocompletado(fn.Nombre, fn.Categoria, fn.Descripcion, fn.Insercion));
                }
            }

            return resultados;
        }

        AddonCargado? addon = _gestorAddons.ObtenerAddonPorExtension(ext);
        if (addon is not null)
        {
            foreach (SnippetAddon snippet in addon.Snippets)
            {
                if (string.IsNullOrEmpty(prefijo) || snippet.Prefijo.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
                {
                    resultados.Add(new ElementoAutocompletado(
                        snippet.Prefijo,
                        addon.Manifiesto.Lenguaje,
                        $"{snippet.Titulo}: {snippet.Descripcion}",
                        snippet.Cuerpo));
                }
            }
        }

        return resultados;
    }
}
