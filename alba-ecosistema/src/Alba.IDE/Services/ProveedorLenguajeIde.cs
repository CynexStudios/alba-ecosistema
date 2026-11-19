using Alba.IDE.Models;

namespace Alba.IDE.Services;

/// <summary>
/// Conecta las herramientas instaladas y addons activos con las capacidades reales del editor:
/// diagnóstico, autocompletado, ejecución y estado de preparación por lenguaje.
/// </summary>
public sealed class ProveedorLenguajeIde
{
    private readonly GestorAddons _gestorAddons;
    private readonly DetectorEntornos _detectorEntornos;

    public ProveedorLenguajeIde(GestorAddons gestorAddons, DetectorEntornos detectorEntornos)
    {
        _gestorAddons = gestorAddons;
        _detectorEntornos = detectorEntornos;
    }

    public async Task<CapacidadesLenguajeTool> EvaluarCapacidadesLenguajeAsync(string extension)
    {
        string ext = extension.StartsWith('.') ? extension.ToLowerInvariant() : $".{extension.ToLowerInvariant()}";
        if (ext is ".cibao" or ".alb")
        {
            return new CapacidadesLenguajeTool
            {
                SoporteEdicion = true,
                Autocompletado = true,
                CompiladorOInterprete = true,
                Depurador = false, // Planificado Fase 7
                FrameworksYBibliotecas = true,
                HerramientasConstruccion = true
            };
        }

        var entornos = await _detectorEntornos.DetectarTodosAsync();
        var extendidas = await _detectorEntornos.DetectarHerramientasExtendidasAsync();
        AddonCargado? addon = _gestorAddons.ObtenerAddonPorExtension(ext);

        bool motorOk = ext switch
        {
            ".py" or ".pyw" => entornos.FirstOrDefault(e => e.Lenguaje == LenguajeSoportado.Python)?.Disponible ?? false,
            ".cs" => entornos.FirstOrDefault(e => e.Lenguaje == LenguajeSoportado.CSharp)?.Disponible ?? false,
            ".cpp" or ".cc" or ".cxx" => entornos.FirstOrDefault(e => e.Lenguaje == LenguajeSoportado.Cpp)?.Disponible ?? false,
            ".html" or ".htm" => true,
            ".js" or ".mjs" => extendidas.GetValueOrDefault("node").Disponible,
            ".ts" => extendidas.GetValueOrDefault("tsc").Disponible || extendidas.GetValueOrDefault("node").Disponible,
            ".go" => extendidas.GetValueOrDefault("go").Disponible,
            ".rs" => extendidas.GetValueOrDefault("rustc").Disponible,
            ".java" => extendidas.GetValueOrDefault("java").Disponible,
            ".php" => extendidas.GetValueOrDefault("php").Disponible,
            ".rb" => extendidas.GetValueOrDefault("ruby").Disponible,
            _ => false
        };

        bool edicionOk = addon is not null || ext is ".cs" or ".cpp" or ".html" or ".js" or ".css" or ".json" or ".xml";
        bool completadoOk = addon is { Snippets.Count: > 0 };
        bool depuradorOk = ext is ".cpp" or ".cc" && extendidas.GetValueOrDefault("gdb").Disponible;

        return new CapacidadesLenguajeTool
        {
            SoporteEdicion = edicionOk,
            Autocompletado = completadoOk,
            CompiladorOInterprete = motorOk,
            Depurador = depuradorOk,
            FrameworksYBibliotecas = motorOk && (ext is ".py" or ".cs" or ".js" or ".ts" or ".go" or ".rs"),
            HerramientasConstruccion = ext switch
            {
                ".cs" => motorOk,
                ".cpp" => extendidas.GetValueOrDefault("cmake").Disponible || motorOk,
                ".go" or ".rs" => motorOk,
                _ => false
            }
        };
    }
}
