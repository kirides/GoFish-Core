using System;
using System.Windows.Input;

namespace GoFishCore.WpfUI.ViewModels;

public class DelegateCommand : DelegateCommand<object>
{
    public DelegateCommand(Action action, Func<bool> canExecute = null)
        : base(_ => action(), canExecute != null ? _ => canExecute() : null)
    {
    }
}

public class DelegateCommand<T> : ICommand
{
    private readonly Action<T> _action;
    private readonly Func<T, bool> _canExecute;

    public DelegateCommand(Action<T> action, Func<T, bool> canExecute = null)
    {
        _action = action;
        _canExecute = canExecute ?? NopCanExecute;

        static bool NopCanExecute(T v) => true;
    }
    public bool CanExecute(object? parameter) => _canExecute((T)parameter);
    public void Execute(object? parameter)
    {
        var val = (T)parameter;
        if (CanExecute(val))
        {
            _action(val);
        }
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}