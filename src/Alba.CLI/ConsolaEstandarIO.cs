using Alba.Engine.Runtime;

namespace Alba.CLI;

public sealed class ConsolaEstandarIO : IConsolaIO
{
    public void EscribirLinea(string texto) => Console.WriteLine(texto);

    public void Escribir(string texto) => Console.Write(texto);

    public string? LeerLinea(string? indicador = null)
    {
        if (!string.IsNullOrEmpty(indicador))
        {
            Console.Write(indicador);
        }
        return Console.ReadLine();
    }
}
