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
using System.Text.RegularExpressions;
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
using System.ComponentModel;
using System.Globalization;
using System.Windows.Media.Imaging;
// Added references to my models and helpers folders
using YouTubeDownloader.Models;
using YouTubeDownloader.Helpers;

namespace YouTubeDownloader
{
    public partial class MainWindow : Window
    {

        private readonly DispatcherTimer _petalTimer;
        private readonly DispatcherTimer _autoSaveTimer;
        private readonly Random _random;
        private string _downloadFolder;
        private readonly string _ytDlpPath;
        private string _downloadsHistoryPath;
        private readonly string _settingsPath;
        private ObservableCollection<DownloadItem> _downloads;
        private Dictionary<DownloadItem, CancellationTokenSource> _downloadCancellations;
        private Dictionary<DownloadItem, PropertyChangedEventHandler> _downloadPropertyHandlers = new();
        
        // Quality selector state
        private VideoInfo? _currentVideoInfo;
        private List<FormatInfo>? _currentFormats;
        private FormatInfo? _selectedFormat;
        private string _selectedAudioFormat = "m4a"; // Default to m4a, can be "mp3"
        private ThumbnailInfo? _selectedThumbnail;
        private SubtitleInfo? _selectedSubtitle;
        private string? _currentUrl;
        
        // Dark mode state
        private bool _isDarkMode = true;
        private bool _showOnlyCompleted = false;

        public MainWindow()
        {
            InitializeComponent();
            
            _random = new Random();
            _downloads = new ObservableCollection<DownloadItem>();
            _downloadCancellations = new Dictionary<DownloadItem, CancellationTokenSource>();
            
            // Set yt-dlp path (will be in the same folder as exe)
            _ytDlpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe");
            
            // Set settings path (in app data)
            var appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "YouTubeDownloader"
            );
            Directory.CreateDirectory(appDataFolder);
            _settingsPath = Path.Combine(appDataFolder, "settings.json");
            
            // Load settings (including download folder)
            LoadSettings();
            
            // Set downloads history path
            _downloadsHistoryPath = Path.Combine(_downloadFolder ?? "", "downloads_history.json");
            
            // Check if yt-dlp exists
            CheckYtDlp();
            
            // Load download history
            LoadDownloadHistory();
            
            // Make window draggable
            MouseLeftButtonDown += (s, e) => DragMove();

            
            // Start petal animation
            _petalTimer = new DispatcherTimer();
            _petalTimer.Interval = TimeSpan.FromMilliseconds(500);
            _petalTimer.Tick += CreatePetal;
            _petalTimer.Start();
            
            // Auto-save timer: saves download progress every 5 seconds during active downloads
            _autoSaveTimer = new DispatcherTimer();
            _autoSaveTimer.Interval = TimeSpan.FromSeconds(5);
            _autoSaveTimer.Tick += (s, e) =>
            {
                if (_downloads.Any(d => d.Status == DownloadStatus.Downloading))
                {
                    SaveDownloadHistory();
                }
                else
                {
                    _autoSaveTimer.Stop();
                }
            };
            
            // Initialize theme (this loads the background image)
            ApplyTheme();
            
            // Save download history on window closing
            Closing += (s, e) => SaveDownloadHistory();
        }

        // Helper to find DarkModeIcon inside the ControlTemplate
        private TextBlock? FindDarkModeIcon()
        {
            return FindVisualChild<TextBlock>(this, "DarkModeIcon");
        }

