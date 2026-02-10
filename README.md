# YouTube Downloader 🌸

A beautiful and simple YouTube video downloader built with C# and WPF. Download your favorite videos in different qualities with a nice cherry blossom themed interface!

![.NET](https://img.shields.io/badge/.NET-8.0-blue)
![WPF](https://img.shields.io/badge/WPF-Windows-blue)
![License](https://img.shields.io/badge/license-MIT-green)



## Features

- 🎥 Download YouTube videos in multiple quality options (360p, 720p, 1080p, etc.)
- 🎵 Download audio-only files in MP3 or M4A format
- 📊 Real-time download progress with speed indicator
- 🌸 Beautiful cherry blossom themed UI with animated petals
- 📝 Queue multiple downloads at once
- 💾 Choose custom download location

## Requirements

- Windows 10 or later x64
- .NET 8.0 Runtime (download from [Microsoft](https://dotnet.microsoft.com/download/dotnet/8.0))
- yt-dlp (automatically downloaded on first run if missing)


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
- [ ] Download history with search
- [ ] Subtitle downloading
- [ ] Multiple simultaneous downloads
- [ ] Themes (dark mode, different colors)
- [ ] Auto-update feature
- [ ] Portable mode (no installation required)

## Contributing

I'm still learning C# and WPF, so if you have suggestions or improvements, feel free to open an issue or PR! 

## Tech Stack

- **Language**: C# 12
- **Framework**: .NET 8.0
- **UI**: WPF (Windows Presentation Foundation)
- **Download Engine**: yt-dlp
- **JSON**: System.Text.Json

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
light mode (https://www.pinterest.com/pin/5981412001759686) 
Dark mode (https://www.pinterest.com/pin/27866091441942633/)
- **Icon** - Custom made (badly)  😅

## License

MIT License - feel free to use this however you want!

## Support

If you found this useful, consider:
- ⭐ Starring this repo
- 🐛 Reporting bugs
- 💡 Suggesting features
- 🔀 Forking and improving it!

---

**Note**: This is a learning project! I started this because i needed application like this. The code is far from to be perfect but it works. Feel free to teach me better ways to do things! 🙂
<img width="449" height="799" alt="2026-02-10 04_21_23-" src="https://github.com/user-attachments/assets/9c9f8ee1-723e-4e4c-8d96-29315e7d7394" />
<img width="450" height="799" alt="2026-02-10 04_21_14-" src="https://github.com/user-attachments/assets/544a81bd-4f11-4728-8b1c-2e9fffe213d0" />
<img width="450" height="796" alt="2026-02-10 04_21_06-" src="https://github.com/user-attachments/assets/8aac60e3-0f98-40a7-83dc-8e0caffc05cc" />
<img width="449" height="797" alt="2026-02-10 04_20_54-" src="https://github.com/user-attachments/assets/194dadcd-4d2e-442f-950b-428ed41f0466" />
<img width="451" height="797" alt="2026-02-10 04_20_23-" src="https://github.com/user-attachments/assets/669d03c0-8b75-40bd-9c7f-4fd630e837b0" />
<img width="450" height="798" alt="2026-02-10 04_20_11-" src="https://github.com/user-attachments/assets/0628c2c6-7e8e-4976-8e7c-abef1d722211" />


