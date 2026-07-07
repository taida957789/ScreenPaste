![ScreenPaste](screenshots/banner.png)

<a href='https://ko-fi.com/M6T122R1AH' target='_blank'><img height='36' style='border:0px;height:36px;' src='https://storage.ko-fi.com/cdn/kofi6.png?v=6' border='0' alt='Buy Me a Coffee at ko-fi.com' /></a>

[繁體中文](README.md) | **English** | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Español](README.es.md)

# ScreenPaste

A lightweight Windows screenshot and annotation tool: capture in one press, annotate instantly, and pin to your screen.

## Features

### Capture
- The whole screen (multi-monitor support)
- **Automatic window detection**: hover over a window to outline it automatically, then click to capture
- Drag to select a custom region, with a size readout and a magnifier for pixel-perfect edges

### Annotate
After capturing, an **icon toolbar** pops up next to the cursor (draggable, and it steers clear of screen edges):

- **Marker** — solid strokes with adjustable thickness / color / opacity
- **Highlighter** — semi-transparent overlay color with adjustable thickness / color / opacity
- **Text** — choose font / size / color / style (normal, bold, italic, strikethrough)
- **Shapes** — rectangle / rounded rectangle / ellipse, outlined or filled, with adjustable line thickness and color
- **Stickers** — paste PNG / JPEG / WebP images, drag to move, scroll to resize
- **Blur** — Gaussian blur / mosaic with adjustable strength
- **Color picker** — supports Hex input, RGB, and opacity adjustment
- **Undo / Redo**

### Output
- Copy to clipboard
- Save as PNG / JPG (save as, or quick-save to a default folder)
- **Pin to screen**: the image floats on top; drag it, scroll to resize, right-click for a menu; with multiple pins, Esc closes the focused one first, or closes them all when none is focused

### More
- Global hotkey (**F1** by default) and launch from the **system tray** icon
- **Multiple languages**: Traditional Chinese / English / 日本語 / 한국어 / Français / Deutsch / Español (follows the system by default)
- **Light / Dark / Follow system** theme
- A centralized **Settings window**: language, hotkey, theme, run at startup, save folder (the hotkey field can be set by simply pressing the key combination)
- Optional **run automatically at startup**
- **Auto-update**: checks for new versions via GitHub Releases (toggle in settings, or check manually on demand); when an update is available it asks and then downloads and installs it in one click

## Install

Download from [Releases](https://github.com/taida957789/ScreenPaste/releases):

- **`ScreenPaste-<version>-setup.exe`** — installer (no administrator rights required, installs to `%APPDATA%\ScreenPaste`, includes a Start menu shortcut and an uninstaller)
- **`ScreenPaste-<version>-win-x64-portable.zip`** — portable, no-install version

Both are self-contained, so **no separate .NET Runtime is required**.

## Quick start

![Toolbar](screenshots/ui.png)

1. Once launched it stays in the system tray; press **F1** (or double-click the tray icon) to start a capture.
2. Hover over a window to auto-outline it and click to capture, or drag to select a custom region.
3. Pick a tool from the pop-up toolbar, adjust its parameters, and annotate on the selection.
4. Press **Copy / Save / Pin** to output; `Esc` goes back to reselect or cancels.

All settings (hotkey, tool defaults, theme, run at startup, and so on) can be adjusted from the tray menu → "Settings…" window.
