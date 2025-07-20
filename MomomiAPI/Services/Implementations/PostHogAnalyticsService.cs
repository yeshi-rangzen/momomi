using MomomiAPI.Models.Enums;
using MomomiAPI.Services.Interfaces;
using System.Text;
using System.Text.Json;

namespace MomomiAPI.Services.Implementations
{
    public class PostHogAnalyticsService : IAnalyticsService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PostHogAnalyticsService> _logger;
        private readonly string _postHogApiKey;
        private readonly string _postHogHost;
        private readonly bool _isEnabled;

        public PostHogAnalyticsService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<PostHogAnalyticsService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            _postHogApiKey = configuration["PostHog:ApiKey"] ?? string.Empty;
            _postHogHost = configuration["PostHog:Host"] ?? "https://app.posthog.com";
            _isEnabled = !string.IsNullOrEmpty(_postHogApiKey) &&
                bool.Parse(configuration["PostHog:Enabled"] ?? "true");

            if (_isEnabled)
            {
                _logger.LogInformation("PostHog Analytics initialized with host: {Host}", _postHogHost);
            }
            else
            {
                _logger.LogWarning("PostHog Analytics is disabled or not configured");
            }
        }

        #region User Registration & Authentication
        public async Task TrackUserRegistrationAsync(Guid userId, UserRegistrationData data)
        {
            if (!_isEnabled) return;

            var properties = new
            {
                user_id = userId.ToString(),
                email_hash = HashEmail(data.Email),
                age = data.Age,
                gender = data.Gender.ToString(),
                heritage = data.Heritage.Select(h => h.ToString()).ToArray(),
                heritage_count = data.Heritage.Count,
                religion = data.Religion.Select(r => r.ToString()).ToArray(),
                languages = data.Languages.Select(l => l.ToString()).ToArray(),
                language_count = data.Languages.Count,
                hometown = data.Hometown,
                registration_method = data.RegistrationMethod,
                timestamp = data.RegistrationTimestamp,
                source = "backend_api"
            };

            await TrackEventAsync("user_registered", userId.ToString(), properties);

            _logger.LogInformation("Tracked user registration for user {UserId}", userId);
        }

        public async Task TrackEmailVerificationSentAsync(string email, int attemptCount)
        {
            if (!_isEnabled) return;

            var properties = new
            {
                email_hash = HashEmail(email),
                attempt_count = attemptCount,
                verification_method = "email_otp",
                timestamp = DateTime.UtcNow,
                source = "backend_api"
            };

            await TrackEventAsync("email_verification_sent", HashEmail(email), properties);
        }

        public async Task TrackUserLoginAsync(Guid userId, LoginData data)
        {
            if (!_isEnabled) return;

            var properties = new
            {
                user_id = userId.ToString(),
                email_hash = HashEmail(data.Email),
                login_method = data.LoginMethod,
                days_since_last_login = data.DaysSinceLastLogin,
                is_returning_user = data.DaysSinceLastLogin > 0,
                timestamp = data.LoginTimestamp,
                source = "backend_api"
            };

            await TrackEventAsync("user_login_success", userId.ToString(), properties);

            // Also track daily activity
            await TrackDailyActivityAsync(userId);
        }

        public async Task TrackDailyActivityAsync(Guid userId)
        {
            if (!_isEnabled) return;

            var properties = new
            {
                user_id = userId.ToString(),
                activity_date = DateTime.UtcNow.Date,
                timestamp = DateTime.UtcNow,
                source = "backend_api"
            };

            await TrackEventAsync("daily_activity_recorded", userId.ToString(), properties);
        }

        #endregion

        #region Discovery & Matching
        public async Task TrackLikeRecordedAsync(Guid userId, Guid likedUserId, LikeInteractionData data)
        {
            if (!_isEnabled) return;

            var properties = new
            {
                user_id = userId.ToString(),
                liked_user_id = likedUserId.ToString(),
                like_type = data.LikeType.ToString(),
                discovery_mode = data.DiscoveryMode,
                cultural_compatibility_score = data.CulturalCompatibilityScore,
                target_heritage = data.TargetHeritage?.Select(h => h.ToString()).ToArray(),
                is_match = data.IsMatch,
                interaction_type = "like",
                timestamp = data.InteractionTimestamp,
                source = "backend_api"
            };

            await TrackEventAsync("like_recorded", userId.ToString(), properties);

            if (data.LikeType == LikeType.SuperLike)
            {
                await TrackEventAsync("super_like_used", userId.ToString(), properties);
            }
        }

        public async Task TrackMatchCreatedAsync(Guid userId, Guid matchedUserId, MatchData data)
        {
            if (!_isEnabled) return;

            var properties = new
            {
                user_id = userId.ToString(),
                matched_user_id = matchedUserId.ToString(),
                match_type = data.MatchType,
                cultural_compatibility_score = data.CulturalCompatibilityScore,
                user1_heritage = data.User1Heritage.Select(h => h.ToString()).ToArray(),
                user2_heritage = data.User2Heritage.Select(h => h.ToString()).ToArray(),
                is_cross_cultural = data.IsCrossCultural,
                heritage_overlap = data.User1Heritage.Intersect(data.User2Heritage).Count(),
                timestamp = data.MatchTimestamp,
                source = "backend_api"
            };

            await TrackEventAsync("match_created", userId.ToString(), properties);

            // Also track for the matched user
            var matchedUserProperties = new
            {
                user_id = matchedUserId.ToString(),
                matched_user_id = userId.ToString(),
                match_type = data.MatchType,
                cultural_compatibility_score = data.CulturalCompatibilityScore,
                user1_heritage = data.User2Heritage.Select(h => h.ToString()).ToArray(),
                user2_heritage = data.User1Heritage.Select(h => h.ToString()).ToArray(),
                is_cross_cultural = data.IsCrossCultural,
                heritage_overlap = data.User1Heritage.Intersect(data.User2Heritage).Count(),
                timestamp = data.MatchTimestamp,
                source = "backend_api"
            };

            await TrackEventAsync("match_created", matchedUserId.ToString(), matchedUserProperties);

            _logger.LogInformation("Tracked match creation between users {UserId} and {MatchedUserId}",
                userId, matchedUserId);
        }

        public async Task TrackMessageDeliveredAsync(Guid senderId, Guid receiverId, MessageData data)
        {
            if (!_isEnabled) return;

            var properties = new
            {
                sender_id = senderId.ToString(),
                receiver_id = receiverId.ToString(),
                conversation_id = data.ConversationId.ToString(),
                message_type = data.MessageType,
                message_length = data.MessageLength,
                is_first_message = data.IsFirstMessage,
                processing_time_ms = data.ProcessingTime.TotalMilliseconds,
                timestamp = data.MessageTimestamp,
                source = "backend_api"
            };

            await TrackEventAsync("message_delivered", senderId.ToString(), properties);
        }

        #endregion

        #region Subscription & Monetization

        public async Task TrackSubscriptionUpgradedAsync(Guid userId, SubscriptionData data)
        {
            if (!_isEnabled) return;

            var properties = new
            {
                user_id = userId.ToString(),
                subscription_type = data.PlanType.ToString(),
                duration_months = data.DurationMonths,
                price_paid = data.PricePaid,
                payment_method = data.PaymentMethod,
                trigger_reason = data.TriggerReason,
                previous_subscription = data.PreviousSubscription.ToString(),
                upgrade_value = data.PricePaid * data.DurationMonths,
                timestamp = data.UpgradeTimestamp,
                source = "backend_api"
            };

            await TrackEventAsync("subscription_upgraded", userId.ToString(), properties);

            _logger.LogInformation("Tracked subscription upgrade for user {UserId} to {PlanType}",
                userId, data.PlanType);
        }

        public async Task TrackSubscriptionCancelledAsync(Guid userId, CancellationData data)
        {
            if (!_isEnabled) return;

            var properties = new
            {
                user_id = userId.ToString(),
                cancellation_reason = data.CancellationReason,
                days_subscribed = data.DaysSubscribed,
                cancelled_plan = data.CancelledPlan.ToString(),
                subscriber_lifetime_days = data.DaysSubscribed,
                timestamp = data.CancellationTimestamp,
                source = "backend_api"
            };

            await TrackEventAsync("subscription_cancelled", userId.ToString(), properties);
        }

        #endregion

        #region Retention & Performance

        public async Task TrackRetentionMilestoneAsync(Guid userId, RetentionData data)
        {
            if (!_isEnabled) return;

            var properties = new
            {
                user_id = userId.ToString(),
                milestone_type = data.MilestoneType,
                registration_date = data.RegistrationDate,
                first_return_date = data.FirstReturnDate,
                total_sessions_in_period = data.TotalSessionsInPeriod,
                days_since_registration = data.DaysSinceRegistration,
                return_frequency = data.TotalSessionsInPeriod / Math.Max(1, data.DaysSinceRegistration),
                timestamp = DateTime.UtcNow,
                source = "backend_api"
            };

            await TrackEventAsync($"retention_{data.MilestoneType}", userId.ToString(), properties);
        }

        public async Task TrackApiPerformanceAsync(string endpoint, TimeSpan responseTime, int statusCode, Guid? userId = null)
        {
            if (!_isEnabled) return;

            var properties = new
            {
                endpoint = endpoint,
                response_time_ms = responseTime.TotalMilliseconds,
                status_code = statusCode,
                is_slow = responseTime.TotalSeconds > 2,
                is_error = statusCode >= 400,
                user_id = userId?.ToString(),
                timestamp = DateTime.UtcNow,
                source = "backend_api"
            };

            var eventName = responseTime.TotalSeconds > 2 ? "api_request_slow" : "api_request_completed";
            await TrackEventAsync(eventName, userId?.ToString() ?? "system", properties);
        }

        #endregion

        #region Core Implementation

        private async Task TrackEventAsync(string eventName, string distinctId, object properties)
        {
            if (!_isEnabled) return;

            try
            {
                var payload = new
                {
                    api_key = _postHogApiKey,
                    @event = eventName,
                    distinct_id = distinctId,
                    properties = properties,
                    timestamp = DateTime.UtcNow
                };

                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_postHogHost}/capture/", content);

                if (!response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to track event {EventName}: {StatusCode} - {Response}",
                        eventName, response.StatusCode, responseContent);
                }
                else
                {
                    _logger.LogDebug("Successfully tracked event {EventName} for user {DistinctId}",
                        eventName, distinctId);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Network error tracking event {EventName}", eventName);
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogError(ex, "Timeout tracking event {EventName}", eventName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error tracking event {EventName}", eventName);
            }
        }

        public async Task<bool> IsHealthyAsync()
        {
            if (!_isEnabled) return true; // Consider disabled analytics as "healthy"

            try
            {
                // Test PostHog connectivity with a simple health check event
                var healthCheckPayload = new
                {
                    api_key = _postHogApiKey,
                    @event = "health_check",
                    distinct_id = "system",
                    properties = new
                    {
                        timestamp = DateTime.UtcNow,
                        source = "backend_health_check"
                    }
                };

                var json = JsonSerializer.Serialize(healthCheckPayload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await _httpClient.PostAsync($"{_postHogHost}/capture/", content, cts.Token);

                var isHealthy = response.IsSuccessStatusCode;

                if (!isHealthy)
                {
                    _logger.LogWarning("PostHog health check failed: {StatusCode}", response.StatusCode);
                }

                return isHealthy;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PostHog health check failed with exception");
                return false;
            }
        }

        private static string HashEmail(string email)
        {
            // Simple hash for privacy - use more sophisticated hashing in production
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(email.ToLowerInvariant()));
            return Convert.ToBase64String(hashedBytes)[..16]; // Take first 16 characters
        }

        #endregion
    }
}
