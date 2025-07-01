using MomomiAPI.Models.Entities;

namespace MomomiAPI.Services.Interfaces
{
    public interface IPhotoService
    {
        Task<UserPhoto?> UploadPhotoAsync(Guid userId, IFormFile file, bool isPrimary = false);
        Task<bool> DeletePhotoAsync(Guid photoId, Guid userId);
        Task<bool> SetPrimaryPhotoAsync(Guid photoId, Guid userId);
        Task<IEnumerable<UserPhoto>> GetUserPhotosAsync(Guid userId);
        Task<bool> ReorderPhotosAsync(Guid userId, List<Guid> photoIds);
    }
}
