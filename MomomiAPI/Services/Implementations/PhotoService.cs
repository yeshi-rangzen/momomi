using Microsoft.EntityFrameworkCore;
using MomomiAPI.Common.Caching;
using MomomiAPI.Common.Constants;
using MomomiAPI.Common.Results;
using MomomiAPI.Data;
using MomomiAPI.Models.DTOs;
using MomomiAPI.Models.Entities;
using MomomiAPI.Services.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using static MomomiAPI.Common.Constants.AppConstants;

namespace MomomiAPI.Services.Implementations
{
    public class PhotoService : IPhotoService
    {
        private readonly Supabase.Client _adminClient;
        private readonly MomomiDbContext _dbContext;
        private readonly ICacheService _cacheService;
        private readonly ILogger<PhotoService> _logger;
        private readonly IConfiguration _configuration;

        private static readonly string[] AllowedMimeTypes =
        {
            "image/jpeg", "image/jpg", "image/png", "image/webp",
            "image/heic", "image/heif"
        };

        private const string UserPhotosBucket = "user-photos";

        public PhotoService(
            [FromKeyedServices("AdminClient")] Supabase.Client adminClient,
            MomomiDbContext dbContext,
            ICacheService cacheService,
            ILogger<PhotoService> logger,
            IConfiguration configuration)
        {
            _adminClient = adminClient ?? throw new ArgumentNullException(nameof(adminClient));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _cacheService = cacheService;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration;
        }

