# FeedHorn - URL & Network Monitoring Dashboard

A comprehensive monitoring application for tracking website performance, network speed, and SSL certificate expiration. Built with ASP.NET Core for Windows IIS deployment.

## Features

### URL Monitoring
- **HTTP & Ping Checks**: Monitor websites via HTTP/HTTPS or ICMP ping
- **Automatic Monitoring**: Background service checks URLs every 5 minutes
- **Response Time Graphs**: Interactive charts with multiple time ranges (1d, 3d, 5d, 14d, 30d)
- **Smart Alerting**: URLs responding slower than average are automatically highlighted
- **Data Retention**: 60-day historical data with automatic cleanup

### Internet Speed Testing
- **Ookla Integration**: Powered by official Speedtest CLI
- **Hourly Testing**: Automatic speed tests every hour
- **Manual Triggers**: Run tests on-demand
- **Comprehensive Metrics**: Download, upload, ping, and jitter tracking
- **Historical Charts**: Visualize speed trends over time

### SSL Certificate Monitoring
- **Certificate Tracking**: Monitor SSL certificate expiration dates
- **Daily Checks**: Automatic certificate validation every 24 hours
- **Expiration Alerts**: Visual warnings for certificates expiring within 30 days
- **Certificate Details**: View issuer, validity dates, and subject information
- **Manual Refresh**: Check certificates on-demand
- **Horizontal Layout**: Clean, card-based display with at-a-glance status

### UI/UX Features
- **Dark Mode**: Toggle between light and dark themes (persists in localStorage)
- **Print-Friendly**: Optimized layouts for printing reports across all tabs
- **Graph Customization**: Toggle data points on/off globally
- **External Tools**: 23+ diagnostic tools across 9 categories:
  - Security & SSL (SSL Labs, Qualys, DNSSEC Debugger, Sucuri)
  - DNS & Domain (DNSChecker, MXToolbox, WhatsMyDNS, WHOIS)
  - HTTP & API Diagnostics (HTTPStatus.io, Hoppscotch, RequestBin)
  - Network & Latency (Traceroute.org, Fast.com, Cloudflare Radar)
  - Website Analytics & SEO (PageSpeed Insights, BuiltWith)
  - Infrastructure Monitoring (UptimeRobot, HetrixTools, Better Stack)
  - Developer Utilities (JSONLint, RegEx101, JWT.io, Crontab Guru)
- **Responsive Design**: Works on desktop, tablet, and mobile

## Quick Start

### Development

1. **Clone and restore**:
   ```bash
   git clone <repository-url>
   cd FeedHorn
   dotnet restore
   ```

2. **Run the application**:
   ```bash
   dotnet run
   ```

3. **Open your browser**:
   Navigate to `http://localhost:5000` or `https://localhost:5001`

### Production Deployment

For detailed deployment instructions, see [docs/FRESH_INSTALL.md](docs/FRESH_INSTALL.md)

**Quick deploy using provided scripts:**

```powershell
# Fresh installation
.\scripts\install-new-server.ps1

# In-place update (preserves database)
.\scripts\deploy-inplace.ps1
```

## Prerequisites

- .NET 8.0 SDK or Runtime
- Windows Server with IIS (for production)
- Ookla Speedtest CLI (optional, for speed test feature)
- SQLite3 (for database management)

## Project Structure

```
FeedHorn/
├── Controllers/
│   ├── MonitoredUrlsController.cs      # URL monitoring API
│   ├── SpeedTestController.cs          # Speed test API
│   └── SslCertificateController.cs     # SSL certificate API
├── Data/
│   └── FeedHornContext.cs              # Entity Framework DbContext
├── Middleware/
│   └── FooterInjectionMiddleware.cs    # Server-side footer injection
├── Models/
│   ├── MonitoredUrl.cs                 # URL entity
│   ├── UrlCheck.cs                     # Check result entity
│   ├── SpeedTest.cs                    # Speed test entity
│   └── SslCertificate.cs               # SSL certificate entity
├── Services/
│   ├── UrlMonitoringService.cs         # URL monitoring background service
│   ├── SpeedTestService.cs             # Speed test background service
│   └── SslMonitoringService.cs         # SSL monitoring background service
├── wwwroot/
│   ├── index.html                      # Main UI with 23+ external tool links
│   ├── styles.css                      # Styling (light/dark mode, print)
│   ├── app.js                          # Frontend logic
│   ├── logo.svg                        # FeedHorn logo
│   └── favicon.svg                     # Favicon
├── docs/
│   ├── FRESH_INSTALL.md                # Fresh installation guide
│   ├── DEPLOYMENT.md                   # Deployment documentation
│   └── PING_UPDATE.md                  # Ping feature notes
├── scripts/
│   ├── install-new-server.ps1          # Fresh install script
│   ├── deploy-inplace.ps1              # Update script
│   ├── deploy.ps1                      # Legacy deployment
│   └── fix-feedhorn.ps1                # Troubleshooting script
├── add-speedtest-table.sql             # SQL migration for speed tests
├── add-sslcertificates-table.sql       # SQL migration for SSL certs
├── Program.cs                           # Application entry point
├── web.config                           # IIS configuration
└── FeedHorn.csproj                     # Project file
```

## Database Schema

