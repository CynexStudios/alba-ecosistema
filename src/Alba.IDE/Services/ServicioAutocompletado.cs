using System.Text.RegularExpressions;
using Alba.Engine.Lexer;
using Alba.IDE.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit.CodeCompletion;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace Alba.IDE.Services;

public sealed class ElementoAutocompletado : ICompletionData
{
    public IImage? Image => null;
    public string Text { get; }
    public object Content { get; }
    public object Description { get; }
    public double Priority { get; }
    public string Categoria { get; }
    private readonly string _textoInsertar;

    public ElementoAutocompletado(
        string text,
        string categoria,
        string descripcion,
        string? textoInsertar = null,
        string? insignia = null,
        string? colorInsigniaHex = null,
        double prioridad = 0)
    {
        Text = text;
        Categoria = categoria;
        Description = descripcion;
        Priority = prioridad;
        _textoInsertar = textoInsertar ?? text;
        Content = ConstruirVistaItem(
            text,
            categoria,
            insignia ?? InferirInsignia(categoria),
            colorInsigniaHex ?? InferirColorHex(categoria));
    }

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, _textoInsertar);
    }

    private static Control ConstruirVistaItem(string texto, string categoria, string insignia, string colorHex)
    {
        IBrush acento = Brush.Parse(colorHex);
        IBrush fgClaro = Brush.Parse("#F3F4F6");
        IBrush fgSec = Brush.Parse("#9CA3AF");

        var badge = new Border
        {
            Background = new SolidColorBrush(Color.Parse(colorHex), 0.18),
            BorderBrush = acento,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = insignia,
                FontSize = 10,
                FontWeight = FontWeight.Bold,
                FontFamily = new FontFamily("JetBrains Mono, Consolas, monospace"),
                Foreground = acento
            }
        };

        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(2, 2)
        };
        panel.Children.Add(badge);
        panel.Children.Add(new TextBlock
        {
            Text = texto,
            FontSize = 12.5,
            FontWeight = FontWeight.SemiBold,
            FontFamily = new FontFamily("JetBrains Mono, Consolas, monospace"),
            Foreground = fgClaro,
            VerticalAlignment = VerticalAlignment.Center
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"· {categoria}",
            FontSize = 11,
            Foreground = fgSec,
            VerticalAlignment = VerticalAlignment.Center
        });
        return panel;
    }

    private static string InferirInsignia(string categoria)
    {
        string c = categoria.ToLowerInvariant();
        if (c.Contains("variable") || c.Contains("const")) return "var";
        if (c.Contains("clase") || c.Contains("tipo") || c.Contains("interfaz")) return "cla";
        if (c.Contains("palabra")) return "kw";
        if (c.Contains("plantilla") || c.Contains("snippet")) return "snp";
        if (c.Contains("html") || c.Contains("etiqueta")) return "tag";
        if (c.Contains("css")) return "css";
        if (c.Contains("método") || c.Contains("metodo")) return "mtd";
        return "fn";
    }

    private static string InferirColorHex(string categoria)
    {
        string c = categoria.ToLowerInvariant();
        if (c.Contains("variable") || c.Contains("const")) return "#38BDF8";
        if (c.Contains("clase") || c.Contains("tipo") || c.Contains("interfaz")) return "#2DD4BF";
        if (c.Contains("palabra")) return "#C084FC";
        if (c.Contains("plantilla") || c.Contains("snippet")) return "#FB923C";
        if (c.Contains("html") || c.Contains("css")) return "#4ADE80";
        return "#FACC15";
    }
}

/// <summary>
/// Motor IntelliSense contextual de alta visibilidad para Cibao Code 9.0, Python, HTML, CSS,
/// JavaScript, TypeScript, C#, C++, símbolos vivos del archivo actual y snippets de complementos.
/// </summary>
public sealed partial class ServicioAutocompletado
{
    private readonly GestorAddons _gestorAddons;