        public async Task<PhotoUploadResult> AddUserPhoto(Guid userId, IFormFile file, bool setAsPrimary = false, int photoOrder = -1)
        {
            try
            {
                _logger.LogInformation("Adding photo for user {UserId}, primary: {SetAsPrimary}", userId, setAsPrimary);

                // Validate file
                var validationResult = ValidatePhotoFile(file);
                if (!validationResult.Success)
                {
                    return PhotoUploadResult.ValidationError(validationResult.ErrorMessage!);
                }

                // Get user and current photo count in single query
                var userData = await GetUserWithPhotoCount(userId);
                if (userData == null)
                {
                    return PhotoUploadResult.UserNotFound();
                }

                // Check photo limit
                if (userData.PhotoCount >= AppConstants.Limits.MaxPhotosPerUser)
                {
                    return PhotoUploadResult.PhotoLimitReached(AppConstants.Limits.MaxPhotosPerUser);
                }

                // Upload to Supabase Storage
                var uploadResult = await UploadToSupabaseStorage(userId, file);
                if (!uploadResult.Success)
                {
                    return PhotoUploadResult.UploadFailed(uploadResult.ErrorMessage!);
                }

                // If this is the first photo, set as primary
                if (userData.PhotoCount == 0 && !setAsPrimary)
                {
                    setAsPrimary = true;
                }

                if (setAsPrimary && userData.PhotoCount > 0)
                {
                    await UnsetOtherPrimaryPhotos(userId);
                }

                // Create photo record
                var userPhoto = new UserPhoto
                {
                    UserId = userId,
                    StoragePath = uploadResult.Data!.FilePath,
                    Url = uploadResult.Data.PublicUrl,
                    ThumbnailUrl = GenerateThumbnailUrl(uploadResult.Data.PublicUrl),
                    PhotoOrder = photoOrder == -1 ? userData.PhotoCount : photoOrder,
                    IsPrimary = setAsPrimary,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.UserPhotos.Add(userPhoto);
                await _dbContext.SaveChangesAsync();

                // Invalidate relevent caches
                await InvalidateUserPhotosCaches(userId);

                var photoDto = MapToPhotoDTO(userPhoto);

                _logger.LogInformation("Successfully added photo {PhotoId} for user {UserId}", userPhoto.Id, userId);
                return PhotoUploadResult.UploadSuccess(photoDto, userData.PhotoCount + 1, setAsPrimary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding photo for user {UserId}", userId);
                return PhotoUploadResult.Error("Failed to upload photo. Please try again.");
            }
        }

        /// <summary>
        /// Batch upload multiple photos with optimized performance
        /// Reduces DB round trips and provides detailed success/failure feedback
        /// </summary>
        public async Task<BatchPhotoUploadResult> AddUserPhotos(Guid userId, List<IFormFile> files, int? primaryPhotoIndex = null)
        {
            try
            {
                _logger.LogInformation("Batch uploading {Count} photos for user {UserId}", files?.Count ?? 0, userId);

                // Pre-validate all files
                var validationErrors = ValidateBatchFiles(files);
                if (validationErrors.Any())
                {
                    return BatchPhotoUploadResult.AllFailed(validationErrors);
                }

                // Validate primary photo index
                if (primaryPhotoIndex.HasValue && (primaryPhotoIndex < 0 || primaryPhotoIndex >= files.Count))
                {
                    return BatchPhotoUploadResult.AllFailed(new List<PhotoUploadError>
                    {
                        new() { FileName = "N/A", ErrorMessage = "Invalid primary photo index", ErrorCode = ErrorCodes.VALIDATION_ERROR }
                    });
                }

                // Get user and current photo count
                var userData = await GetUserWithPhotoCount(userId);
                if (userData == null)
                {
                    return BatchPhotoUploadResult.UserNotFound();
                }

                // Check if batch upload would exceed limit
                var availableSlots = AppConstants.Limits.MaxPhotosPerUser - userData.PhotoCount;
                if (files.Count > availableSlots)
                {
                    return BatchPhotoUploadResult.AllFailed(new List<PhotoUploadError>
                    {
                        new() {
                            FileName = "Batch",
                            ErrorMessage = $"Cannot upload {files.Count} photos. Only {availableSlots} slots available",
                            ErrorCode = ErrorCodes.BUSINESS_RULE_VIOLATION
                        }
                    });
                }

                var successfulPhotos = new List<UserPhotoDTO>();
                var failedPhotos = new List<PhotoUploadError>();
                var photosToAdd = new List<UserPhoto>();

                // Process each file
                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    try
                    {
                        // Validate individual file
                        var validationResult = ValidatePhotoFile(file);
                        if (!validationResult.Success)
                        {
                            failedPhotos.Add(new PhotoUploadError
                            {
                                FileName = file.FileName,
                                ErrorMessage = validationResult.ErrorMessage!,
                                ErrorCode = ErrorCodes.VALIDATION_ERROR
                            });
                            continue;
                        }

                        // Upload to storage
                        var uploadResult = await UploadToSupabaseStorage(userId, file);
                        if (!uploadResult.Success)
                        {
                            failedPhotos.Add(new PhotoUploadError
                            {
                                FileName = file.FileName,
                                ErrorMessage = uploadResult.ErrorMessage!,
                                ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR
                            });
                            continue;
                        }

                        // Determine if this should be primary
                        var isPrimary = false;
                        if (userData.PhotoCount == 0 && i == 0) // First photo for user
                        {
                            isPrimary = true;
                        }
                        else if (primaryPhotoIndex.HasValue && i == primaryPhotoIndex.Value)
                        {
                            isPrimary = true;
                        }

                        // Create photo record
                        var userPhoto = new UserPhoto
                        {
                            UserId = userId,
                            StoragePath = uploadResult.Data!.FilePath,
                            Url = uploadResult.Data.PublicUrl,
                            ThumbnailUrl = GenerateThumbnailUrl(uploadResult.Data.PublicUrl),
                            PhotoOrder = userData.PhotoCount + photosToAdd.Count,
                            IsPrimary = isPrimary,
                            CreatedAt = DateTime.UtcNow
                        };

                        photosToAdd.Add(userPhoto);
                        successfulPhotos.Add(MapToPhotoDTO(userPhoto));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing file {FileName} for user {UserId}", file.FileName, userId);
                        failedPhotos.Add(new PhotoUploadError
                        {
                            FileName = file.FileName,
                            ErrorMessage = "Processing failed",
                            ErrorCode = ErrorCodes.INTERNAL_SERVER_ERROR
                        });
                    }
                }

                // If no photos were successful, return failure
                if (!photosToAdd.Any())
                {
                    return BatchPhotoUploadResult.AllFailed(failedPhotos);
                }

                var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

                return await executionStrategy.ExecuteAsync(async () =>
                {
                    // Batch database operations
                    using var transaction = await _dbContext.Database.BeginTransactionAsync();
                    try
                    {
                        // If any photo is marked as primary, unset existing primary photos
                        if (photosToAdd.Any(p => p.IsPrimary))
                        {
                            await UnsetOtherPrimaryPhotos(userId);
                        }

                        // Add all photos in one operation
                        _dbContext.UserPhotos.AddRange(photosToAdd);
                        await _dbContext.SaveChangesAsync();
                        await transaction.CommitAsync();

                        // Invalidate caches after successful commit
                        await InvalidateUserPhotosCaches(userId);

                        var result = new BatchPhotoUploadData
                        {
                            SuccessfulPhotos = successfulPhotos,
                            FailedPhotos = failedPhotos,
                            TotalPhotosCount = userData.PhotoCount + successfulPhotos.Count
                        };

                        _logger.LogInformation("Batch upload completed for user {UserId}: {Successful} successful, {Failed} failed",
                            userId, successfulPhotos.Count, failedPhotos.Count);

                        return BatchPhotoUploadResult.UploadSuccess(result);
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Database transaction failed during batch upload for user {UserId}", userId);

                        // Clean up uploaded files on transaction failure
                        await CleanupUploadedFiles(photosToAdd.Select(p => p.StoragePath).ToList());

                        return BatchPhotoUploadResult.Error("Failed to save photos to database");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during batch photo upload for user {UserId}", userId);
                return BatchPhotoUploadResult.Error("Batch upload failed. Please try again.");
            }
        }

        /// <summary>
        /// Removes a photo with optimized cache invalidation
        /// </summary>
        public async Task<PhotoDeletionResult> RemovePhoto(Guid userId, Guid photoId)
        {
            try
            {
                _logger.LogInformation("Removing photo {PhotoId} for user {UserId}", photoId, userId);

                var photo = await _dbContext.UserPhotos
                    .FirstOrDefaultAsync(p => p.Id == photoId && p.UserId == userId);

                if (photo == null)
                {
                    return PhotoDeletionResult.PhotoNotFound();
                }

                var wasPrimary = photo.IsPrimary;
                var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

                return await executionStrategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _dbContext.Database.BeginTransactionAsync();
                    try
                    {
                        // If this was the primary photo, set another as primary
                        //if (wasPrimary)
                        //{
                        //    newPrimaryPhotoId = await SetNewPrimaryPhoto(userId, photoId);
                        //}

                        // Remove from database
                        _dbContext.UserPhotos.Remove(photo);

                        var remainingPhotos = await _dbContext.UserPhotos
                        .Where(p => p.UserId == userId && p.Id != photo.Id)
                        .OrderBy(p => p.PhotoOrder)
                        .ToListAsync();

                        // Reassign PhotoOrders in ascending order (0,1,2,3...)
                        for (int i = 0; i < remainingPhotos.Count; i++)
                        {
                            remainingPhotos[i].PhotoOrder = i;
                            remainingPhotos[i].IsPrimary = (i == 0); // first photo is always primary
                        }


                        await _dbContext.SaveChangesAsync();
                        await transaction.CommitAsync();

                        // Get remaining photo count
                        var newPrimaryPhotoId = remainingPhotos.FirstOrDefault()?.Id;

                        // Delete from storage (don't fail the operation if this fails)
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await DeleteFromSupabaseStorage(photo.StoragePath);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to delete photo from storage: {StoragePath}", photo.StoragePath);
                            }
                        });

                        // Invalidate caches
                        await InvalidateUserPhotosCaches(userId);
                        var remainingPhotoDtos = remainingPhotos.Select((photo) => new UserPhotoDTO
                        {
                            Id = photo.Id,
                            Url = photo.Url,
                            ThumbnailUrl = photo.ThumbnailUrl,
                            IsPrimary = photo.IsPrimary,
                            PhotoOrder = photo.PhotoOrder,
                        }).ToList();

                        _logger.LogInformation("Successfully removed photo {PhotoId} for user {UserId}", photoId, userId);

                        return PhotoDeletionResult.DeleteSuccess(photoId, wasPrimary, newPrimaryPhotoId, remainingPhotoDtos);
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing photo {PhotoId} for user {UserId}", photoId, userId);
                return PhotoDeletionResult.Error("Failed to remove photo. Please try again.");
            }
        }

        /// <summary>
        /// Reorders photos with batch update optimization
        /// </summary>
        public async Task<PhotoReorderResult> ReorderPhotos(Guid userId, List<Guid> orderedPhotoIds)
        {
            try
            {
                _logger.LogInformation("Reordering photos for user {UserId}", userId);

                if (orderedPhotoIds == null || !orderedPhotoIds.Any())
                {
                    return PhotoReorderResult.ValidationError("Photo IDs list cannot be empty");
                }

                var photos = await _dbContext.UserPhotos
                    .Where(p => p.UserId == userId && orderedPhotoIds.Contains(p.Id))
                    .ToListAsync();

                if (photos.Count != orderedPhotoIds.Count)
                {
                    return PhotoReorderResult.ValidationError("One or more photo IDs are invalid");
                }

                // Update photo order in memory
                for (int i = 0; i < orderedPhotoIds.Count; i++)
                {
                    var photo = photos.FirstOrDefault(p => p.Id == orderedPhotoIds[i]);
                    if (photo != null)
                    {
                        photo.PhotoOrder = i;
                    }
                }

                await _dbContext.SaveChangesAsync();

                // Invalidate caches
                await InvalidateUserPhotosCaches(userId);

                var reorderedPhotoDtos = photos
                    .OrderBy(p => p.PhotoOrder)
                    .Select(MapToPhotoDTO)
                    .ToList();

                _logger.LogInformation("Successfully reordered photos for user {UserId}", userId);
                return PhotoReorderResult.ReorderSuccess(reorderedPhotoDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering photos for user {UserId}", userId);
                return PhotoReorderResult.Error("Failed to reorder photos. Please try again.");
            }
        }

        /// <summary>
        /// Sets a photo as primary with optimized database operations
        /// </summary>
        public async Task<PrimaryPhotoResult> SetPrimaryPhoto(Guid userId, Guid photoId)
        {
            try
            {
                _logger.LogInformation("Setting primary photo {PhotoId} for user {UserId}", photoId, userId);

                // Get all user photos in single query
                var photos = await _dbContext.UserPhotos
                    .Where(p => p.UserId == userId)
                    .ToListAsync();

                var targetPhoto = photos.FirstOrDefault(p => p.Id == photoId);
                if (targetPhoto == null)
                {
                    return PrimaryPhotoResult.PhotoNotFound();
                }

                var previousPrimaryPhoto = photos.FirstOrDefault(p => p.IsPrimary);
                var previousPrimaryPhotoId = previousPrimaryPhoto?.Id;

                // Update primary flags in memory first
                foreach (var photo in photos)
                {
                    photo.IsPrimary = photo.Id == photoId;
                }

                await _dbContext.SaveChangesAsync();

                // Invalidate caches
                await InvalidateUserPhotosCaches(userId);

                var targetPhotoDto = MapToPhotoDTO(targetPhoto);

                _logger.LogInformation("Successfully set primary photo {PhotoId} for user {UserId}", photoId, userId);
                return PrimaryPhotoResult.UpdateSuccess(photoId, previousPrimaryPhotoId, targetPhotoDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting primary photo {PhotoId} for user {UserId}", photoId, userId);
                return PrimaryPhotoResult.Error("Failed to set primary photo. Please try again.");
            }
        }

        /// <summary>
        /// Gets user photos with caching support
        /// </summary>
        public async Task<OperationResult<List<UserPhotoDTO>>> GetUserPhotos(Guid userId)
        {
            try
            {
                var cacheKey = CacheKeys.Users.Photos(userId);

                var photos = await _cacheService.GetOrSetAsync(
                    cacheKey,
                    async () =>
                    {
                        var userPhotos = await _dbContext.UserPhotos
                            .Where(p => p.UserId == userId)
                            .OrderBy(p => p.PhotoOrder)
                            .ToListAsync();

                        return userPhotos.Select(MapToPhotoDTO).ToList();
                    },
                    CacheKeys.Duration.UserProfile
                );

                return OperationResult<List<UserPhotoDTO>>.Successful(photos ?? new List<UserPhotoDTO>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting photos for user {UserId}", userId);
                return OperationResult<List<UserPhotoDTO>>.Failed("Failed to retrieve photos");
            }
        }


        #region Private Helper Methods
        /// Validates photo file format, size, and basic integrity
        private static OperationResult ValidatePhotoFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return OperationResult.ValidationFailure("No file uploaded");
            }

            if (!AllowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                return OperationResult.ValidationFailure("Invalid file type. Only JPEG, PNG and WebP images are allowed");
            }

            // Increased size limit since mobile pre-compresses efficiently
            if (file.Length > AppConstants.FileSizes.MaxPhotoSizeBytes)
            {
                var maxSizeMB = AppConstants.FileSizes.MaxPhotoSizeBytes / (1024 * 1024);
                return OperationResult.ValidationFailure($"File size exceeds {maxSizeMB}MB limit");
            }

            // Additional validation for very small files (likely corrupted)
            if (file.Length < 1024) // Less than 1KB
            {
                return OperationResult.ValidationFailure("File appears to be corrupted or too small");
            }

            return OperationResult.Successful();
        }

        /// Batch validation with enhanced mobile-specific checks
        private static List<PhotoUploadError> ValidateBatchFiles(List<IFormFile> files)
        {
            var errors = new List<PhotoUploadError>();

            if (files == null || !files.Any())
            {
                errors.Add(new PhotoUploadError
                {
                    FileName = "Batch",
                    ErrorMessage = "No files provided",
                    ErrorCode = ErrorCodes.VALIDATION_ERROR
                });
                return errors;
            }

            // Check for duplicate file names (mobile apps sometimes generate same names)
            var duplicateNames = files.GroupBy(f => f.FileName)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key);

            foreach (var duplicateName in duplicateNames)
            {
                errors.Add(new PhotoUploadError
                {
                    FileName = duplicateName ?? "Unknown",
                    ErrorMessage = "Duplicate file names detected",
                    ErrorCode = ErrorCodes.VALIDATION_ERROR
                });
            }

            // Validate individual files
            foreach (var file in files)
            {
                var validation = ValidatePhotoFile(file);
                if (!validation.Success)
                {
                    errors.Add(new PhotoUploadError
                    {
                        FileName = file.FileName ?? "Unknown",
                        ErrorMessage = validation.ErrorMessage!,
                        ErrorCode = ErrorCodes.VALIDATION_ERROR
                    });
                }
            }

            return errors;
        }

        /// Gets user data with photo count in a single optimized query
        private async Task<UserPhotoData?> GetUserWithPhotoCount(Guid userId)
        {
            return await _dbContext.Users
                .Where(u => u.Id == userId)
                .Select(u => new UserPhotoData
                {
                    UserId = u.Id,
                    PhotoCount = u.Photos.Count()
                })
                .FirstOrDefaultAsync();
        }

        /// Uploads file to Supabase Storage with image processing
        private async Task<OperationResult<SupabaseUploadResult>> UploadToSupabaseStorage(Guid userId, IFormFile file)
        {
            try
            {
                // Generate unique filename
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var fileName = $"user_{userId}_{Guid.NewGuid()}{fileExtension}";
                var filePath = $"users/{userId}/{fileName}";

                // Process and resize image
                using var processedImageStream = await ProcessImageAsync(file);
                var fileBytes = processedImageStream.ToArray();

                // Upload to Supabase Storage
                var uploadResult = await _adminClient.Storage
                    .From(UserPhotosBucket)
                    .Upload(fileBytes, filePath, new Supabase.Storage.FileOptions
                    {
                        ContentType = file.ContentType,
                        Upsert = false
                    });

                if (uploadResult == null)
                {
                    return OperationResult<SupabaseUploadResult>.Failed("Failed to upload photo to storage");
                }

                // Get public URL
                var publicUrl = _adminClient.Storage.From(UserPhotosBucket).GetPublicUrl(filePath);

                var result = new SupabaseUploadResult
                {
                    FilePath = filePath,
                    PublicUrl = publicUrl,
                    FileName = fileName
                };

                return OperationResult<SupabaseUploadResult>.Successful(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading to Supabase Storage");
                return OperationResult<SupabaseUploadResult>.Failed("Photo upload failed. Please try again later");
            }
        }

        /// Deletes file from Supabase Storage
        private async Task<OperationResult> DeleteFromSupabaseStorage(string filePath)
        {
            try
            {
                var deleteResult = await _adminClient.Storage
                    .From(UserPhotosBucket)
                    .Remove(new List<string> { filePath });

                if (deleteResult == null || !deleteResult.Any())
                {
                    return OperationResult.Failed("Failed to delete photo from storage");
                }

                return OperationResult.Successful();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting from Supabase Storage: {FilePath}", filePath);
                return OperationResult.Failed("Failed to delete photo from storage");
            }
        }

        /// Processes and resizes images for optimal storage and performance
        private async Task<MemoryStream> ProcessImageAsync(IFormFile file)
        {
            try
            {
                using var inputStream = file.OpenReadStream();
                using var image = await Image.LoadAsync(inputStream);

                _logger.LogDebug("Processing image: {Width}x{Height}, Size: {Size}KB",
                    image.Width, image.Height, file.Length / 1024);

                // Check if image is already mobile-processed (1080x1350 with 4:5 ratio)
                var isMobileProcessed = IsMobileProcessedImage(image.Width, image.Height);
                if (isMobileProcessed && file.Length <= 3 * 1024 * 1024) // 3MB threshold
                {
                    // Image is already optimally processed by mobile, minimal processing
                    _logger.LogDebug("Image appears to be mobile-processed, applying minimal processing");
                    return await ApplyMinimalProcessing(image, file.ContentType);
                }
                else
                {
                    // Apply full processing for web uploads or oversized images
                    _logger.LogDebug("Applying full image processing");
                    return await ApplyFullProcessing(image, file.ContentType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image");
                throw new InvalidOperationException("Failed to process image", ex);
            }
        }

        /// <summary>
        /// Checks if image dimensions match mobile app output (4:5 ratio around 1080x1350)
        /// </summary>
        private static bool IsMobileProcessedImage(int width, int height)
        {
            // Check for 4:5 ratio with some tolerance
            var ratio = (double)width / height;
            var expectedRatio = 4.0 / 5.0; // 0.8
            var ratioTolerance = 0.05; // 5% tolerance

            var isCorrectRatio = Math.Abs(ratio - expectedRatio) <= ratioTolerance;

            // Check if dimensions are close to mobile output (within 10% tolerance)
            var isCorrectSize = width >= 972 && width <= 1188 && // 1080 ± 10%
                               height >= 1215 && height <= 1485;  // 1350 ± 10%

            return isCorrectRatio && isCorrectSize;
        }

        /// <summary>
        /// Minimal processing for mobile-uploaded images (just format conversion if needed)
        /// </summary>
        private async Task<MemoryStream> ApplyMinimalProcessing(Image image, string contentType)
        {
            var outputStream = new MemoryStream();

            // Convert to JPEG if not already, with slightly higher quality since it's pre-compressed
            var encoder = contentType.ToLowerInvariant() switch
            {
                "image/jpeg" or "image/jpg" => new JpegEncoder { Quality = AppConstants.FileSizes.HighQualityJpeg },
                _ => new JpegEncoder { Quality = AppConstants.FileSizes.HighQualityJpeg } // Convert others to JPEG
            };

            await image.SaveAsync(outputStream, encoder);
            outputStream.Position = 0;
            return outputStream;
        }

        /// <summary>
        /// Full processing for web uploads or non-standard images
        /// </summary>
        private async Task<MemoryStream> ApplyFullProcessing(Image image, string contentType)
        {
            // Calculate resize dimensions to 4:5 ratio matching mobile app
            var (newWidth, newHeight) = CalculateOptimalDimensions(image.Width, image.Height);

            // Resize image to match mobile specifications
            image.Mutate(x => x.Resize(newWidth, newHeight));

            var outputStream = new MemoryStream();
            var encoder = new JpegEncoder { Quality = AppConstants.FileSizes.MediumQualityJpeg }; // Match mobile compression

            await image.SaveAsync(outputStream, encoder);
            outputStream.Position = 0;
            return outputStream;
        }

        /// <summary>
        /// Calculates optimal dimensions maintaining 4:5 ratio like mobile app
        /// </summary>
        private static (int width, int height) CalculateOptimalDimensions(int originalWidth, int originalHeight)
        {
            // Target 4:5 ratio (0.8)
            var targetRatio = 4.0 / 5.0;
            var currentRatio = (double)originalWidth / originalHeight;

            int newWidth, newHeight;

            if (currentRatio > targetRatio)
            {
                // Image is wider than target ratio, fit by height
                newHeight = Math.Min(originalHeight, AppConstants.FileSizes.DisplayPhotoHeight);
                newWidth = (int)(newHeight * targetRatio);
            }
            else
            {
                // Image is taller than target ratio, fit by width
                newWidth = Math.Min(originalWidth, AppConstants.FileSizes.DisplayPhotoWidth);
                newHeight = (int)(newWidth / targetRatio);
            }

            return (newWidth, newHeight);
        }

        /// Gets appropriate image encoder based on content type
        private static IImageEncoder GetImageEncoder(string mimeType)
        {
            return mimeType.ToLowerInvariant() switch
            {
                "image/jpeg" or "image/jpg" => new JpegEncoder { Quality = 85 },
                "image/png" => new PngEncoder(),
                "image/webp" => new WebpEncoder { Quality = 85 },
                _ => new JpegEncoder { Quality = 85 } // Default to JPEG
            };
        }

        /// Unsets primary flag from all other user photos
        private async Task UnsetOtherPrimaryPhotos(Guid userId)
        {
            await _dbContext.UserPhotos
                .Where(p => p.UserId == userId && p.IsPrimary)
                .ExecuteUpdateAsync(p => p.SetProperty(photo => photo.IsPrimary, false));
        }

        /// Sets a new primary photo when the current primary is deleted
        /// Returns the ID of the new primary photo, or null if no photos remain
        private async Task<Guid?> SetNewPrimaryPhoto(Guid userId, Guid excludePhotoId)
        {
            var nextPhoto = await _dbContext.UserPhotos
                .Where(p => p.UserId == userId && p.Id != excludePhotoId)
                .OrderBy(p => p.PhotoOrder)
                .FirstOrDefaultAsync();

            if (nextPhoto != null)
            {
                nextPhoto.IsPrimary = true;
                return nextPhoto.Id;
            }

            return null;
        }

        /// Enhanced thumbnail generation with multiple sizes for different use cases
        private string GenerateThumbnailUrl(string originalUrl)
        {
            // Generate multiple thumbnail variations for different contexts
            var baseUrl = originalUrl.Split('?')[0]; // Remove any existing query params

            // Primary thumbnail for lists/discovery (square crop)
            return $"{baseUrl}?width={AppConstants.FileSizes.ThumbnailSize}&height={AppConstants.FileSizes.ThumbnailSize}&resize=cover&quality={AppConstants.FileSizes.ThumbnailQualityJpeg}";
        }

        /// Generate different image variations for various app contexts
        private Dictionary<string, string> GenerateImageVariations(string originalUrl)
        {
            var baseUrl = originalUrl.Split('?')[0];

            return new Dictionary<string, string>
            {
                ["original"] = originalUrl,
                ["thumbnail"] = $"{baseUrl}?width={AppConstants.FileSizes.ThumbnailSize}&height={AppConstants.FileSizes.ThumbnailSize}&resize=cover&quality={AppConstants.FileSizes.ThumbnailQualityJpeg}",
                ["web_preview"] = $"{baseUrl}?width={AppConstants.FileSizes.WebPreviewWidth}&height={AppConstants.FileSizes.WebPreviewHeight}&resize=fit&quality={AppConstants.FileSizes.MediumQualityJpeg}",
                ["mobile_optimized"] = originalUrl // Already optimized by our processing
            };
        }

        /// Maps UserPhoto entity to DTO
        private static UserPhotoDTO MapToPhotoDTO(UserPhoto photo)
        {
            return new UserPhotoDTO
            {
                Id = photo.Id,
                Url = photo.Url,
                ThumbnailUrl = photo.ThumbnailUrl,
                PhotoOrder = photo.PhotoOrder,
                IsPrimary = photo.IsPrimary
            };
        }

        /// Invalidates all user photo-related caches using batch operations
        private async Task InvalidateUserPhotosCaches(Guid userId)
        {
            //var keysToInvalidate = new[]
            //{
            //    CacheKeys.Users.Photos(userId),
            //    CacheKeys.Users.Profile(userId)  // Profile includes photos
            //};

            //await _cacheService.RemoveManyAsync(keysToInvalidate);

            var userCacheKey = CacheKeys.Users.Profile(userId);
            await _cacheService.RemoveAsync(userCacheKey);

            _logger.LogDebug("Invalidated photo caches for user {UserId}", userId);
        }

        /// Cleans up uploaded files in case of transaction failure
        private async Task CleanupUploadedFiles(List<string> filePaths)
        {
            try
            {
                if (filePaths.Any())
                {
                    await _adminClient.Storage
                        .From(UserPhotosBucket)
                        .Remove(filePaths);

                    _logger.LogInformation("Cleaned up {Count} uploaded files after transaction failure", filePaths.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup uploaded files: {FilePaths}", string.Join(", ", filePaths));
            }
        }
        #endregion

        #region Helper Classes
        /// <summary>
        /// Helper class for upload results
        /// </summary>
        private class SupabaseUploadResult
        {
            public string FilePath { get; set; } = string.Empty;
            public string PublicUrl { get; set; } = string.Empty;
            public string FileName { get; set; } = string.Empty;
        }

        /// <summary>
        /// Helper class for user photo data queries
        /// </summary>
        private class UserPhotoData
        {
            public Guid UserId { get; set; }
            public int PhotoCount { get; set; }
        }
        #endregion
    }
}
