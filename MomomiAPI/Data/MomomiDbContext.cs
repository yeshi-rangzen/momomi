using Microsoft.EntityFrameworkCore;
using MomomiAPI.Data.Configurations;
using MomomiAPI.Models.Entities;

namespace MomomiAPI.Data
{
    public class MomomiDbContext : DbContext
    {
        public MomomiDbContext(DbContextOptions<MomomiDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<UserPhoto> UserPhotos { get; set; }
        public DbSet<UserPreference> UserPreferences { get; set; }
        public DbSet<UserSwipe> UserSwipes { get; set; }
        public DbSet<Conversation> Conversations { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<UserReport> UserReports { get; set; }
        public DbSet<UserSubscription> UserSubscriptions { get; set; }
        public DbSet<UserUsageLimit> UserUsageLimits { get; set; }
        public DbSet<PushNotification> PushNotifications { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Apply configurations
            modelBuilder.ApplyConfiguration(new UserConfiguration());
            modelBuilder.ApplyConfiguration(new UserPhotoConfiguration());
            modelBuilder.ApplyConfiguration(new UserPreferenceConfiguration());
            modelBuilder.ApplyConfiguration(new UserSwipeConfiguration());
            modelBuilder.ApplyConfiguration(new ConversationConfiguration());
            modelBuilder.ApplyConfiguration(new MessageConfiguration());
            modelBuilder.ApplyConfiguration(new UserReportConfiguration());
            modelBuilder.ApplyConfiguration(new UserSubscriptionConfiguration());
            modelBuilder.ApplyConfiguration(new UserUsageLimitConfiguration());
            modelBuilder.ApplyConfiguration(new PushNotificationConfiguration());

            // Enable UUID extension for PostgreSQL
            modelBuilder.HasPostgresExtension("uuid-ossp");

            // Configure updated_at triggers
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var properties = entityType.GetProperties()
                    .Where(p => p.Name == "UpdatedAt" && p.ClrType == typeof(DateTime));

                foreach (var property in properties)
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .Property(property.Name)
                        .HasDefaultValueSql("NOW()")
                        .ValueGeneratedOnAddOrUpdate();
                }
            }

            // Configure created_at defaults
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var properties = entityType.GetProperties()
                    .Where(p => p.Name == "CreatedAt" && p.ClrType == typeof(DateTime));

                foreach (var property in properties)
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .Property(property.Name)
                        .HasDefaultValueSql("NOW()");
                }
            }
        }

        public override int SaveChanges()
        {
            UpdateTimestamps();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateTimestamps();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void UpdateTimestamps()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                if (entry.Property("UpdatedAt") != null)
                {
                    entry.Property("UpdatedAt").CurrentValue = DateTime.UtcNow;
                }

                if (entry.State == EntityState.Added && entry.Property("CreatedAt") != null)
                {
                    entry.Property("CreatedAt").CurrentValue = DateTime.UtcNow;
                }
            }
        }
    }
}