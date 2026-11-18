using System.Globalization;
using System.Text;
using Alba.Engine.Diagnostics;

namespace Alba.Engine.Lexer;

/// <summary>
/// Resultado del análisis léxico con la lista de tokens y cualquier error detectado.
/// </summary>
public sealed record ResultadoLexico(
    IReadOnlyList<Token> Tokens,
    IReadOnlyList<ErrorAlba> Errores
)
{
    public bool TieneErrores => Errores.Count > 0;
}

/// <summary>
/// Analizador léxico oficial del lenguaje Alba v0.1.0.
/// Convierte código fuente UTF-8 en tokens con coordenadas exactas de línea y columna.
/// </summary>
public sealed class AnalizadorLexico
{
    private readonly string _codigoFuente;
    private readonly string _archivo;
    private readonly List<Token> _tokens = [];
    private readonly List<ErrorAlba> _errores = [];

    private int _indice;
    private int _linea = 1;
    private int _columna = 1;
    private int _profundidadParentesis;
    private int _profundidadCorchetes;

    public AnalizadorLexico(string codigoFuente, string archivo = "<entrada>")
    {
        _codigoFuente = codigoFuente ?? string.Empty;
        _archivo = string.IsNullOrWhiteSpace(archivo) ? "<entrada>" : archivo;
    }

    public ResultadoLexico Analizar()
    {
        while (!EstaAlFinal())
        {
            char actual = MirarActual();

            // Espacios horizontales y tabulaciones
            if (actual == ' ' || actual == '\t')
            {
                Avanzar();
                continue;
            }

            // Saltos de línea (\r\n o \n)
            if (actual == '\r' || actual == '\n')
            {
                ProcesarSaltoLinea();
                continue;
            }

            // Comentario de una línea (# ...)
            if (actual == '#')
            {
                SaltarComentarioLinea();
                continue;
            }

            // Comentario multilínea (/* ... */)
            if (actual == '/' && MirarSiguiente() == '*')
            {
                SaltarComentarioMultilinea();
                continue;
            }

            // Literales de texto ("..." o '...')
            if (actual == '"' || actual == '\'')
            {
                LeerTextoLiteral(actual);
                continue;
            }

            // Literales numéricos (entero o decimal)
            if (char.IsAsciiDigit(actual))
            {
                LeerNumero();
                continue;
            }

            // Identificadores y palabras reservadas
            if (EsInicioIdentificador(actual))
            {
                LeerIdentificadorOPalabraReservada();
                continue;
            }

            // Operadores y delimitadores
            LeerOperadorODelimitador();
        }

        _tokens.Add(new Token(
            TipoToken.FinArchivo,
            string.Empty,
            null,
            new UbicacionFuente(_archivo, _linea, _columna, 1)
        ));

        return new ResultadoLexico(_tokens, _errores);
    }

    private void ProcesarSaltoLinea()
    {
        int lineaInicio = _linea;
        int colInicio = _columna;

        if (MirarActual() == '\r' && MirarSiguiente() == '\n')
        {
            Avanzar();
        }
        Avanzar(); // consume '\n' o '\r'

        _linea++;
        _columna = 1;

        // Dentro de () o [] se permiten expresiones multilínea sin emitir SaltoLinea
        if (_profundidadParentesis == 0 && _profundidadCorchetes == 0)
        {
            // Evita emitir múltiples tokens SaltoLinea consecutivos
            if (_tokens.Count > 0 && _tokens[^1].Tipo != TipoToken.SaltoLinea)
            {
                _tokens.Add(new Token(
                    TipoToken.SaltoLinea,
                    "\\n",
                    null,
                    new UbicacionFuente(_archivo, lineaInicio, colInicio, 1)
                ));
            }
        }
    }

    private void SaltarComentarioLinea()
    {
        while (!EstaAlFinal() && MirarActual() != '\n' && MirarActual() != '\r')
        {
            Avanzar();
        }
    }

