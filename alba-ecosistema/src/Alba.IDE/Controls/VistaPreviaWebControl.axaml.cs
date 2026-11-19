using System.Collections.ObjectModel;
using Alba.IDE.Models;
using Alba.IDE.Services;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace Alba.IDE.Controls;

public partial class VistaPreviaWebControl : UserControl
{
    private readonly ObservableCollection<MensajeConsolaWeb> _mensajes = [];
    private ModoViewportWeb _modoViewport = ModoViewportWeb.Escritorio;
    private string _ultimaRuta = string.Empty;
    private string _ultimoHtml = string.Empty;

    public bool AutoRecargaActiva => ChkAutoRecarga.IsChecked == true;

    public event Action? SolicitarRecarga;
    public event Action? SolicitarAbrirExterno;
    public event Action? SolicitarCerrar;
    public event Action<string>? SolicitarNavegarEnlaceRelativo;

    public VistaPreviaWebControl()
    {
        InitializeComponent();
        ListaConsolaWeb.ItemsSource = _mensajes;
        AplicarModoViewport(ModoViewportWeb.Escritorio);
    }

    public void ActualizarVistaPrevia(string? rutaArchivo, string contenidoHtml, string? urlLocal)
    {
        _ultimaRuta = rutaArchivo ?? string.Empty;
        _ultimoHtml = contenidoHtml ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(urlLocal))
        {
            TxtUrlWeb.Text = urlLocal;
        }

        ResultadoAnalisisWeb res = AnalizadorWebHtml.Analizar(_ultimaRuta, _ultimoHtml);
        TxtTituloPaginaWeb.Text = res.TituloPagina;

        try
        {
            MarcoViewport.Background = Brush.Parse(res.FondoCssHex);
        }
        catch
        {
            MarcoViewport.Background = Brush.Parse("#0D1320");
        }

        IBrush textoBrush = IntentarColor(res.ColorTextoCssHex, Brushes.WhiteSmoke);
        IBrush acentoBrush = IntentarColor(res.ColorAcentoCssHex, Brushes.DeepSkyBlue);

        ContenedorPreviewDom.Children.Clear();
        if (res.Bloques.Count == 0)
        {
            ContenedorPreviewDom.Children.Add(new TextBlock
            {
                Text = "Sin elementos visibles detectados en <body>.",
                FontSize = 12,
                Foreground = Brushes.SlateGray
            });
        }
        else
        {
            foreach (BloquePreviewWeb b in res.Bloques)
            {
                ContenedorPreviewDom.Children.Add(ConstruirControlPreview(b, textoBrush, acentoBrush));
            }
        }

