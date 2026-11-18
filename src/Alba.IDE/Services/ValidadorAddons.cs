using System.Text.RegularExpressions;
using Alba.IDE.Models;

namespace Alba.IDE.Services;

public sealed record ResultadoValidacionAddon(
    bool EsValido,
    string Mensaje,
    string? RutaSintaxisResuelta,
    string? RutaScriptResuelta
);

/// <summary>
/// Valida manifiestos de complementos, rutas relativas y comandos permitidos para prevenir
/// ejecución arbitraria o escalamiento fuera del directorio del addon.
/// </summary>
public static class ValidadorAddons
{
    private static readonly Regex RegexId = new(@"^[a-z0-9-]+$", RegexOptions.Compiled);
    private static readonly Regex RegexVersion = new(@"^\d+\.\d+\.\d+(-[a-zA-Z0-9.-]+)?$", RegexOptions.Compiled);
    private static readonly Regex RegexExtension = new(@"^\.[a-zA-Z0-9]+$", RegexOptions.Compiled);

    private static readonly HashSet<string> EjecutablesPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        "python3",
        "python",
        "py",
        "dotnet",
        "node",
        "gcc",
        "g++",
        "clang++"
    };

    public static ResultadoValidacionAddon Validar(ManifiestoAddon manifiesto, string carpetaAddon)
    {
        if (string.IsNullOrWhiteSpace(manifiesto.Id) || !RegexId.IsMatch(manifiesto.Id))
        {
            return Fallo("Identificador inválido. Solo se permiten minúsculas, números y guiones.");
        }

        if (string.IsNullOrWhiteSpace(manifiesto.Nombre))
        {
            return Fallo("El manifiesto debe definir un nombre.");
        }

        if (string.IsNullOrWhiteSpace(manifiesto.Version) || !RegexVersion.IsMatch(manifiesto.Version))
        {
            return Fallo("Versión semántica inválida (se esperaba formato X.Y.Z).");
        }

        if (string.IsNullOrWhiteSpace(manifiesto.Lenguaje))
        {
            return Fallo("El manifiesto debe indicar el lenguaje compatible.");
        }

        if (manifiesto.Extensiones.Count == 0 || manifiesto.Extensiones.Any(ext => !RegexExtension.IsMatch(ext)))
        {
            return Fallo("Las extensiones deben tener formato '.ext' válido.");
        }

        if (string.IsNullOrWhiteSpace(manifiesto.Ejecutable) || !EsEjecutableSeguro(manifiesto.Ejecutable))
        {
            return Fallo($"Ejecutable '{manifiesto.Ejecutable}' no autorizado en la lista blanca de seguridad.");
        }

        string? rutaSintaxis = null;
        if (!string.IsNullOrWhiteSpace(manifiesto.Sintaxis))
        {
            if (!TryResolverRutaSegura(carpetaAddon, manifiesto.Sintaxis, out rutaSintaxis) || !File.Exists(rutaSintaxis))
            {
                return Fallo($"Recurso de sintaxis inválido o fuera del directorio del addon: '{manifiesto.Sintaxis}'.");
            }
        }

        if (!string.IsNullOrWhiteSpace(manifiesto.Snippets))
        {
            if (!TryResolverRutaSegura(carpetaAddon, manifiesto.Snippets, out string? rutaSnippets) || !File.Exists(rutaSnippets))
            {
                return Fallo($"Archivo de snippets inválido o fuera del directorio del addon: '{manifiesto.Snippets}'.");
            }
        }

        string? rutaScript = null;
        if (!string.IsNullOrWhiteSpace(manifiesto.ScriptEntrada))
        {
            if (!TryResolverRutaSegura(carpetaAddon, manifiesto.ScriptEntrada, out rutaScript) || !File.Exists(rutaScript))
            {
                return Fallo($"Script de entrada inválido o fuera del directorio del addon: '{manifiesto.ScriptEntrada}'.");
            }
        }

        return new ResultadoValidacionAddon(true, "Validado", rutaSintaxis, rutaScript);
    }

    public static bool EsEjecutableSeguro(string ejecutable)
    {
        if (ejecutable.Contains('/') || ejecutable.Contains('\\') || ejecutable.Contains(' '))
        {
            return false;
        }
        return EjecutablesPermitidos.Contains(ejecutable);
    }

    public static bool TryResolverRutaSegura(string carpetaAddon, string rutaRelativa, out string? rutaResuelta)
    {
        rutaResuelta = null;
        if (string.IsNullOrWhiteSpace(rutaRelativa) || Path.IsPathRooted(rutaRelativa))
        {
            return false;
        }

        string raiz = Path.GetFullPath(carpetaAddon);
        if (!raiz.EndsWith(Path.DirectorySeparatorChar))
        {
            raiz += Path.DirectorySeparatorChar;
        }

        string candidata = Path.GetFullPath(Path.Combine(raiz, rutaRelativa));
        if (!candidata.StartsWith(raiz, StringComparison.Ordinal))
        {
            return false;
        }

        rutaResuelta = candidata;
        return true;
    }

    private static ResultadoValidacionAddon Fallo(string mensaje) =>
        new(false, mensaje, null, null);
}
