using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MomomiAPI.Models.Entities;

namespace MomomiAPI.Data.Configurations
{
    public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
    {
        public void Configure(EntityTypeBuilder<Conversation> builder)
        {
            builder.HasKey(c => c.Id);

            //  For finding user conversations efficiently
            builder.HasIndex(c => new { c.User1Id, c.IsActive, c.UpdatedAt })
                .HasDatabaseName("idx_conversations_user1_active")
                .IsDescending(false, false, true)
                .IncludeProperties(c => new { c.User2Id, c.CreatedAt });

            builder.HasIndex(c => new { c.User2Id, c.IsActive, c.UpdatedAt })
                .HasDatabaseName("idx_conversations_user2_active")
                .IsDescending(false, false, true)
                .IncludeProperties(c => new { c.User1Id, c.CreatedAt });

            // Unique constraint to prevent duplicate conversations
            builder.HasIndex(c => new { c.User1Id, c.User2Id })
                .IsUnique()
                .HasDatabaseName("idx_conversations_users_unique");

            // For finding specific conversation between two users
            builder.HasIndex(c => new { c.User1Id, c.User2Id, c.IsActive })
                .HasDatabaseName("idx_conversations_users_active");

            // Configure explicit relationships to avoid ambiguity
            builder.HasOne(c => c.User1)
                .WithMany(u => u.ConversationsAsUser1)
                .HasForeignKey(c => c.User1Id)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(c => c.User2)
                .WithMany(u => u.ConversationsAsUser2)
                .HasForeignKey(c => c.User2Id)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(c => c.Messages)
                .WithOne(m => m.Conversation)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Ensure User1Id < User2Id for consistency
            builder.ToTable(t => t.HasCheckConstraint("CHK_Conversation_UserOrder", "user1_id < user2_id"));
        }
    }
}
