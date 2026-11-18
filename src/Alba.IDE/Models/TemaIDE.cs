namespace Alba.IDE.Models;

/// <summary>
/// Define un tema visual completo de Alba Studio (UI + sintaxis).
/// </summary>
public sealed class TemaIDE
{
    public string Id { get; set; } = "alba-crepusculo";
    public string Nombre { get; set; } = "Alba Crepúsculo";
    public bool EsOscuro { get; set; } = true;
    public string FondoBase { get; set; } = "#0A0F1A";
    public string FondoSuperficie { get; set; } = "#101726";
    public string FondoPanel { get; set; } = "#0D1320";
    public string FondoBarra { get; set; } = "#141D2F";
    public string FondoEditor { get; set; } = "#0B111E";
    public string FondoLineaActual { get; set; } = "#162033";
    public string FondoHover { get; set; } = "#1E293B";
    public string BordeSutil { get; set; } = "#1E2A3E";
    public string TextoPrincipal { get; set; } = "#E2E8F0";
    public string TextoSecundario { get; set; } = "#94A3B8";
    public string TextoApagado { get; set; } = "#64748B";
    public string AcentoPrimario { get; set; } = "#38BDF8";
    public string AcentoSecundario { get; set; } = "#F59E0B";
    public string ExitoColor { get; set; } = "#22C55E";
    public string AdvertenciaColor { get; set; } = "#FBBF24";
    public string ErrorColor { get; set; } = "#EF4444";
    public ColoresSintaxisConfig Sintaxis { get; set; } = new();
}
