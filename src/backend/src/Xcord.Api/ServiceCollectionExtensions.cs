using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using StackExchange.Redis;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Xcord.Api;
using Xcord.Features;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;

namespace Xcord.Api;

public static class ServiceCollectionExtensions
{
    public static WebApplicationBuilder AddXcordServices(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var config = builder.Configuration;

        // Options
        AddOptionsRegistrations(services, config);

        // Database
        var dbOptions = config.GetSection(DatabaseOptions.SectionName)
            .Get<DatabaseOptions>() ?? throw new InvalidOperationException("Database configuration is required");
        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(dbOptions.ConnectionString));

        // Redis
        var redisOpts = config.GetSection(RedisOptions.SectionName)
            .Get<RedisOptions>() ?? throw new InvalidOperationException("Redis configuration is required");
        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var cfg = ConfigurationOptions.Parse(redisOpts.ConnectionString);
            cfg.AbortOnConnectFail = false;
            cfg.ConnectTimeout = 5000;
            cfg.SyncTimeout = 1000;
            cfg.ConnectRetry = 3;
            return ConnectionMultiplexer.Connect(cfg);
        });

        // JSON serialization
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new SnowflakeJsonConverter());
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        // Framework services
        services.AddOpenApi();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        services.AddHttpContextAccessor();
        services.AddHttpClient();

        // Request handlers (auto-discovery)
        services.AddRequestHandlers(typeof(FeaturesAssemblyMarker).Assembly);
        services.AddSingleton(Log.Logger);

        // Core services
        AddCoreServices(services, config);

        // Background services
        AddBackgroundServices(services);

        // GIF service
        AddGifService(services, config);

        // Presence
        services.AddSingleton<IPresenceService, RedisPresenceService>();
        services.AddSingleton<IPresenceNotifier, SignalRPresenceNotifier>();
        services.AddHostedService<PresenceCleanup>();

        // Notification settings
        services.AddScoped<INotificationSettingService, NotificationSettingService>();

        // Health checks
        services.AddHealthChecks()
            .AddCheck<DbHealthCheck>("database")
            .AddCheck<RedisHealthCheck>("redis")
            .AddCheck<MinioHealthCheck>("minio");

        // OpenTelemetry
        var instanceOpts = config.GetSection(InstanceOptions.SectionName).Get<InstanceOptions>();
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService("xcord-instance", serviceInstanceId: instanceOpts?.Domain ?? "unknown"))
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddConsoleExporter());

        // SignalR with Redis backplane
        services.AddSignalR(options => options.EnableDetailedErrors = builder.Environment.IsDevelopment())
            .AddJsonProtocol(options =>
            {
                // Snowflake IDs exceed JavaScript's Number.MAX_SAFE_INTEGER, so
                // the HTTP API uses SnowflakeJsonConverter to serialize them as strings.
                // SignalR must do the same:
                //   WriteAsString  — server-sent events write longs as "12345" so JS
                //                    doesn't lose precision on Snowflake IDs.
                //   AllowReadingFromString — hub methods accept longs that the frontend
                //                           sends as JSON strings.
                options.PayloadSerializerOptions.NumberHandling =
                    System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
                    | System.Text.Json.Serialization.JsonNumberHandling.WriteAsString;

                // Serialize enums as camelCase strings ("online", "idle", "dnd")
                // matching the frontend PresenceStatus type and the HTTP API convention.
                options.PayloadSerializerOptions.Converters.Add(
                    new System.Text.Json.Serialization.JsonStringEnumConverter(
                        System.Text.Json.JsonNamingPolicy.CamelCase));
            })
            .AddStackExchangeRedis(redisOpts.ConnectionString, options =>
                options.Configuration.ChannelPrefix = RedisChannel.Literal(redisOpts.ChannelPrefix));
        services.AddSingleton<Microsoft.AspNetCore.SignalR.IHubFilter, HubRateLimitFilter>();

        // Rate limiting
        AddRateLimiting(services, config);

        // CORS
        AddCors(services, config, builder.Environment);

        // Authentication & Authorization
        AddAuth(services, config);

        return builder;
    }

    private static void AddOptionsRegistrations(IServiceCollection services, IConfiguration config)
    {
        void Bind<T>(string section) where T : class =>
            services.AddOptions<T>().Bind(config.GetSection(section)).ValidateDataAnnotations().ValidateOnStart();

        Bind<DatabaseOptions>(DatabaseOptions.SectionName);
        Bind<JwtOptions>(JwtOptions.SectionName);
        Bind<StorageOptions>(StorageOptions.SectionName);
        Bind<RedisOptions>(RedisOptions.SectionName);
        Bind<LiveKitOptions>(LiveKitOptions.SectionName);
        Bind<CorsOptions>(CorsOptions.SectionName);
        Bind<InstanceOptions>(InstanceOptions.SectionName);
        Bind<SnowflakeOptions>(SnowflakeOptions.SectionName);
        Bind<RateLimitingOptions>(RateLimitingOptions.SectionName);
        Bind<HubOptions>(HubOptions.SectionName);
        Bind<GifOptions>(GifOptions.SectionName);
        Bind<EmailOptions>(EmailOptions.SectionName);
        Bind<OutboxOptions>(OutboxOptions.SectionName);

        // Tier options default to permissive when not provided (standalone instances)
        services.AddOptions<TierOptions>().Bind(config.GetSection(TierOptions.SectionName));

        services.AddOptions<EncryptionOptions>().Bind(config.GetSection(EncryptionOptions.SectionName));

        // Member billing (Stripe Connect for per-server subscriptions)
        services.AddOptions<MemberBillingOptions>().Bind(config.GetSection(MemberBillingOptions.SectionName));
    }

    private static void AddCoreServices(IServiceCollection services, IConfiguration config)
    {
        var snowflakeOpts = config.GetSection(SnowflakeOptions.SectionName)
            .Get<SnowflakeOptions>() ?? throw new InvalidOperationException("Snowflake configuration is required");

        services.AddSingleton(new SnowflakeIdGenerator(snowflakeOpts.WorkerId));
        services.AddSingleton<IKekProvider, FileKekProvider>();
        services.AddSingleton<EncryptionKeyHolder>();
        services.AddSingleton<IEncryptionService>(sp =>
            new PgCryptoEncryptionService(sp.GetRequiredService<EncryptionKeyHolder>().Key));

        services.AddScoped<IJwtService, JwtService>();
        services.AddSingleton<RsaKeySingleton>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IConversationResolver, ConversationResolver>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddHttpClient<ILiveKitService, LiveKitService>();
        services.AddSingleton<ISlowmodeService, RedisSlowmodeService>();
        services.AddScoped<Xcord.Features.Messages.IMessageProcessor, Xcord.Features.Messages.MessageProcessor>();
        services.AddSingleton<IStorageService, S3StorageService>();
        services.AddSingleton<IThumbnailService, ImageSharpThumbnailService>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddSingleton<IEmailService, SmtpEmailService>();
        services.AddScoped<IAutomodService, AutomodService>();
        services.AddScoped<IAutomodActionExecutor, AutomodActionExecutor>();
        services.AddScoped<ITimeoutService, TimeoutService>();
        services.AddSingleton<IEventDispatcher, SignalREventDispatcher>();
        services.AddSingleton<ISystemBroadcaster, SignalRSystemBroadcaster>();
        services.AddSingleton<SsrfSafeHttpClient>();
        services.AddSingleton<OpenGraphParser>();
        services.AddScoped<IMemberBillingService, MemberBillingService>();
        services.AddScoped<Xcord.Features.Billing.MemberBillingWebhookHandler>();
    }

    private static void AddBackgroundServices(IServiceCollection services)
    {
        services.AddHostedService<OutboxDispatcher>();
        services.AddHostedService<OutboxCleanup>();
        services.AddHostedService<AttachmentCleanup>();
        services.AddHostedService<ThumbnailProcessor>();
        services.AddHostedService<ThreadArchiver>();
        services.AddHostedService<EmbedExtractor>();
        services.AddHostedService<EventLifecycleService>();
        services.AddHostedService<EventNotifier>();
        services.AddHostedService<PollCloser>();
        services.AddHostedService<CallTimeoutService>();
    }

    private static void AddGifService(IServiceCollection services, IConfiguration config)
    {
        var gifOpts = config.GetSection(GifOptions.SectionName).Get<GifOptions>() ?? new GifOptions();
        services.AddSingleton<IGifService>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GifOptions>>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            return gifOpts.Provider.ToLowerInvariant() switch
            {
                "tenor" => new TenorGifService(factory, options, loggerFactory.CreateLogger<TenorGifService>()),
                "giphy" => new GiphyGifService(factory, options, loggerFactory.CreateLogger<GiphyGifService>()),
                _ => new NoOpGifService()
            };
        });
    }

    private static void AddRateLimiting(IServiceCollection services, IConfiguration config)
    {
        var opts = config.GetSection(RateLimitingOptions.SectionName)
            .Get<RateLimitingOptions>() ?? new RateLimitingOptions();

        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetSlidingWindowLimiter(ip, _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = opts.MaxRequests,
                    Window = TimeSpan.FromSeconds(opts.WindowSeconds),
                    SegmentsPerWindow = 2,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
            });
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });
    }

    private static void AddCors(IServiceCollection services, IConfiguration config, IWebHostEnvironment env)
    {
        var corsOpts = config.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

        if (corsOpts.AllowedOrigins.Length == 0 && !env.IsDevelopment())
            throw new InvalidOperationException("Cors:AllowedOrigins must not be empty in non-Development environments");

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                if (corsOpts.AllowedOrigins.Length > 0)
                {
                    policy.WithOrigins(corsOpts.AllowedOrigins)
                        .AllowAnyMethod().AllowAnyHeader().AllowCredentials();
                }
                else
                {
                    policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                }
            });
        });
    }

    private static void AddAuth(IServiceCollection services, IConfiguration config)
    {
        var jwtOpts = config.GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>() ?? throw new InvalidOperationException("JWT configuration is required");

        services.AddAuthentication(options =>
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
                ValidIssuer = jwtOpts.Issuer,
                ValidAudience = jwtOpts.Audience,
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "sub"
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        context.Token = accessToken;
                    return Task.CompletedTask;
                }
            };
        })
        .AddScheme<BotAuthenticationOptions, BotAuthenticationHandler>("Bot", _ => { })
        .AddScheme<AuthenticationSchemeOptions, TicketAuthHandler>("Ticket", _ => { });

        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser().Build();

            options.AddPolicy(Policies.User, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("email_confirmed", "true")
                .RequireAssertion(ctx => !ctx.User.HasClaim("bot", "true")));

            options.AddPolicy(Policies.Bot, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("bot", "true"));

            options.AddPolicy(Policies.Admin, policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("email_confirmed", "true")
                .RequireAssertion(ctx => !ctx.User.HasClaim("bot", "true"))
                .RequireClaim("admin", "true"));
        });
    }
}
