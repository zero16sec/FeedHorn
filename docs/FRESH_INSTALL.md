# FeedHorn - Fresh Windows Installation Guide

Complete guide for installing FeedHorn on a brand new Windows Server or Windows 10/11 machine.

---

## Prerequisites

- Windows Server 2019+ OR Windows 10/11 Pro
- Administrator access
- Internet connection

---

## Step 1: Install .NET 8.0 Runtime & Hosting Bundle

### Option A: Download Manually

1. Go to: https://dotnet.microsoft.com/en-us/download/dotnet/8.0

2. Download **ASP.NET Core 8.0 Runtime - Windows Hosting Bundle**
   - Direct link: https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/runtime-aspnetcore-8.0.21-windows-hosting-bundle-installer
   - File: `dotnet-hosting-8.0.21-win.exe`

3. Run the installer (requires admin)

4. **Restart your computer** or run:
   ```powershell
   net stop was /y
   net start w3svc
   ```

### Option B: Install via PowerShell (Automated)

Run PowerShell as Administrator and execute:

```powershell
# Download and install .NET 8.0 Hosting Bundle
$url = "https://download.visualstudio.microsoft.com/download/pr/d8cf1fe3-21c2-4d3a-b4a8-c10d4d59c4a6/a0de8ff1a63c8a9a5e0fa8deae1fdda9/dotnet-hosting-8.0.21-win.exe"
$output = "$env:TEMP\dotnet-hosting-8.0.21-win.exe"

Write-Host "Downloading .NET 8.0 Hosting Bundle..." -ForegroundColor Green
Invoke-WebRequest -Uri $url -OutFile $output

Write-Host "Installing .NET 8.0 Hosting Bundle..." -ForegroundColor Green
Start-Process -FilePath $output -Args "/install /quiet /norestart" -Wait

Write-Host "Restarting IIS..." -ForegroundColor Green
net stop was /y
net start w3svc

Write-Host ".NET 8.0 Hosting Bundle installed!" -ForegroundColor Green
```

### Verify Installation

```powershell
dotnet --list-runtimes
```

You should see:
```
Microsoft.AspNetCore.App 8.0.21 [C:\Program Files\dotnet\shared\Microsoft.AspNetCore.App]
Microsoft.NETCore.App 8.0.21 [C:\Program Files\dotnet\shared\Microsoft.NETCore.App]
```

---

## Step 2: Install IIS

Run PowerShell as Administrator:

```powershell
# Install IIS
Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole -All
Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServer -All
Enable-WindowsOptionalFeature -Online -FeatureName IIS-CommonHttpFeatures -All
Enable-WindowsOptionalFeature -Online -FeatureName IIS-HttpErrors -All
Enable-WindowsOptionalFeature -Online -FeatureName IIS-ApplicationDevelopment -All
Enable-WindowsOptionalFeature -Online -FeatureName IIS-NetFxExtensibility45 -All
Enable-WindowsOptionalFeature -Online -FeatureName IIS-HealthAndDiagnostics -All
Enable-WindowsOptionalFeature -Online -FeatureName IIS-HttpLogging -All
Enable-WindowsOptionalFeature -Online -FeatureName IIS-Security -All
Enable-WindowsOptionalFeature -Online -FeatureName IIS-RequestFiltering -All
Enable-WindowsOptionalFeature -Online -FeatureName IIS-Performance -All
Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerManagementTools -All
Enable-WindowsOptionalFeature -Online -FeatureName IIS-ManagementConsole -All
Enable-WindowsOptionalFeature -Online -FeatureName IIS-ASPNET45 -All

Write-Host "IIS installed successfully!" -ForegroundColor Green
```

---

## Step 3: Complete Automated Setup Script

Save this as `install-feedhorn.ps1` and run as Administrator:

