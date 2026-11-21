namespace Alba.Engine.Lexer;

/// <summary>
/// Todos los tipos de tokens reconocidos por el analizador léxico de Alba v0.1.0.
/// </summary>
public enum TipoToken
{
    // Literales e identificadores
    Identificador,
    EnteroLiteral,
    DecimalLiteral,
    TextoLiteral,

    // Palabras reservadas: Declaraciones y tipos
    Variable,
    Constante,
    TipoEntero,
    TipoDecimal,
    TipoTexto,
    TipoBooleano,
    TipoLista,
    TipoDiccionario,
    TipoVacio,

    // Palabras reservadas: Funciones y control
    Funcion,
    Retornar,
    Si,
    Sino,
    SinoSi,
    Elegir,
    Caso,
    Defecto,
    Mientras,
    Para,
    En,
    Romper,
    Continuar,

    // Palabras reservadas: Clases y objetos
    Clase,
    Extiende,
    Constructor,
    Nuevo,
    Este,
    Super,
    Publico,
    Privado,

    // Palabras reservadas: Manejo de errores
    Intentar,
    Capturar,
    Finalmente,
    Lanzar,

    // Palabras reservadas: Entrada y salida
    Imprimir,
    Leer,

    // Palabras reservadas: Valores y operadores lógicos
    Verdadero,
    Falso,
    Nulo,
    LogicoY,
    LogicoO,
    LogicoNo,
    Es,

    // Palabras reservadas: Módulos
    Importar,
    Desde,
    Como,
    Modulo,
    Exportar,

    // Literales de texto interpolado ($"...")
    TextoInterpInicio,
    TextoInterpMedio,
    TextoInterpFin,

    // Operadores aritméticos
    Mas,
    Menos,
    Estrella,
    Barra,
    Porcentaje,
    Potencia,

    // Operadores de asignación
    Igual,
    MasIgual,
    MenosIgual,
    EstrellaIgual,
    BarraIgual,

    // Operadores de comparación y especiales
    IgualIgual,
    Diferente,
    Menor,
    MenorIgual,
    Mayor,
    MayorIgual,
    NuloCoalescente,
    InterrogacionPunto,
    MasMas,
    MenosMenos,
    PuntoPunto,
    PuntoPuntoIgual,

    // Delimitadores y puntuación
    ParentesisIzq,
    ParentesisDer,
    LlaveIzq,
    LlaveDer,
    CorcheteIzq,
    CorcheteDer,
    Coma,
    Punto,
    DosPuntos,
    PuntoYComa,
    Flecha,
    FlechaGorda,

    // Control de estructura y fin de flujo
    SaltoLinea,
    FinArchivo
}
