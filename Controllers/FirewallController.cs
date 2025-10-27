using FeedHorn.Data;
using FeedHorn.Models;
using FeedHorn.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FeedHorn.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FirewallController : ControllerBase
{
    private readonly FeedHornContext _context;
    private readonly PaloAltoService _paloAltoService;
    private readonly EncryptionService _encryptionService;
    private readonly ILogger<FirewallController> _logger;

    public FirewallController(
        FeedHornContext context,
        PaloAltoService paloAltoService,
        EncryptionService encryptionService,
        ILogger<FirewallController> logger)
    {
        _context = context;
        _paloAltoService = paloAltoService;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> GetAll()
    {
        var firewalls = await _context.PaloAltoFirewalls
            .OrderBy(f => f.FriendlyName)
            .ToListAsync();

        // Don't expose encrypted API keys
        return Ok(firewalls.Select(f => new
        {
            f.Id,
            f.FriendlyName,
            f.Hostname,
            f.FirewallHostname,
            f.Model,
            f.SerialNumber,
            f.SoftwareVersion,
            f.LastTestedAt
        }));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<object>> Get(int id)
    {
        var firewall = await _context.PaloAltoFirewalls.FindAsync(id);
        if (firewall == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            firewall.Id,
            firewall.FriendlyName,
            firewall.Hostname,
            firewall.FirewallHostname,
            firewall.Model,
            firewall.SerialNumber,
            firewall.SoftwareVersion,
            firewall.LastTestedAt
        });
    }

    [HttpPost]
    public async Task<ActionResult<object>> Create([FromBody] CreateFirewallRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FriendlyName) ||
            string.IsNullOrWhiteSpace(request.Hostname) ||
            string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "All fields are required" });
        }

        // Get API key from firewall
        var (success, apiKey, error) = await _paloAltoService.GetApiKey(request.Hostname, request.Username, request.Password);
        if (!success || apiKey == null)
        {
            return BadRequest(new { message = $"Failed to get API key: {error}" });
        }

        // Get system info
        var (infoSuccess, systemInfo, infoError) = await _paloAltoService.GetSystemInfo(request.Hostname, apiKey);
        if (!infoSuccess || systemInfo == null)
        {
            return BadRequest(new { message = $"Failed to get system info: {infoError}" });
        }

        // Encrypt and store
        var encryptedKey = _encryptionService.Encrypt(apiKey);

        var firewall = new PaloAltoFirewall
        {
            FriendlyName = request.FriendlyName,
            Hostname = request.Hostname,
            EncryptedApiKey = encryptedKey,
            FirewallHostname = systemInfo.Hostname,
            Model = systemInfo.Model,
            SerialNumber = systemInfo.SerialNumber,
            SoftwareVersion = systemInfo.SoftwareVersion,
            CreatedAt = DateTime.UtcNow,
            LastTestedAt = DateTime.UtcNow
        };

        _context.PaloAltoFirewalls.Add(firewall);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = firewall.Id }, new
        {
            firewall.Id,
            firewall.FriendlyName,
            firewall.Hostname,
            firewall.FirewallHostname,
            firewall.Model,
            firewall.SerialNumber,
            firewall.SoftwareVersion
        });
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, [FromBody] UpdateFirewallRequest request)
    {
        var firewall = await _context.PaloAltoFirewalls.FindAsync(id);
        if (firewall == null)
        {
            return NotFound();
        }

        // If credentials provided, get new API key
        if (!string.IsNullOrWhiteSpace(request.Username) && !string.IsNullOrWhiteSpace(request.Password))
        {
            var (success, apiKey, error) = await _paloAltoService.GetApiKey(
                request.Hostname ?? firewall.Hostname,
                request.Username,
                request.Password);

            if (!success || apiKey == null)
            {
                return BadRequest(new { message = $"Failed to get API key: {error}" });
            }

            firewall.EncryptedApiKey = _encryptionService.Encrypt(apiKey);

            // Update system info
            var (infoSuccess, systemInfo, infoError) = await _paloAltoService.GetSystemInfo(
                request.Hostname ?? firewall.Hostname,
                apiKey);

            if (infoSuccess && systemInfo != null)
            {
                firewall.FirewallHostname = systemInfo.Hostname;
                firewall.Model = systemInfo.Model;
                firewall.SerialNumber = systemInfo.SerialNumber;
                firewall.SoftwareVersion = systemInfo.SoftwareVersion;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.FriendlyName))
        {
            firewall.FriendlyName = request.FriendlyName;
        }

        if (!string.IsNullOrWhiteSpace(request.Hostname))
        {
            firewall.Hostname = request.Hostname;
        }

        firewall.LastTestedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var firewall = await _context.PaloAltoFirewalls.FindAsync(id);
        if (firewall == null)
        {
            return NotFound();
        }

        _context.PaloAltoFirewalls.Remove(firewall);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id}/test-url")]
    public async Task<ActionResult<object>> TestUrl(int id, [FromBody] TestUrlRequest request)
    {
        var firewall = await _context.PaloAltoFirewalls.FindAsync(id);
        if (firewall == null)
        {
            return NotFound(new { message = "Firewall not found" });
        }

        var apiKey = _encryptionService.Decrypt(firewall.EncryptedApiKey);

        var (success, result, error) = await _paloAltoService.TestUrl(
            firewall.Hostname,
            apiKey,
            request.SourceIp,
            request.Url);

        if (!success)
        {
            return BadRequest(new { message = error });
        }

        firewall.LastTestedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(result);
    }

    [HttpPost("{id}/query-logs")]
    public async Task<ActionResult<object>> QueryLogs(int id, [FromBody] QueryLogsRequest request)
    {
        var firewall = await _context.PaloAltoFirewalls.FindAsync(id);
        if (firewall == null)
        {
            return NotFound(new { message = "Firewall not found" });
        }

        var apiKey = _encryptionService.Decrypt(firewall.EncryptedApiKey);

        // Resolve domain if provided
        List<string>? destinationIps = null;
        bool isSinkhole = false;
        string? resolvedName = null;

        if (!string.IsNullOrWhiteSpace(request.Domain))
        {
            var (dnsSuccess, ips, sinkhole, dnsError) = await _paloAltoService.ResolveDomain(request.Domain);
            if (dnsSuccess)
            {
                destinationIps = ips;
                isSinkhole = sinkhole;
                if (sinkhole && ips.Any())
                {
                    resolvedName = ips.First();
                }
            }
        }

        var (success, logs, error) = await _paloAltoService.QueryTrafficLogs(
            firewall.Hostname,
            apiKey,
            request.SourceIp,
            destinationIps,
            request.HoursAgo,
            100);

        if (!success)
        {
            return BadRequest(new { message = error });
        }

        firewall.LastTestedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            logs,
            dnsResolution = new
            {
                domain = request.Domain,
                resolvedIps = destinationIps,
                isSinkhole,
                resolvedName
            }
        });
    }
}

public class CreateFirewallRequest
{
    public required string FriendlyName { get; set; }
    public required string Hostname { get; set; }
    public required string Username { get; set; }
    public required string Password { get; set; }
}

public class UpdateFirewallRequest
{
    public string? FriendlyName { get; set; }
    public string? Hostname { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public class TestUrlRequest
{
    public required string SourceIp { get; set; }
    public required string Url { get; set; }
}

public class QueryLogsRequest
{
    public required string SourceIp { get; set; }
    public string? Domain { get; set; }
    public required int HoursAgo { get; set; }
}
