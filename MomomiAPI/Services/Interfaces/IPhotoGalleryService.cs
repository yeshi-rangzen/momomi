using MomomiAPI.Common.Results;
using MomomiAPI.Models.DTOs;

namespace MomomiAPI.Services.Interfaces
{
    public interface IPhotoGalleryService
    {
        Task<OperationResult<List<UserPhotoDTO>>> GetUserPhotoGallery(Guid userId);
        Task<OperationResult<UserPhotoDTO>> GetPrimaryPhoto(Guid userId);
        Task<OperationResult<int>> GetPhotoCount(Guid userId);
    }
}