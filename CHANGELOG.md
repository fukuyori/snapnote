# Changelog

## 4.1.1 - 2026-10-11

### Fixed
- Disabled the tray icon hover tooltip to prevent it from remaining visible at the top-left of the primary display.

## 4.1.0 - 2026-09-19

### Added
- Added diagnostic logging for window-selection candidates and results.

### Fixed
- Window selection now ignores full-screen Windows input and shell backing surfaces that could be mistaken for the selected window on the primary display.

## 4.0.0 - 2026-09-10

### Added
- The image editor window now displays `SnapNote Studio` and the application version in its title.
- Added a signed `uiAccess` release mode for receiving capture hotkeys while elevated applications are active.

### Changed
- The Settings dialog uses the `Snapnote` application name, while the image editor title no longer includes a localized editing suffix.
- Capture hotkeys now use a low-level keyboard hook instead of `RegisterHotKey`.

## 3.2.1 - 2026-09-07

### Added
- The Settings dialog now displays the application version at the top.

## 3.2.0 - 2026-09-07

### Added
- Added an optional 1–9 second capture timer. Press a number key before selecting an area or window to show a countdown and capture the latest screen when it reaches zero.

## 3.1.2 - 2026-08-06

### Fixed
- Fixed the system tray tooltip remaining visible in screenshots when capture starts while the tooltip is open.

## 3.1.1 - 2026-08-01

### Fixed
- Fixed the Resize dialog height so the Cancel and OK buttons remain fully visible at different display scaling and text-size settings.

## 3.1.0 - 2026-07-31

### Added
- OCR results now detect paragraphs and bullet/numbered list items instead of returning one line per detected text line. Wrapped lines within a paragraph are joined together, distinct paragraphs are separated by a blank line, and consecutive list items are kept tight without extra blank lines between them.

## 3.0.0 - 2026-07-31

### Added
- Added OCR (text extraction) in the editor, using the Windows OCR engine. Multiple OCR languages can be selected in Settings; the best-scoring result is kept automatically.
- Added translation of OCR results via a local Ollama server (endpoint and model configurable in Settings), shown alongside the OCR text.
- OCR and translation results are now shown in a resizable pane docked to the right of the editor, instead of a separate dialog window.
- Added `Ctrl` + mouse wheel to zoom the OCR result pane's text size.
- Added a "New Here" toggle in the editor: when enabled, the next capture (taken with the usual shortcut) replaces the image in that same editor window instead of opening a new one.
- Added "Auto OCR" and "Auto Translate" toggles in the editor to automatically run OCR (and optionally translation) whenever a new capture loads.
- Added per-action configurable keyboard shortcuts in Settings for Copy, Save, OCR, Translate, and Close in the editor.
- The "New Here" toggle state is remembered between sessions.

### Changed
- The editor window's minimum size was reduced from 1200x800 to 640x480.
- Tightened spacing between sidebar tool icons and reduced the sidebar's width to remove excess empty space.

## 2.2.1 - 2026-07-23

### Added
- The editor window now remembers its size, position, and maximized state between sessions, instead of resizing to fit the captured image each time.

## 2.2.0 - 2026-07-06

### Changed
- Pressing `Esc` while the previous-region preview (`Space`) is shown now cancels only the preview and returns to normal range/window selection, instead of closing the capture overlay.
- Pressing `Space` again while the previous-region preview is shown now captures the previewed region immediately, matching the existing `Enter` behavior.

### Fixed
- Fixed clicks on windows inside the previous-region preview area not being detected, caused by the fully transparent overlay pixels there allowing clicks to pass through to the window behind.

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
