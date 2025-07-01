using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MomomiAPI.Services.Interfaces;
using System.Security.Claims;

namespace MomomiAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PhotosController : ControllerBase
    {
        private readonly IPhotoService _photoService;
        private readonly ILogger<PhotosController> _logger;

        public PhotosController(IPhotoService photoService, ILogger<PhotosController> logger)
        {
            _photoService = photoService;
            _logger = logger;
        }

        /// <summary>
        /// Upload a photo for the current user.
        /// </summary>
        [HttpPost("upload")]
        public async Task<IActionResult> UploadPhoto(IFormFile file, [FromQuery] bool isPrimary = false)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                if (file == null || file.Length == 0)
                    return BadRequest(new { message = "No file uploaded" });

                // Validate file type
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp" };
                if (!allowedTypes.Contains(file.ContentType)) // 5 MB limit
                {
                    return BadRequest(new { message = "Invalid file type" });
                }

                // Validate file size (5 MB limit)
                if (file.Length > 5 * 1024 * 1024)
                {
                    return BadRequest(new { message = "File size exceeds 5 MB limit" });
                }

                var photo = await _photoService.UploadPhotoAsync(userId.Value, file, isPrimary);
                if (photo == null)
                    return BadRequest(new { message = "Failed to upload photo" });

                return Ok(new { message = "Photo uploaded successfully", photo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading photo");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Get all photos for the current user.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetUserPhotos()
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var photos = await _photoService.GetUserPhotosAsync(userId.Value);
                return Ok(photos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user photos");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Delete a photo
        /// </summary>
        [HttpDelete("{photoId}")]
        public async Task<IActionResult> DeletePhoto(Guid photoId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var success = await _photoService.DeletePhotoAsync(photoId, userId.Value);
                if (!success)
                    return NotFound(new { message = "Photo not found or you do not have permission to delete it" });

                return Ok(new { message = "Photo deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting photo {PhotoId}", photoId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Set a photo as primary
        /// </summary>
        [HttpPut("{photoId}/primary")]
        public async Task<IActionResult> SetPrimaryPhoto(Guid photoId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                var success = await _photoService.SetPrimaryPhotoAsync(photoId, userId.Value);
                if (!success)
                    return NotFound(new { message = "Photo not found or you do not have permission to set it as primary" });

                return Ok(new { message = "Primary photo set successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting primary photo {PhotoId}", photoId);
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        /// <summary>
        /// Reorder photos
        /// </summary>
        [HttpPut("reorder")]
        public async Task<IActionResult> ReorderPhotos([FromBody] List<Guid> photoIds)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                    return Unauthorized();

                if (photoIds == null || !photoIds.Any())
                    return BadRequest(new { message = "No photo IDs provided" });

                var success = await _photoService.ReorderPhotosAsync(userId.Value, photoIds);
                if (!success)
                    return BadRequest(new { message = "Failed to reorder photos" });

                return Ok(new { message = "Photos reordered successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering photos");
                return StatusCode(500, new { message = "Internal server error" });
            }
        }

        private Guid? GetCurrentUserId()
        {
            // Extract user ID from the JWT token or context
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }
}
