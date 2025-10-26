using FeedHorn.Data;
using FeedHorn.Models;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;

namespace FeedHorn.Services;

public class SpeedTestService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SpeedTestService> _logger;
    private readonly string _speedtestPath;

    public SpeedTestService(
        IServiceProvider serviceProvider,
        ILogger<SpeedTestService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Try to find speedtest.exe in common locations
        // Prefer application directory to avoid permission issues
        var possiblePaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "speedtest.exe"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "speedtest.exe"),
            "speedtest.exe" // In PATH
        };

        _speedtestPath = possiblePaths.FirstOrDefault(File.Exists) ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "speedtest.exe");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Speed Test Service started");

        // Run first test after 1 minute
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSpeedTest();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running speed test");
            }

            // Wait 1 hour before next test
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task RunSpeedTest()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FeedHornContext>();

        _logger.LogInformation("Running speed test...");

        try
        {
            var result = await ExecuteSpeedTestCli();

            context.SpeedTests.Add(result);
            await context.SaveChangesAsync();

            _logger.LogInformation(
                "Speed test completed - Download: {Download} Mbps, Upload: {Upload} Mbps, Ping: {Ping} ms",
                result.DownloadMbps,
                result.UploadMbps,
                result.PingMs);

            // Cleanup old tests (keep last 60 days)
            await CleanupOldTests(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Speed test failed");

            context.SpeedTests.Add(new SpeedTest
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            });

            await context.SaveChangesAsync();
        }
    }

    private async Task<SpeedTest> ExecuteSpeedTestCli()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _speedtestPath,
            Arguments = "--accept-license --accept-gdpr --format=json",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new Exception($"Speedtest failed: {error}");
        }

        // Parse JSON output
        var jsonDoc = JsonDocument.Parse(output);
        var root = jsonDoc.RootElement;

        return new SpeedTest
        {
            IsSuccess = true,
            DownloadMbps = Math.Round(root.GetProperty("download").GetProperty("bandwidth").GetDouble() / 125000.0, 2), // bits to Mbps
            UploadMbps = Math.Round(root.GetProperty("upload").GetProperty("bandwidth").GetDouble() / 125000.0, 2),
            PingMs = (int)root.GetProperty("ping").GetProperty("latency").GetDouble(),
            JitterMs = (int)root.GetProperty("ping").GetProperty("jitter").GetDouble(),
            ServerName = root.GetProperty("server").GetProperty("name").GetString(),
            ServerLocation = $"{root.GetProperty("server").GetProperty("location").GetString()}, {root.GetProperty("server").GetProperty("country").GetString()}",
            Isp = root.GetProperty("isp").GetString()
        };
    }

    private async Task CleanupOldTests(FeedHornContext context)
    {
        var sixtyDaysAgo = DateTime.UtcNow.AddDays(-60);

        var testsToDelete = await context.SpeedTests
            .Where(t => t.TestedAt < sixtyDaysAgo)
            .ToListAsync();

        if (testsToDelete.Any())
        {
            context.SpeedTests.RemoveRange(testsToDelete);
            await context.SaveChangesAsync();
            _logger.LogInformation("Cleaned up {Count} old speed tests", testsToDelete.Count);
        }
    }
}
