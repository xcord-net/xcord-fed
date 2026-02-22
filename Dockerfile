# ===== Stage 1: Build backend =====
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build-backend
WORKDIR /src

# Copy solution and project files for restore
COPY src/backend/Directory.Build.props src/backend/
COPY src/backend/Xcord.sln src/backend/
COPY src/backend/src/Xcord.Api/Xcord.Api.csproj src/backend/src/Xcord.Api/
COPY src/backend/src/Xcord.Features/Xcord.Features.csproj src/backend/src/Xcord.Features/
COPY src/backend/src/Xcord.Infrastructure/Xcord.Infrastructure.csproj src/backend/src/Xcord.Infrastructure/
COPY src/backend/src/Xcord.Shared/Xcord.Shared.csproj src/backend/src/Xcord.Shared/

# Restore dependencies
RUN dotnet restore src/backend/src/Xcord.Api/Xcord.Api.csproj

# Copy full source
COPY src/backend/ src/backend/

# Publish
RUN dotnet publish src/backend/src/Xcord.Api/Xcord.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ===== Stage 2: Build frontend (client SPA) =====
FROM node:22-alpine AS build-frontend
WORKDIR /src

COPY src/frontend/package*.json ./
RUN npm ci --production=false

COPY src/frontend/ ./
RUN npm run build

# ===== Stage 3: Runtime =====
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS runtime
WORKDIR /app

# Install jq for config processing
RUN apk add --no-cache jq wget

# Create non-root user
RUN addgroup -g 1001 xcord && \
    adduser -u 1001 -G xcord -s /bin/sh -D xcord

# Copy published backend
COPY --from=build-backend /app/publish .

# Copy frontend SPA to wwwroot
COPY --from=build-frontend /src/dist ./wwwroot/

# Copy entrypoint
COPY docker/entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh

# Set ownership
RUN chown -R xcord:xcord /app

USER xcord

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:80/health || exit 1

ENTRYPOINT ["/app/entrypoint.sh"]
