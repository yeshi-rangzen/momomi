using Microsoft.EntityFrameworkCore;
using MomomiAPI.Common.Caching;
using MomomiAPI.Common.Results;
using MomomiAPI.Data;
using MomomiAPI.Helpers;
using MomomiAPI.Models.Entities;
using MomomiAPI.Models.Enums;
using MomomiAPI.Models.Requests;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Services.Implementations
{
    public class ReportingService : IReportingService
    {
        private readonly MomomiDbContext _dbContext;
        private readonly ICacheService _cacheService;
        private readonly ILogger<ReportingService> _logger;

        public ReportingService(
            MomomiDbContext dbContext,
            ICacheService cacheService,
            ILogger<ReportingService> logger)
        {
            _dbContext = dbContext;
            _cacheService = cacheService;
            _logger = logger;
        }

        /// <summary>
        /// Reports a user for policy violations with optimized database operations
        /// </summary>
        public async Task<OperationResult> ReportUserAsync(ReportUserRequest reportRequest)
        {
            try
            {
                _logger.LogInformation("User {ReporterId} attempting to report user {ReportedUserId} for {Reason}",
                    reportRequest.ReporterUserId, reportRequest.ReportedUserId, reportRequest.Reason);

                // Validate inputs
                var validationResult = ValidateReportRequest(reportRequest.ReporterUserId!, reportRequest.ReportedUserId, reportRequest.Description);
                if (!validationResult.Success)
                {
                    return UserReportResult.ValidationError(validationResult.ErrorMessage!);
                }

                // Get user data and check if already reported in single query
                var reportData = await GetReportValidationDataAsync((Guid)reportRequest.ReporterUserId!, reportRequest.ReportedUserId);
                if (!reportData.IsValid)
                {
                    return reportData.ErrorResult!;
                }

                var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

                return await executionStrategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _dbContext.Database.BeginTransactionAsync();
                    try
                    {
                        // Create report record
                        var userReport = new UserReport
                        {
                            ReporterEmail = reportData.ReporterEmail,
                            ReportedEmail = reportData.ReportedEmail,
                            ReportedGender = reportRequest.ReportedUserGender,
                            Reason = reportRequest.Reason,
                            Description = reportRequest.Description,
                            Status = "pending",
                            CreatedAt = DateTime.UtcNow
                        };

                        _dbContext.UserReports.Add(userReport);

                        // Remove any existing matches/conversations between users
                        var removalResult = await RemoveUserInteractionsAsync((Guid)reportRequest.ReporterUserId!, reportRequest.ReportedUserId);

                        await _dbContext.SaveChangesAsync();
                        await transaction.CommitAsync();

                        // Invalidate relevant caches (fire and forget for performance
                        FireAndForgetHelper.Run(
                            InvalidateReportingCaches((Guid)reportRequest.ReporterUserId!, reportRequest.ReportedUserId),
                            _logger,
                            "Invalidate Reporting Caches");


                        return UserReportResult.Successful();
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reporting user {ReportedUserId} by user {ReporterId}",
                    reportRequest.ReporterUserId, reportRequest.ReportedUserId);
                return UserReportResult.Error("Unable to submit report. Please try again.");
            }
        }

        /// <summary>
        /// Blocks a user and removes all interactions (optimized for immediate blocking)
        /// </summary>
        public async Task<OperationResult> BlockUserAsync(BlockUserRequest blockRequest)
        {
            try
            {
                _logger.LogInformation("User {BlockerId} attempting to block user {BlockedUserId}",
                    blockRequest.BlockerUserId, blockRequest.BlockedUserId);

                // Validate inputs
                if (blockRequest.BlockerUserId == blockRequest.BlockedUserId)
                {
                    return BlockUserResult.CannotBlockSelf();
                }

                // Check if already blocked and validate users exist
                var blockData = await GetBlockValidationDataAsync((Guid)blockRequest.BlockerUserId!, blockRequest.BlockedUserId);
                if (!blockData.IsValid)
                {
                    return blockData.ErrorResult!;
                }

                var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

                return await executionStrategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _dbContext.Database.BeginTransactionAsync();
                    try
                    {
                        // Create block report (using Block reason)
                        var blockReport = new UserReport
                        {
                            ReporterEmail = blockData.BlockerEmail,
                            ReportedEmail = blockData.BlockedEmail,
                            ReportedGender = blockRequest.BlockedUserGender,
                            Reason = ReportReason.Block,
                            Description = "Quick blocked",
                            Status = "auto-resolved",
                            CreatedAt = DateTime.UtcNow,
                            ResolvedAt = DateTime.UtcNow
                        };

                        _dbContext.UserReports.Add(blockReport);

                        await _dbContext.SaveChangesAsync();
                        await transaction.CommitAsync();

                        _logger.LogInformation("User {BlockerId} successfully blocked user {BlockedUserId}",
                            blockRequest.BlockerUserId, blockRequest.BlockedUserId);

                        return BlockUserResult.Successful();
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error blocking user {BlockedUserId} by user {BlockerId}",
                    blockRequest.BlockerUserId, blockRequest.BlockedUserId);
                return BlockUserResult.Error("Unable to block user. Please try again.");
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Validates report request inputs
        /// </summary>
        private static OperationResult ValidateReportRequest(Guid? reporterId, Guid reportedId, string? description)
        {
            if (reporterId == reportedId)
            {
                return OperationResult.ValidationFailure("You cannot report yourself");
            }

            if (!string.IsNullOrEmpty(description) && description.Length > 1000)
            {
                return OperationResult.ValidationFailure("Report description cannot exceed 1000 characters");
            }

            return OperationResult.Successful();
        }

        /// <summary>
        /// Gets validation data for reporting in a single optimized query
        /// </summary>
        private async Task<ReportValidationData> GetReportValidationDataAsync(
            Guid reporterUserId,
            Guid reportedUserId)
        {
            var reporterUserEmail = await _dbContext.Users
                .Where(u => u.Id == reporterUserId && u.IsActive)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            var reportedUserEmail = await _dbContext.Users
                .Where(u => u.Id == reportedUserId && u.IsActive)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            var alreadyReported = await _dbContext.UserReports
                .AnyAsync(ur => ur.ReporterEmail == reporterUserEmail &&
                                ur.ReportedEmail == reportedUserEmail);

            if (alreadyReported)
            {
                return ReportValidationData.Invalid(UserReportResult.AlreadyReported());
            }

            return ReportValidationData.Valid(reporterUserEmail!, reportedUserEmail!);
        }

        /// <summary>
        /// Gets validation data for blocking in a single optimized query
        /// </summary>
        private async Task<BlockValidationData> GetBlockValidationDataAsync(Guid blockerUserId, Guid blockedUserId)
        {
            var blockerUserEmail = await _dbContext.Users
                .Where(u => u.Id == blockerUserId && u.IsActive)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            var blockedUserEmail = await _dbContext.Users
                .Where(u => u.Id == blockedUserId && u.IsActive)
                .Select(u => u.Email)
                .FirstOrDefaultAsync();

            var alreadyReported = await _dbContext.UserReports
               .AnyAsync(ur => ur.ReporterEmail == blockerUserEmail &&
                               ur.ReportedEmail == blockedUserEmail);

            if (alreadyReported)
            {
                return BlockValidationData.Invalid(BlockUserResult.AlreadyBlocked());
            }

            return BlockValidationData.Valid(blockerUserEmail!, blockedUserEmail!);
        }

        /// <summary>
        /// Removes all interactions between two users (swipes, matches, conversations)
        /// </summary>
        private async Task<InteractionRemovalResult> RemoveUserInteractionsAsync(Guid userId1, Guid userId2)
        {
            var result = new InteractionRemovalResult();

            try
            {
                // Remove swipes between users
                var swipes = await _dbContext.UserSwipes
                    .Where(us => (us.SwiperUserId == userId1 && us.SwipedUserId == userId2) ||
                                (us.SwiperUserId == userId2 && us.SwipedUserId == userId1))
                    .ToListAsync();

                if (swipes.Any())
                {
                    _dbContext.UserSwipes.RemoveRange(swipes);
                    result.MatchRemoved = true;
                }

                // Remove conversation and messages
                var conversation = await _dbContext.Conversations
                    .Include(c => c.Messages)
                    .FirstOrDefaultAsync(c => (c.User1Id == userId1 && c.User2Id == userId2) ||
                                            (c.User1Id == userId2 && c.User2Id == userId1));

                if (conversation != null)
                {
                    result.MessagesDeleted = conversation.Messages.Count;

                    // Delete messages first
                    if (conversation.Messages.Any())
                    {
                        _dbContext.Messages.RemoveRange(conversation.Messages);
                    }

                    // Delete conversation
                    _dbContext.Conversations.Remove(conversation);
                    result.ConversationDeleted = true;

                    _logger.LogDebug("Removed conversation {ConversationId} and {MessageCount} messages between users {UserId1} and {UserId2}",
                        conversation.Id, result.MessagesDeleted, userId1, userId2);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing interactions between users {UserId1} and {UserId2}", userId1, userId2);
                throw;
            }
        }

        /// <summary>
        /// Invalidates reporting-related caches
        /// </summary>
        private async Task InvalidateReportingCaches(Guid reporterId, Guid reportedUserId)
        {
            try
            {
                var keysToInvalidate = new List<string>
                {
                    CacheKeys.Matching.UserMatches(reporterId),
                    CacheKeys.Matching.UserMatches(reportedUserId),
                    CacheKeys.Messaging.UserConversations(reporterId),
                    CacheKeys.Messaging.UserConversations(reportedUserId),
                };

                await _cacheService.RemoveManyAsync(keysToInvalidate);
                _logger.LogDebug("Invalidated {Count} cache keys for reporting between users {ReporterId} and {ReportedUserId}",
                    keysToInvalidate.Count, reporterId, reportedUserId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate reporting caches for users {ReporterId} and {ReportedUserId}",
                    reporterId, reportedUserId);
            }
        }

        #endregion

        #region Helper Classes

        /// <summary>
        /// Helper class for report validation results
        /// </summary>
        private class ReportValidationData
        {
            public bool IsValid { get; set; }
            public string ReporterEmail { get; set; } = string.Empty;
            public string ReportedEmail { get; set; } = string.Empty;
            public UserReportResult? ErrorResult { get; set; }

            public static ReportValidationData Valid(string reporterEmail, string reportedEmail) => new() { IsValid = true, ReporterEmail = reporterEmail, ReportedEmail = reportedEmail };
            public static ReportValidationData Invalid(UserReportResult errorResult) =>
                new() { IsValid = false, ErrorResult = errorResult };
        }

        /// <summary>
        /// Helper class for block validation results
        /// </summary>
        private class BlockValidationData
        {
            public bool IsValid { get; set; }

            public string BlockerEmail { get; set; } = string.Empty;
            public string BlockedEmail { get; set; } = string.Empty;
            public BlockUserResult? ErrorResult { get; set; }

            public static BlockValidationData Valid(string blockerEmail, string blockedEmail) => new() { IsValid = true, BlockerEmail = blockerEmail, BlockedEmail = blockedEmail };
            public static BlockValidationData Invalid(BlockUserResult errorResult) =>
                new() { IsValid = false, ErrorResult = errorResult };
        }

        /// <summary>
        /// Helper class for interaction removal results
        /// </summary>
        private class InteractionRemovalResult
        {
            public bool MatchRemoved { get; set; }
            public bool ConversationDeleted { get; set; }
            public int MessagesDeleted { get; set; }
        }

        #endregion
    }
}