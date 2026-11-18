using Alba.Engine.Diagnostics;

namespace Alba.Engine.Ast;

/// <summary>
/// Nodo base de todo el Árbol de Sintaxis Abstracta (AST) de Alba.
/// </summary>
public abstract record NodoAst(UbicacionFuente Ubicacion);

/// <summary>
/// Nodo base para expresiones que producen un valor al evaluarse.
/// </summary>
public abstract record Expresion(UbicacionFuente Ubicacion) : NodoAst(Ubicacion);

/// <summary>
/// Nodo base para sentencias ejecutables.
/// </summary>
public abstract record Sentencia(UbicacionFuente Ubicacion) : NodoAst(Ubicacion);

/// <summary>
/// Nodo base para declaraciones (variables, constantes, funciones, clases).
/// </summary>
public abstract record Declaracion(UbicacionFuente Ubicacion) : Sentencia(Ubicacion);

/// <summary>
/// Raíz del AST correspondiente a un archivo fuente completo de Alba.
/// </summary>
public sealed record ProgramaAst(
    string Archivo,
    IReadOnlyList<Sentencia> Sentencias,
    UbicacionFuente Ubicacion
) : NodoAst(Ubicacion);

/// <summary>
/// Representa una anotación de tipo explícita (ej: entero, decimal, texto, Persona).
/// </summary>
public sealed record AnotacionTipo(
    string Nombre,
    UbicacionFuente Ubicacion
);

/// <summary>
/// Parámetro formal de una función, método o constructor.
/// </summary>
public sealed record ParametroAst(
    string Nombre,
    AnotacionTipo? Tipo,
    UbicacionFuente Ubicacion
);

/// <summary>
/// Visibilidad de miembros dentro de una clase.
/// </summary>
public enum ModificadorAcceso
{
    Publico,
    Privado
}