```powershell
#Requires -RunAsAdministrator

# FeedHorn Complete Installation Script
# For fresh Windows installations

param(
    [string]$InstallPath = "C:\inetpub\wwwroot\FeedHorn",
    [string]$SiteName = "FeedHorn",
    [int]$Port = 80
)

Write-Host "=== FeedHorn Installation Script ===" -ForegroundColor Cyan
Write-Host ""

# Check if .NET 8.0 is installed
Write-Host "1. Checking .NET 8.0..." -ForegroundColor Green
$dotnetRuntimes = dotnet --list-runtimes 2>$null | Select-String "Microsoft.AspNetCore.App 8.0"

if (-not $dotnetRuntimes) {
    Write-Host "   .NET 8.0 not found. Installing..." -ForegroundColor Yellow

    $url = "https://download.visualstudio.microsoft.com/download/pr/d8cf1fe3-21c2-4d3a-b4a8-c10d4d59c4a6/a0de8ff1a63c8a9a5e0fa8deae1fdda9/dotnet-hosting-8.0.21-win.exe"
    $output = "$env:TEMP\dotnet-hosting-8.0.21-win.exe"

    Invoke-WebRequest -Uri $url -OutFile $output
    Start-Process -FilePath $output -Args "/install /quiet /norestart" -Wait

    Write-Host "   .NET 8.0 installed!" -ForegroundColor Green
} else {
    Write-Host "   .NET 8.0 already installed" -ForegroundColor Green
}

# Install IIS if not present
Write-Host "2. Checking IIS..." -ForegroundColor Green
$iis = Get-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole

if ($iis.State -ne "Enabled") {
    Write-Host "   Installing IIS..." -ForegroundColor Yellow
    Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole -All -NoRestart
    Enable-WindowsOptionalFeature -Online -FeatureName IIS-ASPNET45 -All -NoRestart
    Enable-WindowsOptionalFeature -Online -FeatureName IIS-ManagementConsole -All -NoRestart
    Write-Host "   IIS installed!" -ForegroundColor Green
} else {
    Write-Host "   IIS already installed" -ForegroundColor Green
}

# Create installation directory
Write-Host "3. Creating installation directory..." -ForegroundColor Green
New-Item -Path $InstallPath -ItemType Directory -Force | Out-Null
New-Item -Path "$InstallPath\logs" -ItemType Directory -Force | Out-Null
New-Item -Path "$InstallPath\tools" -ItemType Directory -Force | Out-Null
Write-Host "   Directory created: $InstallPath" -ForegroundColor Green

# Import WebAdministration module
Import-Module WebAdministration

# Create Application Pool
Write-Host "4. Creating Application Pool..." -ForegroundColor Green
$appPoolName = $SiteName

if (Test-Path "IIS:\AppPools\$appPoolName") {
    Remove-WebAppPool -Name $appPoolName
}

New-WebAppPool -Name $appPoolName
Set-ItemProperty "IIS:\AppPools\$appPoolName" -Name "managedRuntimeVersion" -Value ""
Set-ItemProperty "IIS:\AppPools\$appPoolName" -Name "startMode" -Value "AlwaysRunning"
Set-ItemProperty "IIS:\AppPools\$appPoolName" -Name "processModel.idleTimeout" -Value "00:00:00"
Write-Host "   App pool created: $appPoolName" -ForegroundColor Green

# Create Website
Write-Host "5. Creating Website..." -ForegroundColor Green

# Remove default site if using port 80
if ($Port -eq 80) {
    if (Test-Path "IIS:\Sites\Default Web Site") {
        Stop-Website -Name "Default Web Site"
    }
}

# Remove existing site if present
if (Test-Path "IIS:\Sites\$SiteName") {
    Remove-Website -Name $SiteName
}

New-Website -Name $SiteName -PhysicalPath $InstallPath -ApplicationPool $appPoolName -Port $Port
Write-Host "   Website created: $SiteName on port $Port" -ForegroundColor Green

# Set permissions
Write-Host "6. Setting permissions..." -ForegroundColor Green
$acl = Get-Acl $InstallPath
$permission = "IIS AppPool\$appPoolName", "Modify", "ContainerInherit,ObjectInherit", "None", "Allow"
$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule $permission
$acl.SetAccessRule($accessRule)
Set-Acl $InstallPath $acl
Write-Host "   Permissions set for IIS AppPool\$appPoolName" -ForegroundColor Green

# Restart IIS
Write-Host "7. Restarting IIS..." -ForegroundColor Green
iisreset /restart | Out-Null

Write-Host ""
Write-Host "=== Installation Complete! ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "1. Copy FeedHorn application files to: $InstallPath" -ForegroundColor White
Write-Host "2. Install Speedtest CLI to: $InstallPath\tools" -ForegroundColor White
Write-Host "3. Ensure web.config is present" -ForegroundColor White
Write-Host "4. Browse to: http://localhost:$Port" -ForegroundColor White
Write-Host ""
Write-Host "Installation Path: $InstallPath" -ForegroundColor Green
Write-Host "Website Name: $SiteName" -ForegroundColor Green
Write-Host "Port: $Port" -ForegroundColor Green
Write-Host "App Pool: $appPoolName" -ForegroundColor Green
Write-Host ""
Write-Host "Note: Speed Test feature requires Speedtest CLI (see Step 5)" -ForegroundColor Yellow
```

