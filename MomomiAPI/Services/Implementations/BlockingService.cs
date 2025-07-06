using Microsoft.EntityFrameworkCore;
using MomomiAPI.Data;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Entities;
using MomomiAPI.Models.Enums;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Services.Implementations
{
    public class BlockingService : IBlockingService
    {
        private readonly MomomiDbContext _dbContext;
        private readonly ILogger<BlockingService> _logger;

        public BlockingService(MomomiDbContext dbContext, ILogger<BlockingService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<bool> BlockUserAsync(Guid blockerUserId, Guid blockedUserId, string? reason = null)
        {
            try
            {
                // Prevent self-blocking
                if (blockerUserId == blockedUserId)
                    return false;

                // Check if already blocked
                var existingBlock = await _dbContext.UserBlocks
                    .FirstOrDefaultAsync(ub => ub.BlockerUserId == blockerUserId && ub.BlockedUserId == blockedUserId);

                if (existingBlock != null)
                    return true; // Already blocked

                // Create block record
                var userBlock = new UserBlock
                {
                    BlockerUserId = blockerUserId,
                    BlockedUserId = blockedUserId,
                    Reason = reason,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.UserBlocks.Add(userBlock);

                // Remove any existing matches/conversations between users
                await RemoveMatchAndConversationAsync(blockerUserId, blockedUserId);

                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("User {BlockerUserId} blocked user {BlockedUserId}", blockerUserId, blockedUserId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error blocking user {BlockedUserId} by user {BlockerUserId}", blockedUserId, blockerUserId);
                return false;
            }
        }

        public async Task<bool> UnblockUserAsync(Guid blockerUserId, Guid blockedUserId)
        {
            try
            {
                var userBlock = await _dbContext.UserBlocks
                    .FirstOrDefaultAsync(ub => ub.BlockerUserId == blockerUserId && ub.BlockedUserId == blockedUserId);

                if (userBlock == null)
                    return true; // Not blocked

                _dbContext.UserBlocks.Remove(userBlock);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("User {BlockerUserId} unblocked user {BlockedUserId}", blockerUserId, blockedUserId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unblocking user {BlockedUserId} by user {BlockerUserId}", blockedUserId, blockerUserId);
                return false;
            }
        }

        public async Task<List<BlockedUserDTO>> GetBlockedUsersAsync(Guid userId)
        {
            try
            {
                var blockedUsers = await _dbContext.UserBlocks
                    .Where(ub => ub.BlockerUserId == userId)
                    .Include(ub => ub.BlockedUser)
                        .ThenInclude(u => u.Photos)
                    .Select(ub => new BlockedUserDTO
                    {
                        UserId = ub.BlockedUserId,
                        FirstName = ub.BlockedUser.FirstName,
                        LastName = ub.BlockedUser.LastName,
                        PrimaryPhoto = ub.BlockedUser.Photos.FirstOrDefault(p => p.IsPrimary) != null ?
                            ub.BlockedUser.Photos.First(p => p.IsPrimary).Url :
                            ub.BlockedUser.Photos.OrderBy(p => p.PhotoOrder).FirstOrDefault() != null ?
                            ub.BlockedUser.Photos.OrderBy(p => p.PhotoOrder).First().Url : null,
                        Reason = ub.Reason,
                        BlockedAt = ub.CreatedAt
                    })
                    .OrderByDescending(bu => bu.BlockedAt)
                    .ToListAsync();

                return blockedUsers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting blocked users for user {UserId}", userId);
                return [];
            }
        }

        public async Task<bool> IsUserBlockedAsync(Guid userId, Guid targetUserId)
        {
            try
            {
                // Check if either user has blocked the other
                var isBlocked = await _dbContext.UserBlocks
                    .AnyAsync(ub => (ub.BlockerUserId == userId && ub.BlockedUserId == targetUserId) ||
                                   (ub.BlockerUserId == targetUserId && ub.BlockedUserId == userId));

                return isBlocked;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {UserId} is blocked by {TargetUserId}", userId, targetUserId);
                return false;
            }
        }

        public async Task<bool> ReportUserAsync(Guid reporterId, Guid reportedUserId, ReportReason reason, string? description = null)
        {
            try
            {
                // Prevent self-reporting
                if (reporterId == reportedUserId)
                    return false;

                // Check if already reported
                var existingReport = await _dbContext.UserReports
                    .FirstOrDefaultAsync(ur => ur.ReporterId == reporterId && ur.ReportedId == reportedUserId);

                if (existingReport != null)
                    return true; // Already reported

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
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("User {ReporterId} reported user {ReportedUserId} for {Reason}",
                    reporterId, reportedUserId, reason);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reporting user {ReportedUserId} by user {ReporterId}", reportedUserId, reporterId);
                return false;
            }
        }

        public async Task<List<UserReportDTO>> GetUserReportsAsync(Guid userId)
        {
            try
            {
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

                return reports;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting reports for user {UserId}", userId);
                return [];
            }
        }

        public async Task<bool> IsUserReportedAsync(Guid reporterId, Guid reportedUserId)
        {
            try
            {
                var isReported = await _dbContext.UserReports
                    .AnyAsync(ur => ur.ReporterId == reporterId && ur.ReportedId == reportedUserId);

                return isReported;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {ReportedUserId} is reported by {ReporterId}", reportedUserId, reporterId);
                return false;
            }
        }

        private async Task RemoveMatchAndConversationAsync(Guid userId1, Guid userId2)
        {
            try
            {
                // Remove match status from likes
                var likes = await _dbContext.UserLikes
                    .Where(ul => (ul.LikerUserId == userId1 && ul.LikedUserId == userId2) ||
                               (ul.LikerUserId == userId2 && ul.LikedUserId == userId1))
                    .ToListAsync();

                foreach (var like in likes)
                {
                    like.IsMatch = false;
                }

                // DELETE conversation and messages instead of deactivating
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
                }

                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing match and conversation between {UserId1} and {UserId2}", userId1, userId2);
            }
        }
    }
}
