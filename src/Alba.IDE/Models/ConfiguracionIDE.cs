namespace Alba.IDE.Models;

/// <summary>
/// Colores personalizables para el resaltado de sintaxis de Cibao Code 9.0.
/// </summary>
public sealed class ColoresSintaxisConfig
{
    public string Comentario { get; set; } = "#6A9955";
    public string Cadena { get; set; } = "#CE9178";
    public string Numero { get; set; } = "#B5CEA8";
    public string PalabraControl { get; set; } = "#C586C0";
    public string PalabraDeclaracion { get; set; } = "#569CD6";
    public string TipoDato { get; set; } = "#4EC9B0";
    public string ValorEspecial { get; set; } = "#4FC1FF";
    public string FuncionIntegrada { get; set; } = "#DCDCAA";
    public string OperadorLogico { get; set; } = "#C586C0";
    public string Delimitador { get; set; } = "#D4D4D4";

    public ColoresSintaxisConfig Clonar() => (ColoresSintaxisConfig)MemberwiseClone();
}

/// <summary>
/// Preferencias persistentes completas de Xynex Code Editor 9.0.
/// </summary>
public sealed class ConfiguracionIDE
{
    public string TemaId { get; set; } = "xynex-vs-pro";
    public bool UsarSintaxisPersonalizada { get; set; }
    public ColoresSintaxisConfig SintaxisPersonalizada { get; set; } = new();

    // Tipografía y colores personalizados del editor
    public string FuenteEditor { get; set; } = "JetBrains Mono";
    public double TamanoFuenteEditor { get; set; } = 14.0;
    public int TamanoTabulacion { get; set; } = 4;
    public bool UsarColoresEditorPersonalizados { get; set; }
    public string ColorFondoEditorPersonalizado { get; set; } = "#1E1E1E";
    public string ColorTextoEditorPersonalizado { get; set; } = "#D4D4D4";

    // Interruptores de experiencia del editor
    public bool MostrarNumerosLinea { get; set; } = true;
    public bool AjusteDeLinea { get; set; }
    public bool ResaltarLineaActual { get; set; } = true;
    public bool MostrarEspaciosEnBlanco { get; set; }
    public bool DiagnosticosEnVivo { get; set; } = true;
    public bool AutocompletadoAutomatico { get; set; } = true;

    // Paneles y sesión
    public bool GuardadoAutomatico { get; set; } = true;
    public int IntervaloGuardadoSegundos { get; set; } = 15;
    public bool RestaurarUltimaSesion { get; set; } = true;
    public bool MostrarPanelLateral { get; set; } = true;
    public bool MostrarPanelInferior { get; set; } = true;
    public bool MostrarPanelAsistente { get; set; } = true;
    public bool MostrarBarraEstado { get; set; } = true;

    // Actualizador automático integrado (silencioso por defecto en v9.0)
    public bool ComprobarActualizacionesAlIniciar { get; set; } = true;
    public bool DescargarActualizacionesAutomaticamente { get; set; } = true;
    public bool ActualizacionSilenciosaAutomatica { get; set; } = true;

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
