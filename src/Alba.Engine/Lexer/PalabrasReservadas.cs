using System.Collections.Frozen;

namespace Alba.Engine.Lexer;

/// <summary>
/// Tabla oficial de palabras reservadas del lenguaje Alba v0.1.0.
/// </summary>
public static class PalabrasReservadas
{
    private static readonly FrozenDictionary<string, TipoToken> Mapa =
        new Dictionary<string, TipoToken>(StringComparer.Ordinal)
        {
            // Declaraciones
            ["variable"] = TipoToken.Variable,
            ["constante"] = TipoToken.Constante,

            // Tipos
            ["entero"] = TipoToken.TipoEntero,
            ["decimal"] = TipoToken.TipoDecimal,
            ["texto"] = TipoToken.TipoTexto,
            ["booleano"] = TipoToken.TipoBooleano,
            ["lista"] = TipoToken.TipoLista,
            ["vacio"] = TipoToken.TipoVacio,

            // Funciones
            ["funcion"] = TipoToken.Funcion,
            ["retornar"] = TipoToken.Retornar,

            // Condicionales
            ["si"] = TipoToken.Si,
            ["sino"] = TipoToken.Sino,
            ["sino_si"] = TipoToken.SinoSi,

            // Ciclos
            ["mientras"] = TipoToken.Mientras,
            ["para"] = TipoToken.Para,
            ["en"] = TipoToken.En,
            ["romper"] = TipoToken.Romper,
            ["continuar"] = TipoToken.Continuar,

            // Clases y encapsulamiento
            ["clase"] = TipoToken.Clase,
            ["constructor"] = TipoToken.Constructor,
            ["nuevo"] = TipoToken.Nuevo,
            ["este"] = TipoToken.Este,
            ["publico"] = TipoToken.Publico,
            ["privado"] = TipoToken.Privado,

            // Manejo de errores
            ["intentar"] = TipoToken.Intentar,
            ["capturar"] = TipoToken.Capturar,
            ["finalmente"] = TipoToken.Finalmente,
            ["lanzar"] = TipoToken.Lanzar,

            // Entrada y salida
            ["imprimir"] = TipoToken.Imprimir,
            ["leer"] = TipoToken.Leer,

            // Valores y lógica
            ["verdadero"] = TipoToken.Verdadero,
            ["falso"] = TipoToken.Falso,
            ["nulo"] = TipoToken.Nulo,
            ["y"] = TipoToken.LogicoY,
            ["o"] = TipoToken.LogicoO,
            ["no"] = TipoToken.LogicoNo,

            // Reservadas futuras (Módulos)
            ["importar"] = TipoToken.Importar,
            ["desde"] = TipoToken.Desde,
            ["como"] = TipoToken.Como,
            ["modulo"] = TipoToken.Modulo,
            ["exportar"] = TipoToken.Exportar
        }.ToFrozenDictionary(StringComparer.Ordinal);

    public static bool TryObtenerTipo(string lexema, out TipoToken tipo) =>
        Mapa.TryGetValue(lexema, out tipo);

    public static IEnumerable<string> ObtenerTodas() => Mapa.Keys;
}
