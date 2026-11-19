namespace Alba.IDE.Models;

public enum TipoMensajeConsola
{
    Salida,
    Error,
    Advertencia,
    Sistema,
    Entrada
}

public sealed record MensajeConsola(
    TipoMensajeConsola Tipo,
    string Texto,
    DateTime MarcaTiempo
)
{
    public string ColorHex => Tipo switch
    {
        TipoMensajeConsola.Salida => "#E2E8F0",
        TipoMensajeConsola.Error => "#F87171",
        TipoMensajeConsola.Advertencia => "#FBBF24",
        TipoMensajeConsola.Sistema => "#60A5FA",
        TipoMensajeConsola.Entrada => "#34D399",
        _ => "#E2E8F0"
    };

    public string Prefijo => Tipo switch
    {
        TipoMensajeConsola.Error => "[ERROR] ",
        TipoMensajeConsola.Advertencia => "[AVISO] ",
        TipoMensajeConsola.Sistema => "[ALBA] ",
        TipoMensajeConsola.Entrada => "> ",
        _ => string.Empty
    };
}
