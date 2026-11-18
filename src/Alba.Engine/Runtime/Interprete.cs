using System.Globalization;
using Alba.Engine.Ast;
using Alba.Engine.Diagnostics;

namespace Alba.Engine.Runtime;

/// <summary>
/// Intérprete del Árbol de Sintaxis Abstracta (AST) de Alba v0.1.0.
/// Ejecuta programas completos con soporte de ámbitos léxicos, tipado gradual,
/// funciones, closures, listas, clases con encapsulamiento y manejo de errores.
/// </summary>
public sealed class Interprete
{
    private readonly IConsolaIO _consola;
    private readonly string _codigoFuente;
    private readonly Entorno _entornoGlobal;
    private readonly Stack<InstanciaAlba> _pilaInstancias = new();
    private readonly Stack<DefinicionClase> _pilaClases = new();

    public Interprete(IConsolaIO consola, string codigoFuente = "")
    {
        _consola = consola ?? throw new ArgumentNullException(nameof(consola));
        _codigoFuente = codigoFuente ?? string.Empty;
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
                    entorno
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
                throw CrearError("ALB-E099", TipoError.Ejecucion, declImp.Ubicacion,
                    "Las importaciones modulares ('importar') están reservadas para Alba v0.2.0 y aún no están habilitadas.");

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

                if (coleccion.Tipo != TipoValorAlba.Lista)
                {
                    throw CrearError("ALB-T006", TipoError.Tipo, idxExpr.Ubicacion,
                        $"La asignación por índice solo es válida en listas, no en '{coleccion.ObtenerNombreTipo()}'.");
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
                $"No se puede iterar sobre un valor de tipo '{iterable.ObtenerNombreTipo()}'. Se esperaba 'lista' o 'texto'.");
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

            case ExprIdentificador id:
                return entorno.Obtener(id.Nombre, id.Ubicacion, _codigoFuente);

            case ExprEste este:
                if (_pilaInstancias.Count == 0)
                {
                    throw CrearError("ALB-E013", TipoError.Ejecucion, este.Ubicacion,
                        "No hay una instancia activa para resolver 'este'.");
                }
                return ValorAlba.CrearInstancia(_pilaInstancias.Peek());

            case ExprAgrupada agr:
                return EvaluarExpresion(agr.Interna, entorno);

            case ExprUnaria unaria:
                return EvaluarUnaria(unaria, entorno);

            case ExprBinaria binaria:
                return EvaluarBinaria(binaria, entorno);

            case ExprLlamada llamada:
                return EvaluarLlamada(llamada, entorno);

            case ExprAccesoMiembro acceso:
            {
                ValorAlba obj = EvaluarExpresion(acceso.Objeto, entorno);
                if (obj.Tipo != TipoValorAlba.Instancia)
                {
                    throw CrearError("ALB-T005", TipoError.Tipo, acceso.Ubicacion,
                        $"No se puede acceder al miembro '{acceso.NombreMiembro}' sobre un valor de tipo '{obj.ObtenerNombreTipo()}'.");
                }
                DefinicionClase? claseActual = _pilaClases.Count > 0 ? _pilaClases.Peek() : null;
                return obj.ComoInstancia().ObtenerMiembro(acceso.NombreMiembro, claseActual, acceso.Ubicacion, _codigoFuente);
            }

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
        ValorAlba objetivo = EvaluarExpresion(llamada.Objetivo, entorno);
        var args = new List<ValorAlba>(llamada.Argumentos.Count);
        foreach (Expresion argExpr in llamada.Argumentos)
        {
            args.Add(EvaluarExpresion(argExpr, entorno));
        }

        if (objetivo.Tipo == TipoValorAlba.FuncionNativa)
        {
            return objetivo.ComoFuncionNativa().Invocar(args, llamada.Ubicacion);
        }

        if (objetivo.Tipo == TipoValorAlba.Funcion)
        {
            return InvocarFuncionAlba(objetivo.ComoFuncion(), args, llamada.Ubicacion);
        }

        throw CrearError("ALB-T009", TipoError.Tipo, llamada.Ubicacion,
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

        // Inicializa campos de instancia con valores por defecto o inicializadores declarados
        _pilaClases.Push(defClase);
        _pilaInstancias.Push(instancia);
        try
        {
            foreach (DeclCampoClase campo in defClase.Campos.Values)
            {
                ValorAlba valorCampo = campo.Inicializador is not null
                    ? EvaluarExpresion(campo.Inicializador, defClase.EntornoDefinicion)
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

        var args = new List<ValorAlba>(nuevo.Argumentos.Count);
        foreach (Expresion argExpr in nuevo.Argumentos)
        {
            args.Add(EvaluarExpresion(argExpr, entorno));
        }

        if (defClase.Constructor is not null)
        {
            var ctorFunc = new FuncionAlba(
                $"{defClase.Nombre}.constructor",
                defClase.Constructor.Parametros,
                tipoRetorno: null,
                defClase.Constructor.Cuerpo,
                defClase.EntornoDefinicion,
                defClase,
                instancia
            );
            InvocarFuncionAlba(ctorFunc, args, nuevo.Ubicacion);
        }
        else if (args.Count > 0)
        {
            throw CrearError("ALB-E011", TipoError.Ejecucion, nuevo.Ubicacion,
                $"La clase '{defClase.Nombre}' no define constructor y no acepta {args.Count} argumento(s).");
        }

        return ValorAlba.CrearInstancia(instancia);
    }

    private ValorAlba EvaluarIndice(ExprIndice idx, Entorno entorno)
    {
        ValorAlba coleccion = EvaluarExpresion(idx.Coleccion, entorno);
        ValorAlba indice = EvaluarExpresion(idx.Indice, entorno);

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

    // --- Biblioteca estándar integrada ---

    private void RegistrarBibliotecaEstandar()
    {
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
                TipoValorAlba.Texto => ValorAlba.CrearEntero(args[0].ComoTexto().Length),
                _ => throw CrearError("ALB-T006", TipoError.Tipo, u,
                    $"'longitud' requiere 'lista' o 'texto', se recibió '{args[0].ObtenerNombreTipo()}'.")
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
            if (args[0].Tipo != TipoValorAlba.Lista)
            {
                throw CrearError("ALB-T006", TipoError.Tipo, u,
                    $"El primer argumento de 'agregar' debe ser 'lista', se recibió '{args[0].ObtenerNombreTipo()}'.");
            }
            args[0].ComoLista().Add(args[1]);
            return ValorAlba.Nulo;
        });

        RegistrarNativa("eliminar", (args, u) =>
        {
            ExigirAridad("eliminar", args, 2, u);
            if (args[0].Tipo != TipoValorAlba.Lista)
            {
                throw CrearError("ALB-T006", TipoError.Tipo, u,
                    $"El primer argumento de 'eliminar' debe ser 'lista', se recibió '{args[0].ObtenerNombreTipo()}'.");
            }
            long indice = ExigirEntero(args[1], "eliminar", u);
            List<ValorAlba> lista = args[0].ComoLista();
            int pos = ConvertirIndiceLista(indice, lista.Count, u);
            ValorAlba eliminado = lista[pos];
            lista.RemoveAt(pos);
            return eliminado;
        });
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

    private ExcepcionAlba CrearError(string codigo, TipoError tipo, UbicacionFuente ubicacion, string mensaje)
    {
        string linea = FormateadorErrores.ExtraerLinea(_codigoFuente, ubicacion.Linea);
        return new ExcepcionAlba(new ErrorAlba(codigo, tipo, ubicacion, mensaje, linea));
    }
}
