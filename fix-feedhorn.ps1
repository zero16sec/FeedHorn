# FeedHorn Database Fix Script
# Run this as Administrator in PowerShell

$feedhornPath = "D:\projects\browsers\FeedHorn\"  # Change if needed
$appPoolName = "FeedHorn"  # Change if needed

Write-Host "=== FeedHorn Database Fix ===" -ForegroundColor Cyan
Write-Host "Path: $feedhornPath" -ForegroundColor Yellow
Write-Host ""

# Step 1: Stop IIS
Write-Host "1. Stopping IIS..." -ForegroundColor Green
iisreset /stop
Start-Sleep -Seconds 3

# Step 2: Check if database exists
$dbPath = Join-Path $feedhornPath "feedhorn.db"
if (-not (Test-Path $dbPath)) {
    Write-Host "Error: Database not found at $dbPath" -ForegroundColor Red
    exit 1
}

# Step 3: Make database writable
Write-Host "2. Making database writable..." -ForegroundColor Green
attrib -r $dbPath

# Step 4: Download sqlite3
Write-Host "3. Downloading SQLite tools..." -ForegroundColor Green
$zipPath = Join-Path $feedhornPath "sqlite.zip"
Invoke-WebRequest -Uri "https://www.sqlite.org/2024/sqlite-tools-win-x64-3450100.zip" -OutFile $zipPath
Expand-Archive -Path $zipPath -DestinationPath $feedhornPath -Force

# Find sqlite3.exe
$sqlitePath = Get-ChildItem -Path $feedhornPath -Recurse -Filter "sqlite3.exe" | Select-Object -First 1 -ExpandProperty FullName

if (-not $sqlitePath) {
    Write-Host "Error: Could not find sqlite3.exe" -ForegroundColor Red
    exit 1
}

# Step 5: Check if column already exists
Write-Host "4. Checking database schema..." -ForegroundColor Green
$checkColumn = & $sqlitePath $dbPath "PRAGMA table_info(MonitoredUrls);" | Select-String "CheckType"

if ($checkColumn) {
    Write-Host "   CheckType column already exists. Skipping ALTER TABLE." -ForegroundColor Yellow
} else {
    Write-Host "5. Adding CheckType column..." -ForegroundColor Green
    try {
        & $sqlitePath $dbPath "ALTER TABLE MonitoredUrls ADD COLUMN CheckType INTEGER NOT NULL DEFAULT 0;"
        Write-Host "   Column added successfully!" -ForegroundColor Green
    } catch {
        Write-Host "   Error adding column: $_" -ForegroundColor Red
    }
}

# Step 6: Verify data
Write-Host "6. Verifying existing URLs..." -ForegroundColor Green
& $sqlitePath $dbPath "SELECT Id, FriendlyName, CheckType FROM MonitoredUrls;"

# Step 7: Clean up
Write-Host "7. Cleaning up..." -ForegroundColor Green
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $feedhornPath "sqlite-tools-win-x64-3450100") -Recurse -Force -ErrorAction SilentlyContinue

# Step 8: Start IIS
Write-Host "8. Starting IIS..." -ForegroundColor Green
iisreset /start

Write-Host ""
Write-Host "=== Fix Complete! ===" -ForegroundColor Cyan
Write-Host "All existing URLs now default to HTTP (CheckType = 0)" -ForegroundColor Green
Write-Host "You should now be able to edit them properly." -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Copy the updated app.js to your server" -ForegroundColor White
Write-Host "2. Clear browser cache (Ctrl+F5)" -ForegroundColor White
Write-Host "3. Try editing a URL - it should show the correct data" -ForegroundColor White