    private void SaltarComentarioMultilinea()
    {
        int lineaInicio = _linea;
        int colInicio = _columna;

        Avanzar(); // '/'
        Avanzar(); // '*'

        while (!EstaAlFinal())
        {
            if (MirarActual() == '*' && MirarSiguiente() == '/')
            {
                Avanzar(); // '*'
                Avanzar(); // '/'
                return;
            }

            if (MirarActual() == '\r' || MirarActual() == '\n')
            {
                if (MirarActual() == '\r' && MirarSiguiente() == '\n')
                {
                    Avanzar();
                }
                Avanzar();
                _linea++;
                _columna = 1;
            }
            else
            {
                Avanzar();
            }
        }

        RegistrarError(
            "ALB-L003",
            new UbicacionFuente(_archivo, lineaInicio, colInicio, 2),
            "Comentario multilínea '/*' sin cerrar antes del fin de archivo."
        );
    }

    private void LeerTextoLiteral(char delimitador)
    {
        int lineaInicio = _linea;
        int colInicio = _columna;
        int inicioIndice = _indice;

        Avanzar(); // consume comilla de apertura
        var valor = new StringBuilder();
        bool cerrado = false;

        while (!EstaAlFinal())
        {
            char c = MirarActual();

            if (c == '\n' || c == '\r')
            {
                break;
            }

            if (c == delimitador)
            {
                Avanzar();
                cerrado = true;
                break;
            }

            if (c == '\\')
            {
                int lineaEscape = _linea;
                int colEscape = _columna;
                Avanzar(); // '\'

                if (EstaAlFinal() || MirarActual() == '\n' || MirarActual() == '\r')
                {
                    break;
                }

                char escapado = Avanzar();
                switch (escapado)
                {
                    case 'n':
                        valor.Append('\n');
                        break;
                    case 't':
                        valor.Append('\t');
                        break;
                    case 'r':
                        valor.Append('\r');
                        break;
                    case '\\':
                        valor.Append('\\');
                        break;
                    case '"':
                        valor.Append('"');
                        break;
                    case '\'':
                        valor.Append('\'');
                        break;
                    default:
                        RegistrarError(
                            "ALB-L004",
                            new UbicacionFuente(_archivo, lineaEscape, colEscape, 2),
                            $"Secuencia de escape no reconocida '\\{escapado}'."
                        );
                        valor.Append(escapado);
                        break;
                }
            }
            else
            {
                valor.Append(Avanzar());
            }
        }

        if (!cerrado)
        {
            RegistrarError(
                "ALB-L001",
                new UbicacionFuente(_archivo, lineaInicio, colInicio, 1),
                "Cadena de texto sin cerrar antes del fin de línea."
            );
            return;
        }

        string lexema = _codigoFuente.Substring(inicioIndice, _indice - inicioIndice);
        _tokens.Add(new Token(
            TipoToken.TextoLiteral,
            lexema,
            valor.ToString(),
            new UbicacionFuente(_archivo, lineaInicio, colInicio, lexema.Length)
        ));
    }

    private void LeerNumero()
    {
        int lineaInicio = _linea;
        int colInicio = _columna;
        int inicioIndice = _indice;
        bool esDecimal = false;

        while (!EstaAlFinal() && char.IsAsciiDigit(MirarActual()))
        {
            Avanzar();
        }

        if (!EstaAlFinal() && MirarActual() == '.' && char.IsAsciiDigit(MirarSiguiente()))
        {
            esDecimal = true;
            Avanzar(); // '.'
            while (!EstaAlFinal() && char.IsAsciiDigit(MirarActual()))
            {
                Avanzar();
            }
        }

        // Si inmediatamente después de dígitos viene una letra (ej: 123abc), es un token numérico inválido
        if (!EstaAlFinal() && EsInicioIdentificador(MirarActual()))
        {
            while (!EstaAlFinal() && EsContinuacionIdentificador(MirarActual()))
            {
                Avanzar();
            }
            string invalido = _codigoFuente.Substring(inicioIndice, _indice - inicioIndice);
            RegistrarError(
                "ALB-L005",
                new UbicacionFuente(_archivo, lineaInicio, colInicio, invalido.Length),
                $"Literal numérico o identificador inválido '{invalido}'."
            );
            return;
        }

        string lexema = _codigoFuente.Substring(inicioIndice, _indice - inicioIndice);
        var ubicacion = new UbicacionFuente(_archivo, lineaInicio, colInicio, lexema.Length);

        if (esDecimal)
        {
            if (double.TryParse(lexema, NumberStyles.Float, CultureInfo.InvariantCulture, out double numeroDecimal))
            {
                _tokens.Add(new Token(TipoToken.DecimalLiteral, lexema, numeroDecimal, ubicacion));
            }
            else
            {
                RegistrarError("ALB-L005", ubicacion, $"Número decimal fuera de rango '{lexema}'.");
            }
        }
        else
        {
            if (long.TryParse(lexema, NumberStyles.Integer, CultureInfo.InvariantCulture, out long numeroEntero))
            {
                _tokens.Add(new Token(TipoToken.EnteroLiteral, lexema, numeroEntero, ubicacion));
            }
            else
            {
                RegistrarError("ALB-L005", ubicacion, $"Número entero fuera de rango '{lexema}'.");
            }
        }
    }

