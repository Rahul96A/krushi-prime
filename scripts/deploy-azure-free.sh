#!/usr/bin/env bash
# Krushi Prime – FREE Azure deployment (Container Apps + Azure SQL free offer). Run in Azure Cloud Shell (Bash):
#   unzip krushiprime-azure-free.zip -d kp && bash kp/deploy.sh [resource-group] [region]
set -euo pipefail
cd "$(dirname "$0")"
RG=${1:-krushiprime-rg}; LOC=${2:-centralindia}
rand() { openssl rand -base64 64 | tr -dc 'A-Za-z0-9' | head -c "$1"; }
SQL_PWD="Kp!$(rand 20)a1"; JWT_KEY="$(rand 64)"; ADMIN_PWD="Kp@$(rand 10)"
az config set extension.use_dynamic_install=yes_without_prompt -o none 2>/dev/null || true

echo "▶ Subscription: $(az account show --query name -o tsv)"
echo "▶ Registering resource providers (first time only)…"
for ns in Microsoft.App Microsoft.OperationalInsights Microsoft.Insights Microsoft.Storage Microsoft.Sql; do az provider register -n $ns --wait -o none; done

az group create -n "$RG" -l "$LOC" -o none
echo "▶ Creating Container Apps (free grant) + Azure SQL (free offer) + storage… (4–8 min)"
OUT=$(az deployment group create -g "$RG" -n krushiprime-free -f containerapps.json \
  -p location="$LOC" sqlAdminPassword="$SQL_PWD" jwtKey="$JWT_KEY" superAdminPassword="$ADMIN_PWD" seedDemoData=true \
  --query properties.outputs -o json)
val() { echo "$OUT" | python3 -c "import sys,json;print(json.load(sys.stdin)['$1']['value'])"; }
URL=$(val url); APP=$(val containerAppName); SA=$(val storageAccount); SHARE=$(val shareName)

echo "▶ Uploading the app ($(du -sh app.zip | cut -f1))…"
rm -rf publish && mkdir publish && unzip -q app.zip -d publish
KEY=$(az storage account keys list -g "$RG" -n "$SA" --query "[0].value" -o tsv)
az storage file upload-batch --account-name "$SA" --account-key "$KEY" --destination "$SHARE" --source publish --max-connections 8 -o none

echo "▶ Starting new revision…"
az containerapp update -g "$RG" -n "$APP" --set-env-vars "DEPLOYED_AT=$(date +%s)" -o none

echo "▶ First start: creating database & demo data (keeps the app awake; 5–15 min)…"
for i in $(seq 1 40); do curl -fs -m 120 "$URL/health" >/dev/null && break; sleep 15; done
TOKEN=""
for i in $(seq 1 60); do
  TOKEN=$(curl -fs -m 60 -X POST "$URL/api/auth/login" -H 'Content-Type: application/json' -d "{\"username\":\"admin\",\"password\":\"$ADMIN_PWD\"}" | python3 -c 'import sys,json;print(json.load(sys.stdin)["token"])' 2>/dev/null || true)
  SHOPS=$([ -n "$TOKEN" ] && curl -fs -m 60 "$URL/api/admin/stats" -H "Authorization: Bearer $TOKEN" | python3 -c 'import sys,json;print(json.load(sys.stdin)["totalShops"])' 2>/dev/null || echo 0)
  printf "\r   demo shops ready: %s/6 " "$SHOPS"
  [ "${SHOPS:-0}" -ge 6 ] && break
  sleep 20
done
echo

cat <<MSG

✅ Krushi Prime is LIVE (free tier): $URL
   Demo shop    : 9999999999 / demo@123
   Vendor admin : admin / $ADMIN_PWD
   SQL password : $SQL_PWD          ← save these now
   Cashfree webhook URL : $URL/api/webhooks/cashfree

   The app sleeps when unused (₹0). The first visit after a pause takes ~10–40 s to wake up.
   To update later: upload a new app.zip and run:  bash kp/update.sh $RG
MSG
