namespace FeedHorn.Models;

public class UrlCheck
{
    public int Id { get; set; }
    public int MonitoredUrlId { get; set; }
    public MonitoredUrl? MonitoredUrl { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    public long ResponseTimeMs { get; set; }
    public int StatusCode { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}
