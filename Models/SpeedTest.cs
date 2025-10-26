namespace FeedHorn.Models;

public class SpeedTest
{
    public int Id { get; set; }
    public DateTime TestedAt { get; set; } = DateTime.UtcNow;
    public double DownloadMbps { get; set; }
    public double UploadMbps { get; set; }
    public int PingMs { get; set; }
    public int JitterMs { get; set; }
    public string? ServerName { get; set; }
    public string? ServerLocation { get; set; }
    public string? Isp { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}
