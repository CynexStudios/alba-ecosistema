using Alba.Engine.Diagnostics;

namespace Alba.Engine.Runtime;

internal sealed class SenalRetornar : Exception
{
    public ValorAlba Valor { get; }
    public SenalRetornar(ValorAlba valor) => Valor = valor;
}

internal sealed class SenalRomper : Exception { }

internal sealed class SenalContinuar : Exception { }

/// <summary>
/// Excepción lanzada explícitamente por el usuario mediante la sentencia 'lanzar'.
/// </summary>
public sealed class ExcepcionLanzadaUsuario : Exception
{
    public ValorAlba ValorLanzado { get; }
    public UbicacionFuente Ubicacion { get; }

    public ExcepcionLanzadaUsuario(ValorAlba valorLanzado, UbicacionFuente ubicacion)
        : base(valorLanzado.ATextoParaImprimir())
    {
        ValorLanzado = valorLanzado;
        Ubicacion = ubicacion;
    }
}
