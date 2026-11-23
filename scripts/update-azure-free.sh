#!/usr/bin/env bash
# Upload a new build (app.zip next to this script) to an existing free deployment.
set -euo pipefail
cd "$(dirname "$0")"
RG=${1:-krushiprime-rg}
SA=$(az storage account list -g "$RG" --query "[0].name" -o tsv)
APP=$(az containerapp list -g "$RG" --query "[0].name" -o tsv)
KEY=$(az storage account keys list -g "$RG" -n "$SA" --query "[0].value" -o tsv)
rm -rf publish && mkdir publish && unzip -q app.zip -d publish
az storage file upload-batch --account-name "$SA" --account-key "$KEY" --destination app --source publish --max-connections 8 -o none
az containerapp update -g "$RG" -n "$APP" --set-env-vars "DEPLOYED_AT=$(date +%s)" -o none
echo "✅ Updated: https://$(az containerapp show -g "$RG" -n "$APP" --query properties.configuration.ingress.fqdn -o tsv)"
