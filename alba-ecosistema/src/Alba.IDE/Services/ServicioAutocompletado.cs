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
/// Genera sugerencias de autocompletado para Cibao (.cibao / .alb) y lenguajes de complementos activos.
/// </summary>
public sealed class ServicioAutocompletado
{
    private readonly GestorAddons _gestorAddons;

    private static readonly (string Nombre, string Categoria, string Descripcion, string? Insercion)[] FuncionesAlba =
    [
        ("imprimir", "E/S", "Imprime valores en la consola", "imprimir()"),
        ("leer", "E/S", "Lee una línea desde la consola", "leer(\"\")"),
        ("rango", "Colecciones", "Genera una lista numérica secuencial", "rango(0, 10)"),
        ("longitud", "Colecciones", "Devuelve la longitud de una lista, diccionario o texto", "longitud()"),
        ("tipo_de", "Tipos", "Devuelve el nombre del tipo del valor", "tipo_de()"),
        ("convertir_entero", "Conversión", "Convierte texto/decimal a entero", "convertir_entero()"),
        ("convertir_decimal", "Conversión", "Convierte texto/entero a decimal", "convertir_decimal()"),
        ("convertir_texto", "Conversión", "Convierte cualquier valor a texto", "convertir_texto()"),
        ("agregar", "Listas", "Agrega un elemento al final de una lista", "agregar(, )"),
        ("eliminar", "Listas", "Elimina y devuelve el elemento en un índice", "eliminar(, )"),
        ("mapear", "Funcional", "Transforma cada elemento de una lista usando una función", "mapear(lista, funcion(x) { retornar x })"),
        ("filtrar", "Funcional", "Filtra elementos de una lista mediante una función booleana", "filtrar(lista, funcion(x) { retornar x > 0 })"),
        ("reducir", "Funcional", "Acumula los elementos de una lista con un reductor", "reducir(lista, 0, funcion(acc, x) { retornar acc + x })"),
        ("ordenar", "Listas", "Devuelve una copia ordenada de la lista", "ordenar()"),
        ("invertir", "Listas", "Devuelve una copia invertida de la lista", "invertir()"),
        ("contiene_elemento", "Listas", "Indica si un elemento existe en la lista", "contiene_elemento(, )"),
        ("indice_de", "Listas", "Busca el índice de un elemento (-1 si no existe)", "indice_de(, )"),
        ("claves", "Diccionarios", "Obtiene la lista de claves de un diccionario", "claves()"),
        ("valores", "Diccionarios", "Obtiene la lista de valores de un diccionario", "valores()"),
        ("contiene_clave", "Diccionarios", "Verifica si una clave existe en el diccionario", "contiene_clave(, \"\")"),
        ("eliminar_clave", "Diccionarios", "Elimina una clave de un diccionario", "eliminar_clave(, \"\")"),
        ("json_a_texto", "JSON", "Serializa un valor o diccionario de Cibao a texto JSON", "json_a_texto()"),
        ("texto_a_json", "JSON", "Convierte un texto JSON válido a diccionario o lista", "texto_a_json()"),
        ("mayusculas", "Texto", "Convierte texto a mayúsculas", "mayusculas()"),
        ("minusculas", "Texto", "Convierte texto a minúsculas", "minusculas()"),
        ("recortar", "Texto", "Elimina espacios iniciales y finales", "recortar()"),
        ("dividir", "Texto", "Divide un texto por separador en una lista", "dividir(, \",\")"),
        ("unir", "Texto", "Une los elementos de una lista con un separador", "unir(, \", \")"),
        ("reemplazar", "Texto", "Reemplaza subcadenas dentro de un texto", "reemplazar(, \"\", \"\")"),
        ("contiene", "Texto/Colecciones", "Verifica si un texto, lista o diccionario contiene el elemento", "contiene(, )"),
        ("empieza_con", "Texto", "Comprueba si el texto inicia con un prefijo", "empieza_con(, \"\")"),
        ("termina_con", "Texto", "Comprueba si el texto finaliza con un sufijo", "termina_con(, \"\")"),
        ("subtexto", "Texto", "Extrae una subcadena por inicio y longitud opcional", "subtexto(, 0, 1)"),
        ("raiz", "Matemáticas", "Calcula la raíz cuadrada", "raiz()"),
        ("abs", "Matemáticas", "Calcula el valor absoluto", "abs()"),
        ("redondear", "Matemáticas", "Redondea al entero o decimales indicados", "redondear()"),
        ("piso", "Matemáticas", "Redondea hacia abajo al entero inferior", "piso()"),
        ("techo", "Matemáticas", "Redondea hacia arriba al entero superior", "techo()"),
        ("minimo", "Matemáticas", "Devuelve el menor de dos números", "minimo(, )"),
        ("maximo", "Matemáticas", "Devuelve el mayor de dos números", "maximo(, )"),
        ("seno", "Matemáticas", "Seno trigonométrico en radianes", "seno()"),
        ("coseno", "Matemáticas", "Coseno trigonométrico en radianes", "coseno()"),
        ("tangente", "Matemáticas", "Tangente trigonométrica en radianes", "tangente()"),
        ("logaritmo", "Matemáticas", "Logaritmo natural (base e)", "logaritmo()"),
        ("aleatorio", "Matemáticas", "Número decimal aleatorio [0,1) o entero en rango(min, max)", "aleatorio()"),
        ("pi", "Matemáticas", "Devuelve la constante matemática Pi", "pi()"),
        ("euler", "Matemáticas", "Devuelve la constante matemática e", "euler()"),
        ("leer_archivo", "Archivos", "Lee el contenido UTF-8 de un archivo", "leer_archivo(\"ruta.txt\")"),
        ("escribir_archivo", "Archivos", "Escribe o sobrescribe un archivo UTF-8", "escribir_archivo(\"ruta.txt\", \"\")"),
        ("agregar_linea_archivo", "Archivos", "Agrega una línea al final de un archivo", "agregar_linea_archivo(\"ruta.txt\", \"\")"),
        ("existe_archivo", "Archivos", "Comprueba si un archivo existe en disco", "existe_archivo(\"ruta.txt\")"),
        ("tiempo_actual_ms", "Tiempo", "Milisegundos Unix UTC actuales", "tiempo_actual_ms()"),
        ("fecha_actual", "Tiempo", "Fecha y hora actual formateada", "fecha_actual()"),
        ("esperar_ms", "Tiempo", "Pausa la ejecución los milisegundos indicados", "esperar_ms(100)"),
        ("elegir_plantilla", "Plantilla Cibao 5", "Estructura elegir / caso / defecto", "elegir valor {\n    caso 1:\n        imprimir(\"Uno\")\n    defecto:\n        imprimir(\"Otro\")\n}"),
        ("diccionario_plantilla", "Plantilla Cibao 5", "Diccionario clave-valor", "{ nombre: \"Cibao\", version: 5 }"),
        ("clase_herencia_plantilla", "Plantilla Cibao 5", "Subclase con extiende y super", "clase Subclase extiende Base {\n    constructor(valor) {\n        super(valor)\n    }\n}")
    ];

    public ServicioAutocompletado(GestorAddons gestorAddons)
    {
        _gestorAddons = gestorAddons;
    }

    public IReadOnlyList<ICompletionData> ObtenerSugerencias(string rutaArchivo, string prefijo)
    {
        var resultados = new List<ICompletionData>();
        string ext = Path.GetExtension(rutaArchivo).ToLowerInvariant();

        if (ext is ".cibao" or ".alb")
        {
            foreach (string kw in PalabrasReservadas.ObtenerTodas().OrderBy(k => k))
            {
                if (string.IsNullOrEmpty(prefijo) || kw.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
                {
                    resultados.Add(new ElementoAutocompletado(kw, "Palabra clave", $"Palabra reservada de Cibao: '{kw}'"));
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
