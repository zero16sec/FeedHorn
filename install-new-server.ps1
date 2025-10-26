#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Complete FeedHorn installation script for fresh Windows machines

.DESCRIPTION
    Installs .NET 8.0, IIS, creates website, and sets up FeedHorn

.PARAMETER InstallPath
    Where to install FeedHorn (default: C:\inetpub\wwwroot\FeedHorn)

.PARAMETER Port
    Port for the website (default: 80)

.EXAMPLE
    .\install-new-server.ps1

.EXAMPLE
    .\install-new-server.ps1 -InstallPath "D:\FeedHorn" -Port 8080
#>

param(
    [string]$InstallPath = "C:\inetpub\wwwroot\FeedHorn",
    [int]$Port = 80,
    [string]$SiteName = "FeedHorn"
)

Write-Host @"
╔════════════════════════════════════════════╗
║   FeedHorn Complete Installation Script   ║
║        For Fresh Windows Machines          ║
╚════════════════════════════════════════════╝
"@ -ForegroundColor Cyan

Write-Host "`nThis will install:" -ForegroundColor Yellow
Write-Host "  • .NET 8.0 Runtime & Hosting Bundle" -ForegroundColor White
Write-Host "  • IIS Web Server" -ForegroundColor White
Write-Host "  • FeedHorn website on port $Port" -ForegroundColor White
Write-Host "`nInstallation Path: $InstallPath" -ForegroundColor Green
Write-Host ""

$continue = Read-Host "Continue with installation? (Y/N)"
if ($continue -ne "Y" -and $continue -ne "y") {
    Write-Host "`nInstallation cancelled" -ForegroundColor Red
    exit
}

# ============================================================================
# STEP 1: Install .NET 8.0 Hosting Bundle
# ============================================================================
Write-Host "`n[1/6] Installing .NET 8.0 Hosting Bundle..." -ForegroundColor Green

$dotnetCheck = dotnet --list-runtimes 2>$null | Select-String "Microsoft.AspNetCore.App 8.0"

if (-not $dotnetCheck) {
    Write-Host "      Downloading .NET 8.0 Hosting Bundle..." -ForegroundColor Yellow

    $url = "https://download.visualstudio.microsoft.com/download/pr/d8cf1fe3-21c2-4d3a-b4a8-c10d4d59c4a6/a0de8ff1a63c8a9a5e0fa8deae1fdda9/dotnet-hosting-8.0.21-win.exe"
    $installer = "$env:TEMP\dotnet-hosting-8.0.21.exe"

    try {
        Invoke-WebRequest -Uri $url -OutFile $installer -UseBasicParsing
        Write-Host "      Installing (this may take a few minutes)..." -ForegroundColor Yellow
        Start-Process -FilePath $installer -Args "/install /quiet /norestart" -Wait -NoNewWindow
        Remove-Item $installer -Force
        Write-Host "      ✓ .NET 8.0 installed successfully" -ForegroundColor Green
    } catch {
        Write-Host "      ✗ Failed to download/install .NET 8.0" -ForegroundColor Red
        Write-Host "      Please download manually from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
        exit 1
    }
} else {
    Write-Host "      ✓ .NET 8.0 already installed" -ForegroundColor Green
}

# ============================================================================
# STEP 2: Install IIS
# ============================================================================
Write-Host "`n[2/6] Installing IIS..." -ForegroundColor Green

$iisFeature = Get-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole -ErrorAction SilentlyContinue

if ($iisFeature.State -ne "Enabled") {
    Write-Host "      Installing IIS features..." -ForegroundColor Yellow

    $features = @(
        "IIS-WebServerRole",
        "IIS-WebServer",
        "IIS-CommonHttpFeatures",
        "IIS-HttpErrors",
        "IIS-ApplicationDevelopment",
        "IIS-NetFxExtensibility45",
        "IIS-HealthAndDiagnostics",
        "IIS-HttpLogging",
        "IIS-Security",
        "IIS-RequestFiltering",
        "IIS-Performance",
        "IIS-WebServerManagementTools",
        "IIS-ManagementConsole",
        "IIS-ASPNET45"
    )

    foreach ($feature in $features) {
        Enable-WindowsOptionalFeature -Online -FeatureName $feature -All -NoRestart -WarningAction SilentlyContinue | Out-Null
    }

    Write-Host "      ✓ IIS installed successfully" -ForegroundColor Green
} else {
    Write-Host "      ✓ IIS already installed" -ForegroundColor Green
}

# ============================================================================
# STEP 3: Create Directories
# ============================================================================
Write-Host "`n[3/6] Creating installation directories..." -ForegroundColor Green

if (!(Test-Path $InstallPath)) {
    New-Item -Path $InstallPath -ItemType Directory -Force | Out-Null
    Write-Host "      ✓ Created: $InstallPath" -ForegroundColor Green
} else {
    Write-Host "      ✓ Directory already exists: $InstallPath" -ForegroundColor Green
}