FeedHorn uses SQLite with the following tables:

- **MonitoredUrls**: URLs being monitored
- **UrlChecks**: Historical check results
- **SpeedTests**: Internet speed test results
- **SslCertificates**: SSL certificate tracking

### Adding Tables to Existing Database

If you're upgrading from a previous version:

```powershell
# Add Speed Test table
Get-Content add-speedtest-table.sql | sqlite3 feedhorn.db

# Add SSL Certificate table
Get-Content add-sslcertificates-table.sql | sqlite3 feedhorn.db
```

## API Endpoints

### URL Monitoring
- `GET /api/monitoredurls` - Get all monitored URLs
- `GET /api/monitoredurls/{id}` - Get specific URL
- `POST /api/monitoredurls` - Add new URL
- `PUT /api/monitoredurls/{id}` - Update URL
- `DELETE /api/monitoredurls/{id}` - Delete URL

### Speed Tests
- `GET /api/speedtest` - Get all speed test results
- `GET /api/speedtest/{id}` - Get specific test
- `POST /api/speedtest/run` - Run test now

### SSL Certificates
- `GET /api/sslcertificate` - Get all certificates
- `GET /api/sslcertificate/{id}` - Get specific certificate
- `POST /api/sslcertificate` - Add new certificate
- `PUT /api/sslcertificate/{id}` - Update certificate
- `DELETE /api/sslcertificate/{id}` - Delete certificate
- `POST /api/sslcertificate/{id}/check` - Check certificate now

## Background Services

FeedHorn runs three background services:

1. **UrlMonitoringService**: Checks URLs every 5 minutes
2. **SpeedTestService**: Runs speed tests every hour (at :00)
3. **SslMonitoringService**: Validates SSL certificates daily

Services run continuously via IIS `AlwaysRunning` application pool mode.

## Configuration

### Change Monitoring Intervals

Edit the respective service file:

```csharp
// UrlMonitoringService.cs - Default: 5 minutes
await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

// SpeedTestService.cs - Default: 1 hour (on the hour)
var delay = nextHour - now;
await Task.Delay(delay, stoppingToken);

// SslMonitoringService.cs - Default: 24 hours
await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
```

### Change Data Retention

Default retention periods:
- URL checks: 60 days
- Speed tests: 60 days
- SSL certificates: Kept indefinitely (manual cleanup required)

Edit the cleanup logic in respective services to adjust retention.

## External Tools Integration

The **Tools** tab provides quick access to 23+ popular diagnostic and monitoring tools across 9 categories:

### Security & SSL Tools
- **SSL Labs** - Comprehensive SSL/TLS testing
- **Qualys SSL Server Test** - SSL configuration analysis
- **DNSSEC Debugger** - DNS security validation
- **Sucuri SiteCheck** - Website security scanner

### DNS & Domain Tools
- **DNSChecker** - Global DNS propagation checker
- **MXToolbox** - Email, DNS, and network diagnostics
- **WhatsMyDNS** - DNS propagation worldwide
- **WHOIS Lookup** - Domain registration information

### HTTP & API Diagnostics
- **HTTPStatus.io** - HTTP response testing
- **Hoppscotch** - API development platform
- **RequestBin** - Webhook and HTTP request inspection

### Network & Latency Tools
- **Traceroute.org** - Network path analysis
- **Fast.com** - Netflix speed test
- **Cloudflare Radar** - Internet traffic insights

### Website Analytics & SEO
- **PageSpeed Insights** - Google performance analysis
- **BuiltWith** - Technology profiler

### Infrastructure Monitoring
- **UptimeRobot** - Website uptime monitoring
- **HetrixTools** - Server monitoring
- **Better Stack** - Modern monitoring platform

### Developer Utilities
- **JSONLint** - JSON validator
- **RegEx101** - Regular expression tester
- **JWT.io** - JSON Web Token debugger
- **Crontab Guru** - Cron schedule expressions

All tools open in new tabs for convenient side-by-side comparison with FeedHorn monitoring data.

## Troubleshooting

### Speed Test Not Working

1. Verify `speedtest.exe` is in `tools/` folder
2. Check IIS application pool has execute permissions
3. Review logs in IIS or Windows Event Viewer
4. Run manual test via API: `POST /api/speedtest/run`

### SSL Certificate Errors

- Ensure URLs start with `https://`
- Check server can reach destination (firewall rules)
- Verify .NET can validate SSL certificates (certificate store issues)

### Database Issues

If database is corrupted:
```powershell
# Backup existing
Move-Item feedhorn.db feedhorn.db.bak

# Restart application - new DB will be created
# Then run migration scripts
```

### Background Services Not Running

1. Verify IIS Application Pool settings:
   - Start Mode: AlwaysRunning
   - Idle Timeout: 0 (disabled)
2. Check `web.config` has `hostingModel="inprocess"`
3. Review Windows Event Viewer for errors

For more troubleshooting, see [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md)

## License

MIT License - see [LICENSE](LICENSE) file for details

## Credits

- Built with ASP.NET Core 8.0
- Charts powered by [Chart.js](https://www.chartjs.org/)
- Icons from [Heroicons](https://heroicons.com/)
- Speed tests via [Ookla Speedtest CLI](https://www.speedtest.net/apps/cli)
