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
            // First, get the base conversation data with related entities
            var conversations = await _dbContext.Conversations
                .Where(conv => conv.User1Id == currentUserId || conv.User2Id == currentUserId)
                .Include(conv => conv.User1)
                    .ThenInclude(u => u.Photos.Where(p => p.IsPrimary))
                .Include(conv => conv.User2)
                    .ThenInclude(u => u.Photos.Where(p => p.IsPrimary))
                .Include(conv => conv.Messages.OrderByDescending(m => m.SentAt).Take(1))
                .Where(conv => (conv.User1Id == currentUserId ? conv.User2.IsActive : conv.User1.IsActive))
                .ToListAsync();

            // Get unread message counts for all conversations in a single query
            var conversationIds = conversations.Select(c => c.Id).ToList();
            var unreadCounts = await _dbContext.Messages
                .Where(m => conversationIds.Contains(m.ConversationId) &&
                           m.SenderId != currentUserId &&
                           !m.IsRead)
                .GroupBy(m => m.ConversationId)
                .Select(g => new { ConversationId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.ConversationId, x => x.Count);

            // Get super like information for all user pairs in a single query
            var otherUserIds = conversations.Select(conv =>
                conv.User1Id == currentUserId ? conv.User2Id : conv.User1Id).ToList();

            var superLikes = await _dbContext.UserSwipes
                .Where(swipe =>
                    ((swipe.SwiperUserId == currentUserId && otherUserIds.Contains(swipe.SwipedUserId)) ||
                     (otherUserIds.Contains(swipe.SwiperUserId) && swipe.SwipedUserId == currentUserId)) &&
                    swipe.SwipeType == SwipeType.SuperLike)
                .Select(swipe => new { swipe.SwiperUserId, swipe.SwipedUserId })
                .ToListAsync();

            // Transform to MatchConversationData in memory
            var matchConversations = conversations.Select(conv =>
            {
                var otherUser = conv.User1Id == currentUserId ? conv.User2 : conv.User1;
                var otherUserId = conv.User1Id == currentUserId ? conv.User2Id : conv.User1Id;
                var lastMessage = conv.Messages.FirstOrDefault();

                return new MatchConversationData
                {
                    ConversationId = conv.Id,
                    MatchedAt = conv.CreatedAt,
                    OtherUserId = otherUserId,
                    IsOtherUserActive = otherUser.IsActive,
                    PrimaryPhotoUrl = otherUser.Photos.FirstOrDefault()?.Url,
                    LastMessage = lastMessage != null ? new LastMessageData
                    {
                        Id = lastMessage.Id,
                        SenderId = lastMessage.SenderId,
                        Content = lastMessage.Content,
                        MessageType = lastMessage.MessageType,
                        IsRead = lastMessage.IsRead,
                        SentAt = lastMessage.SentAt
                    } : null,
                    UnreadCount = unreadCounts.GetValueOrDefault(conv.Id, 0),
                    IsFromSuperLike = superLikes.Any(sl =>
                        (sl.SwiperUserId == currentUserId && sl.SwipedUserId == otherUserId) ||
                        (sl.SwiperUserId == otherUserId && sl.SwipedUserId == currentUserId))
                };
            })
            .OrderByDescending(match => match.LastMessage?.SentAt ?? match.MatchedAt)
            .ToList();

            return matchConversations;
        }
        #endregion
    }
}
