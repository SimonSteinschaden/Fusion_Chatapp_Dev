using System;
using System.Windows;
using WPF_Fusion.MVVM.View;
using WPF_Fusion.MVVM.ViewModel;
using WPF_Fusion.Interface;

namespace WPF_Fusion.Infrastructure
{
    /// <summary>
    /// Default dialog service implementation that maps ViewModels to their corresponding Views
    /// and displays them as modal WPF dialogs.
    /// </summary>
    public sealed class DialogService : IDialogService
    {
        public bool? ShowDialog(object viewModel)
        {
            Window dialog = viewModel switch
            {
                UsernameViewModel => new UsernameView(),
                CreateGroupViewModel => new CreateGroupView(),
                _ => throw new NotSupportedException($"No dialog mapped for {viewModel.GetType().Name}")
            };

            // Owner is null during startup, before MainWindow exists - that is fine,
            // the dialog then simply centres on screen.
            dialog.Owner = Application.Current?.MainWindow;
            dialog.DataContext = viewModel;

            return dialog.ShowDialog();
        }
    }
}