# ===== Stage 1: Build backend =====
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build-backend
WORKDIR /src
ARG VERSION=0.0.0-dev

# Copy solution and project files for restore
COPY src/backend/Directory.Build.props src/backend/
COPY src/backend/Xcord.sln src/backend/
COPY src/backend/src/Xcord.Api/Xcord.Api.csproj src/backend/src/Xcord.Api/
COPY src/backend/src/Xcord.Features/Xcord.Features.csproj src/backend/src/Xcord.Features/
COPY src/backend/src/Xcord.Infrastructure/Xcord.Infrastructure.csproj src/backend/src/Xcord.Infrastructure/
COPY src/backend/src/Xcord.Shared/Xcord.Shared.csproj src/backend/src/Xcord.Shared/
COPY xcord-common/src/Xcord.Common/Xcord.Common.csproj xcord-common/src/Xcord.Common/
COPY xcord-common/src/Xcord.Captcha/Xcord.Captcha.csproj xcord-common/src/Xcord.Captcha/

# The RID comes from the build platform, not from a literal. `linux-musl-x64`
# was hardcoded here and in the publish below, which fails on arm64 before it
# fails usefully: the restore succeeds, and then the OpenAPI document generator
# (Microsoft.Extensions.ApiDescription.Server) tries to RUN the freshly built
# x64 assembly on an aarch64 host and dies with MSB3073 exit code 2, naming a
# targets file rather than the architecture.
#
# Derived from `uname -m` rather than from BuildKit's TARGETARCH, because
# `ARG TARGETARCH=amd64` SHADOWS the automatic value — a declared default wins
# over the platform arg, so the first attempt at this fix silently produced
# linux-musl-x64 again on an aarch64 host. TARGETARCH is still honoured when
# explicitly passed, for a cross-build.
ARG TARGETARCH
RUN arch="${TARGETARCH:-$(uname -m)}"; \
    case "$arch" in \
      amd64|x86_64)  rid=linux-musl-x64 ;; \
      arm64|aarch64) rid=linux-musl-arm64 ;; \
      *) echo "unsupported build architecture: $arch" >&2; exit 1 ;; \
    esac; \
    echo "$rid" > /tmp/rid; \
    echo "building for $arch -> $rid"

# Restore dependencies
RUN dotnet restore src/backend/src/Xcord.Api/Xcord.Api.csproj -r "$(cat /tmp/rid)" -p:PublishReadyToRun=true

# Copy full source
COPY xcord-common/ xcord-common/
COPY src/backend/ src/backend/

# Publish
RUN dotnet publish src/backend/src/Xcord.Api/Xcord.Api.csproj \
    -c Release \
    -o /app/publish \
    -p:Version=$VERSION \
    -r "$(cat /tmp/rid)" \
    -p:PublishReadyToRun=true \
    --self-contained false \
    --no-restore

# ===== Stage 2: Build frontend (client SPA) =====
FROM node:24.11.1-alpine3.22 AS build-frontend
WORKDIR /src
ARG VERSION=0.0.0-dev
ENV VITE_APP_VERSION=$VERSION

COPY src/frontend/package*.json ./
RUN npm ci --production=false

COPY src/frontend/ ./
RUN npm run build

# ===== Stage 2b: Build admin SPA =====
FROM node:24.11.1-alpine3.22 AS build-admin
WORKDIR /app
ARG VERSION=0.0.0-dev
ENV VITE_APP_VERSION=$VERSION

COPY src/admin/ .
RUN if [ -f package.json ]; then \
        npm ci && npm run build; \
    else \
        mkdir -p dist && \
        echo '<!DOCTYPE html><html><head><title>Admin</title></head><body><h1>Admin SPA - Coming Soon</h1></body></html>' > dist/index.html; \
    fi

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

# Copy admin SPA to wwwroot/admin
COPY --from=build-admin /app/dist ./wwwroot/admin

# Copy entrypoint
COPY docker/entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh

# Set ownership
RUN chown -R xcord:xcord /app

USER xcord

EXPOSE 80

# start-period covers cold .NET startup: EF migration check + crypto init + RSA
# generation + admin/server seeding + ASP.NET startup. Observed ~13s on a fast
# host; CI runners (2-vCPU GitHub free tier) routinely exceed 20s. retries cap
# the unhealthy window in steady state without giving up during cold boot.
HEALTHCHECK --interval=10s --timeout=3s --start-period=60s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:80/health || exit 1

ENTRYPOINT ["/app/entrypoint.sh"]
