using FeedHorn.Data;
using FeedHorn.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Text.Json;

namespace FeedHorn.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SpeedTestController : ControllerBase
{
    private readonly FeedHornContext _context;
    private readonly ILogger<SpeedTestController> _logger;
    private readonly string _speedtestPath;

    public SpeedTestController(FeedHornContext context, ILogger<SpeedTestController> logger)
    {
        _context = context;
        _logger = logger;

        // Try to find speedtest.exe in common locations
        // Prefer application directory to avoid permission issues
        var possiblePaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "speedtest.exe"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "speedtest.exe"),
            "speedtest.exe" // In PATH
        };

        _speedtestPath = possiblePaths.FirstOrDefault(System.IO.File.Exists) ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "speedtest.exe");
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetSpeedTests()
    {
        // Get data for last 60 days
        var sixtyDaysAgo = DateTime.UtcNow.AddDays(-60);

        var tests = await _context.SpeedTests
            .Where(t => t.TestedAt >= sixtyDaysAgo)
            .OrderByDescending(t => t.TestedAt)
            .Select(t => new
            {
                t.Id,
                t.TestedAt,
                t.DownloadMbps,
                t.UploadMbps,
                t.PingMs,
                t.JitterMs,
                t.ServerName,
                t.ServerLocation,
                t.Isp,
                t.IsSuccess,
                t.ErrorMessage
            })
            .ToListAsync();

        return Ok(tests);
    }

    [HttpGet("latest")]
    public async Task<ActionResult<object>> GetLatestSpeedTest()
    {
        var latest = await _context.SpeedTests
            .OrderByDescending(t => t.TestedAt)
            .Select(t => new
            {
                t.Id,
                t.TestedAt,
                t.DownloadMbps,
                t.UploadMbps,
                t.PingMs,
                t.JitterMs,
                t.ServerName,
                t.ServerLocation,
                t.Isp,
                t.IsSuccess,
                t.ErrorMessage
            })
            .FirstOrDefaultAsync();

        if (latest == null)
        {
            return NotFound("No speed tests available yet");
        }

        return Ok(latest);
    }

    [HttpPost("run")]
    public async Task<ActionResult<object>> RunSpeedTest()
    {
        _logger.LogInformation("Manual speed test requested");
        _logger.LogInformation("Using speedtest path: {Path}", _speedtestPath);
        _logger.LogInformation("File exists: {Exists}", System.IO.File.Exists(_speedtestPath));

        try
        {
            var result = await ExecuteSpeedTestCli();

            _context.SpeedTests.Add(result);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Manual speed test completed - Download: {Download} Mbps, Upload: {Upload} Mbps, Ping: {Ping} ms",
                result.DownloadMbps,
                result.UploadMbps,
                result.PingMs);

            return Ok(new
            {
                result.Id,
                result.TestedAt,
                result.DownloadMbps,
                result.UploadMbps,
                result.PingMs,
                result.JitterMs,
                result.ServerName,
                result.ServerLocation,
                result.Isp,
                result.IsSuccess,
                result.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual speed test failed. Path: {Path}, File Exists: {Exists}",
                _speedtestPath, System.IO.File.Exists(_speedtestPath));

            var failedTest = new SpeedTest
            {
                IsSuccess = false,
                ErrorMessage = ex.Message
            };

            _context.SpeedTests.Add(failedTest);
            await _context.SaveChangesAsync();

            return StatusCode(500, new { error = ex.Message, path = _speedtestPath });
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
}
