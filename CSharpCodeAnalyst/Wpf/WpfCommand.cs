using System.Windows.Input;

namespace CSharpCodeAnalyst.Wpf;

public class WpfCommand : ICommand
{
    private readonly Func<bool>? _canExecute;
    private readonly Action _execute;

    public WpfCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter)
    {
        return _canExecute == null || _canExecute();
    }

    public void Execute(object? parameter)
    {
        _execute();
    }

    public static void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}

public class WpfCommand<T> : ICommand
{
    private readonly Func<T, bool>? _canExecute;
    private readonly Action<T> _execute;

    public WpfCommand(Action<T> execute, Func<T, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter)
    {
        if (_canExecute == null)
        {
            return true;
        }

        if (parameter is not T)
        {
            return false;
        }

        return _canExecute((T)parameter);
    }

    public void Execute(object? parameter)
    {
        if (parameter is not T)
        {
            return;
        }

        _execute((T)parameter);
    }

    public void RaiseCanExecuteChanged()
    {
        CommandManager.InvalidateRequerySuggested();
    }
}