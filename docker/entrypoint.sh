#!/bin/sh
set -e

CONFIG_PATH="/run/secrets/xcord-config"
APPSETTINGS_PATH="/app/appsettings.Production.json"

# XCORD_CONFIG_INLINE allows the config JSON to be passed as an environment
# variable instead of a Docker secret file. Used by the hub provisioner when
# it starts instance containers via the Docker socket proxy.
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
            WindowSeconds: (.rateLimiting.windowSeconds // 60)
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
            WindowSeconds: (.rateLimiting.windowSeconds // 60)
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
