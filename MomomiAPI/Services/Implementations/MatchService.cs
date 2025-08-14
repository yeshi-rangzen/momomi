using Microsoft.EntityFrameworkCore;
using MomomiAPI.Common.Caching;
using MomomiAPI.Common.Results;
using MomomiAPI.Data;
using MomomiAPI.Models.Enums;
using MomomiAPI.Services.Interfaces;
using static MomomiAPI.Common.Constants.AppConstants;

namespace MomomiAPI.Services.Implementations
{
    public class MatchService : IMatchService
    {
        private readonly MomomiDbContext _dbContext;
        private readonly ICacheService _cacheService;
        private readonly ICacheInvalidation _cacheInvalidation;
        private readonly ILogger<MatchService> _logger;

        public MatchService(
            MomomiDbContext dbContext,
            ICacheService cacheService,
            ICacheInvalidation cacheInvalidation,
            ILogger<MatchService> logger)
        {
            _dbContext = dbContext;
            _cacheService = cacheService;
            _cacheInvalidation = cacheInvalidation;
            _logger = logger;
        }

        public async Task<MatchResult> GetMatchConversations(Guid currentUserId)
        {
            try
            {
                _logger.LogInformation("Retrieving matches for user {UserId}", currentUserId);

                // Check cache first
                var cacheKey = CacheKeys.Matching.UserMatches(currentUserId);
                var cachedMatches = await _cacheService.GetAsync<List<MatchConversationData>>(cacheKey);

                if (cachedMatches != null)
                {
                    _logger.LogInformation("Cache hit for user {UserId}", currentUserId);
                    return MatchResult.FoundMatches(cachedMatches, true);
                }

                // Single optimized query with better projections to get matches
                var matchConvList = await GetUserMatchesFromDatabase(currentUserId);

                // Cache the results
                await _cacheService.SetAsync(cacheKey, matchConvList, CacheKeys.Duration.UserMatches);
                _logger.LogInformation("Retrieved {MatchCount} matches for user {UserId}",
                    matchConvList.Count, currentUserId);

                return MatchResult.FoundMatches(matchConvList, false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving matches for user {UserId}", currentUserId);
                return MatchResult.Error("An error occurred while retrieving matches.");
            }
        }


        public async Task<OperationResult> RemoveMatchConversation(Guid currentUserId, Guid targetUserId, SwipeType removedSwipeType = SwipeType.Unmatched)
        {
            try
            {
                _logger.LogInformation("Removing match conversation for user {UserId} with matched user {MatchedUserId}", currentUserId, targetUserId);

                var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

                return await executionStrategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _dbContext.Database.BeginTransactionAsync();
                    try
                    {
                        // Update the SwipeType in the UserSwipes table
                        var swipeRecord = await _dbContext.UserSwipes
                            .FirstOrDefaultAsync(us => (
                                (us.SwiperUserId == currentUserId && us.SwipedUserId == targetUserId) ||
                                (us.SwiperUserId == targetUserId && us.SwipedUserId == currentUserId))
                            );

                        if (swipeRecord != null)
                        {
                            swipeRecord.SwipeType = removedSwipeType;
                            swipeRecord.UpdatedAt = DateTime.UtcNow;
                            _dbContext.UserSwipes.Update(swipeRecord);
                        }

                        // Remove the converation and its messages
                        var conversation = await _dbContext.Conversations
                           .Include(c => c.Messages)
                           .FirstOrDefaultAsync(c =>
                               (c.User1Id == currentUserId && c.User2Id == targetUserId) ||
                               (c.User1Id == targetUserId && c.User2Id == currentUserId));

                        if (conversation != null)
                        {
                            // Delete all messages first
                            if (conversation.Messages.Count > 0)
                            {
                                _dbContext.Messages.RemoveRange(conversation.Messages);
                            }

                            // Then delete the conversation itself
                            _dbContext.Conversations.Remove(conversation);
                        }
                        await _dbContext.SaveChangesAsync();
                        await transaction.CommitAsync();

                        // Clear relevant caches
                        await _cacheInvalidation.InvalidateMatchingCaches(currentUserId, targetUserId);
                        await _cacheInvalidation.InvalidateUserConversations(currentUserId);
                        await _cacheInvalidation.InvalidateUserConversations(targetUserId);
                        if (conversation != null)
                        {
                            await _cacheInvalidation.InvalidateConversationCache(conversation.Id);
                        }

                        return OperationResult.Successful(new Dictionary<string, object>
                        {
                            { "removed_at", DateTime.UtcNow },
                            { "conversation_deleted", conversation != null }
                        });
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
                _logger.LogError(ex, "Error removing match conversation for user {UserId} with matched user {MatchedUserId}", currentUserId, targetUserId);
                return OperationResult.Failed(ErrorCodes.INTERNAL_SERVER_ERROR, "Unable to remove match. Please try again.");
            }
        }

        #region Private Helper Methods
        // Single query with proper joins and minimal data transfer
        private async Task<List<MatchConversationData>> GetUserMatchesFromDatabase(Guid currentUserId)
        {
            return await _dbContext.Conversations
                .Where(conv => conv.User1Id == currentUserId || conv.User2Id == currentUserId)
                .Select(conv => new MatchConversationData
                {
                    ConversationId = conv.Id,
                    MatchedAt = conv.CreatedAt,

                    OtherUserId = conv.User1Id == currentUserId ? conv.User2Id : conv.User1Id,
                    IsOtherUserActive = conv.User1Id == currentUserId ? conv.User2.IsActive : conv.User1.IsActive,
                    PrimaryPhotoUrl = (conv.User1Id == currentUserId ? conv.User2 : conv.User1)
                        .Photos.Where(p => p.IsPrimary)
                        .Select(p => p.Url)
                        .FirstOrDefault(),

                    LastMessage = conv.Messages
                    .OrderByDescending(m => m.SentAt)
                            .Select(m => new LastMessageData
                            {
                                Id = m.Id,
                                SenderId = m.SenderId,
                                Content = m.Content,
                                MessageType = m.MessageType,
                                IsRead = m.IsRead,
                                SentAt = m.SentAt
                            })
                            .FirstOrDefault(),

                    UnreadCount = conv.Messages.Count(m => m.SenderId != currentUserId && !m.IsRead),

                    IsFromSuperLike = _dbContext.UserSwipes.Any(swipe =>
                                               ((swipe.SwiperUserId == currentUserId && swipe.SwipedUserId == (conv.User1Id == currentUserId ? conv.User2Id : conv.User1Id)) ||
                                                (swipe.SwiperUserId == (conv.User1Id == currentUserId ? conv.User2Id : conv.User1Id) && swipe.SwipedUserId == currentUserId)) &&
                                               swipe.SwipeType == SwipeType.SuperLike)
                })
                .Where(match => match.IsOtherUserActive == true)
                .OrderByDescending(match => match.LastMessage != null ? match.LastMessage.SentAt : match.MatchedAt)
                .ToListAsync();
        }
        #endregion
    }
}
