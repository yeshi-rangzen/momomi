using MomomiAPI.Middleware;

namespace MomomiAPI.Extensions
{
    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseAnalyticsMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AnalyticsMiddleware>();
        }
    }
}