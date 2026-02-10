# Installation Guide

This guide will help you get the YouTube Downloader up and running on your computer.

## Quick Start (For Non-Programmers)

If you just want to use the app and don't care about the code:

1. Download the latest release from the Releases page
2. Extract the ZIP file to any folder (like `C:\Program Files\YouTubeDownloader`)
3. Double-click `YouTubeDownloader.exe`
4. That's it! The app will auto-download yt-dlp if needed.


## Installing .NET 8.0 Runtime

The app requires .NET 8.0 to run. Here's how to install it:

1. Go to https://dotnet.microsoft.com/download/dotnet/8.0
2. Under ".NET Desktop Runtime 8.0.x", click **Download x64**
3. Run the installer
4. Restart your computer (if prompted)

**Already have .NET?** You can check by opening Command Prompt and typing:
```
dotnet --version
```
If it shows 8.0.x, you're good to go!

## For Developers: Building from Source

### Prerequisites

- Visual Studio 2022 or later (Community Edition is free)
- .NET 8.0 SDK
- Git (optional, for cloning)

### Step 1: Get the Code

**Option A: Clone with Git**
```bash
git clone https://github.com/yourusername/youtube-downloader.git
cd youtube-downloader
```

**Option B: Download ZIP**
1. Click the green "Code" button on GitHub
2. Select "Download ZIP"
3. Extract to a folder

### Step 2: Build the Project

**Using Visual Studio:**
1. Open `YouTubeDownloader.sln`
2. Right-click the solution → "Restore NuGet Packages" (if needed)
3. Press F5 or click "Start" to build and run

**Using Command Line:**
```bash
# Restore dependencies (usually automatic)
dotnet restore

# Build the project
dotnet build

# Run the app
dotnet run

# Or build a release version
dotnet build --configuration Release
```

### Step 3: Find the Executable

After building, you'll find the .exe at:
```
bin/Debug/net8.0-windows/YouTubeDownloader.exe
```

Or for Release builds:
```
bin/Release/net8.0-windows/YouTubeDownloader.exe
```

## Manual yt-dlp Installation (Optional)

The app downloads yt-dlp automatically, but if you want to install it manually:

1. Download from: https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe
2. Place `yt-dlp.exe` in the same folder as `YouTubeDownloader.exe`
3. Done!

## Troubleshooting

### "The application requires .NET 8.0"
- Install the .NET 8.0 Desktop Runtime (see above)

### "Windows protected your PC" warning
- Click "More info" → "Run anyway"
- This happens because the .exe isn't digitally signed (costs money!)

### "yt-dlp.exe is not recognized"
- The app should auto-download it on first run
- If it fails, download manually and place it in the app folder

### App won't start / crashes immediately
1. Make sure you have .NET 8.0 installed
2. Try running as Administrator
3. Check Windows Event Viewer for error details
4. Open an issue on GitHub with the error message

### Downloads fail
- Check your internet connection
- Try a different video URL
- Make sure you have write permissions to the download folder
- yt-dlp might need updating (delete yt-dlp.exe and let the app re-download it)

### UI looks weird / blurry
- Right-click `YouTubeDownloader.exe` → Properties → Compatibility
- Check "Disable display scaling on high DPI settings"
- Or change your Windows display scaling settings

## Portable Mode

Want to run from a USB drive?

1. Copy the entire app folder to your USB drive
2. Make sure `yt-dlp.exe` is in the same folder
3. Run from anywhere!

The app stores downloads wherever you choose, so no registry entries or AppData files are created.

## Uninstallation

To remove the app:

1. Delete the application folder
2. That's it! No registry entries to clean up.

