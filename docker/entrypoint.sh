#!/bin/sh
set -e

CONFIG_PATH="/run/secrets/xcord-config"
APPSETTINGS_ENV="${ASPNETCORE_ENVIRONMENT:-Production}"
APPSETTINGS_PATH="/app/appsettings.${APPSETTINGS_ENV}.json"

# XCORD_CONFIG_INLINE is a developer escape hatch that allows the config JSON
# to be passed as an environment variable instead of a Docker secret file.
# NOTE: this is NOT used by the hub provisioner in production - the hub creates
# a Docker secret and mounts it at /run/secrets/xcord-config instead, which
# prevents sensitive credentials from appearing in `docker inspect` output.
if [ -n "${XCORD_CONFIG_INLINE:-}" ]; then
    echo "Reading configuration from XCORD_CONFIG_INLINE environment variable..."
    echo "$XCORD_CONFIG_INLINE" | jq '{
        Database: {
            ConnectionString: .database.connectionString
        },
        Redis: {
            ConnectionString: .redis.connectionString,
            ChannelPrefix: .redis.channelPrefix
        },
        Jwt: {
            Issuer: .jwt.issuer,
            Audience: .jwt.audience
        },
        Storage: {
            Endpoint: .storage.endpoint,
            AccessKey: .storage.accessKey,
            SecretKey: .storage.secretKey,
            Bucket: .storage.bucket,
            UseSSL: (.storage.useSsl // false)
        },
        LiveKit: {
            Host: .livekit.host,
            ApiKey: .livekit.apiKey,
            ApiSecret: .livekit.apiSecret
        },
        Cors: {
            AllowedOrigins: (.cors.allowedOrigins // [])
        },
        Instance: {
            Domain: .instance.domain,
            Name: (.instance.name // "Xcord")
        },
        Snowflake: {
            WorkerId: .snowflake.workerId
        },
        Email: {
            SmtpHost: (.email.smtpHost // ""),
            SmtpPort: (.email.smtpPort // 587),
            SmtpUsername: (.email.smtpUsername // ""),
            SmtpPassword: (.email.smtpPassword // ""),
            FromAddress: (.email.fromAddress // ""),
            FromName: (.email.fromName // "Xcord"),
            UseSsl: (if .email.useSsl == null then true else .email.useSsl end),
            DevMode: (.email.devMode // false)
        },
        Encryption: {
            EncryptionKey: (.encryption.encryptionKey // "")
        },
        RateLimiting: {
            MaxRequests: (.rateLimiting.maxRequests // 100),
            WindowSeconds: (.rateLimiting.windowSeconds // 60),
            AuthRegisterPermitLimit: (.rateLimiting.authRegisterPermitLimit // 3),
            AuthForgotPasswordPermitLimit: (.rateLimiting.authForgotPasswordPermitLimit // 3),
            AuthPermitLimit: (.rateLimiting.authPermitLimit // 10)
        },
        Gif: {
            Provider: (.gif.provider // "none"),
            ApiKey: (.gif.apiKey // "")
        },
        Outbox: {
            PollingIntervalMs: (.outbox.pollingIntervalMs // 1000),
            BatchSize: (.outbox.batchSize // 100),
            CleanupIntervalMinutes: (.outbox.cleanupIntervalMinutes // 60),
            RetentionHours: (.outbox.retentionHours // 24),
            MaxRetryCount: (.outbox.maxRetryCount // 5)
        },
        Hub: {
            Enabled: (.hub.enabled // false),
            GatewayApiUrl: (.hub.gatewayApiUrl // ""),
            BootstrapToken: (.hub.bootstrapToken // ""),
            Origin: (.hub.origin // "")
        },
        Tier: {
            CanUseVoiceChannels: (.tier.canUseVoiceChannels // true),
            CanUseVideoChannels: (.tier.canUseVideoChannels // true),
            CanCreateBots: (.tier.canCreateBots // true),
            CanUseWebhooks: (.tier.canUseWebhooks // true),
            CanUseCustomEmoji: (.tier.canUseCustomEmoji // true),
            CanUseThreads: (.tier.canUseThreads // true),
            CanUseForumChannels: (.tier.canUseForumChannels // true),
            CanUseScheduledEvents: (.tier.canUseScheduledEvents // true),
            CanUseHdVideo: (.tier.canUseHdVideo // false),
            CanUseSimulcast: (.tier.canUseSimulcast // false),
            CanUseRecording: (.tier.canUseRecording // false),
            MaxUsers: (.tier.maxUsers // 0),
            MaxServers: (.tier.maxServers // 0),
            MaxStorageMb: (.tier.maxStorageMb // 0),
            MaxRateLimit: (.tier.maxRateLimit // 0),
            MaxVoiceConcurrency: (.tier.maxVoiceConcurrency // 0),
            MaxVideoConcurrency: (.tier.maxVideoConcurrency // 0),
            MaxAudioBitrateKbps: (.tier.maxAudioBitrateKbps // 0),
            MaxVideoBitrateKbps: (.tier.maxVideoBitrateKbps // 0),
            MaxVideoWidth: (.tier.maxVideoWidth // 0),
            MaxVideoHeight: (.tier.maxVideoHeight // 0),
            MaxVideoFps: (.tier.maxVideoFps // 0),
            MaxScreenShareBitrateKbps: (.tier.maxScreenShareBitrateKbps // 0)
        },
        Auth: {
            BcryptWorkFactor: (.auth.bcryptWorkFactor // 12)
        },
        MemberBilling: {
            StripeSecretKey: (.memberBilling.stripeSecretKey // ""),
            StripeWebhookSecret: (.memberBilling.stripeWebhookSecret // "")
        },
        TestSeed: {
            Key: (.testSeed.key // "")
        }
    }' > "$APPSETTINGS_PATH"
    echo "Configuration generated at $APPSETTINGS_PATH"
elif [ -f "$CONFIG_PATH" ]; then
    echo "Reading configuration from Docker secret..."

    # Transform xcord.json config to appsettings.Production.json using jq
    jq '{
        Database: {
            ConnectionString: .database.connectionString
        },
        Redis: {
            ConnectionString: .redis.connectionString,
            ChannelPrefix: .redis.channelPrefix
        },
        Jwt: {
            Issuer: .jwt.issuer,
            Audience: .jwt.audience
        },
        Storage: {
            Endpoint: .storage.endpoint,
            AccessKey: .storage.accessKey,
            SecretKey: .storage.secretKey,
            Bucket: .storage.bucket,
            UseSSL: (.storage.useSsl // false)
        },
        LiveKit: {
            Host: .livekit.host,
            ApiKey: .livekit.apiKey,
            ApiSecret: .livekit.apiSecret
        },
        Cors: {
            AllowedOrigins: (.cors.allowedOrigins // [])
        },
        Instance: {
            Domain: .instance.domain,
            Name: (.instance.name // "Xcord")
        },
        Snowflake: {
            WorkerId: .snowflake.workerId
        },
        Email: {
            SmtpHost: (.email.smtpHost // ""),
            SmtpPort: (.email.smtpPort // 587),
            SmtpUsername: (.email.smtpUsername // ""),
            SmtpPassword: (.email.smtpPassword // ""),
            FromAddress: (.email.fromAddress // ""),
            FromName: (.email.fromName // "Xcord"),
            UseSsl: (if .email.useSsl == null then true else .email.useSsl end),
            DevMode: (.email.devMode // false)
        },
        Encryption: {
            EncryptionKey: (.encryption.encryptionKey // "")
        },
        RateLimiting: {
            MaxRequests: (.rateLimiting.maxRequests // 100),
            WindowSeconds: (.rateLimiting.windowSeconds // 60),
            AuthRegisterPermitLimit: (.rateLimiting.authRegisterPermitLimit // 3),
            AuthForgotPasswordPermitLimit: (.rateLimiting.authForgotPasswordPermitLimit // 3),
            AuthPermitLimit: (.rateLimiting.authPermitLimit // 10)
        },
        Gif: {
            Provider: (.gif.provider // "none"),
            ApiKey: (.gif.apiKey // "")
        },
        Outbox: {
            PollingIntervalMs: (.outbox.pollingIntervalMs // 1000),
            BatchSize: (.outbox.batchSize // 100),
            CleanupIntervalMinutes: (.outbox.cleanupIntervalMinutes // 60),
            RetentionHours: (.outbox.retentionHours // 24),
            MaxRetryCount: (.outbox.maxRetryCount // 5)
        },
        Hub: {
            Enabled: (.hub.enabled // false),
            GatewayApiUrl: (.hub.gatewayApiUrl // ""),
            BootstrapToken: (.hub.bootstrapToken // ""),
            Origin: (.hub.origin // "")
        },
        Tier: {
            CanUseVoiceChannels: (.tier.canUseVoiceChannels // true),
            CanUseVideoChannels: (.tier.canUseVideoChannels // true),
            CanCreateBots: (.tier.canCreateBots // true),
            CanUseWebhooks: (.tier.canUseWebhooks // true),
            CanUseCustomEmoji: (.tier.canUseCustomEmoji // true),
            CanUseThreads: (.tier.canUseThreads // true),
            CanUseForumChannels: (.tier.canUseForumChannels // true),
            CanUseScheduledEvents: (.tier.canUseScheduledEvents // true),
            CanUseHdVideo: (.tier.canUseHdVideo // false),
            CanUseSimulcast: (.tier.canUseSimulcast // false),
            CanUseRecording: (.tier.canUseRecording // false),
            MaxUsers: (.tier.maxUsers // 0),
            MaxServers: (.tier.maxServers // 0),
            MaxStorageMb: (.tier.maxStorageMb // 0),
            MaxRateLimit: (.tier.maxRateLimit // 0),
            MaxVoiceConcurrency: (.tier.maxVoiceConcurrency // 0),
            MaxVideoConcurrency: (.tier.maxVideoConcurrency // 0),
            MaxAudioBitrateKbps: (.tier.maxAudioBitrateKbps // 0),
            MaxVideoBitrateKbps: (.tier.maxVideoBitrateKbps // 0),
            MaxVideoWidth: (.tier.maxVideoWidth // 0),
            MaxVideoHeight: (.tier.maxVideoHeight // 0),
            MaxVideoFps: (.tier.maxVideoFps // 0),
            MaxScreenShareBitrateKbps: (.tier.maxScreenShareBitrateKbps // 0)
        },
        Auth: {
            BcryptWorkFactor: (.auth.bcryptWorkFactor // 12)
        },
        MemberBilling: {
            StripeSecretKey: (.memberBilling.stripeSecretKey // ""),
            StripeWebhookSecret: (.memberBilling.stripeWebhookSecret // "")
        },
        TestSeed: {
            Key: (.testSeed.key // "")
        }
    }' "$CONFIG_PATH" > "$APPSETTINGS_PATH"

    echo "Configuration generated at $APPSETTINGS_PATH"
else
    echo "No Docker secret found at $CONFIG_PATH, using existing configuration"
fi

# Run EF Core migrations if --migrate flag is passed
if [ "$1" = "--migrate" ]; then
    echo "Running database migrations..."
    # Use the EF Core bundle or dotnet ef approach
    # For published apps, we rely on auto-migration in development
    # In production, migrations should be applied via the app startup
    export ASPNETCORE_ENVIRONMENT=Production
    shift
fi

# Set production environment
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Production}"
export ASPNETCORE_URLS="http://+:80"

echo "Starting Xcord Federation Instance..."
exec dotnet Xcord.Api.dll "$@"
