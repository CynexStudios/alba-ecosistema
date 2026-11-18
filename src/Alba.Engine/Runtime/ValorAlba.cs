using System.Globalization;
using System.Text;

namespace Alba.Engine.Runtime;

public enum TipoValorAlba
{
    Nulo,
    Entero,
    Decimal,
    Texto,
    Booleano,
    Lista,
    Funcion,
    FuncionNativa,
    Clase,
    Instancia
}

/// <summary>
/// Representación unificada de cualquier valor en tiempo de ejecución dentro de Alba.
/// </summary>
public sealed class ValorAlba
{
    public static readonly ValorAlba Nulo = new(TipoValorAlba.Nulo, null);
    public static readonly ValorAlba Verdadero = new(TipoValorAlba.Booleano, true);
    public static readonly ValorAlba Falso = new(TipoValorAlba.Booleano, false);

    public TipoValorAlba Tipo { get; }
    public object? Dato { get; }

    private ValorAlba(TipoValorAlba tipo, object? dato)
    {
        Tipo = tipo;
        Dato = dato;
    }

    public static ValorAlba CrearEntero(long valor) => new(TipoValorAlba.Entero, valor);
    public static ValorAlba CrearDecimal(double valor) => new(TipoValorAlba.Decimal, valor);
    public static ValorAlba CrearTexto(string valor) => new(TipoValorAlba.Texto, valor ?? string.Empty);
    public static ValorAlba CrearBooleano(bool valor) => valor ? Verdadero : Falso;
    public static ValorAlba CrearLista(List<ValorAlba> elementos) => new(TipoValorAlba.Lista, elementos);
    public static ValorAlba CrearFuncion(FuncionAlba funcion) => new(TipoValorAlba.Funcion, funcion);
    public static ValorAlba CrearFuncionNativa(FuncionNativaAlba funcion) => new(TipoValorAlba.FuncionNativa, funcion);
    public static ValorAlba CrearClase(DefinicionClase clase) => new(TipoValorAlba.Clase, clase);
    public static ValorAlba CrearInstancia(InstanciaAlba instancia) => new(TipoValorAlba.Instancia, instancia);

    public long ComoEntero() => (long)Dato!;
    public double ComoDecimal() => (double)Dato!;
    public double ComoNumeroDouble() => Tipo == TipoValorAlba.Entero ? (long)Dato! : (double)Dato!;
    public string ComoTexto() => (string)Dato!;
    public bool ComoBooleano() => (bool)Dato!;
    public List<ValorAlba> ComoLista() => (List<ValorAlba>)Dato!;
    public FuncionAlba ComoFuncion() => (FuncionAlba)Dato!;
    public FuncionNativaAlba ComoFuncionNativa() => (FuncionNativaAlba)Dato!;
    public DefinicionClase ComoClase() => (DefinicionClase)Dato!;
    public InstanciaAlba ComoInstancia() => (InstanciaAlba)Dato!;

    public bool EsNumero => Tipo is TipoValorAlba.Entero or TipoValorAlba.Decimal;

    public string ObtenerNombreTipo() => Tipo switch
    {
        TipoValorAlba.Nulo => "nulo",
        TipoValorAlba.Entero => "entero",
        TipoValorAlba.Decimal => "decimal",
        TipoValorAlba.Texto => "texto",
        TipoValorAlba.Booleano => "booleano",
        TipoValorAlba.Lista => "lista",
        TipoValorAlba.Funcion => "funcion",
        TipoValorAlba.FuncionNativa => "funcion",
        TipoValorAlba.Clase => "clase",
        TipoValorAlba.Instancia => ComoInstancia().Clase.Nombre,
        _ => "desconocido"
    };

    public bool EsCompatibleConAnotacion(string nombreTipo)
    {
        return nombreTipo switch
        {
            "entero" => Tipo == TipoValorAlba.Entero,
            "decimal" => Tipo is TipoValorAlba.Decimal or TipoValorAlba.Entero,
            "texto" => Tipo == TipoValorAlba.Texto,
            "booleano" => Tipo == TipoValorAlba.Booleano,
            "lista" => Tipo == TipoValorAlba.Lista,
            "nulo" => Tipo == TipoValorAlba.Nulo,
            "vacio" => Tipo == TipoValorAlba.Nulo,
            _ => Tipo == TipoValorAlba.Nulo ||
                 (Tipo == TipoValorAlba.Instancia && ComoInstancia().Clase.Nombre == nombreTipo)
        };
    }

