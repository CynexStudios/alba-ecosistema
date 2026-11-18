namespace Alba.Engine.Diagnostics;

/// <summary>
/// Representa una posición exacta dentro de un archivo fuente de Alba.
/// </summary>
public readonly record struct UbicacionFuente(
    string Archivo,
    int Linea,
    int Columna,
    int Longitud = 1
)
{
    public static readonly UbicacionFuente Desconocida = new("<desconocido>", 1, 1, 1);

    public override string ToString() => $"{Archivo}:{Linea}:{Columna}";
}
