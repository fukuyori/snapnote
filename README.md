# SnapNote Studio

<p align="center">
  <img src="Resources/icon.svg" width="128" height="128" alt="SnapNote Studio Icon">
</p>

**SnapNote Studio** is a powerful screen capture and annotation tool for Windows. Capture any area of your screen and annotate it with arrows, shapes, text, blur effects, and more.

[日本語版 README](README.ja.md)

## Features

### Capture
- **Region selection**: Click and drag to select any area of your screen
- **Multi-monitor support**: Works seamlessly across multiple displays
- **High DPI support**: Crisp captures on high-resolution displays

### Annotation Tools
| Tool | Shortcut | Description |
|------|----------|-------------|
| Select | V | Select and move annotations |
| Arrow | A | Draw arrows to point at things |
| Line | L | Draw straight lines |
| Rectangle | R | Draw rectangles |
| Ellipse | E | Draw ellipses/circles |
| Text | T | Add text with customizable font size |
| Number | N | Add numbered steps (①②③...) |
| Highlighter | H | Semi-transparent highlight pen |

### Effect Tools
| Tool | Shortcut | Description |
|------|----------|-------------|
| Fill | F | Draw filled rectangles |
| Mosaic | M | Pixelate sensitive information |
| Blur | B | Blur sensitive areas |
| Spotlight | S | Darken everything except selected area |
| Magnifier | G | Zoom in on a specific area |

### Image Operations
- **Crop**: Trim the image to a selected area
- **Rotate**: Rotate the image 90° clockwise
- **Resize**: Scale the image with aspect ratio preservation

### Additional Features
- **Undo/Redo**: Full history support (Ctrl+Z / Ctrl+Y)
- **Copy to clipboard**: Quick sharing (Ctrl+C)
- **Save to file**: PNG or JPEG format (Ctrl+S)
- **Customizable hotkey**: Choose your preferred capture shortcut
- **System tray**: Runs quietly in the background
- **Multi-language**: English and Japanese support

## System Requirements

- Windows 10/11 (64-bit)
- .NET 8.0 Runtime

## Installation

### Option 1: Download Release
1. Download the latest release from the [Releases](../../releases) page
2. Extract the ZIP file
3. Run `SnapNoteStudio.exe`

### Option 2: Build from Source

#### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 or VS Code (optional)

#### Build Steps

```bash
# Clone the repository
git clone https://github.com/yourusername/SnapNoteStudio.git
cd SnapNoteStudio

# Restore dependencies
dotnet restore

# Build (Debug)
dotnet build -c Debug

# Build (Release)
dotnet build -c Release

# Run
dotnet run -c Release
```

#### Publish as Single Executable

```bash
# Create self-contained single-file executable
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Output will be in: bin/Release/net8.0-windows/win-x64/publish/
```

## Usage

### Starting a Capture

1. **Hotkey**: Press `Ctrl+Shift+S` (default) to start capture mode
2. **System Tray**: Double-click the tray icon, or right-click and select "Capture"

### Capture Mode

1. Click and drag to select the area you want to capture
2. Release the mouse button to open the editor
3. Press `Escape` to cancel

### Editor

1. Use the left sidebar to select annotation tools
2. Adjust color, thickness, and opacity in the top toolbar
3. Draw annotations on the image
4. Use `Ctrl+Z` to undo, `Ctrl+Y` to redo
5. Click "Copy" or press `Ctrl+C` to copy to clipboard
6. Click "Save" or press `Ctrl+S` to save to file

### Settings

Right-click the system tray icon and select "Settings" to:
- Change the capture hotkey
- Enable/disable Windows startup
- Set default tool settings
- Change language (English/Japanese)

## Keyboard Shortcuts

### Global
| Shortcut | Action |
|----------|--------|
| Ctrl+Shift+S | Start capture (default, configurable) |

### Editor
| Shortcut | Action |
|----------|--------|
| Ctrl+Z | Undo |
| Ctrl+Y | Redo |
| Ctrl+C | Copy to clipboard |
| Ctrl+S | Save to file |
| Delete | Delete selected annotation |
| Escape | Deselect / Cancel crop mode |
| V, A, L, R, E, T, N, H, F, M, B, S, G | Tool shortcuts |

## Configuration

Settings are stored in:
```
%APPDATA%\SnapNoteStudio\settings.json
```

## License

MIT License - see [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Acknowledgments

- [Hardcodet.NotifyIcon.Wpf](https://github.com/hardcodet/wpf-notifyicon) for system tray functionality
- Built with .NET 8.0 and WPF