    private void LeerIdentificadorOPalabraReservada()
    {
        int lineaInicio = _linea;
        int colInicio = _columna;
        int inicioIndice = _indice;

        while (!EstaAlFinal() && EsContinuacionIdentificador(MirarActual()))
        {
            Avanzar();
        }

        string lexema = _codigoFuente.Substring(inicioIndice, _indice - inicioIndice);
        var ubicacion = new UbicacionFuente(_archivo, lineaInicio, colInicio, lexema.Length);

        if (PalabrasReservadas.TryObtenerTipo(lexema, out TipoToken tipoReservado))
        {
            object? literal = tipoReservado switch
            {
                TipoToken.Verdadero => true,
                TipoToken.Falso => false,
                _ => null
            };
            _tokens.Add(new Token(tipoReservado, lexema, literal, ubicacion));
        }
        else
        {
            _tokens.Add(new Token(TipoToken.Identificador, lexema, null, ubicacion));
        }
    }

    private void LeerOperadorODelimitador()
    {
        int lineaInicio = _linea;
        int colInicio = _columna;
        char c = Avanzar();

        switch (c)
        {
            case '+':
                if (Coincide('='))
                    Emitir(TipoToken.MasIgual, "+=", lineaInicio, colInicio);
                else
                    Emitir(TipoToken.Mas, "+", lineaInicio, colInicio);
                break;

            case '-':
                if (Coincide('='))
                    Emitir(TipoToken.MenosIgual, "-=", lineaInicio, colInicio);
                else if (Coincide('>'))
                    Emitir(TipoToken.Flecha, "->", lineaInicio, colInicio);
                else
                    Emitir(TipoToken.Menos, "-", lineaInicio, colInicio);
                break;

            case '*':
                if (Coincide('='))
                    Emitir(TipoToken.EstrellaIgual, "*=", lineaInicio, colInicio);
                else
                    Emitir(TipoToken.Estrella, "*", lineaInicio, colInicio);
                break;

            case '/':
                if (Coincide('='))
                    Emitir(TipoToken.BarraIgual, "/=", lineaInicio, colInicio);
                else
                    Emitir(TipoToken.Barra, "/", lineaInicio, colInicio);
                break;

            case '%':
                Emitir(TipoToken.Porcentaje, "%", lineaInicio, colInicio);
                break;

            case '^':
                Emitir(TipoToken.Potencia, "^", lineaInicio, colInicio);
                break;

            case '=':
                if (Coincide('='))
                    Emitir(TipoToken.IgualIgual, "==", lineaInicio, colInicio);
                else
                    Emitir(TipoToken.Igual, "=", lineaInicio, colInicio);
                break;

            case '!':
                if (Coincide('='))
                {
                    Emitir(TipoToken.Diferente, "!=", lineaInicio, colInicio);
                }
                else
                {
                    RegistrarError(
                        "ALB-L002",
                        new UbicacionFuente(_archivo, lineaInicio, colInicio, 1),
                        "Carácter inesperado '!'. En Alba utiliza 'no' para negación lógica o '!=' para desigualdad."
                    );
                }
                break;

            case '<':
                if (Coincide('='))
                    Emitir(TipoToken.MenorIgual, "<=", lineaInicio, colInicio);
                else
                    Emitir(TipoToken.Menor, "<", lineaInicio, colInicio);
                break;

            case '>':
                if (Coincide('='))
                    Emitir(TipoToken.MayorIgual, ">=", lineaInicio, colInicio);
                else
                    Emitir(TipoToken.Mayor, ">", lineaInicio, colInicio);
                break;

            case '(':
                _profundidadParentesis++;
                Emitir(TipoToken.ParentesisIzq, "(", lineaInicio, colInicio);
                break;

            case ')':
                if (_profundidadParentesis > 0) _profundidadParentesis--;
                Emitir(TipoToken.ParentesisDer, ")", lineaInicio, colInicio);
                break;

            case '[':
                _profundidadCorchetes++;
                Emitir(TipoToken.CorcheteIzq, "[", lineaInicio, colInicio);
                break;

            case ']':
                if (_profundidadCorchetes > 0) _profundidadCorchetes--;
                Emitir(TipoToken.CorcheteDer, "]", lineaInicio, colInicio);
                break;

            case '{':
                Emitir(TipoToken.LlaveIzq, "{", lineaInicio, colInicio);
                break;

            case '}':
                Emitir(TipoToken.LlaveDer, "}", lineaInicio, colInicio);
                break;

            case ',':
                Emitir(TipoToken.Coma, ",", lineaInicio, colInicio);
                break;

            case '.':
                Emitir(TipoToken.Punto, ".", lineaInicio, colInicio);
                break;

            case ':':
                Emitir(TipoToken.DosPuntos, ":", lineaInicio, colInicio);
                break;

            case ';':
                Emitir(TipoToken.PuntoYComa, ";", lineaInicio, colInicio);
                break;

            case '&':
                RegistrarError(
                    "ALB-L002",
                    new UbicacionFuente(_archivo, lineaInicio, colInicio, 1),
                    "Carácter inesperado '&'. En Alba se utiliza la palabra reservada 'y' para conjunción lógica."
                );
                break;

            case '|':
                RegistrarError(
                    "ALB-L002",
                    new UbicacionFuente(_archivo, lineaInicio, colInicio, 1),
                    "Carácter inesperado '|'. En Alba se utiliza la palabra reservada 'o' para disyunción lógica."
                );
                break;

            default:
                RegistrarError(
                    "ALB-L002",
                    new UbicacionFuente(_archivo, lineaInicio, colInicio, 1),
                    $"Carácter no válido en el código fuente: '{c}'."
                );
                break;
        }
    }

