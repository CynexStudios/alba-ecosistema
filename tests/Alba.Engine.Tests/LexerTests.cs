using Alba.Engine.Diagnostics;
using Alba.Engine.Lexer;
using Xunit;

namespace Alba.Engine.Tests;

public sealed class LexerTests
{
    [Fact]
    public void DebeReconocerPalabrasReservadasYTiposEnEspanol()
    {
        string codigo = "variable x: entero = 42\nconstante activo: booleano = verdadero";
        var lexer = new AnalizadorLexico(codigo, "prueba.alb");

        ResultadoLexico resultado = lexer.Analizar();

        Assert.False(resultado.TieneErrores);
        Assert.Equal(new[]
        {
            TipoToken.Variable,
            TipoToken.Identificador,
            TipoToken.DosPuntos,
            TipoToken.TipoEntero,
            TipoToken.Igual,
            TipoToken.EnteroLiteral,
            TipoToken.SaltoLinea,
            TipoToken.Constante,
            TipoToken.Identificador,
            TipoToken.DosPuntos,
            TipoToken.TipoBooleano,
            TipoToken.Igual,
            TipoToken.Verdadero,
            TipoToken.FinArchivo
        }, resultado.Tokens.Select(t => t.Tipo).ToArray());

        Assert.Equal(42L, resultado.Tokens[5].ValorLiteral);
        Assert.Equal(true, resultado.Tokens[12].ValorLiteral);
    }

    [Fact]
    public void DebeIgnorarComentariosDeUnaYVariasLineas()
    {
        string codigo = """
            # Comentario inicial
            variable a = 10 /* comentario
            multilinea */ + 5
            """;
        var lexer = new AnalizadorLexico(codigo, "comentarios.alb");

        ResultadoLexico resultado = lexer.Analizar();

        Assert.False(resultado.TieneErrores);
        Assert.Equal(new[]
        {
            TipoToken.Variable,
            TipoToken.Identificador,
            TipoToken.Igual,
            TipoToken.EnteroLiteral,
            TipoToken.Mas,
            TipoToken.EnteroLiteral,
            TipoToken.FinArchivo
        }, resultado.Tokens.Select(t => t.Tipo).ToArray());

        // Verifica que la línea después del comentario multilínea es exacta (línea 3)
        Token mas = resultado.Tokens[4];
        Assert.Equal(3, mas.Ubicacion.Linea);
    }

    [Fact]
    public void DebeProcesarCadenasConSecuenciasDeEscape()
    {
        string codigo = "\"Hola\\n\\\"Alba\\\"\\t2026\"";
        var lexer = new AnalizadorLexico(codigo, "texto.alb");

        ResultadoLexico resultado = lexer.Analizar();

        Assert.False(resultado.TieneErrores);
        Assert.Equal(TipoToken.TextoLiteral, resultado.Tokens[0].Tipo);
        Assert.Equal("Hola\n\"Alba\"\t2026", resultado.Tokens[0].ValorLiteral);
    }

    [Fact]
    public void DebeSuprimirSaltosDeLineaDentroDeParentesisYCorchetes()
    {
        string codigo = """
            variable lista_num = [
                1,
                2,
                3
            ]
            """;
        var lexer = new AnalizadorLexico(codigo, "listas.alb");

        ResultadoLexico resultado = lexer.Analizar();

        Assert.False(resultado.TieneErrores);
        Assert.DoesNotContain(resultado.Tokens, t => t.Tipo == TipoToken.SaltoLinea);
    }

    [Fact]
    public void DebeDetectarCadenaSinCerrarConUbicacionExacta()
    {
        string codigo = "variable saludo = \"Hola mundo";
        var lexer = new AnalizadorLexico(codigo, "error_texto.alb");

        ResultadoLexico resultado = lexer.Analizar();

        Assert.True(resultado.TieneErrores);
        ErrorAlba error = Assert.Single(resultado.Errores);
        Assert.Equal(TipoError.Lexico, error.Tipo);
        Assert.Equal("ALB-L001", error.Codigo);
        Assert.Equal(1, error.Ubicacion.Linea);
        Assert.Equal(19, error.Ubicacion.Columna);
    }

    [Fact]
    public void DebeDetectarOperadorLogicoInvalidoYSugerirSintaxisAlba()
    {
        string codigo = "si !activo { imprimir(1) }";
        var lexer = new AnalizadorLexico(codigo, "negacion.alb");

        ResultadoLexico resultado = lexer.Analizar();

        Assert.True(resultado.TieneErrores);
        ErrorAlba error = Assert.Single(resultado.Errores);
        Assert.Equal("ALB-L002", error.Codigo);
        Assert.Contains("no", error.Mensaje);
    }
}
