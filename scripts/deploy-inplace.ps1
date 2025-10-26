# FeedHorn In-Place Deployment Script
# Run as Administrator
# Use when IIS points to the source folder

$projectPath = "D:\projects\browsers\FeedHorn"

Write-Host "=== FeedHorn In-Place Deployment ===" -ForegroundColor Cyan

# Stop IIS
Write-Host "1. Stopping IIS..." -ForegroundColor Green
iisreset /stop
Start-Sleep -Seconds 5

# Stop app pool specifically
Write-Host "   Stopping FeedHorn app pool..." -ForegroundColor Green
Stop-WebAppPool -Name "FeedHorn" -ErrorAction SilentlyContinue
Start-Sleep -Seconds 3

# Backup database
Write-Host "2. Backing up database..." -ForegroundColor Green
if (Test-Path "$projectPath\feedhorn.db") {
    Copy-Item "$projectPath\feedhorn.db" "$projectPath\feedhorn.db.backup" -Force
    Write-Host "   Database backed up" -ForegroundColor Green
}

# Clean and build
Write-Host "3. Building project..." -ForegroundColor Green
cd $projectPath

# Temporarily rename web.config to avoid lock
$webConfigExists = Test-Path "$projectPath\web.config"
if ($webConfigExists) {
    Move-Item "$projectPath\web.config" "$projectPath\web.config.temp" -Force
}

# Remove conflicting output files from root
Write-Host "   Cleaning old output files..." -ForegroundColor Green
Remove-Item "$projectPath\FeedHorn.deps.json" -Force -ErrorAction SilentlyContinue
Remove-Item "$projectPath\FeedHorn.runtimeconfig.json" -Force -ErrorAction SilentlyContinue
Remove-Item "$projectPath\FeedHorn.dll" -Force -ErrorAction SilentlyContinue
Remove-Item "$projectPath\FeedHorn.exe" -Force -ErrorAction SilentlyContinue
Remove-Item "$projectPath\FeedHorn.pdb" -Force -ErrorAction SilentlyContinue

# Clean and build
dotnet clean
dotnet build -c Release
dotnet publish -c Release --no-build -o .

# Restore web.config
if ($webConfigExists) {
    Move-Item "$projectPath\web.config.temp" "$projectPath\web.config" -Force
}

# Restore database
Write-Host "4. Restoring database..." -ForegroundColor Green
if (Test-Path "$projectPath\feedhorn.db.backup") {
    Copy-Item "$projectPath\feedhorn.db.backup" "$projectPath\feedhorn.db" -Force
    Remove-Item "$projectPath\feedhorn.db.backup" -Force
    Write-Host "   Database restored" -ForegroundColor Green
}

# Create logs folder if needed
Write-Host "5. Ensuring logs folder exists..." -ForegroundColor Green
New-Item -Path "$projectPath\logs" -ItemType Directory -Force | Out-Null

# Start IIS
Write-Host "6. Starting IIS..." -ForegroundColor Green
iisreset /start

Write-Host ""
Write-Host "=== Deployment Complete! ===" -ForegroundColor Cyan
Write-Host "Clear your browser cache (Ctrl+F5) and test" -ForegroundColor Yellow
