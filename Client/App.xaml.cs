using System.Windows;
using WPF_Fusion.Interface;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System;
using WPF_Fusion.Service;
using WPF_Fusion.MVVM.ViewModel;
using WPF_Fusion.Infrastructure;
namespace WPF_Fusion
{
	/// <summary>
	/// App entry point.
	/// Shows LoginWindow first and opens MainWindow after successful login.
	/// </summary>
	public partial class App : Application
	{
		/// <summary>
		/// Used when App.config carries no ApiBaseUrl entry.
		/// </summary>
		private const string DefaultApiBaseUrl = "http://127.0.0.1:5214/";

		public static HttpClient Http { get; private set; } = null!;
		public static IAuthService AuthService { get; private set; } = null!;
		public static IUserService UserApiService { get; private set; } = null!;
		public static IChatService ChatService { get; private set; } = null!;

		protected override void OnStartup(StartupEventArgs e)
		{
			// Force TLS 1.2 for .NET Framework clients
			ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

			base.OnStartup(e);
			Http = new HttpClient
			{
				BaseAddress = ResolveApiBaseUrl()
			};
			AuthService = new AuthApiService(Http);//calls Rest API

			UserApiService = new UserApiService(Http);
			ChatService = new ChatApiService(Http);

			ShowLogin();
		}

		/// <summary>
		/// Shows the login window. On success the main window opens, otherwise the
		/// application exits.
		/// </summary>
		private void ShowLogin()
		{
			var login = new LoginWindow();

			if (login.ShowDialog() == true)
				ShowMain();
			else
				ExitApplication();
		}

		/// <summary>
		/// Shows the main window and wires sign-out so the user returns to the login
		/// window instead of the application closing.
		/// </summary>
		private void ShowMain()
		{
			var main = new MainWindow();
			MainWindow = main;

			var signingOut = false;

			if (main.DataContext is MainViewModel vm)
			{
				vm.RefreshUser();
				vm.SignOutRequested += (_, __) =>
				{
					signingOut = true;
					main.Close();
				};
			}

			main.Closed += (_, __) =>
			{
				if (signingOut)
					ShowLogin();
				else
					ExitApplication();
			};

			main.Show();
		}

		/// <summary>
		/// Shuts the application down once. ShutdownMode is OnExplicitShutdown so that
		/// closing the main window during sign-out does not terminate the process.
		/// </summary>
		private void ExitApplication()
		{
			if (_shuttingDown)
				return;

			_shuttingDown = true;
			Shutdown();
		}

		private bool _shuttingDown;

		/// <summary>
		/// Reads the API base address from App.config (key "ApiBaseUrl").
		/// Falls back to the local development server if the entry is missing or malformed.
		/// </summary>
		private static Uri ResolveApiBaseUrl()
		{
			var configured = ConfigurationManager.AppSettings["ApiBaseUrl"];

			if (string.IsNullOrWhiteSpace(configured))
				configured = DefaultApiBaseUrl;

			// HttpClient only combines relative paths correctly when the base address
			// ends with a slash.
			if (!configured.EndsWith("/"))
				configured += "/";

			if (!Uri.TryCreate(configured, UriKind.Absolute, out var uri))
			{
				MessageBox.Show(
					$"Invalid ApiBaseUrl in App.config: '{configured}'.\n" +
					$"Falling back to {DefaultApiBaseUrl}.",
					"Configuration", MessageBoxButton.OK, MessageBoxImage.Warning);

				uri = new Uri(DefaultApiBaseUrl);
			}

			return uri;
		}
	}
}
