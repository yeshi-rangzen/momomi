using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using MomomiAPI.Data;
using MomomiAPI.Extensions;
using MomomiAPI.HealthChecks;
using MomomiAPI.Hubs;
using MomomiAPI.Services.Interfaces;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    // This is to prevent circular reference issues
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Momomi API",
        Version = "v1",
        Description = "Dating app API for Himalayan and Northeast Indian communities",
    });

    // Add JWT authentication to Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>() // No specific scopes required
        }
    });
});

// Database
builder.Services.AddDbContext<MomomiDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null);
        });

    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});


// Redis configuration - Updated for Upstash
builder.Services.AddSingleton<IConnectionMultiplexer>(provider =>
{
    try
    {
        // Get configuration
        var configuration = builder.Configuration;
        var redisEndpoint = configuration["Upstash:RedisEndpoint"] ??
                           configuration.GetConnectionString("Redis");
        var redisPassword = configuration["Upstash:RedisPassword"];

        // Build connection string for Upstash Redis
        var configurationOptions = new ConfigurationOptions
        {
            EndPoints = { redisEndpoint! },
            Password = redisPassword,
            Ssl = true, // Enable SSL for Upstash
            SslProtocols = System.Security.Authentication.SslProtocols.Tls12,
            AbortOnConnectFail = false, // Allow retries
            ConnectRetry = 3,
            ConnectTimeout = 10000, // 10 seconds
            SyncTimeout = 5000, // 5 seconds
            AsyncTimeout = 5000, // 5 seconds
            ReconnectRetryPolicy = new ExponentialRetry(5000), // Exponential backoff
            KeepAlive = 60, // Keep connection alive
            DefaultDatabase = 0
        };

        // Add logging for debugging
        var logger = provider.GetService<ILogger<Program>>();
        logger?.LogInformation("Connecting to Redis at: {Endpoint}", redisEndpoint);

        var multiplexer = ConnectionMultiplexer.Connect(configurationOptions);

        // Test connection
        var database = multiplexer.GetDatabase();
        database.Ping();

        logger?.LogInformation("Successfully connected to Redis");
        return multiplexer;
    }
    catch (Exception ex)
    {
        var logger = provider.GetService<ILogger<Program>>();
        logger?.LogError(ex, "Failed to connect to Redis: {Message}", ex.Message);

        // Return a null multiplexer or throw - depending on your preference
        return null;
        //throw new InvalidOperationException("Redis connection failed", ex);
    }
});

// Supabase
// User Client (with anon key) - for operations that work with Supabase Auth
builder.Services.AddKeyedSingleton<Supabase.Client>("UserClient", (provider, key) =>
{
    var supabaseConfig = builder.Configuration.GetSection("Supabase");
    return new Supabase.Client(
        supabaseConfig["Url"]!,
        supabaseConfig["Key"]!, // Anon key
        new Supabase.SupabaseOptions
        {
            AutoConnectRealtime = true,
            AutoRefreshToken = true
        }
    );
});

// Admin Client (with service role key) - for storage operations and health checks
builder.Services.AddKeyedSingleton<Supabase.Client>("AdminClient", (provider, key) =>
{
    var supabaseConfig = builder.Configuration.GetSection("Supabase");
    return new Supabase.Client(
        supabaseConfig["Url"]!,
        supabaseConfig["ServiceRoleKey"]!, // Service role key
        new Supabase.SupabaseOptions
        {
            AutoConnectRealtime = false, // Usually don't need realtime for admin
            AutoRefreshToken = false
        }
    );
});

builder.Services.AddSingleton<Supabase.Client>(provider =>
    provider.GetRequiredKeyedService<Supabase.Client>("UserClient"));


// Authentication  
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSecretKey = builder.Configuration["Jwt:SecretKey"];
        if (string.IsNullOrEmpty(jwtSecretKey))
        {
            throw new ArgumentNullException(nameof(jwtSecretKey), "JWT Secret key cannot be null or empty.");
        }

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
            ClockSkew = TimeSpan.FromMinutes(5)
        };

        // Configure SignalR authentication
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chathub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },

            OnTokenValidated = async context =>
            {
                var jwtService = context.HttpContext.RequestServices.GetRequiredService<IJwtService>();
                var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

                try
                {
                    //Check if token is blacklisted
                    var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                    if (!string.IsNullOrEmpty(jti))
                    {
                        var isBlacklisted = await jwtService.IsTokenBlacklistedAsync(jti);
                        if (isBlacklisted)
                        {
                            logger.LogWarning("Blacklisted token attempted access: {Jti}", jti);
                            context.Fail("Token has been revoked");
                            return;
                        }
                    }

                    // Check for user revocation
                    var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (Guid.TryParse(userIdClaim, out var userId))
                    {
                        // Validate user exists and is active
                        var isActiveUser = await userService.IsActiveUser(userId);
                        if (isActiveUser == null)
                        {
                            logger.LogWarning("Token valid but user not found: {UserId}", userId);
                            context.Fail("User not found");
                            return;
                        }

                        if (isActiveUser == false)
                        {
                            logger.LogWarning("Token valid but user is inactive: {UserId}", userId);
                            context.Fail("User account is inactive");
                            return;
                        }

                        // Check if all user tokens were revoked after this token was issued
                        var issuedAtClaim = context.Principal?.FindFirst(JwtRegisteredClaimNames.Iat)?.Value;
                        if (long.TryParse(issuedAtClaim, out var issuedAt))
                        {
                            var cacheService = context.HttpContext.RequestServices.GetRequiredService<ICacheService>();
                            var revocationKey = $"user_token_revocation_{userId}";
                            var revocationTimestamp = await cacheService.GetAsync<long?>(revocationKey);

                            if (revocationTimestamp.HasValue && issuedAt < revocationTimestamp.Value)
                            {
                                logger.LogWarning("Token issued before user revocation: {UserId}", userId);
                                context.Fail("Token has been revoked");
                                return;
                            }
                        }

                        logger.LogDebug("Token validated successfully for user: {UserId}", userId);
                    }
                    else
                    {
                        logger.LogWarning("Token missing or invalid user ID claim");
                        context.Fail("Invalid token claims");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error during token validation");
                    context.Fail("Token validation failed");
                }
            },

            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("JWT Authentication failed: {Exception}", context.Exception.Message);

                if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                {
                    context.Response.Headers.Append("Token-Expired", "true");
                }

                return Task.CompletedTask;
            },

            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = 401;
                context.Response.ContentType = "application/json";

                var response = new
                {
                    error = "unauthorized",
                    message = "Invalid or missing authentication token",
                    timestamp = DateTime.UtcNow
                };

                return context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
            }
        };
    });

