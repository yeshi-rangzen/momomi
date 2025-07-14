using Microsoft.AspNetCore.Mvc;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Controllers
{
    public class PhotosController : BaseApiController
    {
        private readonly IPhotoManagementService _photoManagementService;
        private readonly IPhotoGalleryService _photoGalleryService;

        public PhotosController(
            IPhotoManagementService photoManagementService,
            IPhotoGalleryService photoGalleryService,
            ILogger<PhotosController> logger) : base(logger)
        {
            _photoManagementService = photoManagementService;
            _photoGalleryService = photoGalleryService;
        }

        /// <summary>
        /// Upload a new photo for the current user
        /// </summary>
        [HttpPost("upload")]
        public async Task<ActionResult> UploadPhoto(IFormFile file, [FromQuery] bool isPrimary = false)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(UploadPhoto), new { isPrimary, fileName = file?.FileName });

            var result = await _photoManagementService.AddUserPhoto(userIdResult.Value, file, isPrimary);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Get all photos for the current user
        /// </summary>
        [HttpGet]
        public async Task<ActionResult> GetMyPhotos()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(GetMyPhotos));

            var result = await _photoGalleryService.GetUserPhotoGallery(userIdResult.Value);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Get photo count for current user
        /// </summary>
        [HttpGet("count")]
        public async Task<ActionResult> GetPhotoCount()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(GetPhotoCount));

            var result = await _photoGalleryService.GetPhotoCount(userIdResult.Value);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Delete a specific photo
        /// </summary>
        [HttpDelete("{photoId}")]
        public async Task<ActionResult> DeletePhoto(Guid photoId)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(DeletePhoto), new { photoId });

            var result = await _photoManagementService.RemovePhoto(userIdResult.Value, photoId);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Set a photo as primary
        /// </summary>
        [HttpPut("{photoId}/set-primary")]
        public async Task<ActionResult> SetPrimaryPhoto(Guid photoId)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(SetPrimaryPhoto), new { photoId });

            var result = await _photoManagementService.SetPrimaryPhoto(userIdResult.Value, photoId);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Reorder photos
        /// </summary>
        [HttpPut("reorder")]
        public async Task<ActionResult> ReorderPhotos([FromBody] List<Guid> photoIds)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(ReorderPhotos), new { photoCount = photoIds?.Count });

            var result = await _photoManagementService.ReorderPhotos(userIdResult.Value, photoIds);
            return HandleOperationResult(result);
        }
    }
}