        private static T? FindVisualChild<T>(DependencyObject parent, string name) where T : FrameworkElement
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild && typedChild.Name == name)
                    return typedChild;
                var result = FindVisualChild<T>(child, name);
                if (result != null) return result;
            }
            return null;
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
            if (UrlTextBox.Text == "Paste YouTube link here\u2026")
            {
                UrlTextBox.Text = "";
                UrlTextBox.Foreground = _isDarkMode
                    ? new SolidColorBrush(Color.FromRgb(230, 230, 230))
                    : new SolidColorBrush(Color.FromRgb(26, 26, 46));
            }
            // Animate focus ring: border turns accent blue
            SearchBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(91, 141, 239));
        }

        private void UrlTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UrlTextBox.Text))
            {
                UrlTextBox.Text = "Paste YouTube link here\u2026";
                UrlTextBox.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            }
            // Restore border
            SearchBorder.BorderBrush = _isDarkMode
                ? new SolidColorBrush(Color.FromArgb(0x55, 255, 255, 255))
                : new SolidColorBrush(Color.FromRgb(226, 232, 243));
        }

        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            // Scale-down then back micro-animation on the button
            var scaleDown = new DoubleAnimation(1.0, 0.95, TimeSpan.FromMilliseconds(80))
            {
                AutoReverse = true,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            DownloadButton.RenderTransformOrigin = new Point(0.5, 0.5);
            if (DownloadButton.RenderTransform is not ScaleTransform)
                DownloadButton.RenderTransform = new ScaleTransform(1, 1);
            (DownloadButton.RenderTransform as ScaleTransform)!.BeginAnimation(ScaleTransform.ScaleXProperty, scaleDown);
            (DownloadButton.RenderTransform as ScaleTransform)!.BeginAnimation(ScaleTransform.ScaleYProperty, scaleDown);

            var url = UrlTextBox.Text;

            if (string.IsNullOrWhiteSpace(url) || url == "Paste YouTube link here\u2026")
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
                ShowLoading("Fetching video information…");

                // Get video info and formats
                var (videoInfo, formats) = await GetVideoInfoAndFormatsAsync(url);
                
                HideLoading();

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
                HideLoading();
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
            
            // Set channel and duration info
            var durationSpan = TimeSpan.FromSeconds(videoInfo.Duration);
            string durationText = durationSpan.Hours > 0 ? 
                $"{durationSpan.Hours}:{durationSpan.Minutes:D2}:{durationSpan.Seconds:D2}" : 
                $"{durationSpan.Minutes}:{durationSpan.Seconds:D2}";
                
            QualityDuration.Text = durationText;
            QualityChannelInfo.Text = $"{videoInfo.Uploader} • {durationSpan.TotalMinutes:F0} min";
            
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
            
            // Store formats and URL
            _currentFormats = formats;
            _currentVideoInfo = videoInfo;
            _currentUrl = UrlTextBox.Text;
            
            // Show video formats by default
            VideoFormatTab_Click(null!, null!);
            
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

        private void SubtitleSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_currentVideoInfo != null)
            {
                ShowSubtitles(_currentVideoInfo.Subtitles);
            }
        }
    
        private Border CreateFormatButton(FormatInfo format)
        {
    var container = new Border
    {
        Background = Brushes.Transparent,
        Padding = new Thickness(10, 12, 10, 12),
        Cursor = Cursors.Hand,
        Tag = format
    };

    var grid = new Grid();
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Radio
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Text
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Badge

    // Radio Button Graphic
    var unselectedRadioBrush = _isDarkMode 
        ? new SolidColorBrush(Color.FromRgb(100, 100, 100)) 
        : new SolidColorBrush(Color.FromRgb(200, 200, 200));

    var radioBorder = new Border
    {
        Width = 20,
        Height = 20,
        CornerRadius = new CornerRadius(10),
        BorderThickness = new Thickness(2),
        BorderBrush = _selectedFormat == format ? new SolidColorBrush(Color.FromRgb(26, 115, 232)) : unselectedRadioBrush,
        Background = Brushes.Transparent,
        Margin = new Thickness(0, 0, 15, 0),
        VerticalAlignment = VerticalAlignment.Center
    };

    if (_selectedFormat == format)
    {
        var dot = new Border
        {
            Width = 10,
            Height = 10,
            CornerRadius = new CornerRadius(5),
            Background = new SolidColorBrush(Color.FromRgb(26, 115, 232)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        radioBorder.Child = dot;
        container.Background = _isDarkMode
            ? new SolidColorBrush(Color.FromRgb(30, 50, 80))
            : new SolidColorBrush(Color.FromRgb(232, 240, 254));
    }

    Grid.SetColumn(radioBorder, 0);
    grid.Children.Add(radioBorder);

    // Text Info - just the resolution, no file size
    var resolutionText = new TextBlock
    {
        Text = format.Height > 0 ? $"{format.Height}p" : "Audio Only",
        FontSize = 15,
        FontWeight = FontWeights.Medium,
        Foreground = _isDarkMode
            ? new SolidColorBrush(Color.FromRgb(230, 230, 230))
            : new SolidColorBrush(Color.FromRgb(31, 41, 55)),
        VerticalAlignment = VerticalAlignment.Center
    };

    Grid.SetColumn(resolutionText, 1);
    grid.Children.Add(resolutionText);

    // "Best" Badge
    var allFormatsInList = (_currentFormats ?? new List<FormatInfo>()).Where(f => format.Height > 0 ? f.Height > 0 : f.Height == 0).ToList();
    bool isBest = allFormatsInList.Count > 0 && (format.Height > 0 ? 
        (format.Height == allFormatsInList.Max(f => f.Height)) :
        (format.Bitrate == allFormatsInList.Max(f => f.Bitrate)));

    if (isBest)
    {
        var badge = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(26, 115, 232)),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 2, 8, 2),
            VerticalAlignment = VerticalAlignment.Center
        };
        
        var badgeText = new TextBlock
        {
            Text = "Best",
            Foreground = Brushes.White,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold
        };
        
        badge.Child = badgeText;
        Grid.SetColumn(badge, 2);
        grid.Children.Add(badge);
    }

    container.Child = grid;

    // Click handler - use MouseLeftButtonDown + Handled so clicks on child elements work
    container.MouseLeftButtonDown += (s, e) =>
    {
        _selectedFormat = format;
        RefreshQualityList();
        e.Handled = true;
    };

    return container;
}

        private void ShowFormats(List<FormatInfo> formats)
        {
            QualityListPanel.Children.Clear();

            // Only default to first if nothing selected or selection not in current list
            if (_selectedFormat == null || !formats.Contains(_selectedFormat))
            {
                _selectedFormat = formats.OrderByDescending(f => f.Height).FirstOrDefault();
            }

            bool isFirst = true;
            foreach (var format in formats.OrderByDescending(f => f.Height))
            {

                var button = CreateFormatButton(format);

                if (!isFirst)
                {
                    var sep = new Border
                    {
                        Height = 1,
                        Background = _isDarkMode
                            ? new SolidColorBrush(Color.FromRgb(55, 55, 60))
                            : new SolidColorBrush(Color.FromRgb(243, 244, 246)),
                        Margin = new Thickness(45, 0, 10, 0)
                    };
                    QualityListPanel.Children.Add(sep);
                }

                QualityListPanel.Children.Add(button);
                isFirst = false;
            }
        }

        private void RefreshQualityList()
        {
            // Determine which tab is active by checking the Effect (active tab has DropShadowEffect)
            if (VideoFormatTab.Effect != null)
            {
                ShowFormats(_currentFormats?.Where(f => f.Height > 0).ToList() ?? new List<FormatInfo>());
            }
            else if (AudioFormatTab.Effect != null)
            {
                ShowAudioFormatsWithFormatSelector(_currentFormats?.Where(f => f.Height == 0).ToList() ?? new List<FormatInfo>());
            }
            else if (ThumbnailTab.Effect != null)
            {
                ShowThumbnails(_currentVideoInfo?.Thumbnails ?? new List<ThumbnailInfo>());
            }
            else if (SubtitleTab.Effect != null)
            {
                ShowSubtitles(_currentVideoInfo?.Subtitles ?? new List<SubtitleInfo>());
            }
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

        private void UpdateTabStyles(Button activeTab)
        {
            var tabs = new[] { VideoFormatTab, AudioFormatTab, ThumbnailTab, SubtitleTab };
            
            foreach (var tab in tabs)
            {
                bool isActive = tab == activeTab;
                
                // Background
                if (isActive)
                {
                    tab.Background = _isDarkMode ? new SolidColorBrush(Color.FromRgb(55, 55, 60)) : Brushes.White;
                    tab.Foreground = _isDarkMode ? new SolidColorBrush(Color.FromRgb(230, 230, 230)) : new SolidColorBrush(Color.FromRgb(31, 41, 55));
                    tab.Effect = new DropShadowEffect { ShadowDepth = 1, BlurRadius = 2, Opacity = 0.1 };
                }
                else
                {
                    tab.Background = Brushes.Transparent;
                    tab.Foreground = _isDarkMode ? new SolidColorBrush(Color.FromRgb(140, 140, 145)) : new SolidColorBrush(Color.FromRgb(107, 114, 128));
                    tab.Effect = null;
                }
            }

            // Show/Hide Subtitle Search
            SubtitleSearchContainer.Visibility = activeTab == SubtitleTab ? Visibility.Visible : Visibility.Collapsed;
        }

        private void VideoFormatTab_Click(object sender, RoutedEventArgs e)
        {
            UpdateTabStyles(VideoFormatTab);
            _selectedFormat = null;
            if (_currentFormats != null)
            {
                ShowFormats(_currentFormats.Where(f => f.Height > 0).ToList());
            }
        }

        private void AudioFormatTab_Click(object sender, RoutedEventArgs e)
        {
            UpdateTabStyles(AudioFormatTab);
            _selectedFormat = null;
            if (_currentFormats != null)
            {
                ShowAudioFormatsWithFormatSelector(_currentFormats.Where(f => f.Height == 0).ToList());
            }
        }

        private void ThumbnailTab_Click(object sender, RoutedEventArgs e)
        {
            UpdateTabStyles(ThumbnailTab);
            if (_currentVideoInfo != null)
            {
                ShowThumbnails(_currentVideoInfo.Thumbnails);
            }
        }

        private void SubtitleTab_Click(object sender, RoutedEventArgs e)
        {
            UpdateTabStyles(SubtitleTab);
            SubtitleSearchBox.Text = ""; // Reset search
            if (_currentVideoInfo != null)
            {
                ShowSubtitles(_currentVideoInfo.Subtitles);
            }
        }
        private void ShowAudioFormatsWithFormatSelector(List<FormatInfo> formats)
        {
            QualityListPanel.Children.Clear();

            // Add format selector (M4A vs MP3)
            var formatSelectorStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 15),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var inactiveBtnBg = _isDarkMode
                ? new SolidColorBrush(Color.FromRgb(55, 55, 60))
                : new SolidColorBrush(Color.FromRgb(226, 232, 240));
            var inactiveBtnFg = _isDarkMode
                ? new SolidColorBrush(Color.FromRgb(160, 160, 165))
                : new SolidColorBrush(Color.FromRgb(71, 85, 105));

            var formatLabel = new TextBlock
            {
                Text = "Audio Format:",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = _isDarkMode
                    ? new SolidColorBrush(Color.FromRgb(180, 180, 185))
                    : new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var m4aButton = new Button
            {
                Content = "M4A",
                Padding = new Thickness(20, 8, 20, 8),
                Background = _selectedAudioFormat == "m4a" ? new SolidColorBrush(Color.FromRgb(59, 130, 246)) : inactiveBtnBg,
                Foreground = _selectedAudioFormat == "m4a" ? Brushes.White : inactiveBtnFg,
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
                Background = _selectedAudioFormat == "mp3" ? new SolidColorBrush(Color.FromRgb(59, 130, 246)) : inactiveBtnBg,
                Foreground = _selectedAudioFormat == "mp3" ? Brushes.White : inactiveBtnFg,
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
                mp3Button.Background = inactiveBtnBg;
                mp3Button.Foreground = inactiveBtnFg;
            };

            mp3Button.Click += (s, e) =>
            {
                _selectedAudioFormat = "mp3";
                mp3Button.Background = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                mp3Button.Foreground = Brushes.White;
                m4aButton.Background = inactiveBtnBg;
                m4aButton.Foreground = inactiveBtnFg;
            };

            formatSelectorStack.Children.Add(formatLabel);
            formatSelectorStack.Children.Add(m4aButton);
            formatSelectorStack.Children.Add(mp3Button);

            QualityListPanel.Children.Add(formatSelectorStack);

            // Add audio quality options
            // Default to first audio format if nothing selected
            if (_selectedFormat == null || !formats.Contains(_selectedFormat))
            {
                _selectedFormat = formats.OrderByDescending(f => f.Bitrate).FirstOrDefault();
            }

            bool isFirst = true;
            foreach (var format in formats.OrderByDescending(f => f.Bitrate))
            {
                var button = CreateFormatButton(format); // Re-use the same button creator
                
                // Add separator if not last
                if (!isFirst) 
                {
                    var sep = new Border 
                    { 
                        Height = 1, 
                        Background = _isDarkMode
                            ? new SolidColorBrush(Color.FromRgb(55, 55, 60))
                            : new SolidColorBrush(Color.FromRgb(243, 244, 246)),
                        Margin = new Thickness(45, 0, 10, 0)
                    };
                    QualityListPanel.Children.Add(sep);
                }
                
                QualityListPanel.Children.Add(button);
                isFirst = false;
            }
        }

        private void ShowThumbnails(List<ThumbnailInfo> thumbnails)
        {
            QualityListPanel.Children.Clear();

            // Only default to first if nothing selected or selection not in current list
            if (_selectedThumbnail == null || !thumbnails.Contains(_selectedThumbnail))
            {
                _selectedThumbnail = thumbnails.FirstOrDefault();
            }

            bool isFirst = true;
            foreach (var thumb in thumbnails)
            {

                var button = CreateThumbnailButton(thumb);
                
                if (!isFirst)
                {
                   var sep = new Border 
                    { 
                        Height = 1, 
                        Background = _isDarkMode
                            ? new SolidColorBrush(Color.FromRgb(55, 55, 60))
                            : new SolidColorBrush(Color.FromRgb(243, 244, 246)),
                        Margin = new Thickness(45, 0, 10, 0)
                    };
                    QualityListPanel.Children.Add(sep);
                }
                
                QualityListPanel.Children.Add(button);
                isFirst = false;
            }
        }

        private Border CreateThumbnailButton(ThumbnailInfo thumb)
        {
            var container = new Border
            {
                Background = Brushes.Transparent,
                Padding = new Thickness(10, 12, 10, 12),
                Cursor = Cursors.Hand,
                Tag = thumb
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Radio
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Text
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Badge

            var unselectedRadioBrush = _isDarkMode 
                ? new SolidColorBrush(Color.FromRgb(100, 100, 100)) 
                : new SolidColorBrush(Color.FromRgb(200, 200, 200));

            var radioBorder = new Border
            {
                Width = 20, Height = 20,
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(2),
                BorderBrush = _selectedThumbnail == thumb ? new SolidColorBrush(Color.FromRgb(26, 115, 232)) : unselectedRadioBrush,
                Background = Brushes.Transparent,
                Margin = new Thickness(0, 0, 15, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            if (_selectedThumbnail == thumb)
            {
                radioBorder.Child = new Border
                {
                    Width = 10, Height = 10,
                    CornerRadius = new CornerRadius(5),
                    Background = new SolidColorBrush(Color.FromRgb(26, 115, 232)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                container.Background = _isDarkMode
                    ? new SolidColorBrush(Color.FromRgb(30, 50, 80))
                    : new SolidColorBrush(Color.FromRgb(232, 240, 254));
            }

            Grid.SetColumn(radioBorder, 0);
            grid.Children.Add(radioBorder);

            var resText = new TextBlock
            {
                Text = $"{thumb.Resolution}",
                FontSize = 15,
                FontWeight = FontWeights.Medium,
                Foreground = _isDarkMode
                    ? new SolidColorBrush(Color.FromRgb(230, 230, 230))
                    : new SolidColorBrush(Color.FromRgb(31, 41, 55)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(resText, 1);
            grid.Children.Add(resText);

            var badge = new Border
            {
                Background = _isDarkMode ? new SolidColorBrush(Color.FromRgb(55, 55, 60)) : new SolidColorBrush(Color.FromRgb(229, 231, 235)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = "JPG/WEBP",
                Foreground = _isDarkMode ? new SolidColorBrush(Color.FromRgb(200, 200, 200)) : new SolidColorBrush(Color.FromRgb(75, 85, 99)),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetColumn(badge, 2);
            grid.Children.Add(badge);

            container.Child = grid;

            container.MouseLeftButtonDown += (s, e) =>
            {
                _selectedThumbnail = thumb;
                ShowThumbnails(_currentVideoInfo!.Thumbnails);
                e.Handled = true;
            };

            return container;
        }

        private void ShowSubtitles(List<SubtitleInfo> subtitles)
        {
            QualityListPanel.Children.Clear();

            var filter = SubtitleSearchBox.Text.Trim().ToLower();

            var filtered = string.IsNullOrEmpty(filter) 
                ? subtitles 
                : subtitles.Where(s => s.DisplayName.ToLower().Contains(filter) || s.LanguageCode.ToLower().Contains(filter)).ToList();

            // Only default to first if nothing selected or selection not in current list
            if (_selectedSubtitle == null || !filtered.Any(s => s == _selectedSubtitle))
            {
                _selectedSubtitle = filtered.FirstOrDefault();
            }

            if (filtered.Count == 0)
            {
                var noResults = new TextBlock
                {
                    Text = "No subtitles found matching filter.",
                    Foreground = Brushes.Gray,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 20, 0, 0)
                };
                QualityListPanel.Children.Add(noResults);
                return;
            }

            bool isFirst = true;

            foreach (var sub in filtered)
            {
                // isFirst is only for separator logic now
                var button = CreateSubtitleButton(sub);
                
                if (!isFirst)
                {
                   var sep = new Border 
                    { 
                        Height = 1, 
                        Background = _isDarkMode
                            ? new SolidColorBrush(Color.FromRgb(55, 55, 60))
                            : new SolidColorBrush(Color.FromRgb(243, 244, 246)),
                        Margin = new Thickness(45, 0, 10, 0)
                    };
                    QualityListPanel.Children.Add(sep);
                }
                
                QualityListPanel.Children.Add(button);
                isFirst = false;
            }
        }

        private Border CreateSubtitleButton(SubtitleInfo sub)
        {
            var container = new Border
            {
                Background = Brushes.Transparent,
                Padding = new Thickness(10, 12, 10, 12),
                Cursor = Cursors.Hand,
                Tag = sub
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); 
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); 
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); 

            var unselectedRadioBrush = _isDarkMode 
                ? new SolidColorBrush(Color.FromRgb(100, 100, 100)) 
                : new SolidColorBrush(Color.FromRgb(200, 200, 200));

            var radioBorder = new Border
            {
                Width = 20, Height = 20,
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(2),
                BorderBrush = _selectedSubtitle == sub ? new SolidColorBrush(Color.FromRgb(26, 115, 232)) : unselectedRadioBrush,
                Background = Brushes.Transparent,
                Margin = new Thickness(0, 0, 15, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            if (_selectedSubtitle == sub)
            {
                radioBorder.Child = new Border
                {
                    Width = 10, Height = 10,
                    CornerRadius = new CornerRadius(5),
                    Background = new SolidColorBrush(Color.FromRgb(26, 115, 232)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                container.Background = _isDarkMode
                    ? new SolidColorBrush(Color.FromRgb(30, 50, 80))
                    : new SolidColorBrush(Color.FromRgb(232, 240, 254));
            }

            Grid.SetColumn(radioBorder, 0);
            grid.Children.Add(radioBorder);

            var nameText = new TextBlock
            {
                Text = sub.DisplayName,
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Foreground = _isDarkMode
                    ? new SolidColorBrush(Color.FromRgb(230, 230, 230))
                    : new SolidColorBrush(Color.FromRgb(31, 41, 55)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(nameText, 1);
            grid.Children.Add(nameText);

            var badge = new Border
            {
                Background = _isDarkMode ? new SolidColorBrush(Color.FromRgb(55, 55, 60)) : new SolidColorBrush(Color.FromRgb(229, 231, 235)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = sub.LanguageCode.ToUpper(),
                Foreground = _isDarkMode ? new SolidColorBrush(Color.FromRgb(200, 200, 200)) : new SolidColorBrush(Color.FromRgb(75, 85, 99)),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetColumn(badge, 2);
            grid.Children.Add(badge);

            container.Child = grid;

            container.MouseLeftButtonDown += (s, e) =>
            {
                _selectedSubtitle = sub;
                // Don't clear search, just refresh list to show selection
                ShowSubtitles(_currentVideoInfo!.Subtitles);
                e.Handled = true;
            };

            return container;
        }

        private async void QualityDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_currentVideoInfo == null)
            {
                MessageBox.Show("No video loaded.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Determine which tab is active by checking DropShadowEffect
            bool isThumbnailTab = ThumbnailTab.Effect != null;
            bool isSubtitleTab = SubtitleTab.Effect != null;

            if (isThumbnailTab)
            {
                if (_selectedThumbnail == null)
                {
                    MessageBox.Show("Please select a thumbnail to download.", "No Selection",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                CloseQualitySelector_Click(sender, null!);
                await DownloadThumbnailAsync(_currentVideoInfo, _selectedThumbnail);
            }
            else if (isSubtitleTab)
            {
                if (_selectedSubtitle == null)
                {
                    MessageBox.Show("Please select a subtitle to download.", "No Selection",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                var url = _currentUrl ?? UrlTextBox.Text;
                CloseQualitySelector_Click(sender, null!);
                await DownloadSubtitleAsync(url, _currentVideoInfo, _selectedSubtitle);
            }
            else
            {
                // Video or Audio tab
                if (_selectedFormat == null)
                {
                    MessageBox.Show("Please select a quality option.", "No Selection",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                CloseQualitySelector_Click(sender, null!);
                var url = _currentUrl ?? UrlTextBox.Text;
                await StartDownloadAsync(url, _currentVideoInfo, _selectedFormat);
            }
        }

        private DispatcherTimer? _loadingPetalTimer;
        
        private void ShowLoading(string message)
        {
            LoadingMessageText.Text = message;

            // Theme the overlay card for dark/light mode — subtle sakura style
            if (_isDarkMode)
            {
                LoadingOverlayBg.Color = Color.FromArgb(170, 15, 10, 18);
                LoadingCard.Background = new LinearGradientBrush(
                    new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(244, 38, 32, 42), 0),
                        new GradientStop(Color.FromArgb(244, 30, 25, 36), 1)
                    },
                    new Point(0, 0), new Point(0, 1));
                LoadingTextFg.Color = Color.FromRgb(230, 210, 215);
                LoadingRingBg.Stroke = new SolidColorBrush(Color.FromRgb(60, 45, 52));
                LoadingSubText.Foreground = new SolidColorBrush(Color.FromRgb(150, 125, 135));
            }
            else
            {
                LoadingOverlayBg.Color = Color.FromArgb(136, 0, 0, 0);
                LoadingCard.Background = new LinearGradientBrush(
                    new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(244, 255, 254, 255), 0),
                        new GradientStop(Color.FromArgb(244, 254, 246, 248), 1)
                    },
                    new Point(0, 0), new Point(0, 1));
                LoadingTextFg.Color = Color.FromRgb(61, 43, 53);
                LoadingRingBg.Stroke = new SolidColorBrush(Color.FromRgb(240, 224, 228));
                LoadingSubText.Foreground = new SolidColorBrush(Color.FromRgb(176, 149, 158));
            }

            // Show with fade-in
            LoadingOverlay.Visibility = Visibility.Visible;
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(250)
            };
            LoadingOverlay.BeginAnimation(OpacityProperty, fadeIn);

            // Start spinner rotation
            var spinAnimation = new DoubleAnimation
            {
                From = 0,
                To = 360,
                Duration = TimeSpan.FromSeconds(1.5),
                RepeatBehavior = RepeatBehavior.Forever
            };
            LoadingSpinnerRotation.BeginAnimation(RotateTransform.AngleProperty, spinAnimation);
            
            // Start animated falling petals in the overlay
            LoadingPetalsCanvas.Children.Clear();
            _loadingPetalTimer = new DispatcherTimer();
            _loadingPetalTimer.Interval = TimeSpan.FromMilliseconds(400);
            _loadingPetalTimer.Tick += CreateLoadingPetal;
            _loadingPetalTimer.Start();
        }

        private void CreateLoadingPetal(object? sender, EventArgs e)
        {
            var petalColors = new[]
            {
                Color.FromArgb(90, 212, 160, 170),  // Muted rose
                Color.FromArgb(80, 200, 150, 165),  // Dusty pink
                Color.FromArgb(70, 220, 180, 190),  // Soft blush
                Color.FromArgb(85, 190, 140, 155),  // Warm mauve
                Color.FromArgb(75, 225, 195, 200),  // Pale rose
            };
            
            var color = petalColors[_random.Next(petalColors.Length)];
            var size = _random.Next(8, 16);
            
            var petal = new Border
            {
                Width = size,
                Height = size,
                Background = new RadialGradientBrush(
                    color,
                    Color.FromArgb(40, 255, 192, 203)
                ),
                CornerRadius = new CornerRadius(size / 2.0, 0, size / 2.0, 0),
                RenderTransformOrigin = new Point(0.5, 0.5),
                Opacity = 0.8
            };

            Canvas.SetLeft(petal, _random.Next(0, Math.Max(1, (int)LoadingOverlay.ActualWidth)));
            Canvas.SetTop(petal, -20);
            LoadingPetalsCanvas.Children.Add(petal);

            var duration = TimeSpan.FromSeconds(_random.Next(4, 8));
            
            // Sway horizontally
            var swayAmount = _random.Next(-40, 40);
            
            var moveDown = new DoubleAnimation
            {
                From = -20,
                To = Math.Max(200, LoadingOverlay.ActualHeight + 20),
                Duration = duration,
                EasingFunction = new SineEase()
            };

            var moveHorizontal = new DoubleAnimation
            {
                By = swayAmount,
                Duration = duration,
                AutoReverse = false
            };

            var rotate = new DoubleAnimation
            {
                From = 0,
                To = _random.Next(-360, 360),
                Duration = duration
            };

            var fadeOut = new DoubleAnimation
            {
                From = 0.8,
                To = 0,
                BeginTime = duration - TimeSpan.FromSeconds(1.5),
                Duration = TimeSpan.FromSeconds(1.5)
            };

            var rotateTransform = new RotateTransform();
            petal.RenderTransform = rotateTransform;

            petal.BeginAnimation(Canvas.TopProperty, moveDown);
            petal.BeginAnimation(Canvas.LeftProperty, moveHorizontal);
            rotateTransform.BeginAnimation(RotateTransform.AngleProperty, rotate);
            petal.BeginAnimation(OpacityProperty, fadeOut);

            moveDown.Completed += (s, _) => LoadingPetalsCanvas.Children.Remove(petal);
        }

        private void HideLoading()
        {
            if (LoadingOverlay.Visibility != Visibility.Visible) return;

            // Stop spinner
            LoadingSpinnerRotation.BeginAnimation(RotateTransform.AngleProperty, null);
            
            // Stop petal timer
            _loadingPetalTimer?.Stop();
            _loadingPetalTimer = null;

            // Fade out
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(200)
            };
            fadeOut.Completed += (s, e) =>
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                LoadingPetalsCanvas.Children.Clear();
            };
            LoadingOverlay.BeginAnimation(OpacityProperty, fadeOut);
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

            // Start auto-save timer to persist progress periodically
            if (!_autoSaveTimer.IsEnabled)
                _autoSaveTimer.Start();

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

    // Start auto-save timer to persist progress periodically
    if (!_autoSaveTimer.IsEnabled)
        _autoSaveTimer.Start();

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

        private async Task DownloadThumbnailAsync(VideoInfo video, ThumbnailInfo thumb)
        {
            var item = new DownloadItem
            {
                Title = video.Title,
                Format = "Image", // JPG/WEBP
                Quality = thumb.Resolution,
                ThumbnailUrl = video.ThumbnailUrl,
                Progress = 0,
                Status = DownloadStatus.Downloading
            };

            _downloads.Add(item);
            AddDownloadToUI(item);
            ShowDownloads_Click(null!, null!);

            // Clear URL box like video downloads do
            UrlTextBox.Text = "Paste YouTube link here\u2026";
            UrlTextBox.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));

            try
            {
                // Create filename
                string safeTitle = SanitizeFileName(video.Title);
                string ext = Path.GetExtension(thumb.Url).Split('?')[0]; // simple extension extraction
                if (string.IsNullOrEmpty(ext)) ext = ".jpg";
                
                string fileName = $"{safeTitle} - {thumb.Resolution}{ext}";
                string filePath = Path.Combine(_downloadFolder, fileName);
                item.FilePath = filePath;

                using var client = new System.Net.Http.HttpClient();
                var data = await client.GetByteArrayAsync(thumb.Url);
                
                await File.WriteAllBytesAsync(filePath, data);

                item.Progress = 100;
                item.Status = DownloadStatus.Completed;
            }
            catch (Exception ex)
            {
                item.Status = DownloadStatus.Failed;
                MessageBox.Show($"Thumbnail download failed: {ex.Message}");
            }
        }

        private async Task DownloadSubtitleAsync(string url, VideoInfo video, SubtitleInfo sub)
        {
             var item = new DownloadItem
            {
                Title = video.Title + " (Subtitle)",
                Format = "Subtitle",
                Quality = sub.DisplayName,
                ThumbnailUrl = video.ThumbnailUrl,
                Progress = 0,
                Status = DownloadStatus.Downloading
            };

            _downloads.Add(item);
            AddDownloadToUI(item);
            ShowDownloads_Click(null!, null!);

            // Clear URL box like video downloads do
            UrlTextBox.Text = "Paste YouTube link here\u2026";
            UrlTextBox.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));

            var cancellationTokenSource = new CancellationTokenSource();
            _downloadCancellations[item] = cancellationTokenSource;

            try
            {
                string safeTitle = SanitizeFileName(video.Title);
                // We don't know exact ext yet (.vtt or .srt usually), yt-dlp decides
                string outputTemplate = Path.Combine(_downloadFolder, $"{safeTitle}.%(ext)s"); 
                item.FilePath = outputTemplate; // Approximation

                var args = $"--skip-download --write-sub --sub-lang {sub.LanguageCode} --output \"{outputTemplate}\" \"{url}\"";
                
                // If auto-generated, use --write-auto-sub
                if (sub.IsAutoGenerated)
                {
                    args = $"--skip-download --write-auto-sub --sub-lang {sub.LanguageCode} --output \"{outputTemplate}\" \"{url}\"";
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = _ytDlpPath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                
                // Track progress blindly (subs are fast)
                var progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                progressTimer.Tick += (s, e) => 
                {
                    if (item.Progress < 90) item.Progress += 5;
                };
                progressTimer.Start();

                process.Start();
                await process.WaitForExitAsync(cancellationTokenSource.Token);
                progressTimer.Stop();

                if (process.ExitCode != 0)
                {
                   throw new Exception($"yt-dlp exited with code {process.ExitCode}");
                }

                item.Progress = 100;
                item.Status = DownloadStatus.Completed;
            }
            catch (Exception ex)
            {
                item.Status = DownloadStatus.Failed;
                if (!(ex is OperationCanceledException))
                {
                    MessageBox.Show($"Subtitle download failed: {ex.Message}");
                }
            }
            finally
            {
                if (_downloadCancellations.ContainsKey(item))
                {
                    _downloadCancellations.Remove(item);
                }
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

                    // === Parse Thumbnails ===
                    if (root.TryGetProperty("thumbnails", out var thumbsArray))
                    {
                        foreach (var t in thumbsArray.EnumerateArray())
                        {
                            if (t.TryGetProperty("url", out var tUrl) && 
                                t.TryGetProperty("width", out var tWidth) && 
                                t.TryGetProperty("height", out var tHeight))
                            {
                                videoInfo.Thumbnails.Add(new ThumbnailInfo
                                {
                                    Url = tUrl.GetString() ?? "",
                                    Width = tWidth.GetInt32(),
                                    Height = tHeight.GetInt32()
                                });
                            }
                        }
                        // Sort by resolution (highest first) and remove duplicates
                        videoInfo.Thumbnails = videoInfo.Thumbnails
                            .OrderByDescending(t => t.Width * t.Height)
                            .GroupBy(t => t.Resolution)
                            .Select(g => g.First())
                            .ToList();
                    }

                    // === Parse Subtitles (Manual) ===
                    if (root.TryGetProperty("subtitles", out var subsObj))
                    {
                        foreach (var prop in subsObj.EnumerateObject())
                        {
                            string langCode = prop.Name;
                            string name = langCode;
                            
                            if (prop.Value.ValueKind == JsonValueKind.Array && prop.Value.GetArrayLength() > 0)
                            {
                                var firstPub = prop.Value[0];
                                if (firstPub.TryGetProperty("name", out var n)) name = n.GetString() ?? langCode;
                            }

                            videoInfo.Subtitles.Add(new SubtitleInfo
                            {
                                LanguageCode = langCode,
                                Name = name,
                                IsAutoGenerated = false
                            });
                        }
                    }

                    // === Parse Auto Captions ===
                    if (root.TryGetProperty("automatic_captions", out var autoSubsObj))
                    {
                        foreach (var prop in autoSubsObj.EnumerateObject())
                        {
                            string langCode = prop.Name;
                            string name = langCode;

                            if (prop.Value.ValueKind == JsonValueKind.Array && prop.Value.GetArrayLength() > 0)
                            {
                                var firstPub = prop.Value[0];
                                if (firstPub.TryGetProperty("name", out var n)) name = n.GetString() ?? langCode;
                            }

                            // Avoid duplicates if manual sub exists
                            if (!videoInfo.Subtitles.Any(s => s.LanguageCode == langCode))
                            {
                                videoInfo.Subtitles.Add(new SubtitleInfo
                                {
                                    LanguageCode = langCode,
                                    Name = name,
                                    IsAutoGenerated = true
                                });
                            }
                        }
                    }
                    
                    // Sort subtitles by name
                    videoInfo.Subtitles = videoInfo.Subtitles.OrderBy(s => s.Name).ToList();

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
                                    
                                    // Try to get accurate filesize - prefer filesize over filesize_approx
                                    long filesize = 0;
                                    if (format.TryGetProperty("filesize", out var fs) && fs.ValueKind == System.Text.Json.JsonValueKind.Number)
                                    {
                                        filesize = fs.GetInt64();
                                    }
                                    else if (format.TryGetProperty("filesize_approx", out var fsa) && fsa.ValueKind == System.Text.Json.JsonValueKind.Number)
                                    {
                                        filesize = fsa.GetInt64();
                                    }
                                    
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
                                            Filesize = filesize,
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
                                    
                                    // Try to get accurate filesize - prefer filesize over filesize_approx
                                    long filesize = 0;
                                    if (format.TryGetProperty("filesize", out var fs) && fs.ValueKind == System.Text.Json.JsonValueKind.Number)
                                    {
                                        filesize = fs.GetInt64();
                                    }
                                    else if (format.TryGetProperty("filesize_approx", out var fsa) && fsa.ValueKind == System.Text.Json.JsonValueKind.Number)
                                    {
                                        filesize = fsa.GetInt64();
                                    }
                                    
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
            
            bool isAudioOnly = outputPath.EndsWith(".mp3") || outputPath.EndsWith(".m4a");
            bool isMultiStream = !isAudioOnly; // video + audio = 2 separate download streams

            // Build yt-dlp arguments
            string arguments;
            if (outputPath.EndsWith(".mp3"))
            {
                arguments = $"--no-playlist --no-part -c -f \"{formatArg}\" " +
                          $"-x --audio-format mp3 --audio-quality 0 -o \"{outputPath}\" \"{url}\"";
            }
            else if (outputPath.EndsWith(".m4a"))
            {
                arguments = $"--no-playlist --no-part -c -f \"{formatArg}\" " +
                          $"-x --audio-format m4a -o \"{outputPath}\" \"{url}\"";
            }
            else
            {
                arguments = $"--no-playlist --no-part -c -f \"{formatArg}+bestaudio/best\" " +
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
            process.Start();

            // Drain stderr in background to prevent deadlock during ffmpeg merge
            _ = process.StandardError.ReadToEndAsync();

            // ── READ RAW STDOUT, splitting on \r AND \n ───────────────────────────────
            // yt-dlp uses \r to overwrite progress on the same line. .NET's
            // BeginOutputReadLine only fires on \n, silently discarding all intermediate
            // progress. We read raw chars so every overwrite is captured.
            //
            // STREAM TRACKING — synchronous, no deferrals:
            // streamCount is incremented synchronously inside the char loop (NOT in a
            // BeginInvoke) so the very first '%' line already has the correct bucket.
            //
            // Multi-stream weighting:  video 0-100% → overall 0-60%
            //                          audio 0-100% → overall 60-100%
            //
            // Cached-stream edge case: yt-dlp may skip the video [download] Destination
            // line and jump directly to audio. We detect a sudden percent drop (new stream
            // without announcement) and advance streamCount automatically.
            int streamCount = 0;          // synchronously maintained — NO BeginInvoke
            double lastRawPercent = -1.0; // to detect silent stream switches
            
            var stdoutTask = Task.Run(async () =>
            {
                var buffer = new System.Text.StringBuilder();
                var reader = process.StandardOutput;
                char[] chunk = new char[256];
                int charsRead;

                while ((charsRead = await reader.ReadAsync(chunk, 0, chunk.Length)) > 0)
                {
                    for (int i = 0; i < charsRead; i++)
                    {
                        char c = chunk[i];
                        if (c == '\r' || c == '\n')
                        {
                            if (buffer.Length > 0)
                            {
                                string line = buffer.ToString();
                                buffer.Clear();

                                // ── Stream-start detection (synchronous, no defer) ──
                                if (line.Contains("[download] Destination:"))
                                {
                                    streamCount++;
                                    lastRawPercent = -1.0; // clean slate for new stream
                                }
                                // ── Progress percentage ──
                                else if (line.Contains("[download]"))
                                {
                                    var match = Regex.Match(line, @"\[download\]\s+(\d+\.?\d*)%");
                                    if (match.Success && double.TryParse(match.Groups[1].Value,
                                        NumberStyles.Float, CultureInfo.InvariantCulture, out var rawPercent))
                                    {
                                        // Detect silent stream switch (percent dropped without Destination line)
                                        if (lastRawPercent >= 0 && rawPercent < lastRawPercent - 5.0)
                                        {
                                            streamCount++;
                                            lastRawPercent = -1.0;
                                        }
                                        lastRawPercent = rawPercent;

                                        // Guard: if Destination line was missed, assume stream 1
                                        if (streamCount == 0) streamCount = 1;

                                        double overallPercent;
                                        if (isMultiStream)
                                        {
                                            if (streamCount == 1)
                                                overallPercent = rawPercent * 0.60; // video: 0→60%
                                            else
                                                overallPercent = 60.0 + rawPercent * 0.40; // audio: 60→100%
                                        }
                                        else
                                        {
                                            overallPercent = rawPercent;
                                        }

                                        // Cap at 99.9 — 100% is set explicitly on completion
                                        overallPercent = Math.Min(Math.Max(overallPercent, 0), 99.9);

                                        double capturedPercent = overallPercent;
                                        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                                        {
                                            // Never regress — only advance
                                            if (capturedPercent > downloadItem.Progress)
                                            {
                                                downloadItem.Progress = capturedPercent;
                                                downloadItem.DownloadedSize = downloadItem.TotalSize * (capturedPercent / 100.0);
                                            }
                                        }));
                                    }
                                }

                                // ── Merge / completion detection → pin to 100% ──
                                if (line.Contains("[Merger]") || line.Contains("[Mux]") || line.Contains("Merging") ||
                                    line.Contains("[ExtractAudio]") || line.Contains("Deleting original file"))
                                {
                                    Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                                    {
                                        downloadItem.Progress = 100;
                                        downloadItem.DownloadedSize = downloadItem.TotalSize;
                                    }));
                                }
                            }
                        }
                        else
                        {
                            buffer.Append(c);
                        }
                    }
                }

                // Flush remaining buffer
                if (buffer.Length > 0)
                {
                    string remaining = buffer.ToString();
                    if (remaining.Contains("[Merger]") || remaining.Contains("[Mux]") ||
                        remaining.Contains("[ExtractAudio]"))
                    {
                        Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                        {
                            downloadItem.Progress = 100;
                            downloadItem.DownloadedSize = downloadItem.TotalSize;
                        }));
                    }
                }
            });

            // Wait for completion or cancellation
            try
            {
                while (!process.HasExited)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        try { process.Kill(); } catch { }
                        throw new OperationCanceledException();
                    }
                    await Task.Delay(100);
                }
                
                // Wait for stdout reader to finish processing remaining output
                await stdoutTask;
            }
            catch (OperationCanceledException)
            {
                try { if (!process.HasExited) process.Kill(); } catch { }
                throw;
            }

            if (!cancellationToken.IsCancellationRequested && process.ExitCode != 0)
            {
                throw new Exception($"yt-dlp failed with exit code {process.ExitCode}");
            }
        }

        /// <summary>
        /// Cleans up temporary/partial files left by yt-dlp when a download is cancelled or removed.
        /// yt-dlp creates separate video (.f{id}.mp4) and audio (.f{id}.webm/.m4a) files before merging.
        /// </summary>
        private void CleanupTempFiles(DownloadItem item)
        {
            try
            {
                if (string.IsNullOrEmpty(item.FilePath)) return;
                
                var directory = Path.GetDirectoryName(item.FilePath);
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(item.FilePath);
                
                if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileNameWithoutExt)) return;
                
                // Delete the main output file if it exists (partial download)
                if (File.Exists(item.FilePath))
                {
                    File.Delete(item.FilePath);
                }
                
                // Delete any yt-dlp temp fragment files (e.g., "Title.f135.mp4", "Title.f251.webm")
                // These follow the pattern: <filename>.f<formatId>.<ext>
                var tempFiles = Directory.GetFiles(directory, fileNameWithoutExt + ".f*");
                foreach (var tempFile in tempFiles)
                {
                    try { File.Delete(tempFile); } catch { }
                }
                
                // Also clean up any .part files (in case --no-part wasn't effective)
                var partFiles = Directory.GetFiles(directory, fileNameWithoutExt + "*.part");
                foreach (var partFile in partFiles)
                {
                    try { File.Delete(partFile); } catch { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to cleanup temp files: {ex.Message}");
            }
        }

        private void AddDownloadToUI(DownloadItem item)
        {
            // === Card Container (clip inner content to the rounded rect) ===
            var itemBorder = new Border
            {
                Background = _isDarkMode
                    ? new SolidColorBrush(Color.FromRgb(38, 42, 54))
                    : new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                CornerRadius = new CornerRadius(16),
                Margin = new Thickness(0, 0, 0, 10),
                Tag = item,
                SnapsToDevicePixels = true,
                ClipToBounds = true
            };
            itemBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 14,
                ShadowDepth = 2,
                Opacity = _isDarkMode ? 0.30 : 0.07,
                Color = Colors.Black
            };

            // Slide-in entrance animation: translate Y from +18 → 0, opacity 0 → 1
            itemBorder.Opacity = 0;
            itemBorder.RenderTransform = new TranslateTransform(0, 18);
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var slideUp = new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                itemBorder.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                (itemBorder.RenderTransform as TranslateTransform)!.BeginAnimation(TranslateTransform.YProperty, slideUp);
            }));

            // Outer card is a grid: Col0 = accent stripe, Col1 = content
            var outerGrid = new Grid();
            outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // stripe
            outerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // content

            // Left-side status color stripe
            var statusStripe = new Border
            {
                Width = 4,
                Background = new SolidColorBrush(Color.FromRgb(91, 141, 239)), // default accent
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetColumn(statusStripe, 0);
            outerGrid.Children.Add(statusStripe);

            // Content wrapper with padding
            var contentWrapper = new Border { Padding = new Thickness(14, 12, 14, 12) };
            Grid.SetColumn(contentWrapper, 1);
            outerGrid.Children.Add(contentWrapper);
            itemBorder.Child = outerGrid;

            // Main vertical stack: Row 1 (thumb+info+ring), Row 2 (buttons)
            var outerStack = new StackPanel();
            contentWrapper.Child = outerStack;

            // === ROW 1: Thumbnail | Title+Meta | Progress Ring ===
            var topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // Thumbnail
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Info
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // Ring

            // -- Thumbnail (square, like reference) --
            var thumbnailBorder = new Border
            {
                Width = 64,
                Height = 64,
                CornerRadius = new CornerRadius(8),
                Background = _isDarkMode
                    ? new SolidColorBrush(Color.FromRgb(55, 60, 70))
                    : new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                Margin = new Thickness(0, 0, 12, 0),
                ClipToBounds = true,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (!string.IsNullOrEmpty(item.ThumbnailUrl))
            {
                try
                {
                    var thumbnailImage = new Image { Stretch = Stretch.UniformToFill };
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(item.ThumbnailUrl);
                    bitmap.DecodePixelWidth = 64;
                    bitmap.EndInit();
                    thumbnailImage.Source = bitmap;
                    thumbnailBorder.Child = thumbnailImage;
                }
                catch
                {
                    thumbnailBorder.Child = new TextBlock
                    {
                        Text = "🎬", FontSize = 26,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                }
            }
            else
            {
                thumbnailBorder.Child = new TextBlock
                {
                    Text = "🎬", FontSize = 26,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }

            Grid.SetColumn(thumbnailBorder, 0);
            topRow.Children.Add(thumbnailBorder);

            // -- Info (Title + Meta) --
            var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var titleText = new TextBlock
            {
                Text = item.Title,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = _isDarkMode
                    ? new SolidColorBrush(Color.FromRgb(230, 235, 240))
                    : new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 180,
                Margin = new Thickness(0, 0, 0, 4)
            };

            var metaText = new TextBlock
            {
                Text = $"{item.Format} • {item.Quality}",
                FontSize = 12,
                Foreground = _isDarkMode
                    ? new SolidColorBrush(Color.FromRgb(140, 150, 165))
                    : new SolidColorBrush(Color.FromRgb(100, 116, 139))
            };

            infoStack.Children.Add(titleText);
            infoStack.Children.Add(metaText);

            Grid.SetColumn(infoStack, 1);
            topRow.Children.Add(infoStack);

            // -- Circular Progress Ring (larger, matching reference) --
            double ringSize = 52;
            double ringRadius = 22;
            double ringCenter = ringSize / 2.0;
            double ringStroke = 4.5;

            var progressContainer = new Grid
            {
                Width = ringSize, Height = ringSize,
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            // Background ring (grey track)
            var bgRing = new System.Windows.Shapes.Ellipse
            {
                Stroke = _isDarkMode
                    ? new SolidColorBrush(Color.FromRgb(55, 60, 70))
                    : new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                StrokeThickness = ringStroke,
                Width = ringSize - 4, Height = ringSize - 4
            };

            // Progress arc — endpoint initialized 0.001px off start so 0% renders as
            // an invisible arc (not the degenerate full-ring that WPF draws when start==end)
            var pathFigure = new PathFigure { StartPoint = new Point(ringCenter, ringCenter - ringRadius) };
            var arcSegment = new ArcSegment
            {
                Point = new Point(ringCenter + 0.001, ringCenter - ringRadius),
                Size = new Size(ringRadius, ringRadius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = false
            };
            pathFigure.Segments.Add(arcSegment);

            var pathGeometry = new PathGeometry();
            pathGeometry.Figures.Add(pathFigure);

            var progressPath = new System.Windows.Shapes.Path
            {
                Stroke = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                StrokeThickness = ringStroke,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Data = pathGeometry
            };

            // Percentage text in center
            var percentText = new TextBlock
            {
                Text = "0%",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = _isDarkMode
                    ? Brushes.White
                    : new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            progressContainer.Children.Add(bgRing);
            progressContainer.Children.Add(progressPath);
            progressContainer.Children.Add(percentText);

            Grid.SetColumn(progressContainer, 2);
            topRow.Children.Add(progressContainer);

            outerStack.Children.Add(topRow);

            // === ROW 2: Action Buttons ===
            var buttonStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 10, 0, 0)
            };

            // Color constants
            var pauseBlueBg = new SolidColorBrush(Color.FromRgb(59, 130, 246));
            var cancelRedBg = new SolidColorBrush(Color.FromRgb(220, 38, 38));
            var resumeGreenBg = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            var removeGreyBg = _isDarkMode
                ? new SolidColorBrush(Color.FromRgb(60, 65, 75))
                : new SolidColorBrush(Color.FromRgb(210, 215, 225));

            // --- Pause Button (Blue) ---
            var pauseButton = new Button
            {
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = "Pause Download"
            };
            pauseButton.Template = CreateDownloadButtonTemplate(pauseBlueBg);
            var pauseBtnContent = new StackPanel { Orientation = Orientation.Horizontal };
            
            var pauseIcon = new TextBlock
            {
                Text = "II",
                FontSize = 11,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            pauseBtnContent.Children.Add(pauseIcon);

            var pauseLabel = new TextBlock
            {
                Text = "Pause",
                FontSize = 13,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            pauseBtnContent.Children.Add(pauseLabel);
            pauseButton.Content = pauseBtnContent;

            // --- Cancel Button (Red) ---
            var cancelButton = new Button
            {
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0),
                ToolTip = "Cancel Download"
            };
            cancelButton.Template = CreateDownloadButtonTemplate(cancelRedBg);
            var cancelBtnContent = new StackPanel { Orientation = Orientation.Horizontal };
            cancelBtnContent.Children.Add(new TextBlock
            {
                Text = "X",
                FontSize = 11,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            cancelBtnContent.Children.Add(new TextBlock
            {
                Text = "Cancel",
                FontSize = 13,
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });
            cancelButton.Content = cancelBtnContent;

            // --- Remove Button (Grey, hidden initially) ---
            var removeButton = new Button
            {
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed
            };
            removeButton.Template = CreateDownloadButtonTemplate(removeGreyBg);
            var removeFg = _isDarkMode
                ? new SolidColorBrush(Color.FromRgb(200, 205, 215))
                : new SolidColorBrush(Color.FromRgb(50, 55, 65));
            var removeBtnContent = new StackPanel { Orientation = Orientation.Horizontal };
            removeBtnContent.Children.Add(new TextBlock
            {
                Text = "X",
                FontSize = 11,
                Foreground = removeFg,
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            removeBtnContent.Children.Add(new TextBlock
            {
                Text = "Remove",
                FontSize = 13,
                Foreground = removeFg,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });
            removeButton.Content = removeBtnContent;

            buttonStack.Children.Add(pauseButton);
            buttonStack.Children.Add(cancelButton);
            buttonStack.Children.Add(removeButton);
            outerStack.Children.Add(buttonStack);

            DownloadsList.Children.Insert(0, itemBorder);

            // Set initial state based on current item status (important for RefreshDownloadCards)
            if (item.Status == DownloadStatus.Paused)
            {
                pauseButton.Template = CreateDownloadButtonTemplate(resumeGreenBg);
                pauseLabel.Text = "Resume";
                pauseButton.ToolTip = "Resume Download";
                pauseIcon.Text = "▶";
                pauseButton.Visibility = Visibility.Visible;
                cancelButton.Visibility = Visibility.Visible;
                removeButton.Visibility = Visibility.Collapsed;
            }
            else if (item.Status == DownloadStatus.Completed)
            {
                statusStripe.Background = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                progressPath.Stroke = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                percentText.Text = "✓";
                percentText.FontSize = 22;
                percentText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                UpdateProgressRing(100);

                pauseButton.Visibility = Visibility.Collapsed;
                cancelButton.Visibility = Visibility.Collapsed;
                removeButton.Visibility = Visibility.Visible;
            }
            else if (item.Status == DownloadStatus.Failed)
            {
                statusStripe.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                progressPath.Stroke = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                percentText.Text = "!";
                percentText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));

                pauseButton.Visibility = Visibility.Collapsed;
                cancelButton.Visibility = Visibility.Collapsed;
                removeButton.Visibility = Visibility.Visible;
            }
            else if (item.Status == DownloadStatus.Downloading)
            {
                // Ensure correct state for actively downloading items
                pauseButton.Template = CreateDownloadButtonTemplate(pauseBlueBg);
                pauseIcon.Text = "II";
                pauseLabel.Text = "Pause";
                pauseButton.ToolTip = "Pause Download";
                pauseButton.Visibility = Visibility.Visible;
                cancelButton.Visibility = Visibility.Visible;
                removeButton.Visibility = Visibility.Collapsed;
                UpdateProgressRing(item.Progress);
                percentText.Text = $"{(int)item.Progress}%";
            }

            // === Progress Ring Math Helper ===
            void UpdateProgressRing(double percentage)
            {
                if (percentage >= 100)
                {
                    progressPath.Data = new EllipseGeometry
                    {
                        Center = new Point(ringCenter, ringCenter),
                        RadiusX = ringRadius,
                        RadiusY = ringRadius
                    };
                    return;
                }

                double angle = (percentage / 100.0) * 360.0;
                double rad = (angle - 90) * (Math.PI / 180.0);
                double x = ringCenter + ringRadius * Math.Cos(rad);
                double y = ringCenter + ringRadius * Math.Sin(rad);

                arcSegment.Point = new Point(x, y);
                arcSegment.IsLargeArc = angle > 180.0;

                if (progressPath.Data is not PathGeometry)
                {
                    progressPath.Data = pathGeometry;
                }
            }

            // === Event Handlers ===

            pauseButton.Click += async (s, e) =>
            {
                if (item.Status == DownloadStatus.Downloading)
                {
                    if (_downloadCancellations.TryGetValue(item, out var cts))
                    {
                        cts.Cancel();
                        item.Status = DownloadStatus.Paused;
                    }
                }
                else if (item.Status == DownloadStatus.Paused)
                {
                    await ResumeDownloadAsync(item);
                }
            };

            cancelButton.Click += (s, e) =>
            {
                if (_downloadCancellations.TryGetValue(item, out var cts))
                {
                    cts.Cancel();
                }
                item.Status = DownloadStatus.Failed;
                
                // Clean up partial/temp files from disk
                CleanupTempFiles(item);
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
                
                // Clean up partial/temp files from disk
                CleanupTempFiles(item);
                
                // Save updated history so removed items don't reappear
                SaveDownloadHistory();
            };

            // === Property Changed Listener ===
            // Both branches use BeginInvoke(Render) — same priority as the progress writer
            // in DownloadVideoAsync. This prevents out-of-order updates and never blocks
            // the background reader task with a synchronous Invoke.
            PropertyChangedEventHandler handler = (s, e) =>
            {
                if (e.PropertyName == nameof(DownloadItem.Progress))
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                    {
                        // Only update the ring text while actively downloading
                        if (item.Status == DownloadStatus.Downloading)
                        {
                            percentText.Text = $"{(int)item.Progress}%";
                            UpdateProgressRing(item.Progress);
                        }
                    }));
                }
                else if (e.PropertyName == nameof(DownloadItem.Status))
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
                    {
                        if (item.Status == DownloadStatus.Paused)
                        {
                            // Switch to green Resume button
                            pauseButton.Template = CreateDownloadButtonTemplate(resumeGreenBg);
                            pauseIcon.Text = "▶";
                            pauseLabel.Text = "Resume";
                            pauseButton.ToolTip = "Resume Download";
                            pauseButton.Visibility = Visibility.Visible;
                            cancelButton.Visibility = Visibility.Visible;
                            removeButton.Visibility = Visibility.Collapsed;
                        }
                        else if (item.Status == DownloadStatus.Downloading)
                        {
                            // Switch back to blue Pause button
                            pauseButton.Template = CreateDownloadButtonTemplate(pauseBlueBg);
                            pauseIcon.Text = "II";
                            pauseLabel.Text = "Pause";
                            pauseButton.ToolTip = "Pause Download";
                            pauseButton.Visibility = Visibility.Visible;
                            cancelButton.Visibility = Visibility.Visible;
                            removeButton.Visibility = Visibility.Collapsed;
                        }
                        else if (item.Status == DownloadStatus.Completed)
                        {
                            // Ensure ring is full before switching to green ✓
                            UpdateProgressRing(100);
                            progressPath.Stroke = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                            percentText.Text = "✓";
                            percentText.FontSize = 22;
                            percentText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));

                            pauseButton.Visibility = Visibility.Collapsed;
                            cancelButton.Visibility = Visibility.Collapsed;
                            removeButton.Visibility = Visibility.Visible;
                        }
                        else if (item.Status == DownloadStatus.Failed)
                        {
                            progressPath.Stroke = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                            percentText.Text = "!";
                            percentText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));

                            pauseButton.Visibility = Visibility.Collapsed;
                            cancelButton.Visibility = Visibility.Collapsed;
                            removeButton.Visibility = Visibility.Visible;
                        }
                    }));
                }
            };

            // Unsubscribe any old handler before adding new one
            if (_downloadPropertyHandlers.TryGetValue(item, out var oldHandler))
            {
                item.PropertyChanged -= oldHandler;
            }
            item.PropertyChanged += handler;
            _downloadPropertyHandlers[item] = handler;
        }

        // Creates a clean rounded button template with proper padding baked in
        private ControlTemplate CreateDownloadButtonTemplate(Brush bg)
        {
            var template = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            borderFactory.SetValue(Border.BackgroundProperty, bg);
            borderFactory.SetValue(Border.PaddingProperty, new Thickness(16, 8, 16, 8));
            borderFactory.Name = "border";

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            borderFactory.AppendChild(contentPresenter);
            template.VisualTree = borderFactory;

            // Hover
            var trigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            trigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.85, "border"));
            template.Triggers.Add(trigger);

            return template;
        }

        // Rebuilds all download cards to reflect the current theme (dark/light)
        private void RefreshDownloadCards()
        {
            DownloadsList.Children.Clear();
            foreach (var item in _downloads)
            {
                AddDownloadToUI(item);
            }
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
                                // Mark interrupted downloads as paused so user can resume
                                download.Status = DownloadStatus.Paused;
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

        private void AllTab_Click(object sender, MouseButtonEventArgs e)
        {
            _showOnlyCompleted = false;
            UpdateDownloadsTabs(allActive: true);

            // Show all downloads
            foreach (Border item in DownloadsList.Children)
            {
                item.Visibility = Visibility.Visible;
            }
        }

        private void DownloadedTab_Click(object sender, MouseButtonEventArgs e)
        {
            _showOnlyCompleted = true;
            UpdateDownloadsTabs(allActive: false);

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

        private void UpdateDownloadsTabs(bool allActive)
        {
            // Active color: accent blue (same in both modes for clarity)
            var activeBrush  = new SolidColorBrush(Color.FromRgb(0x5B, 0x8D, 0xEF));
            var inactiveBrush = new SolidColorBrush(_isDarkMode
                ? Color.FromRgb(0x8B, 0x99, 0xB3)
                : Color.FromRgb(0x9C, 0xA3, 0xAF));

            // All tab text + icon
            AllTabText.Foreground   = allActive ? activeBrush : inactiveBrush;
            AllTabIcon.Stroke       = allActive ? activeBrush : inactiveBrush;

            // Downloaded tab text + icon
            DownloadedTabText.Foreground  = !allActive ? activeBrush : inactiveBrush;
            DownloadedTabIcon.Stroke      = !allActive ? activeBrush : inactiveBrush;

            // Pill background: crisp white in light, elevated surface in dark
            TabPill.Background = _isDarkMode
                ? new SolidColorBrush(Color.FromRgb(0x32, 0x35, 0x4A))
                : Brushes.White;

            // Slide the pill: col 0 = All, col 1 = Downloaded
            // Guard: ActualWidth can be 0 when the panel hasn't been laid out yet
            // (e.g. theme toggle called before DownloadsPanel slides in)
            double containerWidth = TabsContainerBorder.ActualWidth;
            if (containerWidth > 8)
            {
                double halfWidth = Math.Max((containerWidth - 8) / 2.0, 1);
                TabPill.Width = halfWidth;
                TabPill.HorizontalAlignment = HorizontalAlignment.Left;
                TabPill.Margin = new Thickness(allActive ? 0 : halfWidth, 0, 0, 0);
            }
            // else: pill keeps its XAML default width until the panel is actually shown
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
                // Remove completed items from the data collection
                var completedItems = _downloads.Where(d => d.Status == DownloadStatus.Completed).ToList();
                foreach (var item in completedItems)
                {
                    _downloads.Remove(item);
                }
                
                // Remove completed items from the UI
                var uiItemsToRemove = DownloadsList.Children
                    .OfType<Border>()
                    .Where(b => b.Tag is DownloadItem item && item.Status == DownloadStatus.Completed)
                    .ToList();

                foreach (var item in uiItemsToRemove)
                {
                    DownloadsList.Children.Remove(item);
                }
                
                // Persist the change so cleared items don't reappear on restart
                SaveDownloadHistory();
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

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            SaveDownloadHistory();
            base.OnClosing(e);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            SaveDownloadHistory();
            Application.Current.Shutdown();
        }

        private void DarkModeToggle_Click(object sender, RoutedEventArgs e)
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
                    // Switch to dark background image
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
                        System.Diagnostics.Debug.WriteLine("dark.jpg not found, keeping current background");
                    }
                    
                    // Update dynamic brushes
                    UpdateBrush("BackgroundBrush", "DarkBackground");
                    UpdateBrush("SearchBoxBrush", "DarkSearchBox");
                    UpdateBrush("CardBgBrush", "DarkCardBg");
                    UpdateBrush("PanelBgBrush", "DarkPanelBg");
                    
                    // Glass card — dark frosted glass
                    GlassCard.Background = new SolidColorBrush(Color.FromArgb(0xCC, 30, 30, 34));
                    AppTitle.Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 235));
                    SearchBorder.Background = new SolidColorBrush(Color.FromArgb(0x33, 255, 255, 255));
                    
                    // Update action button styles for dark mode
                    UpdateActionButtonsTheme(true);
                    BackgroundOverlay.Background = new SolidColorBrush(Color.FromArgb(0x55, 0, 0, 0));
                    
                    // URL text box
                    if (UrlTextBox.Text == "Paste YouTube link here\u2026")
                        UrlTextBox.Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 130));
                    
                    // Quality dialog
                    QualityDialogBorder.Background = new SolidColorBrush(Color.FromRgb(36, 36, 40));
                    QualityHeaderText.Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 230));
                    QualityCloseButton.Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 165));
                    QualityVideoTitle.Foreground = new SolidColorBrush(Color.FromRgb(230, 230, 230));
                    QualityChannelInfo.Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 145));
                    QualityTabContainer.Background = new SolidColorBrush(Color.FromRgb(28, 28, 32));
                    
                    // Downloads panel — dark mode
                    DownloadsHeaderBorder.Background = new SolidColorBrush(Color.FromRgb(0x19, 0x1B, 0x27));
                    DownloadsTitle.Foreground = Brushes.White;
                    var backArrowDark = FindVisualChild<System.Windows.Shapes.Path>(this, "BackArrowPath");
                    if (backArrowDark != null) backArrowDark.Stroke = new SolidColorBrush(Color.FromRgb(0xBB, 0xC8, 0xE0));
                    TabsContainerBorder.Background = new SolidColorBrush(Color.FromRgb(0x24, 0x26, 0x36));
                    TabsContainerBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x38, 0x3A, 0x50));
                    // Subtitle search container
                    SubtitleSearchContainer.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2D, 0x3E));
                    SubtitleSearchBox.Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE6, 0xF4));
                    SubtitleSearchBox.CaretBrush = new SolidColorBrush(Color.FromRgb(0x5B, 0x8D, 0xEF));
                    // Update gear icon fill via visual tree
                    var gearIcon = FindVisualChild<System.Windows.Shapes.Path>(this, "SettingsGearIcon");
                    if (gearIcon != null) gearIcon.Fill = new SolidColorBrush(Color.FromRgb(0xBB, 0xCC, 0xFF));
                    var settingsBg = FindVisualChild<Border>(this, "SettingsBorder");
                    if (settingsBg != null) settingsBg.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2F, 0x45));
                    // Update tab colors for current active tab
                    UpdateDownloadsTabs(allActive: !_showOnlyCompleted);
                    
                    // Dark mode icon → moon
                    var icon = FindDarkModeIcon();
                    if (icon != null) icon.Text = "\U0001F319";
                }
                else
                {
                    // Switch to light background image
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
                    
                    // Update dynamic brushes
                    UpdateBrush("BackgroundBrush", "LightBackground");
                    UpdateBrush("SearchBoxBrush", "LightSearchBox");
                    UpdateBrush("CardBgBrush", "LightCardBg");
                    UpdateBrush("PanelBgBrush", "LightPanelBg");
                    
                    // Glass card — light frosted glass
                    GlassCard.Background = new SolidColorBrush(Color.FromArgb(0xB3, 255, 255, 255));
                    AppTitle.Foreground = new SolidColorBrush(Color.FromRgb(26, 26, 46));
                    SearchBorder.Background = new SolidColorBrush(Color.FromArgb(0xCC, 255, 255, 255));
                    
                    // Update action button styles for light mode
                    UpdateActionButtonsTheme(false);
                    BackgroundOverlay.Background = new SolidColorBrush(Color.FromArgb(0x26, 0, 0, 0));
                    
                    // URL text box
                    if (UrlTextBox.Text == "Paste YouTube link here\u2026")
                        UrlTextBox.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                    
                    // Quality dialog
                    QualityDialogBorder.Background = Brushes.White;
                    QualityHeaderText.Foreground = new SolidColorBrush(Color.FromRgb(26, 26, 46));
                    QualityCloseButton.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
                    QualityVideoTitle.Foreground = new SolidColorBrush(Color.FromRgb(31, 41, 55));
                    QualityChannelInfo.Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128));
                    QualityTabContainer.Background = new SolidColorBrush(Color.FromRgb(243, 244, 246));
                    
                    // Downloads panel — light mode
                    DownloadsHeaderBorder.Background = new SolidColorBrush(Color.FromRgb(0xFA, 0xFB, 0xFF));
                    DownloadsTitle.Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                    var backArrowLight = FindVisualChild<System.Windows.Shapes.Path>(this, "BackArrowPath");
                    if (backArrowLight != null) backArrowLight.Stroke = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
                    TabsContainerBorder.Background = new SolidColorBrush(Color.FromRgb(0xED, 0xF0, 0xF7));
                    TabsContainerBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0xDC, 0xE8));
                    // Subtitle search container
                    SubtitleSearchContainer.Background = new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6));
                    SubtitleSearchBox.Foreground = new SolidColorBrush(Color.FromRgb(0x37, 0x41, 0x51));
                    SubtitleSearchBox.CaretBrush = new SolidColorBrush(Color.FromRgb(0x5B, 0x8D, 0xEF));
                    // Update gear icon fill via visual tree
                    var gearIcon = FindVisualChild<System.Windows.Shapes.Path>(this, "SettingsGearIcon");
                    if (gearIcon != null) gearIcon.Fill = new SolidColorBrush(Color.FromRgb(0x5B, 0x8D, 0xEF));
                    var settingsBg = FindVisualChild<Border>(this, "SettingsBorder");
                    if (settingsBg != null) settingsBg.Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xF2, 0xFF));
                    // Update tab colors for current active tab
                    UpdateDownloadsTabs(allActive: !_showOnlyCompleted);
                    
                    // Dark mode icon → sun
                    var icon = FindDarkModeIcon();
                    if (icon != null) icon.Text = "\u2600";
                }
                
                // Rebuild download cards so they pick up the current theme
                RefreshDownloadCards();
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

        private void UpdateActionButtonsTheme(bool darkMode)
        {
            try
            {
                // Find the action buttons inside the GlassCard
                var glassCardContent = GlassCard.Child as StackPanel;
                if (glassCardContent == null) return;
                
                // The action buttons grid is the last child of the StackPanel
                var actionGrid = glassCardContent.Children[glassCardContent.Children.Count - 1] as Grid;
                if (actionGrid == null) return;

                var iconStroke = darkMode
                    ? new SolidColorBrush(Color.FromRgb(200, 205, 213))   // lighter icons for dark mode
                    : new SolidColorBrush(Color.FromRgb(71, 85, 105));    // #475569 for light mode

                var buttonBg = darkMode
                    ? new SolidColorBrush(Color.FromArgb(0x26, 255, 255, 255))  // subtle white in dark mode
                    : new SolidColorBrush(Color.FromArgb(0xBB, 255, 255, 255)); // frosted white in light mode

                // Iterate through the grid to find buttons
                foreach (var child in actionGrid.Children)
                {
                    if (child is Button btn)
                    {
                        // Apply the template so we can find child elements
                        btn.ApplyTemplate();
                        var templateBorder = btn.Template?.FindName("border", btn) as Border;
                        if (templateBorder != null)
                        {
                            templateBorder.Background = buttonBg;
                        }
                        
                        // Update icon paths by traversing visual tree
                        UpdatePathStrokesInVisualTree(btn, iconStroke);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating action buttons theme: {ex.Message}");
            }
        }

        private void UpdatePathStrokesInVisualTree(System.Windows.DependencyObject parent, Brush stroke)
        {
            int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is System.Windows.Shapes.Path path && path.Stroke != null)
                {
                    path.Stroke = stroke;
                }
                UpdatePathStrokesInVisualTree(child, stroke);
            }
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    var json = File.ReadAllText(_settingsPath);
                    var settings = JsonDocument.Parse(json);
                    var root = settings.RootElement;
                    
                    if (root.TryGetProperty("downloadFolder", out var folderProp))
                    {
                        var folder = folderProp.GetString();
                        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                        {
                            _downloadFolder = folder;
                        }
                        else
                        {
                            SetDefaultDownloadFolder();
                        }
                    }
                    else
                    {
                        SetDefaultDownloadFolder();
                    }
                    
                    // Load dark mode preference (defaults to true)
                    if (root.TryGetProperty("isDarkMode", out var darkModeProp))
                    {
                        _isDarkMode = darkModeProp.GetBoolean();
                    }
                }
                else
                {
                    SetDefaultDownloadFolder();
                }
            }
            catch
            {
                SetDefaultDownloadFolder();
            }
            
            Directory.CreateDirectory(_downloadFolder);
        }

        private void SetDefaultDownloadFolder()
        {
            _downloadFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                "YouTubeDownloader"
            );
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new
                {
                    downloadFolder = _downloadFolder,
                    isDarkMode = _isDarkMode
                };
                
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        private void DownloadSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select Download Folder",
                SelectedPath = _downloadFolder,
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var newFolder = dialog.SelectedPath;
                
                // Confirm if folder is different
                if (newFolder != _downloadFolder)
                {
                    var result = MessageBox.Show(
                        $"Change download folder to:\n{newFolder}\n\n" +
                        "Future downloads will be saved to this location.\n" +
                        "Existing downloads will remain in their current location.",
                        "Change Download Folder",
                        MessageBoxButton.OKCancel,
                        MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.OK)
                    {
                        _downloadFolder = newFolder;
                        Directory.CreateDirectory(_downloadFolder);
                        _downloadsHistoryPath = Path.Combine(_downloadFolder, "downloads_history.json");
                        SaveSettings();
                        
                        MessageBox.Show(
                            $"Download folder updated successfully!\n\nNew location:\n{_downloadFolder}",
                            "Success",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    }
                }
            }
        }
    }

    // Models moved to src/Models folder - keeping code organized!
}
