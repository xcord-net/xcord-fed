#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
COMPOSE="docker compose -f $ROOT/docker/docker-compose.yml"

case "${1:-up}" in
    down)
        $COMPOSE down -v 2>/dev/null || true
        echo "Stopped."
        exit 0
        ;;
    up|dev) ;;
    rebuild)
        $COMPOSE down -v 2>/dev/null || true
        $COMPOSE build --no-cache app
        echo "Rebuilt. Run './run.sh' to start."
        exit 0
        ;;
    *)
        echo "Usage: ./run.sh [up|down|rebuild]"
        exit 0
        ;;
esac

# Fresh state every run
$COMPOSE down -v 2>/dev/null || true

# Build
echo "Building..."
$COMPOSE build app --quiet

# Start
echo "Starting..."
$COMPOSE up -d

# Wait for app health
printf "Waiting for app"
for i in $(seq 1 90); do
    if curl -sf http://localhost:8080/health > /dev/null 2>&1; then
        echo " ready"
        break
    fi
    if [ "$i" -eq 90 ]; then
        echo " FAILED"
        $COMPOSE logs app --tail 30
        exit 1
    fi
    printf "."
    sleep 1
done

# Cleanup on exit
trap '$COMPOSE down -v 2>/dev/null || true' INT TERM

echo ""
echo "================================================================"
echo "  Xcord"
echo ""
echo "  App:       http://localhost:8080"
echo "  Mailpit:   http://localhost:8025"
echo ""
echo "  Admin:     admin@xcord.local / Admin123!"
echo ""
echo "  ./run.sh down       Stop"
echo "  ./run.sh rebuild    Rebuild image"
echo "================================================================"
echo ""

# Follow logs
$COMPOSE logs -f app
