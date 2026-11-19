using Alba.Engine.Diagnostics;
using Alba.Engine.Runtime;
using Xunit;

namespace Alba.Engine.Tests;

public sealed class ErroresTests
{
    [Fact]
    public void DebeRechazarAccesoExternoAMiembroPrivado()
    {
        string codigo = """
            clase Secreto {
                privado clave: entero = 1234
            }
            variable s = nuevo Secreto()
            imprimir(s.clave)
            """;

        ResultadoEjecucion res = EjecutorAlba.Ejecutar(codigo, "privacidad.alb");

        Assert.False(res.Exito);
        ErrorAlba err = Assert.Single(res.Errores);
        Assert.Equal(TipoError.Ejecucion, err.Tipo);
        Assert.Equal("ALB-E010", err.Codigo);
        Assert.Equal(5, err.Ubicacion.Linea);
    }

    [Fact]
    public void DebeRechazarAsignacionIncompatibleConAnotacionDeTipo()
    {
        string codigo = "variable edad: entero = \"veinte\"";
        ResultadoEjecucion res = EjecutorAlba.Ejecutar(codigo, "tipos.alb");

        Assert.False(res.Exito);
        ErrorAlba err = Assert.Single(res.Errores);
        Assert.Equal(TipoError.Tipo, err.Tipo);
        Assert.Equal("ALB-T001", err.Codigo);
    }

    [Fact]
    public void DebeImpedirReasignacionDeConstante()
    {
        string codigo = """
            constante PI = 3.14
            PI = 3.1416
            """;

        ResultadoEjecucion res = EjecutorAlba.Ejecutar(codigo, "constantes.alb");

        Assert.False(res.Exito);
        ErrorAlba err = Assert.Single(res.Errores);
        Assert.Equal("ALB-E002", err.Codigo);
    }

    [Fact]
    public void DebeDetectarDivisionPorCeroYPermitirCapturarla()
    {
        string codigo = """
            variable resultado = ""
            intentar {
                variable x = 10 / 0
            } capturar (e) {
                resultado = e
            }
            """;

        ResultadoEjecucion res = EjecutorAlba.Ejecutar(codigo, "divcero.alb");
        Assert.True(res.Exito);
    }

    [Fact]
    public void DebeReportarDivisionPorCeroNoCapturadaConFormatoEnEspanol()
    {
        string codigo = "variable x = 10 / 0";
        ResultadoEjecucion res = EjecutorAlba.Ejecutar(codigo, "divcero_sin_catch.alb");

        Assert.False(res.Exito);
        ErrorAlba err = Assert.Single(res.Errores);
        Assert.Equal("ALB-E005", err.Codigo);

        string texto = FormateadorErrores.Formatear(err);
        Assert.Contains("ERROR DE EJECUCIÓN", texto);
        Assert.Contains("División por cero", texto);
    }
}
