using System.Collections.Specialized;
using System.Diagnostics;
using Alba.IDE.Models;
using Alba.IDE.Services;
using Alba.IDE.ViewModels;
using Avalonia;
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
    private INotifyCollectionChanged? _suscripcionTerminalLineas;
    private VentanaActualizacion? _ventanaActualizadorActiva;
    private bool _actualizadorInicioMostrado;

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
            else if (e.PropertyName == nameof(VentanaPrincipalViewModel.LineasTerminalActiva))
            {
                EngancharScrollTerminal();
            }
        };

        _vm.SolicitarIrAProblema += p => AbrirYEnfocarLinea(p.RutaArchivo, p.Linea, p.Columna);

        _vm.ConsultarAutoRecargaWeb = () => VistaPreviaWeb.AutoRecargaActiva;
        _vm.SolicitarActualizarVistaWeb += (ruta, html, url) =>
            Dispatcher.UIThread.Post(() => VistaPreviaWeb.ActualizarVistaPrevia(ruta, html, url));
        _vm.LogWebRecibido += msg =>
            Dispatcher.UIThread.Post(() => VistaPreviaWeb.AgregarLogExterno(msg));

        VistaPreviaWeb.SolicitarRecarga += () => _vm.RefrescarVistaPreviaWeb(sincronizarDisco: true);
        VistaPreviaWeb.SolicitarAbrirExterno += () => _vm.AbrirWebEnNavegadorExterno();
        VistaPreviaWeb.SolicitarCerrar += () => _vm.CerrarVistaPreviaWeb();
        VistaPreviaWeb.SolicitarNavegarEnlaceRelativo += ResolverYNavegarEnlaceWeb;

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

        _vm.MensajesChatIA.CollectionChanged += (_, _) =>
        {
            if (_vm.MensajesChatIA.Count > 0)
            {
                Dispatcher.UIThread.Post(() => ListaChatIA.ScrollIntoView(_vm.MensajesChatIA[^1]));
            }
        };

        _vm.SolicitarGuardarBorradorComo += borrador =>
        {
            Dispatcher.UIThread.Post(async () => await GuardarBorradorComoAsync(borrador));
        };

        _vm.SolicitarCierrePorActualizacionAutomatica += () =>
        {
            Dispatcher.UIThread.Post(() => Close());
        };

        EngancharScrollTerminal();

        _vm.InicializarSegunArgumento(rutaInicial);
        EditorPrincipal.CargarArchivo(_vm.ArchivoActivo);

        KeyDown += AlAtajoTeclado;
        Closing += (_, _) =>
        {
            _vm.GuardarEstadoSesion();
            _vm.Dispose();
        };

        _vm.ActualizacionListaParaInstalar += info =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (!IsVisible || _actualizadorInicioMostrado || _ventanaActualizadorActiva is not null) return;
                _actualizadorInicioMostrado = true;
                AbrirActualizadorIntegrado(info);
            });
        };

        if (_vm.Configuracion.ComprobarActualizacionesAlIniciar)
        {
            _ = _vm.ComprobarActualizacionesAsync(silencioso: true);
        }
    }

    private void EngancharScrollTerminal()
    {
        if (_suscripcionTerminalLineas is not null)
        {
            _suscripcionTerminalLineas.CollectionChanged -= AlCambiarLineasTerminal;
        }

        _suscripcionTerminalLineas = _vm.LineasTerminalActiva;
        if (_suscripcionTerminalLineas is not null)
        {
            _suscripcionTerminalLineas.CollectionChanged += AlCambiarLineasTerminal;
        }
    }

    private void AlCambiarLineasTerminal(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var lineas = _vm.LineasTerminalActiva;
        if (lineas is not null && lineas.Count > 0)
        {
            Dispatcher.UIThread.Post(() => ListaTerminal.ScrollIntoView(lineas[^1]));
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

    private void AlCrearArchivoSinCarpeta(object? sender, RoutedEventArgs e)
    {
        _vm.CrearArchivoSinCarpeta(".cibao");
    }

    private void AlCrearScratchPorExtension(object? sender, RoutedEventArgs e)
    {
        if (sender is Control el && el.Tag is string ext && !string.IsNullOrWhiteSpace(ext))
        {
            _vm.CrearArchivoSinCarpeta(ext);
        }
    }

    private async Task GuardarBorradorComoAsync(ArchivoAbierto borrador)
    {
        IStorageFile? destino = await StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Guardar archivo sin carpeta en disco",
                SuggestedFileName = borrador.NombreArchivo
            });

        string? ruta = destino?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(ruta))
        {
            _vm.CompletarGuardadoBorradorEnRuta(borrador, ruta);
            EditorPrincipal.AplicarResaltadoPorExtension(ruta);
        }
    }

    private void AlCrearProyecto(object? sender, RoutedEventArgs e) =>
        _ = AbrirAsistenteCreacionAsync(ModalidadCreacion.Proyecto);

    private void AlCrearPrograma(object? sender, RoutedEventArgs e) =>
        _ = AbrirAsistenteCreacionAsync(ModalidadCreacion.Programa);

    private void AlCrearArchivoIndependiente(object? sender, RoutedEventArgs e) =>
        _ = AbrirAsistenteCreacionAsync(ModalidadCreacion.ArchivoIndependiente);

    private async Task AbrirAsistenteCreacionAsync(ModalidadCreacion modalidad)
    {
        string? carpetaSugerida = _vm.RutaProyectoActual ?? RutasUsuarioAlba.ObtenerCarpetaProyectosPorDefecto();
        var dialogo = new DialogoAsistenteCreacion(modalidad, _vm.DetectorEntornos, _vm.ServicioCreacion, carpetaSugerida);
        SolicitudCreacion? solicitud = await dialogo.ShowDialog<SolicitudCreacion?>(this);
        if (solicitud is not null)
        {
            _vm.EjecutarCreacion(solicitud);
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
        if (carpetaDestino is null)
        {
            await AbrirAsistenteCreacionAsync(ModalidadCreacion.ArchivoIndependiente);
            return;
        }

        var dialogo = new DialogoEntradaTexto("Nuevo Archivo", "Nombre del archivo (ej: modulo.cibao):", "nuevo.cibao");
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

    private void AlSeleccionarArchivoIndependiente(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox lb && lb.SelectedItem is ArchivoAbierto archivo)
        {
            _vm.ArchivoActivo = archivo;
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
        _vm.MostrarCentroTools = false;
        _vm.SeccionLateral = SeccionBarraLateral.Busqueda;
        _vm.MostrarBarraLateral = true;
        Dispatcher.UIThread.Post(() => TxtBusquedaProyecto.Focus());
    }

    private void AlAbrirTools(object? sender, RoutedEventArgs e)
    {
        _vm.AbrirCentroTools();
    }

    private void AlCerrarCentroTools(object? sender, RoutedEventArgs e)
    {
        _vm.CerrarCentroTools();
    }

    private async void AlReescanearTools(object? sender, RoutedEventArgs e)
    {
        _vm.MostrarToast("Escaneando SDKs, compiladores e intérpretes del equipo...");
        await _vm.RefrescarEntornosYToolsAsync(forzar: true);
        _vm.MostrarToast("Detección de herramientas actualizada.");
    }

    private async void AlInstalarAddonTools(object? sender, RoutedEventArgs e)
    {
        IReadOnlyList<IStorageFolder> carpetas = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Seleccionar carpeta del complemento (con manifest.json)",
                AllowMultiple = false
            });

        if (carpetas.Count == 0) return;
        string? ruta = carpetas[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(ruta)) return;

        await _vm.InstalarAddonEnToolsAsync(ruta);
        if (_vm.ArchivoActivo is not null)
        {
            EditorPrincipal.AplicarResaltadoPorExtension(_vm.ArchivoActivo.RutaArchivo);
            _vm.ArchivoActivo.Lenguaje = _vm.DetectarNombreLenguaje(_vm.ArchivoActivo.RutaArchivo);
        }
    }

    private async void AlDesinstalarToolSeleccionado(object? sender, RoutedEventArgs e)
    {
        if (_vm.ToolSeleccionado is null || !_vm.ToolSeleccionado.EsAddonLocal) return;

        var confirmacion = new DialogoConfirmacion(
            "Desinstalar complemento",
            $"¿Eliminar '{_vm.ToolSeleccionado.Nombre}'?",
            "Se eliminará la carpeta del complemento de tus addons de usuario.",
            "Desinstalar");

        if (await confirmacion.ShowDialog<bool>(this))
        {
            await _vm.DesinstalarToolSeleccionadoAsync();
        }
    }

    private async void AlInstalarPaqueteIntegradoTool(object? sender, RoutedEventArgs e)
    {
        if (_vm.ToolSeleccionado is null) return;

        var confirmacion = new DialogoConfirmacion(
            "Instalar paquete integrado",
            $"¿Instalar '{_vm.ToolSeleccionado.Nombre}'?",
            $"Permisos: {_vm.ToolSeleccionado.PermisosRequeridos}\nSe instalará el paquete verificado con resaltado XSHD y snippets.",
            "Instalar ahora");

        if (!await confirmacion.ShowDialog<bool>(this)) return;

        await _vm.InstalarPaqueteToolSeleccionadoAsync();
        if (_vm.ArchivoActivo is not null)
        {
            EditorPrincipal.AplicarResaltadoPorExtension(_vm.ArchivoActivo.RutaArchivo);
            _vm.ArchivoActivo.Lenguaje = _vm.DetectarNombreLenguaje(_vm.ArchivoActivo.RutaArchivo);
        }
    }

    private async void AlInstalarDependenciaProyecto(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not DependenciaProyectoItem dep) return;

        var confirmacion = new DialogoConfirmacion(
            "Instalar dependencias del proyecto",
            $"¿Instalar dependencias {dep.Nombre}?",
            $"Manifiesto: {dep.ArchivoManifiesto}\nComando: {dep.ComandoInstalacion}",
            "Confirmar e instalar");

        if (await confirmacion.ShowDialog<bool>(this))
        {
            await _vm.InstalarDependenciaProyectoAsync(dep);
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
        var doc = new VentanaDocumentacion(codigoEjemplo => _vm.CrearArchivoSinCarpeta(".cibao", codigoEjemplo));
        doc.Show(this);
    }

    private async void AlAbrirAddonsOnline(object? sender, RoutedEventArgs e)
    {
        var ventana = new VentanaAddons(_vm.GestorAddons);
        await ventana.ShowDialog(this);
        await _vm.RefrescarEntornosYToolsAsync(forzar: false);
        if (_vm.ArchivoActivo is not null)
        {
            EditorPrincipal.AplicarResaltadoPorExtension(_vm.ArchivoActivo.RutaArchivo);
            _vm.ArchivoActivo.Lenguaje = _vm.DetectarNombreLenguaje(_vm.ArchivoActivo.RutaArchivo);
        }
    }

    private async void AlAbrirCuentaUsuario(object? sender, RoutedEventArgs e) =>
        await AbrirDialogoCuentaAsync(abrirEnTabIA: false);

    private async void AlConectarTuIA(object? sender, RoutedEventArgs e) =>
        await AbrirDialogoCuentaAsync(abrirEnTabIA: true);

    private async Task AbrirDialogoCuentaAsync(bool abrirEnTabIA)
    {
        var dlg = new DialogoCuentaUsuario(_vm.BaseDatosUsuarios, abrirEnTabIA);
        await dlg.ShowDialog(this);
        _vm.RecargarEstadoCuentaEIA();
    }

    private void AlAbrirTutorCibao(object? sender, RoutedEventArgs e) => _vm.AbrirTutorCibaoDesdeCero();
    private void AlAlternarAsistente(object? sender, RoutedEventArgs e)
    {
        _vm.MostrarPantallaInicio = false;
        _vm.MostrarPanelAsistente = !_vm.MostrarPanelAsistente;
    }
    private void AlSeleccionarTabTutor(object? sender, RoutedEventArgs e) => _vm.PestanaDerechaActiva = 0;
    private void AlSeleccionarTabAgenteIA(object? sender, RoutedEventArgs e) => _vm.PestanaDerechaActiva = 1;
    private void AlAnteriorLeccionTutor(object? sender, RoutedEventArgs e) => _vm.AnteriorLeccionTutor();
    private void AlSiguienteLeccionTutor(object? sender, RoutedEventArgs e) => _vm.SiguienteLeccionTutor();
    private void AlInsertarLeccionTutor(object? sender, RoutedEventArgs e) => _vm.InsertarLeccionTutorEnEditor();
    private void AlProbarLeccionTutor(object? sender, RoutedEventArgs e)
    {
        _vm.InsertarLeccionTutorEnEditor();
        _ = _vm.EjecutarActivoAsync();
    }
    private void AlPreguntarTutorBot(object? sender, RoutedEventArgs e) => _vm.PreguntarAlTutorBot();
    private void AlTeclaPreguntaTutor(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _vm.PreguntarAlTutorBot();
        }
    }

    private void AlEnviarConsultaIA(object? sender, RoutedEventArgs e) => _ = _vm.EnviarConsultaIAAsync();
    private void AlTeclaConsultaIA(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            e.Handled = true;
            _ = _vm.EnviarConsultaIAAsync();
        }
    }
    private void AlLimpiarChatIA(object? sender, RoutedEventArgs e) => _vm.LimpiarChatIA();
    private void AlAplicarCodigoIA(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn) _vm.AplicarBloqueCodigoIA(btn.Tag as string);
    }
    private void AlNuevoArchivoCodigoIA(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn) _vm.CrearArchivoConCodigoIA(btn.Tag as string);
    }

    private void AlAbrirPaletaComandos(object? sender, RoutedEventArgs e) => MostrarPaletaComandos();

    private void AlAbrirArchivosRapidos(object? sender, RoutedEventArgs e) => MostrarAperturaRapidaArchivos();

    private void MostrarPaletaComandos()
    {
        var comandos = new List<ComandoPaleta>
        {
            new() { Id = "nuevo-sin-carpeta", Titulo = "Crear archivo rápido sin carpeta (Cibao 9.0)", Categoria = "Creación", Ejecutar = () => _vm.CrearArchivoSinCarpeta(".cibao") },
            new() { Id = "tutor-bot", Titulo = "Abrir Bot Tutor Cibao 9.0 (Aprender desde cero)", Categoria = "Aprendizaje", Ejecutar = () => _vm.AbrirTutorCibaoDesdeCero() },
            new() { Id = "agente-ia", Titulo = "Abrir Agente IA y conectar modelo propio", Categoria = "IA", Ejecutar = () => { _vm.MostrarPantallaInicio = false; _vm.MostrarPanelAsistente = true; _vm.PestanaDerechaActiva = 1; } },
            new() { Id = "addons-online", Titulo = "Abrir Tienda de Addons Online (API)", Categoria = "Extensiones", Ejecutar = () => AlAbrirAddonsOnline(this, new RoutedEventArgs()) },
            new() { Id = "cuenta-db", Titulo = "Cuenta Xynex y Bóveda de claves IA", Categoria = "Seguridad", Ejecutar = () => AlAbrirCuentaUsuario(this, new RoutedEventArgs()) },
            new() { Id = "nuevo-proyecto", Titulo = "Crear proyecto...", Categoria = "Creación", Ejecutar = () => AlCrearProyecto(this, new RoutedEventArgs()) },
            new() { Id = "nuevo-programa", Titulo = "Crear programa directo...", Categoria = "Creación", Ejecutar = () => AlCrearPrograma(this, new RoutedEventArgs()) },
            new() { Id = "nuevo-archivo-ind", Titulo = "Crear archivo independiente...", Categoria = "Creación", Ejecutar = () => AlCrearArchivoIndependiente(this, new RoutedEventArgs()) },
            new() { Id = "abrir-carpeta", Titulo = "Abrir carpeta de proyecto...", Categoria = "Proyecto", Ejecutar = () => AlAbrirCarpeta(this, new RoutedEventArgs()) },
            new() { Id = "abrir-archivo", Titulo = "Abrir archivo...", Categoria = "Archivo", Ejecutar = () => AlAbrirArchivo(this, new RoutedEventArgs()) },
            new() { Id = "guardar", Titulo = "Guardar archivo actual", Categoria = "Archivo", Atajo = _vm.Configuracion.AtajosTeclado.GetValueOrDefault("Guardar", "Ctrl+S"), Ejecutar = () => _vm.GuardarArchivoActivo() },
            new() { Id = "ejecutar", Titulo = "Ejecutar archivo actual", Categoria = "Ejecución", Atajo = _vm.Configuracion.AtajosTeclado.GetValueOrDefault("Ejecutar", "F5"), Ejecutar = () => _ = _vm.EjecutarActivoAsync() },
            new() { Id = "verificar", Titulo = "Verificar sintaxis del archivo actual", Categoria = "Diagnóstico", Atajo = _vm.Configuracion.AtajosTeclado.GetValueOrDefault("Verificar", "Ctrl+Shift+B"), Ejecutar = () => _vm.VerificarArchivoActivo() },
            new() { Id = "verificar-proyecto", Titulo = "Verificar todos los archivos Cibao del proyecto", Categoria = "Diagnóstico", Ejecutar = () => _vm.VerificarTodoElProyecto() },
            new() { Id = "buscar-proyecto", Titulo = "Buscar en todo el proyecto", Categoria = "Búsqueda", Atajo = _vm.Configuracion.AtajosTeclado.GetValueOrDefault("BuscarProyecto", "Ctrl+Shift+F"), Ejecutar = () => AlAbrirBusquedaProyecto(this, new RoutedEventArgs()) },
            new() { Id = "abrir-rapido", Titulo = "Ir a archivo del proyecto...", Categoria = "Navegación", Atajo = _vm.Configuracion.AtajosTeclado.GetValueOrDefault("AbrirRapido", "Ctrl+P"), Ejecutar = MostrarAperturaRapidaArchivos },
            new() { Id = "terminal", Titulo = "Abrir Terminal multi-sesión", Categoria = "Paneles", Ejecutar = () => _vm.SeleccionarPestanaInferior(PestanaPanelInferior.Terminal) },
            new() { Id = "nueva-terminal", Titulo = "Crear nueva pestaña de Terminal", Categoria = "Terminal", Ejecutar = () => _vm.CrearNuevaTerminal() },
            new() { Id = "tools", Titulo = "Abrir Centro de Herramientas (Tools)", Categoria = "Tools", Ejecutar = () => _vm.AbrirCentroTools() },
            new() { Id = "web-preview", Titulo = "Abrir / alternar Vista Web integrada", Categoria = "Web", Ejecutar = () => AlAlternarVistaWeb(this, new RoutedEventArgs()) },
            new() { Id = "web-external", Titulo = "Abrir proyecto web en navegador externo (Live Server)", Categoria = "Web", Ejecutar = () => _vm.AbrirWebEnNavegadorExterno() },
            new() { Id = "github-panel", Titulo = "Abrir panel Git y GitHub", Categoria = "GitHub", Ejecutar = () => _vm.SeleccionarSeccionLateral(SeccionBarraLateral.GitHub) },
            new() { Id = "github-login", Titulo = "Iniciar sesión en GitHub...", Categoria = "GitHub", Ejecutar = () => AlIniciarSesionGitHub(this, new RoutedEventArgs()) },
            new() { Id = "github-publicar", Titulo = "Publicar proyecto actual en GitHub...", Categoria = "GitHub", Ejecutar = () => AlPublicarEnGitHub(this, new RoutedEventArgs()) },
            new() { Id = "github-clonar", Titulo = "Clonar repositorio Git...", Categoria = "GitHub", Ejecutar = () => AlClonarUrlManual(this, new RoutedEventArgs()) },
            new() { Id = "tema", Titulo = "Alternar tema visual", Categoria = "Apariencia", Ejecutar = () => _vm.AlternarTemaRapido() },
            new() { Id = "configuracion", Titulo = "Abrir Configuración", Categoria = "IDE", Ejecutar = () => AlAbrirConfiguracion(this, new RoutedEventArgs()) },
            new() { Id = "docs", Titulo = "Abrir Documentación de Cibao", Categoria = "Ayuda", Ejecutar = () => AlAbrirDocumentacion(this, new RoutedEventArgs()) },
            new() { Id = "actualizaciones", Titulo = "Abrir actualizador integrado (GitHub Releases)", Categoria = "Ayuda", Ejecutar = () => AbrirActualizadorIntegrado(_vm.UltimaInfoActualizacion) },
            new() { Id = "inicio", Titulo = "Ir a Pantalla de Inicio", Categoria = "Vista", Ejecutar = () => _vm.MostrarPantallaInicio = true }
        };

        var paleta = new VentanaPaletaComandos(comandos, "Escribe un comando de Xynex Code Editor...");
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
    private void AlComprobarActualizaciones(object? sender, RoutedEventArgs e) => AbrirActualizadorIntegrado(_vm.UltimaInfoActualizacion);

    private void AbrirActualizadorIntegrado(InformacionActualizacion? info)
    {
        if (_ventanaActualizadorActiva is not null)
        {
            _ventanaActualizadorActiva.Activate();
            return;
        }

        var ventana = new VentanaActualizacion(
            _vm,
            info,
            _vm.RutaPaqueteActualizacionListo,
            solicitarCierreApp: () => Dispatcher.UIThread.Post(() => Close()));
        _ventanaActualizadorActiva = ventana;
        ventana.Closed += (_, _) => _ventanaActualizadorActiva = null;
        ventana.ShowDialog(this);
    }

    private void AlAlternarVistaWeb(object? sender, RoutedEventArgs e)
    {
        if (_vm.MostrarVistaPreviaWeb)
        {
            _vm.CerrarVistaPreviaWeb();
        }
        else
        {
            _vm.AbrirVistaPreviaWeb();
        }
    }

    private void AlAbrirWebExterno(object? sender, RoutedEventArgs e) => _vm.AbrirWebEnNavegadorExterno();

    private void ResolverYNavegarEnlaceWeb(string enlaceRelativo)
    {
        string limpio = enlaceRelativo.Split('?', '#')[0].Trim().TrimStart('/');
        if (string.IsNullOrWhiteSpace(limpio)) return;

        string? carpetaActual = _vm.ArchivoActivo is not null
            ? Path.GetDirectoryName(_vm.ArchivoActivo.RutaArchivo)
            : _vm.RutaProyectoActual;

        string? candidato = !string.IsNullOrWhiteSpace(carpetaActual)
            ? Path.GetFullPath(Path.Combine(carpetaActual, limpio))
            : null;

        if ((string.IsNullOrWhiteSpace(candidato) || !File.Exists(candidato)) &&
            !string.IsNullOrWhiteSpace(_vm.RutaProyectoActual))
        {
            candidato = Path.GetFullPath(Path.Combine(_vm.RutaProyectoActual, limpio));
        }

        if (!string.IsNullOrWhiteSpace(candidato) && File.Exists(candidato))
        {
            _vm.AbrirArchivo(candidato);
            _vm.AbrirVistaPreviaWeb(candidato);
        }
        else
        {
            _vm.MostrarToast($"El enlace '{enlaceRelativo}' no existe en el proyecto.");
        }
    }

    private void AlAbrirTerminal(object? sender, RoutedEventArgs e)
    {
        _vm.MostrarPantallaInicio = false;
        _vm.SeleccionarPestanaInferior(PestanaPanelInferior.Terminal);
        Dispatcher.UIThread.Post(() => TxtComandoTerminal.Focus());
    }

    private void AlCerrarSesionTerminalClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is SesionTerminalTab sesion)
        {
            _vm.CerrarTerminal(sesion);
        }
    }

    private void AlEjecutarComandoRecomendado(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string cmd)
        {
            _vm.EjecutarComandoRecomendadoTerminal(cmd);
        }
    }

    private void AlAbrirEjemploHolaMundo(object? sender, RoutedEventArgs e)
    {
        _vm.AbrirTutorCibaoDesdeCero();
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
    private void AlRailGitHub(object? sender, RoutedEventArgs e)
    {
        _vm.MostrarPantallaInicio = false;
        _vm.SeleccionarSeccionLateral(SeccionBarraLateral.GitHub);
        _ = _vm.RefrescarGitAsync();
    }
    private void AlAbrirPanelGitHubInicio(object? sender, RoutedEventArgs e)
    {
        _vm.MostrarPantallaInicio = false;
        _vm.MostrarCentroTools = false;
        _vm.SeccionLateral = SeccionBarraLateral.GitHub;
        _vm.MostrarBarraLateral = true;
        _ = _vm.RefrescarGitAsync();
    }
    private void AlRailTools(object? sender, RoutedEventArgs e) => _vm.SeleccionarSeccionLateral(SeccionBarraLateral.Tools);
    private void AlRailHerramientas(object? sender, RoutedEventArgs e) => _vm.SeleccionarSeccionLateral(SeccionBarraLateral.Herramientas);

    private async void AlIniciarSesionGitHub(object? sender, RoutedEventArgs e)
    {
        var dialogo = new DialogoLoginGitHub(_vm.ServicioGitHub);
        CuentaGitHub? cuenta = await dialogo.ShowDialog<CuentaGitHub?>(this);
        if (cuenta is not null)
        {
            _vm.MostrarToast($"Conectado a GitHub como @{cuenta.Login}.");
            _vm.RegistrarActividad("GitHub", $"Sesión iniciada como @{cuenta.Login}.");
        }
    }

    private async void AlCerrarSesionGitHub(object? sender, RoutedEventArgs e)
    {
        var confirmacion = new DialogoConfirmacion(
            "Cerrar sesión de GitHub",
            "¿Desconectar cuenta de GitHub?",
            "Se eliminará el token cifrado de este equipo.",
            "Cerrar sesión");
        if (await confirmacion.ShowDialog<bool>(this))
        {
            _vm.CerrarSesionGitHub();
        }
    }

    private async void AlPublicarEnGitHub(object? sender, RoutedEventArgs e)
    {
        if (!_vm.TieneProyectoAbierto || string.IsNullOrWhiteSpace(_vm.RutaProyectoActual))
        {
            _vm.MostrarToast("Abre o crea una carpeta de proyecto antes de publicar en GitHub.");
            return;
        }

        if (!_vm.ServicioGitHub.EstaConectado)
        {
            var login = new DialogoLoginGitHub(_vm.ServicioGitHub);
            CuentaGitHub? cuenta = await login.ShowDialog<CuentaGitHub?>(this);
            if (cuenta is null) return;
        }

        _vm.GuardarTodos(silencioso: true);

        var dialogo = new DialogoPublicarGitHub(_vm.ServicioGitHub, _vm.ServicioGit, _vm.RutaProyectoActual);
        RepositorioGitHub? repo = await dialogo.ShowDialog<RepositorioGitHub?>(this);
        if (repo is not null)
        {
            _vm.RefrescarExplorador();
            await _vm.RefrescarGitAsync();
            await _vm.CargarRepositoriosGitHubAsync();
            _vm.RegistrarActividad("GitHub", $"Proyecto publicado en {repo.HtmlUrl}");
            _vm.MostrarToast($"Publicado en GitHub: {repo.NombreCompleto}");
        }
    }

    private void AlActualizarReposGitHub(object? sender, RoutedEventArgs e) => _ = _vm.CargarRepositoriosGitHubAsync();

    private async void AlClonarUrlManual(object? sender, RoutedEventArgs e)
    {
        string url = _vm.UrlClonacionManual?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(url))
        {
            var dlg = new DialogoEntradaTexto("Clonar repositorio", "URL HTTPS del repositorio Git:", "https://github.com/usuario/repositorio.git");
            string? ingresada = await dlg.ShowDialog<string?>(this);
            if (string.IsNullOrWhiteSpace(ingresada)) return;
            url = ingresada.Trim();
            _vm.UrlClonacionManual = url;
        }

        await SeleccionarCarpetaYClonarAsync(url);
    }

    private async void AlClonarRepositorioLista(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is RepositorioGitHub repo)
        {
            await SeleccionarCarpetaYClonarAsync(repo.CloneUrl);
        }
    }

    private async Task SeleccionarCarpetaYClonarAsync(string cloneUrl)
    {
        IReadOnlyList<IStorageFolder> carpetas = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Selecciona la carpeta padre donde se clonará el repositorio",
                AllowMultiple = false
            });

        if (carpetas.Count == 0) return;
        string? destino = carpetas[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(destino)) return;

        await _vm.ClonarYAbrirRepositorioAsync(cloneUrl, destino);
    }

    private void AlRefrescarGit(object? sender, RoutedEventArgs e) => _ = _vm.RefrescarGitAsync();
    private void AlInicializarGit(object? sender, RoutedEventArgs e) => _ = _vm.InicializarGitEnProyectoAsync();
    private void AlPullGit(object? sender, RoutedEventArgs e) => _ = _vm.PullGitAsync();
    private void AlPushGit(object? sender, RoutedEventArgs e) => _ = _vm.PushGitAsync();
    private void AlCrearCommitGit(object? sender, RoutedEventArgs e) => _ = _vm.CrearCommitGitAsync();
    private void AlPrepararTodoGit(object? sender, RoutedEventArgs e) => _ = _vm.PrepararTodoGitAsync();

    private async void AlCrearRamaGit(object? sender, RoutedEventArgs e)
    {
        var dlg = new DialogoEntradaTexto("Nueva rama Git", "Nombre de la nueva rama:", "feature/nueva-mejora");
        string? nombre = await dlg.ShowDialog<string?>(this);
        if (!string.IsNullOrWhiteSpace(nombre))
        {
            await _vm.CrearRamaGitAsync(nombre);
        }
    }

    private void AlPrepararCambioGit(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ArchivoCambioGit cambio)
        {
            _ = _vm.PrepararCambioGitAsync(cambio);
        }
    }

    private void AlQuitarCambioGit(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ArchivoCambioGit cambio)
        {
            _ = _vm.QuitarPreparadoGitAsync(cambio);
        }
    }

    private async void AlVerDiffCambioGit(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ArchivoCambioGit cambio) return;
        string diff = await _vm.ObtenerDiffCambioGitAsync(cambio);
        var visor = new DialogoDiffGit(cambio.RutaRelativa, cambio.EtiquetaEstado, diff);
        await visor.ShowDialog(this);
    }

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
                _vm.LimpiarTerminalActiva();
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
        else if (e.Key == Key.Up)
        {
            string? anterior = _vm.NavegarHistorialTerminal(haciaAtras: true);
            if (anterior is not null)
            {
                e.Handled = true;
                _vm.ComandoTerminal = anterior;
            }
        }
        else if (e.Key == Key.Down)
        {
            string? siguiente = _vm.NavegarHistorialTerminal(haciaAtras: false);
            if (siguiente is not null)
            {
                e.Handled = true;
                _vm.ComandoTerminal = siguiente;
            }
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
