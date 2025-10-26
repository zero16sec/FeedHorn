# FeedHorn IIS Deployment Guide

## Prerequisites Checklist

### 1. Install ASP.NET Core Runtime Hosting Bundle

Download and install the **ASP.NET Core 8.0 Runtime Hosting Bundle** from:
https://dotnet.microsoft.com/download/dotnet/8.0

- Look for "Hosting Bundle" under the Runtime section
- After installation, **restart IIS** or reboot the server
- Verify installation by running in PowerShell:
  ```powershell
  dotnet --info
  ```

### 2. Configure Application Pool

1. Open **IIS Manager**
2. Go to **Application Pools**
3. Find or create the pool for FeedHorn
4. Right-click → **Basic Settings**:
   - **.NET CLR Version**: **No Managed Code**
   - **Managed Pipeline Mode**: Integrated
5. Right-click → **Advanced Settings**:
   - **Start Mode**: AlwaysRunning (optional, keeps monitoring service alive)
   - **Idle Time-out**: Increase to 0 or higher value (prevents stopping)

### 3. Configure Website

1. Open **IIS Manager**
2. Select your **FeedHorn** site
3. Right-click → **Basic Settings**:
   - **Application pool**: Select the pool configured above
   - **Physical path**: Point to your deployment folder (e.g., `C:\inetpub\FeedHorn\publish`)

### 4. Set Folder Permissions

The Application Pool identity needs **Read/Write** access to the application folder:

**Using IIS Manager:**
1. Select the FeedHorn site
2. Right-click → **Edit Permissions**
3. Go to **Security** tab → **Edit**
4. Click **Add**
5. Enter: `IIS AppPool\[YourAppPoolName]` (e.g., `IIS AppPool\FeedHorn`)
6. Grant **Modify** permissions
7. Click **OK**

**Or using PowerShell (run as Administrator):**
```powershell
$appPoolName = "FeedHorn"  # Replace with your app pool name
$folderPath = "C:\inetpub\FeedHorn\publish"  # Replace with your path

$acl = Get-Acl $folderPath
$permission = "IIS AppPool\$appPoolName", "Modify", "ContainerInherit,ObjectInherit", "None", "Allow"
$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule $permission
$acl.SetAccessRule($accessRule)
Set-Acl $folderPath $acl

Write-Host "Permissions set successfully for IIS AppPool\$appPoolName"
```

### 5. Verify web.config

Your `web.config` should be in the deployment folder. Verify it contains:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet"
                  arguments=".\FeedHorn.dll"
                  stdoutLogEnabled="true"
                  stdoutLogFile=".\logs\stdout"
                  hostingModel="inprocess" />
    </system.webServer>
  </location>
</configuration>
```

**Note:** Set `stdoutLogEnabled="true"` for troubleshooting (change to false later).

### 6. Create Logs Folder

In your deployment folder, create a `logs` folder:
```powershell
New-Item -Path "C:\inetpub\FeedHorn\publish\logs" -ItemType Directory -Force
```

### 7. Start the Application Pool & Website

1. In IIS Manager, go to **Application Pools**
2. Right-click your FeedHorn pool → **Start**
3. Go to **Sites**
4. Right-click FeedHorn → **Start**

### 8. Test the Application

1. Open a browser
2. Navigate to: `http://localhost` or `http://[your-server-ip]`
3. You should see the FeedHorn interface

## Troubleshooting

### Common Errors

#### 500.19 - Configuration Error
- **Cause**: web.config is invalid or ASP.NET Core module not installed
- **Solution**:
  - Install Hosting Bundle
  - Verify web.config syntax
  - Restart IIS: `iisreset`

#### 500.0 - In-Process Handler Load Failure
- **Cause**: ASP.NET Core Runtime not installed or wrong version
- **Solution**:
  - Install ASP.NET Core 8.0 Hosting Bundle
  - Restart IIS

#### 500.30 - In-Process Start Failure
- **Cause**: Application startup error
- **Solution**:
  - Check `logs\stdout-*.log` files
  - Verify FeedHorn.dll exists in deployment folder
  - Check permissions

#### 403 - Forbidden
- **Cause**: Permission issues
- **Solution**:
  - Grant IIS AppPool account Modify permissions (see step 4)

#### Database Errors
- **Cause**: Can't create/write to feedhorn.db
- **Solution**:
  - Ensure IIS AppPool has write permissions to the deployment folder
  - Delete existing feedhorn.db and restart to recreate