---

## Step 4: Deploy FeedHorn Application

### Option A: Copy Pre-Built Files

1. **On development machine**, publish the app:
   ```bash
   cd /Users/ssivley/projects/browser/FeedHorn
   dotnet publish -c Release -o ./publish
   ```

2. **Copy the entire `publish` folder** to the new server at `C:\inetpub\wwwroot\FeedHorn`

3. **Restart IIS** on the server:
   ```powershell
   iisreset
   ```

### Option B: Build on Server

1. **Copy source files** to server (e.g., `D:\FeedHorn-Source`)

2. **Build and deploy**:
   ```powershell
   cd D:\FeedHorn-Source
   dotnet publish -c Release -o C:\inetpub\wwwroot\FeedHorn
   iisreset
   ```

---

## Step 5: Install Ookla Speedtest CLI (for Speed Test Feature)

FeedHorn includes automated internet speed testing using the Ookla Speedtest CLI.

### Download and Install Speedtest CLI

1. **Download Speedtest CLI**:
   - Visit: https://www.speedtest.net/apps/cli
   - Download the Windows version
   - Or download directly: https://install.speedtest.net/app/cli/ookla-speedtest-1.2.0-win64.zip

2. **Extract and setup** (Run PowerShell as Administrator):

   ```powershell
   # Create tools directory in FeedHorn
   $feedhornPath = "C:\inetpub\wwwroot\FeedHorn"
   New-Item -Path "$feedhornPath\tools" -ItemType Directory -Force

   # Download Speedtest CLI
   $downloadUrl = "https://install.speedtest.net/app/cli/ookla-speedtest-1.2.0-win64.zip"
   $zipPath = "$env:TEMP\speedtest.zip"
   Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath

   # Extract to tools directory
   Expand-Archive -Path $zipPath -DestinationPath "$feedhornPath\tools" -Force

   # Clean up
   Remove-Item $zipPath

   Write-Host "Speedtest CLI installed to $feedhornPath\tools" -ForegroundColor Green
   ```

3. **Accept the license** (required on first run):

   ```powershell
   cd C:\inetpub\wwwroot\FeedHorn\tools
   .\speedtest.exe --accept-license --accept-gdpr
   ```

### Verify Installation

```powershell
C:\inetpub\wwwroot\FeedHorn\tools\speedtest.exe --version
```

You should see the Speedtest CLI version information.

---

## Step 6: Verify Installation

1. **Open browser** and go to: `http://localhost`

2. **You should see** the FeedHorn UI with:
   - Antenna logo header
   - Two tabs: "URL Monitoring" and "Speed Test"

3. **Test URL Monitoring**:
   - Click "Add New URL"
   - Enter name: "Test"
   - Select "HTTP/HTTPS Check"
   - Enter URL: `https://www.google.com`
   - Save
   - Wait 5 minutes and refresh to see first check result

4. **Test Speed Test Feature**:
   - Click the "Speed Test" tab
   - Click "Run Test Now" button
   - Wait 10-30 seconds for test to complete
   - You should see Download/Upload/Ping/Jitter stats and graphs

5. **Background Services**:
   - URL monitoring runs every 5 minutes automatically
   - Speed tests run every hour automatically
   - Both services continue running even when browser is closed

---

## Troubleshooting

### Issue: 500.19 Error
**Solution**: Ensure ASP.NET Core Hosting Bundle is installed and restart IIS

