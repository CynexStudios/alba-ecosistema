using Alba.Engine.Diagnostics;
using Alba.Engine.Lexer;
using Alba.Engine.Parser;

namespace Alba.Engine.Runtime;

/// <summary>
/// Resultado completo de ejecutar un programa Alba (análisis léxico, sintáctico y ejecución).
/// </summary>
public sealed record ResultadoEjecucion(
    bool Exito,
    IReadOnlyList<ErrorAlba> Errores,
    ValorAlba UltimoValor
);

/// <summary>
/// Fachada oficial de alto nivel que integra Lexer, Parser e Intérprete de Alba.
/// </summary>
public static class EjecutorAlba
{
    public static ResultadoEjecucion Ejecutar(
        string codigoFuente,
        string archivo = "<entrada>",
        IConsolaIO? consola = null)
    {
        var (lexico, sintactico) = AnalizadorSintactico.AnalizarCodigo(codigoFuente, archivo);

        if (lexico.TieneErrores || sintactico.TieneErrores)
        {
            var errores = new List<ErrorAlba>(lexico.Errores.Count + sintactico.Errores.Count);
            errores.AddRange(lexico.Errores);
            errores.AddRange(sintactico.Errores);
            return new ResultadoEjecucion(false, errores, ValorAlba.Nulo);
        }

        IConsolaIO io = consola ?? new ConsolaNulaIO();
        var interprete = new Interprete(io, codigoFuente);

        try
        {
            ValorAlba ultimo = interprete.Ejecutar(sintactico.Programa);
            return new ResultadoEjecucion(true, Array.Empty<ErrorAlba>(), ultimo);
        }
        catch (ExcepcionAlba ex)
        {
            return new ResultadoEjecucion(false, new[] { ex.Error }, ValorAlba.Nulo);
        }
        catch (ExcepcionLanzadaUsuario exUsuario)
        {
            string linea = FormateadorErrores.ExtraerLinea(codigoFuente, exUsuario.Ubicacion.Linea);
            var error = new ErrorAlba(
                "ALB-E090",
                TipoError.Ejecucion,
                exUsuario.Ubicacion,
                $"Excepción no capturada: {exUsuario.ValorLanzado.ATextoParaImprimir()}",
                linea
            );
            return new ResultadoEjecucion(false, new[] { error }, ValorAlba.Nulo);
        }
    }

    private sealed class ConsolaNulaIO : IConsolaIO
    {
        public void EscribirLinea(string texto) { }
        public void Escribir(string texto) { }
        public string? LeerLinea(string? indicador = null) => string.Empty;
    }
}
