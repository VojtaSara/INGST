# INGST - Ingest Inspector

Professional video and audio ingest tool for media production workflows.

## 🎯 Features

- **Multi-Camera Support**: Import and manage multiple camera feeds
- **Audio Processing**: Automatic detection and import of audio files (MIC1.wav, MIC2.wav, etc.)
- **Smart File Renaming**: Audio files are automatically renamed to prevent conflicts
- **Fast Copying**: Optimized file copying with progress tracking
- **Pause/Stop Controls**: Full control over copy operations
- **Thumbnail Generation**: Automatic video frame extraction for previews
- **Professional UI**: Dark theme optimized for media workflows

## 📁 Final Build Location

The final executable is located at:
```
bin\Release\net9.0-windows\win-x64\publish\INGST.exe
```

## 🚀 Quick Start

1. **Run the application**: Double-click `INGST.exe` or use `Run_INGST.bat`
2. **Import Camera Files**: Click "IMPORT" for each camera section
3. **Import Audio Files**: Click "IMPORT AUDIO" and select folder with audio subfolders
4. **Set Destination**: Click "Select Destination Folder"
5. **Build Project**: Fill in Project Code, Name, and click "BUILD PROJECT"

## ⚙️ Requirements

- **Windows 10/11** (64-bit)
- **FFmpeg**: Must be installed and available in PATH
- **.NET 9.0 Runtime**: Included in the build

## 🔧 Technical Details

- **Framework**: .NET 9.0 Windows
- **UI**: WPF with dark theme
- **Video Processing**: FFmpeg integration
- **Audio Analysis**: Custom WAV silence detection
- **File Operations**: Optimized async copying with progress

## 📋 File Structure

```
INGST/
├── INGST.exe              # Main executable (413KB)
├── mainicon.ico           # Application icon
├── GEARS.gif              # UI logo
└── Run_INGST.bat          # Quick launcher
```

## 🎨 Custom Icon

The application uses `mainicon.ico` as the custom icon for:
- Application window
- Taskbar icon
- File association icon

## ⚡ Performance Features

- **Fast Copying**: 1MB buffer size for optimal speed
- **Progress Tracking**: Real-time speed and time remaining
- **Pause/Stop**: Full control over operations
- **Auto Cleanup**: Unfinished files are cleaned up on cancellation
- **Force Quit**: Red X immediately closes all processes

## 🔒 Safety Features

- **File Validation**: Checks for silent audio files
- **Progress Backup**: Tracks copied vs unfinished files
- **Error Handling**: Comprehensive error recovery
- **Process Cleanup**: Kills hanging FFmpeg processes on exit

---

**Version**: 1.0.0  
**Build Date**: July 2025  
**Target**: Windows x64 