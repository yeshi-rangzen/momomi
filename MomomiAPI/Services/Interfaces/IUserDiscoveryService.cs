using MomomiAPI.Common.Results;

namespace MomomiAPI.Services.Interfaces
{
    public interface IUserDiscoveryService
    {
        Task<DiscoveryResult> FindUsersForSwiping(Guid userId, int count = 10);
        Task<DiscoveryResult> FindUsersGlobally(Guid userId, int count = 10);
        Task<DiscoveryResult> FindUsersLocally(Guid userId, int count = 10, int maxDistanceKm = 50);
    }
}