### Issue: 500.31 Error
**Solution**:
```powershell
# Verify .NET 8.0 is installed
dotnet --list-runtimes

# Restart IIS
iisreset
```

### Issue: Can't access database
**Solution**: Check IIS app pool has write permissions
```powershell
$path = "C:\inetpub\wwwroot\FeedHorn"
$acl = Get-Acl $path
$permission = "IIS AppPool\FeedHorn", "Modify", "ContainerInherit,ObjectInherit", "None", "Allow"
$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule $permission
$acl.SetAccessRule($accessRule)
Set-Acl $path $acl
```

### Issue: Site not accessible from network
**Solution**: Add firewall rule
```powershell
New-NetFirewallRule -DisplayName "FeedHorn HTTP" -Direction Inbound -Protocol TCP -LocalPort 80 -Action Allow
```

### Issue: Speed tests not running
**Solution**: Verify Speedtest CLI is installed and accessible
```powershell
# Check if speedtest.exe exists
Test-Path "C:\inetpub\wwwroot\FeedHorn\tools\speedtest.exe"

# If not found, install it (see Step 5)

# Test manually
C:\inetpub\wwwroot\FeedHorn\tools\speedtest.exe --accept-license --accept-gdpr --format=json

# Check database for SpeedTests table
# If missing, see DEPLOYMENT.md for SQL script to add it
```

### Issue: Background services stopped
**Solution**: Verify app pool settings
```powershell
Import-Module WebAdministration

# Check app pool settings
Get-ItemProperty "IIS:\AppPools\FeedHorn" -Name startMode
Get-ItemProperty "IIS:\AppPools\FeedHorn" -Name processModel

# Should show:
# startMode: AlwaysRunning
# idleTimeout: 00:00:00

# Fix if needed:
Set-ItemProperty "IIS:\AppPools\FeedHorn" -Name "startMode" -Value "AlwaysRunning"
Set-ItemProperty "IIS:\AppPools\FeedHorn" -Name "processModel.idleTimeout" -Value "00:00:00"

# Restart
Restart-WebAppPool -Name "FeedHorn"
```

---

## Quick Reference

### Essential Commands

```powershell
# Restart IIS
iisreset

# Check .NET version
dotnet --list-runtimes

# View website status
Get-Website

# Start/Stop site
Start-Website -Name "FeedHorn"
Stop-Website -Name "FeedHorn"

# View app pool status
Get-WebAppPoolState -Name "FeedHorn"

# Restart app pool
Restart-WebAppPool -Name "FeedHorn"

# View logs
Get-Content "C:\inetpub\wwwroot\FeedHorn\logs\stdout-*.log" -Tail 50
```

### File Locations

- **Application**: `C:\inetpub\wwwroot\FeedHorn`
- **Database**: `C:\inetpub\wwwroot\FeedHorn\feedhorn.db`
- **Logs**: `C:\inetpub\wwwroot\FeedHorn\logs`
- **Config**: `C:\inetpub\wwwroot\FeedHorn\web.config`
- **Tools**: `C:\inetpub\wwwroot\FeedHorn\tools` (contains speedtest.exe)

### Features

- **URL Monitoring**: Checks HTTP/HTTPS endpoints or Ping hosts every 5 minutes
- **Speed Testing**: Runs Ookla speed tests every hour automatically
- **Data Retention**: Keeps 60 days of historical data for both features
- **Background Services**: Both services run continuously even when browser is closed
- **Graphs & Analytics**: Time-series graphs with 1d/3d/5d/14d/30d filtering

---

## Security Notes

For production deployments:

1. **Use HTTPS**: Configure SSL certificate in IIS
2. **Change default port**: Use port other than 80
3. **Add authentication**: Configure Windows Auth or implement login
4. **Restrict firewall**: Only allow specific IPs if needed
5. **Backup database**: Schedule regular backups of `feedhorn.db`

---

## Complete Installation Script (All-in-One)

Save as `complete-install.ps1` and run as Administrator:

