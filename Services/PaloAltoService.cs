using System.Net;
using System.Text;
using System.Xml.Linq;
using FeedHorn.Models;

namespace FeedHorn.Services;

public class PaloAltoService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PaloAltoService> _logger;

    public PaloAltoService(HttpClient httpClient, ILogger<PaloAltoService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<(bool success, string? apiKey, string? error)> GetApiKey(string hostname, string username, string password)
    {
        try
        {
            var url = $"https://{hostname}/api/?type=keygen&user={Uri.EscapeDataString(username)}&password={Uri.EscapeDataString(password)}";

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };

            using var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(30);

            var response = await client.GetStringAsync(url);
            var xml = XDocument.Parse(response);

            var status = xml.Root?.Attribute("status")?.Value;
            if (status == "success")
            {
                var key = xml.Root?.Element("result")?.Element("key")?.Value;
                return (true, key, null);
            }

            var errorMsg = xml.Root?.Element("result")?.Element("msg")?.Value ?? "Unknown error";
            return (false, null, errorMsg);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting API key from {Hostname}", hostname);
            return (false, null, ex.Message);
        }
    }

    public async Task<(bool success, SystemInfo? info, string? error)> GetSystemInfo(string hostname, string apiKey)
    {
        try
        {
            var url = $"https://{hostname}/api/?type=op&cmd=<show><system><info></info></system></show>&key={apiKey}";

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };

            using var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(30);

            var response = await client.GetStringAsync(url);
            var xml = XDocument.Parse(response);

            var status = xml.Root?.Attribute("status")?.Value;
            if (status != "success")
            {
                var errorMsg = xml.Root?.Element("result")?.Element("msg")?.Value ?? "Failed to get system info";
                return (false, null, errorMsg);
            }

            var system = xml.Root?.Element("result")?.Element("system");
            var info = new SystemInfo
            {
                Hostname = system?.Element("hostname")?.Value ?? system?.Element("devicename")?.Value ?? "Unknown",
                Model = system?.Element("model")?.Value ?? "Unknown",
                SerialNumber = system?.Element("serial")?.Value ?? "Unknown",
                SoftwareVersion = system?.Element("sw-version")?.Value ?? "Unknown"
            };

            return (true, info, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting system info from {Hostname}", hostname);
            return (false, null, ex.Message);
        }
    }

    public async Task<(bool success, UrlTestResult? result, string? error)> TestUrl(string hostname, string apiKey, string sourceIp, string url)
    {
        try
        {
            var cmd = $"<test><url><source>{sourceIp}</source><url>{url}</url></url></test>";
            var apiUrl = $"https://{hostname}/api/?type=op&cmd={Uri.EscapeDataString(cmd)}&key={apiKey}";

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };

            using var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(30);

            var response = await client.GetStringAsync(apiUrl);
            var xml = XDocument.Parse(response);

            var status = xml.Root?.Attribute("status")?.Value;
            if (status != "success")
            {
                var errorMsg = xml.Root?.Element("result")?.Element("msg")?.Value ?? "Test failed";
                return (false, null, errorMsg);
            }

            var result = new UrlTestResult
            {
                Url = url,
                Category = xml.Root?.Element("result")?.Value?.Trim() ?? "unknown",
                SourceIp = sourceIp
            };

            return (true, result, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing URL {Url} from {Hostname}", url, hostname);
            return (false, null, ex.Message);
        }
    }

    public async Task<(bool success, List<string> ips, bool isSinkhole, string? error)> ResolveDomain(string domain)
    {
        try
        {
            var ips = new List<string>();
            var hostEntry = await Dns.GetHostEntryAsync(domain);

            foreach (var address in hostEntry.Aliases.Concat(new[] { hostEntry.HostName }))
            {
                // Check for sinkhole CNAME
                if (address.Contains("sinkhole", StringComparison.OrdinalIgnoreCase))
                {
                    return (true, new List<string> { address }, true, null);
                }
            }

            foreach (var address in hostEntry.AddressList)
            {
                ips.Add(address.ToString());
            }

            return (true, ips, false, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error resolving domain {Domain}", domain);
            return (false, new List<string>(), false, ex.Message);
        }
    }

    public async Task<(bool success, List<TrafficLogEntry> logs, string? error)> QueryTrafficLogs(
        string hostname,
        string apiKey,
        string sourceIp,
        List<string>? destinationIps,
        int hoursAgo,
        int maxLogs = 100)
    {
        try
        {
            // Build filter query
            var filterParts = new List<string>();
            filterParts.Add($"(addr.src eq {sourceIp})");

            if (destinationIps != null && destinationIps.Any())
            {
                var destFilter = string.Join(" or ", destinationIps.Select(ip => $"(addr.dst eq {ip})"));
                filterParts.Add($"( {destFilter} )");
            }

            // Add time window
            var endTime = DateTime.Now;
            var startTime = endTime.AddHours(-hoursAgo);
            var startTimeStr = startTime.ToString("yyyy/MM/dd HH:mm:ss");
            var endTimeStr = endTime.ToString("yyyy/MM/dd HH:mm:ss");

            filterParts.Add($"( time_generated geq \"{startTimeStr}\" )");
            filterParts.Add($"( time_generated leq \"{endTimeStr}\" )");

            var filter = string.Join(" and ", filterParts);
            var encodedFilter = Uri.EscapeDataString(filter);

            var url = $"https://{hostname}/api/?type=log&log-type=traffic&key={apiKey}&query={encodedFilter}&nlogs={maxLogs}&dir=backward";

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };

            using var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(60);

            var response = await client.GetStringAsync(url);
            var xml = XDocument.Parse(response);

            var status = xml.Root?.Attribute("status")?.Value;
            if (status != "success")
            {
                var errorMsg = xml.Root?.Element("result")?.Element("msg")?.Value ?? "Query failed";
                return (false, new List<TrafficLogEntry>(), errorMsg);
            }

            var logs = new List<TrafficLogEntry>();
            var entries = xml.Root?.Element("result")?.Element("log")?.Elements("logs")?.Elements("entry");

            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    var log = new TrafficLogEntry
                    {
                        SourceIp = entry.Element("src")?.Value ?? "",
                        DestinationIp = entry.Element("dst")?.Value ?? "",
                        TimeGenerated = entry.Element("time_generated")?.Value ?? "",
                        Action = entry.Element("action")?.Value ?? "",
                        SessionEndReason = entry.Element("session_end_reason")?.Value ?? "",
                        Bytes = entry.Element("bytes")?.Value ?? "0",
                        BytesSent = entry.Element("bytes_sent")?.Value ?? "0",
                        BytesReceived = entry.Element("bytes_received")?.Value ?? "0",
                        Category = entry.Element("category")?.Value ?? "",
                        Application = entry.Element("app")?.Value ?? "",
                        DestinationPort = entry.Element("dport")?.Value ?? ""
                    };
                    logs.Add(log);
                }
            }

            return (true, logs, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying traffic logs from {Hostname}", hostname);
            return (false, new List<TrafficLogEntry>(), ex.Message);
        }
    }
}

public class SystemInfo
{
    public required string Hostname { get; set; }
    public required string Model { get; set; }
    public required string SerialNumber { get; set; }
    public required string SoftwareVersion { get; set; }
}

public class UrlTestResult
{
    public required string Url { get; set; }
    public required string Category { get; set; }
    public required string SourceIp { get; set; }
}

public class TrafficLogEntry
{
    public required string SourceIp { get; set; }
    public required string DestinationIp { get; set; }
    public required string TimeGenerated { get; set; }
    public required string Action { get; set; }
    public required string SessionEndReason { get; set; }
    public required string Bytes { get; set; }
    public required string BytesSent { get; set; }
    public required string BytesReceived { get; set; }
    public required string Category { get; set; }
    public required string Application { get; set; }
    public required string DestinationPort { get; set; }
}
