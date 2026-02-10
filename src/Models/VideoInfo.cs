using System;

namespace YouTubeDownloader.Models
{
    // This class holds information about a YouTube video
    // I learned about classes from a C# tutorial
    public class VideoInfo
    {
        public string Title { get; set; } = string.Empty;
        public int Duration { get; set; }
        public string Uploader { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
    }
}
