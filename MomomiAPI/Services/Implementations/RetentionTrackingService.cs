using Microsoft.EntityFrameworkCore;
using MomomiAPI.Data;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Services.Implementations
{
    public class RetentionTrackingService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RetentionTrackingService> _logger;

        public RetentionTrackingService(IServiceProvider serviceProvider, ILogger<RetentionTrackingService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await TrackRetentionMilestones();
                    await Task.Delay(TimeSpan.FromHours(6), stoppingToken); // Run every 6 hours
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in retention tracking service");
                    await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken); // Wait before retry
                }
            }
        }

        private async Task TrackRetentionMilestones()
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MomomiDbContext>();
            var analyticsService = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();

            var now = DateTime.UtcNow;

            // Track 1-day retention
            await TrackRetentionForPeriod(dbContext, analyticsService, now.AddDays(-1), "day_1");

            // Track 7-day retention
            await TrackRetentionForPeriod(dbContext, analyticsService, now.AddDays(-7), "day_7");

            // Track 30-day retention
            await TrackRetentionForPeriod(dbContext, analyticsService, now.AddDays(-30), "day_30");
        }

        private async Task TrackRetentionForPeriod(MomomiDbContext dbContext, IAnalyticsService analyticsService,
            DateTime registrationDate, string milestoneType)
        {
            var targetDate = registrationDate.Date;

            var usersFromDate = await dbContext.Users
                .Where(u => u.CreatedAt.Date == targetDate && u.IsActive)
                .Select(u => new { u.Id, u.CreatedAt, u.LastActive })
                .ToListAsync();

            foreach (var user in usersFromDate)
            {
                var daysSinceRegistration = (int)(DateTime.UtcNow - user.CreatedAt).TotalDays;

                // Consider user as retained if they were active within the last 24 hours of the milestone
                var isRetained = user.LastActive >= DateTime.UtcNow.AddDays(-1);

                if (isRetained)
                {
                    var retentionData = new RetentionData
                    {
                        MilestoneType = milestoneType,
                        RegistrationDate = user.CreatedAt,
                        FirstReturnDate = user.LastActive,
                        TotalSessionsInPeriod = 1, // Simplified - could be enhanced
                        DaysSinceRegistration = daysSinceRegistration
                    };

                    await analyticsService.TrackRetentionMilestoneAsync(user.Id, retentionData);
                }
            }

            _logger.LogDebug("Processed {Count} users for {MilestoneType} retention",
                usersFromDate.Count, milestoneType);
        }
    }
}