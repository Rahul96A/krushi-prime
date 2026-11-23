# One command local demo (Windows): builds the React UI into the API and starts on http://localhost:5080
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
Write-Host "Building React UI..." -ForegroundColor Green
Push-Location frontend
npm install --no-audit --no-fund
$env:KP_OUT_DIR = "../backend/src/KrushiPrime.Api/wwwroot"
npx vite build
Remove-Item Env:KP_OUT_DIR
Pop-Location
Write-Host "Starting Krushi Prime on http://localhost:5080  (demo 9999999999 / demo@123, admin / Admin@123)" -ForegroundColor Yellow
Start-Process "http://localhost:5080"
Set-Location backend/src/KrushiPrime.Api
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5080"
dotnet run
