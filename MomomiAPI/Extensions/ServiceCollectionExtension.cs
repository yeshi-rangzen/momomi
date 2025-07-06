using MomomiAPI.Helpers;
using MomomiAPI.Services.Implementations;
using MomomiAPI.Services.Interfaces;

namespace MomomiAPI.Extensions
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddMomomiServices(this IServiceCollection services)
        {
            // Register services here
            services.AddScoped<IAuthService, SupabaseAuthService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IMatchingService, MatchingService>();
            services.AddScoped<IMessageService, MessageService>();
            services.AddScoped<IPhotoService, CloudinaryPhotoService>();
            services.AddScoped<ICacheService, UpstashCacheService>();
            services.AddScoped<IJwtService, JwtService>();
            services.AddScoped<ISubscriptionService, SubscriptionService>();
            services.AddScoped<IBlockingService, BlockingService>();
            services.AddScoped<IPushNotificationService, PushNotificationService>();

            // Register helpers
            services.AddScoped<MatchingAlgorithm>();

            // HTTP Client for push notifications
            services.AddHttpClient<IPushNotificationService, PushNotificationService>();

            return services;
        }

        public static IServiceCollection AddMomomiConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            // Register configuration settings
            services.Configure<CloudinarySettings>(configuration.GetSection("Cloudinary"));
            services.Configure<SupabaseSettings>(configuration.GetSection("Supabase"));
            services.Configure<JwtSettings>(configuration.GetSection("JWT"));
            services.Configure<AppSettings>(configuration.GetSection("AppSettings"));
            services.Configure<PushNotificationSettings>(configuration.GetSection("PushNotifications"));

            return services;
        }

        // Configuration classes
        public class CloudinarySettings
        {
            public string CloudName { get; set; } = string.Empty;
            public string ApiKey { get; set; } = string.Empty;
            public string ApiSecret { get; set; } = string.Empty;
        }

        public class SupabaseSettings
        {
            public string Url { get; set; } = string.Empty;
            public string Key { get; set; } = string.Empty;
            public string ServiceRoleKey { get; set; } = string.Empty;
        }

        public class JwtSettings
        {
            public string SecretKey { get; set; } = string.Empty;
            public string Issuer { get; set; } = string.Empty;
            public string Audience { get; set; } = string.Empty;
            public int ExpirationHours { get; set; } = 24;
        }

        public class AppSettings
        {
            public string DefaultPhotoUrl { get; set; } = string.Empty;
            public int MaxPhotosPerUser { get; set; } = 6;
            public int MaxDistance { get; set; } = 100;
            public int MinAge { get; set; } = 18;
            public int MaxAge { get; set; } = 65;
        }

        public class PushNotificationSettings
        {
            public string ExpoAccessToken { get; set; } = string.Empty;
            public string FcmServerKey { get; set; } = string.Empty;
            public bool EnablePushNotifications { get; set; } = true;
        }
    }
}
