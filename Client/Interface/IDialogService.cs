namespace WPF_Fusion.Interface
{
    /// <summary>
    /// Provides an abstraction for displaying modal dialogs.
    /// Allows ViewModels to open dialogs without directly referencing UI components.
    /// </summary>
    public interface IDialogService
    {
        bool? ShowDialog(object viewModel);
    }
}