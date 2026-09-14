using System.Windows;
using WPF_Fusion.Interface;

namespace WPF_Fusion.MVVM.View
{
	public partial class UsernameView : Window
	{
		public UsernameView()
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
