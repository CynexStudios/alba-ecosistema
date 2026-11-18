using Alba.Engine.Diagnostics;

namespace Alba.Engine.Lexer;

/// <summary>
/// Unidad léxica producida por el analizador léxico con su ubicación exacta.
/// </summary>
public sealed record Token(
    TipoToken Tipo,
    string Lexema,
    object? ValorLiteral,
    UbicacionFuente Ubicacion
)
{
    public override string ToString() =>
        ValorLiteral is not null
            ? $"{Tipo}('{Lexema}', {ValorLiteral}) @ {Ubicacion}"
            : $"{Tipo}('{Lexema}') @ {Ubicacion}";
}
