using System.Collections.ObjectModel;
using Alba.IDE.Models;
using Avalonia.Threading;

namespace Alba.IDE.ViewModels;

/// <summary>
/// Administra la consola integrada del IDE: salidas diferenciadas, errores y entrada interactiva para 'leer'.
/// </summary>
public sealed class ConsolaViewModel : ViewModelBase
{
    private string _entradaUsuario = string.Empty;
    private bool _esperandoEntrada;
    private string _indicadorEntrada = "Entrada requerida:";
    private Action<string>? _alEnviarEntrada;

    public ObservableCollection<MensajeConsola> Mensajes { get; } = [];

    public string EntradaUsuario
    {
        get => _entradaUsuario;
        set => SetProperty(ref _entradaUsuario, value);
    }

    public bool EsperandoEntrada
    {
        get => _esperandoEntrada;
        private set => SetProperty(ref _esperandoEntrada, value);
    }

    public string IndicadorEntrada
    {
        get => _indicadorEntrada;
        private set => SetProperty(ref _indicadorEntrada, value);
    }

    public RelayCommand ComandoLimpiar { get; }
    public RelayCommand ComandoEnviarEntrada { get; }

    public ConsolaViewModel()
    {
        ComandoLimpiar = new RelayCommand(_ => Limpiar());
        ComandoEnviarEntrada = new RelayCommand(_ => EnviarEntradaActual(), _ => EsperandoEntrada);
    }

    public void AgregarMensaje(TipoMensajeConsola tipo, string texto)
    {
        Dispatcher.UIThread.Post(() =>
        {
            Mensajes.Add(new MensajeConsola(tipo, texto, DateTime.Now));
        });
    }

    public void SolicitarEntrada(string? indicador, Action<string> callback)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _alEnviarEntrada = callback;
            IndicadorEntrada = string.IsNullOrWhiteSpace(indicador)
                ? "Escribe tu entrada y presiona Enter:"
                : indicador;
            EsperandoEntrada = true;
            ComandoEnviarEntrada.NotificarCambio();
        });
    }

    public void CancelarSolicitudEntrada()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _alEnviarEntrada = null;
            EsperandoEntrada = false;
            EntradaUsuario = string.Empty;
            ComandoEnviarEntrada.NotificarCambio();
        });
    }

    public void EnviarEntradaActual()
    {
        if (!EsperandoEntrada || _alEnviarEntrada is null)
        {
            return;
        }

        string valor = EntradaUsuario;
        Action<string> callback = _alEnviarEntrada;

        EntradaUsuario = string.Empty;
        EsperandoEntrada = false;
        _alEnviarEntrada = null;
        ComandoEnviarEntrada.NotificarCambio();

        Mensajes.Add(new MensajeConsola(TipoMensajeConsola.Entrada, valor, DateTime.Now));
        callback(valor);
    }

    public void Limpiar()
    {
        Mensajes.Clear();
    }
}
