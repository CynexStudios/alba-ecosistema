using Alba.Engine.Ast;
using Alba.Engine.Diagnostics;

namespace Alba.Engine.Runtime;

/// <summary>
/// Representa un objeto instanciado de una clase de Alba, con campos y control de encapsulamiento.
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
        if (Clase.Campos.TryGetValue(nombre, out DeclCampoClase? declCampo))
        {
            ValidarAcceso(declCampo.Acceso, nombre, claseActualEnEjecucion, ubicacion, codigoFuente);
            return _campos[nombre].Valor;
        }

        if (Clase.Metodos.TryGetValue(nombre, out DeclMetodoClase? declMetodo))
        {
            ValidarAcceso(declMetodo.Acceso, nombre, claseActualEnEjecucion, ubicacion, codigoFuente);
            var metodoEnlazado = new FuncionAlba(
                declMetodo.Nombre,
                declMetodo.Parametros,
                declMetodo.TipoRetorno,
                declMetodo.Cuerpo,
                Clase.EntornoDefinicion,
                Clase,
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

    internal void AsignarCampo(
        string nombre,
        ValorAlba nuevoValor,
        DefinicionClase? claseActualEnEjecucion,
        UbicacionFuente ubicacion,
        string codigoFuente)
    {
        if (!Clase.Campos.TryGetValue(nombre, out DeclCampoClase? declCampo))
        {
            throw CrearError(
                "ALB-E009",
                TipoError.Ejecucion,
                ubicacion,
                $"La clase '{Clase.Nombre}' no posee un campo llamado '{nombre}'.",
                codigoFuente
            );
        }

        ValidarAcceso(declCampo.Acceso, nombre, claseActualEnEjecucion, ubicacion, codigoFuente);

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

    private void ValidarAcceso(
        ModificadorAcceso acceso,
        string nombreMiembro,
        DefinicionClase? claseActualEnEjecucion,
        UbicacionFuente ubicacion,
        string codigoFuente)
    {
        if (acceso == ModificadorAcceso.Privado && !ReferenceEquals(claseActualEnEjecucion, Clase))
        {
            throw CrearError(
                "ALB-E010",
                TipoError.Ejecucion,
                ubicacion,
                $"Acceso denegado al miembro privado '{nombreMiembro}' de la clase '{Clase.Nombre}'.",
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
