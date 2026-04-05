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

### Option 1: Download Installer
1. Download the installer (`SnapNoteStudio_Setup_x.x.x.exe`) from the [Latest Release](https://github.com/fukuyori/snapnote/releases/latest) page
2. Run the installer and follow the on-screen instructions

### Option 2: Build from Source

#### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Inno Setup](https://jrsoftware.org/isdownload.php) (only required for creating the installer)
- Visual Studio 2022 or VS Code (optional)

#### Build Steps

```bash
# Clone the repository
git clone https://github.com/fukuyori/snapnote.git
cd snapnote

# Restore dependencies
dotnet restore

# Build
dotnet build -c Release

# Run
dotnet run -c Release
```

#### Publish as Single Executable

```bash
dotnet publish -c Release

# Output: bin/Release/net8.0-windows/win-x64/publish/SnapNoteStudio.exe
```

#### Creating the Installer

With Inno Setup installed, run the following:

```bash
# 1. Publish the Release build
dotnet publish -c Release

# 2. Create the installer
iscc installer.iss

# Output: installer_output/SnapNoteStudio_Setup_x.x.x.exe
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

Right-click the system tray icon and select "Settings" to open the settings dialog.

| Setting | Description | Default |
|---------|-------------|---------|
| Language | Switch the display language. Supports English / 日本語 / 简体中文 / Español / 한국어 | English |
| Capture hotkey | Select the shortcut key to start screen capture. Available options: PrintScreen / Ctrl+PrintScreen / Alt+PrintScreen / Ctrl+Shift+S / Ctrl+Shift+C / Ctrl+Alt+S / F12 / Ctrl+F12 | Ctrl+Shift+S |
| Start with Windows | When checked, SnapNote Studio will automatically start when Windows starts. Registered via registry (HKCU) | OFF |
| Thickness | Set the default stroke thickness for drawing tools (1–10) | 3 |
| Opacity | Set the default opacity for drawing tools (10%–100%) | 100% |

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