    private static readonly (string Nombre, string Categoria, string Descripcion, string? Insercion)[] FuncionesCibao =
    [
        ("mostrar", "E/S Cibao 9.0", "Muestra valores en consola con formato legible", "mostrar(\"\")"),
        ("imprimir", "E/S", "Imprime valores en la consola", "imprimir()"),
        ("leer", "E/S", "Lee una línea ingresada por el usuario", "leer(\"\")"),
        ("rango", "Colecciones", "Genera una lista numérica secuencial", "rango(0, 10)"),
        ("rango_paso", "Colecciones 9.0", "Genera un rango numérico con paso personalizado", "rango_paso(0, 10, 2)"),
        ("longitud", "Colecciones", "Devuelve el tamaño de lista, diccionario o texto", "longitud()"),
        ("tipo_de", "Tipos", "Devuelve el nombre del tipo del valor", "tipo_de()"),
        ("es_nulo", "Tipos 9.0", "Devuelve verdadero si el valor es nulo", "es_nulo()"),
        ("es_numero", "Tipos 9.0", "Comprueba si el valor es entero o decimal", "es_numero()"),
        ("convertir_entero", "Conversión", "Convierte texto/decimal a entero", "convertir_entero()"),
        ("convertir_decimal", "Conversión", "Convierte texto/entero a decimal", "convertir_decimal()"),
        ("convertir_texto", "Conversión", "Convierte cualquier valor a texto", "convertir_texto()"),
        ("agregar", "Listas", "Agrega un elemento al final de la lista", "agregar(, )"),
        ("eliminar", "Listas", "Elimina un elemento por índice", "eliminar(, )"),
        ("mapear", "Funcional", "Transforma elementos con una función o lambda", "mapear(lista, x => x)"),
        ("filtrar", "Funcional", "Filtra elementos con predicado o lambda", "filtrar(lista, x => x > 0)"),
        ("reducir", "Funcional", "Acumula elementos en un valor final", "reducir(lista, 0, (acc, x) => acc + x)"),
        ("encontrar", "Funcional 9.0", "Devuelve el primer elemento que cumple el predicado", "encontrar(lista, x => x > 0)"),
        ("alguno", "Funcional 9.0", "Verdadero si al menos un elemento cumple", "alguno(lista, x => x > 0)"),
        ("todos", "Funcional 9.0", "Verdadero si todos los elementos cumplen", "todos(lista, x => x > 0)"),
        ("unicos", "Listas 9.0", "Elimina duplicados preservando orden", "unicos()"),
        ("aplanar", "Listas 9.0", "Aplana un nivel de listas anidadas", "aplanar()"),
        ("enumerar", "Listas 9.0", "Convierte lista en pares { indice, valor }", "enumerar()"),
        ("ordenar", "Listas", "Devuelve copia ordenada", "ordenar()"),
        ("invertir", "Listas", "Invierte el orden de una lista o texto", "invertir()"),
        ("claves", "Diccionarios", "Lista de claves del diccionario", "claves()"),
        ("valores", "Diccionarios", "Lista de valores del diccionario", "valores()"),
        ("contiene_clave", "Diccionarios", "Verifica si existe la clave", "contiene_clave(, \"\")"),
        ("json_a_texto", "JSON", "Serializa un valor Cibao a texto JSON", "json_a_texto()"),
        ("texto_a_json", "JSON", "Convierte JSON válido a diccionario/lista", "texto_a_json()"),
        ("sha256", "Seguridad 9.0", "Calcula hash SHA-256 criptográfico", "sha256(\"\")"),
        ("uuid", "Seguridad 9.0", "Genera un identificador UUID v4 único", "uuid()"),
        ("mayusculas", "Texto", "Convierte texto a MAYÚSCULAS", "mayusculas()"),
        ("minusculas", "Texto", "Convierte texto a minúsculas", "minusculas()"),
        ("recortar", "Texto", "Elimina espacios iniciales y finales", "recortar()"),
        ("dividir", "Texto", "Divide un texto en lista por separador", "dividir(, \",\")"),
        ("unir", "Texto", "Une una lista de textos con separador", "unir(, \", \")"),
        ("reemplazar", "Texto", "Reemplaza subcadenas", "reemplazar(, \"\", \"\")"),
        ("formatear", "Texto 9.0", "Reemplaza marcadores {0}, {1} en plantilla", "formatear(\"Hola {0}\", )"),
        ("raiz", "Matemáticas", "Raíz cuadrada", "raiz()"),
        ("potencia", "Matemáticas 9.0", "Eleva base a exponente", "potencia(, 2)"),
        ("abs", "Matemáticas", "Valor absoluto", "abs()"),
        ("redondear", "Matemáticas", "Redondea número", "redondear()"),
        ("aleatorio", "Matemáticas", "Aleatorio [0,1) o entero en rango", "aleatorio()"),
        ("leer_archivo", "Archivos", "Lee archivo UTF-8 completo", "leer_archivo(\"datos.txt\")"),
        ("escribir_archivo", "Archivos", "Escribe archivo UTF-8", "escribir_archivo(\"datos.txt\", \"\")"),
        ("existe_archivo", "Archivos", "Comprueba si existe archivo en disco", "existe_archivo(\"datos.txt\")"),
        ("fecha_actual", "Tiempo", "Fecha y hora actual", "fecha_actual()"),
        ("esperar_ms", "Tiempo", "Pausa en milisegundos", "esperar_ms(100)"),
        ("para_rango", "Plantilla Cibao 9.0", "Ciclo para con rango inclusivo 1..=10", "para i en 1..=10 {\n    mostrar($\"Paso {i}\")\n}"),
        ("elegir_caso", "Plantilla Cibao 9.0", "Estructura elegir / caso / defecto", "elegir valor {\n    caso 1 { mostrar(\"Uno\") }\n    defecto { mostrar(\"Otro\") }\n}"),
        ("intentar_capturar", "Plantilla Cibao 9.0", "Manejo seguro de errores", "intentar {\n    \n} capturar (err) {\n    mostrar($\"Error: {err}\")\n}"),
        ("clase_cibao9", "Plantilla Cibao 9.0", "Clase orientada a objetos con constructor", "clase Entidad {\n    publico nombre: texto\n    constructor(nombre: texto) {\n        este.nombre = nombre\n    }\n    publico funcion saludar(): texto {\n        retornar $\"Hola {este.nombre}\"\n    }\n}")
    ];

