using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Alba.IDE.Models;

namespace Alba.IDE.Services;

public sealed record MensajeChatIA(
    string Rol,
    string Contenido,
    string? BloqueCodigoExtraido = null,
    bool EsError = false)
{
    public bool TieneBloqueCodigo => !string.IsNullOrWhiteSpace(BloqueCodigoExtraido);
    public string EtiquetaRol => Rol switch
    {
        "usuario" => "Tú",
        "sistema" => "Aviso",
        _ => "Agente IA"
    };
}

public sealed record ContextoCodigoIA(
    string NombreArchivo,
    string Lenguaje,
    string CodigoActual,
    IReadOnlyList<string> ErroresActuales);

/// <summary>
/// Conecta al Agente de IA usando exclusivamente el proveedor y clave/endpoint propios del usuario
/// almacenados de forma cifrada en la base de datos local (Ollama, Gemini, OpenAI-compatible o Claude).
/// </summary>
public sealed partial class ServicioAgenteIA
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(75) };

    public static string PromptSistemaAgente => """
        Eres el Agente de Ingeniería integrado en Xynex Code Editor 9.0.
        Ayudas al desarrollador con Cibao Code 9.0 (.cibao/.alb), Python, HTML, CSS, JavaScript, TypeScript, C# y C++.
        Reglas clave de Cibao 9.0:
        - Declaración: `variable nombre: texto = "valor"`, `constante PI = 3.14`
        - Tipos: entero, decimal, texto, booleano, lista, diccionario, vacio
        - Salida: `imprimir(...)` o `mostrar(...)`, cadenas `$"Hola {nombre}"` y multilínea `\"\"\"...\"\"\"`
        - Rangos: `1..5`, `1..=5`; opcional: `obj?.prop ?? "defecto"`
        - Funciones y lambdas: `funcion suma(a: entero, b: entero): entero { retornar a + b }` o `(a, b) => a + b`
        - Control: `si / sino_si / sino`, `elegir / caso / defecto`, `para x en coleccion`, `mientras`, `intentar / capturar`
        Responde en español claro, directo y con un bloque ```cibao (o del lenguaje pedido) listo para aplicar al editor.
        """.Replace("\\\"\\\"\\\"", "\"\"\"");

    public async Task<MensajeChatIA> EnviarConsultaAsync(
        PerfilIAConectado perfil,
        string apiKeyPlana,
        IReadOnlyList<MensajeChatIA> historial,
        string consultaUsuario,
        ContextoCodigoIA contexto,
        CancellationToken ct = default)
    {
        if (!ServicioSeguridad.EsUrlHttpSegura(perfil.EndpointUrl, permitirHttpLocalhost: true))
        {
            return new MensajeChatIA(
                "sistema",
                "URL de endpoint rechazada por seguridad. Usa HTTPS o http://localhost / http://127.0.0.1.",
                EsError: true);
        }

        if (perfil.Proveedor != ProveedorIAUsuario.OllamaLocal && string.IsNullOrWhiteSpace(apiKeyPlana))
        {
            return new MensajeChatIA(
                "sistema",
                "Falta tu API Key personal en el perfil seleccionado. Configúrala en Cuenta / Conectar mi IA.",
                EsError: true);
        }

        try
        {
            string mensajeConContexto = ConstruirMensajeConContexto(consultaUsuario, contexto);
            using var req = ConstruirHttpRequest(perfil, apiKeyPlana, historial, mensajeConContexto);
            using HttpResponseMessage resp = await Http.SendAsync(req, ct);
            string jsonResp = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                string detalle = jsonResp.Length > 400 ? jsonResp[..400] + "..." : jsonResp;
                return new MensajeChatIA(
                    "sistema",
                    $"El proveedor ({perfil.EtiquetaProveedor}) respondió {(int)resp.StatusCode}: {detalle}",
                    EsError: true);
            }

            string textoRespuesta = ExtraerTextoRespuesta(perfil.Proveedor, jsonResp);
            if (string.IsNullOrWhiteSpace(textoRespuesta))
            {
                return new MensajeChatIA("sistema", "La IA devolvió una respuesta vacía.", EsError: true);
            }

            string? codigo = ExtraerPrimerBloqueCodigo(textoRespuesta);
            return new MensajeChatIA("agente", textoRespuesta.Trim(), codigo);
        }
        catch (TaskCanceledException)
        {
            return new MensajeChatIA("sistema", "La solicitud al agente IA excedió el tiempo límite o fue cancelada.", EsError: true);
        }
        catch (Exception ex)
        {
            return new MensajeChatIA("sistema", $"Error al conectar con tu IA: {ex.Message}", EsError: true);
        }
    }

    public static string ConstruirMensajeConContexto(string consultaUsuario, ContextoCodigoIA contexto)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[Archivo activo: {contexto.NombreArchivo} | Lenguaje: {contexto.Lenguaje}]");
        if (contexto.ErroresActuales.Count > 0)
        {
            sb.AppendLine("Errores detectados actualmente:");
            foreach (string err in contexto.ErroresActuales.Take(6))
            {
                sb.AppendLine($"- {err}");
            }
        }

        string codigoRecortado = contexto.CodigoActual.Length > 4500
            ? contexto.CodigoActual[..4500] + "\n... (truncado)"
            : contexto.CodigoActual;

        if (!string.IsNullOrWhiteSpace(codigoRecortado))
        {
            sb.AppendLine("Código actual en el editor:");
            sb.AppendLine("```");
            sb.AppendLine(codigoRecortado);
            sb.AppendLine("```");
        }

        sb.AppendLine("Solicitud del usuario:");
        sb.AppendLine(consultaUsuario);
        return sb.ToString();
    }

    public static HttpRequestMessage ConstruirHttpRequest(
        PerfilIAConectado perfil,
        string apiKeyPlana,
        IReadOnlyList<MensajeChatIA> historial,
        string mensajeConContexto)
    {
        string baseUrl = perfil.EndpointUrl.Trim().TrimEnd('/');

        switch (perfil.Proveedor)
        {
            case ProveedorIAUsuario.OllamaLocal:
            {
                string url = baseUrl.EndsWith("/api/chat", StringComparison.OrdinalIgnoreCase)
                    ? baseUrl
                    : $"{baseUrl}/api/chat";
                var mensajes = new List<object> { new { role = "system", content = PromptSistemaAgente } };
                foreach (MensajeChatIA m in historial.TakeLast(6))
                {
                    if (!m.EsError)
                        mensajes.Add(new { role = m.Rol == "usuario" ? "user" : "assistant", content = m.Contenido });
                }
                mensajes.Add(new { role = "user", content = mensajeConContexto });

                string json = JsonSerializer.Serialize(new
                {
                    model = perfil.Modelo,
                    messages = mensajes,
                    stream = false
                });
                return new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            }

            case ProveedorIAUsuario.GoogleGemini:
            {
                string url = $"{baseUrl}/v1beta/models/{Uri.EscapeDataString(perfil.Modelo)}:generateContent";
                var contents = new List<object>();
                foreach (MensajeChatIA m in historial.TakeLast(6))
                {
                    if (!m.EsError)
                    {
                        contents.Add(new
                        {
                            role = m.Rol == "usuario" ? "user" : "model",
                            parts = new[] { new { text = m.Contenido } }
                        });
                    }
                }
                contents.Add(new
                {
                    role = "user",
                    parts = new[] { new { text = $"{PromptSistemaAgente}\n\n{mensajeConContexto}" } }
                });

                string json = JsonSerializer.Serialize(new { contents });
                var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                req.Headers.TryAddWithoutValidation("x-goog-api-key", apiKeyPlana);
                return req;
            }

            case ProveedorIAUsuario.AnthropicClaude:
            {
                var msgs = new List<object>();
                foreach (MensajeChatIA m in historial.TakeLast(6))
                {
                    if (!m.EsError)
                        msgs.Add(new { role = m.Rol == "usuario" ? "user" : "assistant", content = m.Contenido });
                }
                msgs.Add(new { role = "user", content = mensajeConContexto });

                string json = JsonSerializer.Serialize(new
                {
                    model = perfil.Modelo,
                    max_tokens = 2048,
                    system = PromptSistemaAgente,
                    messages = msgs
                });
                var req = new HttpRequestMessage(HttpMethod.Post, baseUrl)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                req.Headers.TryAddWithoutValidation("x-api-key", apiKeyPlana);
                req.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
                return req;
            }

            case ProveedorIAUsuario.OpenAICompatible:
            default:
            {
                string url = baseUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
                    ? baseUrl
                    : $"{baseUrl}/v1/chat/completions";
                var mensajes = new List<object> { new { role = "system", content = PromptSistemaAgente } };
                foreach (MensajeChatIA m in historial.TakeLast(6))
                {
                    if (!m.EsError)
                        mensajes.Add(new { role = m.Rol == "usuario" ? "user" : "assistant", content = m.Contenido });
                }
                mensajes.Add(new { role = "user", content = mensajeConContexto });

                string json = JsonSerializer.Serialize(new
                {
                    model = perfil.Modelo,
                    messages = mensajes,
                    temperature = 0.2
                });
                var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKeyPlana);
                return req;
            }
        }
    }

    public static string ExtraerTextoRespuesta(ProveedorIAUsuario proveedor, string jsonResp)
    {
        using JsonDocument doc = JsonDocument.Parse(jsonResp);
        JsonElement root = doc.RootElement;

        switch (proveedor)
        {
            case ProveedorIAUsuario.OllamaLocal:
                if (root.TryGetProperty("message", out JsonElement msg) &&
                    msg.TryGetProperty("content", out JsonElement content))
                {
                    return content.GetString() ?? string.Empty;
                }
                break;

            case ProveedorIAUsuario.GoogleGemini:
                if (root.TryGetProperty("candidates", out JsonElement cands) && cands.GetArrayLength() > 0 &&
                    cands[0].TryGetProperty("content", out JsonElement gContent) &&
                    gContent.TryGetProperty("parts", out JsonElement parts) && parts.GetArrayLength() > 0 &&
                    parts[0].TryGetProperty("text", out JsonElement gText))
                {
                    return gText.GetString() ?? string.Empty;
                }
                break;

            case ProveedorIAUsuario.AnthropicClaude:
                if (root.TryGetProperty("content", out JsonElement aContent) && aContent.GetArrayLength() > 0 &&
                    aContent[0].TryGetProperty("text", out JsonElement aText))
                {
                    return aText.GetString() ?? string.Empty;
                }
                break;

            case ProveedorIAUsuario.OpenAICompatible:
            default:
                if (root.TryGetProperty("choices", out JsonElement choices) && choices.GetArrayLength() > 0 &&
                    choices[0].TryGetProperty("message", out JsonElement oMsg) &&
                    oMsg.TryGetProperty("content", out JsonElement oContent))
                {
                    return oContent.GetString() ?? string.Empty;
                }
                break;
        }

        return string.Empty;
    }

    public static string? ExtraerPrimerBloqueCodigo(string texto)
    {
        Match m = RegexBloqueCodigo().Match(texto);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }

    [GeneratedRegex(@"```(?:[a-zA-Z0-9_+-]+)?\s*\r?\n([\s\S]*?)\r?\n```", RegexOptions.Compiled)]
    private static partial Regex RegexBloqueCodigo();
}
