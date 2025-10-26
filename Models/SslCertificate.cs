namespace FeedHorn.Models;

public class SslCertificate
{
    public int Id { get; set; }
    public string FriendlyName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public DateTime LastChecked { get; set; }
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public int DaysUntilExpiration { get; set; }
}
