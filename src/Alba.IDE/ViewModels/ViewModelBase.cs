using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Alba.IDE.ViewModels;

public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T campo, T valor, [CallerMemberName] string? propiedad = null)
    {
        if (EqualityComparer<T>.Default.Equals(campo, valor))
        {
            return false;
        }
        campo = valor;
        OnPropertyChanged(propiedad);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propiedad = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propiedad));
}

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _ejecutar;
    private readonly Func<object?, bool>? _puedeEjecutar;

    public RelayCommand(Action<object?> ejecutar, Func<object?, bool>? puedeEjecutar = null)
    {
        _ejecutar = ejecutar ?? throw new ArgumentNullException(nameof(ejecutar));
        _puedeEjecutar = puedeEjecutar;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _puedeEjecutar?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _ejecutar(parameter);

    public void NotificarCambio() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
