using MomomiAPI.Models.DTOs;

namespace MomomiAPI.Services.Interfaces
{
    public interface IPhotoService
    {
        Task<UserPhotoDTO?> UploadPhotoAsync(Guid userId, IFormFile file, bool isPrimary = false);
        Task<bool> DeletePhotoAsync(Guid photoId, Guid userId);
        Task<bool> SetPrimaryPhotoAsync(Guid photoId, Guid userId);
        Task<IEnumerable<UserPhotoDTO>> GetUserPhotosAsync(Guid userId);
        Task<bool> ReorderPhotosAsync(Guid userId, List<Guid> photoIds);
    }
}
