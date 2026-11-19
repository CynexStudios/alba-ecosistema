namespace Alba.Engine.Diagnostics;

/// <summary>
/// Representa un diagnóstico estructurado producido por el Lexer, Parser o Intérprete.
/// </summary>
public sealed record ErrorAlba(
    string Codigo,
    TipoError Tipo,
    UbicacionFuente Ubicacion,
    string Mensaje,
    string LineaCodigo = ""
);

/// <summary>
/// Excepción controlada del motor Alba que encapsula un diagnóstico estructurado.
/// </summary>
public sealed class ExcepcionAlba : Exception
{
    public ErrorAlba Error { get; }

    public ExcepcionAlba(ErrorAlba error)
        : base(error.Mensaje)
    {
        Error = error;
    }
}
