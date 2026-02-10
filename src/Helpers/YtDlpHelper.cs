using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace YouTubeDownloader.Helpers
{
    // Helper class for yt-dlp operations
    // Trying to separate concerns like I read in a tutorial
    public static class YtDlpHelper
    {
        // Check if yt-dlp.exe exists in the application directory
        public static bool IsYtDlpInstalled(string ytDlpPath)
        {
            return File.Exists(ytDlpPath);
        }

        // Download yt-dlp from GitHub
        // This was tricky to figure out but I got it working!
        public static async Task<bool> DownloadYtDlpAsync(string ytDlpPath)
        {
            try
            {
                // Create a simple progress window
                var progressWindow = new Window
                {
                    Title = "Downloading yt-dlp...",
                    Width = 300,
                    Height = 100,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.NoResize
                };

                var textBlock = new System.Windows.Controls.TextBlock
                {
                    Text = "Downloading yt-dlp.exe...",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 14
                };

                progressWindow.Content = textBlock;
                progressWindow.Show();

                using var client = new HttpClient();
                var url = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
                var response = await client.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(ytDlpPath, bytes);
                    
                    progressWindow.Close();
                    MessageBox.Show("yt-dlp downloaded successfully!", "Success", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return true;
                }
                else
                {
                    progressWindow.Close();
                    MessageBox.Show("Failed to download yt-dlp. Please download manually from:\n" +
                        "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe\n\n" +
                        "And place it in the same folder as this application.",
                        "Download Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error downloading yt-dlp: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        // Show a prompt if yt-dlp is missing
        public static async Task<bool> PromptAndDownloadIfMissingAsync(string ytDlpPath)
        {
            if (!IsYtDlpInstalled(ytDlpPath))
            {
                var result = MessageBox.Show(
                    $"yt-dlp.exe not found at:\n{ytDlpPath}\n\n" +
                    "This app requires yt-dlp to download videos.\n\n" +
                    "Would you like to download it now?",
                    "Missing yt-dlp",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    return await DownloadYtDlpAsync(ytDlpPath);
                }
                return false;
            }
            return true;
        }
    }
}