```powershell
#Requires -RunAsAdministrator

Write-Host "=== FeedHorn Complete Installation ===" -ForegroundColor Cyan
Write-Host "This will install .NET 8.0, IIS, and configure FeedHorn" -ForegroundColor Yellow
Write-Host ""
$continue = Read-Host "Continue? (Y/N)"

if ($continue -ne "Y") {
    Write-Host "Installation cancelled" -ForegroundColor Red
    exit
}

# Install .NET 8.0
Write-Host "`n1. Installing .NET 8.0 Hosting Bundle..." -ForegroundColor Green
$url = "https://download.visualstudio.microsoft.com/download/pr/d8cf1fe3-21c2-4d3a-b4a8-c10d4d59c4a6/a0de8ff1a63c8a9a5e0fa8deae1fdda9/dotnet-hosting-8.0.21-win.exe"
$output = "$env:TEMP\dotnet-hosting.exe"
Invoke-WebRequest -Uri $url -OutFile $output
Start-Process -FilePath $output -Args "/install /quiet /norestart" -Wait
Remove-Item $output

# Install IIS
Write-Host "`n2. Installing IIS..." -ForegroundColor Green
Enable-WindowsOptionalFeature -Online -FeatureName IIS-WebServerRole -All -NoRestart | Out-Null
Enable-WindowsOptionalFeature -Online -FeatureName IIS-ASPNET45 -All -NoRestart | Out-Null
Enable-WindowsOptionalFeature -Online -FeatureName IIS-ManagementConsole -All -NoRestart | Out-Null

# Setup directories
Write-Host "`n3. Creating directories..." -ForegroundColor Green
$installPath = "C:\inetpub\wwwroot\FeedHorn"
New-Item -Path $installPath -ItemType Directory -Force | Out-Null
New-Item -Path "$installPath\logs" -ItemType Directory -Force | Out-Null
New-Item -Path "$installPath\tools" -ItemType Directory -Force | Out-Null

# Create App Pool
Write-Host "`n4. Configuring IIS..." -ForegroundColor Green
Import-Module WebAdministration
New-WebAppPool -Name "FeedHorn" -Force
Set-ItemProperty "IIS:\AppPools\FeedHorn" -Name "managedRuntimeVersion" -Value ""
Set-ItemProperty "IIS:\AppPools\FeedHorn" -Name "startMode" -Value "AlwaysRunning"
Set-ItemProperty "IIS:\AppPools\FeedHorn" -Name "processModel.idleTimeout" -Value "00:00:00"

# Create Website
if (Test-Path "IIS:\Sites\FeedHorn") { Remove-Website -Name "FeedHorn" }
New-Website -Name "FeedHorn" -PhysicalPath $installPath -ApplicationPool "FeedHorn" -Port 80

# Set Permissions
$acl = Get-Acl $installPath
$permission = "IIS AppPool\FeedHorn", "Modify", "ContainerInherit,ObjectInherit", "None", "Allow"
$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule $permission
$acl.SetAccessRule($accessRule)
Set-Acl $installPath $acl

# Restart IIS
iisreset /restart | Out-Null

Write-Host "`n=== Installation Complete! ===" -ForegroundColor Cyan
Write-Host "`nNext Steps:" -ForegroundColor Yellow
Write-Host "1. Copy FeedHorn files to $installPath" -ForegroundColor White
Write-Host "2. Install Speedtest CLI (see Step 5 above)" -ForegroundColor White
Write-Host "3. Browse to http://localhost" -ForegroundColor White
```

### Quick Speedtest CLI Install (Optional Add-on)

Add this to the complete installation script if you want to include Speedtest CLI:

```powershell
# Install Speedtest CLI
Write-Host "`n5. Installing Speedtest CLI..." -ForegroundColor Green
$downloadUrl = "https://install.speedtest.net/app/cli/ookla-speedtest-1.2.0-win64.zip"
$zipPath = "$env:TEMP\speedtest.zip"
Invoke-WebRequest -Uri $downloadUrl -OutFile $zipPath
Expand-Archive -Path $zipPath -DestinationPath "$installPath\tools" -Force
Remove-Item $zipPath

# Accept license
& "$installPath\tools\speedtest.exe" --accept-license --accept-gdpr | Out-Null
Write-Host "   Speedtest CLI installed!" -ForegroundColor Green
```

---

This guide provides everything needed for a fresh Windows installation!
