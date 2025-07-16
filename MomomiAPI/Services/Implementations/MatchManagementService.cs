using Microsoft.EntityFrameworkCore;
using MomomiAPI.Common.Caching;
using MomomiAPI.Common.Results;
using MomomiAPI.Data;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Entities;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Services.Implementations
{
    public class MatchManagementService : IMatchManagementService
    {
        private readonly MomomiDbContext _dbContext;
        private readonly ICacheService _cacheService;
        private readonly ICacheInvalidation _cacheInvalidation;
        private readonly ILogger<MatchManagementService> _logger;

        public MatchManagementService(
            MomomiDbContext dbContext,
            ICacheService cacheService,
            ICacheInvalidation cacheInvalidation,
            ILogger<MatchManagementService> logger)
        {
            _dbContext = dbContext;
            _cacheService = cacheService;
            _cacheInvalidation = cacheInvalidation;
            _logger = logger;
        }

        public async Task<OperationResult<List<MatchDTO>>> GetUserMatches(Guid userId)
        {
            try
            {
                _logger.LogInformation("Retrieving matches for user {UserId}", userId);

                var cacheKey = CacheKeys.Matching.UserMatches(userId);
                var cachedMatches = await _cacheService.GetAsync<List<MatchDTO>>(cacheKey);

                if (cachedMatches != null)
                {
                    _logger.LogDebug("Returning cached matches for user {UserId}", userId);
                    return OperationResult<List<MatchDTO>>.Successful(cachedMatches);
                }

                // Get all matches where user is involved and both users liked each other
                var matches = await _dbContext.UserLikes
                    .Where(ul => (ul.LikerUserId == userId || ul.LikedUserId == userId) &&
                                 ul.IsMatch && ul.IsLike)
                    .Include(ul => ul.LikerUser)
                        .ThenInclude(u => u.Photos)
                    .Include(ul => ul.LikedUser)
                        .ThenInclude(u => u.Photos)
                    .OrderByDescending(ul => ul.CreatedAt)
                    .ToListAsync();

                var matchDtos = new List<MatchDTO>();

                foreach (var match in matches)
                {
                    // Determine the other user (not the current user)
                    var otherUser = match.LikerUserId == userId ? match.LikedUser : match.LikerUser;

                    // Skip if other user is inactive
                    if (!otherUser.IsActive)
                        continue;

                    // Get conversation details for last message and unread count
                    var conversation = await _dbContext.Conversations
                        .Include(c => c.Messages)
                        .FirstOrDefaultAsync(c =>
                            (c.User1Id == userId && c.User2Id == otherUser.Id) ||
                            (c.User1Id == otherUser.Id && c.User2Id == userId));

                    MessageDTO? lastMessage = null;
                    var unreadCount = 0;

                    if (conversation != null)
                    {
                        var lastMsg = conversation.Messages
                            .OrderByDescending(m => m.SentAt)
                            .FirstOrDefault();

                        if (lastMsg != null)
                        {
                            lastMessage = new MessageDTO
                            {
                                Id = lastMsg.Id,
                                ConversationId = lastMsg.ConversationId,
                                SenderId = lastMsg.SenderId,
                                SenderName = lastMsg.SenderId == userId ? "You" : otherUser.FirstName ?? "",
                                Content = lastMsg.Content,
                                MessageType = lastMsg.MessageType,
                                IsRead = lastMsg.IsRead,
                                SentAt = lastMsg.SentAt
                            };
                        }

                        // Count unread messages from the other user
                        unreadCount = conversation.Messages
                            .Count(m => m.SenderId != userId && !m.IsRead);
                    }

                    // Check if this was a super like
                    var isFromSuperLike = match.LikeType == Models.Enums.LikeType.SuperLike ||
                                         await _dbContext.UserLikes
                                             .AnyAsync(ul =>
                                                 ((ul.LikerUserId == userId && ul.LikedUserId == otherUser.Id) ||
                                                  (ul.LikerUserId == otherUser.Id && ul.LikedUserId == userId)) &&
                                                 ul.LikeType == Models.Enums.LikeType.SuperLike);

                    var matchDto = new MatchDTO
                    {
                        MatchId = match.Id,
                        UserId = otherUser.Id,
                        FirstName = otherUser.FirstName,
                        LastName = otherUser.LastName,
                        Age = otherUser.DateOfBirth.HasValue ?
                            DateTime.UtcNow.Year - otherUser.DateOfBirth.Value.Year : 0,
                        PrimaryPhoto = otherUser.Photos
                            .FirstOrDefault(p => p.IsPrimary)?.Url ??
                            otherUser.Photos
                            .OrderBy(p => p.PhotoOrder)
                            .FirstOrDefault()?.Url,
                        Heritage = otherUser.Heritage,
                        MatchedAt = match.CreatedAt,
                        LastMessage = lastMessage,
                        UnreadCount = unreadCount,
                        IsFromSuperLike = isFromSuperLike
                    };

                    matchDtos.Add(matchDto);
                }

                // Remove duplicates based on UserId (in case there are multiple match records)
                var uniqueMatches = matchDtos
                    .GroupBy(m => m.UserId)
                    .Select(g => g.OrderByDescending(m => m.MatchedAt).First())
                    .OrderByDescending(m => m.LastMessage?.SentAt ?? m.MatchedAt)
                    .ToList();

                // Cache for 15 minutes
                await _cacheService.SetAsync(cacheKey, uniqueMatches, CacheKeys.Duration.UserMatches);

                _logger.LogInformation("Retrieved {Count} matches for user {UserId}", uniqueMatches.Count, userId);
                return OperationResult<List<MatchDTO>>.Successful(uniqueMatches);
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
    }
}