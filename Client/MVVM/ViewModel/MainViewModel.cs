using System;
using WPF_Fusion.Infrastructure;

namespace WPF_Fusion.MVVM.ViewModel
{
	public class MainViewModel : ObservableObject
	{
		//COMMANDS
		public RelayCommand HomeViewCommand { get; }
		public RelayCommand ChatViewCommand { get; }
		public RelayCommand LogoutCommand { get; }

		//VIEWS
		public HomeViewModel HomeVM { get; }
		public ChatViewModel ChatVM { get; }

		/// <summary>
		/// Raised when the user signs out or the session expires, so the shell can
		/// return to the login window.
		/// </summary>
		public event EventHandler? SignOutRequested;

		//USERNAME
		public string CurrentUsername => Session.Username ?? "Unknown";
		public string GreetingUsername => $"Welcome, {CurrentUsername}";

		public void RefreshUser()
		{
			OnPropertyChanged(nameof(CurrentUsername));
			OnPropertyChanged(nameof(GreetingUsername));
		}

		//CURRENT VIEW
		private object _currentView;
		public object CurrentView
		{
			get => _currentView;
			set { _currentView = value; OnPropertyChanged(); }
		}

		public MainViewModel()
		{
			HomeVM = new HomeViewModel();
			ChatVM = new ChatViewModel(App.UserApiService, App.ChatService);

			CurrentView = HomeVM;

			HomeViewCommand = new RelayCommand(() => CurrentView = HomeVM);
			ChatViewCommand = new RelayCommand(() => CurrentView = ChatVM);
			LogoutCommand = new RelayCommand(SignOut);

			// A rejected token must not leave the user staring at empty lists.
			Session.Expired += OnSessionExpired;
		}

		private void OnSessionExpired(object? sender, EventArgs e)
		{
			System.Windows.Application.Current?.Dispatcher.Invoke(() =>
			{
				System.Windows.MessageBox.Show(
					"Your session has expired. Please sign in again.",
					"Signed out",
					System.Windows.MessageBoxButton.OK,
					System.Windows.MessageBoxImage.Information);

				SignOut();
			});
		}

		/// <summary>
		/// Drops the session, stops polling and asks the shell to show the login window.
		/// </summary>
		private void SignOut()
		{
			Session.Expired -= OnSessionExpired;

			ChatVM.Dispose();
			Session.Clear();

			if (App.Http != null)
				App.Http.DefaultRequestHeaders.Authorization = null;

			SignOutRequested?.Invoke(this, EventArgs.Empty);
		}
	}
}
