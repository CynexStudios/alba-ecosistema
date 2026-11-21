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
    LogicoO,
    NuloCoalescente
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

public sealed record ExprSuper(
    UbicacionFuente Ubicacion
) : Expresion(Ubicacion);

public sealed record ExprComprobacionTipo(
    Expresion Valor,
    AnotacionTipo TipoEsperado,
    UbicacionFuente Ubicacion
) : Expresion(Ubicacion);

public sealed record EntradaDiccionario(
    Expresion Clave,
    Expresion Valor,
    UbicacionFuente Ubicacion
);

public sealed record ExprDiccionario(
    IReadOnlyList<EntradaDiccionario> Entradas,
    UbicacionFuente Ubicacion
) : Expresion(Ubicacion);

public sealed record ParteInterpolada(
    string? TextoLiteral,
    Expresion? Expresion
);

public sealed record ExprTextoInterpolado(
    IReadOnlyList<ParteInterpolada> Partes,
    UbicacionFuente Ubicacion
) : Expresion(Ubicacion);

public sealed record ExprFuncionAnonima(
    IReadOnlyList<ParametroAst> Parametros,
    AnotacionTipo? TipoRetorno,
    SentenciaBloque Cuerpo,
    UbicacionFuente Ubicacion
) : Expresion(Ubicacion);

public sealed record ExprRango(
    Expresion Inicio,
    Expresion Fin,
    bool Inclusivo,
    UbicacionFuente Ubicacion
) : Expresion(Ubicacion);

public sealed record ExprAccesoMiembroOpcional(
    Expresion Objeto,
    string NombreMiembro,
    UbicacionFuente Ubicacion
) : Expresion(Ubicacion);
