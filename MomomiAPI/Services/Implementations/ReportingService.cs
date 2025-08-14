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
        public async Task<UserReportResult> ReportUserAsync(Guid reporterId, Guid reportedUserId,
            ReportReason reason, string? description = null)
        {
            try
            {
                _logger.LogInformation("User {ReporterId} attempting to report user {ReportedUserId} for {Reason}",
                    reporterId, reportedUserId, reason);

                // Validate inputs
                var validationResult = ValidateReportRequest(reporterId, reportedUserId, description);
                if (!validationResult.Success)
                {
                    return UserReportResult.ValidationError(validationResult.ErrorMessage!);
                }

                // Get user data and check if already reported in single query
                var reportData = await GetReportValidationDataAsync(reporterId, reportedUserId);
                if (!reportData.IsValid)
                {
                    return reportData.ErrorResult!;
                }

                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                try
                {
                    // Create report record
                    var userReport = new UserReport
                    {
                        ReporterId = reporterId,
                        ReportedId = reportedUserId,
                        Reason = reason,
                        Description = description,
                        Status = "pending",
                        CreatedAt = DateTime.UtcNow
                    };

                    _dbContext.UserReports.Add(userReport);

                    // Remove any existing matches/conversations between users
                    var removalResult = await RemoveUserInteractionsAsync(reporterId, reportedUserId);

                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Invalidate relevant caches (fire and forget for performance)
                    _ = Task.Run(async () => await InvalidateReportingCaches(reporterId, reportedUserId));

                    _logger.LogInformation("User {ReporterId} successfully reported user {ReportedUserId} for {Reason}",
                        reporterId, reportedUserId, reason);

                    return UserReportResult.Successful(
                        userReport.Id,
                        reportedUserId,
                        reason,
                        description,
                        removalResult.MatchRemoved,
                        removalResult.ConversationDeleted
                    );
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reporting user {ReportedUserId} by user {ReporterId}",
                    reportedUserId, reporterId);
                return UserReportResult.Error("Unable to submit report. Please try again.");
            }
        }

        /// <summary>
        /// Blocks a user and removes all interactions (optimized for immediate blocking)
        /// </summary>
        public async Task<BlockUserResult> BlockUserAsync(Guid blockerId, Guid blockedUserId)
        {
            try
            {
                _logger.LogInformation("User {BlockerId} attempting to block user {BlockedUserId}",
                    blockerId, blockedUserId);

                // Validate inputs
                if (blockerId == blockedUserId)
                {
                    return BlockUserResult.CannotBlockSelf();
                }

                // Check if already blocked and validate users exist
                var blockData = await GetBlockValidationDataAsync(blockerId, blockedUserId);
                if (!blockData.IsValid)
                {
                    return blockData.ErrorResult!;
                }

                using var transaction = await _dbContext.Database.BeginTransactionAsync();
                try
                {
                    // Create block report (using Block reason)
                    var blockReport = new UserReport
                    {
                        ReporterId = blockerId,
                        ReportedId = blockedUserId,
                        Reason = ReportReason.Block,
                        Description = "User blocked",
                        Status = "auto-resolved",
                        CreatedAt = DateTime.UtcNow,
                        ResolvedAt = DateTime.UtcNow
                    };

                    _dbContext.UserReports.Add(blockReport);

                    // Remove all interactions between users
                    var removalResult = await RemoveUserInteractionsAsync(blockerId, blockedUserId);

                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // Invalidate caches (fire and forget)
                    _ = Task.Run(async () => await InvalidateBlockingCaches(blockerId, blockedUserId));

                    _logger.LogInformation("User {BlockerId} successfully blocked user {BlockedUserId}",
                        blockerId, blockedUserId);

                    return BlockUserResult.Successful(
                        blockedUserId,
                        removalResult.MatchRemoved,
                        removalResult.ConversationDeleted,
                        removalResult.MessagesDeleted
                    );
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error blocking user {BlockedUserId} by user {BlockerId}",
                    blockedUserId, blockerId);
                return BlockUserResult.Error("Unable to block user. Please try again.");
            }
        }

        /// <summary>
        /// Gets user's submitted reports with caching
        /// </summary>
        public async Task<UserReportsListResult> GetUserReportsAsync(Guid userId, int page = 1, int pageSize = 20)
        {
            try
            {
                _logger.LogInformation("Getting reports for user {UserId}, page {Page}", userId, page);

                var cacheKey = $"user_reports:{userId}:page:{page}:size:{pageSize}";

                var reportsData = await _cacheService.GetOrSetAsync(
                    cacheKey,
                    async () => await FetchUserReportsFromDatabase(userId, page, pageSize),
                    CacheKeys.Duration.UserProfile
                );

                if (reportsData == null)
                {
                    reportsData = new UserReportsListData();
                }

                return UserReportsListResult.Successful(
                    reportsData.Reports,
                    reportsData.TotalCount,
                    reportsData.PendingCount,
                    reportsData.HasMore
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting reports for user {UserId}", userId);
                return UserReportsListResult.Error("Unable to retrieve reports. Please try again.");
            }
        }

        /// <summary>
        /// Gets user's blocked users list with caching
        /// </summary>
        public async Task<BlockedUsersListResult> GetBlockedUsersAsync(Guid userId, int page = 1, int pageSize = 50)
        {
            try
            {
                _logger.LogInformation("Getting blocked users for user {UserId}, page {Page}", userId, page);

                var cacheKey = CacheKeys.Safety.BlockedUsers(userId);

                var blockedData = await _cacheService.GetOrSetAsync(
                    cacheKey,
                    async () => await FetchBlockedUsersFromDatabase(userId, page, pageSize),
                    CacheKeys.Duration.BlockedUsers
                );

                if (blockedData == null)
                {
                    blockedData = new BlockedUsersListData();
                }

                return BlockedUsersListResult.Successful(
                    blockedData.BlockedUsers,
                    blockedData.TotalCount,
                    blockedData.HasMore
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting blocked users for user {UserId}", userId);
                return BlockedUsersListResult.Error("Unable to retrieve blocked users. Please try again.");
            }
        }

        /// <summary>
        /// Unblocks a user by removing the block report
        /// </summary>
        public async Task<UnblockUserResult> UnblockUserAsync(Guid blockerId, Guid blockedUserId)
        {
            try
            {
                _logger.LogInformation("User {BlockerId} attempting to unblock user {BlockedUserId}",
                    blockerId, blockedUserId);

                // Find the block report
                var blockReport = await _dbContext.UserReports
                    .FirstOrDefaultAsync(ur => ur.ReporterId == blockerId &&
                                              ur.ReportedId == blockedUserId &&
                                              ur.Reason == ReportReason.Block);

                if (blockReport == null)
                {
                    return UnblockUserResult.NotBlocked();
                }

                // Remove the block report
                _dbContext.UserReports.Remove(blockReport);
                await _dbContext.SaveChangesAsync();

                // Invalidate caches
                await InvalidateBlockingCaches(blockerId, blockedUserId);

                _logger.LogInformation("User {BlockerId} successfully unblocked user {BlockedUserId}",
                    blockerId, blockedUserId);

                return UnblockUserResult.Successful(blockedUserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unblocking user {BlockedUserId} by user {BlockerId}",
                    blockedUserId, blockerId);
                return UnblockUserResult.Error("Unable to unblock user. Please try again.");
            }
        }

        /// <summary>
        /// Checks if a user is blocked by another user (with caching)
        /// </summary>
        public async Task<bool> IsUserBlockedAsync(Guid userId, Guid potentialBlockedUserId)
        {
            try
            {
                var cacheKey = CacheKeys.Safety.BlockStatus(userId, potentialBlockedUserId);

                return await _cacheService.GetOrSetAsync(
                    cacheKey,
                    async () => await _dbContext.UserReports
                        .AnyAsync(ur => ur.ReporterId == userId &&
                                       ur.ReportedId == potentialBlockedUserId &&
                                       ur.Reason == ReportReason.Block),
                    CacheKeys.Duration.BlockedUsers
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {UserId} blocked {BlockedUserId}",
                    userId, potentialBlockedUserId);
                return false; // Assume not blocked on error to avoid breaking app flow
            }
        }

        #region Private Helper Methods

        /// <summary>
        /// Validates report request inputs
        /// </summary>
        private static OperationResult ValidateReportRequest(Guid reporterId, Guid reportedUserId, string? description)
        {
            if (reporterId == reportedUserId)
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
        private async Task<ReportValidationData> GetReportValidationDataAsync(Guid reporterId, Guid reportedUserId)
        {
            var validationQuery = await _dbContext.Users
                .Where(u => u.Id == reportedUserId && u.IsActive)
                .Select(u => new
                {
                    UserExists = true,
                    AlreadyReported = _dbContext.UserReports
                        .Any(ur => ur.ReporterId == reporterId && ur.ReportedId == reportedUserId)
                })
                .FirstOrDefaultAsync();

            if (validationQuery?.UserExists != true)
            {
                return ReportValidationData.Invalid(UserReportResult.UserNotFound());
            }

            if (validationQuery.AlreadyReported)
            {
                return ReportValidationData.Invalid(UserReportResult.AlreadyReported());
            }

            return ReportValidationData.Valid();
        }

        /// <summary>
        /// Gets validation data for blocking in a single optimized query
        /// </summary>
        private async Task<BlockValidationData> GetBlockValidationDataAsync(Guid blockerId, Guid blockedUserId)
        {
            var validationQuery = await _dbContext.Users
                .Where(u => u.Id == blockedUserId && u.IsActive)
                .Select(u => new
                {
                    UserExists = true,
                    AlreadyBlocked = _dbContext.UserReports
                        .Any(ur => ur.ReporterId == blockerId &&
                                  ur.ReportedId == blockedUserId &&
                                  ur.Reason == ReportReason.Block)
                })
                .FirstOrDefaultAsync();

            if (validationQuery?.UserExists != true)
            {
                return BlockValidationData.Invalid(BlockUserResult.UserNotFound());
            }

            if (validationQuery.AlreadyBlocked)
            {
                return BlockValidationData.Invalid(BlockUserResult.AlreadyBlocked());
            }

            return BlockValidationData.Valid();
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
        /// Fetches user reports from database with pagination
        /// </summary>
        private async Task<UserReportsListData> FetchUserReportsFromDatabase(Guid userId, int page, int pageSize)
        {
            var skip = (page - 1) * pageSize;

            var totalCount = await _dbContext.UserReports
                .CountAsync(ur => ur.ReporterId == userId);

            var pendingCount = await _dbContext.UserReports
                .CountAsync(ur => ur.ReporterId == userId && ur.Status == "pending");

            var reports = await _dbContext.UserReports
                .Where(ur => ur.ReporterId == userId)
                .Include(ur => ur.Reported)
                .ThenInclude(u => u.Photos.Where(p => p.IsPrimary))
                .OrderByDescending(ur => ur.CreatedAt)
                .Skip(skip)
                .Take(pageSize)
                .Select(ur => new UserReportDTO
                {
                    Id = ur.Id,
                    ReportedUserId = ur.ReportedId,
                    ReportedUserName = ur.Reported.FirstName + (ur.Reported.LastName != null ? " " + ur.Reported.LastName : ""),
                    PrimaryPhoto = ur.Reported.Photos.FirstOrDefault(p => p.IsPrimary) != null
                        ? ur.Reported.Photos.First(p => p.IsPrimary).Url
                        : null,
                    Reason = ur.Reason,
                    Description = ur.Description,
                    Status = ur.Status,
                    ReportedAt = ur.CreatedAt,
                    ResolvedAt = ur.ResolvedAt
                })
                .ToListAsync();

            return new UserReportsListData
            {
                Reports = reports,
                TotalCount = totalCount,
                PendingCount = pendingCount,
                HasMore = skip + pageSize < totalCount,
                LastUpdated = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Fetches blocked users from database with pagination
        /// </summary>
        private async Task<BlockedUsersListData> FetchBlockedUsersFromDatabase(Guid userId, int page, int pageSize)
        {
            var skip = (page - 1) * pageSize;

            var totalCount = await _dbContext.UserReports
                .CountAsync(ur => ur.ReporterId == userId && ur.Reason == ReportReason.Block);

            var blockedUsers = await _dbContext.UserReports
                .Where(ur => ur.ReporterId == userId && ur.Reason == ReportReason.Block)
                .Include(ur => ur.Reported)
                .ThenInclude(u => u.Photos.Where(p => p.IsPrimary))
                .OrderByDescending(ur => ur.CreatedAt)
                .Skip(skip)
                .Take(pageSize)
                .Select(ur => new BlockedUserDTO
                {
                    UserId = ur.ReportedId,
                    FirstName = ur.Reported.FirstName,
                    LastName = ur.Reported.LastName,
                    PrimaryPhotoUrl = ur.Reported.Photos.FirstOrDefault(p => p.IsPrimary) != null
                        ? ur.Reported.Photos.First(p => p.IsPrimary).Url
                        : null,
                    ThumbnailUrl = ur.Reported.Photos.FirstOrDefault(p => p.IsPrimary) != null
                        ? ur.Reported.Photos.First(p => p.IsPrimary).ThumbnailUrl
                        : null,
                    BlockedAt = ur.CreatedAt,
                    IsActive = ur.Reported.IsActive
                })
                .ToListAsync();

            return new BlockedUsersListData
            {
                BlockedUsers = blockedUsers,
                TotalCount = totalCount,
                HasMore = skip + pageSize < totalCount,
                LastUpdated = DateTime.UtcNow
            };
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
                    CacheKeys.Safety.UserReports(reporterId),
                    CacheKeys.Matching.UserMatches(reporterId),
                    CacheKeys.Matching.UserMatches(reportedUserId),
                    CacheKeys.Messaging.UserConversations(reporterId),
                    CacheKeys.Messaging.UserConversations(reportedUserId),
                    CacheKeys.Safety.BlockStatus(reporterId, reportedUserId),
                    CacheKeys.Discovery.GlobalResults(reporterId),
                    CacheKeys.Discovery.LocalResults(reporterId)
                };

                // Add discovery cache invalidation
                //for (int count = 5; count <= 30; count += 5)
                //{
                //    keysToInvalidate.Add(CacheKeys.Discovery.GlobalResults(reporterId, count));
                //    keysToInvalidate.Add(CacheKeys.Discovery.LocalResults(reporterId, count));
                //}

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

        /// <summary>
        /// Invalidates blocking-related caches
        /// </summary>
        private async Task InvalidateBlockingCaches(Guid blockerId, Guid blockedUserId)
        {
            try
            {
                var keysToInvalidate = new List<string>
                {
                    CacheKeys.Safety.BlockedUsers(blockerId),
                    CacheKeys.Safety.BlockStatus(blockerId, blockedUserId),
                    CacheKeys.Matching.UserMatches(blockerId),
                    CacheKeys.Matching.UserMatches(blockedUserId),
                    CacheKeys.Messaging.UserConversations(blockerId),
                    CacheKeys.Messaging.UserConversations(blockedUserId),
                    CacheKeys.Discovery.GlobalResults(blockerId),
                    CacheKeys.Discovery.GlobalResults(blockedUserId),
                    CacheKeys.Discovery.LocalResults(blockerId),
                    CacheKeys.Discovery.LocalResults(blockedUserId),
                    $"user_reports:{blockerId}:page:1:size:20" // Clear first page of reports
                };

                // Add discovery cache invalidation for both users
                //foreach (var userId in new[] { blockerId, blockedUserId })
                //{
                //    for (int count = 5; count <= 30; count += 5)
                //    {
                //        keysToInvalidate.Add(CacheKeys.Discovery.GlobalResults(userId, count));
                //        keysToInvalidate.Add(CacheKeys.Discovery.LocalResults(userId, count));
                //    }
                //}

                await _cacheService.RemoveManyAsync(keysToInvalidate);
                _logger.LogDebug("Invalidated {Count} cache keys for blocking between users {BlockerId} and {BlockedUserId}",
                    keysToInvalidate.Count, blockerId, blockedUserId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate blocking caches for users {BlockerId} and {BlockedUserId}",
                    blockerId, blockedUserId);
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
            public UserReportResult? ErrorResult { get; set; }

            public static ReportValidationData Valid() => new() { IsValid = true };
            public static ReportValidationData Invalid(UserReportResult errorResult) =>
                new() { IsValid = false, ErrorResult = errorResult };
        }

        /// <summary>
        /// Helper class for block validation results
        /// </summary>
        private class BlockValidationData
        {
            public bool IsValid { get; set; }
            public BlockUserResult? ErrorResult { get; set; }

            public static BlockValidationData Valid() => new() { IsValid = true };
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