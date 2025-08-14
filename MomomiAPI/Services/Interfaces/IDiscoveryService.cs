using MomomiAPI.Common.Results;

namespace MomomiAPI.Services.Interfaces
{
    public interface IDiscoveryService
    {
        Task<DiscoveryResult> DiscoverCandidatesAsync(Guid userId, int maxResults);
    }
}