    private static readonly (string Nombre, string Categoria, string Descripcion, string Insercion)[] MetodosPuntoCibao =
    [
        ("agregar", "Método Lista", "Agrega un elemento al final", "agregar()"),
        ("filtrar", "Método Lista", "Filtra elementos con lambda", "filtrar(x => x > 0)"),
        ("mapear", "Método Lista", "Transforma elementos con lambda", "mapear(x => x)"),
        ("encontrar", "Método Lista 9.0", "Busca el primer elemento que coincide", "encontrar(x => )"),
        ("alguno", "Método Lista 9.0", "Comprueba si alguno cumple", "alguno(x => )"),
        ("todos", "Método Lista 9.0", "Comprueba si todos cumplen", "todos(x => )"),
        ("unicos", "Método Lista 9.0", "Devuelve lista sin repetidos", "unicos()"),
        ("aplanar", "Método Lista 9.0", "Aplana listas anidadas", "aplanar()"),
        ("ordenar", "Método Lista", "Ordena los elementos", "ordenar()"),
        ("invertir", "Método Lista/Texto", "Invierte el orden", "invertir()"),
        ("longitud", "Método Colección", "Cantidad de elementos o caracteres", "longitud()"),
        ("mayusculas", "Método Texto", "Texto en mayúsculas", "mayusculas()"),
        ("minusculas", "Método Texto", "Texto en minúsculas", "minusculas()"),
        ("recortar", "Método Texto", "Elimina espacios en los extremos", "recortar()"),
        ("dividir", "Método Texto", "Divide por delimitador", "dividir(\",\")"),
        ("reemplazar", "Método Texto", "Reemplaza subtexto", "reemplazar(\"a\", \"b\")"),
        ("contiene", "Método Colección/Texto", "Verifica existencia", "contiene()")
    ];

