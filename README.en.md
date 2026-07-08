![ScreenPaste](screenshots/banner.png)

<a href='https://ko-fi.com/M6T122R1AH' target='_blank'><img height='36' style='border:0px;height:36px;' src='https://storage.ko-fi.com/cdn/kofi6.png?v=6' border='0' alt='Buy Me a Coffee at ko-fi.com' /></a>

[繁體中文](README.md) | **English** | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Español](README.es.md)

# ScreenPaste

A lightweight Windows screenshot, annotation and screen-recording tool: capture in one press, annotate instantly, pin to your screen, and record any region to GIF / MP4 / WebP.

## Features

### Capture
- The whole screen (multi-monitor support)
- **Automatic window & UI-element detection**: hover over a window or an interface element (buttons, panels, web-page blocks) to outline it, then click to capture; scroll the **mouse wheel** to switch the detection level (element ⇄ window)
- Drag to select a custom region, with a size readout and a magnifier for pixel-perfect edges

### Annotate
After capturing, an **icon toolbar** pops up next to the cursor (draggable, and it steers clear of screen edges):

- **Marker** — solid strokes with adjustable thickness / color / opacity
- **Highlighter** — semi-transparent overlay color with adjustable thickness / color / opacity
- **Text** — choose font / size / color / style (normal, bold, italic, strikethrough); clicking outside the box commits it
- **Shapes** — rectangle / rounded rectangle / ellipse, outlined or filled, with adjustable line thickness and color
- **Line / arrow** — drag to draw; each end can be toggled to an arrowhead; adjustable thickness and color; hold `Shift` to snap to 45° angles
- **Stickers** — paste PNG / JPEG / WebP images, drag to move, scroll to resize
- **Blur** — Gaussian blur / mosaic with adjustable strength
- **Direct select / move** — hover any placed annotation and **drag it right away**; `Delete` removes it; moves and deletes are undoable
- **Color picker** — Hex input, RGB and opacity (translucent colors preview over a checkerboard); custom colors are remembered across sessions, right-click a swatch to remove it
- **Undo / Redo** — via buttons and hotkeys; every slider shows a live numeric readout

### Output
- Copy to clipboard
- Save as PNG / JPG (save as, or quick-save to a default folder)
- **Pin to screen**: the image floats on top; drag it, scroll to resize, right-click for a menu; with multiple pins, Esc closes the focused one first, or closes them all when none is focused

### Region recording
- A dedicated global hotkey (**F2** by default): drag-select the region to record, press again (or click Stop) to finish
- A red frame marks the region while recording, with a small pill bar showing the timer and a Stop button
- After recording, an **editor** opens: looping preview, timeline trim handles (`Space` to play, `←`/`→` frame stepping, `I`/`O` to set trim in/out), and export with a progress bar
- Export as **GIF / MP4 / WebP**, switchable right in the editor; a setting can skip the editor and save immediately
- Optional mouse-cursor capture, frame rate 10–30 fps
- Encoded by the bundled `ffmpeg` — no separate install required

### More
- Global hotkeys (**F1** capture, **F2** recording, both configurable) and launch from the **system tray** icon
- **Multiple languages**: Traditional Chinese / English / 日本語 / 한국어 / Français / Deutsch / Español (follows the system by default)
- **Light / Dark / Follow system** theme, with a modern rounded UI and dark title bars
- A centralized **Settings window**: language, hotkeys, theme, run at startup, save folder, recording format / frame rate (hotkey fields are set by simply pressing the combination)
- Optional **run automatically at startup**
- **Auto-update**: checks for new versions via GitHub Releases (toggle in settings, or check manually); one click to download and install

## Install

Download from [Releases](https://github.com/taida957789/ScreenPaste/releases):

- **`ScreenPaste-<version>-setup.exe`** — installer (no administrator rights required, installs to `%APPDATA%\ScreenPaste`, includes a Start menu shortcut and an uninstaller)
- **`ScreenPaste-<version>-win-x64-portable.zip`** — portable, no-install version

Both are self-contained — **no separate .NET Runtime is required**, and `ffmpeg` (for region recording) is bundled.

## Quick start

![Toolbar](screenshots/ui.png)

1. Once launched it stays in the system tray; press **F1** (or double-click the tray icon) to start a capture.
2. Hover over a window or UI element to auto-outline it and click to capture, or drag to select a custom region.
3. Pick a tool from the pop-up toolbar, adjust its parameters, and annotate on the selection.
4. Press **Copy / Save / Pin** to output; `Esc` goes back to reselect or cancels.

Recording: press **F2** and drag-select a region to start; press **F2** again (or click Stop) to finish, then preview, trim and export as GIF/MP4/WebP in the editor (a setting can save immediately instead).

## Hotkeys

| Context | Keys | Action |
|---|---|---|
| Global | `F1` | Start a capture (configurable) |
| Global | `F2` | Start / stop region recording (configurable) |
| Selecting | Mouse wheel | Switch detection level (UI element ⇄ window) |
| Annotating | `Ctrl+Z` / `Ctrl+Y` | Undo / redo (configurable) |
| Annotating | `Ctrl+C` | Copy to clipboard (configurable) |
| Annotating | `Ctrl+S` / `Ctrl+Shift+S` | Save as / quick save (configurable) |
| Annotating | `Delete` | Delete the selected annotation |
| Annotating | `Shift` + drag (line) | Snap to 45° angles |
| Annotating | `Enter` or click outside | Commit the text box (`Shift+Enter` for a new line) |
| Annotating | `Esc` | Deselect → reselect region → close |
| Recording editor | `Space` | Play / pause |
| Recording editor | `←` / `→` | Frame step |
| Recording editor | `Home` / `End` | Jump to trim start / end |
| Recording editor | `I` / `O` | Set trim in / out at the playhead |
| Pinned window | `Ctrl+C` / `Esc` | Copy / close |

All settings (hotkeys, tool defaults, theme, run at startup, recording format / frame rate, and so on) can be adjusted from the tray menu → "Settings…" window.

> Building from source: run `scripts/fetch-ffmpeg.ps1` once to download the bundled `ffmpeg.exe` (CI does this automatically on release).
