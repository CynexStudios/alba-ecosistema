using System.Text;
using Alba.Engine.Diagnostics;
using Alba.Engine.Parser;
using Alba.Engine.Runtime;

namespace Alba.CLI;

public static class Program
{
    private const string VersionAlba = "Alba v2.0.0";

    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        if (args.Length == 0)
        {
            MostrarAyuda();
            return 0;
        }

        string comando = args[0].ToLowerInvariant();

        switch (comando)
        {
            case "version":
            case "--version":
            case "-v":
                Console.WriteLine(VersionAlba);
                return 0;

            case "ayuda":
            case "--help":
            case "-h":
                MostrarAyuda();
                return 0;

            case "ejecutar":
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("Uso: alba ejecutar <archivo.alb>");
                    return 1;
                }
                return EjecutarArchivo(args[1]);

            case "verificar":
                if (args.Length < 2)
                {
                    Console.Error.WriteLine("Uso: alba verificar <archivo.alb>");
                    return 1;
                }
                return VerificarArchivo(args[1]);

            default:
                // Permite invocar directamente: alba programa.alb
                if (args.Length == 1 && File.Exists(args[0]))
                {
                    return EjecutarArchivo(args[0]);
                }

                Console.Error.WriteLine($"Comando desconocido: '{args[0]}'.");
                MostrarAyuda();
                return 1;
        }
    }

    private static int EjecutarArchivo(string ruta)
    {
        if (!ValidarRutaArchivo(ruta, out string codigoFuente, out string nombreArchivo))
        {
            return 1;
        }

        var consola = new ConsolaEstandarIO();
        ResultadoEjecucion resultado = EjecutorAlba.Ejecutar(codigoFuente, nombreArchivo, consola);

        if (!resultado.Exito)
        {
            foreach (ErrorAlba error in resultado.Errores)
            {
                Console.Error.WriteLine(FormateadorErrores.Formatear(error));
                Console.Error.WriteLine();
            }
            return 1;
        }

        return 0;
    }

    private static int VerificarArchivo(string ruta)
    {
        if (!ValidarRutaArchivo(ruta, out string codigoFuente, out string nombreArchivo))
        {
            return 1;
        }

        var (lexico, sintactico) = AnalizadorSintactico.AnalizarCodigo(codigoFuente, nombreArchivo);

        if (!lexico.TieneErrores && !sintactico.TieneErrores)
        {
            Console.WriteLine($"Verificación exitosa: '{nombreArchivo}' no contiene errores léxicos ni sintácticos.");
            return 0;
        }

        foreach (ErrorAlba err in lexico.Errores)
        {
            Console.Error.WriteLine(FormateadorErrores.Formatear(err));
            Console.Error.WriteLine();
        }
        foreach (ErrorAlba err in sintactico.Errores)
        {
            Console.Error.WriteLine(FormateadorErrores.Formatear(err));
            Console.Error.WriteLine();
        }
        return 1;
    }

    private static bool ValidarRutaArchivo(string ruta, out string contenido, out string nombreArchivo)
    {
        contenido = string.Empty;
        nombreArchivo = Path.GetFileName(ruta);

        if (!File.Exists(ruta))
        {
            Console.Error.WriteLine($"No se encontró el archivo '{ruta}'.");
            return false;
        }

        contenido = File.ReadAllText(ruta, Encoding.UTF8);
        return true;
    }

    private static void MostrarAyuda()
    {
        Console.WriteLine($"{VersionAlba} - Motor e Intérprete del lenguaje Alba");
        Console.WriteLine();
        Console.WriteLine("Uso:");
        Console.WriteLine("  alba ejecutar <archivo.alb>    Ejecuta un programa escrito en Alba");
        Console.WriteLine("  alba verificar <archivo.alb>   Analiza sintácticamente sin ejecutar");
        Console.WriteLine("  alba version                   Muestra la versión del motor");
        Console.WriteLine("  alba ayuda                     Muestra esta ayuda");
    }
}
