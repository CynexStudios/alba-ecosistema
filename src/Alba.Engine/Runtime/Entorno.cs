using Alba.Engine.Diagnostics;

namespace Alba.Engine.Runtime;

public sealed class SimboloAlba
{
    public ValorAlba Valor { get; set; }
    public string? TipoRestriccion { get; }
    public bool EsConstante { get; }
    public bool EsNativoIntegrado { get; }

    public SimboloAlba(ValorAlba valor, string? tipoRestriccion, bool esConstante, bool esNativoIntegrado = false)
    {
        Valor = valor;
        TipoRestriccion = tipoRestriccion;
        EsConstante = esConstante;
        EsNativoIntegrado = esNativoIntegrado;
    }
}

/// <summary>
/// Entorno léxico de variables y constantes con soporte de ámbitos anidados y closures.
/// </summary>
public sealed class Entorno
{
    private readonly Dictionary<string, SimboloAlba> _simbolos = new(StringComparer.Ordinal);
    public Entorno? Padre { get; }

    public Entorno(Entorno? padre = null)
    {
        Padre = padre;
    }

    internal IReadOnlyDictionary<string, ValorAlba> ObtenerExportacionesUsuario()
    {
        var dict = new Dictionary<string, ValorAlba>(StringComparer.Ordinal);
        foreach (var (nombre, simbolo) in _simbolos)
        {
            if (!simbolo.EsNativoIntegrado)
            {
                dict[nombre] = simbolo.Valor;
            }
        }
        return dict;
    }

    public void Declarar(
        string nombre,
        ValorAlba valor,
        string? tipoRestriccion,
        bool esConstante,
        UbicacionFuente ubicacion,
        string codigoFuente)
    {
        if (_simbolos.ContainsKey(nombre))
        {
            throw CrearError(
                "ALB-E001",
                TipoError.Ejecucion,
                ubicacion,
                $"El identificador '{nombre}' ya fue declarado en este mismo ámbito.",
                codigoFuente
            );
        }

        if (tipoRestriccion is not null && !valor.EsCompatibleConAnotacion(tipoRestriccion))
        {
            throw CrearError(
                "ALB-T001",
                TipoError.Tipo,
                ubicacion,
                $"No se puede asignar un valor de tipo '{valor.ObtenerNombreTipo()}' a '{nombre}' declarado como '{tipoRestriccion}'.",
                codigoFuente
            );
        }

        ValorAlba valorAjustado = valor.AjustarParaAnotacion(tipoRestriccion);
        _simbolos[nombre] = new SimboloAlba(valorAjustado, tipoRestriccion, esConstante);
    }

    public void DeclararInterno(string nombre, ValorAlba valor, bool esConstante = true)
    {
        _simbolos[nombre] = new SimboloAlba(valor, null, esConstante, esNativoIntegrado: true);
    }

    internal void DeclararDesdeModulo(string nombre, ValorAlba valor, UbicacionFuente ubicacion, string codigoFuente)
    {
        if (_simbolos.TryGetValue(nombre, out SimboloAlba? existente))
        {
            if (existente.EsNativoIntegrado || ReferenceEquals(existente.Valor.Dato, valor.Dato))
            {
                _simbolos[nombre] = new SimboloAlba(valor, null, false);
                return;
            }

            throw CrearError(
                "ALB-E001",
                TipoError.Ejecucion,
                ubicacion,
                $"El símbolo importado '{nombre}' colisiona con una declaración previa en este mismo ámbito.",
                codigoFuente
            );
        }

        _simbolos[nombre] = new SimboloAlba(valor, null, false);
    }

    public ValorAlba Obtener(string nombre, UbicacionFuente ubicacion, string codigoFuente)
    {
        if (_simbolos.TryGetValue(nombre, out SimboloAlba? simbolo))
        {
            return simbolo.Valor;
        }

        if (Padre is not null)
        {
            return Padre.Obtener(nombre, ubicacion, codigoFuente);
        }

        throw CrearError(
            "ALB-E003",
            TipoError.Ejecucion,
            ubicacion,
            $"La variable o función '{nombre}' no está definida en este ámbito.",
            codigoFuente
        );
    }

    public void Asignar(string nombre, ValorAlba nuevoValor, UbicacionFuente ubicacion, string codigoFuente)
    {
        if (_simbolos.TryGetValue(nombre, out SimboloAlba? simbolo))
        {
            if (simbolo.EsConstante)
            {
                throw CrearError(
                    "ALB-E002",
                    TipoError.Ejecucion,
                    ubicacion,
                    $"No se puede reasignar la constante '{nombre}'.",
                    codigoFuente
                );
            }

            if (simbolo.TipoRestriccion is not null && !nuevoValor.EsCompatibleConAnotacion(simbolo.TipoRestriccion))
            {
                throw CrearError(
                    "ALB-T001",
                    TipoError.Tipo,
                    ubicacion,
                    $"No se puede asignar un valor de tipo '{nuevoValor.ObtenerNombreTipo()}' a la variable '{nombre}' declarada como '{simbolo.TipoRestriccion}'.",
                    codigoFuente
                );
            }

            simbolo.Valor = nuevoValor.AjustarParaAnotacion(simbolo.TipoRestriccion);
            return;
        }

        if (Padre is not null)
        {
            Padre.Asignar(nombre, nuevoValor, ubicacion, codigoFuente);
            return;
        }

        throw CrearError(
            "ALB-E003",
            TipoError.Ejecucion,
            ubicacion,
            $"No se puede asignar a '{nombre}' porque no ha sido declarada.",
            codigoFuente
        );
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
