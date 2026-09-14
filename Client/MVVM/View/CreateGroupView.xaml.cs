using System.Windows;
using WPF_Fusion.Interface;

namespace WPF_Fusion.MVVM.View
{
	/// <summary>
	/// Dialog for creating a group conversation. Closing is driven by the ViewModel
	/// through <see cref="IDialogRequestClose"/>.
	/// </summary>
	public partial class CreateGroupView : Window
	{
		public CreateGroupView()
		{
			InitializeComponent();

			Loaded += (_, __) =>
			{
				if (DataContext is IDialogRequestClose vm)
				{
					vm.CloseRequested += (_, e) =>
					{
						DialogResult = e.DialogResult;
						Close();
					};
				}
			};
		}
	}
}
