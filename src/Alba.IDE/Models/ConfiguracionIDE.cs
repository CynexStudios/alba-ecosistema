namespace Alba.IDE.Models;

/// <summary>
/// Colores personalizables para el resaltado de sintaxis de Alba.
/// </summary>
public sealed class ColoresSintaxisConfig
{
    public string Comentario { get; set; } = "#6A9955";
    public string Cadena { get; set; } = "#F4A261";
    public string Numero { get; set; } = "#B5CEA8";
    public string PalabraControl { get; set; } = "#C084FC";
    public string PalabraDeclaracion { get; set; } = "#38BDF8";
    public string TipoDato { get; set; } = "#2DD4BF";
    public string ValorEspecial { get; set; } = "#60A5FA";
    public string FuncionIntegrada { get; set; } = "#FACC15";
    public string OperadorLogico { get; set; } = "#C084FC";
    public string Delimitador { get; set; } = "#94A3B8";

    public ColoresSintaxisConfig Clonar() => (ColoresSintaxisConfig)MemberwiseClone();
}

/// <summary>
/// Preferencias persistentes del usuario para Alba Studio 2.0.
/// </summary>
public sealed class ConfiguracionIDE
{
    public string TemaId { get; set; } = "alba-crepusculo";
    public bool UsarSintaxisPersonalizada { get; set; }
    public ColoresSintaxisConfig SintaxisPersonalizada { get; set; } = new();
    public double TamanoFuenteEditor { get; set; } = 14.0;
    public int TamanoTabulacion { get; set; } = 4;
    public bool GuardadoAutomatico { get; set; }
    public int IntervaloGuardadoSegundos { get; set; } = 15;
    public bool RestaurarUltimaSesion { get; set; } = true;
    public bool ComprobarActualizacionesAlIniciar { get; set; } = true;
    public bool MostrarPanelLateral { get; set; } = true;
    public bool MostrarPanelInferior { get; set; } = true;

    public Dictionary<string, string> AtajosTeclado { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Guardar"] = "Ctrl+S",
        ["GuardarTodo"] = "Ctrl+Shift+S",
        ["Ejecutar"] = "F5",
        ["Verificar"] = "Ctrl+Shift+B",
        ["PaletaComandos"] = "Ctrl+Shift+P",
        ["AbrirRapido"] = "Ctrl+P",
        ["BuscarArchivo"] = "Ctrl+F",
        ["BuscarProyecto"] = "Ctrl+Shift+F",
        ["AlternarBarraLateral"] = "Ctrl+B",
        ["AlternarPanelInferior"] = "Ctrl+J",
        ["Configuracion"] = "Ctrl+Comma"
    };
}
