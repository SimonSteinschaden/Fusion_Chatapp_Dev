using System;

namespace WPF_Fusion.MVVM.Model
{
    /// <summary>
    /// Represents a chat message prepared for UI display,
    /// including sender information and layout-related metadata.
    /// </summary>
    public class MessageModel
    {
		public Guid MessageId { get; set; }
		public Guid SenderUserId { get; set; }
		public string Username { get; set; } = "";
        public string? UsernameColor { get; set; }
        public string? ImageSource { get; set; }

        public string Message { get; set; } = "";

        /// <summary>
        /// Send time in local time, for display.
        /// </summary>
        public DateTime Time { get; set; }

        /// <summary>
        /// Send time in UTC, used for ordering and for the incremental poll cursor.
        /// Keeping both avoids comparing a local timestamp against a UTC one.
        /// </summary>
        public DateTime SentAtUtc { get; set; }

        // Indicates whether the message was sent by the current user.
        public bool IsNativeOrigin { get; set; }

        // Optional flag used for UI grouping (e.g., first message in a sequence).
        public bool FirstMessage { get; set; }
    }
}