    private static readonly (string Nombre, string Categoria, string Descripcion, string Insercion)[] PythonItems =
    [
        ("def", "Palabra clave Python", "Define una función", "def nombre(param):\n    pass"),
        ("class", "Palabra clave Python", "Define una clase", "class MiClase:\n    def __init__(self):\n        pass"),
        ("if", "Palabra clave Python", "Condicional if", "if condicion:\n    "),
        ("elif", "Palabra clave Python", "Condicional elif", "elif condicion:\n    "),
        ("else", "Palabra clave Python", "Rama alternativa", "else:\n    "),
        ("for", "Palabra clave Python", "Bucle for in", "for item in coleccion:\n    "),
        ("while", "Palabra clave Python", "Bucle while", "while condicion:\n    "),
        ("try", "Palabra clave Python", "Bloque try / except", "try:\n    \nexcept Exception as e:\n    print(e)"),
        ("import", "Palabra clave Python", "Importar módulo", "import "),
        ("from", "Palabra clave Python", "Importación selectiva", "from modulo import "),
        ("return", "Palabra clave Python", "Retornar valor", "return "),
        ("async def", "Palabra clave Python", "Función asíncrona", "async def tarea():\n    "),
        ("print", "Función Python", "Imprime en salida estándar", "print()"),
        ("len", "Función Python", "Longitud de colección o cadena", "len()"),
        ("range", "Función Python", "Secuencia numérica", "range(0, 10)"),
        ("enumerate", "Función Python", "Itera con índice y valor", "enumerate()"),
        ("isinstance", "Función Python", "Comprueba tipo de objeto", "isinstance(obj, str)"),
        ("input", "Función Python", "Lee entrada desde consola", "input(\"\")"),
        ("open", "Función Python", "Abre archivo", "with open(\"archivo.txt\", \"r\", encoding=\"utf-8\") as f:\n    datos = f.read()")
    ];

    private static readonly (string Nombre, string Categoria, string Descripcion, string Insercion)[] JsTsItems =
    [
        ("const", "Palabra clave JS/TS", "Constante inmutable por referencia", "const "),
        ("let", "Palabra clave JS/TS", "Variable de bloque", "let "),
        ("function", "Palabra clave JS/TS", "Declara una función", "function nombre() {\n  \n}"),
        ("async", "Palabra clave JS/TS", "Función asíncrona", "async function () {\n  \n}"),
        ("await", "Palabra clave JS/TS", "Espera una promesa", "await "),
        ("interface", "Tipo TypeScript", "Define contrato de objeto TS", "interface Modelo {\n  id: number;\n  nombre: string;\n}"),
        ("type", "Tipo TypeScript", "Alias de tipo TS", "type Resultado = "),
        ("class", "Clase JS/TS", "Clase ES6/TS", "class Servicio {\n  constructor() {}\n}"),
        ("import", "Módulo JS/TS", "Importar módulo ES", "import { } from \"./modulo\";"),
        ("export", "Módulo JS/TS", "Exportar símbolo", "export "),
        ("console.log", "Runtime JS/TS", "Imprime en consola", "console.log();"),
        ("document.querySelector", "DOM Web", "Selecciona el primer elemento DOM", "document.querySelector(\"#id\")"),
        ("addEventListener", "DOM Web", "Suscribe evento en elemento DOM", "addEventListener(\"click\", () => {\n  \n})"),
        ("fetch", "API Web JS/TS", "Petición HTTP Fetch", "const resp = await fetch(\"https://\");\nconst data = await resp.json();"),
        ("Promise", "Runtime JS/TS", "Promesa asíncrona", "new Promise((resolve, reject) => {\n  \n})"),
        ("try/catch", "Plantilla JS/TS", "Captura de errores", "try {\n  \n} catch (error) {\n  console.error(error);\n}")
    ];

