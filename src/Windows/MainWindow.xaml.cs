// Main Window - This is the main UI window for the YouTube Downloader
// I organized the code into folders to make it cleaner!
// - Models: VideoInfo, FormatInfo, DownloadItem (data classes)
// - Helpers: FileHelper, YtDlpHelper (utility functions)
// - Windows: This file and VideoPreviewDialog (UI code)

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using System.Text.Json;
using System.Threading;
using System.Windows.Media.Imaging;
// Added references to my models and helpers folders
using YouTubeDownloader.Models;
using YouTubeDownloader.Helpers;

namespace YouTubeDownloader
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer _clockTimer;
        private readonly DispatcherTimer _petalTimer;
        private readonly Random _random;
        private readonly string _downloadFolder;
        private readonly string _ytDlpPath;
        private readonly string _downloadsHistoryPath;
        private ObservableCollection<DownloadItem> _downloads;
        private Dictionary<DownloadItem, CancellationTokenSource> _downloadCancellations;
        
        // Quality selector state
        private VideoInfo? _currentVideoInfo;
        private List<FormatInfo>? _currentFormats;
        private FormatInfo? _selectedFormat;
        private string _selectedAudioFormat = "m4a"; // Default to m4a, can be "mp3"
        
        // Dark mode state
        private bool _isDarkMode = false;

        public MainWindow()
        {
            InitializeComponent();
            
            _random = new Random();
            _downloads = new ObservableCollection<DownloadItem>();
            _downloadCancellations = new Dictionary<DownloadItem, CancellationTokenSource>();
            
            // Set download folder
            _downloadFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                "YouTubeDownloader"
            );
            Directory.CreateDirectory(_downloadFolder);
            
            // Set yt-dlp path (will be in the same folder as exe)
            _ytDlpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe");
            
            // Set downloads history path
            _downloadsHistoryPath = Path.Combine(_downloadFolder, "downloads_history.json");
            
            // Check if yt-dlp exists
            CheckYtDlp();
            
            // Load download history
            LoadDownloadHistory();
            
            // Make window draggable
            MouseLeftButtonDown += (s, e) => DragMove();
            
            // Start clock timer
            _clockTimer = new DispatcherTimer();
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += UpdateClock;
            _clockTimer.Start();
            UpdateClock(null, null);
            
            // Start petal animation
            _petalTimer = new DispatcherTimer();
            _petalTimer.Interval = TimeSpan.FromMilliseconds(500);
            _petalTimer.Tick += CreatePetal;
            _petalTimer.Start();
            
            // Save download history on window closing
            Closing += (s, e) => SaveDownloadHistory();
        }

        private void UpdateClock(object? sender, EventArgs? e)
        {
            var now = DateTime.Now;
            TimeDisplay.Text = now.ToString("HH:mm");
            DateDisplay.Text = now.ToString("MMMM dd, dddd");
        }

        private async void CheckYtDlp()
        {
            if (!File.Exists(_ytDlpPath))
            {
                var result = MessageBox.Show(
                    $"yt-dlp.exe not found at:\n{_ytDlpPath}\n\n" +
                    "This app requires yt-dlp to download videos.\n\n" +
                    "Would you like to download it now?",
                    "Missing yt-dlp",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    await DownloadYtDlp();
                }
            }
        }

        private async Task DownloadYtDlp()
        {
            try
            {
                var progressWindow = new Window
                {
                    Title = "Downloading yt-dlp...",
                    Width = 300,
                    Height = 100,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.NoResize
                };

                var textBlock = new TextBlock
                {
                    Text = "Downloading yt-dlp.exe...",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 14
                };

                progressWindow.Content = textBlock;
                progressWindow.Show();

                using var client = new System.Net.Http.HttpClient();
                var url = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
                var response = await client.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(_ytDlpPath, bytes);
                    
                    progressWindow.Close();
                    MessageBox.Show("yt-dlp downloaded successfully!", "Success", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    progressWindow.Close();
                    MessageBox.Show("Failed to download yt-dlp. Please download manually from:\n" +
                        "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe\n\n" +
                        "And place it in the same folder as this application.",
                        "Download Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error downloading yt-dlp: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreatePetal(object? sender, EventArgs? e)
        {
            var petal = new Border
            {
                Width = 12,
                Height = 12,
                Background = new RadialGradientBrush(
                    Color.FromArgb(178, 255, 183, 197),
                    Color.FromArgb(76, 255, 192, 203)
                ),
                CornerRadius = new CornerRadius(6, 0, 6, 0),
                RenderTransformOrigin = new Point(0.5, 0.5),
                Opacity = 0.7
            };

            Canvas.SetLeft(petal, _random.Next(0, 405));
            Canvas.SetTop(petal, -20);
            PetalsCanvas.Children.Add(petal);

            var duration = TimeSpan.FromSeconds(_random.Next(12, 20));
            
            var moveAnimation = new DoubleAnimation
            {
                From = -20,
                To = 740,
                Duration = duration,
                EasingFunction = new SineEase()
            };

            var rotateAnimation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = duration
            };

            var fadeOut = new DoubleAnimation
            {
                From = 0.7,
                To = 0,
                BeginTime = duration - TimeSpan.FromSeconds(2),
                Duration = TimeSpan.FromSeconds(2)
            };

            var rotateTransform = new RotateTransform();
            petal.RenderTransform = rotateTransform;

            petal.BeginAnimation(Canvas.TopProperty, moveAnimation);
            rotateTransform.BeginAnimation(RotateTransform.AngleProperty, rotateAnimation);
            petal.BeginAnimation(OpacityProperty, fadeOut);

            moveAnimation.Completed += (s, _) => PetalsCanvas.Children.Remove(petal);
        }

        private void UrlTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (UrlTextBox.Text == "Paste YouTube Link here...")
            {
                UrlTextBox.Text = "";
                UrlTextBox.Foreground = new SolidColorBrush(Color.FromRgb(26, 26, 46));
            }
        }

        private void UrlTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UrlTextBox.Text))
            {
                UrlTextBox.Text = "Paste YouTube Link here...";
                UrlTextBox.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
            }
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            var url = UrlTextBox.Text;
            
            if (string.IsNullOrWhiteSpace(url) || url == "Paste YouTube Link here...")
            {
                MessageBox.Show("Please paste a YouTube URL first!", "No URL", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check if yt-dlp exists
            if (!File.Exists(_ytDlpPath))
            {
                MessageBox.Show(
                    $"yt-dlp.exe NOT FOUND!\n\n" +
                    $"Looking for it at:\n{_ytDlpPath}\n\n" +
                    $"Please download yt-dlp.exe from:\n" +
                    $"https://github.com/yt-dlp/yt-dlp/releases/latest\n\n" +
                    $"And place it in the same folder as this EXE file.",
                    "Missing yt-dlp", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
                return;
            }

            try
            {
                // Validate URL format
                if (!url.Contains("youtube.com") && !url.Contains("youtu.be"))
                {
                    MessageBox.Show("Please enter a valid YouTube URL.", "Invalid URL",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Show loading indicator
                var loadingWindow = ShowLoadingWindow("Fetching video information...");

                // Get video info and formats
                var (videoInfo, formats) = await GetVideoInfoAndFormatsAsync(url);
                
                loadingWindow.Close();

                if (videoInfo == null || formats == null || formats.Count == 0)
                {
                    MessageBox.Show(
                        "Failed to get video information.\n\n" +
                        "Possible reasons:\n" +
                        "• Video is private or deleted\n" +
                        "• Video is age-restricted\n" +
                        "• Network connection issue\n" +
                        "• yt-dlp needs updating\n\n" +
                        "Check the Output window in Visual Studio for details.",
                        "Error", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Error);
                    return;
                }

                // Store the info and show quality selector
                _currentVideoInfo = videoInfo;
                _currentFormats = formats;
                
                ShowQualitySelector(videoInfo, formats, url);
            }
            catch (Exception ex)
            {
                var errorMessage = "Download failed!\n\n";
                errorMessage += $"Error: {ex.Message}\n\n";
                errorMessage += $"Stack trace:\n{ex.StackTrace}";
                
                MessageBox.Show(errorMessage, "Download Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ShowQualitySelector(VideoInfo videoInfo, List<FormatInfo> formats, string url)
        {
            // Set video title
            QualityVideoTitle.Text = videoInfo.Title;
            
            // Load thumbnail if available
            if (!string.IsNullOrEmpty(videoInfo.ThumbnailUrl))
            {
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(videoInfo.ThumbnailUrl);
                    bitmap.EndInit();
                    QualityThumbnail.Source = bitmap;
                }
                catch
                {
                    // Ignore thumbnail load errors
                }
            }
            
            // Store formats
            _currentFormats = formats;
            _currentVideoInfo = videoInfo;
            
            // Show video formats by default
            ShowFormats(formats.Where(f => f.Height > 0).ToList());
            
            // Show overlay with animation
            QualityOverlay.Visibility = Visibility.Visible;
            
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(200)
            };
            QualityOverlay.BeginAnimation(OpacityProperty, fadeIn);
        }

        private Border CreateFormatButton(FormatInfo format)
        {
            bool isFirst = QualityListPanel.Children.Count == 0;
            if (isFirst) _selectedFormat = format;
            
            var button = new Border
            {
                Background = isFirst ? new SolidColorBrush(Color.FromRgb(230, 242, 255)) : Brushes.White,
                BorderBrush = isFirst ? new SolidColorBrush(Color.FromRgb(0, 122, 255)) : new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(15, 12, 15, 12),
                Margin = new Thickness(0, 0, 0, 10),
                Cursor = Cursors.Hand,
                Tag = format
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textStack = new StackPanel();
            
            var resolutionText = new TextBlock
            {
                Text = format.Height > 0 ? format.Resolution : "Audio Only",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(30, 30, 30))
            };

            var sizeText = new TextBlock
            {
                Text = format.Filesize > 0 ? 
                    $"~{format.Filesize / (1024.0 * 1024.0):F1} MB" :
                    $"~{format.Bitrate / 1000}kbps",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                Margin = new Thickness(0, 4, 0, 0)
            };

            textStack.Children.Add(resolutionText);
            textStack.Children.Add(sizeText);
            Grid.SetColumn(textStack, 0);

            // Add "BEST" badge for highest quality video or highest bitrate audio
            var allFormatsInList = _currentFormats.Where(f => f.Height > 0 ? f.Height > 0 : f.Height == 0).ToList();
            bool isBest = format.Height > 0 ? 
                (format.Height == allFormatsInList.Max(f => f.Height)) :
                (format.Bitrate == allFormatsInList.Max(f => f.Bitrate));
                
            if (isBest)
            {
                var badge = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(0, 122, 255)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8, 4, 8, 4)
                };
                
                var badgeText = new TextBlock
                {
                    Text = "BEST",
                    Foreground = Brushes.White,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold
                };
                
                badge.Child = badgeText;
                Grid.SetColumn(badge, 1);
                grid.Children.Add(badge);
            }

            grid.Children.Add(textStack);
            button.Child = grid;

            // Click handler
            button.MouseDown += (s, e) =>
            {
                _selectedFormat = format;
                
                // Update UI - deselect all
                foreach (Border child in QualityListPanel.Children)
                {
                    child.Background = Brushes.White;
                    child.BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220));
                }
                
                // Select this one
                button.Background = new SolidColorBrush(Color.FromRgb(230, 242, 255));
                button.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 255));
            };

            return button;
        }

        private void CloseQualitySelector_Click(object sender, RoutedEventArgs e)
        {
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(200)
            };
            
            fadeOut.Completed += (s, ev) =>
            {
                QualityOverlay.Visibility = Visibility.Collapsed;
            };
            
            QualityOverlay.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void QualityOverlay_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Close when clicking on dark overlay (not the white panel)
            if (e.OriginalSource == QualityOverlay)
            {
                CloseQualitySelector_Click(sender, null!);
            }
        }

        private void VideoFormatTab_Click(object sender, RoutedEventArgs e)
        {
            // Update tab styles
            VideoFormatTab.Background = Application.Current.Resources["BlueGradient"] as Brush;
            VideoFormatTab.Foreground = Brushes.White;
            AudioFormatTab.Background = new SolidColorBrush(Color.FromRgb(226, 232, 240));
            AudioFormatTab.Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105));

            // Show video formats
            if (_currentFormats != null)
            {
                ShowFormats(_currentFormats.Where(f => f.Height > 0).ToList());
            }
        }

        private void AudioFormatTab_Click(object sender, RoutedEventArgs e)
        {
            // Update tab styles
            AudioFormatTab.Background = Application.Current.Resources["BlueGradient"] as Brush;
            AudioFormatTab.Foreground = Brushes.White;
            VideoFormatTab.Background = new SolidColorBrush(Color.FromRgb(226, 232, 240));
            VideoFormatTab.Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105));

            // Show audio-only formats
            if (_currentFormats != null)
            {
                ShowAudioFormatsWithFormatSelector(_currentFormats.Where(f => f.Height == 0).ToList());
            }
        }

        private void ShowAudioFormatsWithFormatSelector(List<FormatInfo> formats)
        {
            QualityListPanel.Children.Clear();
            _selectedFormat = null;

            // Add format selector (M4A vs MP3)
            var formatSelectorStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 15),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var formatLabel = new TextBlock
            {
                Text = "Audio Format:",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var m4aButton = new Button
            {
                Content = "M4A",
                Padding = new Thickness(20, 8, 20, 8),
                Background = _selectedAudioFormat == "m4a" ? new SolidColorBrush(Color.FromRgb(59, 130, 246)) : new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                Foreground = _selectedAudioFormat == "m4a" ? Brushes.White : new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                BorderThickness = new Thickness(0),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var mp3Button = new Button
            {
                Content = "MP3",
                Padding = new Thickness(20, 8, 20, 8),
                Background = _selectedAudioFormat == "mp3" ? new SolidColorBrush(Color.FromRgb(59, 130, 246)) : new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                Foreground = _selectedAudioFormat == "mp3" ? Brushes.White : new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                BorderThickness = new Thickness(0),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };

            m4aButton.Click += (s, e) =>
            {
                _selectedAudioFormat = "m4a";
                m4aButton.Background = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                m4aButton.Foreground = Brushes.White;
                mp3Button.Background = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                mp3Button.Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105));
            };

            mp3Button.Click += (s, e) =>
            {
                _selectedAudioFormat = "mp3";
                mp3Button.Background = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                mp3Button.Foreground = Brushes.White;
                m4aButton.Background = new SolidColorBrush(Color.FromRgb(226, 232, 240));
                m4aButton.Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105));
            };

            formatSelectorStack.Children.Add(formatLabel);
            formatSelectorStack.Children.Add(m4aButton);
            formatSelectorStack.Children.Add(mp3Button);

            QualityListPanel.Children.Add(formatSelectorStack);

            // Add audio quality options
            bool isFirst = true;
            foreach (var format in formats.OrderByDescending(f => f.Bitrate))
            {
                var button = CreateAudioFormatButton(format, isFirst);
                QualityListPanel.Children.Add(button);
                isFirst = false;
            }
        }

        private Border CreateAudioFormatButton(FormatInfo format, bool isFirst)
        {
            if (isFirst) _selectedFormat = format;
            
            var button = new Border
            {
                Background = isFirst ? new SolidColorBrush(Color.FromRgb(230, 242, 255)) : Brushes.White,
                BorderBrush = isFirst ? new SolidColorBrush(Color.FromRgb(0, 122, 255)) : new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(15, 12, 15, 12),
                Margin = new Thickness(0, 0, 0, 10),
                Cursor = Cursors.Hand,
                Tag = format
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textStack = new StackPanel();
            
            var resolutionText = new TextBlock
            {
                Text = "Audio Only",
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(30, 30, 30))
            };

            var sizeText = new TextBlock
            {
                Text = format.Filesize > 0 ? 
                    $"~{format.Filesize / (1024.0 * 1024.0):F1} MB" :
                    $"~{format.Bitrate / 1000}kbps",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                Margin = new Thickness(0, 4, 0, 0)
            };

            textStack.Children.Add(resolutionText);
            textStack.Children.Add(sizeText);
            Grid.SetColumn(textStack, 0);

            // Add "BEST" badge for highest bitrate audio
            if (_currentFormats != null)
            {
                var audioFormats = _currentFormats.Where(f => f.Height == 0).ToList();
                bool isBest = audioFormats.Count > 0 && format.Bitrate == audioFormats.Max(f => f.Bitrate);
                    
                if (isBest)
                {
                    var badge = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(0, 122, 255)),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(8, 4, 8, 4)
                    };
                    
                    var badgeText = new TextBlock
                    {
                        Text = "BEST",
                        Foreground = Brushes.White,
                        FontSize = 11,
                        FontWeight = FontWeights.Bold
                    };
                    
                    badge.Child = badgeText;
                    Grid.SetColumn(badge, 1);
                    grid.Children.Add(badge);
                }
            }

            grid.Children.Add(textStack);
            button.Child = grid;

            // Click handler
            button.MouseDown += (s, e) =>
            {
                _selectedFormat = format;
                
                // Update UI - deselect all audio format buttons (skip the first child which is the format selector)
                for (int i = 1; i < QualityListPanel.Children.Count; i++)
                {
                    if (QualityListPanel.Children[i] is Border childBorder)
                    {
                        childBorder.Background = Brushes.White;
                        childBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(220, 220, 220));
                    }
                }
                
                // Select this one
                button.Background = new SolidColorBrush(Color.FromRgb(230, 242, 255));
                button.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 122, 255));
            };

            return button;
        }

        private void ShowFormats(List<FormatInfo> formats)
        {
            QualityListPanel.Children.Clear();
            _selectedFormat = null;

            foreach (var format in formats.OrderByDescending(f => f.Bitrate))
            {
                var button = CreateFormatButton(format);
                QualityListPanel.Children.Add(button);
            }
        }

        private async void QualityDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFormat == null || _currentVideoInfo == null)
            {
                MessageBox.Show("Please select a quality option.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Close the selector
            CloseQualitySelector_Click(sender, null!);

            // Start download
            var url = UrlTextBox.Text;
            await StartDownloadAsync(url, _currentVideoInfo, _selectedFormat);
        }

        private Window ShowLoadingWindow(string message)
        {
            var loadingWindow = new Window
            {
                Title = "Please Wait",
                Width = 350,
                Height = 120,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Background = new SolidColorBrush(Color.FromRgb(255, 250, 250))
            };

            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var textBlock = new TextBlock
            {
                Text = message,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(26, 26, 46)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(20)
            };

            stack.Children.Add(textBlock);
            loadingWindow.Content = stack;
            loadingWindow.Show();

            return loadingWindow;
        }

        private async Task StartDownloadAsync(string url, VideoInfo videoInfo, FormatInfo selectedFormat)
        {
            // Check for duplicate downloads
            if (_downloads.Any(d => d.Title == videoInfo.Title && d.Status == DownloadStatus.Downloading))
            {
                MessageBox.Show("This video is already being downloaded!", "Duplicate Download",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Create download item
            var downloadItem = new DownloadItem
            {
                Title = videoInfo.Title,
                Format = selectedFormat.Height > 0 ? "MP4" : (_selectedAudioFormat == "mp3" ? "MP3" : "M4A"),
                Quality = selectedFormat.Resolution,
                TotalSize = selectedFormat.Filesize / (1024.0 * 1024.0),
                Status = DownloadStatus.Downloading,
                Url = url,
                FormatId = selectedFormat.FormatId,
                ThumbnailUrl = videoInfo.ThumbnailUrl,
                AudioFormat = _selectedAudioFormat
            };

            // Add to downloads list
            _downloads.Add(downloadItem);
            AddDownloadToUI(downloadItem);

            // Show downloads panel
            ShowDownloads_Click(null!, null!);

            // Clear URL box
            UrlTextBox.Text = "Paste YouTube Link here...";
            UrlTextBox.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));

            // Create cancellation token
            var cts = new CancellationTokenSource();
            _downloadCancellations[downloadItem] = cts;

            try
            {
                // Create filename and check for duplicates
                var baseFileName = SanitizeFileName(videoInfo.Title);
                string extension;
                
                // Determine extension based on format
                if (selectedFormat.Height > 0)
                {
                    extension = ".mp4"; // Video
                }
                else
                {
                    extension = _selectedAudioFormat == "mp3" ? ".mp3" : ".m4a"; // Audio
                }
                
                var fileName = baseFileName + extension;
                var filePath = Path.Combine(_downloadFolder, fileName);
                
                // Check for duplicates and add (1), (2), etc.
                int counter = 1;
                while (File.Exists(filePath))
                {
                    fileName = $"{baseFileName} ({counter}){extension}";
                    filePath = Path.Combine(_downloadFolder, fileName);
                    counter++;
                }

                // Store the file path in the download item for resume functionality
                downloadItem.FilePath = filePath;

                await DownloadVideoAsync(url, filePath, selectedFormat.FormatId, downloadItem, cts.Token);

                if (!cts.Token.IsCancellationRequested)
                {
                    downloadItem.Status = DownloadStatus.Completed;
                }
            }
            catch (OperationCanceledException)
            {
                // Don't override status if it was set to Paused by user
                if (downloadItem.Status != DownloadStatus.Paused)
                {
                    downloadItem.Status = DownloadStatus.Failed;
                }
            }
            catch (Exception ex)
            {
                downloadItem.Status = DownloadStatus.Failed;
                MessageBox.Show($"Download failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _downloadCancellations.Remove(downloadItem);
            }
        }

        private async Task ResumeDownloadAsync(DownloadItem item)
        {
            // Validate that we have the necessary information to resume
            if (string.IsNullOrEmpty(item.Url) || string.IsNullOrEmpty(item.FormatId) || string.IsNullOrEmpty(item.FilePath))
            {
                MessageBox.Show("Cannot resume: Missing download information.", "Resume Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Change status to downloading
            item.Status = DownloadStatus.Downloading;

            // Create new cancellation token
            var cts = new CancellationTokenSource();
            _downloadCancellations[item] = cts;

            try
            {
                await DownloadVideoAsync(item.Url, item.FilePath, item.FormatId, item, cts.Token);

                if (!cts.Token.IsCancellationRequested)
                {
                    item.Status = DownloadStatus.Completed;
                }
            }
            catch (OperationCanceledException)
            {
                // Don't override status if it was set to Paused by user
                if (item.Status != DownloadStatus.Paused)
                {
                    item.Status = DownloadStatus.Failed;
                }
            }
            catch (Exception ex)
            {
                item.Status = DownloadStatus.Failed;
                MessageBox.Show($"Resume failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _downloadCancellations.Remove(item);
            }
        }

        private async Task<(VideoInfo?, List<FormatInfo>?)> GetVideoInfoAndFormatsAsync(string url)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = _ytDlpPath,
                    Arguments = $"--dump-json --no-playlist \"{url}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                // DEBUG OUTPUT
                System.Diagnostics.Debug.WriteLine($"=== YT-DLP DEBUG ===");
                System.Diagnostics.Debug.WriteLine($"Exit Code: {process.ExitCode}");
                System.Diagnostics.Debug.WriteLine($"Output Length: {output?.Length ?? 0}");
                System.Diagnostics.Debug.WriteLine($"Error: {error}");
                System.Diagnostics.Debug.WriteLine($"==================");

                if (process.ExitCode != 0)
                {
                    // Show the actual error from yt-dlp
                    MessageBox.Show(
                        $"yt-dlp failed to fetch video info.\n\n" +
                        $"Error from yt-dlp:\n{error}\n\n" +
                        $"Exit Code: {process.ExitCode}\n\n" +
                        $"URL attempted: {url}",
                        "yt-dlp Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return (null, null);
                }

                if (string.IsNullOrWhiteSpace(output))
                {
                    MessageBox.Show(
                        "yt-dlp returned no data.\n\n" +
                        "This might mean:\n" +
                        "• The video doesn't exist\n" +
                        "• The URL is incorrect\n" +
                        "• YouTube is blocking the request",
                        "No Data",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return (null, null);
                }

                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    var jsonDoc = JsonDocument.Parse(output);
                    var root = jsonDoc.RootElement;

                    var videoInfo = new VideoInfo
                    {
                        Title = root.GetProperty("title").GetString() ?? "Unknown",
                        Duration = root.TryGetProperty("duration", out var duration) ? duration.GetInt32() : 0,
                        Uploader = root.TryGetProperty("uploader", out var uploader) ? uploader.GetString() ?? "Unknown" : "Unknown",
                        ThumbnailUrl = root.TryGetProperty("thumbnail", out var thumb) ? thumb.GetString() : null
                    };

                    // Parse available formats
                    var formats = new List<FormatInfo>();
                    
                    if (root.TryGetProperty("formats", out var formatsArray))
                    {
                        foreach (var format in formatsArray.EnumerateArray())
                        {
                            try
                            {
                                // Check if format has video or audio
                                var hasVideo = format.TryGetProperty("vcodec", out var vcodec) && 
                                             vcodec.GetString() != "none";
                                var hasAudio = format.TryGetProperty("acodec", out var acodec) && 
                                             acodec.GetString() != "none";

                                // Get bitrate (for audio formats mainly)
                                var bitrate = format.TryGetProperty("abr", out var abr) ? 
                                           (abr.ValueKind == System.Text.Json.JsonValueKind.Number ? (int)abr.GetDouble() : 0) : 0;
                                
                                // If no abr, try tbr (total bitrate)
                                if (bitrate == 0 && format.TryGetProperty("tbr", out var tbr) && 
                                    tbr.ValueKind == System.Text.Json.JsonValueKind.Number)
                                {
                                    bitrate = (int)tbr.GetDouble();
                                }

                                if (hasVideo)
                                {
                                    var height = format.TryGetProperty("height", out var h) ? 
                                               (h.ValueKind == System.Text.Json.JsonValueKind.Number ? h.GetInt32() : 0) : 0;
                                    
                                    var filesize = format.TryGetProperty("filesize", out var fs) ? 
                                                 (fs.ValueKind == System.Text.Json.JsonValueKind.Number ? fs.GetInt64() : 0) :
                                                 (format.TryGetProperty("filesize_approx", out var fsa) ? 
                                                 (fsa.ValueKind == System.Text.Json.JsonValueKind.Number ? fsa.GetInt64() : 0) : 0);
                                    
                                    var formatId = format.TryGetProperty("format_id", out var fid) ? (fid.GetString() ?? "") : "";
                                    var ext = format.TryGetProperty("ext", out var extension) ? (extension.GetString() ?? "mp4") : "mp4";
                                    
                                    // FPS can be int, double, or missing - handle all cases
                                    int fps = 30;
                                    if (format.TryGetProperty("fps", out var fpsVal) && fpsVal.ValueKind == System.Text.Json.JsonValueKind.Number)
                                    {
                                        try
                                        {
                                            fps = fpsVal.TryGetInt32(out var fpsInt) ? fpsInt : (int)fpsVal.GetDouble();
                                        }
                                        catch
                                        {
                                            fps = 30;
                                        }
                                    }

                                    if (height > 0)
                                    {
                                        formats.Add(new FormatInfo
                                        {
                                            FormatId = formatId,
                                            Resolution = $"{height}p",
                                            Height = height,
                                            Filesize = filesize > 0 ? filesize : (height * 1920 * 100), // Estimate if not available
                                            HasAudio = hasAudio,
                                            Extension = ext,
                                            Fps = fps,
                                            Bitrate = bitrate
                                        });
                                    }
                                }
                                else if (hasAudio)
                                {
                                    // Audio-only format
                                    var formatId = format.TryGetProperty("format_id", out var fid) ? (fid.GetString() ?? "") : "";
                                    var ext = format.TryGetProperty("ext", out var extension) ? (extension.GetString() ?? "m4a") : "m4a";
                                    var filesize = format.TryGetProperty("filesize", out var fs) ? 
                                                 (fs.ValueKind == System.Text.Json.JsonValueKind.Number ? fs.GetInt64() : 0) :
                                                 (format.TryGetProperty("filesize_approx", out var fsa) ? 
                                                 (fsa.ValueKind == System.Text.Json.JsonValueKind.Number ? fsa.GetInt64() : 0) : 0);
                                    
                                    formats.Add(new FormatInfo
                                    {
                                        FormatId = formatId,
                                        Resolution = "audio only",
                                        Height = 0,  // 0 indicates audio-only
                                        Filesize = filesize,
                                        HasAudio = true,
                                        Extension = ext,
                                        Fps = 0,
                                        Bitrate = bitrate
                                    });
                                }
                            }
                            catch (Exception formatEx)
                            {
                                // Skip this format if parsing fails
                                System.Diagnostics.Debug.WriteLine($"Skipping format due to parse error: {formatEx.Message}");
                                continue;
                            }
                        }
                    }

                    // Remove duplicates and sort by quality
                    formats = formats
                        .GroupBy(f => f.Height)
                        .Select(g => g.OrderByDescending(f => f.HasAudio).First())
                        .OrderByDescending(f => f.Height)
                        .ToList();

                    // If no formats found, add a default "best" option
                    if (formats.Count == 0)
                    {
                        formats.Add(new FormatInfo
                        {
                            FormatId = "best",
                            Resolution = "Best Available",
                            Height = 1080,
                            Filesize = 50 * 1024 * 1024,
                            HasAudio = true,
                            Extension = "mp4"
                        });
                    }

                    return (videoInfo, formats);
                }

                return (null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error parsing yt-dlp response:\n\n{ex.Message}\n\n" +
                    $"This usually means:\n" +
                    $"• yt-dlp returned invalid data\n" +
                    $"• The video format is unusual\n" +
                    $"• JSON parsing failed\n\n" +
                    $"Full error: {ex.ToString()}",
                    "Parsing Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return (null, null);
            }
        }

        private async Task DownloadVideoAsync(string url, string outputPath, string formatId, DownloadItem downloadItem, CancellationToken cancellationToken)
        {
            var formatArg = formatId == "best" ? "bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best" : formatId;
            
            // Build arguments based on output format
            string arguments;
            if (outputPath.EndsWith(".mp3"))
            {
                // For MP3, extract audio and convert
                arguments = $"--newline --no-playlist -f \"{formatArg}\" " +
                          $"-x --audio-format mp3 --audio-quality 0 -o \"{outputPath}\" \"{url}\"";
            }
            else if (outputPath.EndsWith(".m4a"))
            {
                // For M4A, extract audio
                arguments = $"--newline --no-playlist -f \"{formatArg}\" " +
                          $"-x --audio-format m4a -o \"{outputPath}\" \"{url}\"";
            }
            else
            {
                // For video (MP4)
                arguments = $"--newline --no-playlist -f \"{formatArg}+bestaudio/best\" " +
                          $"--merge-output-format mp4 -o \"{outputPath}\" \"{url}\"";
            }
            
            var startInfo = new ProcessStartInfo
            {
                FileName = _ytDlpPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            
            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    // Parse progress from yt-dlp output
                    if (e.Data.Contains("[download]") && e.Data.Contains("%"))
                    {
                        try
                        {
                            var parts = e.Data.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var part in parts)
                            {
                                if (part.EndsWith("%"))
                                {
                                    var percentStr = part.TrimEnd('%');
                                    if (double.TryParse(percentStr, out var percent))
                                    {
                                        Dispatcher.Invoke(() =>
                                        {
                                            downloadItem.Progress = percent;
                                            downloadItem.DownloadedSize = downloadItem.TotalSize * (percent / 100.0);
                                        });
                                    }
                                    break;
                                }
                            }
                        }
                        catch { }
                    }
                }
            };

            process.Start();
            process.BeginOutputReadLine();

            // Wait for completion or cancellation
            while (!process.HasExited)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    process.Kill();
                    throw new OperationCanceledException();
                }
                await Task.Delay(100);
            }

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                throw new Exception($"yt-dlp failed: {error}");
            }
        }

        private void AddDownloadToUI(DownloadItem item)
        {
            var itemBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(204, 255, 255, 255)), // 80% transparent white
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 15),
                Tag = item
            };
            itemBorder.Effect = new DropShadowEffect
            {
                BlurRadius = 15,
                ShadowDepth = 4,
                Opacity = 0.1,
                Color = Color.FromRgb(59, 130, 246)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header row
            var headerStack = new StackPanel { Orientation = Orientation.Horizontal };
            
            var thumbnail = new Border
            {
                Width = 60,
                Height = 60,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                Margin = new Thickness(0, 0, 12, 0),
                ClipToBounds = true
            };
            
            // Try to load real thumbnail, fallback to emoji icon
            if (!string.IsNullOrEmpty(item.ThumbnailUrl))
            {
                try
                {
                    var thumbnailImage = new Image
                    {
                        Stretch = Stretch.UniformToFill
                    };
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(item.ThumbnailUrl);
                    bitmap.DecodePixelWidth = 60;
                    bitmap.EndInit();
                    thumbnailImage.Source = bitmap;
                    thumbnail.Child = thumbnailImage;
                }
                catch
                {
                    // Fallback to emoji icon if thumbnail fails to load
                    var emojiIcon = new TextBlock
                    {
                        Text = "🎬",
                        FontSize = 32,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    thumbnail.Child = emojiIcon;
                }
            }
            else
            {
                // Use emoji icon as fallback
                var emojiIcon = new TextBlock
                {
                    Text = "🎬",
                    FontSize = 32,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                thumbnail.Child = emojiIcon;
            }

            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            
            var titleText = new TextBlock
            {
                Text = item.Title,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 200
            };

            var metaText = new TextBlock
            {
                Text = $"{item.Format} • {item.Quality}",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 4, 0, 0)
            };

            infoStack.Children.Add(titleText);
            infoStack.Children.Add(metaText);

            var statusGradient = new LinearGradientBrush();
            statusGradient.StartPoint = new Point(0, 0);
            statusGradient.EndPoint = new Point(1, 1);
            statusGradient.GradientStops.Add(new GradientStop(Color.FromRgb(59, 130, 246), 0));
            statusGradient.GradientStops.Add(new GradientStop(Color.FromRgb(37, 99, 235), 1));

            var statusBorder = new Border
            {
                Width = 50,
                Height = 50,
                CornerRadius = new CornerRadius(25),
                Background = statusGradient,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var statusText = new TextBlock
            {
                Text = "0%",
                Foreground = Brushes.White,
                FontSize = 20,  // BIGGER percentage text
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            statusBorder.Child = statusText;

            headerStack.Children.Add(thumbnail);
            headerStack.Children.Add(infoStack);
            
            var headerGrid = new Grid();
            headerGrid.Children.Add(headerStack);
            headerGrid.Children.Add(statusBorder);

            Grid.SetRow(headerGrid, 0);
            grid.Children.Add(headerGrid);

            // Progress row - NO BAR, just text
            var progressStack = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
            
            var progressText = new TextBlock
            {
                Text = $"0 MB / {item.TotalSize:F1} MB",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139))
            };

            progressStack.Children.Add(progressText);

            // Buttons
            var buttonStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 10, 0, 0)
            };

            // Pause/Resume Button
            var pauseResumeButton = new Button
            {
                Content = item.Status == DownloadStatus.Paused ? "▶️ Resume" : "⏸ Pause",
                Padding = new Thickness(15, 8, 15, 8),
                Background = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 0),
                Visibility = (item.Status == DownloadStatus.Downloading || item.Status == DownloadStatus.Paused) ? Visibility.Visible : Visibility.Collapsed
            };

            pauseResumeButton.Click += async (s, e) =>
            {
                if (item.Status == DownloadStatus.Downloading)
                {
                    // Pause the download
                    if (_downloadCancellations.TryGetValue(item, out var cts))
                    {
                        cts.Cancel();
                        item.Status = DownloadStatus.Paused;
                        // Button content and visibility will be updated by PropertyChanged handler
                    }
                }
                else if (item.Status == DownloadStatus.Paused)
                {
                    // Resume the download
                    await ResumeDownloadAsync(item);
                }
            };

            var cancelButton = new Button
            {
                Content = "❌ Cancel",
                Padding = new Thickness(15, 8, 15, 8),
                Background = new SolidColorBrush(Color.FromRgb(239, 68, 68)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                Visibility = item.Status == DownloadStatus.Downloading ? Visibility.Visible : Visibility.Collapsed
            };

            // Cancel button click handler
            cancelButton.Click += (s, e) =>
            {
                if (_downloadCancellations.TryGetValue(item, out var cts))
                {
                    cts.Cancel();
                    item.Status = DownloadStatus.Failed;
                }
            };

            // Remove button (hidden initially)
            var removeButton = new Button
            {
                Content = "🗑️ Remove",
                Padding = new Thickness(15, 8, 15, 8),
                Background = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                BorderThickness = new Thickness(0),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                Visibility = (item.Status == DownloadStatus.Paused || item.Status == DownloadStatus.Failed || item.Status == DownloadStatus.Completed) ? Visibility.Visible : Visibility.Collapsed
            };

            removeButton.Click += (s, e) =>
            {
                _downloads.Remove(item);
                DownloadsList.Children.Remove(itemBorder);
                if (_downloadCancellations.TryGetValue(item, out var cts))
                {
                    cts.Cancel();
                    _downloadCancellations.Remove(item);
                }
            };

            buttonStack.Children.Add(pauseResumeButton);
            buttonStack.Children.Add(cancelButton);
            buttonStack.Children.Add(removeButton);

            progressStack.Children.Add(buttonStack);

            Grid.SetRow(progressStack, 1);
            grid.Children.Add(progressStack);

            itemBorder.Child = grid;
            DownloadsList.Children.Add(itemBorder);

            // Update progress
            item.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(DownloadItem.Progress))
                {
                    Dispatcher.Invoke(() =>
                    {
                        statusText.Text = $"{(int)item.Progress}%";
                        progressText.Text = $"{item.DownloadedSize:F1} MB / {item.TotalSize:F1} MB";

                        if (item.Status == DownloadStatus.Completed)
                        {
                            var completedGradient = new LinearGradientBrush();
                            completedGradient.StartPoint = new Point(0, 0);
                            completedGradient.EndPoint = new Point(1, 1);
                            completedGradient.GradientStops.Add(new GradientStop(Color.FromRgb(16, 185, 129), 0));
                            completedGradient.GradientStops.Add(new GradientStop(Color.FromRgb(5, 150, 105), 1));
                            
                            statusBorder.Background = completedGradient;
                            statusText.Text = "✓";
                            statusText.FontSize = 28;
                            buttonStack.Visibility = Visibility.Collapsed;
                        }
                    });
                }
                else if (e.PropertyName == nameof(DownloadItem.Status))
                {
                    Dispatcher.Invoke(() =>
                    {
                        System.Diagnostics.Debug.WriteLine($"Status changed to: {item.Status}");
                        
                        if (item.Status == DownloadStatus.Paused)
                        {
                            // When paused: Show Resume button + Remove button
                            pauseResumeButton.Content = "▶️ Resume";
                            pauseResumeButton.Visibility = Visibility.Visible;
                            cancelButton.Visibility = Visibility.Collapsed;
                            removeButton.Visibility = Visibility.Visible;
                            
                            System.Diagnostics.Debug.WriteLine($"PAUSED - Resume visibility: {pauseResumeButton.Visibility}, Remove visibility: {removeButton.Visibility}");
                        }
                        else if (item.Status == DownloadStatus.Failed)
                        {
                            // When failed: Only show Remove
                            pauseResumeButton.Visibility = Visibility.Collapsed;
                            cancelButton.Visibility = Visibility.Collapsed;
                            removeButton.Visibility = Visibility.Visible;
                        }
                        else if (item.Status == DownloadStatus.Downloading)
                        {
                            // When downloading: Show Pause and Cancel
                            pauseResumeButton.Content = "⏸️ Pause";
                            pauseResumeButton.Visibility = Visibility.Visible;
                            cancelButton.Visibility = Visibility.Visible;
                            removeButton.Visibility = Visibility.Collapsed;
                        }
                        else if (item.Status == DownloadStatus.Completed)
                        {
                            // Hide all buttons
                            pauseResumeButton.Visibility = Visibility.Collapsed;
                            cancelButton.Visibility = Visibility.Collapsed;
                            removeButton.Visibility = Visibility.Collapsed;
                        }
                    });
                }
            };
        }

        private string SanitizeFileName(string fileName)
        {
            var invalids = Path.GetInvalidFileNameChars();
            return string.Join("_", fileName.Split(invalids, StringSplitOptions.RemoveEmptyEntries));
        }

        private void SaveDownloadHistory()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(_downloads, options);
                File.WriteAllText(_downloadsHistoryPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save download history: {ex.Message}");
            }
        }

        private void LoadDownloadHistory()
        {
            try
            {
                if (File.Exists(_downloadsHistoryPath))
                {
                    var json = File.ReadAllText(_downloadsHistoryPath);
                    var downloads = JsonSerializer.Deserialize<List<DownloadItem>>(json);
                    
                    if (downloads != null && downloads.Count > 0)
                    {
                        foreach (var download in downloads)
                        {
                            // Only load completed, failed, or paused downloads
                            // Don't load downloads that were in progress when app closed
                            if (download.Status == DownloadStatus.Downloading)
                            {
                                download.Status = DownloadStatus.Failed;
                            }
                            
                            _downloads.Add(download);
                            AddDownloadToUI(download);
                        }
                        
                        // Show downloads panel if we loaded any downloads
                        ShowDownloads_Click(null!, null!);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load download history: {ex.Message}");
            }
        }

        private void ShowDownloads_Click(object sender, RoutedEventArgs e)
        {
            var animation = new DoubleAnimation
            {
                From = 405,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var transform = new TranslateTransform();
            DownloadsPanel.RenderTransform = transform;
            transform.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        private void HideDownloads_Click(object sender, RoutedEventArgs e)
        {
            var animation = new DoubleAnimation
            {
                From = 0,
                To = 405,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var transform = DownloadsPanel.RenderTransform as TranslateTransform;
            transform?.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        private void AllTab_Click(object sender, RoutedEventArgs e)
        {
            AllTab.Background = Application.Current.Resources["BlueGradient"] as Brush;
            AllTab.Foreground = Brushes.White;
            DownloadedTab.Background = new SolidColorBrush(Color.FromRgb(226, 232, 240));
            DownloadedTab.Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105));

            // Show all downloads
            foreach (Border item in DownloadsList.Children)
            {
                item.Visibility = Visibility.Visible;
            }
        }

        private void DownloadedTab_Click(object sender, RoutedEventArgs e)
        {
            DownloadedTab.Background = Application.Current.Resources["BlueGradient"] as Brush;
            DownloadedTab.Foreground = Brushes.White;
            AllTab.Background = new SolidColorBrush(Color.FromArgb(128, 226, 232, 240));
            AllTab.Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105));

            // Show only completed downloads
            foreach (Border item in DownloadsList.Children)
            {
                if (item.Tag is DownloadItem downloadItem)
                {
                    item.Visibility = downloadItem.Status == DownloadStatus.Completed 
                        ? Visibility.Visible 
                        : Visibility.Collapsed;
                }
            }
        }

        private void ClearList_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Clear all completed downloads from the list?",
                "Clear List",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                var itemsToRemove = DownloadsList.Children
                    .OfType<Border>()
                    .Where(b => b.Tag is DownloadItem item && item.Status == DownloadStatus.Completed)
                    .ToList();

                foreach (var item in itemsToRemove)
                {
                    DownloadsList.Children.Remove(item);
                }
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(_downloadFolder))
            {
                Process.Start("explorer.exe", _downloadFolder);
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void DarkModeToggle_Click(object sender, MouseButtonEventArgs e)
        {
            _isDarkMode = !_isDarkMode;
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            try
            {
                var app = Application.Current;
                
                if (_isDarkMode)
                {
                    // Switch to dark mode
                    try
                    {
                        var darkImageUri = new Uri("pack://application:,,,/Assets/dark.jpg");
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = darkImageUri;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        BackgroundImage.Source = bitmap;
                    }
                    catch
                    {
                        // If dark.jpg doesn't exist, just darken the current background
                        System.Diagnostics.Debug.WriteLine("dark.jpg not found, keeping current background");
                    }
                    
                    // Update dynamic brushes to dark colors
                    UpdateBrush("BackgroundBrush", "DarkBackground");
                    UpdateBrush("SearchBoxBrush", "DarkSearchBox");
                    UpdateBrush("CardBgBrush", "DarkCardBg");
                    UpdateBrush("PanelBgBrush", "DarkPanelBg");
                    
                    // Animate toggle to right (dark mode)
                    var animation = new ThicknessAnimation
                    {
                        To = new Thickness(31, 0, 0, 0),
                        Duration = TimeSpan.FromMilliseconds(200),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    DarkModeToggleThumb.BeginAnimation(MarginProperty, animation);
                    
                    // Change toggle background to black
                    DarkModeToggleBackground.Background = new SolidColorBrush(Color.FromRgb(28, 28, 30));
                    
                    // Change thumb to dark with moon icon
                    DarkModeToggleThumb.Background = new SolidColorBrush(Color.FromRgb(142, 142, 147));
                    DarkModeIcon.Text = "🌙";
                    DarkModeIcon.Foreground = Brushes.White;
                }
                else
                {
                    // Switch to light mode
                    try
                    {
                        var lightImageUri = new Uri("pack://application:,,,/Assets/sakura.jpg");
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = lightImageUri;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        BackgroundImage.Source = bitmap;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error loading sakura.jpg: {ex.Message}");
                    }
                    
                    // Update dynamic brushes to light colors
                    UpdateBrush("BackgroundBrush", "LightBackground");
                    UpdateBrush("SearchBoxBrush", "LightSearchBox");
                    UpdateBrush("CardBgBrush", "LightCardBg");
                    UpdateBrush("PanelBgBrush", "LightPanelBg");
                    
                    // Animate toggle to left (light mode)
                    var animation = new ThicknessAnimation
                    {
                        To = new Thickness(3, 0, 0, 0),
                        Duration = TimeSpan.FromMilliseconds(200),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    DarkModeToggleThumb.BeginAnimation(MarginProperty, animation);
                    
                    // Change toggle background to white
                    DarkModeToggleBackground.Background = Brushes.White;
                    
                    // Change thumb to golden/yellow with sun icon
                    DarkModeToggleThumb.Background = new SolidColorBrush(Color.FromRgb(255, 215, 0));
                    DarkModeIcon.Text = "☀";
                    DarkModeIcon.Foreground = Brushes.White;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error switching theme: {ex.Message}", "Theme Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                System.Diagnostics.Debug.WriteLine($"Theme error: {ex}");
            }
        }

        private void UpdateBrush(string brushKey, string colorKey)
        {
            try
            {
                var app = Application.Current;
                if (app.Resources.Contains(colorKey) && app.Resources[colorKey] is Color color)
                {
                    app.Resources[brushKey] = new SolidColorBrush(color);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating brush {brushKey}: {ex.Message}");
            }
        }
    }

    // Models moved to src/Models folder - keeping code organized!
}
