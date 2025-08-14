using MomomiAPI.Common.Results;
using MomomiAPI.Models.DTOs;

namespace MomomiAPI.Services.Interfaces
{
    public interface IPhotoService
    {
        Task<PhotoUploadResult> AddUserPhoto(Guid userId, IFormFile file, bool setAsPrimary = false);
        Task<BatchPhotoUploadResult> AddUserPhotos(Guid userId, List<IFormFile> files, int? primaryPhotoIndex = null);
        Task<PhotoDeletionResult> RemovePhoto(Guid userId, Guid photoId);
        Task<PrimaryPhotoResult> SetPrimaryPhoto(Guid userId, Guid photoId);
        Task<PhotoReorderResult> ReorderPhotos(Guid userId, List<Guid> orderedPhotoIds);
        Task<OperationResult<List<UserPhotoDTO>>> GetUserPhotos(Guid userId);

    }
}