// Authorization
builder.Services.AddAuthorization();

// IMemoryCache registration
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1000; // Limit memory cache size
    options.CompactionPercentage = 0.25; // Compact when 75% full
});

// Add Momomi Services
builder.Services.AddMomomiServices();
builder.Services.AddMomomiConfiguration(builder.Configuration);
builder.Services.AddResponseCompression();

// SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 1024 * 1024; // 1MB
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });

    if (builder.Environment.IsDevelopment())
    {
        options.AddPolicy("Development",
            policy =>
            {
                policy.WithOrigins(
                        "http://localhost:3000",
                        "http://localhost:8081",
                        "http://192.168.0.164:8081", // Add your IP
                        "exp://192.168.0.164:8081"   // Expo Go format
                         )
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            });
    }
});

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    // Strict rate limiting for OTP requests
    options.AddFixedWindowLimiter("OtpPolicy", opt =>
    {
        opt.PermitLimit = 3; // 3 OTP requests per window
        opt.Window = TimeSpan.FromHours(1); // 1 hour window
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0; // No queuing for OTP requests
    });

    // General auth rate limiting
    options.AddFixedWindowLimiter("AuthPolicy", opt =>
    {
        opt.PermitLimit = 5; // 5 auth requests per minute
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 2;
    });

    // General API rate limiting
    options.AddFixedWindowLimiter("GeneralPolicy", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 10;
    });
});

// Register health check services
builder.Services.AddScoped<RedisHealthCheck>();
builder.Services.AddScoped<SupabaseHealthCheck>();
builder.Services.AddScoped<SupabaseStorageHealthCheck>();

// Register Analytics Services
builder.Services.AddAnalyticsServices(builder.Configuration);

// Register Retention Tracking Background Service
//builder.Services.AddHostedService<RetentionTrackingService>();

// Health checks using custom classes
builder.Services.AddHealthChecks()
    .AddDbContextCheck<MomomiDbContext>("database", HealthStatus.Unhealthy, new[] { "db", "database" })
    .AddCheck<RedisHealthCheck>("redis", tags: new[] { "cache", "redis" })
    .AddCheck<SupabaseHealthCheck>("supabase", tags: new[] { "auth", "supabase" })
    .AddCheck<SupabaseStorageHealthCheck>("supabase-storage", tags: new[] { "storage", "supabase" });
//.AddCheck<PostHogHealthCheck>("posthog", tags: new[] { "analytics", "posthog" });


var app = builder.Build();
//app.UseAnalyticsMiddleware();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    //app.UseCors("Development");
    app.UseCors(policy => policy
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());
}
else
{
    app.UseCors("AllowAll");
    app.UseHttpsRedirection();
}


app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseResponseCompression();


app.MapControllers().RequireRateLimiting("GeneralPolicy");
// Auth endpoints with stricter rate limiting
app.MapControllerRoute(
name: "auth",
pattern: "api/auth/{action}",
defaults: new { controller = "Auth" })
.RequireRateLimiting("AuthPolicy");

app.MapHub<ChatHub>("/chatHub");

// MAP HEALTH CHECK ENDPOINTS HERE - This is crucial!
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var response = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            checks = report.Entries.Select(x => new
            {
                name = x.Key,
                status = x.Value.Status.ToString(),
                description = x.Value.Description,
                duration = Math.Round(x.Value.Duration.TotalMilliseconds, 2),
                tags = x.Value.Tags,
                data = x.Value.Data
            }),
            totalDuration = Math.Round(report.TotalDuration.TotalMilliseconds, 2)
        };

        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            WriteIndented = true
        }));
    }
});

// Auto-migrate database on startup (Development only)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<MomomiDbContext>();
    try
    {
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database");
    }
    app.Urls.Add("http://0.0.0.0:5029");
    app.Urls.Add("http://localhost:5029");
}

app.Run();
