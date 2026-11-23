#!/usr/bin/env bash
# Deploys Krushi Prime to Azure (free tier) in ~10 minutes. Requires: az CLI (logged in), node 20+, .NET 8 SDK.
# Usage: ./scripts/deploy-azure.sh <resource-group> [location]
set -euo pipefail
RG=${1:?resource group name required}; LOC=${2:-centralindia}
cd "$(dirname "$0")/.."
rand() { openssl rand -base64 48 | tr -dc 'A-Za-z0-9' | head -c "$1"; }
SQL_PWD="Kp!$(rand 20)a1"; JWT_KEY="$(rand 64)"; ADMIN_PWD="Kp@$(rand 10)"

az group create -n "$RG" -l "$LOC" -o none
echo "▶ Provisioning App Service (F1) + Azure SQL (free offer) + App Insights…"
OUT=$(az deployment group create -g "$RG" -f infra/main.bicep -p infra/main.parameters.json \
      -p location="$LOC" sqlAdminPassword="$SQL_PWD" jwtKey="$JWT_KEY" superAdminPassword="$ADMIN_PWD" \
      --query properties.outputs -o json)
APP=$(echo "$OUT" | python3 -c 'import sys,json;print(json.load(sys.stdin)["webAppName"]["value"])')
URL=$(echo "$OUT" | python3 -c 'import sys,json;print(json.load(sys.stdin)["url"]["value"])')

echo "▶ Building…"
(cd frontend && npm ci --no-audit --no-fund && npm run build && rm -rf ../backend/src/KrushiPrime.Api/wwwroot && cp -r dist ../backend/src/KrushiPrime.Api/wwwroot)
rm -rf /tmp/kp-publish /tmp/kp.zip
dotnet publish backend/src/KrushiPrime.Api/KrushiPrime.Api.csproj -c Release -o /tmp/kp-publish
(cd /tmp/kp-publish && zip -qr /tmp/kp.zip .)

echo "▶ Deploying to $APP…"
az webapp deploy -g "$RG" -n "$APP" --src-path /tmp/kp.zip --type zip -o none

cat <<MSG

✅ Krushi Prime is live: $URL
   Vendor admin : admin / $ADMIN_PWD
   Demo shop    : 9999999999 / demo@123
   (first request may take ~30s while the free database resumes)
   SAVE THESE SECRETS NOW → SQL password: $SQL_PWD
MSG
