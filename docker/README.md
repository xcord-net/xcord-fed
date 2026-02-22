# Xcord Federation Docker Setup

This directory contains the Docker configuration for running an Xcord federation instance.

## Files

- `Dockerfile` - Multi-stage build for backend and frontend
- `entrypoint.sh` - Startup script that reads Docker secrets and configures the app
- `xcord-config.example.json` - Example configuration file format
- `docker-compose.yml` - Local development orchestration
- `shared-services.yml` - Shared infrastructure (PostgreSQL, Redis, MinIO, LiveKit)
- `livekit.yaml` - LiveKit server configuration

## Building the Image

From the `xcord-fed` directory:

```bash
docker build -t xcord-fed:latest .
```

## Running with Docker Compose

For local development with shared services:

```bash
# Start shared infrastructure
docker compose -f docker/shared-services.yml up -d

# Start the federation instance
docker compose -f docker/docker-compose.yml up -d
```

## Running Standalone

To run a single instance with Docker secrets:

```bash
# Create a config file
cp docker/xcord-config.example.json /path/to/xcord-config.json

# Edit the config file with your settings
nano /path/to/xcord-config.json

# Create a Docker secret
docker secret create xcord-config /path/to/xcord-config.json

# Run the container
docker run -d \
  --name xcord-instance \
  --secret xcord-config \
  -p 8080:80 \
  xcord-fed:latest
```

## Configuration

The container reads its configuration from `/run/secrets/xcord-config` (Docker secret).
This JSON file is transformed by the entrypoint script into `appsettings.Production.json`.

See `xcord-config.example.json` for the complete configuration schema.

### Required Configuration Sections

- `database` - PostgreSQL connection
- `redis` - Redis connection and channel prefix
- `jwt` - JWT issuer and audience
- `storage` - MinIO/S3 configuration
- `livekit` - LiveKit voice server configuration
- `instance` - Instance domain
- `snowflake` - Worker ID for distributed ID generation
- `encryption` - Encryption key for sensitive data

### Optional Configuration Sections

- `cors` - CORS allowed origins (defaults to allow all)
- `email` - SMTP configuration (defaults to dev mode)
- `rateLimiting` - Rate limiting settings
- `gif` - GIF provider (tenor/giphy/none)
- `outbox` - Transactional outbox settings

## Health Check

The container includes a health check endpoint at `/health` that returns:

```json
{
  "status": "healthy",
  "timestamp": "2025-01-15T12:34:56.789Z"
}
```

The Docker health check runs every 30 seconds with a 5-second timeout.

## Migrations

Database migrations are applied automatically in development mode.

For production, ensure migrations are applied before starting:
- Run migrations during deployment via a separate job
- Or use the `--migrate` flag (migrations run on startup)

## Non-Root User

The container runs as user `xcord:xcord` (UID/GID 1001) for security.

## Ports

- Port 80 - HTTP API and WebSocket (SignalR)

## Volumes

No persistent volumes are required. All state is stored in:
- PostgreSQL (database)
- Redis (cache/realtime)
- MinIO (file storage)

## Environment Variables

The entrypoint script respects these environment variables:

- `ASPNETCORE_ENVIRONMENT` - Defaults to `Production`
- `ASPNETCORE_URLS` - Defaults to `http://+:80`

## Multi-Stage Build

The Dockerfile uses three stages:

1. **build-backend** - .NET SDK 9.0 Alpine to compile the backend
2. **build-frontend** - Node 22 Alpine to build the SolidJS frontend
3. **runtime** - .NET ASP.NET 9.0 Alpine with both backend and frontend

This results in a minimal production image with only the runtime dependencies.
