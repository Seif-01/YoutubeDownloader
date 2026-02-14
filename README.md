# YouTube Downloader 🌸

A beautiful and simple YouTube video downloader built with C# and WPF. Download your favorite videos in different qualities with a nice cherry blossom themed interface!

![.NET](https://img.shields.io/badge/.NET-8.0-blue)
![WPF](https://img.shields.io/badge/WPF-Windows-blue)
![License](https://img.shields.io/badge/license-MIT-green)



## Features

-  Download YouTube videos in multiple quality options (360p, 720p, 1080p, etc.)
-  Download audio-only files in MP3 or M4A format
-  Real-time download progress with speed indicator
-  Beautiful cherry blossom themed UI with animated petals
-  Queue multiple downloads at once
-  Choose custom download location

## Requirements

- Windows 10 or later x64
- [FFMPEG](https://ffmpeg.org/download.html) (make sure to add it to PATH on Windows).
- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- [yt-dlp](https://github.com/yt-dlp/yt-dlp/releases)(automatically downloaded on first run if missing)


## How It Works

This app uses [yt-dlp](https://github.com/yt-dlp/yt-dlp) under the hood to download videos. It's basically a pretty UI wrapper around yt-dlp with some extra features I added.

The app will automatically download yt-dlp on first run if it's not found. No manual setup needed!

## Project Structure

```
src/
├── Windows/      - UI Windows (MainWindow, dialogs)
├── Models/       - Data classes (VideoInfo, FormatInfo, etc.)
└── Helpers/      - Utility functions (file operations, yt-dlp integration)
```

I organized the code into folders to make it easier to maintain. Still learning about proper project architecture but this works for now!

## Known Issues

- Sometimes the progress percentage jumps around (yt-dlp does this, not my code!)
- The sakura petals animation might lag on older computers
- Sometimes it take long befor video start downloading


## Roadmap

Things I want to add eventually:

- [ ] Playlist support (download entire playlists)
- [x] Download history with search
- [ ] Subtitle downloading
- [x] Multiple simultaneous downloads
- [x] Themes (dark mode, different colors)
- [ ] Auto-update feature
- [x] Portable mode (no installation required)

## Contributing

I'm still learning C# and WPF, so if you have suggestions or improvements, feel free to open an issue or PR! 

## Tech Stack

- **Language**: C# 12
- **Framework**: .NET 8.0
- **UI**: WPF (Windows Presentation Foundation)
- **Download Engine**: yt-dlp

No external NuGet packages required! Everything is built-in except yt-dlp.

## FAQ

**Q: Is this legal?**  
A: This tool is for downloading videos you have permission to download. Please respect copyright laws and YouTube's Terms of Service.

**Q: Why does Windows Defender flag it?**  
A: Sometimes unsigned .exe files trigger warnings. The app is safe - you can check the source code yourself!

**Q: Can I download age-restricted videos?**  
A: Not currently. This is a limitation of yt-dlp when not logged in.

**Q: Does it work with other sites?**  
A: Technically yes (yt-dlp supports many sites), but I only tested with YouTube. YMMV!

## Credits

- **yt-dlp** - The amazing download engine: https://github.com/yt-dlp/yt-dlp
- **Sakura background** - Free stock image from the pintrest : 
[light mode](https://www.pinterest.com/pin/74872412552353463/) 
[Dark mode](https://www.pinterest.com/pin/1125968708311749/)
- **Icon** - Custom made (badly) 

## License

MIT License - feel free to use this however you want!

## Support

If you found this useful, consider:
-  Starring this repo
-  Reporting bugs
-  Suggesting features
-  Forking and improving it!

---

**Note**: This is a learning project! I started this because i needed application like this. The code is far from to be perfect but it works. Feel free to teach me better ways to do things! 🙂

<img width="446" height="797" alt="2026-02-13 22_54_53-" src="https://github.com/user-attachments/assets/27f818c9-debd-4a3a-9f04-4cc90f4db96d" />

<img width="449" height="795" alt="2026-02-13 22_55_11-" src="https://github.com/user-attachments/assets/864b4de3-1c51-45d5-b9d3-8897f0c95f7d" />

<img width="448" height="795" alt="2026-02-13 22_55_44-" src="https://github.com/user-attachments/assets/c5d18bc2-930e-4fb0-a2aa-6009092f0aeb" />

<img width="452" height="794" alt="2026-02-13 22_57_05-" src="https://github.com/user-attachments/assets/10d13586-9338-4a44-8ce0-d7d74495220b" />

<img width="449" height="795" alt="2026-02-13 22_57_20-" src="https://github.com/user-attachments/assets/63876099-d0cf-43d0-b2e0-d293a24a9f1c" />









