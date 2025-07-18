using MomomiAPI.Common.Caching;
using MomomiAPI.Common.Constants;
using MomomiAPI.Common.Results;
using MomomiAPI.Data;
using MomomiAPI.Models;
using MomomiAPI.Models.Entities;
using MomomiAPI.Services.Interfaces;
using static MomomiAPI.Models.Requests.AuthenticationRequests;

namespace MomomiAPI.Services.Implementations
{
    public class UserRegistrationService : IUserRegistrationService
    {
        private readonly MomomiDbContext _dbContext;
        private readonly ICacheService _cacheService;
        private readonly IJwtService _jwtService;
        private readonly IEmailVerificationService _emailVerificationService;
        private readonly ILogger<UserRegistrationService> _logger;

        public UserRegistrationService(
            MomomiDbContext dbContext,
            ICacheService cacheService,
            IJwtService jwtService,
            IEmailVerificationService emailVerificationService,
            ILogger<UserRegistrationService> logger)
        {
            _dbContext = dbContext;
            _cacheService = cacheService;
            _jwtService = jwtService;
            _emailVerificationService = emailVerificationService;
            _logger = logger;
        }

        public async Task<RegistrationResult> RegisterNewUser(CompleteRegistrationRequest request)
        {
            try
            {
                _logger.LogInformation("Starting registration for {Email}", request.Email);

                // Check if email is already registered
                var isEmailRegistered = await _emailVerificationService.IsEmailAlreadyRegistered(request.Email);
                if (isEmailRegistered)
                {
                    return RegistrationResult.EmailAlreadyRegistered();
                }

                // Validate age requirement
                var age = DateTime.UtcNow.Year - request.DateOfBirth.Year;
                if (age < AppConstants.Limits.MinAge)
                {
                    return RegistrationResult.UnderageUser();
                }

                // Verify the verification token
                var verificationKey = CacheKeys.Authentication.EmailVerification(request.Email, request.VerificationToken);
                var verificationData = await _cacheService.GetAsync<RegisterVerificationData>(verificationKey);

                if (verificationData == null)
                {
                    return RegistrationResult.InvalidVerificationToken();
                }

                // Clear verification token (single use)
                await _cacheService.RemoveAsync(verificationKey);

                // Extract Supabase user ID from verification data
                var supabaseUserId = verificationData.SupabaseUserId;
                if (string.IsNullOrEmpty(supabaseUserId))
                {
                    return RegistrationResult.InvalidVerificationToken();
                }

                // Create user in our database
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    SupabaseUid = Guid.Parse(supabaseUserId),
                    Email = request.Email,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    DateOfBirth = request.DateOfBirth.ToUniversalTime(),
                    Gender = request.Gender,
                    InterestedIn = request.InterestedIn,
                    PhoneNumber = request.PhoneNumber,
                    Bio = request.Bio,
                    Hometown = request.Hometown,
                    Heritage = request.Heritage,
                    Religion = request.Religion,
                    LanguagesSpoken = request.LanguagesSpoken,
                    EnableGlobalDiscovery = AppConstants.DefaultValues.DefaultGlobalDiscovery,
                    IsDiscoverable = AppConstants.DefaultValues.DefaultIsDiscoverable,
                    IsGloballyDiscoverable = AppConstants.DefaultValues.DefaultIsGloballyDiscoverable,
                    NotificationsEnabled = AppConstants.DefaultValues.DefaultNotificationsEnabled,
                    MaxDistanceKm = AppConstants.DefaultValues.DefaultMaxDistance,
                    MinAge = AppConstants.DefaultValues.DefaultMinAge,
                    MaxAge = AppConstants.DefaultValues.DefaultMaxAge,
                    CreatedAt = DateTime.UtcNow,
                    LastActive = DateTime.UtcNow,
                    IsActive = true
                };

                _dbContext.Users.Add(user);
                await _dbContext.SaveChangesAsync();

                // Generate our custom tokens
                var accessToken = _jwtService.GenerateAccessToken(user);
                var refreshToken = _jwtService.GenerateRefreshToken();
                var refreshExpiry = DateTime.UtcNow.AddDays(30);

                // Store refresh token
                await _jwtService.StoreRefreshTokenAsync(user.Id, refreshToken, refreshExpiry);

                _logger.LogInformation("User registration completed successfully: {Email}", request.Email);

                return RegistrationResult.Success(user, accessToken, refreshToken, DateTime.UtcNow.AddHours(1));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for {Email}", request.Email);
                return RegistrationResult.Failed(ex.Message);
            }
        }
    }
}