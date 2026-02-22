using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Threading.RateLimiting;
using Xcord.Api;
using Xcord.Features;
using Xcord.Infrastructure.Services;
using Xcord;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Xcord.Instance")
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Configure Options with validation
builder.Services.AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<StorageOptions>()
    .Bind(builder.Configuration.GetSection(StorageOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<RedisOptions>()
    .Bind(builder.Configuration.GetSection(RedisOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<LiveKitOptions>()
    .Bind(builder.Configuration.GetSection(LiveKitOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<CorsOptions>()
    .Bind(builder.Configuration.GetSection(CorsOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<InstanceOptions>()
    .Bind(builder.Configuration.GetSection(InstanceOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<SnowflakeOptions>()
    .Bind(builder.Configuration.GetSection(SnowflakeOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<RateLimitingOptions>()
    .Bind(builder.Configuration.GetSection(RateLimitingOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<HubOptions>()
    .Bind(builder.Configuration.GetSection(HubOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<GifOptions>()
    .Bind(builder.Configuration.GetSection(GifOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<EmailOptions>()
    .Bind(builder.Configuration.GetSection(EmailOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<EncryptionOptions>()
    .Bind(builder.Configuration.GetSection(EncryptionOptions.SectionName));

builder.Services.AddOptions<OutboxOptions>()
    .Bind(builder.Configuration.GetSection(OutboxOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Configure Database
var databaseOptions = builder.Configuration
    .GetSection(DatabaseOptions.SectionName)
    .Get<DatabaseOptions>() ?? throw new InvalidOperationException("Database configuration is required");

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(databaseOptions.ConnectionString);
});

// Configure Redis
var redisOptions = builder.Configuration
    .GetSection(RedisOptions.SectionName)
    .Get<RedisOptions>() ?? throw new InvalidOperationException("Redis configuration is required");

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configurationOptions = ConfigurationOptions.Parse(redisOptions.ConnectionString);
    configurationOptions.AbortOnConnectFail = false;
    configurationOptions.ConnectTimeout = 5000;
    configurationOptions.SyncTimeout = 1000;
    configurationOptions.ConnectRetry = 3;
    return ConnectionMultiplexer.Connect(configurationOptions);
});

// Serialize snowflake IDs as strings (they exceed JS Number.MAX_SAFE_INTEGER)
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new SnowflakeJsonConverter());
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// Add services to the container
builder.Services.AddOpenApi();

// Register exception handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Register Request Handlers
builder.Services.AddRequestHandlers(typeof(FeaturesAssemblyMarker).Assembly);

// Register Serilog logger
builder.Services.AddSingleton(Log.Logger);

// Register HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// Register Snowflake ID generator
var snowflakeOptions = builder.Configuration
    .GetSection(SnowflakeOptions.SectionName)
    .Get<SnowflakeOptions>() ?? throw new InvalidOperationException("Snowflake configuration is required");

builder.Services.AddSingleton(new SnowflakeIdGenerator(snowflakeOptions.WorkerId));

// Register Encryption Service (deferred — key loaded at startup from config or DB)
builder.Services.AddSingleton<EncryptionKeyHolder>();
builder.Services.AddSingleton<IEncryptionService>(sp =>
    new PgCryptoEncryptionService(sp.GetRequiredService<EncryptionKeyHolder>().Key));

// Register JWT Service and RSA Key Singleton
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddSingleton<RsaKeySingleton>();

// Register Permission Service
builder.Services.AddScoped<IPermissionService, PermissionService>();

// Register Conversation Resolver
builder.Services.AddScoped<Xcord.Infrastructure.Services.IConversationResolver, Xcord.Infrastructure.Services.ConversationResolver>();

// Register Current User Service
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Register LiveKit Service
builder.Services.AddHttpClient<Xcord.Infrastructure.Services.ILiveKitService, Xcord.Infrastructure.Services.LiveKitService>();

// Register Slowmode Service
builder.Services.AddSingleton<Xcord.Infrastructure.Services.ISlowmodeService, Xcord.Infrastructure.Services.RedisSlowmodeService>();

// Register Message Processor
builder.Services.AddScoped<Xcord.Features.Messages.IMessageProcessor, Xcord.Features.Messages.MessageProcessor>();

// Register Storage Service
builder.Services.AddSingleton<Xcord.Infrastructure.Services.IStorageService, Xcord.Infrastructure.Services.S3StorageService>();

// Register Thumbnail Service
builder.Services.AddSingleton<Xcord.Infrastructure.Services.IThumbnailService, Xcord.Infrastructure.Services.ImageSharpThumbnailService>();

// Register Outbox Writer
builder.Services.AddScoped<Xcord.Infrastructure.Services.IOutboxWriter, Xcord.Infrastructure.Services.OutboxWriter>();

// Register Email Service
builder.Services.AddSingleton<Xcord.Infrastructure.Services.IEmailService, Xcord.Infrastructure.Services.SmtpEmailService>();

// Register Automod Service
builder.Services.AddScoped<Xcord.Infrastructure.Services.IAutomodService, Xcord.Infrastructure.Services.AutomodService>();

// Register Automod Action Executor
builder.Services.AddScoped<Xcord.Infrastructure.Services.IAutomodActionExecutor, Xcord.Infrastructure.Services.AutomodActionExecutor>();

// Register Timeout Service
builder.Services.AddScoped<Xcord.Infrastructure.Services.ITimeoutService, Xcord.Infrastructure.Services.TimeoutService>();

// Register Event Dispatcher
builder.Services.AddSingleton<IEventDispatcher, SignalREventDispatcher>();

// Register System Broadcaster
builder.Services.AddSingleton<Xcord.Infrastructure.Services.ISystemBroadcaster, SignalRSystemBroadcaster>();

// Register Outbox Background Services
builder.Services.AddHostedService<Xcord.Infrastructure.Services.OutboxDispatcher>();
builder.Services.AddHostedService<Xcord.Infrastructure.Services.OutboxCleanup>();

// Register Attachment Cleanup Background Service
builder.Services.AddHostedService<Xcord.Infrastructure.Services.AttachmentCleanup>();

// Register Thumbnail Processor Background Service
builder.Services.AddHostedService<Xcord.Infrastructure.Services.ThumbnailProcessor>();

// Register Thread Archiver Background Service
builder.Services.AddHostedService<Xcord.Infrastructure.Services.ThreadArchiver>();

// Register Embed Extractor Background Service
builder.Services.AddHostedService<Xcord.Infrastructure.Services.EmbedExtractor>();

// Register Event Lifecycle and Notifier Background Services
builder.Services.AddHostedService<Xcord.Infrastructure.Services.EventLifecycleService>();
builder.Services.AddHostedService<Xcord.Infrastructure.Services.EventNotifier>();

// Register Poll Closer Background Service
builder.Services.AddHostedService<Xcord.Infrastructure.Services.PollCloser>();

// Register Call Timeout Background Service
builder.Services.AddHostedService<Xcord.Infrastructure.Services.CallTimeoutService>();

// Register SSRF-safe HTTP client and OpenGraph parser
builder.Services.AddSingleton<Xcord.Infrastructure.Services.SsrfSafeHttpClient>();
builder.Services.AddSingleton<Xcord.Infrastructure.Services.OpenGraphParser>();

// Register GIF Service based on provider configuration
var gifOptions = builder.Configuration
    .GetSection(GifOptions.SectionName)
    .Get<GifOptions>() ?? new GifOptions();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<Xcord.Infrastructure.Services.IGifService>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GifOptions>>();
    var logger = sp.GetRequiredService<ILoggerFactory>();

    return gifOptions.Provider.ToLowerInvariant() switch
    {
        "tenor" => new Xcord.Infrastructure.Services.TenorGifService(
            httpClientFactory,
            options,
            logger.CreateLogger<Xcord.Infrastructure.Services.TenorGifService>()),
        "giphy" => new Xcord.Infrastructure.Services.GiphyGifService(
            httpClientFactory,
            options,
            logger.CreateLogger<Xcord.Infrastructure.Services.GiphyGifService>()),
        _ => new Xcord.Infrastructure.Services.NoOpGifService()
    };
});

// Register Presence Services
builder.Services.AddSingleton<Xcord.Infrastructure.Services.IPresenceService, Xcord.Infrastructure.Services.RedisPresenceService>();
builder.Services.AddSingleton<Xcord.Infrastructure.Services.IPresenceNotifier, Xcord.Api.SignalRPresenceNotifier>();
builder.Services.AddHostedService<Xcord.Infrastructure.Services.PresenceCleanup>();

// Register Notification Settings Service
builder.Services.AddScoped<Xcord.Infrastructure.Services.INotificationSettingService, Xcord.Infrastructure.Services.NotificationSettingService>();

// Configure Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<DbHealthCheck>("database")
    .AddCheck<RedisHealthCheck>("redis")
    .AddCheck<MinioHealthCheck>("minio");

// Configure OpenTelemetry
var instanceOptions = builder.Configuration
    .GetSection(InstanceOptions.SectionName)
    .Get<InstanceOptions>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("xcord-instance", serviceInstanceId: instanceOptions?.Domain ?? "unknown"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter();
    });

// Register SignalR with Redis backplane
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
})
.AddStackExchangeRedis(redisOptions.ConnectionString, options =>
{
    options.Configuration.ChannelPrefix = RedisChannel.Literal(redisOptions.ChannelPrefix);
});

// Register Hub Rate Limit Filter (SignalR resolves IHubFilter from DI)
builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IHubFilter, HubRateLimitFilter>();

// Configure Rate Limiting
var rateLimitingOptions = builder.Configuration
    .GetSection(RateLimitingOptions.SectionName)
    .Get<RateLimitingOptions>() ?? new RateLimitingOptions();

builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetSlidingWindowLimiter(ipAddress, _ =>
            new SlidingWindowRateLimiterOptions
            {
                PermitLimit = rateLimitingOptions.MaxRequests,
                Window = TimeSpan.FromSeconds(rateLimitingOptions.WindowSeconds),
                SegmentsPerWindow = 2,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// Configure CORS
var corsOptions = builder.Configuration
    .GetSection(CorsOptions.SectionName)
    .Get<CorsOptions>() ?? new CorsOptions();

if (corsOptions.AllowedOrigins.Length == 0 && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException("Cors:AllowedOrigins must not be empty in non-Development environments");
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (corsOptions.AllowedOrigins.Length > 0)
        {
            policy.WithOrigins(corsOptions.AllowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
        else
        {
            // Development only: allow any origin without credentials
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        }
    });
});

// Configure Authentication & Authorization
var jwtOptions = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>() ?? throw new InvalidOperationException("JWT configuration is required");

// Note: JWT authentication will be configured after RSA key is loaded at startup
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidAudience = jwtOptions.Audience,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = "sub"
        // IssuerSigningKey will be set after app startup when RSA key is loaded
    };

    // Enable JWT authentication for SignalR
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
})
.AddScheme<BotAuthenticationOptions, BotAuthenticationHandler>("Bot", options => { })
.AddScheme<AuthenticationSchemeOptions, TicketAuthHandler>("Ticket", options => { });

builder.Services.AddAuthorization(options =>
{
    // Default: just authenticated (for auth-flow endpoints like ConfirmEmail, Logout)
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy(Policies.User, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("email_confirmed", "true")
        .RequireAssertion(ctx => !ctx.User.HasClaim("bot", "true")));

    options.AddPolicy(Policies.Bot, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("bot", "true"));

    // Admin = User + admin claim
    options.AddPolicy(Policies.Admin, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("email_confirmed", "true")
        .RequireAssertion(ctx => !ctx.User.HasClaim("bot", "true"))
        .RequireClaim("admin", "true"));
});

var app = builder.Build();

// Bootstrap: migrate database, ensure RSA key pair, load key for JWT validation
// Skip entirely in Testing environment — app.Services access triggers a premature host build
// in WebApplicationFactory's deferred host model, before test configuration is applied.
// The test fixture handles DB schema, RSA keys, and JWT configuration directly.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await db.Database.MigrateAsync();
    Log.Information("Database migrations applied");

    // Ensure RSA key pair exists
    var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
    await jwtService.EnsureRsaKeyPairAsync();
    Log.Information("RSA key pair verified");

    // Load RSA public key into singleton for JWT validation
    var rsaKeySingleton = scope.ServiceProvider.GetRequiredService<RsaKeySingleton>();
    var publicKeySetting = db.SystemSettings.FirstOrDefault(s => s.Key == "RsaPublicKey");
    if (publicKeySetting != null)
    {
        rsaKeySingleton.LoadPublicKey(publicKeySetting.Value);
        Log.Information("RSA public key loaded for JWT validation");
    }

    // Configure JWT validation with the loaded RSA key
    var rsaKey = app.Services.GetRequiredService<RsaKeySingleton>();
    var jwtBearerOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions>>();
    var currentOptions = jwtBearerOptions.Get(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme);
    if (currentOptions != null)
    {
        currentOptions.TokenValidationParameters.IssuerSigningKey = rsaKey.GetPublicKey();
    }

    // Bootstrap encryption key: config → DB → generate on first boot
    var encKeyHolder = app.Services.GetRequiredService<EncryptionKeyHolder>();
    var configKey = builder.Configuration.GetSection("Encryption:EncryptionKey").Value;
    var dbKey = db.SystemSettings.FirstOrDefault(s => s.Key == "EncryptionKey");

    if (!string.IsNullOrEmpty(configKey))
    {
        // Config-provided key (standalone mode) — store in DB if not already there
        if (dbKey == null)
        {
            db.SystemSettings.Add(new Xcord.Entities.SystemSetting
            {
                Key = "EncryptionKey",
                Value = configKey,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }
        encKeyHolder.SetKey(configKey);
        Log.Information("Encryption key loaded from configuration");
    }
    else if (dbKey != null)
    {
        // Key already in DB (normal restart)
        encKeyHolder.SetKey(dbKey.Value);
        Log.Information("Encryption key loaded from database");
    }
    else
    {
        // First boot — generate new key
        var newKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        db.SystemSettings.Add(new Xcord.Entities.SystemSetting
        {
            Key = "EncryptionKey",
            Value = newKey,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        encKeyHolder.SetKey(newKey);
        Log.Information("Generated new encryption key on first boot");
    }
}

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Forwarded headers (must be first to correctly resolve client IPs behind reverse proxy)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

// Middleware Stack (exact order per architecture)
// 1. Global Exception Handler
app.UseExceptionHandler();

// 2. Serilog Request Logging
app.UseSerilogRequestLogging();

// 3. Security Headers
app.UseSecurityHeaders();

// 4. Rate Limiting
app.UseRateLimiter();

// 5. HTTPS Redirection (Production only)
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// 6. CORS
app.UseCors();

// 6.5 Access Token Cookie → Authorization Header
app.Use(async (context, next) =>
{
    if (!context.Request.Headers.ContainsKey("Authorization") &&
        context.Request.Cookies.TryGetValue("access_token", out var token))
    {
        context.Request.Headers.Authorization = $"Bearer {token}";
    }
    await next();
});

// 7. Authentication
app.UseAuthentication();

// 8. Authorization
app.UseAuthorization();

// 9. Static Files
app.UseStaticFiles();

// Map Health Endpoint
app.MapHealthEndpoint();

// Map All Handler Endpoints (auto-discovery)
app.MapHandlerEndpoints(typeof(FeaturesAssemblyMarker).Assembly);

// Map SignalR Hub
app.MapHub<MainHub>("/hubs/main");

// 10. Admin SPA Fallback (for SPA routing)
app.MapWhen(
    ctx => ctx.Request.Path.StartsWithSegments("/admin"),
    adminApp =>
    {
        adminApp.Run(async context =>
        {
            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync("wwwroot/admin/index.html");
        });
    });

// 11. Chat Client SPA Fallback (for SPA routing)
app.MapFallbackToFile("index.html");

try
{
    Log.Information("Starting Xcord.Api");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Make Program accessible for WebApplicationFactory in integration tests
public partial class Program { }
