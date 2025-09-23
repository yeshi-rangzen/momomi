using MomomiAPI.Models.DTOs;
using static MomomiAPI.Common.Constants.AppConstants;

namespace MomomiAPI.Common.Results
{
    #region Photo Upload Result
    /// Photo upload-specific data
    public class PhotoUploadData
    {
        public UserPhotoDTO Photo { get; set; } = null!;
        public int TotalPhotosCount { get; set; }
        public bool SetAsPrimary { get; set; }
    }
    public class PhotoUploadResult : OperationResult<PhotoUploadData>
    {
        private PhotoUploadResult(bool success, PhotoUploadData? data, string? errorCode = null,
            string? errorMessage = null, Dictionary<string, object>? metadata = null)
            : base(success, data, errorCode, errorMessage, metadata)
        {
        }

        public static PhotoUploadResult UploadSuccess(UserPhotoDTO photo, int totalPhotosCount, bool setAsPrimary,
            Dictionary<string, object>? metadata = null)
        {
            var data = new PhotoUploadData
            {
                Photo = photo,
                TotalPhotosCount = totalPhotosCount,
                SetAsPrimary = setAsPrimary
            };

            return new PhotoUploadResult(true, data, null, null, metadata);
        }

        public static PhotoUploadResult ValidationError(string message)
            => new(false, null, ErrorCodes.VALIDATION_ERROR, message);

        public static PhotoUploadResult PhotoLimitReached(int maxPhotos)
            => new(false, null, ErrorCodes.BUSINESS_RULE_VIOLATION,
                $"Maximum of {maxPhotos} photos allowed per user");

        public static PhotoUploadResult UserNotFound()
            => new(false, null, ErrorCodes.USER_NOT_FOUND, "User not found");

        public static PhotoUploadResult UploadFailed(string message)
            => new(false, null, ErrorCodes.INTERNAL_SERVER_ERROR,
                $"Photo upload failed: {message}");

        public static PhotoUploadResult Error(string message)
            => new(false, null, ErrorCodes.INTERNAL_SERVER_ERROR, message);
    }

    #endregion

    #region Batch Photo Upload Result
    /// Batch photo upload data
    public class BatchPhotoUploadData
    {
        public List<UserPhotoDTO> SuccessfulPhotos { get; set; } = new();
        public List<PhotoUploadError> FailedPhotos { get; set; } = new();
        public int TotalPhotosCount { get; set; }
        public int SuccessfulCount => SuccessfulPhotos.Count;
        public int FailedCount => FailedPhotos.Count;
    }

    public class PhotoUploadError
    {
        public string FileName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
    }
    public class BatchPhotoUploadResult : OperationResult<BatchPhotoUploadData>
    {
        private BatchPhotoUploadResult(bool success, BatchPhotoUploadData? data, string? errorCode = null,
            string? errorMessage = null, Dictionary<string, object>? metadata = null)
            : base(success, data, errorCode, errorMessage, metadata)
        {
        }

        public static BatchPhotoUploadResult UploadSuccess(BatchPhotoUploadData data,
            Dictionary<string, object>? metadata = null)
        {
            // Consider success if at least one photo was uploaded
            var isSuccess = data.SuccessfulCount > 0;
            return new BatchPhotoUploadResult(isSuccess, data, null, null, metadata);
        }

        public static BatchPhotoUploadResult AllFailed(List<PhotoUploadError> errors)
        {
            var data = new BatchPhotoUploadData
            {
                FailedPhotos = errors,
                TotalPhotosCount = 0
            };

            return new BatchPhotoUploadResult(false, data, ErrorCodes.VALIDATION_ERROR,
                "All photo uploads failed");
        }

        public static BatchPhotoUploadResult UserNotFound()
            => new(false, null, ErrorCodes.USER_NOT_FOUND, "User not found");

        public static BatchPhotoUploadResult Error(string message)
            => new(false, null, ErrorCodes.INTERNAL_SERVER_ERROR, message);
    }

    #endregion

    #region Photo Deletion Result
    /// Photo deletion data
    public class PhotoDeletionData
    {
        public Guid DeletedPhotoId { get; set; }
    }
    public class PhotoDeletionResult : OperationResult<PhotoDeletionData>
    {
        private PhotoDeletionResult(bool success, PhotoDeletionData? data, string? errorCode = null,
            string? errorMessage = null, Dictionary<string, object>? metadata = null)
            : base(success, data, errorCode, errorMessage, metadata)
        {
        }

        public static PhotoDeletionResult DeleteSuccess(Guid deletedPhotoId, Dictionary<string, object>? metadata = null)
        {
            var data = new PhotoDeletionData
            {
                DeletedPhotoId = deletedPhotoId,
            };

            return new PhotoDeletionResult(true, data, null, null, metadata);
        }

        public static PhotoDeletionResult PhotoNotFound()
            => new(false, null, ErrorCodes.USER_NOT_FOUND,
                "Photo not found or you don't have permission to delete");

        public static PhotoDeletionResult Error(string message)
            => new(false, null, ErrorCodes.INTERNAL_SERVER_ERROR, message);
    }
    #endregion

    #region Photo Reorder Result
    public class PhotoReorderData
    {
        public List<UserPhotoDTO> ReorderedPhotos { get; set; } = new();
        public int PhotosCount { get; set; }
    }

    public class PhotoReorderResult : OperationResult
    {
        private PhotoReorderResult(bool success, string? errorCode = null,
            string? errorMessage = null, Dictionary<string, object>? metadata = null)
            : base(success, errorCode, errorMessage, metadata)
        {
        }

        public static PhotoReorderResult ReorderSuccess(Dictionary<string, object>? metadata = null)
        {
            return new PhotoReorderResult(true, null, null, metadata);
        }

        public static PhotoReorderResult ValidationError(string message)
            => new(false, ErrorCodes.VALIDATION_ERROR, message);

        public static PhotoReorderResult Error(string message)
            => new(false, ErrorCodes.INTERNAL_SERVER_ERROR, message);
    }
    #endregion

    #region Primary Photo Update Result
    /// Primary photo update data
    public class PrimaryPhotoData
    {
        public Guid NewPrimaryPhotoId { get; set; }
        public Guid? PreviousPrimaryPhotoId { get; set; }
        public UserPhotoDTO NewPrimaryPhoto { get; set; } = null!;
    }

    public class PrimaryPhotoResult : OperationResult<PrimaryPhotoData>
    {
        private PrimaryPhotoResult(bool success, PrimaryPhotoData? data, string? errorCode = null,
            string? errorMessage = null, Dictionary<string, object>? metadata = null)
            : base(success, data, errorCode, errorMessage, metadata)
        {
        }

        public static PrimaryPhotoResult UpdateSuccess(Guid newPrimaryPhotoId, Guid? previousPrimaryPhotoId,
            UserPhotoDTO newPrimaryPhoto, Dictionary<string, object>? metadata = null)
        {
            var data = new PrimaryPhotoData
            {
                NewPrimaryPhotoId = newPrimaryPhotoId,
                PreviousPrimaryPhotoId = previousPrimaryPhotoId,
                NewPrimaryPhoto = newPrimaryPhoto
            };

            return new PrimaryPhotoResult(true, data, null, null, metadata);
        }

        public static PrimaryPhotoResult PhotoNotFound()
            => new(false, null, ErrorCodes.USER_NOT_FOUND,
                "Photo not found or you don't have permission to modify it");

        public static PrimaryPhotoResult Error(string message)
            => new(false, null, ErrorCodes.INTERNAL_SERVER_ERROR, message);
    }
    #endregion
}
