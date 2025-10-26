using FeedHorn.Data;
using FeedHorn.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FeedHorn.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MonitoredUrlsController : ControllerBase
{
    private readonly FeedHornContext _context;

    public MonitoredUrlsController(FeedHornContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetMonitoredUrls()
    {
        // Get data for last 60 days
        var sixtyDaysAgo = DateTime.UtcNow.AddDays(-60);

        var urls = await _context.MonitoredUrls
            .Select(u => new
            {
                u.Id,
                u.FriendlyName,
                u.Url,
                u.CheckType,
                u.CreatedAt,
                Checks = u.Checks
                    .Where(c => c.CheckedAt >= sixtyDaysAgo)
                    .OrderByDescending(c => c.CheckedAt)
                    .Select(c => new
                    {
                        c.CheckedAt,
                        c.ResponseTimeMs,
                        c.StatusCode,
                        c.IsSuccess,
                        c.ErrorMessage
                    }).ToList(),
                AverageResponseTime = u.Checks.Any() ? u.Checks.Average(c => c.ResponseTimeMs) : 0,
                LatestCheck = u.Checks.OrderByDescending(c => c.CheckedAt).FirstOrDefault()
            })
            .ToListAsync();

        return Ok(urls);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult> GetMonitoredUrl(int id)
    {
        var url = await _context.MonitoredUrls
            .Where(u => u.Id == id)
            .Select(u => new
            {
                u.Id,
                u.FriendlyName,
                u.Url,
                u.CheckType,
                u.CreatedAt
            })
            .FirstOrDefaultAsync();

        if (url == null)
        {
            return NotFound();
        }

        return Ok(url);
    }

    [HttpPost]
    public async Task<ActionResult<MonitoredUrl>> CreateMonitoredUrl(MonitoredUrlDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FriendlyName) || string.IsNullOrWhiteSpace(dto.Url))
        {
            return BadRequest("FriendlyName and Url are required");
        }

        // Validate URL format based on check type
        if (dto.CheckType == CheckType.Http)
        {
            if (!Uri.TryCreate(dto.Url, UriKind.Absolute, out _))
            {
                return BadRequest("Invalid URL format. Must be a valid HTTP/HTTPS URL (e.g., https://example.com)");
            }
        }
        // For Ping, we allow hostnames, IPs, or URLs (we'll extract the hostname)

        var existingUrl = await _context.MonitoredUrls.FirstOrDefaultAsync(u => u.Url == dto.Url);
        if (existingUrl != null)
        {
            return Conflict("This URL is already being monitored");
        }

        var monitoredUrl = new MonitoredUrl
        {
            FriendlyName = dto.FriendlyName,
            Url = dto.Url,
            CheckType = dto.CheckType
        };

        _context.MonitoredUrls.Add(monitoredUrl);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetMonitoredUrl), new { id = monitoredUrl.Id }, monitoredUrl);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateMonitoredUrl(int id, MonitoredUrlDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.FriendlyName) || string.IsNullOrWhiteSpace(dto.Url))
        {
            return BadRequest("FriendlyName and Url are required");
        }

        var monitoredUrl = await _context.MonitoredUrls.FindAsync(id);
        if (monitoredUrl == null)
        {
            return NotFound();
        }

        // Validate URL format based on check type
        if (dto.CheckType == CheckType.Http)
        {
            if (!Uri.TryCreate(dto.Url, UriKind.Absolute, out _))
            {
                return BadRequest("Invalid URL format. Must be a valid HTTP/HTTPS URL (e.g., https://example.com)");
            }
        }
        // For Ping, we allow hostnames, IPs, or URLs (we'll extract the hostname)

        // Check if URL already exists (excluding current record)
        var existingUrl = await _context.MonitoredUrls
            .FirstOrDefaultAsync(u => u.Url == dto.Url && u.Id != id);
        if (existingUrl != null)
        {
            return Conflict("This URL is already being monitored");
        }

        monitoredUrl.FriendlyName = dto.FriendlyName;
        monitoredUrl.Url = dto.Url;
        monitoredUrl.CheckType = dto.CheckType;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMonitoredUrl(int id)
    {
        var monitoredUrl = await _context.MonitoredUrls.FindAsync(id);
        if (monitoredUrl == null)
        {
            return NotFound();
        }

        _context.MonitoredUrls.Remove(monitoredUrl);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

public record MonitoredUrlDto(string FriendlyName, string Url, CheckType CheckType = CheckType.Http);
