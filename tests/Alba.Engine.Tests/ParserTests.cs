using Alba.Engine.Ast;
using Alba.Engine.Diagnostics;
using Alba.Engine.Parser;
using Xunit;

namespace Alba.Engine.Tests;

public sealed class ParserTests
{
    [Fact]
    public void DebeRespetarPrecedenciaYAsociatividadDeOperadores()
    {
        // 2 + 3 * 4 ^ 2 ^ 1  =>  2 + (3 * (4 ^ (2 ^ 1)))
        string codigo = "variable r = 2 + 3 * 4 ^ 2 ^ 1";
        var (lexico, sintactico) = AnalizadorSintactico.AnalizarCodigo(codigo, "precedencia.alb");

        Assert.False(lexico.TieneErrores);
        Assert.False(sintactico.TieneErrores);

        var decl = Assert.IsType<DeclVariable>(Assert.Single(sintactico.Programa.Sentencias));
        var suma = Assert.IsType<ExprBinaria>(decl.Inicializador);
        Assert.Equal(OperadorBinario.Suma, suma.Operador);

        var prod = Assert.IsType<ExprBinaria>(suma.Derecha);
        Assert.Equal(OperadorBinario.Multiplicacion, prod.Operador);

        var potIzq = Assert.IsType<ExprBinaria>(prod.Derecha);
        Assert.Equal(OperadorBinario.Potencia, potIzq.Operador);

        var potDer = Assert.IsType<ExprBinaria>(potIzq.Derecha);
        Assert.Equal(OperadorBinario.Potencia, potDer.Operador);
    }

    [Fact]
    public void DebeConstruirAstCompletoParaClaseConEncapsulamientoYErrores()
    {
        string codigo = """
            clase CuentaBancaria {
                publico titular: texto
                privado saldo: decimal

                constructor(titular: texto, saldo_inicial: decimal) {
                    este.titular = titular
                    este.saldo = saldo_inicial
                }

                publico funcion retirar(monto: decimal) -> decimal {
                    si monto > este.saldo {
                        lanzar "Fondos insuficientes."
                    }
                    este.saldo -= monto
                    retornar este.saldo
                }
            }

            intentar {
                variable cuenta = nuevo CuentaBancaria("Lucía", 150.0)
                imprimir(cuenta.retirar(25.0))
            } capturar (err) {
                imprimir(err)
            } finalmente {
                imprimir("Fin")
            }
            """;

        var (lexico, sintactico) = AnalizadorSintactico.AnalizarCodigo(codigo, "banco.alb");

        Assert.False(lexico.TieneErrores);
        Assert.False(sintactico.TieneErrores);
        Assert.Equal(2, sintactico.Programa.Sentencias.Count);

        var clase = Assert.IsType<DeclClase>(sintactico.Programa.Sentencias[0]);
        Assert.Equal("CuentaBancaria", clase.Nombre);
        Assert.Equal(2, clase.Campos.Count);
        Assert.Equal(ModificadorAcceso.Publico, clase.Campos[0].Acceso);
        Assert.Equal(ModificadorAcceso.Privado, clase.Campos[1].Acceso);
        Assert.NotNull(clase.Constructor);
        Assert.Single(clase.Metodos);
        Assert.Equal("decimal", clase.Metodos[0].TipoRetorno?.Nombre);

        var intentar = Assert.IsType<SentenciaIntentar>(sintactico.Programa.Sentencias[1]);
        Assert.NotNull(intentar.BloqueCapturar);
        Assert.Equal("err", intentar.BloqueCapturar!.NombreError);
        Assert.NotNull(intentar.BloqueFinalmente);
    }

    [Fact]
    public void DebeConstruirCondicionalesEncadenadosYParaEn()
    {
        string codigo = """
            para i en rango(1, 5) {
                si i == 2 {
                    continuar
                }
                sino_si i == 4 {
                    romper
                }
                sino {
                    imprimir(i)
                }
            }
            """;

        var (lexico, sintactico) = AnalizadorSintactico.AnalizarCodigo(codigo, "flujo.alb");

        Assert.False(lexico.TieneErrores);
        Assert.False(sintactico.TieneErrores);

        var para = Assert.IsType<SentenciaPara>(Assert.Single(sintactico.Programa.Sentencias));
        Assert.Equal("i", para.VariableIteracion);
        var si = Assert.IsType<SentenciaSi>(Assert.Single(para.Cuerpo.Sentencias));
        Assert.Single(si.RamasSinoSi);
        Assert.NotNull(si.BloqueSino);
    }

    [Fact]
    public void DebeReportarErrorSintacticoConFormatoExactoCuandoFaltaExpresionDespuesDeIgual()
    {
        string codigo = "variable resultado =";
        var (_, sintactico) = AnalizadorSintactico.AnalizarCodigo(codigo, "programa.alb");

        Assert.True(sintactico.TieneErrores);
        ErrorAlba error = Assert.Single(sintactico.Errores);
        Assert.Equal(TipoError.Sintactico, error.Tipo);
        Assert.Equal("ALB-S030", error.Codigo);
        Assert.Equal(1, error.Ubicacion.Linea);
        Assert.Equal(21, error.Ubicacion.Columna);

        string formateado = FormateadorErrores.Formatear(error);
        Assert.Contains("ERROR SINTÁCTICO", formateado);
        Assert.Contains("Archivo: programa.alb", formateado);
        Assert.Contains("variable resultado =", formateado);
        Assert.Contains("^", formateado);
    }

    [Fact]
    public void DebeRechazarRomperFueraDeCicloYConstanteSinInicializador()
    {
        string codigo = """
            constante PI: decimal
            romper
            """;

        var (_, sintactico) = AnalizadorSintactico.AnalizarCodigo(codigo, "invalidas.alb");

        Assert.True(sintactico.TieneErrores);
        Assert.Contains(sintactico.Errores, e => e.Codigo == "ALB-S003");
        Assert.Contains(sintactico.Errores, e => e.Codigo == "ALB-S026");
    }

    [Fact]
    public void DebeRecuperarseDeLlaveDeCierreSueltaEnNivelSuperior()
    {
        string codigo = "}\nvariable x = 10";
        var (lexico, sintactico) = AnalizadorSintactico.AnalizarCodigo(codigo, "llave_suelta.alb");

        Assert.False(lexico.TieneErrores);
        Assert.True(sintactico.TieneErrores);
        Assert.Contains(sintactico.Errores, e => e.Codigo == "ALB-S034");
        Assert.Contains(sintactico.Programa.Sentencias, s => s is DeclVariable d && d.Nombre == "x");
    }
}
