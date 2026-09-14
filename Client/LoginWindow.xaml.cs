using System;
using System.IO;
using System.Windows;
using WPF_Fusion.Infrastructure;
using WPF_Fusion.MVVM.ViewModel;

namespace WPF_Fusion
{
	/// <summary>
	/// Login window displayed at application startup.
	/// Handles user authentication UI and transitions to the main window
	/// after a successful login.
	/// </summary>
	public partial class LoginWindow : Window
	{
		private LoginViewModel? _vm;

		/// <summary>
		/// Initializes the login window, creates the corresponding ViewModel,
		/// and subscribes to login success events.
		/// </summary>
		public LoginWindow()
		{
			InitializeComponent();

			var dialogService = new DialogService();

			// Create and assign the ViewModel
			_vm = new LoginViewModel(App.AuthService, dialogService);
			_vm.LoginSucceeded += OnLoginSucceeded;
			DataContext = _vm;

			// Start and stop background video with window lifecycle
			Loaded += (_, __) => StartBackgroundVideo();
			Closed += (_, __) => StopBackgroundVideo();
		}

		/// <summary>
		/// Handles successful login events raised by the LoginViewModel.
		/// Opens the main window and closes the login window.
		/// This handler is executed only once.
		/// </summary>
		private void OnLoginSucceeded(object? sender, EventArgs e)
		{
			// Unsubscribe to prevent double execution
			if (_vm != null)
				_vm.LoginSucceeded -= OnLoginSucceeded;

			DialogResult = true;
		}

		/// <summary>
		/// Starts playing the background video on a loop, if the video file exists.
		/// </summary>
		private void StartBackgroundVideo()
		{
			var path = Path.Combine(
				AppDomain.CurrentDomain.BaseDirectory,
				"Assets",
				"login.mp4");

			// Fallback: do nothing if the video file is missing
			if (!File.Exists(path))
				return;

			BgVideo.Source = new Uri(path, UriKind.Absolute);

			// Loop the video indefinitely
			BgVideo.MediaEnded += (_, __) =>
			{
				BgVideo.Position = TimeSpan.Zero;
				BgVideo.Play();
			};

			BgVideo.Play();
		}

		/// <summary>
		/// Stops the background video playback.
		/// Errors are ignored to avoid shutdown issues.
		/// </summary>
		private void StopBackgroundVideo()
		{
			try
			{
				BgVideo.Stop();
			}
			catch
			{
				// does nothing
			}
		}
	}
}
