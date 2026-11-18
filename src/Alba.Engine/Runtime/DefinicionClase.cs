using Alba.Engine.Ast;

namespace Alba.Engine.Runtime;

/// <summary>
/// Representa la definición en tiempo de ejecución de una clase de Alba.
/// </summary>
public sealed class DefinicionClase
{
    public string Nombre { get; }
    public IReadOnlyDictionary<string, DeclCampoClase> Campos { get; }
    public DeclConstructor? Constructor { get; }
    public IReadOnlyDictionary<string, DeclMetodoClase> Metodos { get; }
    public Entorno EntornoDefinicion { get; }

    public DefinicionClase(
        string nombre,
        IReadOnlyDictionary<string, DeclCampoClase> campos,
        DeclConstructor? constructor,
        IReadOnlyDictionary<string, DeclMetodoClase> metodos,
        Entorno entornoDefinicion)
    {
        Nombre = nombre;
        Campos = campos;
        Constructor = constructor;
        Metodos = metodos;
        EntornoDefinicion = entornoDefinicion;
    }
}
