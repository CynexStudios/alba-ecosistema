using System.Collections.Concurrent;
using Alba.Engine.Runtime;

namespace Alba.IDE.Services;

/// <summary>
/// Puente thread-safe entre el intérprete en segundo plano y la consola interactiva del IDE.
/// Permite ejecutar programas que invoquen 'leer(...)' sin bloquear el hilo de interfaz gráfica.
/// </summary>
public sealed class ConsolaIdeIO : IConsolaIO, IDisposable
{
    private readonly BlockingCollection<string> _entradas = new();
    private readonly Action<string> _alEscribirLinea;
    private readonly Action<string> _alEscribir;
    private readonly Action<string?> _alSolicitarEntrada;
    private readonly CancellationToken _cancellationToken;

    public ConsolaIdeIO(
        Action<string> alEscribirLinea,
        Action<string> alEscribir,
        Action<string?> alSolicitarEntrada,
        CancellationToken cancellationToken)
    {
        _alEscribirLinea = alEscribirLinea;
        _alEscribir = alEscribir;
        _alSolicitarEntrada = alSolicitarEntrada;
        _cancellationToken = cancellationToken;
    }

    public void EscribirLinea(string texto) => _alEscribirLinea(texto);

    public void Escribir(string texto) => _alEscribir(texto);

    public string? LeerLinea(string? indicador = null)
    {
        _alSolicitarEntrada(indicador);
        return _entradas.Take(_cancellationToken);
    }

    public void EnviarEntrada(string texto)
    {
        if (!_entradas.IsAddingCompleted)
        {
            _entradas.Add(texto);
        }
    }

    public void Dispose() => _entradas.Dispose();
}
