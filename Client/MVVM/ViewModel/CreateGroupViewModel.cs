using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using WPF_Fusion.Infrastructure;
using WPF_Fusion.Interface;

namespace WPF_Fusion.MVVM.ViewModel
{
	/// <summary>
	/// Collects the title and member list for a new group conversation.
	/// Members are entered as comma-separated usernames; the current user is added
	/// by the server and must not be listed.
	/// </summary>
	public sealed class CreateGroupViewModel : ObservableObject, IDialogRequestClose
	{
		/// <summary>
		/// A group needs three members in total, and the creator is one of them.
		/// </summary>
		private const int MinOtherMembers = 2;

		private string _title = "";
		public string Title
		{
			get => _title;
			set
			{
				if (_title == value) return;
				_title = value;
				RaiseValidation();
			}
		}

		private string _members = "";
		/// <summary>
		/// Raw input, e.g. "anna, bernd, chris".
		/// </summary>
		public string Members
		{
			get => _members;
			set
			{
				if (_members == value) return;
				_members = value;
				RaiseValidation();
			}
		}

		/// <summary>
		/// Parsed, trimmed, de-duplicated usernames from <see cref="Members"/>.
		/// </summary>
		public IReadOnlyList<string> ParticipantUsernames =>
			(_members ?? "")
				.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
				.Select(s => s.Trim())
				.Where(s => s.Length > 0)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

		public string? ErrorMessage
		{
			get
			{
				if (string.IsNullOrWhiteSpace(Title))
					return "Group name is required.";

				if (Title.Trim().Length > 60)
					return "Group name must be 60 characters or fewer.";

				if (ParticipantUsernames.Count < MinOtherMembers)
					return $"Enter at least {MinOtherMembers} other members, separated by commas.";

				return null;
			}
		}

		public bool CanSubmit => ErrorMessage == null;

		public ICommand OkCommand { get; }
		public ICommand CancelCommand { get; }

		public event EventHandler<DialogCloseRequestedEventArgs>? CloseRequested;

		public CreateGroupViewModel()
		{
			OkCommand = new RelayCommand(
				() => CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs(true)),
				() => CanSubmit
			);

			CancelCommand = new RelayCommand(
				() => CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs(false))
			);
		}

		private void RaiseValidation()
		{
			OnPropertyChanged(nameof(Title));
			OnPropertyChanged(nameof(Members));
			OnPropertyChanged(nameof(ParticipantUsernames));
			OnPropertyChanged(nameof(ErrorMessage));
			OnPropertyChanged(nameof(CanSubmit));
		}
	}
}
