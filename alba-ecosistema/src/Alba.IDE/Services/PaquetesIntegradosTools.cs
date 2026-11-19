using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Alba.IDE.Models;

namespace Alba.IDE.Services;

/// <summary>
/// Instala paquetes oficiales verificados de integración de lenguajes (resaltado XSHD + snippets + ejecutor)
/// directamente en el directorio de addons del usuario sin pasos manuales.
/// </summary>
public static class PaquetesIntegradosTools
{
    private sealed record DefinicionPaquete(
        string Id,
        string Nombre,
        string Lenguaje,
        string Descripcion,
        string[] Extensiones,
        string Ejecutable,
        string[] PalabrasClave,
        (string Prefijo, string Titulo, string Descripcion, string Cuerpo)[] Snippets);

    private static readonly Dictionary<string, DefinicionPaquete> Paquetes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pack-python-xynex"] = new(
            "pack-python-xynex",
            "Paquete Oficial Python para Xynex",
            "Python",
            "Resaltado XSHD, snippets inteligentes y conexión directa con Python 3.",
            [".py", ".pyw"],
            "python3",
            ["def", "class", "if", "elif", "else", "for", "while", "return", "import", "from", "as", "try", "except", "finally", "with", "async", "await", "yield", "lambda", "True", "False", "None"],
            [
                ("def", "Función Python", "Declara una función tipada", "def nombre(param: str) -> None:\n    pass"),
                ("ifmain", "Punto de entrada", "Bloque principal ejecutable", "if __name__ == \"__main__\":\n    main()"),
                ("class", "Clase Python", "Define una clase con constructor", "class MiClase:\n    def __init__(self) -> None:\n        pass")
            ]),
        ["pack-web-ts"] = new(
            "pack-web-ts",
            "Paquete TypeScript y JavaScript",
            "TypeScript / JS",
            "Resaltado sintáctico, plantillas y snippets modernos para .ts y .js.",
            [".ts", ".tsx", ".mjs"],
            "node",
            ["const", "let", "function", "interface", "type", "class", "export", "import", "async", "await", "return", "if", "else", "for", "while", "try", "catch"],
            [
                ("fn", "Función flecha", "Declara una función constante", "const nombre = () => {\n  \n};"),
                ("iface", "Interface TypeScript", "Declara una interfaz tipada", "export interface Modelo {\n  id: string;\n}")
            ]),
        ["pack-go-lang"] = new(
            "pack-go-lang",
            "Paquete Go (Golang)",
            "Go",
            "Soporte de edición, snippets y ejecución para archivos .go.",
            [".go"],
            "go",
            ["package", "import", "func", "var", "const", "type", "struct", "interface", "if", "else", "for", "range", "return", "go", "defer", "chan", "select"],
            [
                ("main", "Programa Go", "Estructura package main", "package main\n\nimport \"fmt\"\n\nfunc main() {\n\tfmt.Println(\"Hola desde Go\")\n}")
            ]),
        ["pack-rust-lang"] = new(
            "pack-rust-lang",
            "Paquete Rust",
            "Rust",
            "Resaltado sintáctico, snippets y compilación para archivos .rs.",
            [".rs"],
            "rustc",
            ["fn", "let", "mut", "const", "struct", "enum", "impl", "trait", "pub", "use", "mod", "match", "if", "else", "loop", "while", "for", "in", "return"],
            [
                ("fnmain", "Función main Rust", "Punto de entrada Rust", "fn main() {\n    println!(\"Hola desde Rust\");\n}")
            ]),
        ["pack-java-lang"] = new(
            "pack-java-lang",
            "Paquete Java",
            "Java",
            "Resaltado sintáctico, plantillas OOP y ejecución para archivos .java.",
            [".java"],
            "java",
            ["public", "private", "protected", "class", "interface", "extends", "implements", "static", "void", "final", "return", "new", "if", "else", "for", "while", "try", "catch", "import", "package"],
            [
                ("psvm", "Clase y main Java", "Crea punto de entrada Java", "public class Principal {\n    public static void main(String[] args) {\n        System.out.println(\"Hola desde Java\");\n    }\n}")
            ]),
        ["pack-php-lang"] = new(
            "pack-php-lang",
            "Paquete PHP",
            "PHP",
            "Soporte de edición, snippets y ejecución CLI para archivos .php.",
            [".php"],
            "php",
            ["function", "class", "public", "private", "protected", "return", "if", "elseif", "else", "foreach", "while", "echo", "require", "namespace", "use"],
            [
                ("php", "Bloque PHP", "Etiqueta inicial y función", "<?php\n\necho \"Hola desde PHP\\n\";\n")
            ]),
        ["pack-ruby-lang"] = new(
            "pack-ruby-lang",
            "Paquete Ruby",
            "Ruby",
            "Resaltado sintáctico, snippets y ejecución para archivos .rb.",
            [".rb"],
            "ruby",
            ["def", "end", "class", "module", "if", "elsif", "else", "unless", "while", "do", "return", "require", "puts", "true", "false", "nil"],
            [
                ("def", "Método Ruby", "Define un método Ruby", "def saludar(nombre)\n  puts \"Hola, #{nombre}\"\nend")
            ])
    };

    public static (bool Exito, string Mensaje, string HashSha256) InstalarPaqueteIncorporado(
        string idPaquete,
        GestorAddons gestorAddons)
    {
        if (!Paquetes.TryGetValue(idPaquete, out DefinicionPaquete? def))
        {
            return (false, $"No existe el paquete integrado '{idPaquete}'.", string.Empty);
        }

        string tempDir = Path.Combine(Path.GetTempPath(), $"xynex_pack_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(tempDir, "syntax"));

        try
        {
            var manifiesto = new ManifiestoAddon
            {
                Id = def.Id,
                Nombre = def.Nombre,
                Version = "5.0.0",
                Descripcion = def.Descripcion,
                Autor = "CynexStudios (Verificado)",
                Lenguaje = def.Lenguaje,
                Extensiones = def.Extensiones.ToList(),
                Ejecutable = def.Ejecutable,
                Sintaxis = $"syntax/{def.Id}.xshd",
                Snippets = "snippets.json"
            };

            string manifestJson = JsonSerializer.Serialize(manifiesto, new JsonSerializerOptions { WriteIndented = true });
            string manifestPath = Path.Combine(tempDir, "manifest.json");
            File.WriteAllText(manifestPath, manifestJson, Encoding.UTF8);

            List<SnippetAddon> snippets = def.Snippets.Select(s => new SnippetAddon
            {
                Prefijo = s.Prefijo,
                Titulo = s.Titulo,
                Descripcion = s.Descripcion,
                Cuerpo = s.Cuerpo
            }).ToList();
            File.WriteAllText(
                Path.Combine(tempDir, "snippets.json"),
                JsonSerializer.Serialize(snippets, new JsonSerializerOptions { WriteIndented = true }),
                Encoding.UTF8);

            string xshd = ConstruirXshd(def.Nombre, def.Extensiones, def.PalabrasClave);
            File.WriteAllText(Path.Combine(tempDir, "syntax", $"{def.Id}.xshd"), xshd, Encoding.UTF8);

            string hash = CalcularSha256(manifestJson + xshd);
            var (exito, mensaje) = gestorAddons.InstalarDesdeCarpeta(tempDir);
            return (exito, exito ? $"{mensaje} (SHA-256: {hash[..12]}...)" : mensaje, hash);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    private static string ConstruirXshd(string nombre, string[] extensiones, string[] palabrasClave)
    {
        string exts = string.Join(";", extensiones);
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\"?>");
        sb.AppendLine($"<SyntaxDefinition name=\"{System.Security.SecurityElement.Escape(nombre)}\" extensions=\"{exts}\" xmlns=\"http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008\">");
        sb.AppendLine("  <Color name=\"Comentario\" foreground=\"#6A9955\" />");
        sb.AppendLine("  <Color name=\"Cadena\" foreground=\"#F4A261\" />");
        sb.AppendLine("  <Color name=\"PalabraClave\" foreground=\"#38BDF8\" fontWeight=\"bold\" />");
        sb.AppendLine("  <Color name=\"Numero\" foreground=\"#B5CEA8\" />");
        sb.AppendLine("  <RuleSet>");
        sb.AppendLine("    <Span color=\"Comentario\" begin=\"//\" />");
        sb.AppendLine("    <Span color=\"Comentario\" begin=\"#\" />");
        sb.AppendLine("    <Span color=\"Cadena\" begin=\"&quot;\" end=\"&quot;\" />");
        sb.AppendLine("    <Span color=\"Cadena\" begin=\"'\" end=\"'\" />");
        sb.AppendLine("    <Rule color=\"Numero\">\\b[0-9]+(\\.[0-9]+)?\\b</Rule>");
        sb.AppendLine("    <Keywords color=\"PalabraClave\">");
        foreach (string kw in palabrasClave)
        {
            sb.AppendLine($"      <Word>{System.Security.SecurityElement.Escape(kw)}</Word>");
        }
        sb.AppendLine("    </Keywords>");
        sb.AppendLine("  </RuleSet>");
        sb.AppendLine("</SyntaxDefinition>");
        return sb.ToString();
    }

    private static string CalcularSha256(string contenido)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(contenido));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
