using FeedHorn.Data;
using FeedHorn.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace FeedHorn.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SslCertificateController : ControllerBase
{
    private readonly FeedHornContext _context;
    private readonly ILogger<SslCertificateController> _logger;

    public SslCertificateController(FeedHornContext context, ILogger<SslCertificateController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<SslCertificate>>> GetAll()
    {
        return await _context.SslCertificates
            .OrderBy(c => c.DaysUntilExpiration)
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<SslCertificate>> Get(int id)
    {
        var cert = await _context.SslCertificates.FindAsync(id);
        if (cert == null)
        {
            return NotFound();
        }
        return cert;
    }

    [HttpPost]
    public async Task<ActionResult<SslCertificate>> Create([FromBody] SslCertificate cert)
    {
        if (string.IsNullOrWhiteSpace(cert.FriendlyName) || string.IsNullOrWhiteSpace(cert.Url))
        {
            return BadRequest("Friendly name and URL are required");
        }

        // Ensure URL has https://
        if (!cert.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (cert.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                cert.Url = "https://" + cert.Url.Substring(7);
            }
            else
            {
                cert.Url = "https://" + cert.Url;
            }
        }

        // Check for duplicate URL
        if (await _context.SslCertificates.AnyAsync(c => c.Url == cert.Url))
        {
            return BadRequest("This URL is already being monitored");
        }

        // Fetch SSL certificate info immediately
        await UpdateCertificateInfo(cert);

        _context.SslCertificates.Add(cert);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = cert.Id }, cert);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] SslCertificate cert)
    {
        if (id != cert.Id)
        {
            return BadRequest();
        }

        var existing = await _context.SslCertificates.FindAsync(id);
        if (existing == null)
        {
            return NotFound();
        }

        existing.FriendlyName = cert.FriendlyName;
        existing.Url = cert.Url;

        // Ensure URL has https://
        if (!existing.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (existing.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                existing.Url = "https://" + existing.Url.Substring(7);
            }
            else
            {
                existing.Url = "https://" + existing.Url;
            }
        }

        // Re-check certificate
        await UpdateCertificateInfo(existing);

        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var cert = await _context.SslCertificates.FindAsync(id);
        if (cert == null)
        {
            return NotFound();
        }

        _context.SslCertificates.Remove(cert);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id}/check")]
    public async Task<ActionResult<SslCertificate>> CheckNow(int id)
    {
        var cert = await _context.SslCertificates.FindAsync(id);
        if (cert == null)
        {
            return NotFound();
        }

        await UpdateCertificateInfo(cert);
        await _context.SaveChangesAsync();

        return cert;
    }

    private async Task UpdateCertificateInfo(SslCertificate cert)
    {
        try
        {
            var uri = new Uri(cert.Url);
            var host = uri.Host;
            var port = uri.Port;

            X509Certificate2? certificate = null;

            using var client = new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) =>
                {
                    if (cert != null)
                    {
                        certificate = new X509Certificate2(cert.GetRawCertData());
                    }
                    return true; // Accept all certificates to capture info
                }
            });

            client.Timeout = TimeSpan.FromSeconds(10);

            try
            {
                await client.GetAsync(cert.Url);
            }
            catch
            {
                // We don't care about HTTP errors, we just want the certificate
            }

            if (certificate != null)
            {
                cert.ValidFrom = certificate.NotBefore.ToUniversalTime();
                cert.ValidTo = certificate.NotAfter.ToUniversalTime();
                cert.Issuer = certificate.Issuer;
                cert.Subject = certificate.Subject;
                cert.LastChecked = DateTime.UtcNow;

                var daysUntilExpiration = (cert.ValidTo - DateTime.UtcNow).Days;
                cert.DaysUntilExpiration = daysUntilExpiration;

                // Check if certificate is valid
                var now = DateTime.UtcNow;
                cert.IsValid = now >= cert.ValidFrom && now <= cert.ValidTo;
                cert.ErrorMessage = cert.IsValid ? null :
                    (now < cert.ValidFrom ? "Certificate not yet valid" : "Certificate expired");

                certificate.Dispose();
            }
            else
            {
                cert.IsValid = false;
                cert.ErrorMessage = "Could not retrieve certificate";
                cert.LastChecked = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking SSL certificate for {Url}", cert.Url);
            cert.IsValid = false;
            cert.ErrorMessage = ex.Message;
            cert.LastChecked = DateTime.UtcNow;
        }
    }
}
