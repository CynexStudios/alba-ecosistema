using System.Net;
using System.Text.RegularExpressions;
using Alba.IDE.Models;

namespace Alba.IDE.Services;

/// <summary>
/// Analiza documentos HTML, CSS, tablas, imágenes locales y scripts JS para detectar errores estructurales
/// y generar un árbol visual completo dentro de la vista previa del IDE.
/// </summary>
public static partial class AnalizadorWebHtml
{
    [GeneratedRegex(@"<title\b[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RegexTitle();

    [GeneratedRegex(@"<(h[1-6]|p|button|a|li|code|pre|label|blockquote|nav|header)\b([^>]*)>(.*?)</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RegexBloques();

    [GeneratedRegex(@"<table\b[^>]*>(.*?)</table>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RegexTabla();

    [GeneratedRegex(@"<tr\b[^>]*>(.*?)</tr>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RegexFilaTabla();

    [GeneratedRegex(@"<(?:td|th)\b[^>]*>(.*?)</(?:td|th)>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RegexCeldaTabla();

    [GeneratedRegex(@"<img\b([^>]*)/?>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RegexImagen();

    [GeneratedRegex(@"<input\b([^>]*)/?>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RegexInputSolo();

    [GeneratedRegex(@"<(link|script|img)\b[^>]*(?:href|src)\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex RegexRecursosLocales();

    [GeneratedRegex(@"<style\b[^>]*>(.*?)</style>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RegexStyle();

    [GeneratedRegex(@"<script\b(?![^>]*\bsrc\s*=)[^>]*>(.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RegexScriptInline();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex RegexStripTags();

    public static ResultadoAnalisisWeb Analizar(string? rutaArchivo, string htmlFuente)
    {
        var errores = new List<MensajeConsolaWeb>();
        var bloques = new List<BloquePreviewWeb>();

        string texto = htmlFuente ?? string.Empty;
        string origen = string.IsNullOrWhiteSpace(rutaArchivo) ? "documento.html" : Path.GetFileName(rutaArchivo);
        string? dirBase = !string.IsNullOrWhiteSpace(rutaArchivo) ? Path.GetDirectoryName(rutaArchivo) : null;

        if (string.IsNullOrWhiteSpace(texto))
        {
            errores.Add(new MensajeConsolaWeb
            {
                Nivel = "render",
                Origen = origen,
                Mensaje = "El documento HTML está vacío."
            });
            return new ResultadoAnalisisWeb { Errores = errores };
        }

        string[] etiquetasCriticas = ["html", "body", "head", "main", "div", "section", "table", "form", "script", "style", "button"];
        foreach (string tag in etiquetasCriticas)
        {
            int abiertas = Regex.Matches(texto, $@"<\s*{tag}\b(?![^>]*/\s*>)", RegexOptions.IgnoreCase).Count;
            int cerradas = Regex.Matches(texto, $@"</\s*{tag}\s*>", RegexOptions.IgnoreCase).Count;
            if (abiertas != cerradas)
            {
                errores.Add(new MensajeConsolaWeb
                {
                    Nivel = "render",
                    Origen = origen,
                    Mensaje = $"Etiqueta <{tag}> desbalanceada ({abiertas} apertura(s), {cerradas} cierre(s))."
                });
            }
        }

        string cssCombinado = string.Concat(RegexStyle().Matches(texto).Select(m => m.Groups[1].Value + "\n"));

        foreach (Match m in RegexRecursosLocales().Matches(texto))
        {
            string tag = m.Groups[1].Value.ToLowerInvariant();
            string refPath = m.Groups[2].Value.Trim();
            if (string.IsNullOrWhiteSpace(refPath) ||
                refPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                refPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                refPath.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                refPath.StartsWith("//", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(dirBase) && Directory.Exists(dirBase))
            {
                string limpio = refPath.Split('?', '#')[0].TrimStart('/');
                if (!ValidadorAddons.TryResolverRutaSegura(dirBase, limpio, out string? rutaCompleta) ||
                    string.IsNullOrWhiteSpace(rutaCompleta) ||
                    !File.Exists(rutaCompleta))
                {
                    errores.Add(new MensajeConsolaWeb
                    {
                        Nivel = "error",
                        Origen = origen,
                        Mensaje = $"Recurso local no encontrado en <{tag}>: '{refPath}'."
                    });
                }
                else if (limpio.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
                {
                    string cssArchivo = File.ReadAllText(rutaCompleta);
                    cssCombinado += "\n" + cssArchivo;
                    VerificarBalanceLlaves(cssArchivo, Path.GetFileName(rutaCompleta), "CSS", errores);
                }
                else if (limpio.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                {
                    string jsArchivo = File.ReadAllText(rutaCompleta);
                    VerificarBalanceLlaves(jsArchivo, Path.GetFileName(rutaCompleta), "JavaScript", errores);
                }
            }
        }

        foreach (Match m in RegexStyle().Matches(texto))
        {
            VerificarBalanceLlaves(m.Groups[1].Value, origen, "CSS embebido", errores);
        }
        foreach (Match m in RegexScriptInline().Matches(texto))
        {
            VerificarBalanceLlaves(m.Groups[1].Value, origen, "JS embebido", errores);
        }

        Match tituloMatch = RegexTitle().Match(texto);
        string titulo = tituloMatch.Success
            ? WebUtility.HtmlDecode(RegexStripTags().Replace(tituloMatch.Groups[1].Value, string.Empty).Trim())
            : origen;

        string fondoHex = ExtraerColorCss(cssCombinado, "body", "background", "#111827");
        string colorHex = ExtraerColorCss(cssCombinado, "body", "color", "#E2E8F0");
        string tarjetaHex = ExtraerColorCss(cssCombinado, @"\.(?:card|contenedor|panel)", "background", "#1E293B");
        string acentoHex = "#007ACC";
        Match btnBg = Regex.Match(cssCombinado, @"button\s*\{[^}]*?background(?:-color)?\s*:\s*(#[0-9a-fA-F]{3,8})", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (btnBg.Success) acentoHex = btnBg.Groups[1].Value;

        string soloBody = ExtraerCuerpo(texto);

        // Extraer imágenes
        foreach (Match m in RegexImagen().Matches(soloBody))
        {
            string attrs = m.Groups[1].Value;
            Match srcM = Regex.Match(attrs, @"src\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            Match altM = Regex.Match(attrs, @"alt\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase);
            string src = srcM.Success ? srcM.Groups[1].Value.Trim() : string.Empty;
            string rutaResuelta = string.Empty;

            if (!string.IsNullOrWhiteSpace(src) && !string.IsNullOrWhiteSpace(dirBase) &&
                !src.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                !src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                string limpio = src.Split('?', '#')[0].TrimStart('/');
                if (ValidadorAddons.TryResolverRutaSegura(dirBase, limpio, out string? r) && r is not null && File.Exists(r))
                {
                    rutaResuelta = r;
                }
            }

            bloques.Add(new BloquePreviewWeb
            {
                Etiqueta = "img",
                Texto = altM.Success && !string.IsNullOrWhiteSpace(altM.Groups[1].Value) ? altM.Groups[1].Value : Path.GetFileName(src),
                ImagenRuta = rutaResuelta,
                Href = src
            });
        }

        // Extraer bloques textuales y de navegación
        foreach (Match m in RegexBloques().Matches(soloBody))
        {
            string tag = m.Groups[1].Value.ToLowerInvariant();
            string attrs = m.Groups[2].Value;
            string contenidoRaw = RegexStripTags().Replace(m.Groups[3].Value, " ");
            string limpio = WebUtility.HtmlDecode(Regex.Replace(contenidoRaw, @"\s+", " ").Trim());
            if (string.IsNullOrWhiteSpace(limpio)) continue;

            string href = string.Empty;
            if (tag == "a")
            {
                Match hm = Regex.Match(attrs, @"href\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                if (hm.Success) href = hm.Groups[1].Value;
            }

            bloques.Add(new BloquePreviewWeb
            {
                Etiqueta = tag,
                Texto = limpio,
                Href = href,
                FondoHex = tarjetaHex
            });
        }

        // Extraer tablas HTML
        foreach (Match mt in RegexTabla().Matches(soloBody))
        {
            var filas = new List<IReadOnlyList<string>>();
            foreach (Match mr in RegexFilaTabla().Matches(mt.Groups[1].Value))
            {
                var celdas = new List<string>();
                foreach (Match mc in RegexCeldaTabla().Matches(mr.Groups[1].Value))
                {
                    string txt = WebUtility.HtmlDecode(Regex.Replace(RegexStripTags().Replace(mc.Groups[1].Value, string.Empty), @"\s+", " ").Trim());
                    celdas.Add(txt);
                }
                if (celdas.Count > 0) filas.Add(celdas);
            }

            if (filas.Count > 0)
            {
                bloques.Add(new BloquePreviewWeb
                {
                    Etiqueta = "table",
                    FilasTabla = filas
                });
            }
        }

        foreach (Match m in RegexInputSolo().Matches(soloBody))
        {
            string attrs = m.Groups[1].Value;
            Match ph = Regex.Match(attrs, @"(?:placeholder|value|type)\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            bloques.Add(new BloquePreviewWeb
            {
                Etiqueta = "input",
                Texto = ph.Success ? WebUtility.HtmlDecode(ph.Groups[1].Value) : "Campo de entrada..."
            });
        }

        return new ResultadoAnalisisWeb
        {
            TituloPagina = string.IsNullOrWhiteSpace(titulo) ? origen : titulo,
            FondoCssHex = fondoHex,
            ColorTextoCssHex = colorHex,
            ColorAcentoCssHex = acentoHex,
            FondoTarjetaCssHex = tarjetaHex,
            Bloques = bloques,
            Errores = errores
        };
    }

    private static string ExtraerCuerpo(string html)
    {
        Match m = Regex.Match(html, @"<body\b[^>]*>(.*?)</body>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        string baseHtml = m.Success ? m.Groups[1].Value : html;
        baseHtml = Regex.Replace(baseHtml, @"<script\b[^>]*>.*?</script>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        baseHtml = Regex.Replace(baseHtml, @"<style\b[^>]*>.*?</style>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return baseHtml;
    }

    private static string ExtraerColorCss(string css, string selectorRegex, string prop, string defecto)
    {
        Match m = Regex.Match(css, $@"{selectorRegex}\s*\{{[^}}]*?\b{prop}(?:-color)?\s*:\s*(#[0-9a-fA-F]{{3,8}})", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return m.Success ? m.Groups[1].Value : defecto;
    }

    private static void VerificarBalanceLlaves(string codigo, string origen, string contexto, List<MensajeConsolaWeb> errores)
    {
        int llaves = 0;
        int paren = 0;
        bool enComillaSimple = false;
        bool enComillaDoble = false;
        bool escape = false;

        foreach (char c in codigo)
        {
            if (escape) { escape = false; continue; }
            if (c == '\\') { escape = true; continue; }
            if (!enComillaDoble && c == '\'') { enComillaSimple = !enComillaSimple; continue; }
            if (!enComillaSimple && c == '"') { enComillaDoble = !enComillaDoble; continue; }
            if (enComillaSimple || enComillaDoble) continue;

            if (c == '{') llaves++;
            else if (c == '}') llaves--;
            else if (c == '(') paren++;
            else if (c == ')') paren--;
        }

        if (llaves != 0 || paren != 0)
        {
            errores.Add(new MensajeConsolaWeb
            {
                Nivel = "error",
                Origen = origen,
                Mensaje = $"Error de sintaxis en {contexto}: llaves ({llaves}) o paréntesis ({paren}) sin cerrar."
            });
        }
    }
}
