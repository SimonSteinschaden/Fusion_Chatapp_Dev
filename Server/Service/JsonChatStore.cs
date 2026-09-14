using System.Text;
using Newtonsoft.Json;
using Server.Interface;
using Server.Persistence;

public sealed class JsonChatStore : IChatStore
{
	private readonly string _conversationsPath;
	private readonly string _messagesPath;

	private readonly SemaphoreSlim _mutex = new SemaphoreSlim(1, 1);

	public JsonChatStore(string conversationsPath, string messagesPath)
	{
		if (string.IsNullOrWhiteSpace(conversationsPath))
			throw new ArgumentException("Conversations file path is required.", nameof(conversationsPath));
		if (string.IsNullOrWhiteSpace(messagesPath))
			throw new ArgumentException("Messages file path is required.", nameof(messagesPath));

		_conversationsPath = conversationsPath;
		_messagesPath = messagesPath;
	}

	// ----------------------------
	// Conversations
	// ----------------------------

	public async Task<ConversationRecord?> GetConversationAsync(Guid conversationId)
	{
		if (conversationId == Guid.Empty)
			return null;

		await _mutex.WaitAsync().ConfigureAwait(false);
		try
		{
			var convos = await LoadConversationsAsync().ConfigureAwait(false);
			return convos.FirstOrDefault(c => c.ConversationId == conversationId);
		}
		finally
		{
			_mutex.Release();
		}
	}

	public async Task<List<ConversationRecord>> GetConversationsForUserAsync(Guid userId)
	{
		if (userId == Guid.Empty) return new List<ConversationRecord>();

		await _mutex.WaitAsync().ConfigureAwait(false);
		try
		{
			var convos = await LoadConversationsAsync().ConfigureAwait(false);
			return convos
				.Where(c => c.ParticipantUserIds != null && c.ParticipantUserIds.Contains(userId))
				.ToList();
		}
		finally
		{
			_mutex.Release();
		}
	}

	public async Task<ConversationRecord> GetOrCreateDirectAsync(Guid userA, Guid userB)
	{
		if (userA == Guid.Empty) throw new ArgumentException("userA is required.", nameof(userA));
		if (userB == Guid.Empty) throw new ArgumentException("userB is required.", nameof(userB));
		if (userA == userB) throw new ArgumentException("Direct chat requires two different users.");

		var pairKey = DirectPairKey(userA, userB);

		await _mutex.WaitAsync().ConfigureAwait(false);
		try
		{
			var convos = await LoadConversationsAsync().ConfigureAwait(false);

			// Find existing direct conversation (order-independent)
			var existing = convos.FirstOrDefault(c =>
				c != null &&
				c.IsGroup == false &&
				c.ParticipantUserIds != null &&
				c.ParticipantUserIds.Count == 2 &&
				DirectPairKey(c.ParticipantUserIds[0], c.ParticipantUserIds[1]) == pairKey);

			if (existing != null)
				return existing;

			// Create new
			var created = new ConversationRecord
			{
				ConversationId = Guid.NewGuid(),
				IsGroup = false,
				Title = null,
				ParticipantUserIds = new List<Guid> { userA, userB }
			};

			convos.Add(created);
			await SaveConversationsAsync(convos).ConfigureAwait(false);

			return created;
		}
		finally
		{
			_mutex.Release();
		}
	}

	public async Task<ConversationRecord> CreateGroupAsync(string title, List<Guid> participants)
	{
		title = (title ?? "").Trim();

		if (participants == null) throw new ArgumentNullException(nameof(participants));

		var cleaned = participants
			.Where(id => id != Guid.Empty)
			.Distinct()
			.ToList();

		if (cleaned.Count < 3)
			throw new ArgumentException("Group chat requires at least 3 distinct participants.", nameof(participants));

		await _mutex.WaitAsync().ConfigureAwait(false);
		try
		{
			var convos = await LoadConversationsAsync().ConfigureAwait(false);

			var created = new ConversationRecord
			{
				ConversationId = Guid.NewGuid(),
				IsGroup = true,
				Title = string.IsNullOrWhiteSpace(title) ? "Group" : title,
				ParticipantUserIds = cleaned
			};

			convos.Add(created);
			await SaveConversationsAsync(convos).ConfigureAwait(false);

			return created;
		}
		finally
		{
			_mutex.Release();
		}
	}

	// ----------------------------
	// Messages
	// ----------------------------

	public async Task<List<MessageRecord>> GetMessagesAsync(Guid conversationId, int take = 50, int skip = 0)
	{
		if (conversationId == Guid.Empty) return new List<MessageRecord>();
		if (take <= 0) take = 50;
		if (take > 500) take = 500;
		if (skip < 0) skip = 0;

		await _mutex.WaitAsync().ConfigureAwait(false);
		try
		{
			var messages = await LoadMessagesAsync().ConfigureAwait(false);

			// Select the newest messages first so that opening a long conversation shows
			// its end, then flip back to chronological order for display.
			return messages
				.Where(m => m.ConversationId == conversationId)
				.OrderByDescending(m => m.SentAtUtc)
				.Skip(skip)
				.Take(take)
				.OrderBy(m => m.SentAtUtc)
				.ToList();
		}
		finally
		{
			_mutex.Release();
		}
	}

