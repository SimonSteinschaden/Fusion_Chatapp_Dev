using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WPF_Fusion.Infrastructure;
using WPF_Fusion.Interface;
using WPF_Fusion.MVVM.Model;

namespace WPF_Fusion.MVVM.ViewModel
{
	public class ChatViewModel : ObservableObject, IDisposable
	{
		public ObservableCollection<ContactModel> Contacts { get; }

		// Commands
		public ICommand AddCommand { get; }
		public ICommand SendCommand { get; }
		public ICommand CreateGroupCommand { get; }

		// Services
		private readonly IUserService? _userApi;
		private readonly IChatService? _chatService;
		private readonly IDialogService _dialogService;

		private CancellationTokenSource? _listenCts;
		private readonly SemaphoreSlim _pollGate = new(1, 1);

		// Polling state (per selected conversation)
		private DateTime _lastSeenUtc = DateTime.MinValue;
		private Guid _listeningConversationId = Guid.Empty;

		// Polling interval (tune as you like)
		private readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(1200);

		/// <summary>
		/// Raised when new messages arrive so the view can scroll to the end.
		/// </summary>
		public event EventHandler? MessagesAppended;

		private ContactModel? _selectedContact;
		public ContactModel? SelectedContact
		{
			get => _selectedContact;
			set
			{
				if (_selectedContact == value) return;
				_selectedContact = value;
				OnPropertyChanged();

				// Load + start listening (fire & forget)
				_ = SwitchConversationAsync();
			}
		}

		private string? _message;
		public string? Message
		{
			get => _message;
			set
			{
				if (_message == value) return;
				_message = value;
				OnPropertyChanged();
			}
		}

		private string? _username;
		public string? Username
		{
			get => _username;
			set
			{
				if (_username == value) return;
				_username = value;
				OnPropertyChanged();
			}
		}

		private bool _isBusy;
		/// <summary>
		/// True while a request is in flight, so the view can show progress.
		/// </summary>
		public bool IsBusy
		{
			get => _isBusy;
			private set
			{
				if (_isBusy == value) return;
				_isBusy = value;
				OnPropertyChanged();
			}
		}

		private string? _statusMessage;
		/// <summary>
		/// Last error shown to the user. Empty when everything is fine.
		/// </summary>
		public string? StatusMessage
		{
			get => _statusMessage;
			private set
			{
				if (_statusMessage == value) return;
				_statusMessage = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(HasStatus));
			}
		}

		public bool HasStatus => !string.IsNullOrWhiteSpace(StatusMessage);

		public ChatViewModel() : this(null, null, new DialogService())
		{
		}

		public ChatViewModel(IUserService userApi, IChatService chatService)
			: this(userApi, chatService, new DialogService())
		{
		}

		private ChatViewModel(IUserService? userApi, IChatService? chatService, IDialogService dialogService)
		{
			_userApi = userApi;
			_chatService = chatService;
			_dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

			Contacts = new ObservableCollection<ContactModel>();

			AddCommand = new AsyncRelayCommand(AddContactAsync);
			SendCommand = new AsyncRelayCommand(SendMessageAsync);
			CreateGroupCommand = new AsyncRelayCommand(CreateGroupAsync);
		}

		public void Dispose()
		{
			StopListening();
			_pollGate.Dispose();
		}

		// ----------------------------------
		// Conversation switching
		// ----------------------------------
		private async Task SwitchConversationAsync()
		{
			StopListening();

			if (SelectedContact == null) return;
			if (_chatService == null) return;
			if (SelectedContact.ConversationId == Guid.Empty) return;

			await RunAsync(LoadMessagesForSelectedAsync);

			// Track the newest message we have, so polling only asks for what follows.
			_lastSeenUtc = SelectedContact.Messages.Count > 0
				? SelectedContact.Messages.Max(m => m.SentAtUtc)
				: DateTime.MinValue;

			_listeningConversationId = SelectedContact.ConversationId;

			StartListening();
		}

