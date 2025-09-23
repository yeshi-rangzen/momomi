using MomomiAPI.Common.Caching;
using MomomiAPI.Common.Constants;
using MomomiAPI.Services.Implementations;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Extensions
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddMomomiServices(this IServiceCollection services)
        {
            // Core infrastructure services
            services.AddScoped<ICacheInvalidation, CacheInvalidationService>();

            // Authentication services
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IJwtService, JwtService>();


            // Discovery and matching services
            services.AddScoped<ISwipeService, SwipeService>();
            services.AddScoped<IDiscoveryService, DiscoveryService>();

            // User management services
            services.AddScoped<IUserService, UserService>();

            // Photo services
            services.AddScoped<IPhotoService, PhotoService>();

            // Messaging services
            services.AddScoped<IMatchService, MatchService>();
            services.AddScoped<IMessageService, MessageService>();

            // Safety services
            services.AddScoped<IReportingService, ReportingService>();

            // Notification services
            services.AddHttpClient<IPushNotificationService, ExpoPushNotificationService>();

            // Cache service (existing)
            services.AddScoped<ICacheService, UpstashCacheService>();

            // Subscription and usage services
            //services.AddScoped<ISubscriptionService, SubscriptionService>();


            return services;
        }

        public static IServiceCollection AddMomomiConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            // Validate and register configuration
            var momomiConfig = new MomomiConfiguration();
            configuration.Bind(momomiConfig);
            momomiConfig.Validate();

            services.AddSingleton(momomiConfig);

            // Register individual configuration sections
            services.Configure<SupabaseSettings>(configuration.GetSection("Supabase"));
            services.Configure<JwtSettings>(configuration.GetSection("JWT"));
            services.Configure<AppLimits>(configuration.GetSection("AppSettings"));

            return services;
        }

        public static IServiceCollection AddAnalyticsServices(this IServiceCollection services, IConfiguration configuration)
        {
            //// Register PostHog Analytics Service
            //services.AddHttpClient<IAnalyticsService, PostHogAnalyticsService>(client =>
            //{
            //    client.Timeout = TimeSpan.FromSeconds(10);
            //    client.DefaultRequestHeaders.Add("User-Agent", "Momomi-API/1.0");
            //});

            //// Register PostHog Health Check
            //services.AddScoped<PostHogHealthCheck>();

            return services;
        }
        // Configuration classes with validation
        public class MomomiConfiguration
        {
            public SupabaseSettings Supabase { get; set; } = new();
            public JwtSettings Jwt { get; set; } = new();
            public AppLimits AppSettings { get; set; } = new();

            public void Validate()
            {
                Supabase.Validate();
                Jwt.Validate();
                AppSettings.Validate();
            }
        }

        public class SupabaseSettings
        {
            public string Url { get; set; } = string.Empty;
            public string Key { get; set; } = string.Empty;
            public string ServiceRoleKey { get; set; } = string.Empty;
            public string Issuer { get; set; } = string.Empty;
            public string ProjectRef { get; set; } = string.Empty;
            public string JwtSecret { get; set; } = string.Empty;
            public string StorageUrl { get; set; } = string.Empty;
            public void Validate()
            {
                if (string.IsNullOrEmpty(Url))
                    throw new InvalidOperationException("Supabase URL is required");
                if (string.IsNullOrEmpty(Key))
                    throw new InvalidOperationException("Supabase Key is required");
                if (string.IsNullOrEmpty(ServiceRoleKey))
                    throw new InvalidOperationException("Supabase Service Role Key is required");
                if (string.IsNullOrEmpty(StorageUrl))
                    throw new InvalidOperationException("Supabase Storage URL is required");
            }
        }

        public class JwtSettings
        {
            public string SecretKey { get; set; } = string.Empty;
            public string Issuer { get; set; } = string.Empty;
            public string Audience { get; set; } = string.Empty;
            public int AccessTokenExpiryMinutes { get; set; } = 60;
            public int RefreshTokenExpiryDays { get; set; } = 30;

            public void Validate()
            {
                if (string.IsNullOrEmpty(SecretKey) || SecretKey.Length < 32)
                    throw new InvalidOperationException("JWT Secret Key must be at least 32 characters long");
                if (string.IsNullOrEmpty(Issuer))
                    throw new InvalidOperationException("JWT Issuer is required");
                if (string.IsNullOrEmpty(Audience))
                    throw new InvalidOperationException("JWT Audience is required");
                if (AccessTokenExpiryMinutes <= 0)
                    throw new InvalidOperationException("Access token expiry must be positive");
                if (RefreshTokenExpiryDays <= 0)
                    throw new InvalidOperationException("Refresh token expiry must be positive");
            }
        }

        public class AppLimits
        {
            public string DefaultPhotoUrl { get; set; } = AppConstants.DefaultValues.DefaultPhotoUrl;
            public int MaxPhotosPerUser { get; set; } = AppConstants.Limits.MaxPhotosPerUser;
            public int MaxDistance { get; set; } = AppConstants.Limits.MaxDistance;
            public int MinAge { get; set; } = AppConstants.Limits.MinAge;
            public int MaxAge { get; set; } = AppConstants.Limits.MaxAge;

            public void Validate()
            {
                if (MaxPhotosPerUser < 1)
                    throw new InvalidOperationException("Must allow at least 1 photo per user");
                if (MinAge < 18)
                    throw new InvalidOperationException("Minimum age must be at least 18");
                if (MaxAge <= MinAge)
                    throw new InvalidOperationException("Maximum age must be greater than minimum age");
                if (MaxDistance < 1)
                    throw new InvalidOperationException("Maximum distance must be at least 1 km");
            }
        }

    }
}