    private static readonly (string Nombre, string Categoria, string Descripcion, string Insercion)[] HtmlItems =
    [
        ("<!DOCTYPE html>", "Plantilla HTML5", "Documento HTML5 completo", "<!DOCTYPE html>\n<html lang=\"es\">\n<head>\n  <meta charset=\"UTF-8\" />\n  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />\n  <title>Mi Página</title>\n</head>\n<body>\n  <h1>Hola Xynex</h1>\n</body>\n</html>"),
        ("div", "Etiqueta HTML", "Contenedor genérico de bloque", "<div>\n  \n</div>"),
        ("section", "Etiqueta HTML", "Sección semántica", "<section>\n  <h2>Sección</h2>\n</section>"),
        ("header", "Etiqueta HTML", "Cabecera superior", "<header>\n  \n</header>"),
        ("nav", "Etiqueta HTML", "Barra de navegación", "<nav>\n  <a href=\"#\">Inicio</a>\n</nav>"),
        ("main", "Etiqueta HTML", "Contenido principal", "<main>\n  \n</main>"),
        ("h1", "Etiqueta HTML", "Encabezado principal", "<h1>Título</h1>"),
        ("h2", "Etiqueta HTML", "Subtítulo", "<h2>Subtítulo</h2>"),
        ("p", "Etiqueta HTML", "Párrafo de texto", "<p></p>"),
        ("button", "Etiqueta HTML", "Botón interactivo", "<button type=\"button\">Aceptar</button>"),
        ("a", "Etiqueta HTML", "Enlace hipervínculo", "<a href=\"https://\">Enlace</a>"),
        ("img", "Etiqueta HTML", "Imagen con ruta y alt", "<img src=\"imagen.png\" alt=\"Descripción\" />"),
        ("input", "Etiqueta HTML", "Campo de entrada", "<input type=\"text\" placeholder=\"Escribe aquí...\" />"),
        ("form", "Etiqueta HTML", "Formulario interactivo", "<form>\n  <label>Nombre</label>\n  <input type=\"text\" />\n  <button type=\"submit\">Enviar</button>\n</form>"),
        ("table", "Etiqueta HTML", "Tabla estructurada", "<table>\n  <tr><th>Columna</th><th>Valor</th></tr>\n  <tr><td>Dato</td><td>100</td></tr>\n</table>"),
        ("style", "Etiqueta HTML", "Estilos CSS embebidos", "<style>\n  body { background: #1e1e1e; color: #f3f4f6; }\n</style>"),
        ("script", "Etiqueta HTML", "Código JavaScript embebido", "<script>\n  console.log(\"Listo\");\n</script>")
    ];

    private static readonly (string Nombre, string Categoria, string Descripcion, string Insercion)[] CssItems =
    [
        ("display", "Propiedad CSS", "Modelo de caja (flex, grid, block)", "display: flex;"),
        ("grid-template-columns", "Propiedad CSS", "Columnas de CSS Grid", "grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));"),
        ("justify-content", "Propiedad CSS", "Alineación horizontal en flex/grid", "justify-content: center;"),
        ("align-items", "Propiedad CSS", "Alineación vertical en flex/grid", "align-items: center;"),
        ("background", "Propiedad CSS", "Color o degradado de fondo", "background: #1E1E1E;"),
        ("color", "Propiedad CSS", "Color del texto", "color: #E2E8F0;"),
        ("border-radius", "Propiedad CSS", "Esquinas redondeadas", "border-radius: 8px;"),
        ("padding", "Propiedad CSS", "Relleno interno", "padding: 16px;"),
        ("margin", "Propiedad CSS", "Margen externo", "margin: 0 auto;"),
        ("font-family", "Propiedad CSS", "Fuente tipográfica", "font-family: system-ui, -apple-system, sans-serif;"),
        ("box-shadow", "Propiedad CSS", "Sombra de elevación", "box-shadow: 0 8px 24px rgba(0, 0, 0, 0.35);")
    ];

