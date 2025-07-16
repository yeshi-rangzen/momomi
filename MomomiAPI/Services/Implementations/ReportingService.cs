using Microsoft.EntityFrameworkCore;
using MomomiAPI.Common.Caching;
using MomomiAPI.Common.Results;
using MomomiAPI.Data;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Entities;
using MomomiAPI.Models.Enums;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Services.Implementations
{
    public class ReportingService : IReportingService
    {
        private readonly MomomiDbContext _dbContext;
        private readonly ICacheService _cacheService;
        private readonly ICacheInvalidation _cacheInvalidation;
        private readonly ILogger<ReportingService> _logger;

        public ReportingService(
            MomomiDbContext dbContext,
            ICacheService cacheService,
            ICacheInvalidation cacheInvalidation,
            ILogger<ReportingService> logger)
        {
            _dbContext = dbContext;
            _cacheService = cacheService;
            _cacheInvalidation = cacheInvalidation;
            _logger = logger;
        }

        public async Task<OperationResult> ReportUserAsync(Guid reporterId, Guid reportedId, ReportReason reason, string? description = null)
        {
            try
            {
                _logger.LogInformation("User {ReporterId} attempting to block user {ReportedId}", reporterId, reportedId);

                // Check if already reported
                var existingReport = await _dbContext.UserReports
                    .FirstOrDefaultAsync(ur => ur.ReporterId == reporterId && ur.ReportedId == reportedId);

                if (existingReport != null)
                {
                    return OperationResult.BusinessRuleViolation("You have already reported this user.");
                }

                // Validate description length if provided
                if (!string.IsNullOrEmpty(description) && description.Length > 1000)
                {
                    return OperationResult.ValidationFailure("Report description cannot exceed 1000 characters.");
                }

                // Create report record
                var userReport = new UserReport
                {
                    ReporterId = reporterId,
                    ReportedId = reportedId,
                    Reason = reason,
                    Description = description,
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.UserReports.Add(userReport);

                // Remove any existing matches/conversations between users
                await RemoveMatchAndConversationAsync(reporterId, reportedId);
                await _dbContext.SaveChangesAsync();

                // Invalidate relevant caches
                await _cacheInvalidation.InvalidateMatchingCaches(reporterId, reportedId);
                await _cacheInvalidation.InvalidateUserDiscovery(reporterId);
                await _cacheService.RemoveAsync(CacheKeys.Safety.UserReports(reporterId));

                _logger.LogInformation("User {ReporterId} reported user {ReportedId} for {Reason}",
                    reporterId, reportedId, reason);
                return OperationResult.Successful()
                    .WithMetadata("reported_user_id", reportedId)
                    .WithMetadata("reason", reason.ToString())
                    .WithMetadata("reported_at", DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reporting user {ReportedId} by user {ReporterId}", reportedId, reporterId);
                return OperationResult.Failed("Unable to submit report. Please try again.");
            }
        }

        public async Task<OperationResult<List<UserReportDTO>>> GetUserReportsAsync(Guid userId)
        {
            try
            {
                _logger.LogDebug("Retrieving reports for user {UserId}", userId);

                var cacheKey = CacheKeys.Safety.UserReports(userId);
                var cachedReports = await _cacheService.GetAsync<List<UserReportDTO>>(cacheKey);

                if (cachedReports != null)
                {
                    _logger.LogDebug("Returning cached reports for user {UserId}", userId);
                    return OperationResult<List<UserReportDTO>>.Successful(cachedReports);
                }

                // Do we need all the reported user details?
                var reports = await _dbContext.UserReports
                    .Where(ur => ur.ReporterId == userId)
                    .Include(ur => ur.Reported)
                        .ThenInclude(u => u.Photos)
                    .Select(ur => new UserReportDTO
                    {
                        Id = ur.Id,
                        ReportedUserId = ur.ReportedId,
                        ReportedUserName = $"{ur.Reported.FirstName} {ur.Reported.LastName}".Trim(),
                        PrimaryPhoto = ur.Reported.Photos.FirstOrDefault(p => p.IsPrimary) != null ?
                            ur.Reported.Photos.First(p => p.IsPrimary).Url :
                            ur.Reported.Photos.OrderBy(p => p.PhotoOrder).FirstOrDefault() != null ?
                            ur.Reported.Photos.OrderBy(p => p.PhotoOrder).First().Url : null,
                        Reason = ur.Reason,
                        Description = ur.Description,
                        Status = ur.Status,
                        ReportedAt = ur.CreatedAt,
                        ResolvedAt = ur.ResolvedAt
                    })
                    .OrderByDescending(r => r.ReportedAt)
                    .ToListAsync();

                // Cache for 15 minutes
                await _cacheService.SetAsync(cacheKey, reports, TimeSpan.FromMinutes(15));

                _logger.LogDebug("Retrieved {Count} reports for user {UserId}", reports.Count, userId);
                return OperationResult<List<UserReportDTO>>.Successful(reports);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting reports for user {UserId}", userId);
                return OperationResult<List<UserReportDTO>>.Failed("Unable to retrieve reports. Please try again.");
            }
        }

        public async Task<OperationResult<bool>> IsUserReportedAsync(Guid reporterId, Guid reportedUserId)
        {
            try
            {
                var isReported = await _dbContext.UserReports
                    .AnyAsync(ur => ur.ReporterId == reporterId && ur.ReportedId == reportedUserId);

                return OperationResult<bool>.Successful(isReported);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {ReportedUserId} is reported by {ReporterId}", reportedUserId, reporterId);
                return OperationResult<bool>.Failed("Unable to check report status. Please try again.");
            }
        }

        private async Task RemoveMatchAndConversationAsync(Guid userId1, Guid userId2)
        {
            try
            {
                var likes = await _dbContext.UserLikes
                    .Where(ul => (ul.LikerUserId == userId1 && ul.LikedUserId == userId2) ||
                               (ul.LikerUserId == userId2 && ul.LikedUserId == userId1))
                    .ToListAsync();

                foreach (var like in likes)
                {
                    _dbContext.UserLikes.Remove(like);
                }

                var conversation = await _dbContext.Conversations
                    .Include(c => c.Messages)
                    .FirstOrDefaultAsync(c => (c.User1Id == userId1 && c.User2Id == userId2) ||
                                            (c.User1Id == userId2 && c.User2Id == userId1));

                if (conversation != null)
                {
                    // Delete all messages first
                    _dbContext.Messages.RemoveRange(conversation.Messages);

                    // Delete the conversation
                    _dbContext.Conversations.Remove(conversation);

                    _logger.LogDebug("Removed conversation {ConversationId} and {MessageCount} messages due to blocking/reporting",
                        conversation.Id, conversation.Messages.Count);
                }

                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing match and conversation between {UserId1} and {UserId2}", userId1, userId2);
                throw; // Re-throw to be handled by calling method
            }
        }
    }
}
