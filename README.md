# FeedHorn - URL Response Monitoring

A web application for monitoring URL response times with real-time graphs and alerting. Built with ASP.NET Core for IIS deployment.

## Features

- **URL Monitoring**: Add multiple URLs with friendly names for easy identification
- **Automatic Checks**: Background service tests URLs every 5 minutes
- **Response Time Graphs**: Visual representation of response times using Chart.js
- **Smart Alerting**: URLs responding slower than average turn red automatically
- **CRUD Operations**: Add, edit, and delete monitored URLs through an intuitive UI
- **IIS Ready**: Configured for deployment on Windows IIS

## Prerequisites

- .NET 8.0 SDK or runtime
- Windows Server with IIS (for production)
- Visual Studio 2022 or VS Code (for development)

## Quick Start

### Development

1. **Restore packages and build**:
   ```bash
   dotnet restore
   dotnet build
   ```

2. **Run the application**:
   ```bash
   dotnet run
   ```

3. **Open your browser**:
   Navigate to `http://localhost:5000` or `https://localhost:5001`

### IIS Deployment

1. **Publish the application**:
   ```bash
   dotnet publish -c Release -o ./publish
   ```

2. **Configure IIS**:
   - Open IIS Manager
   - Create a new Application Pool with .NET CLR version "No Managed Code"
   - Create a new website or application pointing to the `publish` folder
   - Ensure the Application Pool identity has read/write permissions to the application folder

3. **Install ASP.NET Core Runtime**:
   - Download and install the ASP.NET Core Runtime Hosting Bundle from Microsoft
   - Restart IIS after installation

4. **Browse to your site**:
   The application will be available at your configured IIS URL

## Project Structure

```
FeedHorn/
├── Controllers/
│   └── MonitoredUrlsController.cs    # API endpoints for URL management
├── Data/
│   └── FeedHornContext.cs           # Entity Framework DbContext
├── Models/
│   ├── MonitoredUrl.cs              # URL entity model
│   └── UrlCheck.cs                  # Check result model
├── Services/
│   └── UrlMonitoringService.cs      # Background service for monitoring
├── wwwroot/
│   ├── index.html                   # Main UI
│   ├── styles.css                   # Styling
│   ├── app.js                       # Frontend logic
│   ├── logo.svg                     # FeedHorn logo
│   └── favicon.svg                  # Favicon
├── Program.cs                        # Application entry point
├── web.config                        # IIS configuration
└── FeedHorn.csproj                  # Project file
```

## How It Works

### Monitoring Process

1. URLs are stored in a SQLite database (`feedhorn.db`)
2. A background service (`UrlMonitoringService`) runs continuously
3. Every 5 minutes, the service:
   - Fetches all monitored URLs from the database
   - Makes HTTP requests to each URL
   - Records response time and status code
   - Stores results in the database
4. Old check results are automatically cleaned up (keeps last 100 per URL)

### Smart Alerting

The UI automatically detects slow responses:
- Calculates average response time for each URL
- Marks a URL as "slow" if the latest response is 50% slower than average AND over 1000ms
- Slow URLs get a red border and red graph line
- Returns to normal appearance when response times improve

### API Endpoints

- `GET /api/monitoredurls` - Get all monitored URLs with recent checks
- `GET /api/monitoredurls/{id}` - Get a specific URL
- `POST /api/monitoredurls` - Add a new URL to monitor
- `PUT /api/monitoredurls/{id}` - Update a monitored URL
- `DELETE /api/monitoredurls/{id}` - Remove a URL from monitoring

## Configuration

### Change Monitoring Interval

Edit `Services/UrlMonitoringService.cs`, line with `TimeSpan.FromMinutes(5)`:

```csharp
await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
```

### Change Database

The application uses SQLite by default. To use SQL Server or another database:

1. Update the connection string in `Program.cs`
2. Install appropriate Entity Framework provider
3. Update `FeedHorn.csproj` with the new package reference

## Troubleshooting

### Database Issues

If the database becomes corrupted, simply delete `feedhorn.db` and restart the application. It will create a new database automatically.

### IIS Errors

- **500.19 Error**: Check that the `web.config` is properly configured
- **500.0 Error**: Ensure ASP.NET Core Runtime Hosting Bundle is installed
- **Database Permission Issues**: Grant the Application Pool identity write access to the application folder

### Performance

For monitoring many URLs (50+), consider:
- Increasing the monitoring interval
- Reducing the number of checks stored per URL (modify cleanup logic)
- Using a more robust database (SQL Server)

## License

This project is provided as-is for URL monitoring purposes.

## Credits

- Built with ASP.NET Core 8.0
- Charts powered by Chart.js
- Icons from Heroicons
