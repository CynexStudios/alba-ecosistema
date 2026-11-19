using Alba.Engine.Ast;

namespace Alba.Engine.Runtime;

/// <summary>
/// Representa la definición en tiempo de ejecución de una clase de Cibao/Alba.
/// Soporta herencia simple mediante <see cref="ClaseBase"/>.
/// </summary>
public sealed class DefinicionClase
{
    public string Nombre { get; }
    public IReadOnlyDictionary<string, DeclCampoClase> Campos { get; }
    public DeclConstructor? Constructor { get; }
    public IReadOnlyDictionary<string, DeclMetodoClase> Metodos { get; }
    public Entorno EntornoDefinicion { get; }
    public DefinicionClase? ClaseBase { get; }

    public DefinicionClase(
        string nombre,
        IReadOnlyDictionary<string, DeclCampoClase> campos,
        DeclConstructor? constructor,
        IReadOnlyDictionary<string, DeclMetodoClase> metodos,
        Entorno entornoDefinicion,
        DefinicionClase? claseBase = null)
    {
        Nombre = nombre;
        Campos = campos;
        Constructor = constructor;
        Metodos = metodos;
        EntornoDefinicion = entornoDefinicion;
        ClaseBase = claseBase;
    }

    public bool EsIgualOSubclaseDe(DefinicionClase otra)
    {
        DefinicionClase? actual = this;
        while (actual is not null)
        {
            if (ReferenceEquals(actual, otra))
            {
                return true;
            }
            actual = actual.ClaseBase;
        }
        return false;
    }

    public bool HeredaDe(string nombreClase)
    {
        DefinicionClase? actual = this;
        while (actual is not null)
        {
            if (string.Equals(actual.Nombre, nombreClase, StringComparison.Ordinal))
            {
                return true;
            }
            actual = actual.ClaseBase;
        }
        return false;
    }
}

/// <summary>
/// Representa un módulo importado en Cibao con su espacio de nombres y símbolos exportados.
/// </summary>
public sealed class ModuloAlba
{
    public string Nombre { get; }
    public IReadOnlyDictionary<string, ValorAlba> Exportaciones { get; }

    public ModuloAlba(string nombre, IReadOnlyDictionary<string, ValorAlba> exportaciones)
    {
        Nombre = nombre;
        Exportaciones = exportaciones;
    }
}
