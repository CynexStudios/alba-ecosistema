using Alba.Engine.Diagnostics;
using Alba.Engine.Runtime;
using Xunit;

namespace Alba.Engine.Tests;

public sealed class Cibao5EvolucionTests
{
    private sealed class ConsolaCapturaIO : IConsolaIO
    {
        public List<string> Salidas { get; } = [];
        public void EscribirLinea(string texto) => Salidas.Add(texto);
        public void Escribir(string texto) => Salidas.Add(texto);
        public string? LeerLinea(string? indicador = null) => string.Empty;
    }

    [Fact]
    public void DebeSoportarDiccionariosLiteralesAccesoIteracionYJson()
    {
        string codigo = """
            variable perfil: diccionario = {
                nombre: "Ana",
                "edad": 25,
                activo: verdadero
            }
            perfil["ciudad"] = "Santiago"
            perfil.edad += 1

            variable listaClaves = []
            para k en perfil {
                agregar(listaClaves, k)
            }

            variable serializado = json_a_texto(perfil)
            variable restaurado = texto_a_json(serializado)

            imprimir(perfil["nombre"], perfil.edad, restaurado.ciudad, longitud(listaClaves))
            """;

        var consola = new ConsolaCapturaIO();
        ResultadoEjecucion res = EjecutorAlba.Ejecutar(codigo, "diccionarios.cibao", consola);

        Assert.True(res.Exito);
        Assert.Equal(new[] { "Ana 26 Santiago 4" }, consola.Salidas);
    }

    [Fact]
    public void DebeEvaluarInterpolacionDeTextoYEscapesCorrectamente()
    {
        string codigo = """
            variable x = 7
            variable y = 3
            variable mapa = { titulo: "Cibao" }
            variable sinLlaves = $"Texto plano"
            variable complejo = $"Proyecto {mapa.titulo}: \{ {x} + {y} = {x + y} \}"
            imprimir(sinLlaves)
            imprimir(complejo)
            """;

        var consola = new ConsolaCapturaIO();
        ResultadoEjecucion res = EjecutorAlba.Ejecutar(codigo, "interpolacion.cibao", consola);

        Assert.True(res.Exito);
        Assert.Equal(new[]
        {
            "Texto plano",
            "Proyecto Cibao: { 7 + 3 = 10 }"
        }, consola.Salidas);
    }

    [Fact]
    public void DebeSoportarHerenciaSuperPolimorfismoYPrivacidad()
    {
        string codigo = """
            clase Animal {
                publico nombre: texto
                privado codigoInterno: entero = 99

                constructor(nombre: texto) {
                    este.nombre = nombre
                }

                publico funcion hablar() -> texto {
                    retornar $"Soy {este.nombre} (#{este.codigoInterno})"
                }
            }

            clase Perro extiende Animal {
                publico raza: texto

                constructor(nombre: texto, raza: texto) {
                    super(nombre)
                    este.raza = raza
                }

                publico funcion hablar() -> texto {
                    retornar $"{super.hablar()} de raza {este.raza}"
                }
            }

            variable a: Animal = nuevo Perro("Max", "Pastor")
            imprimir(a.hablar())
            imprimir(a es Animal, a es Perro, nulo es Animal, nulo es nulo)
            """;

        var consola = new ConsolaCapturaIO();
        ResultadoEjecucion res = EjecutorAlba.Ejecutar(codigo, "herencia.cibao", consola);

        Assert.True(res.Exito);
        Assert.Equal(new[]
        {
            "Soy Max (#99) de raza Pastor",
            "verdadero verdadero falso verdadero"
        }, consola.Salidas);
    }

    [Fact]
    public void DebeProtegerCamposPrivadosDeClaseBaseEnSubclases()
    {
        string codigo = """
            clase Base {
                privado secreto: entero = 42
            }
            clase Hija extiende Base {
                publico funcion intentarLeer() -> entero {
                    retornar este.secreto
                }
            }
            variable h = nuevo Hija()
            imprimir(h.intentarLeer())
            """;

        ResultadoEjecucion res = EjecutorAlba.Ejecutar(codigo, "privacidad_herencia.cibao");
        Assert.False(res.Exito);
        ErrorAlba err = Assert.Single(res.Errores);
        Assert.Equal("ALB-E010", err.Codigo);
    }

