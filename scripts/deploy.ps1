# FeedHorn Deployment Script
# Run as Administrator

$sourcePath = "D:\projects\browsers\FeedHorn"  # Where you copied source files
$deployPath = "C:\inetpub\wwwroot\FeedHorn"   # Where IIS points to
$tempPublish = "$sourcePath\publish_temp"

Write-Host "=== FeedHorn Deployment ===" -ForegroundColor Cyan

# Stop IIS
Write-Host "1. Stopping IIS..." -ForegroundColor Green
iisreset /stop
Start-Sleep -Seconds 3

# Backup database
Write-Host "2. Backing up database..." -ForegroundColor Green
if (Test-Path "$deployPath\feedhorn.db") {
    Copy-Item "$deployPath\feedhorn.db" "$sourcePath\feedhorn.db.backup" -Force
    Write-Host "   Database backed up" -ForegroundColor Green
}

# Clean and build the project
Write-Host "3. Building project..." -ForegroundColor Green
cd $sourcePath
dotnet clean
Remove-Item -Path $tempPublish -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish -c Release -o $tempPublish

# Copy to deployment folder
Write-Host "   Copying to deployment folder..." -ForegroundColor Green
Remove-Item -Path "$deployPath\*" -Recurse -Force -Exclude "feedhorn.db*","logs"
Copy-Item -Path "$tempPublish\*" -Destination $deployPath -Recurse -Force

# Restore database if it was overwritten
Write-Host "4. Restoring database..." -ForegroundColor Green
if (Test-Path "$sourcePath\feedhorn.db.backup") {
    Copy-Item "$sourcePath\feedhorn.db.backup" "$deployPath\feedhorn.db" -Force
    Remove-Item "$sourcePath\feedhorn.db.backup" -Force
    Write-Host "   Database restored" -ForegroundColor Green
}

# Cleanup temp folder
Write-Host "5. Cleaning up..." -ForegroundColor Green
Remove-Item -Path $tempPublish -Recurse -Force -ErrorAction SilentlyContinue

# Start IIS
Write-Host "6. Starting IIS..." -ForegroundColor Green
iisreset /start

Write-Host ""
Write-Host "=== Deployment Complete! ===" -ForegroundColor Cyan
Write-Host "Clear your browser cache (Ctrl+F5) and test" -ForegroundColor Yellow
