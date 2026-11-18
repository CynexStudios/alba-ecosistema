using Alba.Engine.Ast;
using Alba.Engine.Diagnostics;

namespace Alba.Engine.Runtime;

/// <summary>
/// Representa una función, método o constructor definido en código Alba con su closure léxico.
/// </summary>
public sealed class FuncionAlba
{
    public string Nombre { get; }
    public IReadOnlyList<ParametroAst> Parametros { get; }
    public AnotacionTipo? TipoRetorno { get; }
    public SentenciaBloque Cuerpo { get; }
    public Entorno Closure { get; }
    public DefinicionClase? ClasePropietaria { get; }
    public InstanciaAlba? InstanciaEnlazada { get; }

    public FuncionAlba(
        string nombre,
        IReadOnlyList<ParametroAst> parametros,
        AnotacionTipo? tipoRetorno,
        SentenciaBloque cuerpo,
        Entorno closure,
        DefinicionClase? clasePropietaria = null,
        InstanciaAlba? instanciaEnlazada = null)
    {
        Nombre = nombre;
        Parametros = parametros;
        TipoRetorno = tipoRetorno;
        Cuerpo = cuerpo;
        Closure = closure;
        ClasePropietaria = clasePropietaria;
        InstanciaEnlazada = instanciaEnlazada;
    }
}

/// <summary>
/// Representa una función integrada nativa del estándar de Alba.
/// </summary>
public sealed class FuncionNativaAlba
{
    public string Nombre { get; }
    private readonly Func<IReadOnlyList<ValorAlba>, UbicacionFuente, ValorAlba> _implementacion;

    public FuncionNativaAlba(
        string nombre,
        Func<IReadOnlyList<ValorAlba>, UbicacionFuente, ValorAlba> implementacion)
    {
        Nombre = nombre;
        _implementacion = implementacion;
    }

    public ValorAlba Invocar(IReadOnlyList<ValorAlba> argumentos, UbicacionFuente ubicacion) =>
        _implementacion(argumentos, ubicacion);
}
