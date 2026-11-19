using System.Text;
using Alba.Engine.Ast;
using Alba.Engine.Diagnostics;
using Alba.Engine.Parser;

namespace Alba.Engine.Runtime;

/// <summary>
/// Intérprete del Árbol de Sintaxis Abstracta (AST) de Cibao v5.0.0.
/// Soporta ámbitos léxicos, tipado gradual, funciones y lambdas con closures,
/// listas, diccionarios, interpolación de texto, herencia de clases, módulos reales,
/// control de flujo 'elegir/caso/defecto', coalescencia nula ('??') y biblioteca estándar.
/// </summary>
public sealed partial class Interprete
{
    private readonly IConsolaIO _consola;
    private readonly string _codigoFuente;
    private readonly string _rutaArchivo;
    private readonly Entorno _entornoGlobal;
    private readonly Stack<InstanciaAlba> _pilaInstancias = new();
    private readonly Stack<DefinicionClase> _pilaClases = new();
    private readonly Dictionary<string, ModuloAlba> _cacheModulos;
    private readonly HashSet<string> _modulosEnCarga;

    public Interprete(IConsolaIO consola, string codigoFuente = "", string rutaArchivo = "<entrada>")
        : this(
            consola,
            codigoFuente,
            rutaArchivo,
            new Dictionary<string, ModuloAlba>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase))
    {
    }

    private Interprete(
        IConsolaIO consola,
        string codigoFuente,
        string rutaArchivo,
        Dictionary<string, ModuloAlba> cacheModulos,
        HashSet<string> modulosEnCarga)
    {
        _consola = consola ?? throw new ArgumentNullException(nameof(consola));
        _codigoFuente = codigoFuente ?? string.Empty;
        _rutaArchivo = string.IsNullOrWhiteSpace(rutaArchivo) ? "<entrada>" : rutaArchivo;
        _cacheModulos = cacheModulos;
        _modulosEnCarga = modulosEnCarga;
        _entornoGlobal = new Entorno();
        RegistrarBibliotecaEstandar();
    }

    public ValorAlba Ejecutar(ProgramaAst programa)
    {
        ValorAlba ultimo = ValorAlba.Nulo;

        foreach (Sentencia sentencia in programa.Sentencias)
        {
            ultimo = EjecutarSentencia(sentencia, _entornoGlobal);
        }

        return ultimo;
    }

    private ValorAlba EjecutarSentencia(Sentencia sentencia, Entorno entorno)
    {
        switch (sentencia)
        {
            case DeclVariable declVar:
            {
                ValorAlba valor = declVar.Inicializador is not null
                    ? EvaluarExpresion(declVar.Inicializador, entorno)
                    : ValorAlba.ValorPorDefectoParaTipo(declVar.Tipo?.Nombre);

                entorno.Declarar(
                    declVar.Nombre,
                    valor,
                    declVar.Tipo?.Nombre,
                    esConstante: false,
                    declVar.Ubicacion,
                    _codigoFuente
                );
                return ValorAlba.Nulo;
            }

            case DeclConstante declConst:
            {
                ValorAlba valor = EvaluarExpresion(declConst.Valor, entorno);
                entorno.Declarar(
                    declConst.Nombre,
                    valor,
                    declConst.Tipo?.Nombre,
                    esConstante: true,
                    declConst.Ubicacion,
                    _codigoFuente
                );
                return ValorAlba.Nulo;
            }

            case DeclFuncion declFunc:
            {
                var funcion = new FuncionAlba(
                    declFunc.Nombre,
                    declFunc.Parametros,
                    declFunc.TipoRetorno,
                    declFunc.Cuerpo,
                    entorno
                );
                entorno.Declarar(
                    declFunc.Nombre,
                    ValorAlba.CrearFuncion(funcion),
                    tipoRestriccion: null,
                    esConstante: true,
                    declFunc.Ubicacion,
                    _codigoFuente
                );
                return ValorAlba.Nulo;
            }

            case DeclClase declClase:
            {
                DefinicionClase? claseBase = null;
                if (!string.IsNullOrWhiteSpace(declClase.NombreClaseBase))
                {
                    ValorAlba valBase = entorno.Obtener(declClase.NombreClaseBase, declClase.Ubicacion, _codigoFuente);
                    if (valBase.Tipo != TipoValorAlba.Clase)
                    {
                        throw CrearError("ALB-E015", TipoError.Ejecucion, declClase.Ubicacion,
                            $"'{declClase.NombreClaseBase}' no es una clase válida para heredar.");
                    }

                    claseBase = valBase.ComoClase();
                    if (string.Equals(claseBase.Nombre, declClase.Nombre, StringComparison.Ordinal))
                    {
                        throw CrearError("ALB-E015", TipoError.Ejecucion, declClase.Ubicacion,
                            $"La clase '{declClase.Nombre}' no puede heredar de sí misma.");
                    }
                }

                var campos = new Dictionary<string, DeclCampoClase>(StringComparer.Ordinal);
                foreach (DeclCampoClase c in declClase.Campos)
                {
                    if (campos.ContainsKey(c.Nombre))
                    {
                        throw CrearError("ALB-E012", TipoError.Ejecucion, c.Ubicacion,
                            $"Campo duplicado '{c.Nombre}' en la clase '{declClase.Nombre}'.");
                    }
                    campos[c.Nombre] = c;
                }

                var metodos = new Dictionary<string, DeclMetodoClase>(StringComparer.Ordinal);
                foreach (DeclMetodoClase m in declClase.Metodos)
                {
                    if (metodos.ContainsKey(m.Nombre))
                    {
                        throw CrearError("ALB-E012", TipoError.Ejecucion, m.Ubicacion,
                            $"Método duplicado '{m.Nombre}' en la clase '{declClase.Nombre}'.");
                    }
                    metodos[m.Nombre] = m;
                }

                var defClase = new DefinicionClase(
                    declClase.Nombre,
                    campos,
                    declClase.Constructor,
                    metodos,
                    entorno,
                    claseBase
                );

                entorno.Declarar(
                    declClase.Nombre,
                    ValorAlba.CrearClase(defClase),
                    tipoRestriccion: null,
                    esConstante: true,
                    declClase.Ubicacion,
                    _codigoFuente
                );
                return ValorAlba.Nulo;
            }

            case DeclImportarFutura declImp:
                EjecutarImportar(declImp, entorno);
                return ValorAlba.Nulo;

            case SentenciaBloque bloque:
                EjecutarBloque(bloque, new Entorno(entorno));
                return ValorAlba.Nulo;

            case SentenciaExpresion exprStmt:
                return EvaluarExpresion(exprStmt.Expresion, entorno);

            case SentenciaAsignacion asig:
                EjecutarAsignacion(asig, entorno);
                return ValorAlba.Nulo;

            case SentenciaSi siStmt:
                EjecutarSi(siStmt, entorno);
                return ValorAlba.Nulo;

            case SentenciaElegir elegirStmt:
                EjecutarElegir(elegirStmt, entorno);
                return ValorAlba.Nulo;

            case SentenciaMientras mientrasStmt:
                EjecutarMientras(mientrasStmt, entorno);
                return ValorAlba.Nulo;

            case SentenciaPara paraStmt:
                EjecutarPara(paraStmt, entorno);
                return ValorAlba.Nulo;

            case SentenciaRetornar retStmt:
            {
                ValorAlba valor = retStmt.Valor is not null
                    ? EvaluarExpresion(retStmt.Valor, entorno)
                    : ValorAlba.Nulo;
                throw new SenalRetornar(valor);
            }

            case SentenciaRomper:
                throw new SenalRomper();

            case SentenciaContinuar:
                throw new SenalContinuar();

            case SentenciaIntentar intentarStmt:
                EjecutarIntentar(intentarStmt, entorno);
                return ValorAlba.Nulo;

            case SentenciaLanzar lanzarStmt:
            {
                ValorAlba valor = EvaluarExpresion(lanzarStmt.Expresion, entorno);
                throw new ExcepcionLanzadaUsuario(valor, lanzarStmt.Ubicacion);
            }

            case SentenciaImprimir imprimirStmt:
            {
                var partes = new List<string>(imprimirStmt.Argumentos.Count);
                foreach (Expresion arg in imprimirStmt.Argumentos)
                {
                    partes.Add(EvaluarExpresion(arg, entorno).ATextoParaImprimir());
                }
                _consola.EscribirLinea(string.Join(" ", partes));
                return ValorAlba.Nulo;
            }

            default:
                throw CrearError("ALB-E999", TipoError.Ejecucion, sentencia.Ubicacion,
                    $"Tipo de sentencia no soportado: {sentencia.GetType().Name}.");
        }
    }

    private void EjecutarImportar(DeclImportarFutura declImp, Entorno entorno)
    {
        ModuloAlba? moduloVirtual = IntentarConstruirModuloVirtual(declImp.RutaModulo);
        ModuloAlba modulo;

        if (moduloVirtual is not null)
        {
            modulo = moduloVirtual;
        }
        else
        {
            string? rutaResuelta = ResolverRutaModuloArchivo(declImp.RutaModulo);
            if (rutaResuelta is null)
            {
                throw CrearError("ALB-E097", TipoError.Ejecucion, declImp.Ubicacion,
                    $"No se encontró el módulo '{declImp.RutaModulo}'.");
            }

            if (_cacheModulos.TryGetValue(rutaResuelta, out ModuloAlba? modCache))
            {
                modulo = modCache;
            }
            else
            {
                if (!_modulosEnCarga.Add(rutaResuelta))
                {
                    throw CrearError("ALB-E098", TipoError.Ejecucion, declImp.Ubicacion,
                        $"Se detectó una importación circular al cargar '{declImp.RutaModulo}'.");
                }

                try
                {
                    string codigoModulo = File.ReadAllText(rutaResuelta, Encoding.UTF8);
                    var (lexico, sintactico) = AnalizadorSintactico.AnalizarCodigo(codigoModulo, rutaResuelta);

                    if (lexico.TieneErrores)
                    {
                        throw new ExcepcionAlba(lexico.Errores[0]);
                    }
                    if (sintactico.TieneErrores)
                    {
                        throw new ExcepcionAlba(sintactico.Errores[0]);
                    }

                    var interpreteModulo = new Interprete(
                        _consola,
                        codigoModulo,
                        rutaResuelta,
                        _cacheModulos,
                        _modulosEnCarga);

                    interpreteModulo.Ejecutar(sintactico.Programa);

                    string nombreModulo = Path.GetFileNameWithoutExtension(rutaResuelta);
                    modulo = new ModuloAlba(nombreModulo, interpreteModulo._entornoGlobal.ObtenerExportacionesUsuario());
                    _cacheModulos[rutaResuelta] = modulo;
                }
                finally
                {
                    _modulosEnCarga.Remove(rutaResuelta);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(declImp.Alias))
        {
            entorno.Declarar(
                declImp.Alias,
                ValorAlba.CrearModulo(modulo),
                tipoRestriccion: null,
                esConstante: true,
                declImp.Ubicacion,
                _codigoFuente);
        }
        else
        {
            foreach (var (nombre, valor) in modulo.Exportaciones)
            {
                entorno.DeclararDesdeModulo(nombre, valor, declImp.Ubicacion, _codigoFuente);
            }
        }
    }

    private string? ResolverRutaModuloArchivo(string rutaModulo)
    {
        string dirBase = ObtenerDirectorioBaseActual();
        string rutaCandidata = Path.IsPathRooted(rutaModulo)
            ? rutaModulo
            : Path.Combine(dirBase, rutaModulo);

        if (File.Exists(rutaCandidata))
        {
            return Path.GetFullPath(rutaCandidata);
        }

        if (!Path.HasExtension(rutaCandidata))
        {
            string conCibao = rutaCandidata + ".cibao";
            if (File.Exists(conCibao))
            {
                return Path.GetFullPath(conCibao);
            }

            string conAlb = rutaCandidata + ".alb";
            if (File.Exists(conAlb))
            {
                return Path.GetFullPath(conAlb);
            }
        }

        return null;
    }

    private string ObtenerDirectorioBaseActual()
    {
        if (!string.IsNullOrWhiteSpace(_rutaArchivo) &&
            _rutaArchivo != "<entrada>" &&
            _rutaArchivo != "<consola>")
        {
            try
            {
                string? dir = Path.GetDirectoryName(Path.GetFullPath(_rutaArchivo));
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    return dir;
                }
            }
            catch
            {
                // Si el nombre de archivo virtual no es una ruta válida, usa directorio actual
            }
        }

        return Directory.GetCurrentDirectory();
    }

    private void EjecutarBloque(SentenciaBloque bloque, Entorno entornoBloque)
    {
        foreach (Sentencia s in bloque.Sentencias)
        {
            EjecutarSentencia(s, entornoBloque);
        }
    }

    private void EjecutarAsignacion(SentenciaAsignacion asig, Entorno entorno)
    {
        ValorAlba valorDerecha = EvaluarExpresion(asig.Valor, entorno);

        switch (asig.Objetivo)
        {
            case ExprIdentificador id:
            {
                ValorAlba final = asig.Operador == OperadorAsignacion.Simple
                    ? valorDerecha
                    : AplicarOperadorAsignacion(
                        entorno.Obtener(id.Nombre, id.Ubicacion, _codigoFuente),
                        asig.Operador,
                        valorDerecha,
                        asig.Ubicacion
                    );
                entorno.Asignar(id.Nombre, final, asig.Ubicacion, _codigoFuente);
                break;
            }

            case ExprAccesoMiembro miembro:
            {
                ValorAlba obj = EvaluarExpresion(miembro.Objeto, entorno);
                if (obj.Tipo == TipoValorAlba.Diccionario)
                {
                    Dictionary<string, ValorAlba> dict = obj.ComoDiccionario();
                    ValorAlba finalDict;
                    if (asig.Operador == OperadorAsignacion.Simple)
                    {
                        finalDict = valorDerecha;
                    }
                    else
                    {
                        if (!dict.TryGetValue(miembro.NombreMiembro, out ValorAlba? actual))
                        {
                            throw CrearError("ALB-E016", TipoError.Ejecucion, miembro.Ubicacion,
                                $"La clave '{miembro.NombreMiembro}' no existe en el diccionario.");
                        }
                        finalDict = AplicarOperadorAsignacion(actual, asig.Operador, valorDerecha, asig.Ubicacion);
                    }
                    dict[miembro.NombreMiembro] = finalDict;
                    break;
                }

                if (obj.Tipo != TipoValorAlba.Instancia)
                {
                    throw CrearError("ALB-T005", TipoError.Tipo, miembro.Ubicacion,
                        $"No se puede asignar la propiedad '{miembro.NombreMiembro}' sobre un valor de tipo '{obj.ObtenerNombreTipo()}'.");
                }

                InstanciaAlba instancia = obj.ComoInstancia();
                DefinicionClase? claseActual = _pilaClases.Count > 0 ? _pilaClases.Peek() : null;

                ValorAlba final = asig.Operador == OperadorAsignacion.Simple
                    ? valorDerecha
                    : AplicarOperadorAsignacion(
                        instancia.ObtenerMiembro(miembro.NombreMiembro, claseActual, miembro.Ubicacion, _codigoFuente),
                        asig.Operador,
                        valorDerecha,
                        asig.Ubicacion
                    );

                instancia.AsignarCampo(miembro.NombreMiembro, final, claseActual, asig.Ubicacion, _codigoFuente);
                break;
            }

            case ExprIndice idxExpr:
            {
                ValorAlba coleccion = EvaluarExpresion(idxExpr.Coleccion, entorno);
                ValorAlba indice = EvaluarExpresion(idxExpr.Indice, entorno);

                if (coleccion.Tipo == TipoValorAlba.Diccionario)
                {
                    if (indice.Tipo != TipoValorAlba.Texto)
                    {
                        throw CrearError("ALB-T007", TipoError.Tipo, idxExpr.Indice.Ubicacion,
                            $"La clave de un diccionario debe ser 'texto', se recibió '{indice.ObtenerNombreTipo()}'.");
                    }

                    Dictionary<string, ValorAlba> dict = coleccion.ComoDiccionario();
                    string clave = indice.ComoTexto();

                    ValorAlba finalDict;
                    if (asig.Operador == OperadorAsignacion.Simple)
                    {
                        finalDict = valorDerecha;
                    }
                    else
                    {
                        if (!dict.TryGetValue(clave, out ValorAlba? actual))
                        {
                            throw CrearError("ALB-E016", TipoError.Ejecucion, idxExpr.Indice.Ubicacion,
                                $"La clave '{clave}' no existe en el diccionario.");
                        }
                        finalDict = AplicarOperadorAsignacion(actual, asig.Operador, valorDerecha, asig.Ubicacion);
                    }

                    dict[clave] = finalDict;
                    break;
                }

                if (coleccion.Tipo != TipoValorAlba.Lista)
                {
                    throw CrearError("ALB-T006", TipoError.Tipo, idxExpr.Ubicacion,
                        $"La asignación por índice solo es válida en listas o diccionarios, no en '{coleccion.ObtenerNombreTipo()}'.");
                }
                if (indice.Tipo != TipoValorAlba.Entero)
                {
                    throw CrearError("ALB-T007", TipoError.Tipo, idxExpr.Indice.Ubicacion,
                        $"El índice de una lista debe ser 'entero', se recibió '{indice.ObtenerNombreTipo()}'.");
                }

                List<ValorAlba> lista = coleccion.ComoLista();
                int pos = ConvertirIndiceLista(indice.ComoEntero(), lista.Count, idxExpr.Indice.Ubicacion);

                ValorAlba final = asig.Operador == OperadorAsignacion.Simple
                    ? valorDerecha
                    : AplicarOperadorAsignacion(lista[pos], asig.Operador, valorDerecha, asig.Ubicacion);

                lista[pos] = final;
                break;
            }

            default:
                throw CrearError("ALB-E004", TipoError.Ejecucion, asig.Ubicacion,
                    "Objetivo de asignación inválido en tiempo de ejecución.");
        }
    }

    private ValorAlba AplicarOperadorAsignacion(
        ValorAlba actual,
        OperadorAsignacion op,
        ValorAlba derecha,
        UbicacionFuente ubicacion)
    {
        OperadorBinario bin = op switch
        {
            OperadorAsignacion.Suma => OperadorBinario.Suma,
            OperadorAsignacion.Resta => OperadorBinario.Resta,
            OperadorAsignacion.Multiplicacion => OperadorBinario.Multiplicacion,
            OperadorAsignacion.Division => OperadorBinario.Division,
            _ => throw new InvalidOperationException()
        };
        return AplicarOperadorBinario(actual, bin, derecha, ubicacion);
    }

    private void EjecutarSi(SentenciaSi siStmt, Entorno entorno)
    {
        if (EvaluarCondicionBooleana(siStmt.Condicion, entorno))
        {
            EjecutarBloque(siStmt.BloqueEntonces, new Entorno(entorno));
            return;
        }

        foreach (RamaSinoSi rama in siStmt.RamasSinoSi)
        {
            if (EvaluarCondicionBooleana(rama.Condicion, entorno))
            {
                EjecutarBloque(rama.Bloque, new Entorno(entorno));
                return;
            }
        }

        if (siStmt.BloqueSino is not null)
        {
            EjecutarBloque(siStmt.BloqueSino, new Entorno(entorno));
        }
    }

    private void EjecutarElegir(SentenciaElegir elegirStmt, Entorno entorno)
    {
        ValorAlba objetivo = EvaluarExpresion(elegirStmt.Objetivo, entorno);

        foreach (CasoElegir caso in elegirStmt.Casos)
        {
            foreach (Expresion valExpr in caso.Valores)
            {
                ValorAlba candidato = EvaluarExpresion(valExpr, entorno);
                if (objetivo.EsIgualA(candidato))
                {
                    EjecutarBloque(caso.Cuerpo, new Entorno(entorno));
                    return;
                }
            }
        }

        if (elegirStmt.BloqueDefecto is not null)
        {
            EjecutarBloque(elegirStmt.BloqueDefecto, new Entorno(entorno));
        }
    }

    private void EjecutarMientras(SentenciaMientras mientrasStmt, Entorno entorno)
    {
        while (EvaluarCondicionBooleana(mientrasStmt.Condicion, entorno))
        {
            try
            {
                EjecutarBloque(mientrasStmt.Cuerpo, new Entorno(entorno));
            }
            catch (SenalContinuar)
            {
                continue;
            }
            catch (SenalRomper)
            {
                break;
            }
        }
    }

    private void EjecutarPara(SentenciaPara paraStmt, Entorno entorno)
    {
        ValorAlba iterable = EvaluarExpresion(paraStmt.Iterable, entorno);
        IEnumerable<ValorAlba> secuencia;

        if (iterable.Tipo == TipoValorAlba.Lista)
        {
            // Copia instantánea para evitar modificación concurrente si se muta dentro del ciclo
            secuencia = iterable.ComoLista().ToArray();
        }
        else if (iterable.Tipo == TipoValorAlba.Diccionario)
        {
            var dict = iterable.ComoDiccionario();
            var claves = new ValorAlba[dict.Count];
            int idx = 0;
            foreach (string clave in dict.Keys)
            {
                claves[idx++] = ValorAlba.CrearTexto(clave);
            }
            secuencia = claves;
        }
        else if (iterable.Tipo == TipoValorAlba.Texto)
        {
            string s = iterable.ComoTexto();
            var chars = new ValorAlba[s.Length];
            for (int i = 0; i < s.Length; i++)
            {
                chars[i] = ValorAlba.CrearTexto(s[i].ToString());
            }
            secuencia = chars;
        }
        else
        {
            throw CrearError("ALB-T008", TipoError.Tipo, paraStmt.Iterable.Ubicacion,
                $"No se puede iterar sobre un valor de tipo '{iterable.ObtenerNombreTipo()}'. Se esperaba 'lista', 'diccionario' o 'texto'.");
        }

        foreach (ValorAlba elemento in secuencia)
        {
            var entornoIteracion = new Entorno(entorno);
            entornoIteracion.Declarar(
                paraStmt.VariableIteracion,
                elemento,
                tipoRestriccion: null,
                esConstante: false,
                paraStmt.Ubicacion,
                _codigoFuente
            );

            try
            {
                EjecutarBloque(paraStmt.Cuerpo, entornoIteracion);
            }
            catch (SenalContinuar)
            {
                continue;
            }
            catch (SenalRomper)
            {
                break;
            }
        }
    }

    private void EjecutarIntentar(SentenciaIntentar intentarStmt, Entorno entorno)
    {
        try
        {
            EjecutarBloque(intentarStmt.BloqueIntentar, new Entorno(entorno));
        }
        catch (ExcepcionLanzadaUsuario exUsuario) when (intentarStmt.BloqueCapturar is not null)
        {
            EjecutarBloqueCapturar(intentarStmt.BloqueCapturar, exUsuario.ValorLanzado, entorno);
        }
        catch (ExcepcionAlba exMotor) when (intentarStmt.BloqueCapturar is not null)
        {
            EjecutarBloqueCapturar(
                intentarStmt.BloqueCapturar,
                ValorAlba.CrearTexto(exMotor.Error.Mensaje),
                entorno
            );
        }
        finally
        {
            if (intentarStmt.BloqueFinalmente is not null)
            {
                EjecutarBloque(intentarStmt.BloqueFinalmente, new Entorno(entorno));
            }
        }
    }

    private void EjecutarBloqueCapturar(BloqueCapturar capturar, ValorAlba valorError, Entorno entornoPadre)
    {
        var entornoCap = new Entorno(entornoPadre);
        if (!string.IsNullOrEmpty(capturar.NombreError))
        {
            entornoCap.Declarar(
                capturar.NombreError,
                valorError,
                capturar.TipoError?.Nombre,
                esConstante: false,
                capturar.Ubicacion,
                _codigoFuente
            );
        }
        EjecutarBloque(capturar.Bloque, entornoCap);
    }

    // --- Evaluación de Expresiones ---

    private ValorAlba EvaluarExpresion(Expresion expr, Entorno entorno)
    {
        switch (expr)
        {
            case ExprLiteral lit:
                return lit.TipoLiteral switch
                {
                    TipoLiteralAlba.Entero => ValorAlba.CrearEntero((long)lit.Valor!),
                    TipoLiteralAlba.Decimal => ValorAlba.CrearDecimal((double)lit.Valor!),
                    TipoLiteralAlba.Texto => ValorAlba.CrearTexto((string)lit.Valor!),
                    TipoLiteralAlba.Booleano => ValorAlba.CrearBooleano((bool)lit.Valor!),
                    TipoLiteralAlba.Nulo => ValorAlba.Nulo,
                    _ => ValorAlba.Nulo
                };

            case ExprTextoInterpolado interp:
            {
                var sb = new StringBuilder();
                foreach (ParteInterpolada parte in interp.Partes)
                {
                    if (parte.TextoLiteral is not null)
                    {
                        sb.Append(parte.TextoLiteral);
                    }
                    else if (parte.Expresion is not null)
                    {
                        sb.Append(EvaluarExpresion(parte.Expresion, entorno).ATextoParaImprimir());
                    }
                }
                return ValorAlba.CrearTexto(sb.ToString());
            }

            case ExprIdentificador id:
                return entorno.Obtener(id.Nombre, id.Ubicacion, _codigoFuente);

            case ExprEste este:
                if (_pilaInstancias.Count == 0)
                {
                    throw CrearError("ALB-E013", TipoError.Ejecucion, este.Ubicacion,
                        "No hay una instancia activa para resolver 'este'.");
                }
                return ValorAlba.CrearInstancia(_pilaInstancias.Peek());

            case ExprSuper sup:
                throw CrearError("ALB-E015", TipoError.Ejecucion, sup.Ubicacion,
                    "La referencia 'super' debe usarse como llamada 'super(...)' o acceso 'super.metodo(...)'.");

            case ExprAgrupada agr:
                return EvaluarExpresion(agr.Interna, entorno);

            case ExprUnaria unaria:
                return EvaluarUnaria(unaria, entorno);

            case ExprBinaria binaria:
                return EvaluarBinaria(binaria, entorno);

            case ExprComprobacionTipo comp:
            {
                ValorAlba valor = EvaluarExpresion(comp.Objetivo, entorno);
                bool coincide = comp.TipoEsperado.Nombre == "nulo"
                    ? valor.Tipo == TipoValorAlba.Nulo
                    : valor.Tipo != TipoValorAlba.Nulo && valor.EsCompatibleConAnotacion(comp.TipoEsperado.Nombre);
                return ValorAlba.CrearBooleano(coincide);
            }

            case ExprLlamada llamada:
                return EvaluarLlamada(llamada, entorno);

            case ExprAccesoMiembro acceso:
                return EvaluarAccesoMiembro(acceso, entorno);

            case ExprIndice idx:
                return EvaluarIndice(idx, entorno);

            case ExprLista lista:
            {
                var elementos = new List<ValorAlba>(lista.Elementos.Count);
                foreach (Expresion el in lista.Elementos)
                {
                    elementos.Add(EvaluarExpresion(el, entorno));
                }
                return ValorAlba.CrearLista(elementos);
            }

            case ExprDiccionario dictExpr:
            {
                var dict = new Dictionary<string, ValorAlba>(StringComparer.Ordinal);
                foreach (EntradaDiccionario entrada in dictExpr.Entradas)
                {
                    ValorAlba clave = EvaluarExpresion(entrada.Clave, entorno);
                    if (clave.Tipo != TipoValorAlba.Texto)
                    {
                        throw CrearError("ALB-T007", TipoError.Tipo, entrada.Clave.Ubicacion,
                            $"La clave de un diccionario debe ser 'texto', se recibió '{clave.ObtenerNombreTipo()}'.");
                    }
                    dict[clave.ComoTexto()] = EvaluarExpresion(entrada.Valor, entorno);
                }
                return ValorAlba.CrearDiccionario(dict);
            }

            case ExprFuncionAnonima fnAnon:
            {
                var fn = new FuncionAlba(
                    "<anonima>",
                    fnAnon.Parametros,
                    fnAnon.TipoRetorno,
                    fnAnon.Cuerpo,
                    entorno
                );
                return ValorAlba.CrearFuncion(fn);
            }

            case ExprNuevo nuevo:
                return EvaluarNuevo(nuevo, entorno);

            case ExprLeer leer:
            {
                string? indicador = leer.Mensaje is not null
                    ? EvaluarExpresion(leer.Mensaje, entorno).ATextoParaImprimir()
                    : null;
                string entrada = _consola.LeerLinea(indicador) ?? string.Empty;
                return ValorAlba.CrearTexto(entrada);
            }

            default:
                throw CrearError("ALB-E999", TipoError.Ejecucion, expr.Ubicacion,
                    $"Expresión no soportada: {expr.GetType().Name}.");
        }
    }

    private ValorAlba EvaluarAccesoMiembro(ExprAccesoMiembro acceso, Entorno entorno)
    {
        if (acceso.Objeto is ExprSuper exprSuper)
        {
            if (_pilaInstancias.Count == 0 || _pilaClases.Count == 0)
            {
                throw CrearError("ALB-E013", TipoError.Ejecucion, exprSuper.Ubicacion,
                    "No hay una instancia o clase activa para resolver 'super'.");
            }

            DefinicionClase claseActual = _pilaClases.Peek();
            if (claseActual.ClaseBase is null)
            {
                throw CrearError("ALB-E015", TipoError.Ejecucion, exprSuper.Ubicacion,
                    $"La clase '{claseActual.Nombre}' no tiene una clase base para resolver 'super.{acceso.NombreMiembro}'.");
            }

            InstanciaAlba instanciaActual = _pilaInstancias.Peek();
            return instanciaActual.ObtenerMetodoSuper(
                claseActual.ClaseBase,
                acceso.NombreMiembro,
                claseActual,
                acceso.Ubicacion,
                _codigoFuente);
        }

        ValorAlba obj = EvaluarExpresion(acceso.Objeto, entorno);

        switch (obj.Tipo)
        {
            case TipoValorAlba.Instancia:
            {
                DefinicionClase? claseActual = _pilaClases.Count > 0 ? _pilaClases.Peek() : null;
                return obj.ComoInstancia().ObtenerMiembro(acceso.NombreMiembro, claseActual, acceso.Ubicacion, _codigoFuente);
            }

            case TipoValorAlba.Modulo:
            {
                ModuloAlba modulo = obj.ComoModulo();
                if (modulo.Exportaciones.TryGetValue(acceso.NombreMiembro, out ValorAlba? exportado))
                {
                    return exportado;
                }
                throw CrearError("ALB-E009", TipoError.Ejecucion, acceso.Ubicacion,
                    $"El módulo '{modulo.Nombre}' no exporta '{acceso.NombreMiembro}'.");
            }

            case TipoValorAlba.Diccionario:
                return ResolverMiembroDiccionario(obj.ComoDiccionario(), acceso.NombreMiembro, acceso.Ubicacion);

            case TipoValorAlba.Lista:
                return ResolverMiembroLista(obj.ComoLista(), acceso.NombreMiembro, acceso.Ubicacion);

            case TipoValorAlba.Texto:
                return ResolverMiembroTexto(obj.ComoTexto(), acceso.NombreMiembro, acceso.Ubicacion);

            default:
                throw CrearError("ALB-T005", TipoError.Tipo, acceso.Ubicacion,
                    $"No se puede acceder al miembro '{acceso.NombreMiembro}' sobre un valor de tipo '{obj.ObtenerNombreTipo()}'.");
        }
    }

    private ValorAlba EvaluarUnaria(ExprUnaria unaria, Entorno entorno)
    {
        ValorAlba val = EvaluarExpresion(unaria.Operando, entorno);

        return unaria.Operador switch
        {
            OperadorUnario.NegacionAritmetica => val.Tipo switch
            {
                TipoValorAlba.Entero => ValorAlba.CrearEntero(-val.ComoEntero()),
                TipoValorAlba.Decimal => ValorAlba.CrearDecimal(-val.ComoDecimal()),
                _ => throw CrearError("ALB-T004", TipoError.Tipo, unaria.Ubicacion,
                    $"El operador unario '-' requiere un número, se recibió '{val.ObtenerNombreTipo()}'.")
            },

            OperadorUnario.NegacionLogica => val.Tipo == TipoValorAlba.Booleano
                ? ValorAlba.CrearBooleano(!val.ComoBooleano())
                : throw CrearError("ALB-T004", TipoError.Tipo, unaria.Ubicacion,
                    $"El operador lógico 'no' requiere un booleano, se recibió '{val.ObtenerNombreTipo()}'."),

            _ => throw new InvalidOperationException()
        };
    }

    private ValorAlba EvaluarBinaria(ExprBinaria binaria, Entorno entorno)
    {
        // Cortocircuito para coalescencia nula '??'
        if (binaria.Operador == OperadorBinario.NuloCoalescente)
        {
            ValorAlba izq = EvaluarExpresion(binaria.Izquierda, entorno);
            return izq.Tipo != TipoValorAlba.Nulo
                ? izq
                : EvaluarExpresion(binaria.Derecha, entorno);
        }

        // Cortocircuito estricto para operadores lógicos 'y' / 'o'
        if (binaria.Operador == OperadorBinario.LogicoY)
        {
            ValorAlba izq = EvaluarExpresion(binaria.Izquierda, entorno);
            ValidarBooleano(izq, binaria.Izquierda.Ubicacion, "y");
            if (!izq.ComoBooleano())
            {
                return ValorAlba.Falso;
            }
            ValorAlba der = EvaluarExpresion(binaria.Derecha, entorno);
            ValidarBooleano(der, binaria.Derecha.Ubicacion, "y");
            return ValorAlba.CrearBooleano(der.ComoBooleano());
        }

        if (binaria.Operador == OperadorBinario.LogicoO)
        {
            ValorAlba izq = EvaluarExpresion(binaria.Izquierda, entorno);
            ValidarBooleano(izq, binaria.Izquierda.Ubicacion, "o");
            if (izq.ComoBooleano())
            {
                return ValorAlba.Verdadero;
            }
            ValorAlba der = EvaluarExpresion(binaria.Derecha, entorno);
            ValidarBooleano(der, binaria.Derecha.Ubicacion, "o");
            return ValorAlba.CrearBooleano(der.ComoBooleano());
        }

        ValorAlba izquierda = EvaluarExpresion(binaria.Izquierda, entorno);
        ValorAlba derecha = EvaluarExpresion(binaria.Derecha, entorno);
        return AplicarOperadorBinario(izquierda, binaria.Operador, derecha, binaria.Ubicacion);
    }

    private ValorAlba AplicarOperadorBinario(
        ValorAlba izquierda,
        OperadorBinario operador,
        ValorAlba derecha,
        UbicacionFuente ubicacion)
    {
        switch (operador)
        {
            case OperadorBinario.Suma:
                if (izquierda.Tipo == TipoValorAlba.Texto || derecha.Tipo == TipoValorAlba.Texto)
                {
                    return ValorAlba.CrearTexto(izquierda.ATextoParaImprimir() + derecha.ATextoParaImprimir());
                }
                if (izquierda.EsNumero && derecha.EsNumero)
                {
                    if (izquierda.Tipo == TipoValorAlba.Entero && derecha.Tipo == TipoValorAlba.Entero)
                        return ValorAlba.CrearEntero(izquierda.ComoEntero() + derecha.ComoEntero());
                    return ValorAlba.CrearDecimal(izquierda.ComoNumeroDouble() + derecha.ComoNumeroDouble());
                }
                break;

            case OperadorBinario.Resta:
                if (izquierda.EsNumero && derecha.EsNumero)
                {
                    if (izquierda.Tipo == TipoValorAlba.Entero && derecha.Tipo == TipoValorAlba.Entero)
                        return ValorAlba.CrearEntero(izquierda.ComoEntero() - derecha.ComoEntero());
                    return ValorAlba.CrearDecimal(izquierda.ComoNumeroDouble() - derecha.ComoNumeroDouble());
                }
                break;

            case OperadorBinario.Multiplicacion:
                if (izquierda.EsNumero && derecha.EsNumero)
                {
                    if (izquierda.Tipo == TipoValorAlba.Entero && derecha.Tipo == TipoValorAlba.Entero)
                        return ValorAlba.CrearEntero(izquierda.ComoEntero() * derecha.ComoEntero());
                    return ValorAlba.CrearDecimal(izquierda.ComoNumeroDouble() * derecha.ComoNumeroDouble());
                }
                break;

            case OperadorBinario.Division:
                if (izquierda.EsNumero && derecha.EsNumero)
                {
                    double divisor = derecha.ComoNumeroDouble();
                    if (divisor == 0.0)
                    {
                        throw CrearError("ALB-E005", TipoError.Ejecucion, ubicacion, "División por cero.");
                    }
                    return ValorAlba.CrearDecimal(izquierda.ComoNumeroDouble() / divisor);
                }
                break;

            case OperadorBinario.Modulo:
                if (izquierda.EsNumero && derecha.EsNumero)
                {
                    double divisor = derecha.ComoNumeroDouble();
                    if (divisor == 0.0)
                    {
                        throw CrearError("ALB-E005", TipoError.Ejecucion, ubicacion, "Módulo por cero.");
                    }
                    if (izquierda.Tipo == TipoValorAlba.Entero && derecha.Tipo == TipoValorAlba.Entero)
                        return ValorAlba.CrearEntero(izquierda.ComoEntero() % derecha.ComoEntero());
                    return ValorAlba.CrearDecimal(izquierda.ComoNumeroDouble() % divisor);
                }
                break;

            case OperadorBinario.Potencia:
                if (izquierda.EsNumero && derecha.EsNumero)
                {
                    if (izquierda.Tipo == TipoValorAlba.Entero
                        && derecha.Tipo == TipoValorAlba.Entero
                        && derecha.ComoEntero() >= 0)
                    {
                        double res = Math.Pow(izquierda.ComoEntero(), derecha.ComoEntero());
                        if (res <= long.MaxValue && res >= long.MinValue)
                            return ValorAlba.CrearEntero((long)res);
                    }
                    return ValorAlba.CrearDecimal(Math.Pow(izquierda.ComoNumeroDouble(), derecha.ComoNumeroDouble()));
                }
                break;

            case OperadorBinario.Igual:
                return ValorAlba.CrearBooleano(izquierda.EsIgualA(derecha));

            case OperadorBinario.Diferente:
                return ValorAlba.CrearBooleano(!izquierda.EsIgualA(derecha));

            case OperadorBinario.Menor:
            case OperadorBinario.MenorIgual:
            case OperadorBinario.Mayor:
            case OperadorBinario.MayorIgual:
                if (izquierda.EsNumero && derecha.EsNumero)
                {
                    double a = izquierda.ComoNumeroDouble();
                    double b = derecha.ComoNumeroDouble();
                    return ValorAlba.CrearBooleano(operador switch
                    {
                        OperadorBinario.Menor => a < b,
                        OperadorBinario.MenorIgual => a <= b,
                        OperadorBinario.Mayor => a > b,
                        OperadorBinario.MayorIgual => a >= b,
                        _ => false
                    });
                }
                if (izquierda.Tipo == TipoValorAlba.Texto && derecha.Tipo == TipoValorAlba.Texto)
                {
                    int cmp = string.CompareOrdinal(izquierda.ComoTexto(), derecha.ComoTexto());
                    return ValorAlba.CrearBooleano(operador switch
                    {
                        OperadorBinario.Menor => cmp < 0,
                        OperadorBinario.MenorIgual => cmp <= 0,
                        OperadorBinario.Mayor => cmp > 0,
                        OperadorBinario.MayorIgual => cmp >= 0,
                        _ => false
                    });
                }
                break;
        }

        throw CrearError("ALB-T004", TipoError.Tipo, ubicacion,
            $"No se puede aplicar el operador '{operador}' entre '{izquierda.ObtenerNombreTipo()}' y '{derecha.ObtenerNombreTipo()}'.");
    }

    private ValorAlba EvaluarLlamada(ExprLlamada llamada, Entorno entorno)
    {
        // Invocación directa super(...) dentro del constructor de una subclase
        if (llamada.Objetivo is ExprSuper exprSuper)
        {
            if (_pilaInstancias.Count == 0 || _pilaClases.Count == 0)
            {
                throw CrearError("ALB-E013", TipoError.Ejecucion, exprSuper.Ubicacion,
                    "No hay una instancia activa para invocar 'super(...)'.");
            }

            DefinicionClase claseActual = _pilaClases.Peek();
            if (claseActual.ClaseBase is null)
            {
                throw CrearError("ALB-E015", TipoError.Ejecucion, exprSuper.Ubicacion,
                    $"La clase '{claseActual.Nombre}' no tiene clase base para invocar 'super(...)'.");
            }

            InstanciaAlba instanciaActual = _pilaInstancias.Peek();
            var argsSuper = new List<ValorAlba>(llamada.Argumentos.Count);
            foreach (Expresion argExpr in llamada.Argumentos)
            {
                argsSuper.Add(EvaluarExpresion(argExpr, entorno));
            }

            InvocarConstructorEnJerarquia(claseActual.ClaseBase, instanciaActual, argsSuper, llamada.Ubicacion);
            return ValorAlba.Nulo;
        }

        ValorAlba objetivo = EvaluarExpresion(llamada.Objetivo, entorno);
        var args = new List<ValorAlba>(llamada.Argumentos.Count);
        foreach (Expresion argExpr in llamada.Argumentos)
        {
            args.Add(EvaluarExpresion(argExpr, entorno));
        }

        return InvocarCallable(objetivo, args, llamada.Ubicacion);
    }

    private ValorAlba InvocarCallable(ValorAlba objetivo, IReadOnlyList<ValorAlba> args, UbicacionFuente ubicacion)
    {
        if (objetivo.Tipo == TipoValorAlba.FuncionNativa)
        {
            return objetivo.ComoFuncionNativa().Invocar(args, ubicacion);
        }

        if (objetivo.Tipo == TipoValorAlba.Funcion)
        {
            return InvocarFuncionAlba(objetivo.ComoFuncion(), args, ubicacion);
        }

        throw CrearError("ALB-T009", TipoError.Tipo, ubicacion,
            $"El valor de tipo '{objetivo.ObtenerNombreTipo()}' no es invocable como función.");
    }

    private ValorAlba InvocarFuncionAlba(
        FuncionAlba func,
        IReadOnlyList<ValorAlba> args,
        UbicacionFuente ubicacionLlamada)
    {
        if (args.Count != func.Parametros.Count)
        {
            throw CrearError("ALB-E011", TipoError.Ejecucion, ubicacionLlamada,
                $"La función '{func.Nombre}' esperaba {func.Parametros.Count} argumento(s), pero recibió {args.Count}.");
        }

        var entornoFunc = new Entorno(func.Closure);
        for (int i = 0; i < func.Parametros.Count; i++)
        {
            ParametroAst param = func.Parametros[i];
            ValorAlba argVal = args[i];

            if (param.Tipo is not null && !argVal.EsCompatibleConAnotacion(param.Tipo.Nombre))
            {
                throw CrearError("ALB-T002", TipoError.Tipo, ubicacionLlamada,
                    $"El parámetro '{param.Nombre}' de '{func.Nombre}' esperaba '{param.Tipo.Nombre}', pero recibió '{argVal.ObtenerNombreTipo()}'.");
            }

            entornoFunc.Declarar(
                param.Nombre,
                argVal,
                param.Tipo?.Nombre,
                esConstante: false,
                param.Ubicacion,
                _codigoFuente
            );
        }

        bool empujoClase = false;
        bool empujoInstancia = false;
        if (func.ClasePropietaria is not null)
        {
            _pilaClases.Push(func.ClasePropietaria);
            empujoClase = true;
        }
        if (func.InstanciaEnlazada is not null)
        {
            _pilaInstancias.Push(func.InstanciaEnlazada);
            empujoInstancia = true;
        }

        try
        {
            EjecutarBloque(func.Cuerpo, entornoFunc);
        }
        catch (SenalRetornar retorno)
        {
            ValorAlba retVal = retorno.Valor;
            if (func.TipoRetorno is not null && !retVal.EsCompatibleConAnotacion(func.TipoRetorno.Nombre))
            {
                throw CrearError("ALB-T003", TipoError.Tipo, ubicacionLlamada,
                    $"La función '{func.Nombre}' declaró retornar '{func.TipoRetorno.Nombre}', pero retornó '{retVal.ObtenerNombreTipo()}'.");
            }
            return retVal.AjustarParaAnotacion(func.TipoRetorno?.Nombre);
        }
        finally
        {
            if (empujoInstancia) _pilaInstancias.Pop();
            if (empujoClase) _pilaClases.Pop();
        }

        if (func.TipoRetorno is not null && func.TipoRetorno.Nombre is not ("vacio" or "nulo"))
        {
            throw CrearError("ALB-T003", TipoError.Tipo, ubicacionLlamada,
                $"La función '{func.Nombre}' declaró retornar '{func.TipoRetorno.Nombre}', pero finalizó sin sentencia 'retornar'.");
        }

        return ValorAlba.Nulo;
    }

    private ValorAlba EvaluarNuevo(ExprNuevo nuevo, Entorno entorno)
    {
        ValorAlba valClase = entorno.Obtener(nuevo.NombreClase, nuevo.Ubicacion, _codigoFuente);
        if (valClase.Tipo != TipoValorAlba.Clase)
        {
            throw CrearError("ALB-T010", TipoError.Tipo, nuevo.Ubicacion,
                $"'{nuevo.NombreClase}' no es una clase válida.");
        }

        DefinicionClase defClase = valClase.ComoClase();
        var instancia = new InstanciaAlba(defClase);

        // Inicializa campos de instancia de la clase base hacia la clase derivada
        var cadenaClases = new Stack<DefinicionClase>();
        for (DefinicionClase? cursor = defClase; cursor is not null; cursor = cursor.ClaseBase)
        {
            cadenaClases.Push(cursor);
        }

        foreach (DefinicionClase nivel in cadenaClases)
        {
            _pilaClases.Push(nivel);
            _pilaInstancias.Push(instancia);
            try
            {
                foreach (DeclCampoClase campo in nivel.Campos.Values)
                {
                    ValorAlba valorCampo = campo.Inicializador is not null
                        ? EvaluarExpresion(campo.Inicializador, nivel.EntornoDefinicion)
                        : ValorAlba.ValorPorDefectoParaTipo(campo.Tipo?.Nombre);

                    if (campo.Tipo is not null && !valorCampo.EsCompatibleConAnotacion(campo.Tipo.Nombre))
                    {
                        throw CrearError("ALB-T001", TipoError.Tipo, campo.Ubicacion,
                            $"No se puede inicializar el campo '{campo.Nombre}' de tipo '{campo.Tipo.Nombre}' con '{valorCampo.ObtenerNombreTipo()}'.");
                    }

                    instancia.InicializarCampo(campo.Nombre, valorCampo, campo.Tipo?.Nombre);
                }
            }
            finally
            {
                _pilaInstancias.Pop();
                _pilaClases.Pop();
            }
        }

        var args = new List<ValorAlba>(nuevo.Argumentos.Count);
        foreach (Expresion argExpr in nuevo.Argumentos)
        {
            args.Add(EvaluarExpresion(argExpr, entorno));
        }

        InvocarConstructorEnJerarquia(defClase, instancia, args, nuevo.Ubicacion);
        return ValorAlba.CrearInstancia(instancia);
    }

    private void InvocarConstructorEnJerarquia(
        DefinicionClase inicio,
        InstanciaAlba instancia,
        IReadOnlyList<ValorAlba> args,
        UbicacionFuente ubicacion)
    {
        DefinicionClase? claseConCtor = inicio;
        while (claseConCtor is not null && claseConCtor.Constructor is null)
        {
            claseConCtor = claseConCtor.ClaseBase;
        }

        if (claseConCtor?.Constructor is not null)
        {
            var ctorFunc = new FuncionAlba(
                $"{claseConCtor.Nombre}.constructor",
                claseConCtor.Constructor.Parametros,
                tipoRetorno: null,
                claseConCtor.Constructor.Cuerpo,
                claseConCtor.EntornoDefinicion,
                claseConCtor,
                instancia
            );
            InvocarFuncionAlba(ctorFunc, args, ubicacion);
        }
        else if (args.Count > 0)
        {
            throw CrearError("ALB-E011", TipoError.Ejecucion, ubicacion,
                $"La clase '{inicio.Nombre}' no define constructor y no acepta {args.Count} argumento(s).");
        }
    }

    private ValorAlba EvaluarIndice(ExprIndice idx, Entorno entorno)
    {
        ValorAlba coleccion = EvaluarExpresion(idx.Coleccion, entorno);
        ValorAlba indice = EvaluarExpresion(idx.Indice, entorno);

        if (coleccion.Tipo == TipoValorAlba.Diccionario)
        {
            if (indice.Tipo != TipoValorAlba.Texto)
            {
                throw CrearError("ALB-T007", TipoError.Tipo, idx.Indice.Ubicacion,
                    $"La clave de un diccionario debe ser 'texto', se recibió '{indice.ObtenerNombreTipo()}'.");
            }

            string clave = indice.ComoTexto();
            if (!coleccion.ComoDiccionario().TryGetValue(clave, out ValorAlba? valor))
            {
                throw CrearError("ALB-E016", TipoError.Ejecucion, idx.Indice.Ubicacion,
                    $"La clave '{clave}' no existe en el diccionario.");
            }
            return valor;
        }

        if (indice.Tipo != TipoValorAlba.Entero)
        {
            throw CrearError("ALB-T007", TipoError.Tipo, idx.Indice.Ubicacion,
                $"El índice debe ser 'entero', se recibió '{indice.ObtenerNombreTipo()}'.");
        }

        if (coleccion.Tipo == TipoValorAlba.Lista)
        {
            List<ValorAlba> lista = coleccion.ComoLista();
            int pos = ConvertirIndiceLista(indice.ComoEntero(), lista.Count, idx.Indice.Ubicacion);
            return lista[pos];
        }

        if (coleccion.Tipo == TipoValorAlba.Texto)
        {
            string s = coleccion.ComoTexto();
            int pos = ConvertirIndiceLista(indice.ComoEntero(), s.Length, idx.Indice.Ubicacion);
            return ValorAlba.CrearTexto(s[pos].ToString());
        }

        throw CrearError("ALB-T006", TipoError.Tipo, idx.Ubicacion,
            $"No se puede indexar un valor de tipo '{coleccion.ObtenerNombreTipo()}'.");
    }

    private int ConvertirIndiceLista(long indice, int longitud, UbicacionFuente ubicacion)
    {
        if (indice < 0 || indice >= longitud)
        {
            throw CrearError("ALB-E006", TipoError.Ejecucion, ubicacion,
                $"Índice {indice} fuera de rango para colección de longitud {longitud}.");
        }
        return (int)indice;
    }

    private bool EvaluarCondicionBooleana(Expresion expr, Entorno entorno)
    {
        ValorAlba valor = EvaluarExpresion(expr, entorno);
        if (valor.Tipo != TipoValorAlba.Booleano)
        {
            throw CrearError("ALB-T004", TipoError.Tipo, expr.Ubicacion,
                $"Se esperaba una condición 'booleano', se recibió '{valor.ObtenerNombreTipo()}'.");
        }
        return valor.ComoBooleano();
    }

    private void ValidarBooleano(ValorAlba valor, UbicacionFuente ubicacion, string operador)
    {
        if (valor.Tipo != TipoValorAlba.Booleano)
        {
            throw CrearError("ALB-T004", TipoError.Tipo, ubicacion,
                $"El operador lógico '{operador}' requiere operandos 'booleano', se recibió '{valor.ObtenerNombreTipo()}'.");
        }
    }

    private void RegistrarNativa(string nombre, Func<IReadOnlyList<ValorAlba>, UbicacionFuente, ValorAlba> impl)
    {
        _entornoGlobal.DeclararInterno(nombre, ValorAlba.CrearFuncionNativa(new FuncionNativaAlba(nombre, impl)));
    }

    private void ExigirAridad(string nombre, IReadOnlyList<ValorAlba> args, int esperada, UbicacionFuente u)
    {
        if (args.Count != esperada)
        {
            throw CrearError("ALB-E011", TipoError.Ejecucion, u,
                $"La función '{nombre}' esperaba {esperada} argumento(s), pero recibió {args.Count}.");
        }
    }

    private long ExigirEntero(ValorAlba valor, string funcion, UbicacionFuente u)
    {
        if (valor.Tipo != TipoValorAlba.Entero)
        {
            throw CrearError("ALB-T002", TipoError.Tipo, u,
                $"'{funcion}' requiere argumentos de tipo 'entero', se recibió '{valor.ObtenerNombreTipo()}'.");
        }
        return valor.ComoEntero();
    }

    private double ExigirNumero(ValorAlba valor, string funcion, UbicacionFuente u)
    {
        if (!valor.EsNumero)
        {
            throw CrearError("ALB-T002", TipoError.Tipo, u,
                $"'{funcion}' requiere argumentos numéricos ('entero' o 'decimal'), se recibió '{valor.ObtenerNombreTipo()}'.");
        }
        return valor.ComoNumeroDouble();
    }

    private string ExigirTexto(ValorAlba valor, string funcion, UbicacionFuente u)
    {
        if (valor.Tipo != TipoValorAlba.Texto)
        {
            throw CrearError("ALB-T002", TipoError.Tipo, u,
                $"'{funcion}' requiere un argumento 'texto', se recibió '{valor.ObtenerNombreTipo()}'.");
        }
        return valor.ComoTexto();
    }

    private List<ValorAlba> ExigirLista(ValorAlba valor, string funcion, UbicacionFuente u)
    {
        if (valor.Tipo != TipoValorAlba.Lista)
        {
            throw CrearError("ALB-T006", TipoError.Tipo, u,
                $"'{funcion}' requiere una 'lista', se recibió '{valor.ObtenerNombreTipo()}'.");
        }
        return valor.ComoLista();
    }

    private Dictionary<string, ValorAlba> ExigirDiccionario(ValorAlba valor, string funcion, UbicacionFuente u)
    {
        if (valor.Tipo != TipoValorAlba.Diccionario)
        {
            throw CrearError("ALB-T006", TipoError.Tipo, u,
                $"'{funcion}' requiere un 'diccionario', se recibió '{valor.ObtenerNombreTipo()}'.");
        }
        return valor.ComoDiccionario();
    }

    private ExcepcionAlba CrearError(string codigo, TipoError tipo, UbicacionFuente ubicacion, string mensaje)
    {
        string linea = FormateadorErrores.ExtraerLinea(_codigoFuente, ubicacion.Linea);
        return new ExcepcionAlba(new ErrorAlba(codigo, tipo, ubicacion, mensaje, linea));
    }
}
