using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
// Added references to my models folder
using YouTubeDownloader.Models;

namespace YouTubeDownloader
{
    public partial class VideoPreviewDialog : Window
    {
        private List<FormatInfo> _formats;
        public FormatInfo? SelectedFormat { get; private set; }

        public VideoPreviewDialog(VideoInfo videoInfo, List<FormatInfo> formats)
        {
            InitializeComponent();
            
            _formats = formats;
            
            // Set video title in Title Case (not ALL CAPS)
            TitleText.Text = ConvertToTitleCase(videoInfo.Title);
            
            // Load high-quality thumbnail (maxresdefault)
            if (!string.IsNullOrEmpty(videoInfo.ThumbnailUrl))
            {
                LoadThumbnail(GetMaxResolutionThumbnail(videoInfo.ThumbnailUrl));
            }
            
            // Create quality options
            CreateQualityOptions();
        }
        
        private string ConvertToTitleCase(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            var textInfo = System.Globalization.CultureInfo.CurrentCulture.TextInfo;
            return textInfo.ToTitleCase(text.ToLower());
        }
        
        private string GetMaxResolutionThumbnail(string thumbnailUrl)
        {
            // Try to get the highest quality thumbnail from YouTube
            // Replace default/hqdefault/mqdefault with maxresdefault
            if (thumbnailUrl.Contains("youtube.com") || thumbnailUrl.Contains("ytimg.com"))
            {
                thumbnailUrl = thumbnailUrl
                    .Replace("/default.jpg", "/maxresdefault.jpg")
                    .Replace("/hqdefault.jpg", "/maxresdefault.jpg")
                    .Replace("/mqdefault.jpg", "/maxresdefault.jpg")
                    .Replace("/sddefault.jpg", "/maxresdefault.jpg");
            }
            return thumbnailUrl;
        }

        private async void LoadThumbnail(string url)
        {
            try
            {
                using var client = new HttpClient();
                var bytes = await client.GetByteArrayAsync(url);
                
                var image = new BitmapImage();
                using (var stream = new System.IO.MemoryStream(bytes))
                {
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.StreamSource = stream;
                    image.EndInit();
                }
                
                ThumbnailImage.Source = image;
            }
            catch
            {
                // If thumbnail fails to load, leave the placeholder
            }
        }

        private void CreateQualityOptions()
        {
            bool isFirst = true;
            
            foreach (var format in _formats)
            {
                var radioButton = new RadioButton
                {
                    Style = (Style)FindResource("ResolutionItem"),
                    GroupName = "Quality",
                    IsChecked = isFirst,
                    Tag = format
                };
                
                // Access the template controls
                radioButton.Loaded += (s, e) =>
                {
                    var rb = s as RadioButton;
                    if (rb?.Template.FindName("resolution", rb) is TextBlock resolutionText)
                    {
                        // Format: "1080p (HD)" style
                        var resText = format.Resolution;
                        if (format.Height >= 2160)
                            resText = $"{format.Height}p (4K)";
                        else if (format.Height >= 1080)
                            resText = $"{format.Height}p (HD)";
                        else
                            resText = $"{format.Height}p";
                        
                        resolutionText.Text = resText;
                    }
                    
                    if (rb?.Template.FindName("filesize", rb) is TextBlock filesizeText)
                    {
                        var audioText = format.HasAudio ? "with audio" : "video only";
                        if (format.Filesize > 0)
                        {
                            var sizeInMB = format.Filesize / (1024.0 * 1024.0);
                            filesizeText.Text = $"~{sizeInMB:F1} MB • {audioText}";
                        }
                        else if (format.Bitrate > 0)
                        {
                            // Show bitrate in Mbps for better accuracy
                            var bitrateMbps = format.Bitrate / 1000.0;
                            filesizeText.Text = $"~{bitrateMbps:F1} Mbps • {audioText}";
                        }
                        else
                        {
                            filesizeText.Text = $"Size unknown • {audioText}";
                        }
                    }
                    
                    // Show "Best" badge for highest quality - smaller, pill-shaped
                    if (format.Height == _formats.Max(f => f.Height) && 
                        rb?.Template.FindName("badgeBorder", rb) is Border badgeBorder &&
                        rb?.Template.FindName("badge", rb) is TextBlock badgeText)
                    {
                        badgeText.Text = "BEST";
                        badgeText.Foreground = new SolidColorBrush(Color.FromRgb(29, 78, 216)); // Dark blue text
                        badgeBorder.Background = new SolidColorBrush(Color.FromRgb(219, 234, 254)); // Light blue background
                        badgeBorder.Visibility = Visibility.Visible;
                    }
                };
                
                radioButton.Checked += (s, e) =>
                {
                    SelectedFormat = format;
                };
                
                QualityPanel.Children.Add(radioButton);
                
                if (isFirst)
                {
                    SelectedFormat = format;
                    isFirst = false;
                }
            }
        }

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedFormat != null)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Please select a quality option.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void VideoTab_Checked(object sender, RoutedEventArgs e)
        {
            // Recreate quality options showing video formats
            if (_formats != null)
            {
                QualityPanel.Children.Clear();
                CreateQualityOptions();
            }
        }

        private void AudioTab_Checked(object sender, RoutedEventArgs e)
        {
            // Show audio formats (simplified - just show message for now)
            if (_formats != null)
            {
                QualityPanel.Children.Clear();
                var textBlock = new TextBlock
                {
                    Text = "Audio download coming soon!",
                    FontSize = 15,
                    Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 20)
                };
                QualityPanel.Children.Add(textBlock);
            }
        }
    }
}
