using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Common.Caching
{
    public class CacheInvalidationService : ICacheInvalidation
    {
        private readonly ICacheService _cacheService;
        private readonly ILogger<CacheInvalidationService> _logger;

        public CacheInvalidationService(ICacheService cacheService, ILogger<CacheInvalidationService> logger)
        {
            _cacheService = cacheService;
            _logger = logger;
        }

        public async Task InvalidateUserProfile(Guid userId)
        {
            var tasks = new[]
            {
                _cacheService.RemoveAsync(CacheKeys.Users.Profile(userId)),
                _cacheService.RemoveAsync(CacheKeys.Users.Photos(userId)),
                _cacheService.RemoveAsync(CacheKeys.Users.Preferences(userId)),
                _cacheService.RemoveAsync(CacheKeys.Users.SubscriptionStatus(userId))
            };

            await Task.WhenAll(tasks);
            _logger.LogDebug("Invalidate user profile cache for {UserId}", userId);
        }

        public async Task InvalidateUserDiscovery(Guid userId)
        {
            var tasks = new List<Task>();

            // Clear all discovery cache variations
            for (int count = 5; count <= 30; count += 5)
            {
                tasks.Add(_cacheService.RemoveAsync(CacheKeys.Discovery.GlobalResults(userId, count)));
                tasks.Add(_cacheService.RemoveAsync(CacheKeys.Discovery.LocalResults(userId, count)));
            }

            await Task.WhenAll(tasks);
            _logger.LogDebug("Invalidated discovery cache for {UserId}", userId);
        }

        public async Task InvalidateUserMatches(Guid userId)
        {
            await _cacheService.RemoveAsync(CacheKeys.Matching.UserMatches(userId));
            _logger.LogDebug("Invalidated matches cache for {UserId}", userId);
        }

        public async Task InvalidateUserConversations(Guid userId)
        {
            await _cacheService.RemoveAsync(CacheKeys.Messaging.UserConversations(userId));
            _logger.LogDebug("Invalidated conversations cache for {UserId}", userId);
        }

        public async Task InvalidateUserRelatedCaches(Guid userId)
        {
            await Task.WhenAll(
                InvalidateUserProfile(userId),
                InvalidateUserDiscovery(userId),
                InvalidateUserMatches(userId),
                InvalidateUserConversations(userId)
            );
        }

        public async Task InvalidateMatchingCaches(Guid user1Id, Guid user2Id)
        {
            await Task.WhenAll(
                InvalidateUserMatches(user1Id),
                InvalidateUserMatches(user2Id),
                _cacheService.RemoveAsync(CacheKeys.Matching.MatchCompatibility(user1Id, user2Id))
            );
        }

        public async Task InvalidateConversationCache(Guid conversationId)
        {
            var tasks = new List<Task>
            {
                _cacheService.RemoveAsync(CacheKeys.Messaging.ConversationDetails(conversationId))
            };

            // Clear paginated message cache
            for (int page = 1; page <= 10; page++)
            {
                tasks.Add(_cacheService.RemoveAsync(CacheKeys.Messaging.ConversationMessages(conversationId, page)));
            }

            await Task.WhenAll(tasks);
            _logger.LogDebug("Invalidated conversation cache for {ConversationId}", conversationId);
        }
    }
}
