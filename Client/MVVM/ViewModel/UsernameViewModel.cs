using System;
using System.Text.RegularExpressions;
using System.Windows.Input;
using WPF_Fusion.Infrastructure;
using WPF_Fusion.Interface;

namespace WPF_Fusion.MVVM.ViewModel
{
	public sealed class UsernameViewModel : ObservableObject, IDialogRequestClose
	{
		private string _username = "";

		public string Username
		{
			get => _username;
			set
			{
				if (_username == value) return;
				_username = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(ErrorMessage));
				OnPropertyChanged(nameof(CanSubmit));
			}
		}

		public string? ErrorMessage
		{
			get
			{
				if (string.IsNullOrWhiteSpace(Username))
					return "Username is required.";

				if (Username.Length < 3)
					return "Minimum 3 characters.";

				if (Username.Length > 24)
					return "Maximum 24 characters.";

				if (!Regex.IsMatch(Username, @"^[a-zA-Z0-9_]+$"))
					return "Only letters, numbers and underscore allowed.";

				return null;
			}
		}

		public bool CanSubmit => ErrorMessage == null;

		public ICommand OkCommand { get; }
		public ICommand CancelCommand { get; }

		public event EventHandler<DialogCloseRequestedEventArgs>? CloseRequested;

		public UsernameViewModel()
		{
			OkCommand = new RelayCommand(
				() => CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs(true)),
				() => CanSubmit
			);

			CancelCommand = new RelayCommand(
				() => CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs(false))
			);
		}
	}
}