    /// <summary>
    /// Normaliza el valor cuando se asigna a una variable anotada (ej: promociona entero a decimal).
    /// </summary>
    public ValorAlba AjustarParaAnotacion(string? nombreTipo)
    {
        if (nombreTipo == "decimal" && Tipo == TipoValorAlba.Entero)
        {
            return CrearDecimal((double)ComoEntero());
        }
        return this;
    }

    public static ValorAlba ValorPorDefectoParaTipo(string? nombreTipo) => nombreTipo switch
    {
        "entero" => CrearEntero(0),
        "decimal" => CrearDecimal(0.0),
        "texto" => CrearTexto(string.Empty),
        "booleano" => Falso,
        "lista" => CrearLista([]),
        _ => Nulo
    };

    public bool EsIgualA(ValorAlba otro)
    {
        if (Tipo == TipoValorAlba.Nulo && otro.Tipo == TipoValorAlba.Nulo)
            return true;
        if (Tipo == TipoValorAlba.Nulo || otro.Tipo == TipoValorAlba.Nulo)
            return false;

        if (EsNumero && otro.EsNumero)
        {
            if (Tipo == TipoValorAlba.Entero && otro.Tipo == TipoValorAlba.Entero)
                return ComoEntero() == otro.ComoEntero();
            return ComoNumeroDouble() == otro.ComoNumeroDouble();
        }

        if (Tipo != otro.Tipo)
            return false;

        return Tipo switch
        {
            TipoValorAlba.Texto => ComoTexto() == otro.ComoTexto(),
            TipoValorAlba.Booleano => ComoBooleano() == otro.ComoBooleano(),
            TipoValorAlba.Lista => CompararListas(ComoLista(), otro.ComoLista()),
            _ => ReferenceEquals(Dato, otro.Dato)
        };
    }

    private static bool CompararListas(List<ValorAlba> a, List<ValorAlba> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (!a[i].EsIgualA(b[i])) return false;
        }
        return true;
    }

    public string ATextoParaImprimir()
    {
        return Tipo switch
        {
            TipoValorAlba.Nulo => "nulo",
            TipoValorAlba.Entero => ComoEntero().ToString(CultureInfo.InvariantCulture),
            TipoValorAlba.Decimal => FormatearDecimal(ComoDecimal()),
            TipoValorAlba.Texto => ComoTexto(),
            TipoValorAlba.Booleano => ComoBooleano() ? "verdadero" : "falso",
            TipoValorAlba.Lista => FormatearLista(ComoLista()),
            TipoValorAlba.Funcion => $"<funcion {ComoFuncion().Nombre}>",
            TipoValorAlba.FuncionNativa => $"<funcion nativa {ComoFuncionNativa().Nombre}>",
            TipoValorAlba.Clase => $"<clase {ComoClase().Nombre}>",
            TipoValorAlba.Instancia => $"<instancia de {ComoInstancia().Clase.Nombre}>",
            _ => string.Empty
        };
    }

    private static string FormatearDecimal(double valor)
    {
        string s = valor.ToString("G15", CultureInfo.InvariantCulture);
        if (!s.Contains('.') && !s.Contains('E') && !s.Contains('e'))
        {
            s += ".0";
        }
        return s;
    }

    private static string FormatearLista(List<ValorAlba> elementos)
    {
        var sb = new StringBuilder("[");
        for (int i = 0; i < elementos.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            ValorAlba el = elementos[i];
            sb.Append(el.Tipo == TipoValorAlba.Texto ? $"\"{el.ComoTexto()}\"" : el.ATextoParaImprimir());
        }
        sb.Append(']');
        return sb.ToString();
    }

    public override string ToString() => ATextoParaImprimir();
}
