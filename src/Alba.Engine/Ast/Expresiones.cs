using Alba.Engine.Diagnostics;

namespace Alba.Engine.Ast;

public enum TipoLiteralAlba
{
    Entero,
    Decimal,
    Texto,
    Booleano,
    Nulo
}

public enum OperadorUnario
{
    NegacionAritmetica,
    NegacionLogica
}

public enum OperadorBinario
{
    Suma,
    Resta,
    Multiplicacion,
    Division,
    Modulo,
    Potencia,
    Igual,
    Diferente,
    Menor,
    MenorIgual,
    Mayor,
    MayorIgual,
    LogicoY,
    LogicoO
}

public sealed record ExprLiteral(
    object? Valor,
    TipoLiteralAlba TipoLiteral,
    UbicacionFuente Ubicacion
) : Expresion(Ubicacion);

public sealed record ExprIdentificador(
    string Nombre,
    UbicacionFuente Ubicacion
) : Expresion(Ubicacion);

public sealed record ExprEste(
    UbicacionFuente Ubicacion
) : Expresion(Ubicacion);

public sealed record ExprAgrupada(
    Expresion Interna,
    UbicacionFuente Ubicacion
) : Expresion(Ubicacion);

public sealed record ExprUnaria(
    OperadorUnario Operador,
    Expresion Operando,
    UbicacionFuente Ubicacion
) : Expresion(Ubicacion);

public sealed record ExprBinaria(
    Expresion Izquierda,
    OperadorBinario Operador,
    Expresion Derecha,
    UbicacionFuente Ubicacion
) : Expresion(Ubicacion);

public sealed record ExprLlamada(
    Expresion Objetivo,
    IReadOnlyList<Expresion> Argumentos,
    UbicacionFuente Ubicacion
) : Expresion(Ubicacion);

public sealed record ExprAccesoMiembro(
    Expresion Objeto,
    string NombreMiembro,
    UbicacionFuente Ubicacion
) : Expresion(Ubicacion);

public sealed record ExprIndice(
    Expresion Coleccion,
    Expresion Indice,
    UbicacionFuente Ubicacion
) : Expresion(Ubicacion);

public sealed record ExprLista(
    IReadOnlyList<Expresion> Elementos,
    UbicacionFuente Ubicacion
) : Expresion(Ubicacion);

public sealed record ExprNuevo(
    string NombreClase,
    IReadOnlyList<Expresion> Argumentos,
    UbicacionFuente Ubicacion
) : Expresion(Ubicacion);

public sealed record ExprLeer(
    Expresion? Mensaje,
    UbicacionFuente Ubicacion
) : Expresion(Ubicacion);
