namespace FeedHorn.Models;

public class PaloAltoFirewall
{
    public int Id { get; set; }
    public required string FriendlyName { get; set; }
    public required string Hostname { get; set; }
    public required string EncryptedApiKey { get; set; }
    public string? FirewallHostname { get; set; } // From show system info
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? SoftwareVersion { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastTestedAt { get; set; }
}
