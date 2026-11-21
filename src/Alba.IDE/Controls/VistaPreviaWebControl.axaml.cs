using System.Collections.ObjectModel;
using System.Diagnostics;
using Alba.IDE.Models;
using Alba.IDE.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

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
            MarcoViewport.Background = Brush.Parse("#111827");
        }

        IBrush textoBrush = IntentarColor(res.ColorTextoCssHex, Brushes.WhiteSmoke);
        IBrush acentoBrush = IntentarColor(res.ColorAcentoCssHex, Brush.Parse("#007ACC"));
        IBrush tarjetaBrush = IntentarColor(res.FondoTarjetaCssHex, Brush.Parse("#1E293B"));

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
                ContenedorPreviewDom.Children.Add(ConstruirControlPreview(b, textoBrush, acentoBrush, tarjetaBrush));
            }
        }

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

    private Control ConstruirControlPreview(
        BloquePreviewWeb bloque,
        IBrush textoBrush,
        IBrush acentoBrush,
        IBrush tarjetaBrush)
    {
        switch (bloque.Etiqueta)
        {
            case "nav":
            case "header":
                return new Border
                {
                    Background = tarjetaBrush,
                    BorderBrush = acentoBrush,
                    BorderThickness = new Thickness(0, 0, 0, 2),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12, 8),
                    Child = new TextBlock
                    {
                        Text = bloque.Texto,
                        FontSize = 13,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = textoBrush,
                        TextWrapping = TextWrapping.Wrap
                    }
                };
            case "h1":
                return new TextBlock
                {
                    Text = bloque.Texto,
                    FontSize = _modoViewport == ModoViewportWeb.Movil ? 20 : 25,
                    FontWeight = FontWeight.Bold,
                    Foreground = textoBrush,
                    TextWrapping = TextWrapping.Wrap
                };
            case "h2":
                return new TextBlock
                {
                    Text = bloque.Texto,
                    FontSize = 18.5,
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
            case "img":
                if (!string.IsNullOrWhiteSpace(bloque.ImagenRuta) && File.Exists(bloque.ImagenRuta))
                {
                    try
                    {
                        return new Border
                        {
                            CornerRadius = new CornerRadius(6),
                            ClipToBounds = true,
                            MaxHeight = 220,
                            Child = new Image
                            {
                                Source = new Bitmap(bloque.ImagenRuta),
                                Stretch = Stretch.Uniform
                            }
                        };
                    }
                    catch
                    {
                        // Si la imagen falla al decodificarse, muestra marcador descriptivo
                    }
                }
                return new Border
                {
                    Background = tarjetaBrush,
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 6),
                    Child = new TextBlock
                    {
                        Text = $"[Imagen: {bloque.Texto} ({bloque.Href})]",
                        FontSize = 11.5,
                        Foreground = acentoBrush
                    }
                };
            case "table":
                var stackTabla = new StackPanel { Spacing = 4 };
                for (int r = 0; r < bloque.FilasTabla.Count; r++)
                {
                    var filaPanel = new Grid();
                    var celdas = bloque.FilasTabla[r];
                    for (int c = 0; c < celdas.Count; c++)
                    {
                        filaPanel.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                        var tb = new TextBlock
                        {
                            Text = celdas[c],
                            FontSize = 12,
                            FontWeight = r == 0 ? FontWeight.Bold : FontWeight.Normal,
                            Foreground = r == 0 ? acentoBrush : textoBrush,
                            Margin = new Thickness(4, 2),
                            TextWrapping = TextWrapping.Wrap
                        };
                        Grid.SetColumn(tb, c);
                        filaPanel.Children.Add(tb);
                    }
                    stackTabla.Children.Add(filaPanel);
                }
                return new Border
                {
                    Background = tarjetaBrush,
                    BorderBrush = Brush.Parse("#334155"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8),
                    Child = stackTabla
                };
            case "button":
                var btn = new Button
                {
                    Content = bloque.Texto,
                    Background = acentoBrush,
                    Foreground = Brushes.White,
                    FontWeight = FontWeight.SemiBold,
                    Padding = new Thickness(14, 7),
                    CornerRadius = new CornerRadius(6),
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                btn.Click += (_, _) => AgregarLogExterno(new MensajeConsolaWeb
                {
                    Nivel = "ok",
                    Origen = "Preview",
                    Mensaje = $"Clic en botón <button>: '{bloque.Texto}'"
                });
                return btn;
            case "a":
                var linkBtn = new Button
                {
                    Content = $"{bloque.Texto} ({(string.IsNullOrWhiteSpace(bloque.Href) ? "#" : bloque.Href)})",
                    Background = Brushes.Transparent,
                    Foreground = acentoBrush,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0, 2),
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
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 5),
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

    private void AlAbrirVentanaApp(object? sender, RoutedEventArgs e)
    {
        string url = TxtUrlWeb.Text?.Trim() ?? string.Empty;
        if (!ServicioSeguridad.EsUrlHttpSegura(url, permitirHttpLocalhost: true))
        {
            SolicitarAbrirExterno?.Invoke();
            return;
        }

        string[] navegadores = OperatingSystem.IsWindows()
            ? ["msedge", "chrome", "brave"]
            : ["google-chrome", "chromium", "chromium-browser", "brave", "microsoft-edge"];

        foreach (string nav in navegadores)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo
                {
                    FileName = nav,
                    ArgumentList = { $"--app={url}", "--new-window" },
                    UseShellExecute = true
                });
                if (p is not null)
                {
                    AgregarLogExterno(new MensajeConsolaWeb
                    {
                        Nivel = "ok",
                        Origen = "Ventana App",
                        Mensaje = $"Vista HTML dedicada abierta con {nav} en {url}"
                    });
                    return;
                }
            }
            catch
            {
                // Intenta siguiente navegador compatible
            }
        }

        SolicitarAbrirExterno?.Invoke();
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
