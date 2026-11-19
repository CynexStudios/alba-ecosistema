using Alba.Engine.Diagnostics;

namespace Alba.Engine.Ast;

public sealed record DeclVariable(
    string Nombre,
    AnotacionTipo? Tipo,
    Expresion? Inicializador,
    UbicacionFuente Ubicacion
) : Declaracion(Ubicacion);

public sealed record DeclConstante(
    string Nombre,
    AnotacionTipo? Tipo,
    Expresion Valor,
    UbicacionFuente Ubicacion
) : Declaracion(Ubicacion);

public sealed record DeclFuncion(
    string Nombre,
    IReadOnlyList<ParametroAst> Parametros,
    AnotacionTipo? TipoRetorno,
    SentenciaBloque Cuerpo,
    UbicacionFuente Ubicacion
) : Declaracion(Ubicacion);

public sealed record DeclCampoClase(
    string Nombre,
    ModificadorAcceso Acceso,
    AnotacionTipo? Tipo,
    Expresion? Inicializador,
    UbicacionFuente Ubicacion
) : NodoAst(Ubicacion);

public sealed record DeclConstructor(
    ModificadorAcceso Acceso,
    IReadOnlyList<ParametroAst> Parametros,
    SentenciaBloque Cuerpo,
    UbicacionFuente Ubicacion
) : NodoAst(Ubicacion);

public sealed record DeclMetodoClase(
    string Nombre,
    ModificadorAcceso Acceso,
    IReadOnlyList<ParametroAst> Parametros,
    AnotacionTipo? TipoRetorno,
    SentenciaBloque Cuerpo,
    UbicacionFuente Ubicacion
) : NodoAst(Ubicacion);

public sealed record DeclClase(
    string Nombre,
    IReadOnlyList<DeclCampoClase> Campos,
    DeclConstructor? Constructor,
    IReadOnlyList<DeclMetodoClase> Metodos,
    UbicacionFuente Ubicacion,
    string? NombreClaseBase = null
) : Declaracion(Ubicacion);

/// <summary>
/// Declaración de importación modular (`importar "ruta.cibao" como alias`).
/// </summary>
public sealed record DeclImportarFutura(
    string RutaModulo,
    string? Alias,
    UbicacionFuente Ubicacion
) : Declaracion(Ubicacion);