        // Actualiza errores de análisis manteniendo logs recientes del navegador
        var logsNavegador = _mensajes.Where(m => m.Origen == "Navegador" || m.Origen == "HTTP 404").TakeLast(25).ToList();
        _mensajes.Clear();
        foreach (MensajeConsolaWeb err in res.Errores)
        {
            _mensajes.Add(err);
        }
        foreach (MensajeConsolaWeb nav in logsNavegador)
        {
            _mensajes.Add(nav);
        }
        ActualizarTituloConsola();
    }

    public void AgregarLogExterno(MensajeConsolaWeb mensaje)
    {
        _mensajes.Add(mensaje);
        while (_mensajes.Count > 100)
        {
            _mensajes.RemoveAt(0);
        }
        ActualizarTituloConsola();
    }

    private Control ConstruirControlPreview(BloquePreviewWeb bloque, IBrush textoBrush, IBrush acentoBrush)
    {
        switch (bloque.Etiqueta)
        {
            case "h1":
                return new TextBlock
                {
                    Text = bloque.Texto,
                    FontSize = _modoViewport == ModoViewportWeb.Movil ? 20 : 24,
                    FontWeight = FontWeight.Bold,
                    Foreground = textoBrush,
                    TextWrapping = TextWrapping.Wrap
                };
            case "h2":
                return new TextBlock
                {
                    Text = bloque.Texto,
                    FontSize = 18,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = textoBrush,
                    TextWrapping = TextWrapping.Wrap
                };
            case "h3":
            case "h4":
            case "h5":
            case "h6":
                return new TextBlock
                {
                    Text = bloque.Texto,
                    FontSize = 14.5,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = textoBrush,
                    TextWrapping = TextWrapping.Wrap
                };
            case "button":
                var btn = new Button
                {
                    Content = bloque.Texto,
                    Background = acentoBrush,
                    Foreground = Brushes.Black,
                    FontWeight = FontWeight.SemiBold,
                    Padding = new Avalonia.Thickness(12, 6),
                    CornerRadius = new Avalonia.CornerRadius(6),
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                btn.Click += (_, _) => AgregarLogExterno(new MensajeConsolaWeb
                {
                    Nivel = "ok",
                    Origen = "Preview",
                    Mensaje = $"Clic simulado en botón <button>: '{bloque.Texto}'"
                });
                return btn;
            case "a":
                var linkBtn = new Button
                {
                    Content = $"{bloque.Texto} ({(string.IsNullOrWhiteSpace(bloque.Href) ? "#" : bloque.Href)})",
                    Background = Brushes.Transparent,
                    Foreground = acentoBrush,
                    BorderThickness = new Avalonia.Thickness(0),
                    Padding = new Avalonia.Thickness(0, 2),
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                linkBtn.Click += (_, _) =>
                {
                    if (!string.IsNullOrWhiteSpace(bloque.Href) &&
                        !bloque.Href.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                        !bloque.Href.StartsWith('#'))
                    {
                        SolicitarNavegarEnlaceRelativo?.Invoke(bloque.Href);
                    }
                    else
                    {
                        AgregarLogExterno(new MensajeConsolaWeb
                        {
                            Nivel = "log",
                            Origen = "Enlace",
                            Mensaje = $"Destino del enlace: {bloque.Href}"
                        });
                    }
                };
                return linkBtn;
            case "input":
                return new TextBox
                {
                    Watermark = bloque.Texto,
                    FontSize = 12,
                    MaxWidth = 340,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
            case "code":
            case "pre":
                return new Border
                {
                    Background = Brush.Parse("#141D2F"),
                    CornerRadius = new Avalonia.CornerRadius(4),
                    Padding = new Avalonia.Thickness(8, 5),
                    Child = new TextBlock
                    {
                        Text = bloque.Texto,
                        FontFamily = new FontFamily("JetBrains Mono, Consolas, monospace"),
                        FontSize = 11.5,
                        Foreground = acentoBrush,
                        TextWrapping = TextWrapping.Wrap
                    }
                };
            default:
                return new TextBlock
                {
                    Text = bloque.Etiqueta == "li" ? $"• {bloque.Texto}" : bloque.Texto,
                    FontSize = 13,
                    Foreground = textoBrush,
                    TextWrapping = TextWrapping.Wrap
                };
        }
    }

    private void AplicarModoViewport(ModoViewportWeb modo)
    {
        _modoViewport = modo;
        switch (modo)
        {
            case ModoViewportWeb.Escritorio:
                MarcoViewport.Width = double.NaN;
                MarcoViewport.MinWidth = 460;
                MarcoViewport.HorizontalAlignment = HorizontalAlignment.Stretch;
                TxtDimensionesViewport.Text = "Escritorio (100%)";
                break;
            case ModoViewportWeb.Tablet:
                MarcoViewport.Width = 768;
                MarcoViewport.MinWidth = 768;
                MarcoViewport.HorizontalAlignment = HorizontalAlignment.Center;
                TxtDimensionesViewport.Text = "Tablet (768px)";
                break;
            case ModoViewportWeb.Movil:
                MarcoViewport.Width = 390;
                MarcoViewport.MinWidth = 390;
                MarcoViewport.HorizontalAlignment = HorizontalAlignment.Center;
                TxtDimensionesViewport.Text = "Móvil (390px)";
                break;
        }
        if (!string.IsNullOrWhiteSpace(_ultimoHtml))
        {
            ActualizarVistaPrevia(_ultimaRuta, _ultimoHtml, TxtUrlWeb.Text);
        }
    }

    private void ActualizarTituloConsola()
    {
        TxtTituloConsolaWeb.Text = $"CONSOLA WEB Y ERRORES ({_mensajes.Count})";
    }

    private static IBrush IntentarColor(string hex, IBrush defecto)
    {
        try { return Brush.Parse(hex); } catch { return defecto; }
    }

    private void AlRecargarPreview(object? sender, RoutedEventArgs e) => SolicitarRecarga?.Invoke();
    private void AlAbrirNavegadorExterno(object? sender, RoutedEventArgs e) => SolicitarAbrirExterno?.Invoke();
    private void AlCerrarPreview(object? sender, RoutedEventArgs e) => SolicitarCerrar?.Invoke();
    private void AlModoEscritorio(object? sender, RoutedEventArgs e) => AplicarModoViewport(ModoViewportWeb.Escritorio);
    private void AlModoTablet(object? sender, RoutedEventArgs e) => AplicarModoViewport(ModoViewportWeb.Tablet);
    private void AlModoMovil(object? sender, RoutedEventArgs e) => AplicarModoViewport(ModoViewportWeb.Movil);

    private void AlLimpiarConsolaWeb(object? sender, RoutedEventArgs e)
    {
        _mensajes.Clear();
        ActualizarTituloConsola();
    }
}
