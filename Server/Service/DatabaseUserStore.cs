using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Interface;
using Server.Models;
using Server.Persistence;

public sealed class DatabaseUserStore : IUserStore
{
	private readonly AppDbContext _db;

	public DatabaseUserStore(AppDbContext db)
	{
		_db = db;
	}

	public async Task<UserRecord?> FindByIdAsync(Guid userId)
	{
		var user = await _db.Users.FindAsync(userId);
		if (user == null) return null;

		return ToRecord(user);
	}

	public async Task<UserRecord?> FindByEmailAsync(string email)
	{
		email = (email ?? "").Trim().ToLowerInvariant();

		var user = await _db.Users
			.FirstOrDefaultAsync(u => u.Email == email);

		if (user == null) return null;

		return ToRecord(user);
	}

	public async Task<UserRecord?> FindByUsernameAsync(string username)
	{
		username = (username ?? "").Trim();

		var user = await _db.Users
			.FirstOrDefaultAsync(u => u.Username == username);

		if (user == null) return null;

		return ToRecord(user);
	}

	public async Task<bool> CreateAsync(UserRecord record)
	{
		if (record == null) return false;

		// prüfen ob bereits vorhanden
		var exists = await _db.Users.AnyAsync(u =>
			u.Email == record.Email ||
			u.Username == record.Username);

		if (exists)
			return false;

		var user = new User
		{
			Id = record.UserId == Guid.Empty ? Guid.NewGuid() : record.UserId,
			Email = record.Email,
			Username = record.Username,
			PasswordHash = record.PasswordHash,
			CreatedAtUtc = DateTime.UtcNow
		};

		_db.Users.Add(user);
		await _db.SaveChangesAsync();

		return true;
	}

	private static UserRecord ToRecord(User user)
	{
		return new UserRecord
		{
			UserId = user.Id,
			Email = user.Email,
			Username = user.Username,
			PasswordHash = user.PasswordHash
		};
	}
}
