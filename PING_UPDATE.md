# FeedHorn Ping Functionality Update

## Overview

This update adds ping monitoring capabilities alongside the existing HTTP/HTTPS checks. Users can now choose between two monitoring types when adding or editing URLs.

## Changes Made

### Backend Changes

#### 1. New Model: `Models/CheckType.cs`
- Added enum with two values: `Http = 0` and `Ping = 1`

#### 2. Updated: `Models/MonitoredUrl.cs`
- Added `CheckType` property (defaults to `CheckType.Http`)

#### 3. Updated: `Services/UrlMonitoringService.cs`
- Added `using System.Net.NetworkInformation` for Ping support
- Renamed `CheckUrl()` to `CheckHttp()`
- Added new `CheckPing()` method that:
  - Extracts hostname from URL
  - Uses .NET Ping class to ping the host
  - Records roundtrip time
  - Returns status code 200 on success, 0 on failure
- Updated `CheckAllUrls()` to route to correct check method based on CheckType

#### 4. Updated: `Controllers/MonitoredUrlsController.cs`
- Updated `GetMonitoredUrls()` to include `CheckType` in response
- Updated DTO: `MonitoredUrlDto` to include `CheckType` parameter (defaults to Http)
- Updated `CreateMonitoredUrl()` to:
  - Accept and save CheckType
  - Validate URL format only for HTTP checks (Ping allows hostnames/IPs)
- Updated `UpdateMonitoredUrl()` with same validation logic

### Frontend Changes

#### 5. Updated: `wwwroot/index.html`
- Added "Check Type" dropdown with two options:
  - "HTTP/HTTPS Check" (value: 0)
  - "Ping Check" (value: 1)
- Changed URL input type from `url` to `text` to support both URLs and hostnames
- Added help text that changes based on selected check type

#### 6. Updated: `wwwroot/styles.css`
- Added styles for `select` dropdowns
- Added `.form-help` style for help text
- Added `.check-badge` styles for displaying check type
- Added `.check-badge-http` (blue badge) and `.check-badge-ping` (purple badge)
- Updated `.url-info h3` to use flexbox for badge alignment

#### 7. Updated: `wwwroot/app.js`
- Added `handleCheckTypeChange()` function that:
  - Updates placeholder text based on selection
  - Updates help text
  - Changes input type between 'url' and 'text'
- Updated `setupEventListeners()` to listen for check type changes
- Updated `createUrlItem()` to display check type badge next to URL name
- Updated `openModal()` to initialize check type dropdown (defaults to HTTP)
- Updated `loadUrlForEdit()` to load and display existing check type
- Updated `handleSubmit()` to include checkType in API request

## How It Works

### HTTP/HTTPS Check (Type 0)
- Makes HTTP GET request to the URL
- Records response time and status code
- Validates that URL is a proper HTTP/HTTPS URL

### Ping Check (Type 1)
- Extracts hostname from URL (or accepts raw hostname/IP)
- Sends ICMP ping using System.Net.NetworkInformation.Ping
- Records roundtrip time
- Returns status 200 for successful ping, 0 for failure
- Accepts formats:
  - `https://example.com` → pings `example.com`
  - `example.com` → pings `example.com`
  - `192.168.1.1` → pings `192.168.1.1`

## Database Migration

### Important: Database Schema Change

The `MonitoredUrl` table now has a new `CheckType` column.

**For existing deployments:**

#### Option 1: Delete and Recreate Database (Easy, loses data)
1. Stop the application
2. Delete `feedhorn.db`
3. Start the application (database will be recreated with new schema)

#### Option 2: Keep Existing Data (Requires SQLite tools)
If you have existing monitored URLs you want to keep:

```sql
-- Using sqlite3 command line or DB Browser for SQLite
ALTER TABLE MonitoredUrls ADD COLUMN CheckType INTEGER NOT NULL DEFAULT 0;
```

This will add the new column and default all existing URLs to HTTP checks (0).

## Deployment Instructions

### For Fresh Installation
Simply deploy all updated files - the database will be created with the correct schema.

### For Existing Installation

1. **Back up your existing database** (copy `feedhorn.db`)

2. **Choose migration approach:**
   - **Delete database** (easiest): Delete `feedhorn.db` before deploying
   - **Preserve data**: Run the SQL ALTER command above on existing database

3. **Deploy updated files:**
   - `Models/CheckType.cs` (new file)
   - `Models/MonitoredUrl.cs`
   - `Services/UrlMonitoringService.cs`
   - `Controllers/MonitoredUrlsController.cs`
   - `wwwroot/index.html`
   - `wwwroot/styles.css`
   - `wwwroot/app.js`

4. **Restart IIS** or application pool

5. **Clear browser cache** (Ctrl+F5) to load new JavaScript/CSS

## Testing

### Test HTTP Check
1. Click "Add New URL"
2. Enter friendly name: "Test HTTP"
3. Select "HTTP/HTTPS Check"
4. Enter URL: `https://www.google.com`
5. Save and wait 5 minutes for first check

### Test Ping Check
1. Click "Add New URL"
2. Enter friendly name: "Test Ping"
3. Select "Ping Check"
4. Enter hostname: `8.8.8.8` or `google.com`
5. Save and wait 5 minutes for first check

## Visual Indicators

- **HTTP checks** display with a **blue "HTTP" badge**
- **Ping checks** display with a **purple "PING" badge**
- Badges appear next to the friendly name in the URL list

## Notes

- Both check types run every 5 minutes
- Both display response time graphs
- Both support slow response detection and red highlighting
- For ping checks, "Status Code" of 200 means successful ping
- Ping timeout is 30 seconds (same as HTTP)
- Ping requires ICMP permissions on the server (usually available by default)

## Troubleshooting

### Ping checks always fail
- Check firewall rules allow ICMP
- Verify the application has permission to send ping requests
- Check Windows Defender or other security software isn't blocking pings

### Form doesn't show check type dropdown
- Clear browser cache (Ctrl+F5)
- Verify `index.html` was updated
- Check browser console for JavaScript errors

### "Column does not exist" error
- Database needs migration (see Database Migration section above)
- Either delete database or run ALTER TABLE command
