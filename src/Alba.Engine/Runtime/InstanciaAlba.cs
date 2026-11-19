using Alba.Engine.Ast;
using Alba.Engine.Diagnostics;

namespace Alba.Engine.Runtime;

/// <summary>
/// Representa un objeto instanciado de una clase de Cibao/Alba, con campos, herencia y control de encapsulamiento.
/// </summary>
public sealed class InstanciaAlba
{
    private readonly Dictionary<string, SimboloAlba> _campos = new(StringComparer.Ordinal);
    public DefinicionClase Clase { get; }

    public InstanciaAlba(DefinicionClase clase)
    {
        Clase = clase;
    }

    internal void InicializarCampo(string nombre, ValorAlba valor, string? tipoRestriccion)
    {
        _campos[nombre] = new SimboloAlba(valor.AjustarParaAnotacion(tipoRestriccion), tipoRestriccion, false);
    }

    internal ValorAlba ObtenerMiembro(
        string nombre,
        DefinicionClase? claseActualEnEjecucion,
        UbicacionFuente ubicacion,
        string codigoFuente)
    {
        var campoEncontrado = BuscarCampoEnJerarquia(Clase, nombre);
        if (campoEncontrado is not null)
        {
            var (claseDecl, declCampo) = campoEncontrado.Value;
            ValidarAcceso(declCampo.Acceso, nombre, claseDecl, claseActualEnEjecucion, ubicacion, codigoFuente);
            return _campos[nombre].Valor;
        }

        var metodoEncontrado = BuscarMetodoEnJerarquia(Clase, nombre);
        if (metodoEncontrado is not null)
        {
            var (claseDecl, declMetodo) = metodoEncontrado.Value;
            ValidarAcceso(declMetodo.Acceso, nombre, claseDecl, claseActualEnEjecucion, ubicacion, codigoFuente);
            var metodoEnlazado = new FuncionAlba(
                declMetodo.Nombre,
                declMetodo.Parametros,
                declMetodo.TipoRetorno,
                declMetodo.Cuerpo,
                claseDecl.EntornoDefinicion,
                claseDecl,
                this
            );
            return ValorAlba.CrearFuncion(metodoEnlazado);
        }

        throw CrearError(
            "ALB-E009",
            TipoError.Ejecucion,
            ubicacion,
            $"La clase '{Clase.Nombre}' no posee ningún campo o método llamado '{nombre}'.",
            codigoFuente
        );
    }

    internal ValorAlba ObtenerMetodoSuper(
        DefinicionClase claseBase,
        string nombre,
        DefinicionClase? claseActualEnEjecucion,
        UbicacionFuente ubicacion,
        string codigoFuente)
    {
        var metodoEncontrado = BuscarMetodoEnJerarquia(claseBase, nombre);
        if (metodoEncontrado is not null)
        {
            var (claseDecl, declMetodo) = metodoEncontrado.Value;
            ValidarAcceso(declMetodo.Acceso, nombre, claseDecl, claseActualEnEjecucion, ubicacion, codigoFuente);
            var metodoEnlazado = new FuncionAlba(
                declMetodo.Nombre,
                declMetodo.Parametros,
                declMetodo.TipoRetorno,
                declMetodo.Cuerpo,
                claseDecl.EntornoDefinicion,
                claseDecl,
                this
            );
            return ValorAlba.CrearFuncion(metodoEnlazado);
        }

        throw CrearError(
            "ALB-E009",
            TipoError.Ejecucion,
            ubicacion,
            $"La clase base '{claseBase.Nombre}' no posee ningún método llamado '{nombre}'.",
            codigoFuente
        );
    }

    internal void AsignarCampo(
        string nombre,
        ValorAlba nuevoValor,
        DefinicionClase? claseActualEnEjecucion,
        UbicacionFuente ubicacion,
        string codigoFuente)
    {
        var campoEncontrado = BuscarCampoEnJerarquia(Clase, nombre);
        if (campoEncontrado is null)
        {
            throw CrearError(
                "ALB-E009",
                TipoError.Ejecucion,
                ubicacion,
                $"La clase '{Clase.Nombre}' no posee un campo llamado '{nombre}'.",
                codigoFuente
            );
        }

        var (claseDecl, declCampo) = campoEncontrado.Value;
        ValidarAcceso(declCampo.Acceso, nombre, claseDecl, claseActualEnEjecucion, ubicacion, codigoFuente);

        SimboloAlba simbolo = _campos[nombre];
        if (simbolo.TipoRestriccion is not null && !nuevoValor.EsCompatibleConAnotacion(simbolo.TipoRestriccion))
        {
            throw CrearError(
                "ALB-T001",
                TipoError.Tipo,
                ubicacion,
                $"No se puede asignar un valor de tipo '{nuevoValor.ObtenerNombreTipo()}' al campo '{nombre}' declarado como '{simbolo.TipoRestriccion}'.",
                codigoFuente
            );
        }

        simbolo.Valor = nuevoValor.AjustarParaAnotacion(simbolo.TipoRestriccion);
    }

    private static (DefinicionClase ClaseDecl, DeclCampoClase Campo)? BuscarCampoEnJerarquia(
        DefinicionClase inicio,
        string nombre)
    {
        DefinicionClase? actual = inicio;
        while (actual is not null)
        {
            if (actual.Campos.TryGetValue(nombre, out DeclCampoClase? campo))
            {
                return (actual, campo);
            }
            actual = actual.ClaseBase;
        }
        return null;
    }

    private static (DefinicionClase ClaseDecl, DeclMetodoClase Metodo)? BuscarMetodoEnJerarquia(
        DefinicionClase inicio,
        string nombre)
    {
        DefinicionClase? actual = inicio;
        while (actual is not null)
        {
            if (actual.Metodos.TryGetValue(nombre, out DeclMetodoClase? metodo))
            {
                return (actual, metodo);
            }
            actual = actual.ClaseBase;
        }
        return null;
    }

    private static void ValidarAcceso(
        ModificadorAcceso acceso,
        string nombreMiembro,
        DefinicionClase clasePropietaria,
        DefinicionClase? claseActualEnEjecucion,
        UbicacionFuente ubicacion,
        string codigoFuente)
    {
        if (acceso == ModificadorAcceso.Privado && !ReferenceEquals(claseActualEnEjecucion, clasePropietaria))
        {
            throw CrearError(
                "ALB-E010",
                TipoError.Ejecucion,
                ubicacion,
                $"Acceso denegado al miembro privado '{nombreMiembro}' de la clase '{clasePropietaria.Nombre}'.",
                codigoFuente
            );
        }
    }

    private static ExcepcionAlba CrearError(
        string codigo,
        TipoError tipo,
        UbicacionFuente ubicacion,
        string mensaje,
        string codigoFuente)
    {
        string linea = FormateadorErrores.ExtraerLinea(codigoFuente, ubicacion.Linea);
        return new ExcepcionAlba(new ErrorAlba(codigo, tipo, ubicacion, mensaje, linea));
    }
}