### Enable Detailed Error Messages (for troubleshooting only)

Edit `web.config` and change:
```xml
<aspNetCore ... stdoutLogEnabled="true" />
```

Then check logs in the `logs` folder.

### Restart Everything

After configuration changes:
```powershell
# Stop everything
iisreset /stop

# Start IIS
iisreset /start
```

## Verification Checklist

- [ ] ASP.NET Core 8.0 Hosting Bundle installed
- [ ] Application Pool set to "No Managed Code"
- [ ] IIS AppPool account has Modify permissions on deployment folder
- [ ] web.config exists and is valid
- [ ] logs folder exists
- [ ] Application Pool is started
- [ ] Website is started
- [ ] Can browse to http://localhost

## Monitoring Service Notes

The background monitoring service will:
- Start automatically when the website starts
- Check URLs every 5 minutes
- Create `feedhorn.db` SQLite database in the deployment folder
- Keep running as long as the Application Pool is active

To keep the monitoring service always running:
- Set Application Pool **Start Mode** to "AlwaysRunning"
- Set Application Pool **Idle Time-out** to 0 (or high value)
- Consider setting up Application Pool recycling schedule during low-traffic hours

## Speed Test Feature (Ookla CLI)

FeedHorn includes an automated speed test feature that runs hourly using the Ookla Speedtest CLI.

### Installing Ookla Speedtest CLI

1. **Download the Speedtest CLI:**
   - Visit: https://www.speedtest.net/apps/cli
   - Download the Windows version
   - Or use direct download: https://install.speedtest.net/app/cli/ookla-speedtest-1.2.0-win64.zip

2. **Install Speedtest CLI:**

   **Option A: Install to Program Files (Recommended)**
   ```powershell
   # Extract to Program Files
   Expand-Archive -Path "ookla-speedtest-*-win64.zip" -DestinationPath "C:\Program Files\Ookla\Speedtest"
   ```

   **Option B: Install to Application Directory**
   ```powershell
   # Extract to your FeedHorn deployment folder
   Expand-Archive -Path "ookla-speedtest-*-win64.zip" -DestinationPath "C:\inetpub\FeedHorn\publish"
   ```

   **Option C: Add to System PATH**
   - Extract speedtest.exe to a folder (e.g., `C:\Tools\Speedtest`)
   - Add that folder to your System PATH environment variable

3. **Accept License Agreement:**
   Run the Speedtest CLI once to accept the license:
   ```powershell
   # Navigate to where speedtest.exe is located
   cd "C:\Program Files\Ookla\Speedtest"

   # Run once to accept license
   .\speedtest.exe --accept-license --accept-gdpr
   ```

4. **Verify Installation:**
   The SpeedTestService will automatically search for speedtest.exe in these locations:
   - `C:\Program Files\Ookla\Speedtest\speedtest.exe`
   - `C:\Program Files (x86)\Ookla\Speedtest\speedtest.exe`
   - Application directory (e.g., `C:\inetpub\FeedHorn\publish\speedtest.exe`)
   - System PATH

5. **Test Manually:**
   ```powershell
   speedtest.exe --accept-license --accept-gdpr --format=json
   ```

### Speed Test Service Behavior

- Runs automatically every hour
- First test runs 1 minute after application starts
- Stores results in the same `feedhorn.db` database
- Keeps 60 days of historical data
- Results visible in the "Speed Test" tab

### Troubleshooting Speed Tests

If speed tests are not appearing:

1. **Check speedtest.exe is accessible:**
   - Verify the file exists in one of the search locations
   - Ensure IIS AppPool account can execute it

2. **Check application logs:**
   - Look in `logs\stdout-*.log` for errors
   - Search for "Speed Test Service" messages

3. **Test manually as AppPool user:**
   ```powershell
   # Run as the IIS AppPool account to verify permissions
   runas /user:"IIS AppPool\FeedHorn" "speedtest.exe --accept-license --accept-gdpr"
   ```

4. **Check database:**
   - Verify SpeedTests table exists in feedhorn.db
   - Look for records with `IsSuccess = true`

## Security Notes

For production:
1. Use HTTPS (configure SSL certificate in IIS)
2. Set `stdoutLogEnabled="false"` in web.config
3. Consider authentication if exposing to internet
4. Firewall rules if needed
