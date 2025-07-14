using MomomiAPI.Common.Results;
using MomomiAPI.Models.DTOs;

namespace MomomiAPI.Services.Interfaces
{
    public interface IPhotoManagementService
    {
        Task<OperationResult<UserPhotoDTO>> AddUserPhoto(Guid userId, IFormFile file, bool setAsPrimary = false);
        Task<OperationResult> RemovePhoto(Guid userId, Guid photoId);
        Task<OperationResult> SetPrimaryPhoto(Guid userId, Guid photoId);
        Task<OperationResult> ReorderPhotos(Guid userId, List<Guid> orderedPhotoIds);
    }
}