$logsPath = Join-Path $InstallPath "logs"
if (!(Test-Path $logsPath)) {
    New-Item -Path $logsPath -ItemType Directory -Force | Out-Null
    Write-Host "      ✓ Created: $logsPath" -ForegroundColor Green
}

# ============================================================================
# STEP 4: Configure IIS Application Pool
# ============================================================================
Write-Host "`n[4/6] Configuring IIS Application Pool..." -ForegroundColor Green

Import-Module WebAdministration -ErrorAction Stop

$appPoolName = $SiteName

if (Test-Path "IIS:\AppPools\$appPoolName") {
    Write-Host "      Removing existing app pool..." -ForegroundColor Yellow
    Remove-WebAppPool -Name $appPoolName
}

New-WebAppPool -Name $appPoolName | Out-Null

# Configure app pool for .NET Core
Set-ItemProperty "IIS:\AppPools\$appPoolName" -Name "managedRuntimeVersion" -Value ""
Set-ItemProperty "IIS:\AppPools\$appPoolName" -Name "startMode" -Value "AlwaysRunning"
Set-ItemProperty "IIS:\AppPools\$appPoolName" -Name "processModel.idleTimeout" -Value "00:00:00"

Write-Host "      ✓ Application pool '$appPoolName' created" -ForegroundColor Green

# ============================================================================
# STEP 5: Create IIS Website
# ============================================================================
Write-Host "`n[5/6] Creating IIS Website..." -ForegroundColor Green

# Stop default website if using port 80
if ($Port -eq 80) {
    if (Test-Path "IIS:\Sites\Default Web Site") {
        Stop-Website -Name "Default Web Site" -ErrorAction SilentlyContinue
        Write-Host "      Default Web Site stopped" -ForegroundColor Yellow
    }
}

# Remove existing site if present
if (Test-Path "IIS:\Sites\$SiteName") {
    Write-Host "      Removing existing website..." -ForegroundColor Yellow
    Remove-Website -Name $SiteName
}

# Create new website
New-Website -Name $SiteName `
            -PhysicalPath $InstallPath `
            -ApplicationPool $appPoolName `
            -Port $Port `
            -Force | Out-Null

Write-Host "      ✓ Website '$SiteName' created on port $Port" -ForegroundColor Green

# ============================================================================
# STEP 6: Set Permissions
# ============================================================================
Write-Host "`n[6/6] Setting file permissions..." -ForegroundColor Green

$acl = Get-Acl $InstallPath
$permission = "IIS AppPool\$appPoolName", "Modify", "ContainerInherit,ObjectInherit", "None", "Allow"
$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule $permission
$acl.SetAccessRule($accessRule)
Set-Acl $InstallPath $acl

Write-Host "      ✓ Permissions granted to IIS AppPool\$appPoolName" -ForegroundColor Green

# ============================================================================
# Restart IIS
# ============================================================================
Write-Host "`nRestarting IIS..." -ForegroundColor Green
net stop was /y | Out-Null
net start w3svc | Out-Null
Write-Host "✓ IIS restarted" -ForegroundColor Green

# ============================================================================
# Installation Complete
# ============================================================================
Write-Host @"

╔════════════════════════════════════════════╗
║      Installation Complete! ✓              ║
╚════════════════════════════════════════════╝
"@ -ForegroundColor Green

Write-Host "`nInstallation Summary:" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray
Write-Host "  Installation Path: " -NoNewline -ForegroundColor White
Write-Host $InstallPath -ForegroundColor Yellow
Write-Host "  Website Name:      " -NoNewline -ForegroundColor White
Write-Host $SiteName -ForegroundColor Yellow
Write-Host "  Port:              " -NoNewline -ForegroundColor White
Write-Host $Port -ForegroundColor Yellow
Write-Host "  App Pool:          " -NoNewline -ForegroundColor White
Write-Host $appPoolName -ForegroundColor Yellow
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Gray

Write-Host "`nNext Steps:" -ForegroundColor Cyan
Write-Host "  1. Copy FeedHorn application files to:" -ForegroundColor White
Write-Host "     $InstallPath" -ForegroundColor Yellow
Write-Host ""
Write-Host "  2. Ensure these files are present:" -ForegroundColor White
Write-Host "     • FeedHorn.dll" -ForegroundColor Gray
Write-Host "     • web.config" -ForegroundColor Gray
Write-Host "     • wwwroot/ folder" -ForegroundColor Gray
Write-Host ""
Write-Host "  3. Browse to:" -ForegroundColor White
Write-Host "     http://localhost:$Port" -ForegroundColor Yellow
Write-Host ""

# Check if localhost is accessible
Write-Host "Testing website..." -ForegroundColor Cyan
try {
    $response = Invoke-WebRequest -Uri "http://localhost:$Port" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
    Write-Host "✓ Website is responding!" -ForegroundColor Green
} catch {
    Write-Host "⚠ Website not responding yet (files may not be deployed)" -ForegroundColor Yellow
}

Write-Host "`n" -NoNewline
