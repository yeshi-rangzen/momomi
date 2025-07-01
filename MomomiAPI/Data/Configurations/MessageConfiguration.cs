using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MomomiAPI.Models.Entities;

namespace MomomiAPI.Data.Configurations
{
    public class MessageConfiguration : IEntityTypeConfiguration<Message>
    {
        public void Configure(EntityTypeBuilder<Message> builder)
        {
            builder.HasKey(m => m.Id);

            builder.HasIndex(m => new { m.ConversationId, m.SentAt })
                .HasDatabaseName("idx_messages_conversation")
                .IsDescending(false, true); // SentAt descending

            builder.HasIndex(m => new { m.ConversationId, m.IsRead })
                .HasDatabaseName("idx_messages_unread")
                .HasFilter("is_read = false");

            // Configure explicit relationships to avoid ambiguity
            builder.HasOne(m => m.Conversation)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(m => m.Sender)
                .WithMany(u => u.MessagesSent)
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
