namespace Alba.IDE.Models;

public enum ProveedorIAUsuario
{
    OllamaLocal,
    OpenAICompatible,
    GoogleGemini,
    AnthropicClaude
}

public sealed class CuentaUsuario
{
    public int Id { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string NombreMostrar { get; set; } = string.Empty;
    public DateTime CreadoUtc { get; set; }
    public DateTime UltimoAccesoUtc { get; set; }

    public string Iniciales =>
        string.IsNullOrWhiteSpace(NombreMostrar)
            ? (NombreUsuario.Length >= 2 ? NombreUsuario[..2].ToUpperInvariant() : "XY")
            : NombreMostrar[..Math.Min(2, NombreMostrar.Length)].ToUpperInvariant();
}

public sealed class PerfilIAConectado
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public ProveedorIAUsuario Proveedor { get; set; }
    public string NombrePerfil { get; set; } = string.Empty;
    public string EndpointUrl { get; set; } = string.Empty;
    public string Modelo { get; set; } = string.Empty;
    public string ApiKeyCifrada { get; set; } = string.Empty;
    public bool EsActivo { get; set; }
    public DateTime ActualizadoUtc { get; set; }

    public string EtiquetaProveedor => Proveedor switch
    {
        ProveedorIAUsuario.OllamaLocal => "Ollama / LM Studio (Local)",
        ProveedorIAUsuario.OpenAICompatible => "OpenAI / Groq / DeepSeek / OpenRouter",
        ProveedorIAUsuario.GoogleGemini => "Google Gemini (Tu API Key)",
        ProveedorIAUsuario.AnthropicClaude => "Anthropic Claude (Tu API Key)",
        _ => Proveedor.ToString()
    };
}

public sealed class ResultadoOperacionCuenta
{
    public bool Exito { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public CuentaUsuario? Cuenta { get; set; }
}
