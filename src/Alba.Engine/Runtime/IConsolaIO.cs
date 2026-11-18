namespace Alba.Engine.Runtime;

/// <summary>
/// Abstracción de entrada y salida de consola para desacoplar el motor del CLI y del IDE.
/// </summary>
public interface IConsolaIO
{
    void EscribirLinea(string texto);
    void Escribir(string texto);
    string? LeerLinea(string? indicador = null);
}