    private static readonly (string Nombre, string Categoria, string Descripcion, string Insercion)[] CSharpCppItems =
    [
        ("Console.WriteLine", "API C#", "Imprime línea en consola .NET", "Console.WriteLine();"),
        ("var", "Palabra clave C#", "Inferencia de tipo", "var "),
        ("public", "Modificador C#/C++", "Acceso público", "public "),
        ("private", "Modificador C#/C++", "Acceso privado", "private "),
        ("async Task", "Patrón C#", "Método asíncrono .NET", "public async Task EjecutarAsync()\n{\n    await Task.CompletedTask;\n}"),
        ("class", "Clase C#/C++", "Declara una clase", "class Entidad\n{\n}"),
        ("std::cout", "Biblioteca C++20", "Salida estándar C++", "std::cout << \"\" << '\\n';"),
        ("std::vector", "Contenedor C++20", "Arreglo dinámico STL", "std::vector<int> "),
        ("std::string", "Tipo C++20", "Cadena de texto STL", "std::string "),
        ("#include <iostream>", "Directiva C++", "Incluye E/S estándar C++", "#include <iostream>")
    ];

    public ServicioAutocompletado(GestorAddons gestorAddons)
    {
        _gestorAddons = gestorAddons;
    }

    public IReadOnlyList<ICompletionData> ObtenerSugerencias(
        string rutaArchivo,
        string prefijo,
        string? codigoDocumento = null,
        char? caracterDisparador = null)
    {
        var resultados = new List<ICompletionData>();
        var nombresAgregados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string ext = Path.GetExtension(rutaArchivo).ToLowerInvariant();
        string textoDoc = codigoDocumento ?? string.Empty;

        void Agregar(string nombre, string cat, string desc, string? ins = null, string? insignia = null, double prioridad = 0)
        {
            if (!string.IsNullOrEmpty(prefijo) &&
                !nombre.Contains(prefijo, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            if (!nombresAgregados.Add($"{nombre}|{cat}")) return;
            resultados.Add(new ElementoAutocompletado(nombre, cat, desc, ins, insignia, prioridad: prioridad));
        }

        // 0. Símbolos vivos declarados en el propio documento (alta prioridad)
        if (caracterDisparador != '.' && caracterDisparador != '<' && !string.IsNullOrWhiteSpace(textoDoc))
        {
            foreach (var sim in ExtraerSimbolosDocumento(textoDoc, ext))
            {
                double prio = !string.IsNullOrEmpty(prefijo) && sim.Nombre.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase) ? 10 : 5;
                Agregar(sim.Nombre, sim.Categoria, sim.Descripcion, sim.Insercion, sim.Insignia, prio);
            }
        }

        // 1. Cibao Code 9.0
        if (ext is ".cibao" or ".alb" or "")
        {
            if (caracterDisparador == '.')
            {
                foreach (var m in MetodosPuntoCibao)
                {
                    Agregar(m.Nombre, m.Categoria, m.Descripcion, m.Insercion, "mtd", 12);
                }
                return resultados;
            }

            foreach (var fn in FuncionesCibao)
            {
                double p = !string.IsNullOrEmpty(prefijo) && fn.Nombre.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase) ? 8 : 2;
                Agregar(fn.Nombre, fn.Categoria, fn.Descripcion, fn.Insercion, prioridad: p);
            }

            foreach (string kw in PalabrasReservadas.ObtenerTodas().OrderBy(k => k))
            {
                Agregar(kw, "Palabra clave", $"Palabra reservada de Cibao 9.0: '{kw}'", kw, "kw", 1);
            }
        }
        else if (ext == ".py")
        {
            foreach (var item in PythonItems)
            {
                Agregar(item.Nombre, item.Categoria, item.Descripcion, item.Insercion);
            }
        }
        else if (ext is ".js" or ".mjs" or ".ts" or ".tsx")
        {
            foreach (var item in JsTsItems)
            {
                Agregar(item.Nombre, item.Categoria, item.Descripcion, item.Insercion);
            }
        }
        else if (ext is ".html" or ".htm")
        {
            foreach (var item in HtmlItems)
            {
                string ins = caracterDisparador == '<' && item.Insercion.StartsWith('<')
                    ? item.Insercion[1..]
                    : item.Insercion;
                Agregar(item.Nombre, item.Categoria, item.Descripcion, ins, "tag");
            }
        }
        else if (ext == ".css")
        {
            foreach (var item in CssItems)
            {
                Agregar(item.Nombre, item.Categoria, item.Descripcion, item.Insercion, "css");
            }
        }
        else if (ext is ".cs" or ".cpp" or ".cc" or ".cxx" or ".h" or ".hpp")
        {
            foreach (var item in CSharpCppItems)
            {
                Agregar(item.Nombre, item.Categoria, item.Descripcion, item.Insercion);
            }
        }

        // 2. Snippets de addons en línea / instalados activos
        foreach (SnippetAddon snippet in _gestorAddons.ObtenerSnippetsActivosPorExtension(ext))
        {
            Agregar(snippet.Prefijo, $"Addon ({snippet.Titulo})", snippet.Descripcion, snippet.Cuerpo, "snp", 4);
        }

        return resultados
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.Text.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(r => r.Text, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<(string Nombre, string Categoria, string Descripcion, string Insercion, string Insignia)> ExtraerSimbolosDocumento(
        string codigo,
        string extension)
    {
        var lista = new List<(string, string, string, string, string)>();
        var vistos = new HashSet<string>(StringComparer.Ordinal);

        void AddSym(string n, string cat, string desc, string ins, string badge)
        {
            if (n.Length < 2 || !vistos.Add(n)) return;
            lista.Add((n, cat, desc, ins, badge));
        }

        if (extension is ".cibao" or ".alb" or "")
        {
            foreach (Match m in RegexSimboloCibao().Matches(codigo))
            {
                string tipo = m.Groups[1].Value.ToLowerInvariant();
                string nombre = m.Groups[2].Value;
                if (tipo == "funcion")
                    AddSym(nombre, "Función local", "Función declarada en este archivo", $"{nombre}()", "fn");
                else if (tipo == "clase")
                    AddSym(nombre, "Clase local", "Clase declarada en este archivo", nombre, "cla");
                else
                    AddSym(nombre, tipo == "constante" ? "Constante local" : "Variable local", $"Identificador '{nombre}' de este archivo", nombre, "var");
            }
        }
        else if (extension == ".py")
        {
            foreach (Match m in RegexSimboloPython().Matches(codigo))
            {
                string kw = m.Groups[1].Value;
                string nombre = m.Groups[2].Value;
                if (kw == "def")
                    AddSym(nombre, "Función local", "Función def en este archivo", $"{nombre}()", "fn");
                else if (kw == "class")
                    AddSym(nombre, "Clase local", "Clase Python local", nombre, "cla");
                else
                    AddSym(nombre, "Variable local", "Variable Python local", nombre, "var");
            }
        }
        else
        {
            foreach (Match m in RegexSimboloGenerico().Matches(codigo))
            {
                string kw = m.Groups[1].Value.ToLowerInvariant();
                string nombre = m.Groups[2].Value;
                if (kw is "function" or "fn")
                    AddSym(nombre, "Función local", "Función local", $"{nombre}()", "fn");
                else if (kw is "class" or "interface" or "struct")
                    AddSym(nombre, "Clase/Tipo local", "Estructura local", nombre, "cla");
                else
                    AddSym(nombre, "Variable local", "Variable del archivo", nombre, "var");
            }
        }

        return lista;
    }

    [GeneratedRegex(@"\b(variable|constante|funcion|clase)\s+([a-zA-Z_áéíóúñÁÉÍÓÚÑ][a-zA-Z0-9_áéíóúñÁÉÍÓÚÑ]*)", RegexOptions.Compiled)]
    private static partial Regex RegexSimboloCibao();

    [GeneratedRegex(@"^\s*(def|class)\s+([a-zA-Z_][a-zA-Z0-9_]*)|^\s*([a-zA-Z_][a-zA-Z0-9_]*)\s*=", RegexOptions.Multiline | RegexOptions.Compiled)]
    private static partial Regex RegexSimboloPython();

    [GeneratedRegex(@"\b(let|const|var|function|class|interface|struct)\s+([a-zA-Z_][a-zA-Z0-9_]*)", RegexOptions.Compiled)]
    private static partial Regex RegexSimboloGenerico();
}
