# Firewall Integration Feature

## Overview

This branch adds comprehensive Palo Alto Networks firewall integration with authentication and policy testing capabilities.

## New Features

### 1. User Authentication
- Login page with username/password authentication
- Default admin account (username: `admin`, password: `admin`)
- Forced password change on first login
- Session-based authentication with secure cookies
- Logout functionality in header
- Password hashing using PBKDF2-SHA256

### 2. Firewall Management
- Add/edit/delete multiple Palo Alto firewalls
- Secure API key storage using ASP.NET Data Protection
- Automatic system info retrieval (hostname, model, serial, version)
- Firewall selection dropdown

### 3. URL Testing
- Test URL categorization from a specific source IP
- Shows URL category as reported by firewall's URL filtering database
- Immediate results display

### 4. Traffic Log Queries
- Query firewall traffic logs for specific source IP
- Optional domain/URL filtering with DNS resolution
- Sinkhole detection (detects if domain resolves to sinkhole CNAME)
- Time window selection (1, 2, or 3 hours ago)
- Displays traffic log details:
  - Source/Destination IPs
  - Time, Action, End Reason
  - Bytes sent/received
  - Application, Category, Port

## Database Changes

Two new tables are automatically created on first run:

### Users Table
```sql
CREATE TABLE Users (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Username TEXT NOT NULL UNIQUE,
    PasswordHash TEXT NOT NULL,
    MustChangePassword INTEGER NOT NULL DEFAULT 0,
    CreatedAt TEXT NOT NULL,
    LastLoginAt TEXT
);
```

Default user created automatically:
- Username: `admin`
- Password: `admin`
- **Must change password on first login**

### PaloAltoFirewalls Table
```sql
CREATE TABLE PaloAltoFirewalls (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FriendlyName TEXT NOT NULL,
    Hostname TEXT NOT NULL,
    EncryptedApiKey TEXT NOT NULL,
    FirewallHostname TEXT,
    Model TEXT,
    SerialNumber TEXT,
    SoftwareVersion TEXT,
    CreatedAt TEXT NOT NULL,
    LastTestedAt TEXT
);
```

## Fresh Deployment Steps

### Prerequisites
- .NET 8.0 Runtime
- Windows Server with IIS
- Palo Alto firewall with:
  - API access enabled
  - Admin account with API permissions
  - Reachable management interface from web server

### Deployment Process

1. **Build the application** (on development machine):
   ```powershell
   dotnet publish -c Release -o publish
   ```

2. **Copy files to server**:
   - Copy entire `publish` folder to server (e.g., `C:\inetpub\FeedHorn_Test`)

3. **Create IIS website**:
   ```powershell
   # Create application pool
   New-WebAppPool -Name "FeedHorn_Test"
   Set-ItemProperty IIS:\AppPools\FeedHorn_Test -Name processModel.identityType -Value 0
   Set-ItemProperty IIS:\AppPools\FeedHorn_Test -Name startMode -Value AlwaysRunning
   Set-ItemProperty IIS:\AppPools\FeedHorn_Test -Name processModel.idleTimeout -Value 0

   # Create website
   New-Website -Name "FeedHorn_Test" `
               -PhysicalPath "C:\inetpub\FeedHorn_Test" `
               -ApplicationPool "FeedHorn_Test" `
               -Port 8080
   ```

4. **First run initialization**:
   - Database is automatically created (`feedhorn.db`)
   - Tables are automatically created
   - Default admin user is automatically created

5. **First login**:
   - Navigate to `http://server:8080/`
   - Login with `admin` / `admin`
   - You'll be forced to change the password immediately

6. **Add a firewall**:
   - Navigate to Firewall tab
   - Click "Add Firewall"
   - Enter:
     - Friendly Name (e.g., "Main Firewall")
     - Firewall IP/Hostname (management interface)
     - Username (admin account)
     - Password
   - System automatically:
     - Calls API to get API key
     - Retrieves system info
     - Encrypts and stores API key
     - **Does NOT store username/password**

## API Endpoints

### Authentication
- `POST /api/auth/login` - Login
- `POST /api/auth/logout` - Logout
- `POST /api/auth/change-password` - Change password
- `GET /api/auth/status` - Get auth status

### Firewall Management
- `GET /api/firewall` - List all firewalls
- `GET /api/firewall/{id}` - Get firewall details
- `POST /api/firewall` - Add new firewall
- `PUT /api/firewall/{id}` - Update firewall
- `DELETE /api/firewall/{id}` - Delete firewall

### Firewall Operations
- `POST /api/firewall/{id}/test-url` - Test URL categorization
  ```json
  {
    "sourceIp": "10.0.0.100",
    "url": "http://example.com"
  }
  ```

- `POST /api/firewall/{id}/query-logs` - Query traffic logs
  ```json
  {
    "sourceIp": "10.0.0.100",
    "domain": "example.com",
    "hoursAgo": 1
  }
  ```

## Security Considerations

### Password Storage
- Passwords hashed using PBKDF2-SHA256 with 10,000 iterations
- 16-byte random salt per password
- Never stored in plaintext

### API Key Storage
- Encrypted using ASP.NET Data Protection API
- Encryption keys stored in:
  - `%LOCALAPPDATA%\ASP.NET\DataProtection-Keys` (user profile)
  - Tied to machine and user account
- Cannot be decrypted if:
  - Moved to different machine
  - Run under different user account

