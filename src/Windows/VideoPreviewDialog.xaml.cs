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
            
            // Set video title
            TitleText.Text = videoInfo.Title;
            
            // Load thumbnail
            if (!string.IsNullOrEmpty(videoInfo.ThumbnailUrl))
            {
                LoadThumbnail(videoInfo.ThumbnailUrl);
            }
            
            // Create quality options
            CreateQualityOptions();
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
                    Style = (Style)FindResource("QualityButton"),
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
                        resolutionText.Text = format.Resolution;
                    }
                    
                    if (rb?.Template.FindName("filesize", rb) is TextBlock filesizeText)
                    {
                        var sizeInMB = format.Filesize / (1024.0 * 1024.0);
                        var audioText = format.HasAudio ? "with audio" : "video only";
                        filesizeText.Text = $"~{sizeInMB:F1} MB • {audioText}";
                    }
                    
                    // Show "Best" badge for highest quality
                    if (format.Height == _formats.Max(f => f.Height) && 
                        rb?.Template.FindName("badge", rb) is TextBlock badgeText)
                    {
                        badgeText.Text = "BEST";
                        
                        var gradient = new LinearGradientBrush
                        {
                            StartPoint = new Point(0, 0),
                            EndPoint = new Point(1, 1)
                        };
                        gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0, 122, 255), 0));  // iOS Blue
                        gradient.GradientStops.Add(new GradientStop(Color.FromRgb(0, 81, 213), 1));
                        
                        badgeText.Background = gradient;
                        badgeText.Foreground = Brushes.White;
                        badgeText.Visibility = Visibility.Visible;
                        
                        var border = new Border
                        {
                            CornerRadius = new CornerRadius(6),
                            Child = badgeText
                        };
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

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
