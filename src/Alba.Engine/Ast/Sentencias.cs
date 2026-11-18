using Alba.Engine.Diagnostics;

namespace Alba.Engine.Ast;

public enum OperadorAsignacion
{
    Simple,
    Suma,
    Resta,
    Multiplicacion,
    Division
}

public sealed record SentenciaBloque(
    IReadOnlyList<Sentencia> Sentencias,
    UbicacionFuente Ubicacion
) : Sentencia(Ubicacion);

public sealed record SentenciaExpresion(
    Expresion Expresion,
    UbicacionFuente Ubicacion
) : Sentencia(Ubicacion);

public sealed record SentenciaAsignacion(
    Expresion Objetivo,
    OperadorAsignacion Operador,
    Expresion Valor,
    UbicacionFuente Ubicacion
) : Sentencia(Ubicacion);

public sealed record RamaSinoSi(
    Expresion Condicion,
    SentenciaBloque Bloque,
    UbicacionFuente Ubicacion
);

public sealed record SentenciaSi(
    Expresion Condicion,
    SentenciaBloque BloqueEntonces,
    IReadOnlyList<RamaSinoSi> RamasSinoSi,
    SentenciaBloque? BloqueSino,
    UbicacionFuente Ubicacion
) : Sentencia(Ubicacion);

public sealed record SentenciaMientras(
    Expresion Condicion,
    SentenciaBloque Cuerpo,
    UbicacionFuente Ubicacion
) : Sentencia(Ubicacion);

public sealed record SentenciaPara(
    string VariableIteracion,
    Expresion Iterable,
    SentenciaBloque Cuerpo,
    UbicacionFuente Ubicacion
) : Sentencia(Ubicacion);

public sealed record SentenciaRetornar(
    Expresion? Valor,
    UbicacionFuente Ubicacion
) : Sentencia(Ubicacion);

public sealed record SentenciaRomper(
    UbicacionFuente Ubicacion
) : Sentencia(Ubicacion);

public sealed record SentenciaContinuar(
    UbicacionFuente Ubicacion
) : Sentencia(Ubicacion);

public sealed record BloqueCapturar(
    string? NombreError,
    AnotacionTipo? TipoError,
    SentenciaBloque Bloque,
    UbicacionFuente Ubicacion
);

public sealed record SentenciaIntentar(
    SentenciaBloque BloqueIntentar,
    BloqueCapturar? BloqueCapturar,
    SentenciaBloque? BloqueFinalmente,
    UbicacionFuente Ubicacion
) : Sentencia(Ubicacion);

public sealed record SentenciaLanzar(
    Expresion Expresion,
    UbicacionFuente Ubicacion
) : Sentencia(Ubicacion);

public sealed record SentenciaImprimir(
    IReadOnlyList<Expresion> Argumentos,
    UbicacionFuente Ubicacion
) : Sentencia(Ubicacion);
