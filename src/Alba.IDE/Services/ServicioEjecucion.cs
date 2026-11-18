using Alba.Engine.Diagnostics;
using Alba.Engine.Parser;
using Alba.Engine.Runtime;
using Alba.IDE.Models;

namespace Alba.IDE.Services;

/// <summary>
/// Coordina la ejecución directa del motor Alba (.alb) y de complementos externos (/addons).
/// </summary>
public sealed class ServicioEjecucion
{
    private readonly GestorAddons _gestorAddons;

    public ServicioEjecucion(GestorAddons gestorAddons)
    {
        _gestorAddons = gestorAddons;
    }

    public async Task<bool> EjecutarArchivoAsync(
        string rutaArchivo,
        string codigoFuente,
        ConsolaIdeIO consolaAlba,
        Action<TipoMensajeConsola, string> registrarMensaje,
        CancellationToken cancellationToken)
    {
        string extension = Path.GetExtension(rutaArchivo).ToLowerInvariant();
        string nombreArchivo = Path.GetFileName(rutaArchivo);

        if (extension == ".alb")
        {
            return await Task.Run(() =>
            {
                var (lexico, sintactico) = AnalizadorSintactico.AnalizarCodigo(codigoFuente, nombreArchivo);

                if (lexico.TieneErrores || sintactico.TieneErrores)
                {
                    foreach (ErrorAlba err in lexico.Errores)
                    {
                        registrarMensaje(TipoMensajeConsola.Error, FormateadorErrores.Formatear(err));
                    }
                    foreach (ErrorAlba err in sintactico.Errores)
                    {
                        registrarMensaje(TipoMensajeConsola.Error, FormateadorErrores.Formatear(err));
                    }
                    return false;
                }

                var interprete = new Interprete(consolaAlba, codigoFuente);
                try
                {
                    interprete.Ejecutar(sintactico.Programa);
                    return true;
                }
                catch (OperationCanceledException)
                {
                    registrarMensaje(TipoMensajeConsola.Advertencia, "Ejecución detenida por el usuario.");
                    return false;
                }
                catch (ExcepcionAlba ex)
                {
                    registrarMensaje(TipoMensajeConsola.Error, FormateadorErrores.Formatear(ex.Error));
                    return false;
                }
                catch (ExcepcionLanzadaUsuario exUsuario)
                {
                    string linea = FormateadorErrores.ExtraerLinea(codigoFuente, exUsuario.Ubicacion.Linea);
                    var error = new ErrorAlba(
                        "ALB-E090",
                        TipoError.Ejecucion,
                        exUsuario.Ubicacion,
                        $"Excepción no capturada: {exUsuario.ValorLanzado.ATextoParaImprimir()}",
                        linea);
                    registrarMensaje(TipoMensajeConsola.Error, FormateadorErrores.Formatear(error));
                    return false;
                }
            }, cancellationToken);
        }

        AddonCargado? addon = _gestorAddons.ObtenerAddonPorExtension(extension);
        if (addon is null)
        {
            registrarMensaje(
                TipoMensajeConsola.Advertencia,
                $"No hay ningún motor o complemento activo para ejecutar archivos '{extension}'.");
            return false;
        }

        registrarMensaje(
            TipoMensajeConsola.Sistema,
            $"Ejecutando mediante complemento '{addon.Manifiesto.Nombre}' ({addon.Manifiesto.Lenguaje})...");

        int codigoSalida = await _gestorAddons.EjecutarConAddonAsync(
            addon,
            rutaArchivo,
            (linea, esError) => registrarMensaje(esError ? TipoMensajeConsola.Error : TipoMensajeConsola.Salida, linea),
            cancellationToken);

        return codigoSalida == 0;
    }
}
