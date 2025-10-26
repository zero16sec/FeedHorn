using FeedHorn.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography.X509Certificates;

namespace FeedHorn.Services;

public class SslMonitoringService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SslMonitoringService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(24); // Check daily

    public SslMonitoringService(IServiceProvider serviceProvider, ILogger<SslMonitoringService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SSL Monitoring Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAllCertificates();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SSL monitoring service");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CheckAllCertificates()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FeedHornContext>();

        var certificates = await context.SslCertificates.ToListAsync();

        _logger.LogInformation("Checking {Count} SSL certificates", certificates.Count);

        foreach (var cert in certificates)
        {
            try
            {
                await UpdateCertificateInfo(cert);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking SSL certificate for {Url}", cert.Url);
                cert.IsValid = false;
                cert.ErrorMessage = ex.Message;
                cert.LastChecked = DateTime.UtcNow;
            }
        }

        await context.SaveChangesAsync();
        _logger.LogInformation("SSL certificate check completed");
    }

    private async Task UpdateCertificateInfo(Models.SslCertificate cert)
    {
        try
        {
            var uri = new Uri(cert.Url);

            X509Certificate2? certificate = null;

            using var client = new HttpClient(new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, c, chain, sslPolicyErrors) =>
                {
                    if (c != null)
                    {
                        certificate = new X509Certificate2(c.GetRawCertData());
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