		// ----------------------------------
		// ADD CONTACT / LOAD CONTACTS
		// ----------------------------------
		private async Task AddContactAsync()
		{
			var input = (Username ?? "").Trim();
			if (string.IsNullOrWhiteSpace(input))
				return;

			var existing = Contacts.FirstOrDefault(c =>
				string.Equals(c.Username, input, StringComparison.OrdinalIgnoreCase));

			if (existing != null)
			{
				SelectedContact = existing;
				Username = "";
				return;
			}

			if (_userApi == null || _chatService == null)
			{
				StatusMessage = "Services are not initialised (UserApi/ChatService).";
				return;
			}

			await RunAsync(async () =>
			{
				var otherUserId = await _userApi.GetUserIdByUsernameAsync(input);

				if (otherUserId == null)
				{
					StatusMessage = $"No user named '{input}'.";
					return;
				}

				if (Session.UserId.HasValue && otherUserId.Value == Session.UserId.Value)
				{
					StatusMessage = "You cannot start a conversation with yourself.";
					return;
				}

				var conversation = await _chatService.GetOrCreateDirectAsync(otherUserId.Value);
				if (conversation == null)
				{
					StatusMessage = "The conversation could not be created.";
					return;
				}

				var newContact = new ContactModel
				{
					Username = input,
					ImageSource = "",
					ConversationId = conversation.ConversationId
				};

				Contacts.Insert(0, newContact);
				SelectedContact = newContact;
				Username = "";
			});
		}

		public async Task LoadConversationsAsync()
		{
			if (_chatService == null)
				return;

			await RunAsync(async () =>
			{
				var convos = await _chatService.GetMyConversationsAsync();

				var previousId = SelectedContact?.ConversationId;
				Contacts.Clear();

				var me = Session.UserId ?? Guid.Empty;

				foreach (var c in convos)
				{
					Contacts.Add(new ContactModel
					{
						Username = DisplayNameFor(c, me),
						ConversationId = c.ConversationId,
						ImageSource = null,
						// Preview so the list is useful before a conversation is opened.
						LastMessagePreview = c.LastMessage?.Content,
						LastMessageAt = c.LastMessage?.SentAtUtc.ToLocalTime()
					});
				}

				// Keep the user on the conversation they were reading.
				SelectedContact =
					Contacts.FirstOrDefault(x => x.ConversationId == previousId)
					?? Contacts.FirstOrDefault();
			});
		}

		/// <summary>
		/// Groups show their title, direct chats show the other participant.
		/// </summary>
		private static string DisplayNameFor(ConversationDto c, Guid me)
		{
			if (c.IsGroup)
				return string.IsNullOrWhiteSpace(c.Title) ? "Group" : c.Title!;

			var other = c.Participants?.FirstOrDefault(p => p.UserId != me);
			return other?.Username ?? "Unknown";
		}

		// ----------------------------------
		// GROUP CHAT
		// ----------------------------------
		private async Task CreateGroupAsync()
		{
			if (_userApi == null || _chatService == null)
			{
				StatusMessage = "Services are not initialised (UserApi/ChatService).";
				return;
			}

			var vm = new CreateGroupViewModel();
			if (_dialogService.ShowDialog(vm) != true)
				return;

			await RunAsync(async () =>
			{
				var ids = new System.Collections.Generic.List<Guid>();

				foreach (var name in vm.ParticipantUsernames)
				{
					var id = await _userApi.GetUserIdByUsernameAsync(name);
					if (id == null)
					{
						StatusMessage = $"No user named '{name}'.";
						return;
					}

					ids.Add(id.Value);
				}

				var convo = await _chatService.CreateGroupAsync(vm.Title.Trim(), ids);
				if (convo == null)
				{
					StatusMessage = "The group could not be created.";
					return;
				}

				var contact = new ContactModel
				{
					Username = string.IsNullOrWhiteSpace(convo.Title) ? "Group" : convo.Title!,
					ConversationId = convo.ConversationId,
					ImageSource = null
				};

				Contacts.Insert(0, contact);
				SelectedContact = contact;
			});
		}

		// ----------------------------------
		// SEND MESSAGE
		// ----------------------------------
		private async Task SendMessageAsync()
		{
			if (SelectedContact == null) return;

			if (SelectedContact.ConversationId == Guid.Empty)
			{
				StatusMessage = "This contact has no conversation yet. Add the contact first.";
				return;
			}

			var text = (Message ?? "").Trim();
			if (string.IsNullOrWhiteSpace(text)) return;

			if (_chatService == null)
			{
				StatusMessage = "ChatService is not initialised.";
				return;
			}

			if (!Session.UserId.HasValue)
			{
				StatusMessage = "No user id in the current session.";
				return;
			}

			var me = Session.UserId.Value;
			var conversationId = SelectedContact.ConversationId;

			await RunAsync(async () =>
			{
				var dto = await _chatService.SendMessageAsync(conversationId, text);
				if (dto == null)
				{
					StatusMessage = "The message could not be sent.";
					return;
				}

				Append(SelectedContact, dto, me);
				Message = "";
			});
		}

