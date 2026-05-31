# Changelog

## 2.0.0 - 2026-05-31

### Added
- Added window capture from the capture overlay by clicking a target window.
- Added previous-region capture: press `Space` before dragging to show the last capture region, then press `Enter` to capture it again.
- Added a cursor-following overlay message while the previous-region/window-selection flow is active.
- Added configurable automatic save location.
- Added timestamp-based PNG auto-save file names.
- Added editor shortcuts:
  - `Ctrl+C` copies the image to the clipboard.
  - `Ctrl+X` copies the image to the clipboard and closes the editor.
  - `Ctrl+S` saves the image.
  - `Ctrl+W` saves the image and closes the editor.
  - `Ctrl+Q` closes the editor without saving.
- Added release build and installer build scripts:
  - `scripts/build-release.ps1`
  - `scripts/build-installer.ps1`

### Changed
- Save now writes directly to the configured save folder instead of showing a save dialog.
- Installer version and application package version are now `2.0.0`.
- Window capture now uses foreground Z-order selection and DWM frame bounds for more accurate window position and size.

### Fixed
- Fixed an Inno Setup `UninstallRun` warning by adding `RunOnceId`.
