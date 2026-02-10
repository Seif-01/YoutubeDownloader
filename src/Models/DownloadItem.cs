using System;
using System.ComponentModel;

namespace YouTubeDownloader.Models
{
    // Download status enum - learned about enums from stackoverflow
    public enum DownloadStatus
    {
        Queued,
        Downloading,
        Paused,
        Completed,
        Failed
    }

    // This class represents a download item in the list
    // implements INotifyPropertyChanged for WPF binding (still learning this part!)
    public class DownloadItem : INotifyPropertyChanged
    {
        private double _progress;
        private double _downloadedSize;
        private DownloadStatus _status;

        public string Title { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public string Quality { get; set; } = string.Empty;
        public double TotalSize { get; set; }
        public string? ThumbnailUrl { get; set; }
        
        // These are for resuming downloads
        public string Url { get; set; } = string.Empty;
        public string FormatId { get; set; } = string.Empty;
        public string? FilePath { get; set; }
        public string AudioFormat { get; set; } = "m4a";

        public double Progress
        {
            get => _progress;
            set
            {
                _progress = value;
                OnPropertyChanged(nameof(Progress));
            }
        }

        public double DownloadedSize
        {
            get => _downloadedSize;
            set
            {
                _downloadedSize = value;
                OnPropertyChanged(nameof(DownloadedSize));
            }
        }

        public DownloadStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