		// ----------------------------------
		// LOAD MESSAGES (initial)
		// ----------------------------------
		private async Task LoadMessagesForSelectedAsync()
		{
			if (_chatService == null || SelectedContact == null) return;
			if (SelectedContact.ConversationId == Guid.Empty) return;
			if (!Session.UserId.HasValue) return;

			var me = Session.UserId.Value;
			var contact = SelectedContact;

			var msgs = await _chatService.GetMessagesAsync(contact.ConversationId, take: 50, skip: 0);

			contact.Messages.Clear();

			foreach (var m in msgs.OrderBy(x => x.SentAtUtc))
				Append(contact, m, me);
		}

		/// <summary>
		/// Appends a message to a contact's list, deduplicating by id and deriving the
		/// "first message of a block" flag from the previous entry.
		/// </summary>
		private void Append(ContactModel contact, MessageDto m, Guid me)
		{
			if (contact.Messages.Any(x => x.MessageId == m.MessageId))
				return;

			var prev = contact.Messages.LastOrDefault();

			contact.Messages.Add(new MessageModel
			{
				MessageId = m.MessageId,
				SenderUserId = m.SenderUserId,

				Username = m.SenderUsername ?? "",
				Message = m.Content,

				// Keep UTC for ordering and comparisons, local time for display.
				SentAtUtc = m.SentAtUtc,
				Time = m.SentAtUtc.ToLocalTime(),

				IsNativeOrigin = m.SenderUserId == me,
				FirstMessage = prev == null || prev.SenderUserId != m.SenderUserId
			});

			contact.LastMessagePreview = m.Content;
			contact.LastMessageAt = m.SentAtUtc.ToLocalTime();

			if (m.SentAtUtc > _lastSeenUtc)
				_lastSeenUtc = m.SentAtUtc;

			MessagesAppended?.Invoke(this, EventArgs.Empty);
		}

		// ----------------------------------
		// LIVE LISTENING (Polling)
		// ----------------------------------
		private void StartListening()
		{
			StopListening();
			_listenCts = new CancellationTokenSource();
			_ = ListenLoopAsync(_listenCts.Token);
		}

		private void StopListening()
		{
			try { _listenCts?.Cancel(); }
			catch { /* ignore */ }
			finally
			{
				_listenCts?.Dispose();
				_listenCts = null;
			}
		}

		private async Task ListenLoopAsync(CancellationToken ct)
		{
			while (!ct.IsCancellationRequested)
			{
				try
				{
					await Task.Delay(_pollInterval, ct);

					// Avoid overlapping polls
					if (!await _pollGate.WaitAsync(0, ct))
						continue;

					try
					{
						if (_chatService == null) continue;
						if (SelectedContact == null) continue;
						if (!Session.UserId.HasValue) continue;

						// If user switched conversations, ignore this loop until restarted
						if (_listeningConversationId == Guid.Empty ||
							SelectedContact.ConversationId != _listeningConversationId)
							continue;

						var newMsgs = await _chatService.GetMessagesAfterAsync(
							SelectedContact.ConversationId,
							afterUtc: _lastSeenUtc);

						if (newMsgs == null || newMsgs.Count == 0)
							continue;

						var me = Session.UserId.Value;
						var contact = SelectedContact;

						Application.Current?.Dispatcher.Invoke(() =>
						{
							foreach (var m in newMsgs.OrderBy(x => x.SentAtUtc))
								Append(contact, m, me);
						});
					}
					finally
					{
						_pollGate.Release();
					}
				}
				catch (OperationCanceledException)
				{
					return;
				}
				catch (ApiException ex) when (ex.IsUnauthorized)
				{
					// Session.NotifyExpired already told the shell; stop polling.
					return;
				}
				catch
				{
					// Transient failure - keep polling, the next round may succeed.
				}
			}
		}

		// ----------------------------------
		// Shared request wrapper
		// ----------------------------------

		/// <summary>
		/// Runs an API call with a busy flag and turns failures into a visible status
		/// message instead of an unhandled exception or a silent no-op.
		/// </summary>
		private async Task RunAsync(Func<Task> action)
		{
			IsBusy = true;
			StatusMessage = null;

			try
			{
				await action();
			}
			catch (ApiException ex)
			{
				StatusMessage = ApiException.Describe(ex);
			}
			catch (Exception ex)
			{
				StatusMessage = ex.Message;
			}
			finally
			{
				IsBusy = false;
			}
		}
	}
}
