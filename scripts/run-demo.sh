#!/usr/bin/env bash
# One command local demo: builds the React UI into the API and starts everything on http://localhost:5080
set -euo pipefail
cd "$(dirname "$0")/.."
echo "▶ Building React UI…"
(cd frontend && npm install --no-audit --no-fund && KP_OUT_DIR=../backend/src/KrushiPrime.Api/wwwroot npx vite build)
echo "▶ Starting Krushi Prime on http://localhost:5080  (demo shop 9999999999 / demo@123 · admin / Admin@123)"
cd backend/src/KrushiPrime.Api
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5080 dotnet run
