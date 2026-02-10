using System;
using System.IO;
using System.Linq;

namespace YouTubeDownloader.Helpers
{
    // Helper class for file operations
    // I put utility functions here to keep code organized
    public static class FileHelper
    {
        // Remove invalid characters from filename
        // Found this technique online, it works pretty well!
        public static string SanitizeFileName(string fileName)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            
            // Also remove some other problematic characters
            sanitized = sanitized.Replace(":", "").Replace("?", "").Replace("\"", "");
            
            return sanitized.Trim();
        }

        // Get unique file path if file already exists
        public static string GetUniqueFilePath(string folderPath, string baseFileName, string extension)
        {
            var filePath = Path.Combine(folderPath, baseFileName + extension);
            
            int counter = 1;
            while (File.Exists(filePath))
            {
                var uniqueName = $"{baseFileName} ({counter}){extension}";
                filePath = Path.Combine(folderPath, uniqueName);
                counter++;
            }
            
            return filePath;
        }
    }
}
