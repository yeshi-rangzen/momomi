using Microsoft.AspNetCore.Mvc;
using MomomiAPI.Common.Results;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Controllers
{
    public class PhotosController : BaseApiController
    {
        private readonly IPhotoService _photoService;

        public PhotosController(
            IPhotoService photoService,
            ILogger<PhotosController> logger) : base(logger)
        {
            _photoService = photoService;
        }

        /// Upload a new photo for the current user
        [HttpPost("upload")]
        public async Task<ActionResult<OperationResult<PhotoUploadData>>> UploadPhoto(IFormFile file, [FromQuery] bool isPrimary = false)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(UploadPhoto), new { isPrimary, fileName = file?.FileName });

            var result = await _photoService.AddUserPhoto(userIdResult.Value, file!, isPrimary);
            return HandleOperationResult(result);
        }

        [HttpPost("upload/batch")]
        public async Task<ActionResult<OperationResult<BatchPhotoUploadData>>> UploadPhotos(
            [FromQuery] int? primaryPhotoIndex = null)
        {
            // Get files from the request form
            var files = Request.Form.Files.Where(f => f.Name == "files").ToList();

            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(UploadPhotos), new
            {
                fileCount = files?.Count,
                primaryPhotoIndex,
                fileNames = files?.Select(f => f.FileName).ToArray(),
                formKeys = Request.Form.Keys.ToArray(), // Debug: see what keys are present
                totalFormFiles = Request.Form.Files.Count // Debug: total files in form
            });

            var result = await _photoService.AddUserPhotos(userIdResult.Value, files!, primaryPhotoIndex);
            return HandleOperationResult(result);
        }

        /// Delete a specific photo
        [HttpDelete("{photoId}")]
        public async Task<ActionResult<OperationResult<PhotoDeletionData>>> DeletePhoto(Guid photoId)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(DeletePhoto), new { photoId });

            var result = await _photoService.RemovePhoto(userIdResult.Value, photoId);
            return HandleOperationResult(result);
        }

        /// Set a photo as primary
        [HttpPut("primary/{photoId}")]
        public async Task<ActionResult<OperationResult<PrimaryPhotoData>>> SetPrimaryPhoto(Guid photoId)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(SetPrimaryPhoto), new { photoId });

            var result = await _photoService.SetPrimaryPhoto(userIdResult.Value, photoId);
            return HandleOperationResult(result);
        }

        /// Reorder photos
        [HttpPut("reorder")]
        public async Task<ActionResult<OperationResult<PhotoReorderData>>> ReorderPhotos([FromBody] List<Guid> photoIds)
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(ReorderPhotos), new { photoCount = photoIds?.Count });

            var result = await _photoService.ReorderPhotos(userIdResult.Value, photoIds);
            return HandleOperationResult(result);
        }

        /// <summary>
        /// Get all photos for the current user (useful for photo management UI)
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<OperationResult<List<UserPhotoDTO>>>> GetUserPhotos()
        {
            var userIdResult = GetCurrentUserIdOrUnauthorized();
            if (userIdResult.Result != null) return userIdResult.Result;

            LogControllerAction(nameof(GetUserPhotos));

            var result = await _photoService.GetUserPhotos(userIdResult.Value);
            return HandleOperationResult(result);
        }
    }
}