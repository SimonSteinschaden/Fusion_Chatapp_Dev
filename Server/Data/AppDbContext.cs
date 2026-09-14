using Microsoft.EntityFrameworkCore;
using Server.Models;
using Server.Models.Chat;

namespace Server.Data
{
    /// <summary>
    /// Entity Framework Core database context defining application entities,
    /// relationships, and constraints for users, clients, and chat conversations.
    /// </summary>
    public class AppDbContext : DbContext
	{
		public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

		public DbSet<User> Users => Set<User>();
		public DbSet<ClientDevice> Clients => Set<ClientDevice>();
		public DbSet<Conversation> Conversations => Set<Conversation>();
		public DbSet<ConversationMember> ConversationMembers => Set<ConversationMember>();
        public DbSet<Message> Messages => Set<Message>();

        protected override void OnModelCreating(ModelBuilder modelBuilder) //postgresql table
		{
			base.OnModelCreating(modelBuilder);

			//CHECKING ON UNIQUE EMAIL,ID,USERNAME
			modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
			modelBuilder.Entity<User>().HasIndex(c => c.Username).IsUnique();
			modelBuilder.Entity<ClientDevice>().HasIndex(c => c.ClientId).IsUnique();
			modelBuilder.Entity<ClientDevice>().HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId);

			modelBuilder.Entity<ConversationMember>().HasKey(cm => new { cm.ConversationId, cm.UserId });
			modelBuilder.Entity<ConversationMember>().HasOne(cm => cm.Conversation).WithMany(c => c.Members).HasForeignKey(cm => cm.ConversationId);
			modelBuilder.Entity<ConversationMember>().HasOne(cm => cm.User).WithMany(u => u.ConversationMemberships).HasForeignKey(cm => cm.UserId);

            // Conversation -> Messages
            modelBuilder.Entity<Message>().HasKey(m => m.Id);
            modelBuilder.Entity<Message>().HasIndex(m => new { m.ConversationId, m.SentAtUtc });

            modelBuilder.Entity<Conversation>().HasMany(c => c.Messages).WithOne(m => m.Conversation).HasForeignKey(m => m.ConversationId);

            // Message -> Sender (User)
            modelBuilder.Entity<Message>().HasOne(m => m.SenderUser).WithMany().HasForeignKey(m => m.SenderUserId).OnDelete(DeleteBehavior.Restrict);

        }
	}
}
