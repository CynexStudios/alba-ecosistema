using System.Text.Json;
using System.Text.RegularExpressions;
using Alba.Engine.Diagnostics;
using Alba.Engine.Parser;
using Alba.IDE.Models;

namespace Alba.IDE.Services;

/// <summary>
/// Detector nativo de errores en tiempo real para Cibao (.cibao/.alb), Python (.py),
/// HTML (.html/.htm), CSS (.css), JavaScript/TypeScript (.js/.ts), JSON (.json) y C#/C++.
/// Funciona de forma 100% nativa e instantánea sin requerir procesos externos para mostrar problemas.
/// </summary>
public sealed partial class ServicioDiagnosticoEnVivo
{
    private static readonly HashSet<string> EtiquetasHtmlVacias = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input",
        "link", "meta", "param", "source", "track", "wbr", "!doctype"
    };

    public static bool EsArchivoCibao(string? rutaArchivo)
    {
        if (string.IsNullOrWhiteSpace(rutaArchivo)) return false;
        string ext = Path.GetExtension(rutaArchivo);
        return ext.Equals(".cibao", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".alb", StringComparison.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ProblemaCodigo> AnalizarDocumento(string rutaArchivo, string codigoFuente)
    {
        string codigo = codigoFuente ?? string.Empty;
        string ext = Path.GetExtension(rutaArchivo ?? string.Empty).ToLowerInvariant();
        string nombre = Path.GetFileName(string.IsNullOrWhiteSpace(rutaArchivo) ? "archivo.cibao" : rutaArchivo);

        return ext switch
        {
            ".cibao" or ".alb" => AnalizarCibao(rutaArchivo ?? nombre, nombre, codigo),
            ".json" => AnalizarJson(rutaArchivo ?? nombre, nombre, codigo),
            ".html" or ".htm" => AnalizarHtml(rutaArchivo ?? nombre, nombre, codigo),
            ".css" => AnalizarCss(rutaArchivo ?? nombre, nombre, codigo),
            ".py" => AnalizarPython(rutaArchivo ?? nombre, nombre, codigo),
            ".js" or ".mjs" or ".ts" => AnalizarLlavesYCadenas(rutaArchivo ?? nombre, nombre, codigo, ext == ".ts" ? "TS" : "JS"),
            ".cs" or ".cpp" or ".cc" or ".h" or ".hpp" => AnalizarLlavesYCadenas(rutaArchivo ?? nombre, nombre, codigo, ext == ".cs" ? "CS" : "CPP"),
            _ => []
        };
    }

    private static IReadOnlyList<ProblemaCodigo> AnalizarCibao(string ruta, string nombre, string codigo)
    {
        var (lexico, sintactico) = AnalizadorSintactico.AnalizarCodigo(codigo, nombre);
        var problemas = new List<ProblemaCodigo>();

        foreach (ErrorAlba err in lexico.Errores)
        {
            problemas.Add(Convertir(ruta, nombre, err));
        }
        foreach (ErrorAlba err in sintactico.Errores)
        {
            problemas.Add(Convertir(ruta, nombre, err));
        }

        return problemas;
    }

    private static IReadOnlyList<ProblemaCodigo> AnalizarJson(string ruta, string nombre, string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return [];
        }

        try
        {
            using JsonDocument _ = JsonDocument.Parse(codigo, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false
            });
            return [];
        }
        catch (JsonException ex)
        {
            return
            [
                CrearProblema(
                    ruta,
                    nombre,
                    "JSON-001",
                    "Error",
                    Math.Max(1, (int)(ex.LineNumber ?? 0) + 1),
                    Math.Max(1, (int)(ex.BytePositionInLine ?? 0) + 1),
                    $"Error de sintaxis JSON: {ex.Message}")
            ];
        }
    }

    private static IReadOnlyList<ProblemaCodigo> AnalizarHtml(string ruta, string nombre, string codigo)
    {
        var problemas = new List<ProblemaCodigo>();
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return problemas;
        }

        if (!codigo.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) &&
            codigo.Contains("<html", StringComparison.OrdinalIgnoreCase))
        {
            problemas.Add(CrearProblema(ruta, nombre, "HTML-W01", "Advertencia", 1, 1,
                "Se recomienda iniciar el documento con <!DOCTYPE html>."));
        }

        var pila = new Stack<(string Tag, int Linea, int Col)>();
        var idsVistos = new HashSet<string>(StringComparer.Ordinal);
        string[] lineas = codigo.Split('\n');

        bool enScriptOStyle = false;
        string etiquetaIgnorada = string.Empty;

        for (int i = 0; i < lineas.Length; i++)
        {
            string linea = lineas[i];
            int numLinea = i + 1;

            foreach (Match mId in RegexIdHtml().Matches(linea))
            {
                string id = mId.Groups[1].Value;
                if (!idsVistos.Add(id))
                {
                    problemas.Add(CrearProblema(ruta, nombre, "HTML-W02", "Advertencia", numLinea, mId.Index + 1,
                        $"Atributo id='{id}' duplicado en el documento HTML."));
                }
            }

            foreach (Match m in RegexEtiquetaHtml().Matches(linea))
            {
                string raw = m.Value;
                if (raw.StartsWith("<!--", StringComparison.Ordinal) || raw.StartsWith("<!", StringComparison.Ordinal))
                    continue;

                bool esCierre = m.Groups[1].Value == "/";
                string tag = m.Groups[2].Value.ToLowerInvariant();
                bool autoCierre = raw.EndsWith("/>", StringComparison.Ordinal);

                if (enScriptOStyle)
                {
                    if (esCierre && tag == etiquetaIgnorada)
                    {
                        enScriptOStyle = false;
                        etiquetaIgnorada = string.Empty;
                        if (pila.Count > 0 && pila.Peek().Tag == tag) pila.Pop();
                    }
                    continue;
                }

                if (EtiquetasHtmlVacias.Contains(tag) || autoCierre)
                    continue;

                if (!esCierre)
                {
                    pila.Push((tag, numLinea, m.Index + 1));
                    if (tag is "script" or "style")
                    {
                        enScriptOStyle = true;
                        etiquetaIgnorada = tag;
                    }
                }
                else
                {
                    if (pila.Count == 0)
                    {
                        problemas.Add(CrearProblema(ruta, nombre, "HTML-E01", "Error", numLinea, m.Index + 1,
                            $"Etiqueta de cierre </{tag}> sin etiqueta de apertura correspondiente."));
                    }
                    else if (pila.Peek().Tag == tag)
                    {
                        pila.Pop();
                    }
                    else
                    {
                        var esperada = pila.Peek();
                        problemas.Add(CrearProblema(ruta, nombre, "HTML-E02", "Error", numLinea, m.Index + 1,
                            $"Se encontró </{tag}> pero la etiqueta abierta más reciente es <{esperada.Tag}> (línea {esperada.Linea})."));
                        if (pila.Any(x => x.Tag == tag))
                        {
                            while (pila.Count > 0 && pila.Pop().Tag != tag) { }
                        }
                    }
                }
            }
        }

        while (pila.Count > 0)
        {
            var sinCerrar = pila.Pop();
            problemas.Add(CrearProblema(ruta, nombre, "HTML-E03", "Error", sinCerrar.Linea, sinCerrar.Col,
                $"La etiqueta <{sinCerrar.Tag}> quedó abierta sin cierre </{sinCerrar.Tag}>."));
        }

        return problemas;
    }

    private static IReadOnlyList<ProblemaCodigo> AnalizarCss(string ruta, string nombre, string codigo)
    {
        var problemas = AnalizarLlavesYCadenas(ruta, nombre, codigo, "CSS").ToList();
        string[] lineas = codigo.Split('\n');
        int nivel = 0;

        for (int i = 0; i < lineas.Length; i++)
        {
            string l = lineas[i].Trim();
            if (string.IsNullOrWhiteSpace(l) || l.StartsWith("/*", StringComparison.Ordinal)) continue;

            if (nivel > 0 && l.Contains(':') && !l.EndsWith(';') && !l.EndsWith('{') && !l.EndsWith('}') && !l.StartsWith('@'))
            {
                problemas.Add(CrearProblema(ruta, nombre, "CSS-W01", "Advertencia", i + 1, Math.Max(1, l.Length),
                    "Posible falta de punto y coma (;) al final de la propiedad CSS."));
            }

            foreach (char c in l)
            {
                if (c == '{') nivel++;
                else if (c == '}' && nivel > 0) nivel--;
            }
        }

        return problemas;
    }

    private static IReadOnlyList<ProblemaCodigo> AnalizarPython(string ruta, string nombre, string codigo)
    {
        var problemas = new List<ProblemaCodigo>();
        string[] lineas = codigo.Split('\n');
        var pila = new Stack<(char Simbolo, int Linea, int Col)>();

        bool usaTabs = false;
        bool usaEspacios = false;

        for (int i = 0; i < lineas.Length; i++)
        {
            string lineaRaw = lineas[i].TrimEnd('\r');
            int numLinea = i + 1;

            if (lineaRaw.StartsWith('\t')) usaTabs = true;
            else if (lineaRaw.StartsWith("  ", StringComparison.Ordinal)) usaEspacios = true;

            if (usaTabs && usaEspacios && !string.IsNullOrWhiteSpace(lineaRaw))
            {
                problemas.Add(CrearProblema(ruta, nombre, "PY-W01", "Advertencia", numLinea, 1,
                    "Mezcla de tabulaciones y espacios en la indentación Python (PEP 8 recomienda 4 espacios)."));
                usaTabs = false; // Evita repetir en todas las líneas
            }

            bool enComillaSimple = false;
            bool enComillaDoble = false;
            bool escape = false;

            for (int c = 0; c < lineaRaw.Length; c++)
            {
                char ch = lineaRaw[c];
                if (escape)
                {
                    escape = false;
                    continue;
                }
                if ((enComillaSimple || enComillaDoble) && ch == '\\')
                {
                    escape = true;
                    continue;
                }
                if (!enComillaDoble && ch == '\'')
                {
                    enComillaSimple = !enComillaSimple;
                    continue;
                }
                if (!enComillaSimple && ch == '"')
                {
                    enComillaDoble = !enComillaDoble;
                    continue;
                }
                if (enComillaSimple || enComillaDoble) continue;
                if (ch == '#') break;

                if (ch is '(' or '[' or '{')
                {
                    pila.Push((ch, numLinea, c + 1));
                }
                else if (ch is ')' or ']' or '}')
                {
                    char esperado = ch switch { ')' => '(', ']' => '[', _ => '{' };
                    if (pila.Count == 0 || pila.Peek().Simbolo != esperado)
                    {
                        problemas.Add(CrearProblema(ruta, nombre, "PY-E01", "Error", numLinea, c + 1,
                            $"Delimitador '{ch}' inesperado o sin apertura correspondiente."));
                    }
                    else
                    {
                        pila.Pop();
                    }
                }
            }

            string trimSinComentario = RemoverComentarioPython(lineaRaw).TrimEnd();
            if (pila.Count == 0 && RegexBloquePython().IsMatch(trimSinComentario) && !trimSinComentario.EndsWith(':'))
            {
                problemas.Add(CrearProblema(ruta, nombre, "PY-E02", "Error", numLinea, Math.Max(1, trimSinComentario.Length),
                    "Falta ':' al final de la declaración de bloque en Python."));
            }
        }

        while (pila.Count > 0)
        {
            var abierto = pila.Pop();
            problemas.Add(CrearProblema(ruta, nombre, "PY-E03", "Error", abierto.Linea, abierto.Col,
                $"Delimitador '{abierto.Simbolo}' sin cerrar."));
        }

        return problemas;
    }

    private static IReadOnlyList<ProblemaCodigo> AnalizarLlavesYCadenas(string ruta, string nombre, string codigo, string prefijo)
    {
        var problemas = new List<ProblemaCodigo>();
        var pila = new Stack<(char Simbolo, int Linea, int Col)>();
        string[] lineas = codigo.Split('\n');
        bool enComentarioBloque = false;
        int lineaComBloque = 1;

        for (int i = 0; i < lineas.Length; i++)
        {
            string l = lineas[i].TrimEnd('\r');
            int numLinea = i + 1;
            bool enDoble = false;
            bool enSimple = false;
            bool enBacktick = false;
            bool escape = false;

            for (int c = 0; c < l.Length; c++)
            {
                char ch = l[c];
                char next = c + 1 < l.Length ? l[c + 1] : '\0';

                if (enComentarioBloque)
                {
                    if (ch == '*' && next == '/')
                    {
                        enComentarioBloque = false;
                        c++;
                    }
                    continue;
                }

                if (escape)
                {
                    escape = false;
                    continue;
                }

                if ((enDoble || enSimple || enBacktick) && ch == '\\')
                {
                    escape = true;
                    continue;
                }

                if (!enSimple && !enBacktick && ch == '"') { enDoble = !enDoble; continue; }
                if (!enDoble && !enBacktick && ch == '\'') { enSimple = !enSimple; continue; }
                if (!enDoble && !enSimple && ch == '`') { enBacktick = !enBacktick; continue; }

                if (enDoble || enSimple || enBacktick) continue;

                if (ch == '/' && next == '/') break;
                if (ch == '/' && next == '*')
                {
                    enComentarioBloque = true;
                    lineaComBloque = numLinea;
                    c++;
                    continue;
                }

                if (ch is '(' or '[' or '{')
                {
                    pila.Push((ch, numLinea, c + 1));
                }
                else if (ch is ')' or ']' or '}')
                {
                    char esperado = ch switch { ')' => '(', ']' => '[', _ => '{' };
                    if (pila.Count == 0 || pila.Peek().Simbolo != esperado)
                    {
                        problemas.Add(CrearProblema(ruta, nombre, $"{prefijo}-E01", "Error", numLinea, c + 1,
                            $"Símbolo de cierre '{ch}' sin apertura correspondiente."));
                    }
                    else
                    {
                        pila.Pop();
                    }
                }
            }

            if (enDoble || enSimple)
            {
                problemas.Add(CrearProblema(ruta, nombre, $"{prefijo}-E02", "Error", numLinea, Math.Max(1, l.Length),
                    "Cadena de texto sin comilla de cierre en esta línea."));
            }
        }

        if (enComentarioBloque)
        {
            problemas.Add(CrearProblema(ruta, nombre, $"{prefijo}-E03", "Error", lineaComBloque, 1,
                "Comentario de bloque /* ... */ sin cierre."));
        }

        while (pila.Count > 0)
        {
            var abierto = pila.Pop();
            problemas.Add(CrearProblema(ruta, nombre, $"{prefijo}-E04", "Error", abierto.Linea, abierto.Col,
                $"'{abierto.Simbolo}' abierto aquí no fue cerrado."));
        }

        return problemas;
    }

    private static string RemoverComentarioPython(string linea)
    {
        bool s = false, d = false, esc = false;
        for (int i = 0; i < linea.Length; i++)
        {
            char c = linea[i];
            if (esc) { esc = false; continue; }
            if ((s || d) && c == '\\') { esc = true; continue; }
            if (!d && c == '\'') s = !s;
            else if (!s && c == '"') d = !d;
            else if (!s && !d && c == '#') return linea[..i];
        }
        return linea;
    }

    private static ProblemaCodigo Convertir(string ruta, string nombre, ErrorAlba err) =>
        CrearProblema(ruta, nombre, err.Codigo, "Error", Math.Max(1, err.Ubicacion.Linea), Math.Max(1, err.Ubicacion.Columna), err.Mensaje);

    private static ProblemaCodigo CrearProblema(
        string ruta,
        string nombre,
        string codigo,
        string severidad,
        int linea,
        int columna,
        string mensaje) => new()
        {
            RutaArchivo = ruta,
            NombreArchivo = nombre,
            Codigo = codigo,
            Severidad = severidad,
            Linea = linea,
            Columna = columna,
            Longitud = 1,
            Mensaje = mensaje
        };

    [GeneratedRegex(@"<\s*(/?)\s*([a-zA-Z0-9:-]+)(?:\s[^>]*)?>", RegexOptions.Compiled)]
    private static partial Regex RegexEtiquetaHtml();

    [GeneratedRegex(@"\bid\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex RegexIdHtml();

    [GeneratedRegex(@"^\s*(?:def|class|if|elif|else|for|while|try|except|finally|with)\b.*$", RegexOptions.Compiled)]
    private static partial Regex RegexBloquePython();
}
