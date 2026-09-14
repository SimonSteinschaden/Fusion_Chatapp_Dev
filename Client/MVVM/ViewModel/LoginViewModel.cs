using System;
using System.ComponentModel;
using System.Net.Mail;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using WPF_Fusion.Infrastructure;
using WPF_Fusion.Interface;

namespace WPF_Fusion.MVVM.ViewModel
{
	/// <summary>
	/// Handles user authentication logic (login & registration).
	/// </summary>
	public sealed class LoginViewModel : INotifyPropertyChanged
	{
		/// <summary>
		/// Mirrors AuthController.MinPasswordLength on the server.
		/// </summary>
		private const int MinPasswordLength = 8;

		/* Private state */
		private string _email = string.Empty;
		private string _password = string.Empty;
		private string _errorMessage = string.Empty;
		private bool _showPassword;

		/* Interfaces */
		private readonly IAuthService _authService;
		private readonly IDialogService _dialogService;

		// 1-Arg ctor bleibt möglich (z.B. bestehender Code), erstellt DialogService automatisch.
		public LoginViewModel(IAuthService authService)
			: this(authService, new DialogService())
		{
		}

		/* Commands */
		public ICommand LoginCommand { get; }
		public ICommand RegisterCommand { get; }
		public ICommand ForgotPasswordCommand { get; }

		/// <summary>
		/// Raised when login was successful.
		/// </summary>
		public event EventHandler? LoginSucceeded;

		public string Email
		{
			get => _email;
			set
			{
				_email = value;
				OnPropertyChanged();
				Validate();
			}
		}

		public string Password
		{
			get => _password;
			set
			{
				_password = value;
				OnPropertyChanged();
				Validate();
			}
		}

		public bool ShowPassword
		{
			get => _showPassword;
			set
			{
				_showPassword = value;
				OnPropertyChanged();
			}
		}

		private bool _isBusy;
		/// <summary>
		/// True while an auth request is in flight; disables the buttons and drives the
		/// progress text.
		/// </summary>
		public bool IsBusy
		{
			get => _isBusy;
			private set
			{
				if (_isBusy == value) return;
				_isBusy = value;
				OnPropertyChanged();
				RaiseCanExecuteChanged();
			}
		}

		public string ErrorMessage
		{
			get => _errorMessage;
			private set
			{
				_errorMessage = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(HasError));
			}
		}

		/// <summary>
		/// Indicates whether an error message is currently shown.
		/// </summary>
		public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

		// 2-Arg ctor: HIER müssen beide Felder gesetzt werden.
		public LoginViewModel(IAuthService authService, IDialogService dialogService)
		{
			_authService = authService ?? throw new ArgumentNullException(nameof(authService));
			_dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

			// Initialization
			LoginCommand = new RelayCommand<object>(_ => Login(), _ => CanSubmit());
			RegisterCommand = new RelayCommand(Register, CanSubmit);
			ForgotPasswordCommand = new RelayCommand(ForgotPassword);

			Validate();
		}

		/// <summary>
		/// Determines whether login or registration may be started.
		/// </summary>
		private bool CanSubmit()
		{
			return
				!IsBusy &&
				!string.IsNullOrWhiteSpace(Email) &&
				EmailIsValid(Email) &&
				!string.IsNullOrWhiteSpace(Password);
		}

		/// <summary>
		/// Validates input and updates error state.
		/// </summary>
		private void Validate()
		{
			if (string.IsNullOrWhiteSpace(Email))
			{
				ErrorMessage = "";
			}
			else if (!EmailIsValid(Email))
			{
				ErrorMessage = "Invalid email address.";
			}
			else
			{
				ErrorMessage = "";
			}

			RaiseCanExecuteChanged();
		}

		private void RaiseCanExecuteChanged()
		{
			(LoginCommand as RelayCommand<object>)?.RaiseCanExecuteChanged();
			(RegisterCommand as RelayCommand)?.RaiseCanExecuteChanged();
		}

		/// <summary>
		/// Checks whether an email address is syntactically valid.
		/// </summary>
		private static bool EmailIsValid(string email)
		{
			try
			{
				_ = new MailAddress(email);
				return true;
			}
			catch
			{
				return false;
			}
		}

		/// <summary>
		/// Executes the login process. The password comes from the bound
		/// <see cref="Password"/> property (kept in sync by PasswordBoxHelper), so there
		/// is a single source for it rather than a second path via CommandParameter.
		/// </summary>
		private async void Login()
		{
			await RunAsync(async () =>
			{
				var result = await _authService.LoginAsync(Email.Trim(), Password);

				if (!result.Success)
				{
					ErrorMessage = result.ErrorMessage ?? "Login failed.";
					return;
				}

				ErrorMessage = "";
				LoginSucceeded?.Invoke(this, EventArgs.Empty);
			});
		}

		/// <summary>
		/// Registers a new user.
		/// </summary>
		private async void Register()
		{
			ErrorMessage = string.Empty;

			var email = Email?.Trim() ?? string.Empty;
			var password = Password ?? string.Empty;

			// Email validieren
			if (string.IsNullOrWhiteSpace(email))
			{
				ErrorMessage = "Please enter an email address.";
				return;
			}

			if (!EmailIsValid(email))
			{
				ErrorMessage = "Invalid email address.";
				return;
			}

			// Password validieren
			if (string.IsNullOrWhiteSpace(password))
			{
				ErrorMessage = "Please enter a password.";
				return;
			}

			// Must match the server-side rule in AuthController.MinPasswordLength.
			if (password.Length < MinPasswordLength)
			{
				ErrorMessage = $"Password must be at least {MinPasswordLength} characters.";
				return;
			}

			// Username Dialog öffnen
			var usernameVM = new UsernameViewModel();
			var dialogResult = _dialogService.ShowDialog(usernameVM);

			if (dialogResult != true)
				return; // User hat abgebrochen

			var username = usernameVM.Username?.Trim() ?? string.Empty;

			if (string.IsNullOrWhiteSpace(username))
			{
				ErrorMessage = "Username is required.";
				return;
			}

			await RunAsync(async () =>
			{
				var result = await _authService.RegisterAsync(email, password, username);

				ErrorMessage = result.Success
					? "Registration successful. You can now sign in."
					: result.ErrorMessage ?? "Registration failed.";
			});
		}

		/// <summary>
		/// Password recovery is not implemented; say so plainly instead of pretending.
		/// </summary>
		private void ForgotPassword()
		{
			ErrorMessage = "Password recovery is not available yet. Please contact an administrator.";
		}

		/// <summary>
		/// Runs an auth call with a busy flag so the window does not look frozen, and
		/// turns any failure into a message instead of an unhandled exception - these
		/// are async void handlers, where an escaping exception terminates the process.
		/// </summary>
		private async Task RunAsync(Func<Task> action)
		{
			IsBusy = true;

			try
			{
				await action();
			}
			catch (Exception ex)
			{
				ErrorMessage = ApiException.Describe(ex);
			}
			finally
			{
				IsBusy = false;
			}
		}

		/* INotifyPropertyChanged */
		public event PropertyChangedEventHandler? PropertyChanged;

		private void OnPropertyChanged([CallerMemberName] string? name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