    private void Emitir(TipoToken tipo, string lexema, int linea, int columna)
    {
        _tokens.Add(new Token(
            tipo,
            lexema,
            null,
            new UbicacionFuente(_archivo, linea, columna, lexema.Length)
        ));
    }

    private void RegistrarError(string codigo, UbicacionFuente ubicacion, string mensaje)
    {
        string lineaCodigo = FormateadorErrores.ExtraerLinea(_codigoFuente, ubicacion.Linea);
        _errores.Add(new ErrorAlba(codigo, TipoError.Lexico, ubicacion, mensaje, lineaCodigo));
    }

    private bool EstaAlFinal() => _indice >= _codigoFuente.Length;

    private char MirarActual() => EstaAlFinal() ? '\0' : _codigoFuente[_indice];

    private char MirarSiguiente() => _indice + 1 >= _codigoFuente.Length ? '\0' : _codigoFuente[_indice + 1];

    private char Avanzar()
    {
        char c = _codigoFuente[_indice++];
        _columna++;
        return c;
    }

    private bool Coincide(char esperado)
    {
        if (EstaAlFinal() || _codigoFuente[_indice] != esperado)
        {
            return false;
        }
        _indice++;
        _columna++;
        return true;
    }

    private static bool EsInicioIdentificador(char c) => char.IsLetter(c) || c == '_';

    private static bool EsContinuacionIdentificador(char c) => char.IsLetterOrDigit(c) || c == '_';
}
