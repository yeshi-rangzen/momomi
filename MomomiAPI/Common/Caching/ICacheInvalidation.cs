namespace MomomiAPI.Common.Caching
{
    public interface ICacheInvalidation
    {
        Task InvalidateUserProfile(Guid userId);
        Task InvalidateUserDiscovery(Guid userId);
        Task InvalidateUserMatches(Guid userId);
        Task InvalidateUserConversations(Guid userId);
        Task InvalidateUserRelatedCaches(Guid userId);
        Task InvalidateMatchingCaches(Guid user1Id, Guid user2Id);
        Task InvalidateConversationCache(Guid conversationId);
    }
}
