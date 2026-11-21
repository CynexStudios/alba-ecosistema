using Alba.Engine.Diagnostics;
using Alba.Engine.Runtime;
using Xunit;

namespace Alba.Engine.Tests;

public sealed class Cibao9Tests
{
    private sealed class ConsolaCapturaIO : IConsolaIO
    {
        public List<string> Salidas { get; } = [];
        public void EscribirLinea(string texto) => Salidas.Add(texto);
        public void Escribir(string texto) => Salidas.Add(texto);
        public string? LeerLinea(string? indicador = null) => string.Empty;
    }

    [Fact]
    public void DebeSoportarRangosExclusivosInclusivosYDescendentes()
    {
        string codigo = """
            // Comentario estilo doble barra en Cibao 9.0
            variable exclus = 1..5
            variable inclus = 1..=5
            variable desc = 5..=3
            mostrar($"exclus={longitud(exclus)},inclus={longitud(inclus)},desc={unir(desc, '-')}")
            """;

        var consola = new ConsolaCapturaIO();
        ResultadoEjecucion res = EjecutorAlba.Ejecutar(codigo, "rangos.cibao", consola);

        Assert.True(res.Exito);
        Assert.Equal(new[] { "exclus=4,inclus=5,desc=5-4-3" }, consola.Salidas);
    }

    [Fact]
    public void DebeSoportarEncadenamientoOpcionalYLambdasFlecha()
    {
        string codigo = """
            variable usuario = { nombre: "Jaderson", perfil: { nivel: 9 } }
            variable vacioUsuario = nulo

            variable nombre = vacioUsuario?.nombre ?? "Invitado"
            variable nivel = usuario?.perfil?.nivel ?? 1
            variable sinCampo = usuario?.perfil?.inexistente ?? 99

            variable nums = 1..=5
            variable dobles = nums.mapear(x => x * 2)
            variable suma = nums.reducir(0, (acc, n) => acc + n)
            variable cuadro = funcion(n: entero): entero => n * n

            mostrar($"{nombre}|{nivel}|{sinCampo}|{unir(dobles, ',')}|{suma}|{cuadro(4)}")
            """;

        var consola = new ConsolaCapturaIO();
        ResultadoEjecucion res = EjecutorAlba.Ejecutar(codigo, "opcional_flecha.cibao", consola);

        Assert.True(res.Exito);
        Assert.Equal(new[] { "Invitado|9|99|2,4,6,8,10|15|16" }, consola.Salidas);
    }

    [Fact]
    public void DebeSoportarIncrementoDecrementoYCadenasMultilinea()
    {
        string codigoReal = """"
            variable cont = 10
            cont++
            cont++
            cont--
            variable textoMulti = """Hola
            Cibao 9.0"""
            mostrar(cont, contiene(textoMulti, "Cibao 9.0"))
            """";

        var consola = new ConsolaCapturaIO();
        ResultadoEjecucion res = EjecutorAlba.Ejecutar(codigoReal, "multi.cibao", consola);

        Assert.True(res.Exito);
        Assert.Equal(new[] { "11 verdadero" }, consola.Salidas);
    }

    [Fact]
    public void DebeSoportarModulosSeguridadColeccionesYMetodosNuevos()
    {
        string codigo = """
            importar "seguridad" como sec
            importar "colecciones" como col

            variable items = [3, 1, 3, 2, 5, 2]
            variable sinDup = items.unicos()
            variable primeroMayor = sinDup.encontrar(x => x > 3)
            variable hayCinco = sinDup.alguno(x => x == 5)
            variable todosPos = sinDup.todos(x => x > 0)
            variable porcion = sinDup.cortar(1, 3)

            variable hash = sec.sha256("cibao9")
            variable b64 = sec.base64_codificar("Hola Xynex")
            variable decod = sec.base64_decodificar(b64)
            variable id = sec.uuid()

            mostrar($"{unir(sinDup, ',')}|{primeroMayor}|{hayCinco}|{todosPos}|{unir(porcion, ',')}|{decod}|{longitud(hash)}|{longitud(id) > 10}|{es_lista(sinDup)}")
            """;

        var consola = new ConsolaCapturaIO();
        ResultadoEjecucion res = EjecutorAlba.Ejecutar(codigo, "modulos9.cibao", consola);

        Assert.True(res.Exito);
        Assert.Equal(new[] { "3,1,2,5|5|verdadero|verdadero|1,2|Hola Xynex|64|verdadero|verdadero" }, consola.Salidas);
    }
}
