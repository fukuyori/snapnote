# Changelog

## 2.1.0 - 2026-06-22

### Added
- Added mouse wheel zoom in the editor, with a maximum zoom level of 1200%.
- Added image viewport panning with middle-button and right-button dragging.
- Added a Move tool that is selected by default when the editor opens. In Move mode, left-button dragging pans the image viewport.
- Added a gentle Sharpen image operation.

### Changed
- Sharpen now participates in the undo/redo history, so `Ctrl+Z` restores the previous image and `Ctrl+Y` reapplies it.
- Installer version and application package version are now `2.1.0`.

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
