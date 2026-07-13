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
using Xcord.Api.Authorization;
using Xcord.Captcha;
using Xcord.Captcha.AspNetCore;
using Xcord.Features;
using Xcord.Infrastructure.Data;
using Xcord.Infrastructure.Options;
using Xcord.Infrastructure.Services;
using Xcord.Infrastructure.Services.Bots;
using Xcord.Infrastructure.Services.Discord;

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
            // Apply per-instance ACL credentials when provisioned by the hub.
            // When Username/Password are absent, the default connection (no auth) is used.
            if (!string.IsNullOrWhiteSpace(redisOpts.Username))
                cfg.User = redisOpts.Username;
            if (!string.IsNullOrWhiteSpace(redisOpts.Password))
                cfg.Password = redisOpts.Password;
            return ConnectionMultiplexer.Connect(cfg);
        });

        // Captcha (ghost-font, portable module) - guards public registration
        var captchaEnabled = config.GetValue<bool>("Captcha:Enabled", true);
        services.AddGhostFontCaptcha(o =>
        {
            o.Enabled = captchaEnabled;
            o.KeyPrefix = $"{redisOpts.ChannelPrefix}captcha:";
        });
        if (captchaEnabled) services.UseRedisCaptchaStore();

        // JSON serialization - explicit camelCase + converters
        services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            options.SerializerOptions.Converters.Add(new SnowflakeJsonConverter());
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        // Framework services
        services.AddOpenApi();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
        services.AddHttpContextAccessor();
        services.AddHttpClient();
        services.AddHttpClient("OutgoingWebhooks", client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Xcord-Webhook/1.0");
            client.Timeout = TimeSpan.FromSeconds(20);
        });
        services.AddHttpClient("BotInteraction", client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Xcord-BotInteraction/1.0");
            client.Timeout = TimeSpan.FromSeconds(10);
        });

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
                .AddHttpClientInstrumentation());

        // SignalR with Redis backplane
        services.AddSignalR(options => options.EnableDetailedErrors = builder.Environment.IsDevelopment())
            .AddJsonProtocol(options =>
            {
                // Snowflake IDs exceed JavaScript's Number.MAX_SAFE_INTEGER.
                // Use the same SnowflakeJsonConverter as the HTTP API so only
                // long fields are stringified (not ints, not permission bitmasks).
                // AllowReadingFromString lets the frontend send IDs as strings.
                options.PayloadSerializerOptions.NumberHandling =
                    System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString;
                options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                options.PayloadSerializerOptions.Converters.Add(new SnowflakeJsonConverter());

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
        Bind<AuthOptions>(AuthOptions.SectionName);

        // Admin options are optional - hub-managed instances get admin via provisioning
        services.AddOptions<AdminOptions>().Bind(config.GetSection(AdminOptions.SectionName));

        // Dev users are optional - only used in Development to seed test accounts
        services.AddOptions<DevUsersOptions>().Bind(config.GetSection(DevUsersOptions.SectionName));

        // Tier options default to permissive when not provided (standalone instances)
        services.AddOptions<TierOptions>().Bind(config.GetSection(TierOptions.SectionName));

        // Federation options default to requiring signature verification
        services.AddOptions<FederationOptions>().Bind(config.GetSection(FederationOptions.SectionName));

        services.AddOptions<EncryptionOptions>().Bind(config.GetSection(EncryptionOptions.SectionName));

        // Member billing (per-server subscriptions paid via this instance's own Stripe account)
        services.AddOptions<MemberBillingOptions>().Bind(config.GetSection(MemberBillingOptions.SectionName));

        // Internal API shared secret (used by xcord-hub to call internal endpoints).
        // Optional in standalone deployments; the InternalKey policy fails closed when unset.
        services.AddOptions<InternalAuthOptions>().Bind(config.GetSection(InternalAuthOptions.SectionName));
    }

    private static void AddCoreServices(IServiceCollection services, IConfiguration config)
    {
        var snowflakeOpts = config.GetSection(SnowflakeOptions.SectionName)
            .Get<SnowflakeOptions>() ?? throw new InvalidOperationException("Snowflake configuration is required");

        services.AddSingleton(new SnowflakeIdGenerator(snowflakeOpts.WorkerId));
        services.AddSingleton<IKekProvider, FileKekProvider>();
        services.AddSingleton<EncryptionKeyHolder>();
        services.AddSingleton<IEncryptionService>(sp =>
            new PgCryptoEncryptionService(sp.GetRequiredService<EncryptionKeyHolder>()));
        services.AddScoped<IKeyRotationService, KeyRotationService>();
        services.AddSingleton<ICursorService, CursorService>();

        services.AddScoped<IJwtService, JwtService>();
        services.AddSingleton<RsaKeySingleton>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IGroupLimitService, GroupLimitService>();
        services.AddScoped<IConversationResolver, ConversationResolver>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddHttpClient<ILiveKitService, LiveKitService>();
        services.AddSingleton<ISlowmodeService, RedisSlowmodeService>();
        services.AddScoped<Xcord.Features.Messages.IMessageProcessor, Xcord.Features.Messages.MessageProcessor>();
        services.AddSingleton<IStorageService, S3StorageService>();
        services.AddSingleton<IThumbnailService, ImageSharpThumbnailService>();
        services.AddSingleton<IImageValidator, ImageSharpImageValidator>();
        services.AddSingleton<OutgoingWebhookEventQueue>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddSingleton<IEmailService, SmtpEmailService>();
        services.AddScoped<IAutomodService, AutomodService>();
        services.AddScoped<IAutomodActionExecutor, AutomodActionExecutor>();
        services.AddScoped<ITimeoutService, TimeoutService>();
        services.AddSingleton<ISystemBroadcaster, SignalRSystemBroadcaster>();
        services.AddSingleton<BotInteractionForwarder>();
        services.AddSingleton<BotAgentRegistry>();
        services.AddSingleton<BotProcessManager>();
        services.AddSingleton<SsrfSafeHttpClient>();
        services.AddSingleton<OpenGraphParser>();
        services.AddScoped<IMemberBillingService, MemberBillingService>();
        services.AddScoped<Xcord.Features.Billing.MemberBillingWebhookHandler>();
        services.AddScoped<Xcord.Features.Broadcasts.BroadcastEgressBuilder>();

        // Discord migration
        services.AddSingleton<DiscordRateLimiter>();
        services.AddHttpClient<DiscordApiClient>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Xcord-Migration/1.0");
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddScoped<DiscordMigrationOrchestrator>();

        // Hub client (optional - only when hub-connected)
        var hubOpts = config.GetSection(HubOptions.SectionName).Get<HubOptions>();
        if (hubOpts?.Enabled == true)
        {
            services.AddHttpClient<IHubClient, HubClient>();
        }
        else
        {
            services.AddSingleton<IHubClient, NullHubClient>();
        }
    }

    private static void AddBackgroundServices(IServiceCollection services)
    {
        services.AddHostedService<AttachmentCleanup>();
        services.AddHostedService<ThumbnailProcessor>();
        services.AddHostedService<ThreadArchiver>();
        services.AddHostedService<EmbedExtractor>();
        services.AddHostedService<EventLifecycleService>();
        services.AddHostedService<EventNotifier>();
        services.AddHostedService<PollCloser>();
        services.AddHostedService<CallTimeoutService>();
        services.AddHostedService<OutgoingWebhookEventProcessor>();
        services.AddHostedService<OutgoingWebhookDeliveryService>();
        services.AddHostedService<OutgoingWebhookDeliveryCleanup>();
        services.AddHostedService<ScheduledMessageDispatcher>();
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

            // Captcha issuance: per-IP limit (default 20/min) to slow mass GIF harvesting.
            // Uses a policy (per-IP partition) rather than AddFixedWindowLimiter, which shares
            // one bucket across all callers.
            options.AddPolicy("captcha", context =>
            {
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = opts.CaptchaPermitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
            });

            // Registration: configurable per-IP limit (default 3/min)
            options.AddFixedWindowLimiter("auth-register", limiterOptions =>
            {
                limiterOptions.PermitLimit = opts.AuthRegisterPermitLimit;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueLimit = 0;
            });

            // Password reset: configurable per-IP limit (default 3/min)
            options.AddFixedWindowLimiter("auth-forgot-password", limiterOptions =>
            {
                limiterOptions.PermitLimit = opts.AuthForgotPasswordPermitLimit;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueLimit = 0;
            });

            // General auth endpoints (login, reset-password): configurable per-IP limit (default 10/min)
            options.AddFixedWindowLimiter("auth", limiterOptions =>
            {
                limiterOptions.PermitLimit = opts.AuthPermitLimit;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.QueueLimit = 0;
            });
        });
    }

    private static readonly string[] MobileOrigins = ["capacitor://localhost", "https://localhost"];

    private static void AddCors(IServiceCollection services, IConfiguration config, IWebHostEnvironment env)
    {
        var corsOpts = config.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                if (corsOpts.AllowedOrigins.Length > 0)
                {
                    var allOrigins = corsOpts.AllowedOrigins.Concat(MobileOrigins).ToArray();
                    policy.WithOrigins(allOrigins)
                        .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH")
                        .WithHeaders("Authorization", "Content-Type", "X-Requested-With", "Accept", "Origin", "X-Xcord-Request")
                        .AllowCredentials();
                }
                else
                {
                    // Federation peers form an unbounded set of origins by design; an instance
                    // cannot enumerate every other instance ahead of time. The default policy
                    // therefore permits any origin so that cross-instance browser access
                    // (federation discovery, embedded content, the hub UI, mobile webviews)
                    // continues to work. Deployments that want a tighter policy can set
                    // Cors:AllowedOrigins explicitly.
                    policy.AllowAnyOrigin()
                        .WithMethods("GET", "POST", "PUT", "DELETE", "PATCH")
                        .WithHeaders("Authorization", "Content-Type", "X-Requested-With", "Accept", "Origin", "X-Xcord-Request");
                }
            });
        });
    }

    private static void AddAuth(IServiceCollection services, IConfiguration config)
    {
        var jwtOpts = config.GetSection(JwtOptions.SectionName)
            .Get<JwtOptions>() ?? throw new InvalidOperationException("JWT configuration is required");

        var hubOpts = config.GetSection(HubOptions.SectionName).Get<HubOptions>() ?? new HubOptions();

        // Pre-load the pinned hub public key (if any). Pinning is per-instance: the
        // operator (or the registration handshake) configures Hub:HubPublicKey and
        // Hub:HubIssuer when this instance federates with a hub. If neither is set,
        // any token with a hub issuer claim is rejected outright in the resolver
        // below. We do not fall back to "trust any RSA key with a matching iss
        // claim" -- that was the MED-3 vulnerability.
        Microsoft.IdentityModel.Tokens.RsaSecurityKey? pinnedHubKey = null;
        if (!string.IsNullOrWhiteSpace(hubOpts.HubPublicKey))
        {
            try
            {
                var rsa = System.Security.Cryptography.RSA.Create();
                rsa.ImportFromPem(hubOpts.HubPublicKey);
                pinnedHubKey = new Microsoft.IdentityModel.Tokens.RsaSecurityKey(rsa);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Hub:HubPublicKey is set but could not be parsed as a PEM-encoded public key. " +
                    "Expected a SubjectPublicKeyInfo block (-----BEGIN PUBLIC KEY-----).", ex);
            }
        }

        var validIssuers = new List<string> { jwtOpts.Issuer };
        if (!string.IsNullOrWhiteSpace(hubOpts.HubIssuer) && pinnedHubKey != null)
        {
            validIssuers.Add(hubOpts.HubIssuer);
        }

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
                ValidIssuers = validIssuers,
                ValidAudience = jwtOpts.Audience,
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "sub",

                // Pin signing keys per-issuer. Tokens with the local instance issuer
                // are validated against the local RSA public key (loaded later via
                // BootstrapService); tokens with the configured hub issuer must be
                // signed by the pinned hub public key. Any other issuer is rejected
                // (ValidateIssuer also enforces this; the resolver additionally
                // returns no keys so signature validation fails defensively).
                IssuerSigningKeyResolver = (token, securityToken, kid, parameters) =>
                {
                    var issuer = securityToken?.Issuer;

                    if (issuer == jwtOpts.Issuer)
                    {
                        // Local instance: use whichever key BootstrapService has
                        // installed on TokenValidationParameters.IssuerSigningKey.
                        return parameters.IssuerSigningKey != null
                            ? new[] { parameters.IssuerSigningKey }
                            : Array.Empty<Microsoft.IdentityModel.Tokens.SecurityKey>();
                    }

                    if (pinnedHubKey != null
                        && !string.IsNullOrWhiteSpace(hubOpts.HubIssuer)
                        && issuer == hubOpts.HubIssuer)
                    {
                        return new Microsoft.IdentityModel.Tokens.SecurityKey[] { pinnedHubKey };
                    }

                    // Unknown issuer (including the hub issuer when no key is
                    // pinned): reject by returning no keys.
                    return Array.Empty<Microsoft.IdentityModel.Tokens.SecurityKey>();
                }
            };

            // Route "Authorization: Bot ..." requests to the Bot auth handler
            // instead of trying to parse them as JWT.
            options.ForwardDefaultSelector = context =>
            {
                var auth = context.Request.Headers.Authorization.ToString();
                if (auth.StartsWith("Bot ", StringComparison.OrdinalIgnoreCase))
                    return "Bot";
                return null; // use default (JWT Bearer)
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

            // Internal endpoints (called by xcord-hub) authenticate via a shared
            // X-Internal-Key header rather than a JWT. The policy does not require
            // an authenticated user; the requirement handler validates the header.
            options.AddPolicy(Policies.InternalKey, policy => policy
                .AddRequirements(new InternalKeyRequirement()));
        });

        // Authorization handler for the InternalKey policy.
        services.AddSingleton<IAuthorizationHandler, InternalKeyAuthorizationHandler>();
    }
}
