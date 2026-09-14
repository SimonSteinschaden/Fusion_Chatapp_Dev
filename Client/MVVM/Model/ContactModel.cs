using System;
using System.Collections.ObjectModel;
using System.Linq;
using WPF_Fusion.Infrastructure;


namespace WPF_Fusion.MVVM.Model
{
    /// <summary>
    /// Represents a chat contact including conversation metadata
    /// and the associated message collection for UI binding.
    /// </summary>
    public class ContactModel : ObservableObject
	{
		public Guid ConversationId { get; set; }

		private string _username = "";
		public string Username
		{
			get => _username;
			set { _username = value; OnPropertyChanged(); }
		}

		public string? ImageSource { get; set; }

        // Messages belonging to this conversation (UI-bound collection).
        public ObservableCollection<MessageModel> Messages { get; } = new();

        private string? _lastMessagePreview;
        /// <summary>
        /// Text of the newest message. Supplied by the server for the conversation list,
        /// so a preview is available before the conversation has ever been opened.
        /// </summary>
        public string? LastMessagePreview
        {
            get => _lastMessagePreview;
            set { _lastMessagePreview = value; OnPropertyChanged(); OnPropertyChanged(nameof(LastMessage)); }
        }

        private DateTime? _lastMessageAt;
        /// <summary>
        /// Local time of the newest message, used to sort and label the list.
        /// </summary>
        public DateTime? LastMessageAt
        {
            get => _lastMessageAt;
            set { _lastMessageAt = value; OnPropertyChanged(); }
        }

        // Prefers a loaded message, falls back to the server-supplied preview.
        public string LastMessage => Messages.LastOrDefault()?.Message ?? LastMessagePreview ?? "";

		public ContactModel()
        {
			// Update LastMessage whenever the message collection changes.
            Messages.CollectionChanged += (_, __) => OnPropertyChanged(nameof(LastMessage));
		}
	}

}
