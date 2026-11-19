using System.Text.Json.Serialization;

namespace Alba.IDE.Models;

/// <summary>
/// Representación deserializada del archivo manifest.json de un complemento.
/// </summary>
public sealed class ManifiestoAddon
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("descripcion")]
    public string Descripcion { get; set; } = string.Empty;

    [JsonPropertyName("autor")]
    public string Autor { get; set; } = string.Empty;

    [JsonPropertyName("lenguaje")]
    public string Lenguaje { get; set; } = string.Empty;

    [JsonPropertyName("extensiones")]
    public List<string> Extensiones { get; set; } = [];

    [JsonPropertyName("ejecutable")]
    public string Ejecutable { get; set; } = string.Empty;

    [JsonPropertyName("scriptEntrada")]
    public string? ScriptEntrada { get; set; }

    [JsonPropertyName("sintaxis")]
    public string? Sintaxis { get; set; }

    [JsonPropertyName("snippets")]
    public string? Snippets { get; set; }

    [JsonPropertyName("dependencias")]
    public List<string> Dependencias { get; set; } = [];
}

public sealed class SnippetAddon
{
    [JsonPropertyName("prefijo")]
    public string Prefijo { get; set; } = string.Empty;

    [JsonPropertyName("titulo")]
    public string Titulo { get; set; } = string.Empty;

    [JsonPropertyName("descripcion")]
    public string Descripcion { get; set; } = string.Empty;

    [JsonPropertyName("cuerpo")]
    public string Cuerpo { get; set; } = string.Empty;
}

public sealed class AddonCargado
{
    public required ManifiestoAddon Manifiesto { get; init; }
    public required string RutaCarpeta { get; init; }
    public bool Activo { get; set; }
    public bool EsValido { get; init; }
    public string EstadoSeguridad { get; init; } = "Validado";
    public string? RutaSintaxisResuelta { get; init; }
    public string? RutaScriptResuelta { get; init; }
    public List<SnippetAddon> Snippets { get; init; } = [];

    public string ExtensionesTexto => string.Join(", ", Manifiesto.Extensiones);
}
