using Alba.Engine.Runtime;
using Xunit;

namespace Alba.Engine.Tests;

public sealed class InterpreteTests
{
    private sealed class ConsolaMemoriaIO : IConsolaIO
    {
        private readonly Queue<string> _entradas;
        public List<string> Salidas { get; } = [];

        public ConsolaMemoriaIO(params string[] entradas)
        {
            _entradas = new Queue<string>(entradas);
        }

        public void EscribirLinea(string texto) => Salidas.Add(texto);
        public void Escribir(string texto) => Salidas.Add(texto);

        public string? LeerLinea(string? indicador = null)
        {
            if (!string.IsNullOrEmpty(indicador))
            {
                Salidas.Add(indicador);
            }
            return _entradas.Count > 0 ? _entradas.Dequeue() : string.Empty;
        }
    }

    [Fact]
    public void DebeEjecutarVariablesConstantesOperadoresYEntradaSalida()
    {
        string codigo = """
            constante FACTOR = 2
            variable nombre = leer("Nombre: ")
            variable base: decimal = 5
            base += 2.5
            variable total = base * FACTOR
            imprimir("Hola", nombre, total)
            """;

        var consola = new ConsolaMemoriaIO("Carlos");
        ResultadoEjecucion res = EjecutorAlba.Ejecutar(codigo, "basico.alb", consola);

        Assert.True(res.Exito);
        Assert.Equal(new[] { "Nombre: ", "Hola Carlos 15.0" }, consola.Salidas);
    }

    [Fact]
    public void DebeEvaluarCondicionalesCiclosRomperYContinuar()
    {
        string codigo = """
            variable suma = 0
            para i en rango(1, 10) {
                si i % 2 != 0 {
                    continuar
                }
                si i > 6 {
                    romper
                }
                suma += i
            }
            imprimir(suma)
            """;

        var consola = new ConsolaMemoriaIO();
        ResultadoEjecucion res = EjecutorAlba.Ejecutar(codigo, "ciclos.alb", consola);

        Assert.True(res.Exito);
        // Pares <= 6: 2 + 4 + 6 = 12
        Assert.Equal(new[] { "12" }, consola.Salidas);
    }

    [Fact]
    public void DebeSoportarFuncionesRecursivasYClosures()
    {
        string codigo = """
            funcion crear_contador(inicio: entero) {
                variable cuenta = inicio
                funcion incrementar() -> entero {
                    cuenta += 1
                    retornar cuenta
                }
                retornar incrementar
            }

            funcion factorial(n: entero) -> entero {
                si n <= 1 {
                    retornar 1
                }
                retornar n * factorial(n - 1)
            }

            variable c = crear_contador(10)
            imprimir(c())
            imprimir(c())
            imprimir(factorial(5))
            """;

        var consola = new ConsolaMemoriaIO();
        ResultadoEjecucion res = EjecutorAlba.Ejecutar(codigo, "funciones.alb", consola);

        Assert.True(res.Exito);
        Assert.Equal(new[] { "11", "12", "120" }, consola.Salidas);
    }

    [Fact]
    public void DebeManipularListasAccesoYModificacion()
    {
        string codigo = """
            variable items: lista = [10, 20, 30]
            items[1] = 25
            agregar(items, 40)
            variable quitado = eliminar(items, 0)
            imprimir(quitado, longitud(items), items)
            """;

        var consola = new ConsolaMemoriaIO();
        ResultadoEjecucion res = EjecutorAlba.Ejecutar(codigo, "listas.alb", consola);

        Assert.True(res.Exito);
        Assert.Equal(new[] { "10 3 [25, 30, 40]" }, consola.Salidas);
    }

    [Fact]
    public void DebeEjecutarClasesConEncapsulamientoEsteYIntentarCapturarFinalmente()
    {
        string codigo = """
            clase Cuenta {
                publico titular: texto
                privado saldo: decimal

                constructor(titular: texto, saldo: decimal) {
                    este.titular = titular
                    este.saldo = saldo
                }

                publico funcion retirar(monto: decimal) -> decimal {
                    si monto > este.saldo {
                        lanzar "Fondos insuficientes"
                    }
                    este.saldo -= monto
                    retornar este.saldo
                }
            }

            intentar {
                variable c = nuevo Cuenta("Ana", 100.0)
                imprimir(c.titular, c.retirar(40.0))
                c.retirar(80.0)
            } capturar (err) {
                imprimir("Capturado:", err)
            } finalmente {
                imprimir("Cierre")
            }
            """;

        var consola = new ConsolaMemoriaIO();
        ResultadoEjecucion res = EjecutorAlba.Ejecutar(codigo, "clases.alb", consola);

        Assert.True(res.Exito);
        Assert.Equal(new[] { "Ana 60.0", "Capturado: Fondos insuficientes", "Cierre" }, consola.Salidas);
    }

    [Fact]
    public void DebeAplicarCortocircuitoEnOperadoresLogicos()
    {
        string codigo = """
            funcion explotar() -> booleano {
                lanzar "No debió evaluarse"
            }

            si falso y explotar() {
                imprimir("inaccesible")
            }
            si verdadero o explotar() {
                imprimir("ok")
            }
            """;

        var consola = new ConsolaMemoriaIO();
        ResultadoEjecucion res = EjecutorAlba.Ejecutar(codigo, "cortocircuito.alb", consola);

        Assert.True(res.Exito);
        Assert.Equal(new[] { "ok" }, consola.Salidas);
    }
}
