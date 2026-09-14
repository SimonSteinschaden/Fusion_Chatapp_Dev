using System;
using System.Windows.Input;
using WPF_Fusion.MVVM.Model;
using WPF_Fusion.MVVM.ViewModel;

namespace WPF_Fusion.Infrastructure
{
    /// <summary>
    /// Generic ICommand implementation that forwards Execute and CanExecute to delegates.
    /// Used for parameterized commands in MVVM.
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private Action<T> _execute;
        private Func<T, bool> _canExecute;

        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
            => _canExecute?.Invoke((T)parameter) ?? true;

        public void Execute(object parameter)
            => _execute((T)parameter);

        public event EventHandler CanExecuteChanged;

        public void RaiseCanExecuteChanged()
            => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// ICommand implementation that forwards Execute and CanExecute to delegates.
    /// Uses CommandManager to integrate with WPF's built-in requery mechanism.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private Action _execute;
        private Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke() ?? true;
        public void Execute(object parameter) => _execute();

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// Forces WPF to re-evaluate CanExecute (manual trigger).
        /// </summary>
        public void RaiseCanExecuteChanged()
            => CommandManager.InvalidateRequerySuggested();
    }
}