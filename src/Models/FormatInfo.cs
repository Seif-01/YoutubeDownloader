using System;

namespace YouTubeDownloader.Models
{
    // Stores info about different video quality formats
    public class FormatInfo
    {
        public string FormatId { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
        public int Height { get; set; }
        public long Filesize { get; set; }
        public bool HasAudio { get; set; }
        public string Extension { get; set; } = string.Empty;
        public int Fps { get; set; }
        public int Bitrate { get; set; }
    }
}
