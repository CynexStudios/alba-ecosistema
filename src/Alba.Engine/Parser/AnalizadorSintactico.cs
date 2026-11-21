using Alba.Engine.Ast;
using Alba.Engine.Diagnostics;
using Alba.Engine.Lexer;

namespace Alba.Engine.Parser;

/// <summary>
/// Resultado del análisis sintáctico con el AST construido y la lista de errores detectados.
/// </summary>
public sealed record ResultadoSintactico(
    ProgramaAst Programa,
    IReadOnlyList<ErrorAlba> Errores
)
{
    public bool TieneErrores => Errores.Count > 0;
}

/// <summary>
/// Analizador sintáctico recursivo descendente para Alba v0.1.0.
/// Implementa la gramática EBNF oficial y valida reglas estructurales estáticas.
/// </summary>
public sealed class AnalizadorSintactico
{
    private sealed class ErrorSincronizacionExcepcion : Exception { }

    private readonly IReadOnlyList<Token> _tokens;
    private readonly string _codigoFuente;
    private readonly string _archivo;
    private readonly List<ErrorAlba> _errores = [];

    private int _posicion;
    private int _profundidadCiclo;
    private int _profundidadFuncion;
    private int _profundidadClase;
    private int _profundidadBloque;

    public AnalizadorSintactico(
        IReadOnlyList<Token> tokens,
        string codigoFuente = "",
        string archivo = "<entrada>")
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _codigoFuente = codigoFuente ?? string.Empty;
        _archivo = string.IsNullOrWhiteSpace(archivo) ? "<entrada>" : archivo;
    }

    /// <summary>
    /// Método de conveniencia que ejecuta Lexer y Parser sobre código fuente en un solo paso.
    /// </summary>
    public static (ResultadoLexico Lexico, ResultadoSintactico Sintactico) AnalizarCodigo(
        string codigoFuente,
        string archivo = "<entrada>")
    {
        var lexer = new AnalizadorLexico(codigoFuente, archivo);
        ResultadoLexico resLexico = lexer.Analizar();
        var parser = new AnalizadorSintactico(resLexico.Tokens, codigoFuente, archivo);
        ResultadoSintactico resSintactico = parser.Analizar();
        return (resLexico, resSintactico);
    }

    public ResultadoSintactico Analizar()
    {
        var sentencias = new List<Sentencia>();
        UbicacionFuente ubicacionInicio = _tokens.Count > 0
            ? _tokens[0].Ubicacion
            : new UbicacionFuente(_archivo, 1, 1, 1);

        SaltarSeparadores();

        while (!EstaAlFinal())
        {
            try
            {
                Sentencia nodo = AnalizarDeclaracionOSentencia(permitirClaseYModulo: true);
                sentencias.Add(nodo);

                if (!EstaAlFinal() && MirarActual().Tipo != TipoToken.LlaveDer)
                {
                    ConsumirSeparadorObligatorio();
                }
            }
            catch (ErrorSincronizacionExcepcion)
            {
                Sincronizar();
            }

            SaltarSeparadores();
        }

        var programa = new ProgramaAst(_archivo, sentencias, ubicacionInicio);
        return new ResultadoSintactico(programa, _errores);
    }

    private Sentencia AnalizarDeclaracionOSentencia(bool permitirClaseYModulo)
    {
        Token actual = MirarActual();

        return actual.Tipo switch
        {
            TipoToken.Variable => AnalizarDeclaracionVariable(),
            TipoToken.Constante => AnalizarDeclaracionConstante(),
            TipoToken.Funcion => AnalizarDeclaracionFuncion(),
            TipoToken.Clase when permitirClaseYModulo => AnalizarDeclaracionClase(),
            TipoToken.Clase => ReportarYAbandonar("ALB-S015", actual.Ubicacion,
                "Las declaraciones de 'clase' solo están permitidas en el nivel superior del archivo."),
            TipoToken.Importar when permitirClaseYModulo => AnalizarDeclaracionImportarFutura(),
            TipoToken.Importar => ReportarYAbandonar("ALB-S016", actual.Ubicacion,
                "Las declaraciones 'importar' deben ubicarse en el nivel superior del archivo."),
            _ => AnalizarSentencia()
        };
    }

    private DeclImportarFutura AnalizarDeclaracionImportarFutura()
    {
        Token tokImportar = Consumir();
        Token ruta = Esperar(TipoToken.TextoLiteral, "ALB-S017",
            "Se esperaba una ruta de módulo entre comillas después de 'importar'.");

        string? alias = null;
        if (Coincide(TipoToken.Como))
        {
            Token idAlias = Esperar(TipoToken.Identificador, "ALB-S018",
                "Se esperaba un identificador de alias después de 'como'.");
            alias = idAlias.Lexema;
        }

        return new DeclImportarFutura((string)ruta.ValorLiteral!, alias, tokImportar.Ubicacion);
    }

    private DeclVariable AnalizarDeclaracionVariable()
    {
        Token tokVar = Consumir();
        Token nombre = Esperar(TipoToken.Identificador, "ALB-S001",
            "Se esperaba el nombre de la variable después de 'variable'.");

        AnotacionTipo? tipo = null;
        if (Coincide(TipoToken.DosPuntos))
        {
            tipo = AnalizarTipo();
        }

        Expresion? inicializador = null;
        if (Coincide(TipoToken.Igual))
        {
            inicializador = AnalizarExpresionEsperada("Se esperaba una expresión después del operador de asignación '='.");
        }

        return new DeclVariable(nombre.Lexema, tipo, inicializador, tokVar.Ubicacion);
    }

    private DeclConstante AnalizarDeclaracionConstante()
    {
        Token tokConst = Consumir();
        Token nombre = Esperar(TipoToken.Identificador, "ALB-S002",
            "Se esperaba el nombre de la constante después de 'constante'.");

        AnotacionTipo? tipo = null;
        if (Coincide(TipoToken.DosPuntos))
        {
            tipo = AnalizarTipo();
        }

        Esperar(TipoToken.Igual, "ALB-S003",
            $"La constante '{nombre.Lexema}' debe inicializarse obligatoriamente con '='.");

        Expresion valor = AnalizarExpresionEsperada("Se esperaba una expresión después del operador de asignación '='.");
        return new DeclConstante(nombre.Lexema, tipo, valor, tokConst.Ubicacion);
    }

    private DeclFuncion AnalizarDeclaracionFuncion()
    {
        Token tokFunc = Consumir();
        Token nombre = Esperar(TipoToken.Identificador, "ALB-S004",
            "Se esperaba el nombre de la función después de 'funcion'.");

        Esperar(TipoToken.ParentesisIzq, "ALB-S006",
            "Se esperaba '(' después del nombre de la función.");
        IReadOnlyList<ParametroAst> parametros = AnalizarListaParametros();
        Esperar(TipoToken.ParentesisDer, "ALB-S007",
            "Se esperaba ')' después de la lista de parámetros.");

        AnotacionTipo? tipoRetorno = null;
        if (Coincide(TipoToken.Flecha, TipoToken.DosPuntos))
        {
            tipoRetorno = AnalizarTipo();
        }

        _profundidadFuncion++;
        try
        {
            SentenciaBloque cuerpo = AnalizarBloque();
            return new DeclFuncion(nombre.Lexema, parametros, tipoRetorno, cuerpo, tokFunc.Ubicacion);
        }
        finally
        {
            _profundidadFuncion--;
        }
    }

    private IReadOnlyList<ParametroAst> AnalizarListaParametros()
    {
        var parametros = new List<ParametroAst>();
        if (MirarActual().Tipo == TipoToken.ParentesisDer)
        {
            return parametros;
        }

        do
        {
            Token nombreParam = Esperar(TipoToken.Identificador, "ALB-S008",
                "Se esperaba el nombre del parámetro.");
            AnotacionTipo? tipoParam = null;
            if (Coincide(TipoToken.DosPuntos))
            {
                tipoParam = AnalizarTipo();
            }
            parametros.Add(new ParametroAst(nombreParam.Lexema, tipoParam, nombreParam.Ubicacion));
        }
        while (Coincide(TipoToken.Coma));

        return parametros;
    }

    private DeclClase AnalizarDeclaracionClase()
    {
        Token tokClase = Consumir();
        Token nombreClase = Esperar(TipoToken.Identificador, "ALB-S009",
            "Se esperaba el nombre de la clase después de 'clase'.");

        string? nombreClaseBase = null;
        if (Coincide(TipoToken.Extiende))
        {
            Token claseBase = Esperar(TipoToken.Identificador, "ALB-S038",
                "Se esperaba el nombre de la clase base después de 'extiende'.");
            nombreClaseBase = claseBase.Lexema;
        }

        SaltarSaltosLinea();
        Esperar(TipoToken.LlaveIzq, "ALB-S010",
            $"Se esperaba '{{' para iniciar el cuerpo de la clase '{nombreClase.Lexema}'.");

        var campos = new List<DeclCampoClase>();
        DeclConstructor? constructor = null;
        var metodos = new List<DeclMetodoClase>();

        _profundidadClase++;
        _profundidadBloque++;
        try
        {
            SaltarSeparadores();

            while (!EstaAlFinal() && MirarActual().Tipo != TipoToken.LlaveDer)
            {
                ModificadorAcceso acceso = ModificadorAcceso.Publico;
                if (Coincide(TipoToken.Publico))
                {
                    acceso = ModificadorAcceso.Publico;
                }
                else if (Coincide(TipoToken.Privado))
                {
                    acceso = ModificadorAcceso.Privado;
                }

                Token tokMiembro = MirarActual();

                if (tokMiembro.Tipo == TipoToken.Constructor)
                {
                    if (constructor is not null)
                    {
                        RegistrarError("ALB-S012", tokMiembro.Ubicacion,
                            $"La clase '{nombreClase.Lexema}' ya define un constructor.");
                    }
                    constructor = AnalizarConstructor(acceso);
                }
                else if (tokMiembro.Tipo == TipoToken.Funcion)
                {
                    metodos.Add(AnalizarMetodoClase(acceso));
                }
                else if (tokMiembro.Tipo == TipoToken.Identificador)
                {
                    campos.Add(AnalizarCampoClase(acceso));
                }
                else if (tokMiembro.Tipo == TipoToken.Variable)
                {
                    ReportarYAbandonar("ALB-S013", tokMiembro.Ubicacion,
                        "Dentro de una clase, los campos se declaran sin 'variable' (ej: 'privado saldo: decimal').");
                }
                else
                {
                    ReportarYAbandonar("ALB-S014", tokMiembro.Ubicacion,
                        $"Miembro de clase inesperado '{tokMiembro.Lexema}'. Se esperaba un campo, 'constructor' o 'funcion'.");
                }

                if (MirarActual().Tipo != TipoToken.LlaveDer)
                {
                    ConsumirSeparadorObligatorio();
                }
                SaltarSeparadores();
            }

            Esperar(TipoToken.LlaveDer, "ALB-S011",
                $"Se esperaba '}}' para cerrar la clase '{nombreClase.Lexema}'.");
        }
        finally
        {
            _profundidadBloque--;
            _profundidadClase--;
        }

        return new DeclClase(nombreClase.Lexema, campos, constructor, metodos, tokClase.Ubicacion, nombreClaseBase);
    }

    private DeclCampoClase AnalizarCampoClase(ModificadorAcceso acceso)
    {
        Token nombre = Consumir();
        AnotacionTipo? tipo = null;
        if (Coincide(TipoToken.DosPuntos))
        {
            tipo = AnalizarTipo();
        }

        Expresion? inicializador = null;
        if (Coincide(TipoToken.Igual))
        {
            inicializador = AnalizarExpresionEsperada("Se esperaba una expresión después del operador '=' en el campo.");
        }

        return new DeclCampoClase(nombre.Lexema, acceso, tipo, inicializador, nombre.Ubicacion);
    }

    private DeclConstructor AnalizarConstructor(ModificadorAcceso acceso)
    {
        Token tokCtor = Consumir();
        Esperar(TipoToken.ParentesisIzq, "ALB-S006", "Se esperaba '(' después de 'constructor'.");
        IReadOnlyList<ParametroAst> parametros = AnalizarListaParametros();
        Esperar(TipoToken.ParentesisDer, "ALB-S007", "Se esperaba ')' después de los parámetros del constructor.");

        _profundidadFuncion++;
        try
        {
            SentenciaBloque cuerpo = AnalizarBloque();
            return new DeclConstructor(acceso, parametros, cuerpo, tokCtor.Ubicacion);
        }
        finally
        {
            _profundidadFuncion--;
        }
    }

    private DeclMetodoClase AnalizarMetodoClase(ModificadorAcceso acceso)
    {
        Token tokFunc = Consumir();
        Token nombre = Esperar(TipoToken.Identificador, "ALB-S004",
            "Se esperaba el nombre del método después de 'funcion'.");

        Esperar(TipoToken.ParentesisIzq, "ALB-S006", "Se esperaba '(' después del nombre del método.");
        IReadOnlyList<ParametroAst> parametros = AnalizarListaParametros();
        Esperar(TipoToken.ParentesisDer, "ALB-S007", "Se esperaba ')' después de los parámetros del método.");

        AnotacionTipo? tipoRetorno = null;
        if (Coincide(TipoToken.Flecha, TipoToken.DosPuntos))
        {
            tipoRetorno = AnalizarTipo();
        }

        _profundidadFuncion++;
        try
        {
            SentenciaBloque cuerpo = AnalizarBloque();
            return new DeclMetodoClase(nombre.Lexema, acceso, parametros, tipoRetorno, cuerpo, tokFunc.Ubicacion);
        }
        finally
        {
            _profundidadFuncion--;
        }
    }

    private AnotacionTipo AnalizarTipo()
    {
        Token tok = MirarActual();
        if (tok.Tipo is TipoToken.TipoEntero
            or TipoToken.TipoDecimal
            or TipoToken.TipoTexto
            or TipoToken.TipoBooleano
            or TipoToken.TipoLista
            or TipoToken.TipoDiccionario
            or TipoToken.TipoVacio
            or TipoToken.Nulo
            or TipoToken.Identificador)
        {
            Consumir();
            return new AnotacionTipo(tok.Lexema, tok.Ubicacion);
        }

        throw LanzarError("ALB-S019", tok.Ubicacion,
            $"Se esperaba un nombre de tipo válido (entero, decimal, texto, booleano, lista, diccionario, vacio o clase) en lugar de '{tok.Lexema}'.");
    }

    private SentenciaBloque AnalizarBloque()
    {
        SaltarSaltosLinea();
        Token llaveIzq = Esperar(TipoToken.LlaveIzq, "ALB-S020",
            "Se esperaba '{' para iniciar un bloque de código.");

        var sentencias = new List<Sentencia>();
        SaltarSeparadores();

        _profundidadBloque++;
        try
        {
            while (!EstaAlFinal() && MirarActual().Tipo != TipoToken.LlaveDer)
            {
                try
                {
                    Sentencia s = AnalizarDeclaracionOSentencia(permitirClaseYModulo: false);
                    sentencias.Add(s);

                    if (MirarActual().Tipo != TipoToken.LlaveDer)
                    {
                        ConsumirSeparadorObligatorio();
                    }
                }
                catch (ErrorSincronizacionExcepcion)
                {
                    Sincronizar();
                }

                SaltarSeparadores();
            }

            Esperar(TipoToken.LlaveDer, "ALB-S021",
                "Se esperaba '}' para cerrar el bloque de código.");
        }
        finally
        {
            _profundidadBloque--;
        }

        return new SentenciaBloque(sentencias, llaveIzq.Ubicacion);
    }

    private Sentencia AnalizarSentencia()
    {
        Token actual = MirarActual();

        return actual.Tipo switch
        {
            TipoToken.Si => AnalizarSi(),
            TipoToken.Elegir => AnalizarElegir(),
            TipoToken.Mientras => AnalizarMientras(),
            TipoToken.Para => AnalizarPara(),
            TipoToken.Retornar => AnalizarRetornar(),
            TipoToken.Romper => AnalizarRomper(),
            TipoToken.Continuar => AnalizarContinuar(),
            TipoToken.Intentar => AnalizarIntentar(),
            TipoToken.Lanzar => AnalizarLanzar(),
            TipoToken.Imprimir => AnalizarImprimir(),
            TipoToken.Publico or TipoToken.Privado => ReportarYAbandonar("ALB-S022", actual.Ubicacion,
                $"El modificador '{actual.Lexema}' solo puede utilizarse dentro de una clase."),
            _ => AnalizarAsignacionOExpresion()
        };
    }

    private SentenciaElegir AnalizarElegir()
    {
        Token tokElegir = Consumir();
        Expresion objetivo = AnalizarExpresionEsperada("Se esperaba una expresión después de 'elegir'.");

        SaltarSaltosLinea();
        Esperar(TipoToken.LlaveIzq, "ALB-S020", "Se esperaba '{' para iniciar el bloque 'elegir'.");
        SaltarSeparadores();

        var casos = new List<CasoElegir>();
        SentenciaBloque? defecto = null;

        _profundidadBloque++;
        try
        {
            while (!EstaAlFinal() && MirarActual().Tipo != TipoToken.LlaveDer)
            {
                if (Coincide(TipoToken.Caso))
                {
                    Token tokCaso = TokenAnterior();
                    var valores = new List<Expresion>();
                    do
                    {
                        valores.Add(AnalizarExpresionEsperada("Se esperaba un valor de comparación después de 'caso'."));
                    }
                    while (Coincide(TipoToken.Coma));

                    SentenciaBloque bloqueCaso = AnalizarCuerpoCaso();
                    casos.Add(new CasoElegir(valores, bloqueCaso, tokCaso.Ubicacion));
                }
                else if (Coincide(TipoToken.Defecto))
                {
                    Token tokDef = TokenAnterior();
                    if (defecto is not null)
                    {
                        RegistrarError("ALB-S040", tokDef.Ubicacion, "Un bloque 'elegir' solo puede contener una rama 'defecto'.");
                    }
                    defecto = AnalizarCuerpoCaso();
                }
                else
                {
                    Token inesperado = MirarActual();
                    throw LanzarError("ALB-S041", inesperado.Ubicacion,
                        $"Se esperaba 'caso', 'defecto' o '}}' dentro de 'elegir', en lugar de '{inesperado.Lexema}'.");
                }

                SaltarSeparadores();
            }

            Esperar(TipoToken.LlaveDer, "ALB-S021", "Se esperaba '}' para cerrar el bloque 'elegir'.");
        }
        finally
        {
            _profundidadBloque--;
        }

        return new SentenciaElegir(objetivo, casos, defecto, tokElegir.Ubicacion);
    }

    private SentenciaBloque AnalizarCuerpoCaso()
    {
        SaltarSaltosLinea();
        if (Coincide(TipoToken.DosPuntos))
        {
            Token tokDosPuntos = TokenAnterior();
            SaltarSeparadores();
            var sentencias = new List<Sentencia>();
            while (!EstaAlFinal() &&
                   MirarActual().Tipo != TipoToken.Caso &&
                   MirarActual().Tipo != TipoToken.Defecto &&
                   MirarActual().Tipo != TipoToken.LlaveDer)
            {
                try
                {
                    Sentencia sent = AnalizarDeclaracionOSentencia(permitirClaseYModulo: false);
                    sentencias.Add(sent);
                    if (!EstaAlFinal() &&
                        MirarActual().Tipo != TipoToken.Caso &&
                        MirarActual().Tipo != TipoToken.Defecto &&
                        MirarActual().Tipo != TipoToken.LlaveDer)
                    {
                        ConsumirSeparadorObligatorio();
                    }
                }
                catch (ErrorSincronizacionExcepcion)
                {
                    SincronizarCaso();
                }
                SaltarSeparadores();
            }
            return new SentenciaBloque(sentencias, tokDosPuntos.Ubicacion);
        }

        if (MirarActual().Tipo == TipoToken.LlaveIzq)
        {
            return AnalizarBloque();
        }

        throw LanzarError("ALB-S020", MirarActual().Ubicacion,
            "Se esperaba ':' o '{' después de la condición de 'caso' o 'defecto'.");
    }

    private void SincronizarCaso()
    {
        while (!EstaAlFinal())
        {
            TipoToken actual = MirarActual().Tipo;
            if (actual is TipoToken.Caso or TipoToken.Defecto or TipoToken.LlaveDer)
            {
                return;
            }
            if (actual is TipoToken.SaltoLinea or TipoToken.PuntoYComa)
            {
                Consumir();
                return;
            }
            Consumir();
        }
    }

    private SentenciaSi AnalizarSi()
    {
        Token tokSi = Consumir();
        Expresion condicion = AnalizarExpresionEsperada("Se esperaba una condición booleana después de 'si'.");
        SentenciaBloque bloqueEntonces = AnalizarBloque();

        var ramasSinoSi = new List<RamaSinoSi>();
        while (MirarDespuesDeSaltosLinea() == TipoToken.SinoSi)
        {
            SaltarSaltosLinea();
            Token tokSinoSi = Consumir();
            Expresion condSinoSi = AnalizarExpresionEsperada("Se esperaba una condición booleana después de 'sino_si'.");
            SentenciaBloque bloqueSinoSi = AnalizarBloque();
            ramasSinoSi.Add(new RamaSinoSi(condSinoSi, bloqueSinoSi, tokSinoSi.Ubicacion));
        }

        SentenciaBloque? bloqueSino = null;
        if (MirarDespuesDeSaltosLinea() == TipoToken.Sino)
        {
            SaltarSaltosLinea();
            Consumir();
            bloqueSino = AnalizarBloque();
        }

        return new SentenciaSi(condicion, bloqueEntonces, ramasSinoSi, bloqueSino, tokSi.Ubicacion);
    }

    private SentenciaMientras AnalizarMientras()
    {
        Token tokMientras = Consumir();
        Expresion condicion = AnalizarExpresionEsperada("Se esperaba una condición booleana después de 'mientras'.");

        _profundidadCiclo++;
        try
        {
            SentenciaBloque cuerpo = AnalizarBloque();
            return new SentenciaMientras(condicion, cuerpo, tokMientras.Ubicacion);
        }
        finally
        {
            _profundidadCiclo--;
        }
    }

    private SentenciaPara AnalizarPara()
    {
        Token tokPara = Consumir();
        Token variable = Esperar(TipoToken.Identificador, "ALB-S023",
            "Se esperaba un identificador de variable de iteración después de 'para'.");
        Esperar(TipoToken.En, "ALB-S024",
            "Se esperaba la palabra reservada 'en' después de la variable del ciclo 'para'.");
        Expresion iterable = AnalizarExpresionEsperada("Se esperaba una colección o rango después de 'en'.");

        _profundidadCiclo++;
        try
        {
            SentenciaBloque cuerpo = AnalizarBloque();
            return new SentenciaPara(variable.Lexema, iterable, cuerpo, tokPara.Ubicacion);
        }
        finally
        {
            _profundidadCiclo--;
        }
    }

    private SentenciaRetornar AnalizarRetornar()
    {
        Token tokRet = Consumir();
        if (_profundidadFuncion == 0)
        {
            RegistrarError("ALB-S025", tokRet.Ubicacion,
                "La sentencia 'retornar' solo puede utilizarse dentro de una función, método o constructor.");
        }

        Expresion? valor = null;
        if (!EsFinDeSentencia())
        {
            valor = AnalizarExpresion();
        }

        return new SentenciaRetornar(valor, tokRet.Ubicacion);
    }

    private SentenciaRomper AnalizarRomper()
    {
        Token tok = Consumir();
        if (_profundidadCiclo == 0)
        {
            RegistrarError("ALB-S026", tok.Ubicacion,
                "La sentencia 'romper' solo puede utilizarse dentro de un ciclo 'mientras' o 'para'.");
        }
        return new SentenciaRomper(tok.Ubicacion);
    }

    private SentenciaContinuar AnalizarContinuar()
    {
        Token tok = Consumir();
        if (_profundidadCiclo == 0)
        {
            RegistrarError("ALB-S027", tok.Ubicacion,
                "La sentencia 'continuar' solo puede utilizarse dentro de un ciclo 'mientras' o 'para'.");
        }
        return new SentenciaContinuar(tok.Ubicacion);
    }

    private SentenciaIntentar AnalizarIntentar()
    {
        Token tokIntentar = Consumir();
        SentenciaBloque bloqueIntentar = AnalizarBloque();

        BloqueCapturar? bloqueCapturar = null;
        if (MirarDespuesDeSaltosLinea() == TipoToken.Capturar)
        {
            SaltarSaltosLinea();
            Token tokCapturar = Consumir();
            string? nombreError = null;
            AnotacionTipo? tipoError = null;

            if (Coincide(TipoToken.ParentesisIzq))
            {
                Token idErr = Esperar(TipoToken.Identificador, "ALB-S028",
                    "Se esperaba el nombre de la variable de error dentro de 'capturar(...)'.");
                nombreError = idErr.Lexema;

                if (Coincide(TipoToken.DosPuntos))
                {
                    tipoError = AnalizarTipo();
                }

                Esperar(TipoToken.ParentesisDer, "ALB-S007",
                    "Se esperaba ')' al cerrar la declaración de 'capturar'.");
            }

            SentenciaBloque cuerpoCapturar = AnalizarBloque();
            bloqueCapturar = new BloqueCapturar(nombreError, tipoError, cuerpoCapturar, tokCapturar.Ubicacion);
        }

        SentenciaBloque? bloqueFinalmente = null;
        if (MirarDespuesDeSaltosLinea() == TipoToken.Finalmente)
        {
            SaltarSaltosLinea();
            Consumir();
            bloqueFinalmente = AnalizarBloque();
        }

        if (bloqueCapturar is null && bloqueFinalmente is null)
        {
            RegistrarError("ALB-S029", tokIntentar.Ubicacion,
                "Un bloque 'intentar' debe ir acompañado de al menos un bloque 'capturar' o 'finalmente'.");
        }

        return new SentenciaIntentar(bloqueIntentar, bloqueCapturar, bloqueFinalmente, tokIntentar.Ubicacion);
    }

    private SentenciaLanzar AnalizarLanzar()
    {
        Token tokLanzar = Consumir();
        Expresion expr = AnalizarExpresionEsperada("Se esperaba una expresión después de 'lanzar'.");
        return new SentenciaLanzar(expr, tokLanzar.Ubicacion);
    }

    private SentenciaImprimir AnalizarImprimir()
    {
        Token tokImprimir = Consumir();
        Esperar(TipoToken.ParentesisIzq, "ALB-S006",
            "Se esperaba '(' después de 'imprimir'.");

        var args = new List<Expresion>();
        if (MirarActual().Tipo != TipoToken.ParentesisDer)
        {
            do
            {
                args.Add(AnalizarExpresion());
            }
            while (Coincide(TipoToken.Coma));
        }

        Esperar(TipoToken.ParentesisDer, "ALB-S007",
            "Se esperaba ')' después de los argumentos de 'imprimir'.");

        return new SentenciaImprimir(args, tokImprimir.Ubicacion);
    }

    private Sentencia AnalizarAsignacionOExpresion()
    {
        Expresion izquierda = AnalizarExpresion();

        Token actual = MirarActual();
        if (actual.Tipo is TipoToken.Igual
            or TipoToken.MasIgual
            or TipoToken.MenosIgual
            or TipoToken.EstrellaIgual
            or TipoToken.BarraIgual)
        {
            Token op = Consumir();
            if (izquierda is not (ExprIdentificador or ExprAccesoMiembro or ExprIndice))
            {
                RegistrarError("ALB-S005", izquierda.Ubicacion,
                    "Objetivo de asignación inválido. Solo se pueden asignar variables, propiedades o elementos de lista.");
            }

            Expresion valor = AnalizarExpresionEsperada(
                $"Se esperaba una expresión después del operador de asignación '{op.Lexema}'.");

            OperadorAsignacion tipoOp = op.Tipo switch
            {
                TipoToken.Igual => OperadorAsignacion.Simple,
                TipoToken.MasIgual => OperadorAsignacion.Suma,
                TipoToken.MenosIgual => OperadorAsignacion.Resta,
                TipoToken.EstrellaIgual => OperadorAsignacion.Multiplicacion,
                TipoToken.BarraIgual => OperadorAsignacion.Division,
                _ => OperadorAsignacion.Simple
            };

            return new SentenciaAsignacion(izquierda, tipoOp, valor, op.Ubicacion);
        }

        if (actual.Tipo is TipoToken.MasMas or TipoToken.MenosMenos)
        {
            Token op = Consumir();
            if (izquierda is not (ExprIdentificador or ExprAccesoMiembro or ExprIndice))
            {
                RegistrarError("ALB-S005", izquierda.Ubicacion,
                    "Objetivo inválido para incremento/decremento. Utiliza una variable o propiedad.");
            }

            OperadorAsignacion tipoOp = op.Tipo == TipoToken.MasMas
                ? OperadorAsignacion.Suma
                : OperadorAsignacion.Resta;
            return new SentenciaAsignacion(
                izquierda,
                tipoOp,
                new ExprLiteral(1L, TipoLiteralAlba.Entero, op.Ubicacion),
                op.Ubicacion);
        }

        return new SentenciaExpresion(izquierda, izquierda.Ubicacion);
    }

    // --- Jerarquía de expresiones (Precedencia 10 -> 1) ---

    private Expresion AnalizarExpresionEsperada(string mensajeSiFalta)
    {
        if (EsFinDeSentencia())
        {
            Token actual = MirarActual();
            throw LanzarError("ALB-S030", actual.Ubicacion, mensajeSiFalta);
        }
        return AnalizarExpresion();
    }

    private Expresion AnalizarExpresion() => AnalizarNuloCoalescente();

    private Expresion AnalizarNuloCoalescente()
    {
        Expresion expr = AnalizarLogicaO();
        while (Coincide(TipoToken.NuloCoalescente))
        {
            Token op = TokenAnterior();
            Expresion derecha = AnalizarLogicaO();
            expr = new ExprBinaria(expr, OperadorBinario.NuloCoalescente, derecha, op.Ubicacion);
        }
        return expr;
    }

    private Expresion AnalizarLogicaO()
    {
        Expresion expr = AnalizarLogicaY();
        while (Coincide(TipoToken.LogicoO))
        {
            Token op = TokenAnterior();
            Expresion derecha = AnalizarLogicaY();
            expr = new ExprBinaria(expr, OperadorBinario.LogicoO, derecha, op.Ubicacion);
        }
        return expr;
    }

    private Expresion AnalizarLogicaY()
    {
        Expresion expr = AnalizarIgualdad();
        while (Coincide(TipoToken.LogicoY))
        {
            Token op = TokenAnterior();
            Expresion derecha = AnalizarIgualdad();
            expr = new ExprBinaria(expr, OperadorBinario.LogicoY, derecha, op.Ubicacion);
        }
        return expr;
    }

    private Expresion AnalizarIgualdad()
    {
        Expresion expr = AnalizarComparacion();
        while (Coincide(TipoToken.IgualIgual, TipoToken.Diferente))
        {
            Token op = TokenAnterior();
            OperadorBinario bin = op.Tipo == TipoToken.IgualIgual
                ? OperadorBinario.Igual
                : OperadorBinario.Diferente;
            Expresion derecha = AnalizarComparacion();
            expr = new ExprBinaria(expr, bin, derecha, op.Ubicacion);
        }
        return expr;
    }

    private Expresion AnalizarComparacion()
    {
        Expresion expr = AnalizarRango();
        while (Coincide(TipoToken.Menor, TipoToken.MenorIgual, TipoToken.Mayor, TipoToken.MayorIgual, TipoToken.Es))
        {
            Token op = TokenAnterior();
            if (op.Tipo == TipoToken.Es)
            {
                AnotacionTipo tipoEsperado = AnalizarTipo();
                expr = new ExprComprobacionTipo(expr, tipoEsperado, op.Ubicacion);
                continue;
            }

            OperadorBinario bin = op.Tipo switch
            {
                TipoToken.Menor => OperadorBinario.Menor,
                TipoToken.MenorIgual => OperadorBinario.MenorIgual,
                TipoToken.Mayor => OperadorBinario.Mayor,
                TipoToken.MayorIgual => OperadorBinario.MayorIgual,
                _ => throw new InvalidOperationException()
            };
            Expresion derecha = AnalizarRango();
            expr = new ExprBinaria(expr, bin, derecha, op.Ubicacion);
        }
        return expr;
    }

    private Expresion AnalizarRango()
    {
        Expresion expr = AnalizarSuma();
        if (Coincide(TipoToken.PuntoPunto, TipoToken.PuntoPuntoIgual))
        {
            Token op = TokenAnterior();
            bool inclusivo = op.Tipo == TipoToken.PuntoPuntoIgual;
            Expresion fin = AnalizarSuma();
            return new ExprRango(expr, fin, inclusivo, op.Ubicacion);
        }
        return expr;
    }

    private Expresion AnalizarSuma()
    {
        Expresion expr = AnalizarProducto();
        while (Coincide(TipoToken.Mas, TipoToken.Menos))
        {
            Token op = TokenAnterior();
            OperadorBinario bin = op.Tipo == TipoToken.Mas
                ? OperadorBinario.Suma
                : OperadorBinario.Resta;
            Expresion derecha = AnalizarProducto();
            expr = new ExprBinaria(expr, bin, derecha, op.Ubicacion);
        }
        return expr;
    }

    private Expresion AnalizarProducto()
    {
        Expresion expr = AnalizarPotencia();
        while (Coincide(TipoToken.Estrella, TipoToken.Barra, TipoToken.Porcentaje))
        {
            Token op = TokenAnterior();
            OperadorBinario bin = op.Tipo switch
            {
                TipoToken.Estrella => OperadorBinario.Multiplicacion,
                TipoToken.Barra => OperadorBinario.Division,
                TipoToken.Porcentaje => OperadorBinario.Modulo,
                _ => throw new InvalidOperationException()
            };
            Expresion derecha = AnalizarPotencia();
            expr = new ExprBinaria(expr, bin, derecha, op.Ubicacion);
        }
        return expr;
    }

    private Expresion AnalizarPotencia()
    {
        Expresion izquierda = AnalizarUnaria();
        if (Coincide(TipoToken.Potencia))
        {
            Token op = TokenAnterior();
            Expresion derecha = AnalizarPotencia();
            return new ExprBinaria(izquierda, OperadorBinario.Potencia, derecha, op.Ubicacion);
        }
        return izquierda;
    }

    private Expresion AnalizarUnaria()
    {
        if (Coincide(TipoToken.Menos, TipoToken.LogicoNo))
        {
            Token op = TokenAnterior();
            OperadorUnario tipoOp = op.Tipo == TipoToken.Menos
                ? OperadorUnario.NegacionAritmetica
                : OperadorUnario.NegacionLogica;
            Expresion operando = AnalizarUnaria();
            return new ExprUnaria(tipoOp, operando, op.Ubicacion);
        }
        return AnalizarPostfija();
    }

    private Expresion AnalizarPostfija()
    {
        Expresion expr = AnalizarPrimaria();

        while (true)
        {
            if (MirarActual().Tipo == TipoToken.SaltoLinea &&
                MirarDespuesDeSaltosLinea() is TipoToken.Punto or TipoToken.InterrogacionPunto)
            {
                SaltarSaltosLinea();
            }

            if (Coincide(TipoToken.ParentesisIzq))
            {
                Token tokParen = TokenAnterior();
                var args = new List<Expresion>();
                if (MirarActual().Tipo != TipoToken.ParentesisDer)
                {
                    do
                    {
                        args.Add(AnalizarExpresion());
                    }
                    while (Coincide(TipoToken.Coma));
                }
                Esperar(TipoToken.ParentesisDer, "ALB-S007",
                    "Se esperaba ')' después de los argumentos de la llamada.");
                expr = new ExprLlamada(expr, args, tokParen.Ubicacion);
            }
            else if (Coincide(TipoToken.CorcheteIzq))
            {
                Token tokCorchete = TokenAnterior();
                Expresion indice = AnalizarExpresionEsperada("Se esperaba una expresión de índice dentro de '[...]'.");
                Esperar(TipoToken.CorcheteDer, "ALB-S031",
                    "Se esperaba ']' para cerrar el acceso por índice.");
                expr = new ExprIndice(expr, indice, tokCorchete.Ubicacion);
            }
            else if (Coincide(TipoToken.Punto))
            {
                Token miembro = Esperar(TipoToken.Identificador, "ALB-S032",
                    "Se esperaba el nombre de una propiedad o método después de '.'.");
                expr = new ExprAccesoMiembro(expr, miembro.Lexema, miembro.Ubicacion);
            }
            else if (Coincide(TipoToken.InterrogacionPunto))
            {
                Token miembro = Esperar(TipoToken.Identificador, "ALB-S032",
                    "Se esperaba el nombre de una propiedad o método después de '?.'.");
                expr = new ExprAccesoMiembroOpcional(expr, miembro.Lexema, miembro.Ubicacion);
            }
            else
            {
                break;
            }
        }

        return expr;
    }

    private Expresion AnalizarPrimaria()
    {
        Token actual = MirarActual();

        switch (actual.Tipo)
        {
            case TipoToken.EnteroLiteral:
                Consumir();
                return new ExprLiteral(actual.ValorLiteral, TipoLiteralAlba.Entero, actual.Ubicacion);

            case TipoToken.DecimalLiteral:
                Consumir();
                return new ExprLiteral(actual.ValorLiteral, TipoLiteralAlba.Decimal, actual.Ubicacion);

            case TipoToken.TextoLiteral:
                Consumir();
                return new ExprLiteral(actual.ValorLiteral, TipoLiteralAlba.Texto, actual.Ubicacion);

            case TipoToken.TextoInterpInicio:
                return AnalizarTextoInterpolado();

            case TipoToken.Verdadero:
            case TipoToken.Falso:
                Consumir();
                return new ExprLiteral(actual.ValorLiteral, TipoLiteralAlba.Booleano, actual.Ubicacion);

            case TipoToken.Nulo:
                Consumir();
                return new ExprLiteral(null, TipoLiteralAlba.Nulo, actual.Ubicacion);

            case TipoToken.Este:
                Consumir();
                if (_profundidadClase == 0)
                {
                    RegistrarError("ALB-S033", actual.Ubicacion,
                        "La referencia 'este' solo puede utilizarse dentro de un método o constructor de una clase.");
                }
                return new ExprEste(actual.Ubicacion);

            case TipoToken.Super:
                Consumir();
                if (_profundidadClase == 0)
                {
                    RegistrarError("ALB-S033", actual.Ubicacion,
                        "La referencia 'super' solo puede utilizarse dentro de un método o constructor de una clase.");
                }
                return new ExprSuper(actual.Ubicacion);

            case TipoToken.Identificador:
            case TipoToken.LogicoY:
                if (_posicion + 1 < _tokens.Count && _tokens[_posicion + 1].Tipo == TipoToken.FlechaGorda)
                {
                    Token paramTok = Consumir();
                    var paramUnico = new List<ParametroAst> { new(paramTok.Lexema, null, paramTok.Ubicacion) };
                    return AnalizarCuerpoLambdaFlecha(paramUnico, null, paramTok.Ubicacion);
                }
                Consumir();
                return new ExprIdentificador(actual.Lexema, actual.Ubicacion);

            case TipoToken.Leer:
                return AnalizarLeer();

            case TipoToken.Nuevo:
                return AnalizarNuevo();

            case TipoToken.Funcion:
                return AnalizarFuncionAnonima();

            case TipoToken.CorcheteIzq:
                return AnalizarListaLiteral();

            case TipoToken.LlaveIzq:
                return AnalizarDiccionarioLiteral();

            case TipoToken.ParentesisIzq:
                if (EsInicioLambdaParentesis())
                {
                    Token tokParenLambda = Consumir();
                    IReadOnlyList<ParametroAst> paramsLambda = AnalizarListaParametros();
                    Esperar(TipoToken.ParentesisDer, "ALB-S007", "Se esperaba ')' después de los parámetros lambda.");
                    AnotacionTipo? retLambda = null;
                    if (Coincide(TipoToken.Flecha, TipoToken.DosPuntos))
                    {
                        retLambda = AnalizarTipo();
                    }
                    return AnalizarCuerpoLambdaFlecha(paramsLambda, retLambda, tokParenLambda.Ubicacion);
                }
                Token paren = Consumir();
                Expresion interna = AnalizarExpresionEsperada("Se esperaba una expresión dentro de '('.");
                Esperar(TipoToken.ParentesisDer, "ALB-S007", "Se esperaba ')' para cerrar la expresión agrupada.");
                return new ExprAgrupada(interna, paren.Ubicacion);

            default:
                throw LanzarError("ALB-S034", actual.Ubicacion,
                    $"Expresión inesperada cerca de '{(actual.Tipo == TipoToken.FinArchivo ? "fin de archivo" : actual.Lexema)}'.");
        }
    }

    private ExprTextoInterpolado AnalizarTextoInterpolado()
    {
        Token inicio = Consumir();
        var partes = new List<ParteInterpolada>();
        string txtIni = (string)(inicio.ValorLiteral ?? string.Empty);
        if (txtIni.Length > 0)
        {
            partes.Add(new ParteInterpolada(txtIni, null));
        }

        while (true)
        {
            Expresion expr = AnalizarExpresionEsperada("Se esperaba una expresión dentro de la interpolación '{...}'.");
            partes.Add(new ParteInterpolada(null, expr));

            Token siguiente = MirarActual();
            if (siguiente.Tipo == TipoToken.TextoInterpMedio)
            {
                Consumir();
                string seg = (string)(siguiente.ValorLiteral ?? string.Empty);
                if (seg.Length > 0)
                {
                    partes.Add(new ParteInterpolada(seg, null));
                }
            }
            else if (siguiente.Tipo == TipoToken.TextoInterpFin)
            {
                Consumir();
                string fin = (string)(siguiente.ValorLiteral ?? string.Empty);
                if (fin.Length > 0)
                {
                    partes.Add(new ParteInterpolada(fin, null));
                }
                break;
            }
            else
            {
                throw LanzarError("ALB-S039", siguiente.Ubicacion,
                    "Se esperaba '}' y el cierre de la cadena interpolada.");
            }
        }

        return new ExprTextoInterpolado(partes, inicio.Ubicacion);
    }

    private ExprFuncionAnonima AnalizarFuncionAnonima()
    {
        Token tokFunc = Consumir();
        Esperar(TipoToken.ParentesisIzq, "ALB-S006", "Se esperaba '(' en la función anónima.");
        IReadOnlyList<ParametroAst> parametros = AnalizarListaParametros();
        Esperar(TipoToken.ParentesisDer, "ALB-S007", "Se esperaba ')' después de los parámetros.");

        AnotacionTipo? tipoRetorno = null;
        if (Coincide(TipoToken.Flecha, TipoToken.DosPuntos))
        {
            tipoRetorno = AnalizarTipo();
        }

        if (MirarActual().Tipo == TipoToken.FlechaGorda)
        {
            return AnalizarCuerpoLambdaFlecha(parametros, tipoRetorno, tokFunc.Ubicacion);
        }

        _profundidadFuncion++;
        try
        {
            SentenciaBloque cuerpo = AnalizarBloque();
            return new ExprFuncionAnonima(parametros, tipoRetorno, cuerpo, tokFunc.Ubicacion);
        }
        finally
        {
            _profundidadFuncion--;
        }
    }

    private ExprFuncionAnonima AnalizarCuerpoLambdaFlecha(
        IReadOnlyList<ParametroAst> parametros,
        AnotacionTipo? tipoRetorno,
        UbicacionFuente ubicacion)
    {
        Token tokFlecha = Esperar(TipoToken.FlechaGorda, "ALB-S044", "Se esperaba '=>' en la expresión lambda.");
        _profundidadFuncion++;
        try
        {
            if (MirarDespuesDeSaltosLinea() == TipoToken.LlaveIzq)
            {
                SentenciaBloque bloque = AnalizarBloque();
                return new ExprFuncionAnonima(parametros, tipoRetorno, bloque, ubicacion);
            }

            Expresion expr = AnalizarExpresionEsperada("Se esperaba una expresión después de '=>'.");
            var bloqueUnico = new SentenciaBloque([new SentenciaRetornar(expr, tokFlecha.Ubicacion)], tokFlecha.Ubicacion);
            return new ExprFuncionAnonima(parametros, tipoRetorno, bloqueUnico, ubicacion);
        }
        finally
        {
            _profundidadFuncion--;
        }
    }

    private bool EsInicioLambdaParentesis()
    {
        if (_posicion >= _tokens.Count || _tokens[_posicion].Tipo != TipoToken.ParentesisIzq)
            return false;

        int i = _posicion + 1;
        int prof = 1;
        while (i < _tokens.Count && prof > 0)
        {
            TipoToken t = _tokens[i].Tipo;
            if (t == TipoToken.ParentesisIzq) prof++;
            else if (t == TipoToken.ParentesisDer) prof--;
            else if (t == TipoToken.FinArchivo) return false;
            i++;
        }

        if (prof != 0 || i >= _tokens.Count)
            return false;

        if (_tokens[i].Tipo is TipoToken.Flecha or TipoToken.DosPuntos)
        {
            i += 2;
        }

        return i < _tokens.Count && _tokens[i].Tipo == TipoToken.FlechaGorda;
    }

    private ExprDiccionario AnalizarDiccionarioLiteral()
    {
        Token tokLlave = Consumir();
        var entradas = new List<EntradaDiccionario>();
        SaltarSeparadores();

        if (MirarActual().Tipo != TipoToken.LlaveDer)
        {
            do
            {
                SaltarSeparadores();
                if (MirarActual().Tipo == TipoToken.LlaveDer)
                {
                    break;
                }

                Token actualClave = MirarActual();
                Expresion clave;
                if ((actualClave.Tipo == TipoToken.Identificador || actualClave.Tipo == TipoToken.LogicoY) &&
                    _posicion + 1 < _tokens.Count &&
                    _tokens[_posicion + 1].Tipo == TipoToken.DosPuntos)
                {
                    Consumir();
                    clave = new ExprLiteral(actualClave.Lexema, TipoLiteralAlba.Texto, actualClave.Ubicacion);
                }
                else
                {
                    clave = AnalizarExpresionEsperada("Se esperaba la clave de la entrada del diccionario.");
                }

                Esperar(TipoToken.DosPuntos, "ALB-S042", "Se esperaba ':' entre la clave y el valor del diccionario.");
                SaltarSaltosLinea();
                Expresion valor = AnalizarExpresionEsperada("Se esperaba el valor después de ':' en el diccionario.");
                entradas.Add(new EntradaDiccionario(clave, valor, actualClave.Ubicacion));
                SaltarSeparadores();
            }
            while (Coincide(TipoToken.Coma));
        }

        SaltarSeparadores();
        Esperar(TipoToken.LlaveDer, "ALB-S021", "Se esperaba '}' para cerrar el diccionario.");
        return new ExprDiccionario(entradas, tokLlave.Ubicacion);
    }

    private ExprLeer AnalizarLeer()
    {
        Token tokLeer = Consumir();
        Esperar(TipoToken.ParentesisIzq, "ALB-S006", "Se esperaba '(' después de 'leer'.");

        Expresion? mensaje = null;
        if (MirarActual().Tipo != TipoToken.ParentesisDer)
        {
            mensaje = AnalizarExpresion();
        }

        Esperar(TipoToken.ParentesisDer, "ALB-S007", "Se esperaba ')' al cerrar 'leer(...)'.");
        return new ExprLeer(mensaje, tokLeer.Ubicacion);
    }

    private ExprNuevo AnalizarNuevo()
    {
        Token tokNuevo = Consumir();
        Token nombreClase = Esperar(TipoToken.Identificador, "ALB-S035",
            "Se esperaba el nombre de la clase después de 'nuevo'.");
        Esperar(TipoToken.ParentesisIzq, "ALB-S006",
            $"Se esperaba '(' para invocar al constructor de '{nombreClase.Lexema}'.");

        var args = new List<Expresion>();
        if (MirarActual().Tipo != TipoToken.ParentesisDer)
        {
            do
            {
                args.Add(AnalizarExpresion());
            }
            while (Coincide(TipoToken.Coma));
        }

        Esperar(TipoToken.ParentesisDer, "ALB-S007",
            "Se esperaba ')' después de los argumentos del constructor.");

        return new ExprNuevo(nombreClase.Lexema, args, tokNuevo.Ubicacion);
    }

    private ExprLista AnalizarListaLiteral()
    {
        Token tokCorchete = Consumir();
        var elementos = new List<Expresion>();

        if (MirarActual().Tipo != TipoToken.CorcheteDer)
        {
            do
            {
                if (MirarActual().Tipo == TipoToken.CorcheteDer)
                {
                    break; // Permite coma final opcional en listas multilínea
                }
                elementos.Add(AnalizarExpresion());
            }
            while (Coincide(TipoToken.Coma));
        }

        Esperar(TipoToken.CorcheteDer, "ALB-S036", "Se esperaba ']' para cerrar la lista.");
        return new ExprLista(elementos, tokCorchete.Ubicacion);
    }

    // --- Utilidades del flujo de tokens y manejo de errores ---

    private void ConsumirSeparadorObligatorio()
    {
        if (EstaAlFinal())
        {
            return;
        }

        if (Coincide(TipoToken.SaltoLinea, TipoToken.PuntoYComa))
        {
            SaltarSeparadores();
            return;
        }

        Token actual = MirarActual();
        throw LanzarError("ALB-S037", actual.Ubicacion,
            $"Se esperaba un salto de línea o ';' antes de '{actual.Lexema}'.");
    }

    private void SaltarSeparadores()
    {
        while (!EstaAlFinal() && (MirarActual().Tipo == TipoToken.SaltoLinea || MirarActual().Tipo == TipoToken.PuntoYComa))
        {
            Consumir();
        }
    }

    private void SaltarSaltosLinea()
    {
        while (!EstaAlFinal() && MirarActual().Tipo == TipoToken.SaltoLinea)
        {
            Consumir();
        }
    }

    private TipoToken MirarDespuesDeSaltosLinea()
    {
        int idx = _posicion;
        while (idx < _tokens.Count && _tokens[idx].Tipo == TipoToken.SaltoLinea)
        {
            idx++;
        }
        return idx < _tokens.Count ? _tokens[idx].Tipo : TipoToken.FinArchivo;
    }

    private bool EsFinDeSentencia()
    {
        TipoToken tipo = MirarActual().Tipo;
        return tipo is TipoToken.SaltoLinea or TipoToken.PuntoYComa or TipoToken.LlaveDer or TipoToken.FinArchivo;
    }

    private bool EstaAlFinal() => MirarActual().Tipo == TipoToken.FinArchivo;

    private Token MirarActual() =>
        _posicion < _tokens.Count ? _tokens[_posicion] : _tokens[^1];

    private Token TokenAnterior() => _tokens[_posicion - 1];

    private Token Consumir()
    {
        Token actual = MirarActual();
        if (!EstaAlFinal())
        {
            _posicion++;
        }
        return actual;
    }

    private bool Coincide(params TipoToken[] tipos)
    {
        TipoToken actual = MirarActual().Tipo;
        foreach (TipoToken t in tipos)
        {
            if (actual == t)
            {
                Consumir();
                return true;
            }
        }
        return false;
    }

    private Token Esperar(TipoToken esperado, string codigo, string mensaje)
    {
        if (MirarActual().Tipo == esperado || (esperado == TipoToken.Identificador && MirarActual().Tipo == TipoToken.LogicoY))
        {
            return Consumir();
        }

        throw LanzarError(codigo, MirarActual().Ubicacion, mensaje);
    }

    private void RegistrarError(string codigo, UbicacionFuente ubicacion, string mensaje)
    {
        string linea = FormateadorErrores.ExtraerLinea(_codigoFuente, ubicacion.Linea);
        _errores.Add(new ErrorAlba(codigo, TipoError.Sintactico, ubicacion, mensaje, linea));
    }

    private ErrorSincronizacionExcepcion LanzarError(string codigo, UbicacionFuente ubicacion, string mensaje)
    {
        RegistrarError(codigo, ubicacion, mensaje);
        return new ErrorSincronizacionExcepcion();
    }

    private Sentencia ReportarYAbandonar(string codigo, UbicacionFuente ubicacion, string mensaje)
    {
        throw LanzarError(codigo, ubicacion, mensaje);
    }

    private void Sincronizar()
    {
        while (!EstaAlFinal())
        {
            TipoToken actual = MirarActual().Tipo;
            if (actual is TipoToken.SaltoLinea or TipoToken.PuntoYComa)
            {
                Consumir();
                return;
            }

            if (actual is TipoToken.LlaveDer)
            {
                if (_profundidadBloque == 0)
                {
                    Consumir();
                }
                return;
            }

            if (actual is TipoToken.Variable
                or TipoToken.Constante
                or TipoToken.Funcion
                or TipoToken.Clase
                or TipoToken.Si
                or TipoToken.Elegir
                or TipoToken.Mientras
                or TipoToken.Para
                or TipoToken.Retornar
                or TipoToken.Intentar
                or TipoToken.Lanzar
                or TipoToken.Imprimir)
            {
                return;
            }

            Consumir();
        }
    }
}
