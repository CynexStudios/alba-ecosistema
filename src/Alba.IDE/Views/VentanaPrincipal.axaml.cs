using System.Diagnostics;
using Alba.IDE.Models;
using Alba.IDE.Services;
using Alba.IDE.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace Alba.IDE.Views;

public partial class VentanaPrincipal : Window
{
    private readonly VentanaPrincipalViewModel _vm;

    public VentanaPrincipal() : this(null)
    {
    }

    public VentanaPrincipal(string? rutaInicial)
    {
        InitializeComponent();

        CargarIconoVentana();

        _vm = new VentanaPrincipalViewModel();
        DataContext = _vm;

        EditorPrincipal.ConfigurarServicios(_vm.ServicioAutocompletado, _vm.GestorAddons, _vm.GestorTemas, _vm.Configuracion);
        EditorPrincipal.CursorCambiado += (linea, col) =>
        {
            _vm.PosicionCursor = $"Ln {linea}, Col {col}";
        };
        EditorPrincipal.TamanoFuenteCambiado += nueva =>
        {
            _vm.Configuracion.TamanoFuenteEditor = nueva;
            _vm.GestorConfiguracion.GuardarConfiguracion();
        };

        _vm.GestorTemas.TemaCambiado += tema =>
        {
            EditorPrincipal.AplicarTema(tema, _vm.Configuracion);
        };

        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(VentanaPrincipalViewModel.ArchivoActivo))
            {
                EditorPrincipal.CargarArchivo(_vm.ArchivoActivo);
                EditorPrincipal.ActualizarMarcadoresErrores(_vm.Problemas);
            }
            else if (e.PropertyName == nameof(VentanaPrincipalViewModel.TotalProblemas))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_vm.ArchivoActivo is not null)
                    {
                        var propios = _vm.Problemas.Where(p =>
                            p.RutaArchivo.Equals(_vm.ArchivoActivo.RutaArchivo, StringComparison.OrdinalIgnoreCase));
                        EditorPrincipal.ActualizarMarcadoresErrores(propios);
                    }
                    else
                    {
                        EditorPrincipal.ActualizarMarcadoresErrores([]);
                    }
                });
            }
        };

        _vm.SolicitarIrAProblema += p => AbrirYEnfocarLinea(p.RutaArchivo, p.Linea, p.Columna);

        _vm.Consola.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ConsolaViewModel.EsperandoEntrada) && _vm.Consola.EsperandoEntrada)
            {
                Dispatcher.UIThread.Post(() => TxtEntradaConsola.Focus());
            }
        };

        _vm.Consola.Mensajes.CollectionChanged += (_, _) =>
        {
            if (_vm.Consola.Mensajes.Count > 0)
            {
                Dispatcher.UIThread.Post(() => ListaConsola.ScrollIntoView(_vm.Consola.Mensajes[^1]));
            }
        };

        _vm.LineasTerminal.CollectionChanged += (_, _) =>
        {
            if (_vm.LineasTerminal.Count > 0)
            {
                Dispatcher.UIThread.Post(() => ListaTerminal.ScrollIntoView(_vm.LineasTerminal[^1]));
            }
        };

        _vm.InicializarSegunArgumento(rutaInicial);
        EditorPrincipal.CargarArchivo(_vm.ArchivoActivo);

        KeyDown += AlAtajoTeclado;
        Closing += (_, _) =>
        {
            _vm.GuardarEstadoSesion();
            _vm.Dispose();
        };

        if (_vm.Configuracion.ComprobarActualizacionesAlIniciar)
        {
            _ = _vm.ComprobarActualizacionesAsync(silencioso: true);
        }
    }

    private void CargarIconoVentana()
    {
        try
        {
            using Stream stream = AssetLoader.Open(new Uri("avares://Alba.IDE/Assets/alba-studio-256.png"));
            Icon = new WindowIcon(new Bitmap(stream));
        }
        catch
        {
            // Mantiene el icono predeterminado si no está embebido en un contexto de prueba
        }
    }

    private async void AlNuevoProyecto(object? sender, RoutedEventArgs e)
    {
        var dialogoNombre = new DialogoEntradaTexto(
            "Nuevo Proyecto Alba",
            "Nombre del proyecto:",
            "MiProyectoAlba");
        string? nombre = await dialogoNombre.ShowDialog<string?>(this);
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return;
        }

        IReadOnlyList<IStorageFolder> carpetas = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Seleccionar carpeta destino para el proyecto",
                AllowMultiple = false
            });

        if (carpetas.Count > 0)
        {
            string? ruta = carpetas[0].TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(ruta))
            {
                _vm.CrearNuevoProyecto(ruta, nombre);
            }
        }
    }

    private async void AlAbrirCarpeta(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFolder> carpetas = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Abrir carpeta de proyecto",
                AllowMultiple = false
            });

        if (carpetas.Count > 0)
        {
            string? ruta = carpetas[0].TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(ruta))
            {
                _vm.AbrirCarpetaProyecto(ruta);
            }
        }
    }

    private async void AlAbrirArchivo(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFile> archivos = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Abrir archivo de código",
                AllowMultiple = true
            });

        foreach (IStorageFile archivo in archivos)
        {
            string? ruta = archivo.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(ruta))
            {
                _vm.AbrirArchivo(ruta);
            }
        }
    }

    private async void AlNuevoArchivo(object? sender, RoutedEventArgs e)
    {
        string? carpetaDestino = ObtenerCarpetaDestinoExplorador();
        if (carpetaDestino is null) return;

        var dialogo = new DialogoEntradaTexto("Nuevo Archivo", "Nombre del archivo (ej: modulo.alb):", "nuevo.alb");
        string? nombre = await dialogo.ShowDialog<string?>(this);
        if (!string.IsNullOrWhiteSpace(nombre))
        {
            _vm.CrearArchivoEnProyecto(carpetaDestino, nombre);
        }
    }

    private async void AlNuevaCarpeta(object? sender, RoutedEventArgs e)
    {
        string? carpetaDestino = ObtenerCarpetaDestinoExplorador();
        if (carpetaDestino is null) return;

        var dialogo = new DialogoEntradaTexto("Nueva Carpeta", "Nombre de la carpeta:", "modulos");
        string? nombre = await dialogo.ShowDialog<string?>(this);
        if (!string.IsNullOrWhiteSpace(nombre))
        {
            _vm.CrearCarpetaEnProyecto(carpetaDestino, nombre);
        }
    }

    private async void AlRenombrarSeleccionado(object? sender, RoutedEventArgs e)
    {
        if (ArbolProyecto.SelectedItem is not NodoProyecto nodo ||
            nodo.RutaCompleta.Equals(_vm.RutaProyectoActual, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var dialogo = new DialogoEntradaTexto("Renombrar", "Nuevo nombre:", nodo.Nombre);
        string? nuevoNombre = await dialogo.ShowDialog<string?>(this);
        if (!string.IsNullOrWhiteSpace(nuevoNombre))
        {
            _vm.RenombrarNodo(nodo, nuevoNombre);
        }
    }

    private void AlDuplicarSeleccionado(object? sender, RoutedEventArgs e)
    {
        if (ArbolProyecto.SelectedItem is NodoProyecto nodo && !nodo.EsCarpeta)
        {
            _vm.DuplicarArchivoNodo(nodo);
        }
    }

    private async void AlEliminarSeleccionado(object? sender, RoutedEventArgs e)
    {
        if (ArbolProyecto.SelectedItem is not NodoProyecto nodo ||
            nodo.RutaCompleta.Equals(_vm.RutaProyectoActual, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string tipo = nodo.EsCarpeta ? "la carpeta y todo su contenido" : "el archivo";
        var confirmacion = new DialogoConfirmacion(
            "Confirmar eliminación",
            $"¿Eliminar '{nodo.Nombre}'?",
            $"Esta acción eliminará permanentemente {tipo} del disco.",
            "Eliminar");

        if (await confirmacion.ShowDialog<bool>(this))
        {
            _vm.EliminarNodo(nodo);
        }
    }

    private void AlDobleClicArbol(object? sender, TappedEventArgs e)
    {
        if (ArbolProyecto.SelectedItem is NodoProyecto nodo && !nodo.EsCarpeta)
        {
            _vm.AbrirArchivo(nodo.RutaCompleta);
        }
    }

    private void AlCerrarPestanaClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ArchivoAbierto archivo)
        {
            _ = CerrarPestanaConConfirmacionAsync(archivo);
        }
    }

    private void AlCerrarPestanaMenu(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Tag is ArchivoAbierto archivo)
        {
            _ = CerrarPestanaConConfirmacionAsync(archivo);
        }
    }

    private void AlCerrarOtrasPestanasMenu(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Tag is ArchivoAbierto archivo)
        {
            _vm.CerrarOtrasPestanas(archivo);
        }
    }

    private void AlCerrarTodasPestanasMenu(object? sender, RoutedEventArgs e) => _vm.CerrarTodasPestanas();

    private async Task CerrarPestanaConConfirmacionAsync(ArchivoAbierto archivo)
    {
        if (archivo.TieneCambiosSinGuardar)
        {
            var dlg = new DialogoConfirmacion(
                "Cambios sin guardar",
                $"¿Descartar cambios en '{archivo.NombreArchivo}'?",
                "El archivo tiene modificaciones que no se han guardado.",
                "Descartar y cerrar");
            if (!await dlg.ShowDialog<bool>(this))
            {
                return;
            }
        }
        _vm.CerrarPestana(archivo);
    }

    private void AlBuscarReemplazar(object? sender, RoutedEventArgs e)
    {
        var dialogo = new DialogoBuscarReemplazar(EditorPrincipal);
        dialogo.Show(this);
    }

    private void AlAbrirBusquedaProyecto(object? sender, RoutedEventArgs e)
    {
        _vm.MostrarPantallaInicio = false;
        _vm.SeccionLateral = SeccionBarraLateral.Busqueda;
        _vm.MostrarBarraLateral = true;
        Dispatcher.UIThread.Post(() => TxtBusquedaProyecto.Focus());
    }

    private async void AlAbrirAddons(object? sender, RoutedEventArgs e)
    {
        var ventana = new VentanaAddons(_vm.GestorAddons);
        await ventana.ShowDialog(this);
        _vm.RecargarListaAddons();
        if (_vm.ArchivoActivo is not null)
        {
            EditorPrincipal.AplicarResaltadoPorExtension(_vm.ArchivoActivo.RutaArchivo);
            _vm.ArchivoActivo.Lenguaje = _vm.DetectarNombreLenguaje(_vm.ArchivoActivo.RutaArchivo);
        }
    }

    private async void AlAbrirConfiguracion(object? sender, RoutedEventArgs e)
    {
        var ventana = new VentanaConfiguracion(_vm);
        await ventana.ShowDialog(this);
        EditorPrincipal.AplicarTema(_vm.GestorTemas.TemaActual, _vm.Configuracion);
    }

    private void AlAbrirDocumentacion(object? sender, RoutedEventArgs e)
    {
        var doc = new VentanaDocumentacion();
        doc.Show(this);
    }

    private void AlAbrirPaletaComandos(object? sender, RoutedEventArgs e) => MostrarPaletaComandos();

    private void AlAbrirArchivosRapidos(object? sender, RoutedEventArgs e) => MostrarAperturaRapidaArchivos();

    private void MostrarPaletaComandos()
    {
        var comandos = new List<ComandoPaleta>
        {
            new() { Id = "nuevo-proyecto", Titulo = "Crear nuevo proyecto Alba...", Categoria = "Proyecto", Ejecutar = () => AlNuevoProyecto(this, new RoutedEventArgs()) },
            new() { Id = "abrir-carpeta", Titulo = "Abrir carpeta de proyecto...", Categoria = "Proyecto", Ejecutar = () => AlAbrirCarpeta(this, new RoutedEventArgs()) },
            new() { Id = "abrir-archivo", Titulo = "Abrir archivo...", Categoria = "Archivo", Ejecutar = () => AlAbrirArchivo(this, new RoutedEventArgs()) },
            new() { Id = "guardar", Titulo = "Guardar archivo actual", Categoria = "Archivo", Atajo = _vm.Configuracion.AtajosTeclado.GetValueOrDefault("Guardar", "Ctrl+S"), Ejecutar = () => _vm.GuardarArchivoActivo() },
            new() { Id = "ejecutar", Titulo = "Ejecutar archivo actual", Categoria = "Ejecución", Atajo = _vm.Configuracion.AtajosTeclado.GetValueOrDefault("Ejecutar", "F5"), Ejecutar = () => _ = _vm.EjecutarActivoAsync() },
            new() { Id = "verificar", Titulo = "Verificar sintaxis del archivo actual", Categoria = "Diagnóstico", Atajo = _vm.Configuracion.AtajosTeclado.GetValueOrDefault("Verificar", "Ctrl+Shift+B"), Ejecutar = () => _vm.VerificarArchivoActivo() },
            new() { Id = "verificar-proyecto", Titulo = "Verificar todos los archivos .alb del proyecto", Categoria = "Diagnóstico", Ejecutar = () => _vm.VerificarTodoElProyecto() },
            new() { Id = "buscar-proyecto", Titulo = "Buscar en todo el proyecto", Categoria = "Búsqueda", Atajo = _vm.Configuracion.AtajosTeclado.GetValueOrDefault("BuscarProyecto", "Ctrl+Shift+F"), Ejecutar = () => AlAbrirBusquedaProyecto(this, new RoutedEventArgs()) },
            new() { Id = "abrir-rapido", Titulo = "Ir a archivo del proyecto...", Categoria = "Navegación", Atajo = _vm.Configuracion.AtajosTeclado.GetValueOrDefault("AbrirRapido", "Ctrl+P"), Ejecutar = MostrarAperturaRapidaArchivos },
            new() { Id = "terminal", Titulo = "Abrir Terminal integrada", Categoria = "Paneles", Ejecutar = () => _vm.SeleccionarPestanaInferior(PestanaPanelInferior.Terminal) },
            new() { Id = "tema", Titulo = "Alternar tema visual", Categoria = "Apariencia", Ejecutar = () => _vm.AlternarTemaRapido() },
            new() { Id = "configuracion", Titulo = "Abrir Configuración", Categoria = "IDE", Ejecutar = () => AlAbrirConfiguracion(this, new RoutedEventArgs()) },
            new() { Id = "addons", Titulo = "Abrir Gestor de Complementos (Addons)", Categoria = "Addons", Ejecutar = () => AlAbrirAddons(this, new RoutedEventArgs()) },
            new() { Id = "docs", Titulo = "Abrir Documentación de Alba", Categoria = "Ayuda", Ejecutar = () => AlAbrirDocumentacion(this, new RoutedEventArgs()) },
            new() { Id = "actualizaciones", Titulo = "Comprobar actualizaciones en GitHub", Categoria = "Ayuda", Ejecutar = () => _ = _vm.ComprobarActualizacionesAsync() },
            new() { Id = "inicio", Titulo = "Ir a Pantalla de Inicio", Categoria = "Vista", Ejecutar = () => _vm.MostrarPantallaInicio = true }
        };

        var paleta = new VentanaPaletaComandos(comandos, "Escribe un comando de Alba Studio...");
        paleta.ShowDialog(this);
    }

    private void MostrarAperturaRapidaArchivos()
    {
        IReadOnlyList<string> archivos = BuscadorProyecto.ListarArchivosProyecto(_vm.RutaProyectoActual);
        string? raiz = _vm.RutaProyectoActual;
        var comandos = archivos.Select(ruta =>
        {
            string rel = !string.IsNullOrWhiteSpace(raiz) ? Path.GetRelativePath(raiz, ruta) : Path.GetFileName(ruta);
            return new ComandoPaleta
            {
                Id = ruta,
                Titulo = rel,
                Categoria = Path.GetExtension(ruta).ToUpperInvariant().TrimStart('.'),
                Ejecutar = () => _vm.AbrirArchivo(ruta)
            };
        }).ToList();

        var paleta = new VentanaPaletaComandos(comandos, "Escribe el nombre de un archivo del proyecto...");
        paleta.ShowDialog(this);
    }

    private void AlIrInicio(object? sender, RoutedEventArgs e) => _vm.MostrarPantallaInicio = true;
    private void AlSalir(object? sender, RoutedEventArgs e) => Close();
    private void AlAlternarBarraLateral(object? sender, RoutedEventArgs e) => _vm.MostrarBarraLateral = !_vm.MostrarBarraLateral;
    private void AlAlternarPanelInferior(object? sender, RoutedEventArgs e) => _vm.MostrarPanelInferior = !_vm.MostrarPanelInferior;
    private void AlCambiarTemaRapido(object? sender, RoutedEventArgs e) => _vm.AlternarTemaRapido();
    private void AlVerificarProyecto(object? sender, RoutedEventArgs e) => _vm.VerificarTodoElProyecto();
    private void AlComprobarActualizaciones(object? sender, RoutedEventArgs e) => _ = _vm.ComprobarActualizacionesAsync();

    private void AlAbrirTerminal(object? sender, RoutedEventArgs e)
    {
        _vm.MostrarPantallaInicio = false;
        _vm.SeleccionarPestanaInferior(PestanaPanelInferior.Terminal);
        Dispatcher.UIThread.Post(() => TxtComandoTerminal.Focus());
    }

    private void AlAbrirEjemploHolaMundo(object? sender, RoutedEventArgs e)
    {
        string? dirEjemplos = RutasUsuarioAlba.BuscarDirectorioInstalacion("examples");
        if (!string.IsNullOrWhiteSpace(dirEjemplos) && Directory.Exists(dirEjemplos))
        {
            _vm.AbrirCarpetaProyecto(dirEjemplos);
            string hola = Path.Combine(dirEjemplos, "01_hola_mundo.alb");
            if (File.Exists(hola)) _vm.AbrirArchivo(hola);
        }
        else
        {
            string carpetaDef = RutasUsuarioAlba.ObtenerCarpetaProyectosPorDefecto();
            _vm.CrearNuevoProyecto(carpetaDef, "HolaAlba");
        }
    }

    private void AlAbrirRecienteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ElementoReciente item)
        {
            AbrirElementoReciente(item);
        }
    }

    private void AlQuitarRecienteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ElementoReciente item)
        {
            _vm.GestorConfiguracion.EliminarReciente(item.Ruta);
            _vm.SincronizarRecientes();
        }
    }

    private void AlAbrirRecienteDobleClic(object? sender, TappedEventArgs e)
    {
        if (sender is ListBox lb && lb.SelectedItem is ElementoReciente item)
        {
            AbrirElementoReciente(item);
        }
    }

    private void AbrirElementoReciente(ElementoReciente item)
    {
        if (item.EsProyecto && Directory.Exists(item.Ruta))
        {
            _vm.AbrirCarpetaProyecto(item.Ruta);
        }
        else if (!item.EsProyecto && File.Exists(item.Ruta))
        {
            _vm.AbrirArchivo(item.Ruta);
        }
        else
        {
            _vm.MostrarToast("La ruta ya no existe en disco.");
        }
    }

    private void AlRailExplorador(object? sender, RoutedEventArgs e) => _vm.SeleccionarSeccionLateral(SeccionBarraLateral.Explorador);
    private void AlRailBusqueda(object? sender, RoutedEventArgs e) => _vm.SeleccionarSeccionLateral(SeccionBarraLateral.Busqueda);
    private void AlRailAddons(object? sender, RoutedEventArgs e) => _vm.SeleccionarSeccionLateral(SeccionBarraLateral.Addons);
    private void AlRailHerramientas(object? sender, RoutedEventArgs e) => _vm.SeleccionarSeccionLateral(SeccionBarraLateral.Herramientas);

    private void AlTabConsola(object? sender, RoutedEventArgs e) => _vm.SeleccionarPestanaInferior(PestanaPanelInferior.Consola);
    private void AlTabProblemas(object? sender, RoutedEventArgs e) => _vm.SeleccionarPestanaInferior(PestanaPanelInferior.Problemas);
    private void AlTabTerminal(object? sender, RoutedEventArgs e)
    {
        _vm.SeleccionarPestanaInferior(PestanaPanelInferior.Terminal);
        Dispatcher.UIThread.Post(() => TxtComandoTerminal.Focus());
    }
    private void AlTabRegistro(object? sender, RoutedEventArgs e) => _vm.SeleccionarPestanaInferior(PestanaPanelInferior.Registro);

    private void AlLimpiarPanelInferior(object? sender, RoutedEventArgs e)
    {
        switch (_vm.PestanaInferior)
        {
            case PestanaPanelInferior.Consola:
                _vm.Consola.Limpiar();
                break;
            case PestanaPanelInferior.Terminal:
                _vm.LineasTerminal.Clear();
                break;
            case PestanaPanelInferior.Registro:
                _vm.RegistroActividad.Clear();
                break;
        }
    }

    private void AlSeleccionarResultadoBusqueda(object? sender, TappedEventArgs e)
    {
        if (ListaResultadosBusqueda.SelectedItem is ResultadoBusquedaProyecto res)
        {
            AbrirYEnfocarLinea(res.RutaArchivo, res.Linea, res.Columna);
        }
    }

    private void AlDobleClicProblema(object? sender, TappedEventArgs e)
    {
        if (ListaProblemas.SelectedItem is ProblemaCodigo p)
        {
            AbrirYEnfocarLinea(p.RutaArchivo, p.Linea, p.Columna);
        }
    }

    private void AbrirYEnfocarLinea(string rutaArchivo, int linea, int columna)
    {
        _vm.AbrirArchivo(rutaArchivo);
        Dispatcher.UIThread.Post(() => EditorPrincipal.IrALineaColumna(linea, columna));
    }

    private void AlAbrirEnExploradorSO(object? sender, RoutedEventArgs e)
    {
        if (!_vm.TieneProyectoAbierto || string.IsNullOrWhiteSpace(_vm.RutaProyectoActual)) return;
        try
        {
            string exe = OperatingSystem.IsWindows() ? "explorer.exe" : (OperatingSystem.IsMacOS() ? "open" : "xdg-open");
            Process.Start(new ProcessStartInfo(exe, _vm.RutaProyectoActual) { UseShellExecute = false });
        }
        catch
        {
            _vm.MostrarToast("No se pudo abrir el explorador del sistema.");
        }
    }

    private void AlTeclaBusquedaProyecto(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _vm.EjecutarBusquedaEnProyecto();
        }
    }

    private void AlTeclaTerminal(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _vm.EnviarComandoTerminal();
        }
    }

    private void AlTeclaEntradaConsola(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _vm.Consola.EnviarEntradaActual();
        }
    }

    private void AlAtajoTeclado(object? sender, KeyEventArgs e)
    {
        var atajos = _vm.Configuracion.AtajosTeclado;

        if (CoincideAtajo(e, atajos.GetValueOrDefault("PaletaComandos", "Ctrl+Shift+P")))
        {
            e.Handled = true;
            MostrarPaletaComandos();
        }
        else if (CoincideAtajo(e, atajos.GetValueOrDefault("AbrirRapido", "Ctrl+P")))
        {
            e.Handled = true;
            MostrarAperturaRapidaArchivos();
        }
        else if (CoincideAtajo(e, atajos.GetValueOrDefault("BuscarProyecto", "Ctrl+Shift+F")))
        {
            e.Handled = true;
            AlAbrirBusquedaProyecto(sender, e);
        }
        else if (CoincideAtajo(e, atajos.GetValueOrDefault("Guardar", "Ctrl+S")))
        {
            e.Handled = true;
            _vm.GuardarArchivoActivo();
        }
        else if (CoincideAtajo(e, atajos.GetValueOrDefault("BuscarArchivo", "Ctrl+F")))
        {
            e.Handled = true;
            AlBuscarReemplazar(sender, e);
        }
        else if (CoincideAtajo(e, atajos.GetValueOrDefault("Verificar", "Ctrl+Shift+B")))
        {
            e.Handled = true;
            _vm.VerificarArchivoActivo();
        }
        else if (CoincideAtajo(e, atajos.GetValueOrDefault("Ejecutar", "F5")))
        {
            e.Handled = true;
            _ = _vm.EjecutarActivoAsync();
        }
        else if (CoincideAtajo(e, atajos.GetValueOrDefault("AlternarBarraLateral", "Ctrl+B")))
        {
            e.Handled = true;
            _vm.MostrarBarraLateral = !_vm.MostrarBarraLateral;
        }
        else if (CoincideAtajo(e, atajos.GetValueOrDefault("AlternarPanelInferior", "Ctrl+J")))
        {
            e.Handled = true;
            _vm.MostrarPanelInferior = !_vm.MostrarPanelInferior;
        }
    }

    private static bool CoincideAtajo(KeyEventArgs e, string atajo)
    {
        if (string.IsNullOrWhiteSpace(atajo)) return false;
        string[] partes = atajo.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (partes.Length == 0) return false;

        bool reqCtrl = partes.Any(p => p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || p.Equals("Control", StringComparison.OrdinalIgnoreCase));
        bool reqShift = partes.Any(p => p.Equals("Shift", StringComparison.OrdinalIgnoreCase));
        bool reqAlt = partes.Any(p => p.Equals("Alt", StringComparison.OrdinalIgnoreCase));

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) != reqCtrl) return false;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) != reqShift) return false;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt) != reqAlt) return false;

        string teclaNombre = partes[^1];
        return Enum.TryParse<Key>(teclaNombre, ignoreCase: true, out Key teclaEsperada) && e.Key == teclaEsperada;
    }

    private string? ObtenerCarpetaDestinoExplorador()
    {
        if (ArbolProyecto.SelectedItem is NodoProyecto nodo)
        {
            return nodo.EsCarpeta ? nodo.RutaCompleta : Path.GetDirectoryName(nodo.RutaCompleta);
        }
        return _vm.RutaProyectoActual;
    }
}