### Session Management
- Secure, HttpOnly cookies
- SameSite=Strict
- 7-day expiration
- Logout clears session

### HTTPS
- **Firewall API calls bypass SSL validation** (uses custom handler)
  - Required because most firewall management interfaces use self-signed certificates
  - Traffic still encrypted, just certificate not validated
  - Only affects firewall communication, not user traffic

## Testing Checklist

### Authentication
- [ ] Fresh deployment creates default admin user
- [ ] Login with admin/admin works
- [ ] Forced password change modal appears
- [ ] Cannot access main app without changing password
- [ ] Changed password works for subsequent logins
- [ ] Logout works
- [ ] Accessing `/` without login redirects to `/login.html`
- [ ] API calls without auth return 401

### Firewall Management
- [ ] Can add new firewall with valid credentials
- [ ] Invalid credentials show error
- [ ] Unreachable firewall shows error
- [ ] System info populated correctly (hostname, model, serial, version)
- [ ] Can select firewall from dropdown
- [ ] Firewall info displays correctly
- [ ] Can edit firewall (update credentials)
- [ ] Can delete firewall with confirmation
- [ ] Deleting firewall removes from dropdown

### URL Testing
- [ ] Cannot test without selecting firewall
- [ ] Test URL returns category
- [ ] Results display correctly
- [ ] Invalid source IP shows error
- [ ] Invalid URL shows error

### Log Queries
- [ ] Cannot query without selecting firewall
- [ ] Query with source IP only works
- [ ] Query with source IP + domain works
- [ ] DNS resolution shows resolved IPs
- [ ] Sinkhole detection works (shows warning)
- [ ] Time window selection works (1/2/3 hours)
- [ ] Log results table displays correctly
- [ ] Action colors (green=allow, red=deny) work
- [ ] Bytes formatted correctly (KB/MB/GB)
- [ ] Empty results show appropriate message

### UI/UX
- [ ] All tabs still work
- [ ] Dark mode toggle works
- [ ] Firewall tab loads firewalls on open
- [ ] Modals open/close correctly
- [ ] Form validation works
- [ ] Error messages display correctly
- [ ] Loading states show during API calls

### Database
- [ ] Database created automatically
- [ ] Tables created automatically
- [ ] Default user created automatically
- [ ] Data persists across restarts
- [ ] Encrypted API keys remain valid after restart

## Known Limitations

1. **Single user system** - Only one user account (admin) is supported
2. **No password recovery** - If admin password is lost, must manually reset database
3. **No API key rotation** - Must delete and re-add firewall to rotate API key
4. **Self-signed cert bypass** - Firewall API calls don't validate SSL certificates
5. **Local DNS resolution** - Uses server's DNS resolver, not firewall's DNS proxy directly
6. **Traffic log limits** - Maximum 100 log entries per query
7. **Time window limits** - Only 1, 2, or 3 hours lookback supported

## Troubleshooting

### Login page keeps redirecting
- Clear browser cookies
- Check `Users` table exists in database
- Verify default user was created: `sqlite3 feedhorn.db "SELECT * FROM Users;"`

### Firewall API key errors
- API key may be expired or revoked
- Delete and re-add firewall to get new API key
- Check firewall API access is enabled

### DNS resolution not working
- Verify server can resolve DNS
- Check firewall's DNS sinkhole configuration
- Domain may not have DNS records

### Traffic log queries return no results
- Check firewall has logs for that time period
- Verify source IP has traffic in firewall logs
- Ensure firewall logging is enabled

### Data Protection errors
- Encryption keys may be missing or inaccessible
- Check `%LOCALAPPDATA%\ASP.NET\DataProtection-Keys`
- May need to recreate encryption keys (will invalidate stored API keys)

## Migration from Existing FeedHorn

If upgrading an existing FeedHorn installation:

1. **Database will auto-upgrade**:
   - Existing tables remain untouched
   - New tables added automatically
   - Default admin user created

2. **No data loss**:
   - URL monitoring continues working
   - Speed tests continue working
   - SSL certificates continue working

3. **Access changes**:
   - Must login with admin/admin on first access
   - Must change password immediately
   - All users will use same admin account

## File Structure

New files added:
```
FeedHorn/
├── Controllers/
│   ├── AuthController.cs          # Authentication API
│   └── FirewallController.cs      # Firewall management API
├── Middleware/
│   └── AuthenticationMiddleware.cs # Auth protection
├── Models/
│   ├── User.cs                    # User model
│   └── PaloAltoFirewall.cs        # Firewall model
├── Services/
│   ├── AuthService.cs             # Password hashing & validation
│   ├── EncryptionService.cs       # API key encryption
│   └── PaloAltoService.cs         # PAN-OS API integration
├── wwwroot/
│   └── login.html                 # Login page
└── docs/
    └── FIREWALL_FEATURE.md        # This file
```

Modified files:
- `Data/FeedHornContext.cs` - Added Users and PaloAltoFirewalls DbSets
- `Program.cs` - Added services, middleware, default user creation
- `wwwroot/index.html` - Added Firewall tab, logout button, firewall modal
- `wwwroot/app.js` - Added firewall tab JavaScript
- `wwwroot/styles.css` - Added firewall tab styles

## Support

For issues or questions about this feature:
1. Check logs in Windows Event Viewer
2. Check browser console for JavaScript errors
3. Verify firewall API access works (test with curl/Postman)
4. Check database integrity: `sqlite3 feedhorn.db ".tables"`
