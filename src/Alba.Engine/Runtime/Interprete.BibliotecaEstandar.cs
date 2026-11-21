using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Alba.Engine.Diagnostics;

namespace Alba.Engine.Runtime;

public sealed partial class Interprete
{
    private void RegistrarBibliotecaEstandar()
    {
        // --- Núcleo y colecciones ---
        RegistrarNativa("rango", (args, u) =>
        {
            long inicio = 0;
            long fin;
            long paso = 1;

            if (args.Count == 1)
            {
                fin = ExigirEntero(args[0], "rango", u);
            }
            else if (args.Count == 2)
            {
                inicio = ExigirEntero(args[0], "rango", u);
                fin = ExigirEntero(args[1], "rango", u);
            }
            else if (args.Count == 3)
            {
                inicio = ExigirEntero(args[0], "rango", u);
                fin = ExigirEntero(args[1], "rango", u);
                paso = ExigirEntero(args[2], "rango", u);
                if (paso == 0)
                    throw CrearError("ALB-E007", TipoError.Ejecucion, u, "El paso de 'rango' no puede ser 0.");
            }
            else
            {
                throw CrearError("ALB-E011", TipoError.Ejecucion, u,
                    "'rango' acepta entre 1 y 3 argumentos enteros.");
            }

            var resultado = new List<ValorAlba>();
            const int limiteSeguridad = 1_000_000;

            if (paso > 0)
            {
                for (long i = inicio; i < fin; i += paso)
                {
                    if (resultado.Count >= limiteSeguridad)
                        throw CrearError("ALB-E008", TipoError.Ejecucion, u, "El rango excede el límite de 1,000,000 elementos.");
                    resultado.Add(ValorAlba.CrearEntero(i));
                }
            }
            else
            {
                for (long i = inicio; i > fin; i += paso)
                {
                    if (resultado.Count >= limiteSeguridad)
                        throw CrearError("ALB-E008", TipoError.Ejecucion, u, "El rango excede el límite de 1,000,000 elementos.");
                    resultado.Add(ValorAlba.CrearEntero(i));
                }
            }

            return ValorAlba.CrearLista(resultado);
        });

        RegistrarNativa("longitud", (args, u) =>
        {
            ExigirAridad("longitud", args, 1, u);
            return args[0].Tipo switch
            {
                TipoValorAlba.Lista => ValorAlba.CrearEntero(args[0].ComoLista().Count),
                TipoValorAlba.Diccionario => ValorAlba.CrearEntero(args[0].ComoDiccionario().Count),
                TipoValorAlba.Texto => ValorAlba.CrearEntero(args[0].ComoTexto().Length),
                _ => throw CrearError("ALB-T006", TipoError.Tipo, u,
                    $"'longitud' requiere 'lista', 'diccionario' o 'texto', se recibió '{args[0].ObtenerNombreTipo()}'.")
            };
        });

        RegistrarNativa("tipo_de", (args, u) =>
        {
            ExigirAridad("tipo_de", args, 1, u);
            return ValorAlba.CrearTexto(args[0].ObtenerNombreTipo());
        });

        RegistrarNativa("convertir_texto", (args, u) =>
        {
            ExigirAridad("convertir_texto", args, 1, u);
            return ValorAlba.CrearTexto(args[0].ATextoParaImprimir());
        });

        RegistrarNativa("convertir_entero", (args, u) =>
        {
            ExigirAridad("convertir_entero", args, 1, u);
            ValorAlba v = args[0];
            return v.Tipo switch
            {
                TipoValorAlba.Entero => v,
                TipoValorAlba.Decimal => ValorAlba.CrearEntero((long)v.ComoDecimal()),
                TipoValorAlba.Booleano => ValorAlba.CrearEntero(v.ComoBooleano() ? 1 : 0),
                TipoValorAlba.Texto when long.TryParse(v.ComoTexto().Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long n)
                    => ValorAlba.CrearEntero(n),
                _ => throw CrearError("ALB-E014", TipoError.Ejecucion, u,
                    $"No se pudo convertir '{v.ATextoParaImprimir()}' a 'entero'.")
            };
        });

        RegistrarNativa("convertir_decimal", (args, u) =>
        {
            ExigirAridad("convertir_decimal", args, 1, u);
            ValorAlba v = args[0];
            return v.Tipo switch
            {
                TipoValorAlba.Decimal => v,
                TipoValorAlba.Entero => ValorAlba.CrearDecimal(v.ComoEntero()),
                TipoValorAlba.Texto when double.TryParse(v.ComoTexto().Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d)
                    => ValorAlba.CrearDecimal(d),
                _ => throw CrearError("ALB-E014", TipoError.Ejecucion, u,
                    $"No se pudo convertir '{v.ATextoParaImprimir()}' a 'decimal'.")
            };
        });

        RegistrarNativa("agregar", (args, u) =>
        {
            ExigirAridad("agregar", args, 2, u);
            ExigirLista(args[0], "agregar", u).Add(args[1]);
            return ValorAlba.Nulo;
        });

        RegistrarNativa("eliminar", (args, u) =>
        {
            ExigirAridad("eliminar", args, 2, u);
            List<ValorAlba> lista = ExigirLista(args[0], "eliminar", u);
            long indice = ExigirEntero(args[1], "eliminar", u);
            int pos = ConvertirIndiceLista(indice, lista.Count, u);
            ValorAlba eliminado = lista[pos];
            lista.RemoveAt(pos);
            return eliminado;
        });

        // --- Matemáticas ---
        RegistrarNativa("raiz", (args, u) =>
        {
            ExigirAridad("raiz", args, 1, u);
            double x = ExigirNumero(args[0], "raiz", u);
            if (x < 0)
                throw CrearError("ALB-E014", TipoError.Ejecucion, u, "No se puede calcular 'raiz' de un número negativo.");
            return ValorAlba.CrearDecimal(Math.Sqrt(x));
        });

        RegistrarNativa("abs", (args, u) =>
        {
            ExigirAridad("abs", args, 1, u);
            ValorAlba v = args[0];
            if (v.Tipo == TipoValorAlba.Entero) return ValorAlba.CrearEntero(Math.Abs(v.ComoEntero()));
            return ValorAlba.CrearDecimal(Math.Abs(ExigirNumero(v, "abs", u)));
        });

        RegistrarNativa("redondear", (args, u) =>
        {
            if (args.Count == 1)
            {
                double x = ExigirNumero(args[0], "redondear", u);
                return ValorAlba.CrearEntero((long)Math.Round(x, MidpointRounding.AwayFromZero));
            }
            if (args.Count == 2)
            {
                double x = ExigirNumero(args[0], "redondear", u);
                int dec = (int)ExigirEntero(args[1], "redondear", u);
                return ValorAlba.CrearDecimal(Math.Round(x, Math.Clamp(dec, 0, 15), MidpointRounding.AwayFromZero));
            }
            throw CrearError("ALB-E011", TipoError.Ejecucion, u, "'redondear' acepta 1 o 2 argumentos.");
        });

        RegistrarNativa("piso", (args, u) =>
        {
            ExigirAridad("piso", args, 1, u);
            return ValorAlba.CrearEntero((long)Math.Floor(ExigirNumero(args[0], "piso", u)));
        });

        RegistrarNativa("techo", (args, u) =>
        {
            ExigirAridad("techo", args, 1, u);
            return ValorAlba.CrearEntero((long)Math.Ceiling(ExigirNumero(args[0], "techo", u)));
        });

        RegistrarNativa("minimo", (args, u) =>
        {
            ExigirAridad("minimo", args, 2, u);
            if (args[0].Tipo == TipoValorAlba.Entero && args[1].Tipo == TipoValorAlba.Entero)
                return ValorAlba.CrearEntero(Math.Min(args[0].ComoEntero(), args[1].ComoEntero()));
            return ValorAlba.CrearDecimal(Math.Min(ExigirNumero(args[0], "minimo", u), ExigirNumero(args[1], "minimo", u)));
        });

        RegistrarNativa("maximo", (args, u) =>
        {
            ExigirAridad("maximo", args, 2, u);
            if (args[0].Tipo == TipoValorAlba.Entero && args[1].Tipo == TipoValorAlba.Entero)
                return ValorAlba.CrearEntero(Math.Max(args[0].ComoEntero(), args[1].ComoEntero()));
            return ValorAlba.CrearDecimal(Math.Max(ExigirNumero(args[0], "maximo", u), ExigirNumero(args[1], "maximo", u)));
        });

        RegistrarNativa("seno", (args, u) =>
        {
            ExigirAridad("seno", args, 1, u);
            return ValorAlba.CrearDecimal(Math.Sin(ExigirNumero(args[0], "seno", u)));
        });

        RegistrarNativa("coseno", (args, u) =>
        {
            ExigirAridad("coseno", args, 1, u);
            return ValorAlba.CrearDecimal(Math.Cos(ExigirNumero(args[0], "coseno", u)));
        });

        RegistrarNativa("tangente", (args, u) =>
        {
            ExigirAridad("tangente", args, 1, u);
            return ValorAlba.CrearDecimal(Math.Tan(ExigirNumero(args[0], "tangente", u)));
        });

        RegistrarNativa("logaritmo", (args, u) =>
        {
            ExigirAridad("logaritmo", args, 1, u);
            double x = ExigirNumero(args[0], "logaritmo", u);
            if (x <= 0)
                throw CrearError("ALB-E014", TipoError.Ejecucion, u, "'logaritmo' requiere un número mayor que cero.");
            return ValorAlba.CrearDecimal(Math.Log(x));
        });

        RegistrarNativa("aleatorio", (args, u) =>
        {
            if (args.Count == 0)
                return ValorAlba.CrearDecimal(Random.Shared.NextDouble());
            if (args.Count == 2)
            {
                long min = ExigirEntero(args[0], "aleatorio", u);
                long max = ExigirEntero(args[1], "aleatorio", u);
                if (min >= max)
                    throw CrearError("ALB-E014", TipoError.Ejecucion, u, "En 'aleatorio(min, max)', 'min' debe ser menor que 'max'.");
                return ValorAlba.CrearEntero(Random.Shared.NextInt64(min, max));
            }
            throw CrearError("ALB-E011", TipoError.Ejecucion, u, "'aleatorio' acepta 0 argumentos o (min, max) enteros.");
        });

        RegistrarNativa("pi", (args, u) =>
        {
            ExigirAridad("pi", args, 0, u);
            return ValorAlba.CrearDecimal(Math.PI);
        });

        RegistrarNativa("euler", (args, u) =>
        {
            ExigirAridad("euler", args, 0, u);
            return ValorAlba.CrearDecimal(Math.E);
        });

        // --- Manipulación de Texto ---
        RegistrarNativa("mayusculas", (args, u) =>
        {
            ExigirAridad("mayusculas", args, 1, u);
            return ValorAlba.CrearTexto(ExigirTexto(args[0], "mayusculas", u).ToUpperInvariant());
        });

        RegistrarNativa("minusculas", (args, u) =>
        {
            ExigirAridad("minusculas", args, 1, u);
            return ValorAlba.CrearTexto(ExigirTexto(args[0], "minusculas", u).ToLowerInvariant());
        });

        RegistrarNativa("recortar", (args, u) =>
        {
            ExigirAridad("recortar", args, 1, u);
            return ValorAlba.CrearTexto(ExigirTexto(args[0], "recortar", u).Trim());
        });

        RegistrarNativa("dividir", (args, u) =>
        {
            ExigirAridad("dividir", args, 2, u);
            string texto = ExigirTexto(args[0], "dividir", u);
            string sep = ExigirTexto(args[1], "dividir", u);
            string[] partes = sep.Length == 0
                ? texto.Select(c => c.ToString()).ToArray()
                : texto.Split(sep, StringSplitOptions.None);
            return ValorAlba.CrearLista(partes.Select(ValorAlba.CrearTexto).ToList());
        });

        RegistrarNativa("unir", (args, u) =>
        {
            ExigirAridad("unir", args, 2, u);
            List<ValorAlba> lista = ExigirLista(args[0], "unir", u);
            string sep = ExigirTexto(args[1], "unir", u);
            return ValorAlba.CrearTexto(string.Join(sep, lista.Select(v => v.ATextoParaImprimir())));
        });

        RegistrarNativa("reemplazar", (args, u) =>
        {
            ExigirAridad("reemplazar", args, 3, u);
            string texto = ExigirTexto(args[0], "reemplazar", u);
            string buscar = ExigirTexto(args[1], "reemplazar", u);
            string nuevo = ExigirTexto(args[2], "reemplazar", u);
            return ValorAlba.CrearTexto(buscar.Length == 0 ? texto : texto.Replace(buscar, nuevo, StringComparison.Ordinal));
        });

        RegistrarNativa("contiene", (args, u) =>
        {
            ExigirAridad("contiene", args, 2, u);
            if (args[0].Tipo == TipoValorAlba.Lista)
            {
                return ValorAlba.CrearBooleano(args[0].ComoLista().Any(x => x.EsIgualA(args[1])));
            }
            if (args[0].Tipo == TipoValorAlba.Diccionario)
            {
                string clave = ExigirTexto(args[1], "contiene", u);
                return ValorAlba.CrearBooleano(args[0].ComoDiccionario().ContainsKey(clave));
            }
            string texto = ExigirTexto(args[0], "contiene", u);
            string sub = ExigirTexto(args[1], "contiene", u);
            return ValorAlba.CrearBooleano(texto.Contains(sub, StringComparison.Ordinal));
        });

        RegistrarNativa("empieza_con", (args, u) =>
        {
            ExigirAridad("empieza_con", args, 2, u);
            return ValorAlba.CrearBooleano(
                ExigirTexto(args[0], "empieza_con", u).StartsWith(ExigirTexto(args[1], "empieza_con", u), StringComparison.Ordinal));
        });

        RegistrarNativa("termina_con", (args, u) =>
        {
            ExigirAridad("termina_con", args, 2, u);
            return ValorAlba.CrearBooleano(
                ExigirTexto(args[0], "termina_con", u).EndsWith(ExigirTexto(args[1], "termina_con", u), StringComparison.Ordinal));
        });

        RegistrarNativa("subtexto", (args, u) =>
        {
            if (args.Count is < 2 or > 3)
                throw CrearError("ALB-E011", TipoError.Ejecucion, u, "'subtexto' acepta (texto, inicio) o (texto, inicio, largo).");
            string s = ExigirTexto(args[0], "subtexto", u);
            int inicio = (int)ExigirEntero(args[1], "subtexto", u);
            if (inicio < 0 || inicio > s.Length)
                throw CrearError("ALB-E006", TipoError.Ejecucion, u, $"Índice inicial {inicio} fuera de rango para texto de longitud {s.Length}.");

            if (args.Count == 2)
                return ValorAlba.CrearTexto(s[inicio..]);

            int largo = (int)ExigirEntero(args[2], "subtexto", u);
            if (largo < 0 || inicio + largo > s.Length)
                throw CrearError("ALB-E006", TipoError.Ejecucion, u, $"Subtexto ({inicio}, {largo}) fuera de rango.");
            return ValorAlba.CrearTexto(s.Substring(inicio, largo));
        });

        // --- Listas funcionales ---
        RegistrarNativa("mapear", (args, u) =>
        {
            ExigirAridad("mapear", args, 2, u);
            List<ValorAlba> lista = ExigirLista(args[0], "mapear", u);
            return MapearLista(lista, args[1], u);
        });

        RegistrarNativa("filtrar", (args, u) =>
        {
            ExigirAridad("filtrar", args, 2, u);
            List<ValorAlba> lista = ExigirLista(args[0], "filtrar", u);
            return FiltrarLista(lista, args[1], u);
        });

        RegistrarNativa("reducir", (args, u) =>
        {
            ExigirAridad("reducir", args, 3, u);
            List<ValorAlba> lista = ExigirLista(args[0], "reducir", u);
            return ReducirLista(lista, args[1], args[2], u);
        });

        RegistrarNativa("ordenar", (args, u) =>
        {
            ExigirAridad("ordenar", args, 1, u);
            List<ValorAlba> lista = ExigirLista(args[0], "ordenar", u);
            return OrdenarListaCopia(lista, u);
        });

        RegistrarNativa("invertir", (args, u) =>
        {
            ExigirAridad("invertir", args, 1, u);
            var copia = new List<ValorAlba>(ExigirLista(args[0], "invertir", u));
            copia.Reverse();
            return ValorAlba.CrearLista(copia);
        });

        RegistrarNativa("contiene_elemento", (args, u) =>
        {
            ExigirAridad("contiene_elemento", args, 2, u);
            List<ValorAlba> lista = ExigirLista(args[0], "contiene_elemento", u);
            return ValorAlba.CrearBooleano(lista.Any(x => x.EsIgualA(args[1])));
        });

        RegistrarNativa("indice_de", (args, u) =>
        {
            ExigirAridad("indice_de", args, 2, u);
            List<ValorAlba> lista = ExigirLista(args[0], "indice_de", u);
            for (int i = 0; i < lista.Count; i++)
            {
                if (lista[i].EsIgualA(args[1])) return ValorAlba.CrearEntero(i);
            }
            return ValorAlba.CrearEntero(-1);
        });

        // --- Diccionarios y JSON ---
        RegistrarNativa("claves", (args, u) =>
        {
            ExigirAridad("claves", args, 1, u);
            Dictionary<string, ValorAlba> dict = ExigirDiccionario(args[0], "claves", u);
            return ValorAlba.CrearLista(dict.Keys.Select(ValorAlba.CrearTexto).ToList());
        });

        RegistrarNativa("valores", (args, u) =>
        {
            ExigirAridad("valores", args, 1, u);
            Dictionary<string, ValorAlba> dict = ExigirDiccionario(args[0], "valores", u);
            return ValorAlba.CrearLista(dict.Values.ToList());
        });

        RegistrarNativa("contiene_clave", (args, u) =>
        {
            ExigirAridad("contiene_clave", args, 2, u);
            Dictionary<string, ValorAlba> dict = ExigirDiccionario(args[0], "contiene_clave", u);
            string clave = ExigirTexto(args[1], "contiene_clave", u);
            return ValorAlba.CrearBooleano(dict.ContainsKey(clave));
        });

        RegistrarNativa("eliminar_clave", (args, u) =>
        {
            ExigirAridad("eliminar_clave", args, 2, u);
            Dictionary<string, ValorAlba> dict = ExigirDiccionario(args[0], "eliminar_clave", u);
            string clave = ExigirTexto(args[1], "eliminar_clave", u);
            return ValorAlba.CrearBooleano(dict.Remove(clave));
        });

        RegistrarNativa("json_a_texto", (args, u) =>
        {
            if (args.Count is < 1 or > 2)
                throw CrearError("ALB-E011", TipoError.Ejecucion, u, "'json_a_texto' acepta (valor) o (valor, formateado).");
            bool formateado = args.Count == 2 && args[1].Tipo == TipoValorAlba.Booleano && args[1].ComoBooleano();
            string json = ConvertirValorAJson(args[0], formateado);
            return ValorAlba.CrearTexto(json);
        });

        RegistrarNativa("texto_a_json", (args, u) =>
        {
            ExigirAridad("texto_a_json", args, 1, u);
            string raw = ExigirTexto(args[0], "texto_a_json", u);
            try
            {
                using JsonDocument doc = JsonDocument.Parse(raw);
                return ConvertirJsonElementAValor(doc.RootElement);
            }
            catch (JsonException ex)
            {
                throw CrearError("ALB-E014", TipoError.Ejecucion, u, $"JSON inválido: {ex.Message}");
            }
        });

        // --- Archivos (UTF-8 seguros) ---
        RegistrarNativa("leer_archivo", (args, u) =>
        {
            ExigirAridad("leer_archivo", args, 1, u);
            string ruta = ResolverRutaArchivoIO(ExigirTexto(args[0], "leer_archivo", u));
            try
            {
                return ValorAlba.CrearTexto(File.ReadAllText(ruta, Encoding.UTF8));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw CrearError("ALB-E017", TipoError.Ejecucion, u, $"No se pudo leer el archivo '{ruta}': {ex.Message}");
            }
        });

        RegistrarNativa("escribir_archivo", (args, u) =>
        {
            ExigirAridad("escribir_archivo", args, 2, u);
            string ruta = ResolverRutaArchivoIO(ExigirTexto(args[0], "escribir_archivo", u));
            string contenido = args[1].ATextoParaImprimir();
            try
            {
                string? dir = Path.GetDirectoryName(ruta);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(ruta, contenido, Encoding.UTF8);
                return ValorAlba.Verdadero;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw CrearError("ALB-E017", TipoError.Ejecucion, u, $"No se pudo escribir el archivo '{ruta}': {ex.Message}");
            }
        });

        RegistrarNativa("agregar_linea_archivo", (args, u) =>
        {
            ExigirAridad("agregar_linea_archivo", args, 2, u);
            string ruta = ResolverRutaArchivoIO(ExigirTexto(args[0], "agregar_linea_archivo", u));
            string linea = args[1].ATextoParaImprimir();
            try
            {
                string? dir = Path.GetDirectoryName(ruta);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.AppendAllText(ruta, linea + Environment.NewLine, Encoding.UTF8);
                return ValorAlba.Verdadero;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw CrearError("ALB-E017", TipoError.Ejecucion, u, $"No se pudo anexar al archivo '{ruta}': {ex.Message}");
            }
        });

        RegistrarNativa("existe_archivo", (args, u) =>
        {
            ExigirAridad("existe_archivo", args, 1, u);
            string ruta = ResolverRutaArchivoIO(ExigirTexto(args[0], "existe_archivo", u));
            return ValorAlba.CrearBooleano(File.Exists(ruta));
        });

        // --- Tiempo y utilidades ---
        RegistrarNativa("tiempo_actual_ms", (args, u) =>
        {
            ExigirAridad("tiempo_actual_ms", args, 0, u);
            return ValorAlba.CrearEntero(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        });

        RegistrarNativa("fecha_actual", (args, u) =>
        {
            ExigirAridad("fecha_actual", args, 0, u);
            return ValorAlba.CrearTexto(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        });

        RegistrarNativa("esperar_ms", (args, u) =>
        {
            ExigirAridad("esperar_ms", args, 1, u);
            long ms = ExigirEntero(args[0], "esperar_ms", u);
            if (ms > 0)
            {
                Thread.Sleep((int)Math.Min(ms, 60_000));
            }
            return ValorAlba.Nulo;
        });

        // --- Cibao 9.0: Colecciones avanzadas, tipos y seguridad ---
        RegistrarNativa("encontrar", (args, u) =>
        {
            ExigirAridad("encontrar", args, 2, u);
            List<ValorAlba> lista = ExigirLista(args[0], "encontrar", u);
            return EncontrarEnLista(lista, args[1], u);
        });

        RegistrarNativa("alguno", (args, u) =>
        {
            ExigirAridad("alguno", args, 2, u);
            List<ValorAlba> lista = ExigirLista(args[0], "alguno", u);
            return AlgunoEnLista(lista, args[1], u);
        });

        RegistrarNativa("todos", (args, u) =>
        {
            ExigirAridad("todos", args, 2, u);
            List<ValorAlba> lista = ExigirLista(args[0], "todos", u);
            return TodosEnLista(lista, args[1], u);
        });

        RegistrarNativa("unicos", (args, u) =>
        {
            ExigirAridad("unicos", args, 1, u);
            List<ValorAlba> lista = ExigirLista(args[0], "unicos", u);
            return UnicosDeLista(lista);
        });

        RegistrarNativa("cortar", (args, u) =>
        {
            if (args.Count is < 2 or > 3)
                throw CrearError("ALB-E011", TipoError.Ejecucion, u, "'cortar' acepta (lista, inicio) o (lista, inicio, fin).");
            List<ValorAlba> lista = ExigirLista(args[0], "cortar", u);
            int inicio = (int)ExigirEntero(args[1], "cortar", u);
            int fin = args.Count == 3 ? (int)ExigirEntero(args[2], "cortar", u) : lista.Count;
            return CortarLista(lista, inicio, fin);
        });

        RegistrarNativa("es_numero", (args, u) =>
        {
            ExigirAridad("es_numero", args, 1, u);
            return ValorAlba.CrearBooleano(args[0].EsNumero);
        });

        RegistrarNativa("es_texto", (args, u) =>
        {
            ExigirAridad("es_texto", args, 1, u);
            return ValorAlba.CrearBooleano(args[0].Tipo == TipoValorAlba.Texto);
        });

        RegistrarNativa("es_lista", (args, u) =>
        {
            ExigirAridad("es_lista", args, 1, u);
            return ValorAlba.CrearBooleano(args[0].Tipo == TipoValorAlba.Lista);
        });

        RegistrarNativa("es_diccionario", (args, u) =>
        {
            ExigirAridad("es_diccionario", args, 1, u);
            return ValorAlba.CrearBooleano(args[0].Tipo == TipoValorAlba.Diccionario);
        });

        RegistrarNativa("es_nulo", (args, u) =>
        {
            ExigirAridad("es_nulo", args, 1, u);
            return ValorAlba.CrearBooleano(args[0].Tipo == TipoValorAlba.Nulo);
        });

        RegistrarNativa("sha256", (args, u) =>
        {
            ExigirAridad("sha256", args, 1, u);
            string entrada = ExigirTexto(args[0], "sha256", u);
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(entrada));
            return ValorAlba.CrearTexto(Convert.ToHexString(hash).ToLowerInvariant());
        });

        RegistrarNativa("uuid", (args, u) =>
        {
            ExigirAridad("uuid", args, 0, u);
            return ValorAlba.CrearTexto(Guid.NewGuid().ToString("D"));
        });

        RegistrarNativa("base64_codificar", (args, u) =>
        {
            ExigirAridad("base64_codificar", args, 1, u);
            string entrada = ExigirTexto(args[0], "base64_codificar", u);
            return ValorAlba.CrearTexto(Convert.ToBase64String(Encoding.UTF8.GetBytes(entrada)));
        });

        RegistrarNativa("base64_decodificar", (args, u) =>
        {
            ExigirAridad("base64_decodificar", args, 1, u);
            string b64 = ExigirTexto(args[0], "base64_decodificar", u);
            try
            {
                byte[] bytes = Convert.FromBase64String(b64);
                return ValorAlba.CrearTexto(Encoding.UTF8.GetString(bytes));
            }
            catch (FormatException)
            {
                throw CrearError("ALB-E014", TipoError.Ejecucion, u, "Cadena Base64 inválida en 'base64_decodificar'.");
            }
        });
    }

    // --- Métodos integrados por punto (OOP ligera sobre tipos primitivos/colecciones) ---

    private ValorAlba ResolverMiembroLista(List<ValorAlba> lista, string miembro, UbicacionFuente ubicacion)
    {
        return miembro switch
        {
            "longitud" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("lista.longitud", (args, u) =>
            {
                ExigirAridad("longitud", args, 0, u);
                return ValorAlba.CrearEntero(lista.Count);
            })),
            "agregar" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("lista.agregar", (args, u) =>
            {
                ExigirAridad("agregar", args, 1, u);
                lista.Add(args[0]);
                return ValorAlba.Nulo;
            })),
            "eliminar" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("lista.eliminar", (args, u) =>
            {
                ExigirAridad("eliminar", args, 1, u);
                int pos = ConvertirIndiceLista(ExigirEntero(args[0], "eliminar", u), lista.Count, u);
                ValorAlba el = lista[pos];
                lista.RemoveAt(pos);
                return el;
            })),
            "contiene" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("lista.contiene", (args, u) =>
            {
                ExigirAridad("contiene", args, 1, u);
                return ValorAlba.CrearBooleano(lista.Any(x => x.EsIgualA(args[0])));
            })),
            "indice_de" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("lista.indice_de", (args, u) =>
            {
                ExigirAridad("indice_de", args, 1, u);
                for (int i = 0; i < lista.Count; i++)
                {
                    if (lista[i].EsIgualA(args[0])) return ValorAlba.CrearEntero(i);
                }
                return ValorAlba.CrearEntero(-1);
            })),
            "invertir" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("lista.invertir", (args, u) =>
            {
                ExigirAridad("invertir", args, 0, u);
                var copia = new List<ValorAlba>(lista);
                copia.Reverse();
                return ValorAlba.CrearLista(copia);
            })),
            "ordenar" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("lista.ordenar", (args, u) =>
            {
                ExigirAridad("ordenar", args, 0, u);
                return OrdenarListaCopia(lista, u);
            })),
            "unir" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("lista.unir", (args, u) =>
            {
                ExigirAridad("unir", args, 1, u);
                string sep = ExigirTexto(args[0], "unir", u);
                return ValorAlba.CrearTexto(string.Join(sep, lista.Select(x => x.ATextoParaImprimir())));
            })),
            "mapear" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("lista.mapear", (args, u) =>
            {
                ExigirAridad("mapear", args, 1, u);
                return MapearLista(lista, args[0], u);
            })),
            "filtrar" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("lista.filtrar", (args, u) =>
            {
                ExigirAridad("filtrar", args, 1, u);
                return FiltrarLista(lista, args[0], u);
            })),
            "reducir" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("lista.reducir", (args, u) =>
            {
                ExigirAridad("reducir", args, 2, u);
                return ReducirLista(lista, args[0], args[1], u);
            })),
            "encontrar" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("lista.encontrar", (args, u) =>
            {
                ExigirAridad("encontrar", args, 1, u);
                return EncontrarEnLista(lista, args[0], u);
            })),
            "alguno" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("lista.alguno", (args, u) =>
            {
                ExigirAridad("alguno", args, 1, u);
                return AlgunoEnLista(lista, args[0], u);
            })),
            "todos" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("lista.todos", (args, u) =>
            {
                ExigirAridad("todos", args, 1, u);
                return TodosEnLista(lista, args[0], u);
            })),
            "unicos" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("lista.unicos", (args, u) =>
            {
                ExigirAridad("unicos", args, 0, u);
                return UnicosDeLista(lista);
            })),
            "cortar" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("lista.cortar", (args, u) =>
            {
                if (args.Count is < 1 or > 2)
                    throw CrearError("ALB-E011", TipoError.Ejecucion, u, "'lista.cortar' acepta (inicio) o (inicio, fin).");
                int inicio = (int)ExigirEntero(args[0], "cortar", u);
                int fin = args.Count == 2 ? (int)ExigirEntero(args[1], "cortar", u) : lista.Count;
                return CortarLista(lista, inicio, fin);
            })),
            _ => throw CrearError("ALB-E009", TipoError.Ejecucion, ubicacion,
                $"La lista no posee ningún método o propiedad llamado '{miembro}'.")
        };
    }

    private ValorAlba ResolverMiembroTexto(string texto, string miembro, UbicacionFuente ubicacion)
    {
        return miembro switch
        {
            "longitud" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("texto.longitud", (args, u) =>
            {
                ExigirAridad("longitud", args, 0, u);
                return ValorAlba.CrearEntero(texto.Length);
            })),
            "mayusculas" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("texto.mayusculas", (args, u) =>
            {
                ExigirAridad("mayusculas", args, 0, u);
                return ValorAlba.CrearTexto(texto.ToUpperInvariant());
            })),
            "minusculas" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("texto.minusculas", (args, u) =>
            {
                ExigirAridad("minusculas", args, 0, u);
                return ValorAlba.CrearTexto(texto.ToLowerInvariant());
            })),
            "recortar" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("texto.recortar", (args, u) =>
            {
                ExigirAridad("recortar", args, 0, u);
                return ValorAlba.CrearTexto(texto.Trim());
            })),
            "dividir" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("texto.dividir", (args, u) =>
            {
                ExigirAridad("dividir", args, 1, u);
                string sep = ExigirTexto(args[0], "dividir", u);
                string[] partes = sep.Length == 0
                    ? texto.Select(c => c.ToString()).ToArray()
                    : texto.Split(sep, StringSplitOptions.None);
                return ValorAlba.CrearLista(partes.Select(ValorAlba.CrearTexto).ToList());
            })),
            "reemplazar" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("texto.reemplazar", (args, u) =>
            {
                ExigirAridad("reemplazar", args, 2, u);
                string buscar = ExigirTexto(args[0], "reemplazar", u);
                string nuevo = ExigirTexto(args[1], "reemplazar", u);
                return ValorAlba.CrearTexto(buscar.Length == 0 ? texto : texto.Replace(buscar, nuevo, StringComparison.Ordinal));
            })),
            "contiene" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("texto.contiene", (args, u) =>
            {
                ExigirAridad("contiene", args, 1, u);
                return ValorAlba.CrearBooleano(texto.Contains(ExigirTexto(args[0], "contiene", u), StringComparison.Ordinal));
            })),
            "empieza_con" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("texto.empieza_con", (args, u) =>
            {
                ExigirAridad("empieza_con", args, 1, u);
                return ValorAlba.CrearBooleano(texto.StartsWith(ExigirTexto(args[0], "empieza_con", u), StringComparison.Ordinal));
            })),
            "termina_con" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("texto.termina_con", (args, u) =>
            {
                ExigirAridad("termina_con", args, 1, u);
                return ValorAlba.CrearBooleano(texto.EndsWith(ExigirTexto(args[0], "termina_con", u), StringComparison.Ordinal));
            })),
            "subtexto" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("texto.subtexto", (args, u) =>
            {
                if (args.Count is < 1 or > 2)
                    throw CrearError("ALB-E011", TipoError.Ejecucion, u, "'subtexto' acepta (inicio) o (inicio, largo).");
                int inicio = (int)ExigirEntero(args[0], "subtexto", u);
                if (inicio < 0 || inicio > texto.Length)
                    throw CrearError("ALB-E006", TipoError.Ejecucion, u, $"Índice inicial {inicio} fuera de rango.");
                if (args.Count == 1)
                    return ValorAlba.CrearTexto(texto[inicio..]);
                int largo = (int)ExigirEntero(args[1], "subtexto", u);
                if (largo < 0 || inicio + largo > texto.Length)
                    throw CrearError("ALB-E006", TipoError.Ejecucion, u, $"Subtexto ({inicio}, {largo}) fuera de rango.");
                return ValorAlba.CrearTexto(texto.Substring(inicio, largo));
            })),
            _ => throw CrearError("ALB-E009", TipoError.Ejecucion, ubicacion,
                $"El tipo 'texto' no posee ningún método llamado '{miembro}'.")
        };
    }

    private ValorAlba ResolverMiembroDiccionario(
        Dictionary<string, ValorAlba> dict,
        string miembro,
        UbicacionFuente ubicacion,
        bool esOpcional = false)
    {
        if (dict.TryGetValue(miembro, out ValorAlba? valor))
        {
            return valor;
        }

        return miembro switch
        {
            "longitud" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("diccionario.longitud", (args, u) =>
            {
                ExigirAridad("longitud", args, 0, u);
                return ValorAlba.CrearEntero(dict.Count);
            })),
            "claves" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("diccionario.claves", (args, u) =>
            {
                ExigirAridad("claves", args, 0, u);
                return ValorAlba.CrearLista(dict.Keys.Select(ValorAlba.CrearTexto).ToList());
            })),
            "valores" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("diccionario.valores", (args, u) =>
            {
                ExigirAridad("valores", args, 0, u);
                return ValorAlba.CrearLista(dict.Values.ToList());
            })),
            "contiene" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("diccionario.contiene", (args, u) =>
            {
                ExigirAridad("contiene", args, 1, u);
                return ValorAlba.CrearBooleano(dict.ContainsKey(ExigirTexto(args[0], "contiene", u)));
            })),
            "eliminar" => ValorAlba.CrearFuncionNativa(new FuncionNativaAlba("diccionario.eliminar", (args, u) =>
            {
                ExigirAridad("eliminar", args, 1, u);
                return ValorAlba.CrearBooleano(dict.Remove(ExigirTexto(args[0], "eliminar", u)));
            })),
            _ when esOpcional => ValorAlba.Nulo,
            _ => throw CrearError("ALB-E009", TipoError.Ejecucion, ubicacion,
                $"El diccionario no contiene la clave ni el método '{miembro}'.")
        };
    }

    private ValorAlba EncontrarEnLista(List<ValorAlba> lista, ValorAlba predicado, UbicacionFuente u)
    {
        foreach (ValorAlba el in lista)
        {
            ValorAlba coincide = InvocarCallable(predicado, [el], u);
            if (coincide.Tipo != TipoValorAlba.Booleano)
            {
                throw CrearError("ALB-T004", TipoError.Tipo, u,
                    $"El predicado de 'encontrar' debe retornar 'booleano', retornó '{coincide.ObtenerNombreTipo()}'.");
            }
            if (coincide.ComoBooleano())
            {
                return el;
            }
        }
        return ValorAlba.Nulo;
    }

    private ValorAlba AlgunoEnLista(List<ValorAlba> lista, ValorAlba predicado, UbicacionFuente u)
    {
        foreach (ValorAlba el in lista)
        {
            ValorAlba coincide = InvocarCallable(predicado, [el], u);
            if (coincide.Tipo != TipoValorAlba.Booleano)
            {
                throw CrearError("ALB-T004", TipoError.Tipo, u,
                    $"El predicado de 'alguno' debe retornar 'booleano', retornó '{coincide.ObtenerNombreTipo()}'.");
            }
            if (coincide.ComoBooleano())
            {
                return ValorAlba.Verdadero;
            }
        }
        return ValorAlba.Falso;
    }

    private ValorAlba TodosEnLista(List<ValorAlba> lista, ValorAlba predicado, UbicacionFuente u)
    {
        foreach (ValorAlba el in lista)
        {
            ValorAlba coincide = InvocarCallable(predicado, [el], u);
            if (coincide.Tipo != TipoValorAlba.Booleano)
            {
                throw CrearError("ALB-T004", TipoError.Tipo, u,
                    $"El predicado de 'todos' debe retornar 'booleano', retornó '{coincide.ObtenerNombreTipo()}'.");
            }
            if (!coincide.ComoBooleano())
            {
                return ValorAlba.Falso;
            }
        }
        return ValorAlba.Verdadero;
    }

    private static ValorAlba UnicosDeLista(List<ValorAlba> lista)
    {
        var resultado = new List<ValorAlba>();
        foreach (ValorAlba el in lista)
        {
            if (!resultado.Any(x => x.EsIgualA(el)))
            {
                resultado.Add(el);
            }
        }
        return ValorAlba.CrearLista(resultado);
    }

    private static ValorAlba CortarLista(List<ValorAlba> lista, int inicio, int fin)
    {
        int start = Math.Clamp(inicio, 0, lista.Count);
        int end = Math.Clamp(fin, start, lista.Count);
        return ValorAlba.CrearLista(lista.GetRange(start, end - start));
    }

    private ValorAlba MapearLista(List<ValorAlba> lista, ValorAlba transformador, UbicacionFuente u)
    {
        var resultado = new List<ValorAlba>(lista.Count);
        foreach (ValorAlba el in lista)
        {
            resultado.Add(InvocarCallable(transformador, [el], u));
        }
        return ValorAlba.CrearLista(resultado);
    }

    private ValorAlba FiltrarLista(List<ValorAlba> lista, ValorAlba predicado, UbicacionFuente u)
    {
        var resultado = new List<ValorAlba>();
        foreach (ValorAlba el in lista)
        {
            ValorAlba keep = InvocarCallable(predicado, [el], u);
            if (keep.Tipo != TipoValorAlba.Booleano)
            {
                throw CrearError("ALB-T004", TipoError.Tipo, u,
                    $"El predicado de 'filtrar' debe retornar 'booleano', retornó '{keep.ObtenerNombreTipo()}'.");
            }
            if (keep.ComoBooleano())
            {
                resultado.Add(el);
            }
        }
        return ValorAlba.CrearLista(resultado);
    }

    private ValorAlba ReducirLista(List<ValorAlba> lista, ValorAlba inicial, ValorAlba reductor, UbicacionFuente u)
    {
        ValorAlba acumulador = inicial;
        foreach (ValorAlba el in lista)
        {
            acumulador = InvocarCallable(reductor, [acumulador, el], u);
        }
        return acumulador;
    }

    private ValorAlba OrdenarListaCopia(List<ValorAlba> lista, UbicacionFuente u)
    {
        var copia = new List<ValorAlba>(lista);
        copia.Sort((a, b) =>
        {
            if (a.EsNumero && b.EsNumero)
            {
                return a.ComoNumeroDouble().CompareTo(b.ComoNumeroDouble());
            }
            if (a.Tipo == TipoValorAlba.Texto && b.Tipo == TipoValorAlba.Texto)
            {
                return string.CompareOrdinal(a.ComoTexto(), b.ComoTexto());
            }
            throw CrearError("ALB-T004", TipoError.Tipo, u,
                $"No se pueden comparar elementos '{a.ObtenerNombreTipo()}' y '{b.ObtenerNombreTipo()}' al ordenar.");
        });
        return ValorAlba.CrearLista(copia);
    }

    private string ResolverRutaArchivoIO(string ruta)
    {
        if (Path.IsPathRooted(ruta))
        {
            return ruta;
        }
        return Path.GetFullPath(Path.Combine(ObtenerDirectorioBaseActual(), ruta));
    }

    private ModuloAlba? IntentarConstruirModuloVirtual(string rutaModulo)
    {
        string clave = rutaModulo.Trim().ToLowerInvariant();
        var exp = new Dictionary<string, ValorAlba>(StringComparer.Ordinal);

        switch (clave)
        {
            case "matematicas":
                exp["PI"] = ValorAlba.CrearDecimal(Math.PI);
                exp["E"] = ValorAlba.CrearDecimal(Math.E);
                AgregarExportNativa(exp, "raiz");
                AgregarExportNativa(exp, "abs");
                AgregarExportNativa(exp, "redondear");
                AgregarExportNativa(exp, "piso");
                AgregarExportNativa(exp, "techo");
                AgregarExportNativa(exp, "minimo");
                AgregarExportNativa(exp, "maximo");
                AgregarExportNativa(exp, "seno");
                AgregarExportNativa(exp, "coseno");
                AgregarExportNativa(exp, "tangente");
                AgregarExportNativa(exp, "logaritmo");
                AgregarExportNativa(exp, "aleatorio");
                return new ModuloAlba("matematicas", exp);

            case "texto":
                AgregarExportNativa(exp, "mayusculas");
                AgregarExportNativa(exp, "minusculas");
                AgregarExportNativa(exp, "recortar");
                AgregarExportNativa(exp, "dividir");
                AgregarExportNativa(exp, "unir");
                AgregarExportNativa(exp, "reemplazar");
                AgregarExportNativa(exp, "contiene");
                AgregarExportNativa(exp, "empieza_con");
                AgregarExportNativa(exp, "termina_con");
                AgregarExportNativa(exp, "subtexto");
                AgregarExportNativa(exp, "longitud");
                return new ModuloAlba("texto", exp);

            case "json":
                AgregarExportNativa(exp, "json_a_texto");
                AgregarExportNativa(exp, "texto_a_json");
                exp["serializar"] = exp["json_a_texto"];
                exp["analizar"] = exp["texto_a_json"];
                return new ModuloAlba("json", exp);

            case "archivos":
                AgregarExportNativa(exp, "leer_archivo");
                AgregarExportNativa(exp, "escribir_archivo");
                AgregarExportNativa(exp, "agregar_linea_archivo");
                AgregarExportNativa(exp, "existe_archivo");
                exp["leer"] = exp["leer_archivo"];
                exp["escribir"] = exp["escribir_archivo"];
                exp["existe"] = exp["existe_archivo"];
                return new ModuloAlba("archivos", exp);

            case "seguridad":
                AgregarExportNativa(exp, "sha256");
                AgregarExportNativa(exp, "uuid");
                AgregarExportNativa(exp, "base64_codificar");
                AgregarExportNativa(exp, "base64_decodificar");
                return new ModuloAlba("seguridad", exp);

            case "colecciones":
                AgregarExportNativa(exp, "mapear");
                AgregarExportNativa(exp, "filtrar");
                AgregarExportNativa(exp, "reducir");
                AgregarExportNativa(exp, "ordenar");
                AgregarExportNativa(exp, "invertir");
                AgregarExportNativa(exp, "encontrar");
                AgregarExportNativa(exp, "alguno");
                AgregarExportNativa(exp, "todos");
                AgregarExportNativa(exp, "unicos");
                AgregarExportNativa(exp, "cortar");
                AgregarExportNativa(exp, "contiene_elemento");
                AgregarExportNativa(exp, "indice_de");
                AgregarExportNativa(exp, "rango");
                return new ModuloAlba("colecciones", exp);

            default:
                return null;
        }
    }

    private void AgregarExportNativa(Dictionary<string, ValorAlba> destino, string nombre)
    {
        destino[nombre] = _entornoGlobal.Obtener(nombre, new UbicacionFuente(_rutaArchivo, 1, 1, 1), _codigoFuente);
    }

    private static string ConvertirValorAJson(ValorAlba valor, bool formateado)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = formateado });
        EscribirValorJson(writer, valor);
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void EscribirValorJson(Utf8JsonWriter writer, ValorAlba valor)
    {
        switch (valor.Tipo)
        {
            case TipoValorAlba.Nulo:
                writer.WriteNullValue();
                break;
            case TipoValorAlba.Entero:
                writer.WriteNumberValue(valor.ComoEntero());
                break;
            case TipoValorAlba.Decimal:
                writer.WriteNumberValue(valor.ComoDecimal());
                break;
            case TipoValorAlba.Booleano:
                writer.WriteBooleanValue(valor.ComoBooleano());
                break;
            case TipoValorAlba.Texto:
                writer.WriteStringValue(valor.ComoTexto());
                break;
            case TipoValorAlba.Lista:
                writer.WriteStartArray();
                foreach (ValorAlba el in valor.ComoLista())
                {
                    EscribirValorJson(writer, el);
                }
                writer.WriteEndArray();
                break;
            case TipoValorAlba.Diccionario:
                writer.WriteStartObject();
                foreach (var (clave, val) in valor.ComoDiccionario())
                {
                    writer.WritePropertyName(clave);
                    EscribirValorJson(writer, val);
                }
                writer.WriteEndObject();
                break;
            default:
                writer.WriteStringValue(valor.ATextoParaImprimir());
                break;
        }
    }

    private static ValorAlba ConvertirJsonElementAValor(JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return ValorAlba.Nulo;
            case JsonValueKind.True:
                return ValorAlba.Verdadero;
            case JsonValueKind.False:
                return ValorAlba.Falso;
            case JsonValueKind.String:
                return ValorAlba.CrearTexto(el.GetString() ?? string.Empty);
            case JsonValueKind.Number:
                if (el.TryGetInt64(out long entero))
                    return ValorAlba.CrearEntero(entero);
                return ValorAlba.CrearDecimal(el.GetDouble());
            case JsonValueKind.Array:
            {
                var lista = new List<ValorAlba>(el.GetArrayLength());
                foreach (JsonElement item in el.EnumerateArray())
                {
                    lista.Add(ConvertirJsonElementAValor(item));
                }
                return ValorAlba.CrearLista(lista);
            }
            case JsonValueKind.Object:
            {
                var dict = new Dictionary<string, ValorAlba>(StringComparer.Ordinal);
                foreach (JsonProperty prop in el.EnumerateObject())
                {
                    dict[prop.Name] = ConvertirJsonElementAValor(prop.Value);
                }
                return ValorAlba.CrearDiccionario(dict);
            }
            default:
                return ValorAlba.Nulo;
        }
    }
}
