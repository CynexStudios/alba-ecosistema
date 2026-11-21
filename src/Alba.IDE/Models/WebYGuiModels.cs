namespace Alba.IDE.Models;

public enum ModoViewportWeb
{
    Escritorio,
    Tablet,
    Movil
}

public sealed class MensajeConsolaWeb
{
    public DateTime MarcaTiempo { get; init; } = DateTime.Now;
    public string Nivel { get; init; } = "log";
    public string Mensaje { get; init; } = string.Empty;
    public string Origen { get; init; } = "Web";

    public string ColorHex => Nivel.ToLowerInvariant() switch
    {
        "error" or "render" => "#F14C4C",
        "warn" or "advertencia" => "#CCA700",
        "ok" or "sistema" => "#3794FF",
        _ => "#CCCCCC"
    };

    public string Resumen => $"[{MarcaTiempo:HH:mm:ss}] [{Nivel.ToUpperInvariant()}] ({Origen}) {Mensaje}";
}

public sealed class BloquePreviewWeb
{
    public string Etiqueta { get; init; } = "p";
    public string Texto { get; init; } = string.Empty;
    public string Href { get; init; } = string.Empty;
    public string ImagenRuta { get; init; } = string.Empty;
    public string FondoHex { get; init; } = string.Empty;
    public IReadOnlyList<IReadOnlyList<string>> FilasTabla { get; init; } = [];
}

public sealed class ResultadoAnalisisWeb
{
    public string TituloPagina { get; init; } = "Vista previa HTML";
    public string FondoCssHex { get; init; } = "#1E1E1E";
    public string ColorTextoCssHex { get; init; } = "#E2E8F0";
    public string ColorAcentoCssHex { get; init; } = "#007ACC";
    public string FondoTarjetaCssHex { get; init; } = "#252526";
    public IReadOnlyList<BloquePreviewWeb> Bloques { get; init; } = [];
    public IReadOnlyList<MensajeConsolaWeb> Errores { get; init; } = [];
    public bool EsValido => Errores.Count == 0;
}

public sealed class EstadoEntornoGrafico
{
    public string Nombre { get; init; } = string.Empty;
    public string Plataforma { get; init; } = string.Empty;
    public bool Disponible { get; init; }
    public string Detalle { get; init; } = string.Empty;

    public string Resumen => $"{Nombre}: {(Disponible ? "Disponible" : "No disponible")} · {Detalle}";
}
