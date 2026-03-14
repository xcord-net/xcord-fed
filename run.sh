#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
COMPOSE_FILE="$ROOT/docker/docker-compose.yml"
BACKEND_PROJECT="$ROOT/src/backend/src/Xcord.Api"

# ── Subcommands ──────────────────────────────────────────────────────────────

CMD="${1:-dev}"
case "$CMD" in
    down)
        if docker compose -f "$COMPOSE_FILE" ps --quiet 2>/dev/null | grep -q .; then
            docker compose -f "$COMPOSE_FILE" down -v
            echo "Infrastructure stopped."
        else
            echo "Not running."
        fi
        exit 0
        ;;
    dev)
        ;; # fall through
    *)
        echo "Usage: $(basename "$0") [dev|down]"
        echo ""
        echo "  dev    Start isolated fed instance with hot-reload (default)"
        echo "  down   Stop and remove infrastructure containers + volumes"
        exit 0
        ;;
esac

# ── Dev mode ─────────────────────────────────────────────────────────────────
# Infrastructure in Docker, backend (dotnet watch) and frontend (vite dev)
# on the host for hot-reload. Ctrl-C kills everything.

# Stop the hub E2E stack if it's using our ports
HUB_STACK="$ROOT/../scripts/.stack/docker-compose.yml"
if [ -f "$HUB_STACK" ] && docker compose -f "$HUB_STACK" ps --quiet 2>/dev/null | grep -q .; then
    echo "==> Hub E2E stack is running -- stopping it to free ports..."
    docker compose -f "$HUB_STACK" down -v --rmi local 2>/dev/null || true
fi

echo "==> Starting infrastructure..."
docker compose -f "$COMPOSE_FILE" up -d

# ── Wait for services ────────────────────────────────────────────────────────

wait_for() {
    local name="$1" cmd="$2" max="${3:-30}"
    printf "    Waiting for %s..." "$name"
    local tries=0
    until eval "$cmd" > /dev/null 2>&1; do
        tries=$((tries + 1))
        if [ "$tries" -ge "$max" ]; then
            echo " FAILED"
            echo "$name did not become healthy within ${max}s."
            exit 1
        fi
        printf "."
        sleep 1
    done
    echo " ready"
}

wait_for "Postgres" "docker compose -f '$COMPOSE_FILE' exec -T postgres pg_isready -U xcord -d xcord_dev"
wait_for "Redis"    "docker compose -f '$COMPOSE_FILE' exec -T redis redis-cli ping"
wait_for "MinIO"    "curl -sf http://localhost:9000/minio/health/live"
wait_for "LiveKit"  "curl -sf http://localhost:7880"
wait_for "Mailpit"  "curl -sf http://localhost:8025"

echo ""
echo "==> Infrastructure ready. Starting host processes..."
echo ""

# ── Color codes ──────────────────────────────────────────────────────────────

GREEN=$'\033[0;32m'
BLUE=$'\033[0;34m'
NC=$'\033[0m'

cleanup() {
    trap '' INT TERM EXIT
    echo ""
    echo "==> Stopping..."
    kill 0 2>/dev/null || true
}
trap cleanup EXIT

# ── Backend: dotnet watch ────────────────────────────────────────────────────

ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS="http://0.0.0.0:5041" \
dotnet watch run --project "$BACKEND_PROJECT" 2>&1 | \
    sed -u "s/^/${GREEN}[backend]${NC}  /" &

# ── Frontend: vite dev ───────────────────────────────────────────────────────

(cd "$ROOT/src/frontend" && exec npx vite --host 0.0.0.0) 2>&1 | \
    sed -u "s/^/${BLUE}[frontend]${NC} /" &

echo "================================================================"
echo "  Fed dev mode is running"
echo ""
echo "  Frontend:       http://localhost:3000"
echo "  Backend API:    http://localhost:5041"
echo "  Mailpit:        http://localhost:8025"
echo "  MinIO console:  http://localhost:9001"
echo ""
echo "  Press Ctrl-C to stop everything."
echo "================================================================"
echo ""

wait
