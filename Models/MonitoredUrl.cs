namespace FeedHorn.Models;

public class MonitoredUrl
{
    public int Id { get; set; }
    public required string FriendlyName { get; set; }
    public required string Url { get; set; }
    public CheckType CheckType { get; set; } = CheckType.Http;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<UrlCheck> Checks { get; set; } = new List<UrlCheck>();
}
