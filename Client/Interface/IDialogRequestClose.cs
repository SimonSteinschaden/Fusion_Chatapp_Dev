using System;

namespace WPF_Fusion.Interface
{
    /// <summary>
    /// Defines a contract for ViewModels that request a dialog to be closed.
    /// Used in MVVM to signal dialog closure without referencing UI elements directly.
    /// </summary>
    public interface IDialogRequestClose
    {
        event EventHandler<DialogCloseRequestedEventArgs> CloseRequested;
    }

    /// <summary>
    /// Provides data for a dialog close request, including an optional dialog result.
    /// </summary>
    public sealed class DialogCloseRequestedEventArgs : EventArgs
    {
        public bool? DialogResult { get; }

        public DialogCloseRequestedEventArgs(bool? dialogResult)
            => DialogResult = dialogResult;
    }
}