    [Fact]
    public void DebeEjecutarElegirSinFallThroughLambdasYMétodosPorPunto()
    {
        string codigo = """
            funcion clasificar(n: entero) -> texto {
                elegir n {
                    caso 1, 2:
                        retornar "bajo"
                    caso 3:
                        retornar "medio"
                    defecto:
                        retornar "alto"
                }
            }

            variable fallback = nulo ?? 100
            variable nums = [5, 2, 8, 1, 4]
            variable salida = nums
                .ordenar()
                .filtrar(funcion(v) { retornar v >= 4 })
                .mapear(funcion(v) { retornar v * 2 })

            variable textoTrim = "  Hola Cibao  ".recortar().mayusculas()
            imprimir(clasificar(2), clasificar(3), clasificar(9), fallback, salida, textoTrim)
            """;

        var consola = new ConsolaCapturaIO();
        ResultadoEjecucion res = EjecutorAlba.Ejecutar(codigo, "control_y_lambdas.cibao", consola);

        Assert.True(res.Exito);
        Assert.Equal(new[] { "bajo medio alto 100 [8, 10, 16] HOLA CIBAO" }, consola.Salidas);
    }

    [Fact]
    public void DebeImportarModulosRealesYDetectarCiclos()
    {
        string dirTemp = Path.Combine(Path.GetTempPath(), $"cibao_mod_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dirTemp);

        try
        {
            string modMat = Path.Combine(dirTemp, "aritmetica.cibao");
            string main = Path.Combine(dirTemp, "principal.cibao");

            File.WriteAllText(modMat, """
                constante FACTOR = 3
                funcion triplicar(n: entero) -> entero {
                    retornar n * FACTOR
                }
                """);

            string codigoMain = """
                importar "aritmetica" como arit
                importar "matematicas" como mat
                imprimir(arit.triplicar(4), mat.raiz(25))
                """;
            File.WriteAllText(main, codigoMain);

            var consola = new ConsolaCapturaIO();
            ResultadoEjecucion res = EjecutorAlba.Ejecutar(codigoMain, main, consola);

            Assert.True(res.Exito);
            Assert.Equal(new[] { "12 5.0" }, consola.Salidas);

            // Verificación de ciclo circular
            string cicloA = Path.Combine(dirTemp, "a.cibao");
            string cicloB = Path.Combine(dirTemp, "b.cibao");
            string srcA = """
                importar "b.cibao" como mb
                variable x = 1
                """;
            File.WriteAllText(cicloA, srcA);
            File.WriteAllText(cicloB, """
                importar "a.cibao" como ma
                variable y = 2
                """);

            ResultadoEjecucion resCiclo = EjecutorAlba.Ejecutar(srcA, cicloA);
            Assert.False(resCiclo.Exito);
            ErrorAlba err = Assert.Single(resCiclo.Errores);
            Assert.Equal("ALB-E098", err.Codigo);
        }
        finally
        {
            try { Directory.Delete(dirTemp, recursive: true); } catch { }
        }
    }

    [Fact]
    public void DebeLeerYEscribirArchivosDeFormaSegura()
    {
        string dirTemp = Path.Combine(Path.GetTempPath(), $"cibao_io_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dirTemp);

        try
        {
            string scriptPath = Path.Combine(dirTemp, "io.cibao");
            string codigo = """
                escribir_archivo("datos.txt", "Linea 1\n")
                agregar_linea_archivo("datos.txt", "Linea 2")
                variable existe = existe_archivo("datos.txt")
                variable contenido = leer_archivo("datos.txt")
                imprimir(existe, contiene(contenido, "Linea 2"))
                """;

            var consola = new ConsolaCapturaIO();
            ResultadoEjecucion res = EjecutorAlba.Ejecutar(codigo, scriptPath, consola);

            Assert.True(res.Exito);
            Assert.Equal(new[] { "verdadero verdadero" }, consola.Salidas);
        }
        finally
        {
            try { Directory.Delete(dirTemp, recursive: true); } catch { }
        }
    }
}
