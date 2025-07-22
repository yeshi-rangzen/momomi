using Microsoft.EntityFrameworkCore;
using MomomiAPI.Common.Caching;
using MomomiAPI.Common.Results;
using MomomiAPI.Data;
using MomomiAPI.Helpers;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Entities;
using MomomiAPI.Models.Enums;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Services.Implementations
{
    public class MatchManagementService : IMatchManagementService
    {
        private readonly MomomiDbContext _dbContext;
        private readonly ICacheService _cacheService;
        private readonly ICacheInvalidation _cacheInvalidation;
        private readonly IAnalyticsService _analyticsService;
        private readonly ILogger<MatchManagementService> _logger;

        public MatchManagementService(
            MomomiDbContext dbContext,
            ICacheService cacheService,
            ICacheInvalidation cacheInvalidation,
            IAnalyticsService analyticsService,
            ILogger<MatchManagementService> logger)
        {
            _dbContext = dbContext;
            _cacheService = cacheService;
            _cacheInvalidation = cacheInvalidation;
            _analyticsService = analyticsService;
            _logger = logger;
        }

        public async Task<OperationResult<List<MatchDTO>>> GetUserMatches(Guid userId)
        {
            try
            {
                _logger.LogInformation("Retrieving matches for user {UserId}", userId);

                var cacheKey = CacheKeys.Matching.UserMatches(userId);
                var cachedMatches = await _cacheService.GetAsync<List<MatchDTO>>(cacheKey);
                if (cachedMatches != null) return OperationResult<List<MatchDTO>>.Successful(cachedMatches);

                // Single optimized query with projections
                var matches = await _dbContext.UserLikes
                    .Where(ul => (ul.LikerUserId == userId || ul.LikedUserId == userId) && ul.IsMatch && ul.IsLike)
                    .Select(ul => new
                    {
                        Match = ul,
                        OtherUser = ul.LikerUserId == userId ? ul.LikedUser : ul.LikerUser,
                        PrimaryPhoto = (ul.LikerUserId == userId ? ul.LikedUser : ul.LikerUser)
                            .Photos.Where(p => p.IsPrimary).Select(p => p.Url).FirstOrDefault(),
                        Conversation = _dbContext.Conversations
                            .Where(c => (c.User1Id == userId && c.User2Id == (ul.LikerUserId == userId ? ul.LikedUserId : ul.LikerUserId)) ||
                                       (c.User1Id == (ul.LikerUserId == userId ? ul.LikedUserId : ul.LikerUserId) && c.User2Id == userId))
                            .Select(c => new
                            {
                                Id = c.Id,
                                LastMessage = c.Messages.OrderByDescending(m => m.SentAt).FirstOrDefault(),
                                UnreadCount = c.Messages.Count(m => m.SenderId != userId && !m.IsRead)
                            }).FirstOrDefault()
                    })
                    .OrderByDescending(x => x.Match.CreatedAt)
                    .ToListAsync();

                var matchDtos = matches.Where(m => m.OtherUser.IsActive).Select(m => new MatchDTO
                {
                    MatchId = m.Match.Id,
                    UserId = m.OtherUser.Id,
                    FirstName = m.OtherUser.FirstName,
                    LastName = m.OtherUser.LastName,
                    Age = m.OtherUser.DateOfBirth.HasValue ? DateTime.UtcNow.Year - m.OtherUser.DateOfBirth.Value.Year : 0,
                    PrimaryPhoto = m.PrimaryPhoto,
                    Heritage = m.OtherUser.Heritage,
                    MatchedAt = m.Match.CreatedAt,
                    LastMessage = m.Conversation?.LastMessage != null ? new MessageDTO
                    {
                        Id = m.Conversation.LastMessage.Id,
                        ConversationId = m.Conversation.LastMessage.ConversationId,
                        SenderId = m.Conversation.LastMessage.SenderId,
                        SenderName = m.Conversation.LastMessage.SenderId == userId ? "You" : m.OtherUser.FirstName ?? "",
                        Content = m.Conversation.LastMessage.Content,
                        MessageType = m.Conversation.LastMessage.MessageType,
                        IsRead = m.Conversation.LastMessage.IsRead,
                        SentAt = m.Conversation.LastMessage.SentAt
                    } : null,
                    UnreadCount = m.Conversation?.UnreadCount ?? 0,
                    IsFromSuperLike = m.Match.LikeType == LikeType.SuperLike
                }).ToList();

                await _cacheService.SetAsync(cacheKey, matchDtos, CacheKeys.Duration.UserMatches);
                return OperationResult<List<MatchDTO>>.Successful(matchDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving matches for user {UserId}", userId);
                return OperationResult<List<MatchDTO>>.Failed("Unable to retrieve matches. Please try again.");
            }
        }

        public async Task<OperationResult> RemoveMatch(Guid userId, Guid matchedUserId)
        {
            try
            {
                _logger.LogInformation("Removing match between user {UserId} and {MatchedUserId}", userId, matchedUserId);

                // Use the execution strategy to handle retries and transactions
                var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

                return await executionStrategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _dbContext.Database.BeginTransactionAsync();
                    try
                    {
                        // Find and update like records to remove match status
                        var likes = await _dbContext.UserLikes
                            .Where(ul => ((ul.LikerUserId == userId && ul.LikedUserId == matchedUserId) ||
                                          (ul.LikerUserId == matchedUserId && ul.LikedUserId == userId)) &&
                                         ul.IsMatch)
                            .ToListAsync();

                        if (!likes.Any())
                        {
                            _logger.LogWarning("No match found between user {UserId} and {MatchedUserId}", userId, matchedUserId);
                            return OperationResult.NotFound("Match not found.");
                        }

                        // Remove match status
                        foreach (var like in likes)
                        {
                            like.IsMatch = false;
                            like.UpdatedAt = DateTime.UtcNow;
                        }

                        // Find and DELETE conversation and all messages
                        var conversation = await _dbContext.Conversations
                            .Include(c => c.Messages)
                            .FirstOrDefaultAsync(c =>
                                (c.User1Id == userId && c.User2Id == matchedUserId) ||
                                (c.User1Id == matchedUserId && c.User2Id == userId));

                        if (conversation != null)
                        {
                            // Delete all messages first (due to foreign key constraints)
                            if (conversation.Messages.Any())
                            {
                                _dbContext.Messages.RemoveRange(conversation.Messages);
                                _logger.LogDebug("Removing {Count} messages for conversation {ConversationId}",
                                    conversation.Messages.Count, conversation.Id);
                            }

                            // Delete the conversation
                            _dbContext.Conversations.Remove(conversation);
                            _logger.LogDebug("Removing conversation {ConversationId}", conversation.Id);
                        }

                        await _dbContext.SaveChangesAsync();
                        await transaction.CommitAsync();

                        // Clear relevant caches
                        await _cacheInvalidation.InvalidateMatchingCaches(userId, matchedUserId);
                        await _cacheInvalidation.InvalidateUserConversations(userId);
                        await _cacheInvalidation.InvalidateUserConversations(matchedUserId);

                        if (conversation != null)
                        {
                            await _cacheInvalidation.InvalidateConversationCache(conversation.Id);
                        }

                        _logger.LogInformation("Successfully removed match between user {UserId} and {MatchedUserId}",
                            userId, matchedUserId);

                        return OperationResult.Successful()
                            .WithMetadata("removed_at", DateTime.UtcNow)
                            .WithMetadata("conversation_deleted", conversation != null);
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
                _logger.LogError(ex, "Error removing match between user {UserId} and {MatchedUserId}", userId, matchedUserId);
                return OperationResult.Failed("Unable to remove match. Please try again.");
            }
        }

        public async Task<OperationResult<bool>> CheckForNewMatch(Guid user1Id, Guid user2Id)
        {
            try
            {
                _logger.LogDebug("Checking for potential match between user {User1Id} and {User2Id}", user1Id, user2Id);

                // Check if both users have liked each other
                var user1LikesUser2 = await _dbContext.UserLikes
                    .FirstOrDefaultAsync(ul => ul.LikerUserId == user1Id &&
                                               ul.LikedUserId == user2Id &&
                                               ul.IsLike);

                var user2LikesUser1 = await _dbContext.UserLikes
                    .FirstOrDefaultAsync(ul => ul.LikerUserId == user2Id &&
                                               ul.LikedUserId == user1Id &&
                                               ul.IsLike);

                var isMatch = user1LikesUser2 != null && user2LikesUser1 != null;

                if (isMatch && (!user1LikesUser2.IsMatch || !user2LikesUser1.IsMatch))
                {
                    // Update both like records to indicate match
                    user1LikesUser2.IsMatch = true;
                    user1LikesUser2.UpdatedAt = DateTime.UtcNow;

                    user2LikesUser1.IsMatch = true;
                    user2LikesUser1.UpdatedAt = DateTime.UtcNow;

                    // Create conversation if it doesn't exist
                    var existingConversation = await _dbContext.Conversations
                        .FirstOrDefaultAsync(c =>
                            (c.User1Id == user1Id && c.User2Id == user2Id) ||
                            (c.User1Id == user2Id && c.User2Id == user1Id));

                    if (existingConversation == null)
                    {
                        var conversation = new Conversation
                        {
                            User1Id = user1Id < user2Id ? user1Id : user2Id, // Ensure consistent ordering
                            User2Id = user1Id < user2Id ? user2Id : user1Id,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _dbContext.Conversations.Add(conversation);
                        _logger.LogDebug("Created new conversation between user {User1Id} and {User2Id}", user1Id, user2Id);
                    }

                    await _dbContext.SaveChangesAsync();

                    // Track match creation analytics
                    _ = Task.Run(async () =>
                    {
                        // Get user data for cultural analysis
                        var user1 = await _dbContext.Users.FindAsync(user1Id);
                        var user2 = await _dbContext.Users.FindAsync(user2Id);

                        if (user1 != null && user2 != null)
                        {
                            var analyticsData = new MatchData
                            {
                                MatchType = DetermineMatchType(user1LikesUser2, user2LikesUser1),
                                CulturalCompatibilityScore = CalculateCulturalCompatibility(user1, user2),
                                User1Heritage = user1.Heritage ?? new List<HeritageType>(),
                                User2Heritage = user2.Heritage ?? new List<HeritageType>(),
                                IsCrossCultural = IsCrossCultural(user1.Heritage, user2.Heritage),
                                MatchTimestamp = DateTime.UtcNow
                            };

                            await _analyticsService.TrackMatchCreatedAsync(user1Id, user2Id, analyticsData);
                        }
                    });

                    // Clear match caches for both users
                    await _cacheInvalidation.InvalidateMatchingCaches(user1Id, user2Id);

                    _logger.LogInformation("New match created between user {User1Id} and {User2Id}", user1Id, user2Id);
                }

                return OperationResult<bool>.Successful(isMatch);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for match between user {User1Id} and {User2Id}", user1Id, user2Id);
                return OperationResult<bool>.Failed("Unable to check for match. Please try again.");
            }
        }

        private static string DetermineMatchType(UserLike like1, UserLike like2)
        {
            if (like1.LikeType == LikeType.SuperLike || like2.LikeType == LikeType.SuperLike)
                return "super_like_match";
            return "regular_match";
        }

        private static double CalculateCulturalCompatibility(User user1, User user2)
        {
            // Use the existing CulturalCompatibility helper
            return CulturalCompatibility.CalculateCompatibilityScore(user1, user2);
        }

        private static bool IsCrossCultural(List<HeritageType>? heritage1, List<HeritageType>? heritage2)
        {
            if (heritage1 == null || heritage2 == null || !heritage1.Any() || !heritage2.Any())
                return false;

            return !heritage1.Intersect(heritage2).Any();
        }
    }
}