	public async Task<List<MessageRecord>> GetMessagesAfterAsync(Guid conversationId, DateTime afterUtc, int take = 200)
	{
		if (conversationId == Guid.Empty) return new List<MessageRecord>();
		if (take <= 0) take = 200;
		if (take > 500) take = 500;

		// Timestamps are stored as UTC; normalise whatever the caller passed in.
		var threshold = afterUtc.Kind == DateTimeKind.Utc
			? afterUtc
			: afterUtc.ToUniversalTime();

		await _mutex.WaitAsync().ConfigureAwait(false);
		try
		{
			var messages = await LoadMessagesAsync().ConfigureAwait(false);

			return messages
				.Where(m => m.ConversationId == conversationId && m.SentAtUtc > threshold)
				.OrderBy(m => m.SentAtUtc)
				.Take(take)
				.ToList();
		}
		finally
		{
			_mutex.Release();
		}
	}

	public async Task<Dictionary<Guid, MessageRecord>> GetLastMessagesAsync(IEnumerable<Guid> conversationIds)
	{
		var ids = (conversationIds ?? Enumerable.Empty<Guid>())
			.Where(id => id != Guid.Empty)
			.ToHashSet();

		if (ids.Count == 0) return new Dictionary<Guid, MessageRecord>();

		await _mutex.WaitAsync().ConfigureAwait(false);
		try
		{
			var messages = await LoadMessagesAsync().ConfigureAwait(false);

			return messages
				.Where(m => ids.Contains(m.ConversationId))
				.GroupBy(m => m.ConversationId)
				.ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.SentAtUtc).First());
		}
		finally
		{
			_mutex.Release();
		}
	}

	public async Task<MessageRecord> AddMessageAsync(Guid conversationId, Guid senderUserId, string content)
	{
		if (conversationId == Guid.Empty) throw new ArgumentException("conversationId is required.", nameof(conversationId));
		if (senderUserId == Guid.Empty) throw new ArgumentException("senderUserId is required.", nameof(senderUserId));

		content = (content ?? "").Trim();
		if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Message content is empty.", nameof(content));

		await _mutex.WaitAsync().ConfigureAwait(false);
		try
		{
			var convos = await LoadConversationsAsync().ConfigureAwait(false);
			var convo = convos.FirstOrDefault(c => c.ConversationId == conversationId);

			if (convo == null)
				throw new InvalidOperationException("Conversation not found.");

			if (convo.ParticipantUserIds == null || !convo.ParticipantUserIds.Contains(senderUserId))
				throw new InvalidOperationException("Sender is not a participant of this conversation.");

			var messages = await LoadMessagesAsync().ConfigureAwait(false);

			// Two messages sent within the same clock tick would share a timestamp.
			// That breaks ordering, and because incremental polling asks for
			// "SentAtUtc > cursor" the second message would never be delivered.
			// Keeping timestamps strictly increasing per conversation avoids both.
			var latestInConversation = messages
				.Where(m => m.ConversationId == conversationId)
				.Select(m => m.SentAtUtc)
				.DefaultIfEmpty(DateTime.MinValue)
				.Max();

			var sentAtUtc = DateTime.UtcNow;
			if (sentAtUtc <= latestInConversation)
				sentAtUtc = latestInConversation.AddTicks(1);

			var created = new MessageRecord
			{
				MessageId = Guid.NewGuid(),
				ConversationId = conversationId,
				SenderUserId = senderUserId,
				Content = content,
				SentAtUtc = sentAtUtc
			};

			messages.Add(created);
			await SaveMessagesAsync(messages).ConfigureAwait(false);

			return created;
		}
		finally
		{
			_mutex.Release();
		}
	}

	// ----------------------------
	// Helpers
	// ----------------------------

	private static string DirectPairKey(Guid a, Guid b)
	{
		// order-independent key
		return a.CompareTo(b) < 0 ? $"{a:N}:{b:N}" : $"{b:N}:{a:N}";
	}

	private async Task<List<ConversationRecord>> LoadConversationsAsync()
		=> await LoadListAsync<ConversationRecord>(_conversationsPath).ConfigureAwait(false);

	private async Task SaveConversationsAsync(List<ConversationRecord> convos)
		=> await SaveListAsync(_conversationsPath, convos).ConfigureAwait(false);

	private async Task<List<MessageRecord>> LoadMessagesAsync()
		=> await LoadListAsync<MessageRecord>(_messagesPath).ConfigureAwait(false);

	private async Task SaveMessagesAsync(List<MessageRecord> messages)
		=> await SaveListAsync(_messagesPath, messages).ConfigureAwait(false);

	private static async Task<List<T>> LoadListAsync<T>(string path)
	{
		if (!File.Exists(path))
			return new List<T>();

		string json;
		using (var reader = new StreamReader(path, Encoding.UTF8))
		{
			json = await reader.ReadToEndAsync().ConfigureAwait(false);
		}

		if (string.IsNullOrWhiteSpace(json))
			return new List<T>();

		try
		{
			var data = JsonConvert.DeserializeObject<List<T>>(json);
			return data ?? new List<T>();
		}
		catch (JsonException)
		{
			return new List<T>();
		}
	}

	/// <summary>
	/// Writes the list atomically: serialise to a temporary file first, then replace the
	/// target. Writing in place would truncate the file before the new content is
	/// written, so a crash mid-write would destroy the entire history.
	/// </summary>
	private static async Task SaveListAsync<T>(string path, List<T> data)
	{
		var dir = Path.GetDirectoryName(path);
		if (!string.IsNullOrWhiteSpace(dir))
			Directory.CreateDirectory(dir);

		var json = JsonConvert.SerializeObject(data, Formatting.Indented);
		var tempPath = path + ".tmp";

		using (var writer = new StreamWriter(tempPath, false, Encoding.UTF8))
		{
			await writer.WriteAsync(json).ConfigureAwait(false);
			await writer.FlushAsync().ConfigureAwait(false);
		}

		if (File.Exists(path))
			File.Replace(tempPath, path, destinationBackupFileName: null);
		else
			File.Move(tempPath, path);
	}
}
