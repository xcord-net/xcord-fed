#!/usr/bin/env bash
# Verification script - run after making changes to ensure everything is consistent.
# Usage: ./scripts/verify.sh
set -euo pipefail

cd "$(dirname "$0")/.."
BACKEND=src/backend
FRONTEND=src/frontend

echo "=== Backend build ==="
dotnet build $BACKEND/Xcord.sln --verbosity quiet
echo "PASS"

echo ""
echo "=== EF model snapshot check ==="
RESULT=$(dotnet ef migrations has-pending-model-changes \
  --project $BACKEND/src/Xcord.Infrastructure \
  --startup-project $BACKEND/src/Xcord.Api 2>&1 | tail -1)

if echo "$RESULT" | grep -q "No changes"; then
  echo "PASS - snapshot in sync"
else
  echo "FAIL - $RESULT"
  echo ""
  echo "Fix: cd $BACKEND && dotnet ef migrations remove --project src/Xcord.Infrastructure --startup-project src/Xcord.Api --force"
  echo "Then: dotnet ef migrations add InitialCreate --project src/Xcord.Infrastructure --startup-project src/Xcord.Api --output-dir Migrations"
  exit 1
fi

echo ""
echo "=== Frontend type check ==="
source ~/.nvm/nvm.sh 2>/dev/null || true
(cd $FRONTEND && npx tsc --noEmit)
echo "PASS"

echo ""
echo "=== Frontend tests ==="
(cd $FRONTEND && npx vitest run 2>&1 | tail -3)

echo ""
dotnet build-server shutdown 2>/dev/null || true
echo "=== All checks passed ==="
