using FeedHorn.Data;
using FeedHorn.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace FeedHorn.Services;

public class UrlMonitoringService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UrlMonitoringService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public UrlMonitoringService(
        IServiceProvider serviceProvider,
        ILogger<UrlMonitoringService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("URL Monitoring Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAllUrls();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in URL monitoring service");
            }

            // Wait 5 minutes before next check
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task CheckAllUrls()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FeedHornContext>();

        var urls = await context.MonitoredUrls.ToListAsync();

        foreach (var monitoredUrl in urls)
        {
            try
            {
                var check = monitoredUrl.CheckType switch
                {
                    CheckType.Http => await CheckHttp(monitoredUrl.Url),
                    CheckType.Ping => await CheckPing(monitoredUrl.Url),
                    _ => throw new NotSupportedException($"Check type {monitoredUrl.CheckType} not supported")
                };

                check.MonitoredUrlId = monitoredUrl.Id;
                context.UrlChecks.Add(check);

                _logger.LogInformation(
                    "Checked {Url} ({Type}) - Status: {Status}, Response Time: {ResponseTime}ms",
                    monitoredUrl.FriendlyName,
                    monitoredUrl.CheckType,
                    check.StatusCode,
                    check.ResponseTimeMs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking URL {Url}", monitoredUrl.FriendlyName);

                context.UrlChecks.Add(new UrlCheck
                {
                    MonitoredUrlId = monitoredUrl.Id,
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    StatusCode = 0,
                    ResponseTimeMs = 0
                });
            }
        }

        await context.SaveChangesAsync();

        // Clean up old checks (keep last 100 per URL)
        await CleanupOldChecks(context);
    }

    private async Task<UrlCheck> CheckHttp(string url)
    {
        var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await httpClient.GetAsync(url);
            stopwatch.Stop();

            return new UrlCheck
            {
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                StatusCode = (int)response.StatusCode,
                IsSuccess = response.IsSuccessStatusCode,
                ErrorMessage = response.IsSuccessStatusCode ? null : $"HTTP {response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return new UrlCheck
            {
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                StatusCode = 0,
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<UrlCheck> CheckPing(string url)
    {
        // Extract hostname from URL if it's a full URL
        string host = url;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            host = uri.Host;
        }
        else
        {
            // Try to parse as just a hostname/IP
            host = url.Replace("http://", "").Replace("https://", "").Split('/')[0];
        }

        using var ping = new Ping();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var reply = await ping.SendPingAsync(host, 30000); // 30 second timeout
            stopwatch.Stop();

            return new UrlCheck
            {
                ResponseTimeMs = reply.RoundtripTime,
                StatusCode = reply.Status == IPStatus.Success ? 200 : 0,
                IsSuccess = reply.Status == IPStatus.Success,
                ErrorMessage = reply.Status != IPStatus.Success ? $"Ping Status: {reply.Status}" : null
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            return new UrlCheck
            {
                ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                StatusCode = 0,
                IsSuccess = false,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task CleanupOldChecks(FeedHornContext context)
    {
        // Delete checks older than 60 days
        var sixtyDaysAgo = DateTime.UtcNow.AddDays(-60);

        var checksToDelete = await context.UrlChecks
            .Where(c => c.CheckedAt < sixtyDaysAgo)
            .ToListAsync();

        if (checksToDelete.Any())
        {
            context.UrlChecks.RemoveRange(checksToDelete);
            _logger.LogInformation("Cleaned up {Count} checks older than 60 days", checksToDelete.Count);
        }

        await context.SaveChangesAsync();
    }
}
