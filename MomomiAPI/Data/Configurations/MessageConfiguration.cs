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

            // For conversation message retrieval with pagination
            builder.HasIndex(m => new { m.ConversationId, m.SentAt })
                .HasDatabaseName("idx_messages_conversation_time")
                .IsDescending(false, true)
                .IncludeProperties(m => new { m.SenderId, m.Content, m.MessageType, m.IsRead });

            // For unread message counts (very frequent query)
            builder.HasIndex(m => new { m.ConversationId, m.SenderId, m.IsRead })
                .HasDatabaseName("idx_messages_unread_count")
                .HasFilter("is_read = false")
                .IncludeProperties(m => m.SentAt);

            // For marking messages as read (batch operations)
            builder.HasIndex(m => new { m.ConversationId, m.IsRead })
                .HasDatabaseName("idx_messages_conversation_read")
                .HasFilter("is_read = false");

            // For getting last message in conversations
            builder.HasIndex(m => new { m.ConversationId, m.SentAt })
                .HasDatabaseName("idx_messages_last_message")
                .IsDescending(false, true);

            // For message deletion/editing (by sender)
            builder.HasIndex(m => new { m.SenderId, m.SentAt })
                .HasDatabaseName("idx_messages_sender_time")
                .IsDescending(false, true);

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
