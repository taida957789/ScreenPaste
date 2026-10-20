![ScreenPaste](screenshots/banner.png)

<a href='https://ko-fi.com/M6T122R1AH' target='_blank'><img height='36' style='border:0px;height:36px;' src='https://storage.ko-fi.com/cdn/kofi6.png?v=6' border='0' alt='Buy Me a Coffee at ko-fi.com' /></a>

[繁體中文](README.md) | **English** | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | [Español](README.es.md)

# ScreenPaste

A lightweight Windows screenshot, annotation and screen-recording tool: capture in one press, annotate instantly, pin to your screen, and record any region to GIF / MP4 / WebP.

## Features

### Capture
- The whole screen (multi-monitor support)
- **Automatic window & UI-element detection**: hover over a window or an interface element (buttons, panels, web-page blocks) to outline it, then click to capture; scroll the **mouse wheel** to switch the detection level (element ⇄ window)
- Drag to select a custom region, with a size readout; a **pixel magnifier** follows the cursor throughout (framing and editing) with a crosshair, coordinates and the pixel color — `C` copies the color value, a `Shift` tap cycles HEX / RGB / HSV
- **Adjustable after selecting**: drag the corner and edge-midpoint handles to resize; with no tool active, drag inside the region to move it — existing annotations stay pinned to the content

### Annotate
After capturing, an **icon toolbar** pops up next to the cursor (draggable, and it steers clear of screen edges):

- **Marker** — solid strokes with adjustable thickness / color / opacity
- **Highlighter** — semi-transparent overlay color with adjustable thickness / color / opacity
- **Text** — choose font / size / color / style (normal, bold, italic, strikethrough); clicking outside the box commits it
- **Shapes** — rectangle / rounded rectangle / ellipse, outlined or filled, with adjustable line thickness and color
- **Line / arrow** — drag to draw; each end can be toggled to an arrowhead; adjustable thickness and color; hold `Shift` to snap to 45° angles
- **Stickers** — paste PNG / JPEG / WebP images, drag to move, scroll to resize
- **Blur** — Gaussian blur / mosaic with adjustable strength; painted above every annotation, so a region hides the strokes and shapes under it, and it can be dragged to a new spot (it re-samples whatever it now covers); the type (Gaussian / mosaic) has a default you can set in Settings, and switching it on the toolbar is remembered for next time
- **Magnifier** — drag to frame the area to enlarge and an enlarged view is placed next to it: rectangle / rounded / circle, with adjustable zoom (1.2–10×), border thickness and colour, connector, shadow, smoothing (off gives the hard pixel grid), and whether the enlarged content includes other annotations; both the view **and the framed source can be dragged at any time** — moving the frame changes what is enlarged and the connector re-aims itself; selecting a magnifier puts **8 resize handles on the framed source** (the enlarged view rescales with it), and the wheel re-zooms it
- **Direct select / move** — hover any placed annotation and **drag it right away**; `Delete` removes it; moves and deletes are undoable
- **Color picker** — Hex input, RGB and opacity (translucent colors preview over a checkerboard); custom colors are remembered across sessions, right-click a swatch to remove it
- **Undo / Redo** — via buttons and hotkeys; every slider shows a live numeric readout

### Output
- Copy to clipboard
- Save as PNG / JPG (save as, or quick-save to a default folder)
- **Pin to screen**: the image floats on top; drag it, scroll to zoom around the cursor, right-click for a menu; a double-click, `Esc` or `Ctrl+W` closes it and `F3` can bring it back (`Shift+Esc` destroys it); with multiple pins, Esc closes the focused one first, or closes them all when none is focused. `F3` also pins whatever the clipboard holds

### Region recording
- A dedicated global hotkey (**F2** by default): drag-select the region to record, or **click a window to auto-detect it** (wheel cycles window/element levels, just like capture), press again (or click Stop) to finish
- A red frame marks the region while recording, with a small pill bar showing the timer and a Stop button
- After recording, an **editor** opens: looping preview, timeline trim handles (`Space` to play, `←`/`→` frame stepping, `I`/`O` to set trim in/out), and export with a progress bar
- **Drag the red frame's border to move the recording region mid-recording** — the interior stays fully interactive
- The editor has the same **annotation tools** as captures (text / shapes / line-arrow / stickers / gaussian blur / mosaic, with full option rows; annotations drag directly, `Delete` removes); `Ctrl+C` quick-exports and puts the file on the clipboard
- Export as **GIF / MP4 / WebP**, switchable right in the editor; a setting can skip the editor and save immediately
- Optional mouse-cursor capture, frame rate 10–30 fps
- **Audio**: system sound (loopback) / microphone / both mixed, selectable in settings; sound is saved only in **MP4** (GIF and WebP are silent). With no usable audio device (common over remote desktop) it falls back to a silent recording
- Encoded by the bundled `ffmpeg` — no separate install required

### More
- Global hotkeys (**F1** capture, **Ctrl+F1** capture and copy, **Shift+F1** custom snip, **F2** recording, **F3** paste / bring back a pin, **Shift+F3** hide / show pins, all configurable) and launch from the **system tray** icon
- **Multiple languages**: Traditional Chinese / English / 日本語 / 한국어 / Français / Deutsch / Español (follows the system by default)
- **Light / Dark / Follow system** theme, with a modern rounded UI and dark title bars
- A centralized **Settings window**: language, hotkeys, theme, run at startup, save folder, annotation defaults (blur type), recording format / frame rate (hotkey fields are set by simply pressing the combination)
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
4. Press **Copy / Save / Pin** to output; `Esc` leaves the capture (asks first, with a don't-ask-again option).

Recording: press **F2** and drag-select a region to start; press **F2** again (or click Stop) to finish, then preview, trim and export as GIF/MP4/WebP in the editor (a setting can save immediately instead).

## Hotkeys

The shortcuts follow Snipaste's wherever it has one. Keys marked *(configurable)* can be changed in Settings → "Hotkeys" / "In-app shortcuts"; the others are fixed.

> **Changed in this version**
> - A **double-click on a pin closes it** (the first time it asks first). Settings → "Pins" can make it toggle the thumbnail or do nothing instead.
> - `Esc` / `Ctrl+W` close a pin **recoverably**: `F3`, pressed while the clipboard is unchanged since the last paste, brings it back where it was. `Shift+Esc` **destroys** it for good.
> - The mouse wheel zooms a pin **around the cursor** (Settings → "Pins" can keep the top-left corner fixed instead).
> - The loupe's colour format (HEX → RGB → HSV) switches on a **`Shift` tap**, pressed and released on its own, so `Shift`+drag and `Shift`+click no longer change it.

### Global

| Keys | Action |
|---|---|
| `F1` | Start a capture (configurable) |
| `Ctrl+F1` | Capture and copy: the chosen area goes straight to the clipboard (configurable) |
| `Shift+F1` | Custom snip: set the area and a delay in a dialog (configurable) |
| `F2` | Start / stop region recording (configurable) |
| `F3` | Pin the clipboard (image, image file, colour value or text) at the cursor; pressed again while the clipboard is unchanged, bring back the pin closed last (configurable) |
| `Shift+F3` | Hide / show all pins (configurable) |
| Unassigned | Pin click-through (the pin under the cursor lets clicks through), snip and pin, snip / copy / pin, copy the last area again, copy the whole screen, snip the active window, snip the screen under the cursor, delayed snip, clear snip history: assign them in Settings → "Hotkeys" / "More hotkeys" |
| Tray icon | Double-click starts a capture; the menu also has custom snip, paste from clipboard, hide / show pins and "Make all pins clickable" |

### Capture: selecting the area

| Keys | Action |
|---|---|
| Click / drag | Take the highlighted window or UI element / frame a custom area |
| Mouse wheel | Switch detection level (UI element ⇄ window) |
| `Tab` | Switch between UI-element and window detection |
| `Space` | Take the highlighted area |
| `Ctrl+A` | Take the screen under the cursor, then all screens |
| `Shift` + drag | Frame a square |
| `Space` held while dragging | Move the area instead of resizing it |
| `W` `A` `S` `D` / arrow keys | Move the cursor one pixel (faster when held); a drag follows it |
| `Alt` tap | Show / hide crosshair guide lines (remembered) |
| `C` | Copy the magnified pixel's colour value |
| `Shift` tap | Cycle the colour readout HEX → RGB → HSV |
| `R` / `Shift+R` | Step back / forward through the areas of earlier snips |
| `,` / `.` | Step back / forward through the snip history (earlier snips, their annotations flattened; kept in memory only) |
| `` ` `` or `!` | Show / hide the mouse pointer in the capture (remembered) |
| `F5` | Capture the screen again from under the overlay |
| Right-click | Drop the area being framed; with none, leave the capture |
| `Esc` | Leave the capture |

### Capture: editing and annotating

| Keys | Action |
|---|---|
| Drag a handle | Resize the area (`Ctrl`: about its centre; `Space` held: move it instead) |
| Drag inside the area (no tool) | Move the area (`Shift`: along one axis) |
| Arrow keys | Move the area one pixel, or the selected annotation |
| `Ctrl` / `Shift` + arrow keys | Grow / shrink the area on that side |
| `Ctrl+A` | Area = the screen under the cursor, then all screens |
| `W` `A` `S` `D` | Move the cursor one pixel (also while drawing) |
| `C` / `Shift` tap | Copy the loupe's colour / cycle its format, as while selecting |
| `R` / `Shift+R`, `,` / `.`, `` ` `` / `!`, `F5` | As while selecting; the annotations stay with the content (`,` / `.` ask first) |
| `Space` | Show / hide the toolbar |
| `Ctrl+1` … `Ctrl+9`, `Ctrl+0` | Shape, line / arrow, marker, highlighter, blur / mosaic, text, number, magnifier, eraser, sticker |
| `T` / `B` / `M` / `E` | Text / marker / blur / eraser |
| `Ctrl+Tab` / `Ctrl+Shift+Tab` | Next / previous tool |
| `Tab` | Switch the tool's variant: rectangle → rounded → ellipse, line ⇄ arrow, Gaussian ⇄ mosaic, the magnifier's shape |
| `1` / `2`, `[` / `]`, mouse wheel | Thinner / thicker (font size with the text tool) |
| `Alt+C` | Pick the colour under the cursor |
| `+` / `-` | Number tool: next number up / down |
| Wheel on a colour swatch | Step the tool's opacity |
| `Shift` + drag while drawing | Square shapes, blur and magnifier frames; straight pen strokes; lines at 45° |
| Click (line tool) | Start a line strip: each click adds a point; right-click, `Esc`, `Enter` or a double-click finishes it |
| Drag an annotation | Move it (`Shift`: along one axis) |
| `Ctrl` + drag an annotation | Drag a copy off it |
| `Shift` + wheel | Scale the selected rectangle / ellipse |
| Wheel on a selected magnifier | Change its zoom |
| `Delete` | Delete the selected annotation |
| `Ctrl+Shift+C` / `Ctrl+Shift+V` | Copy the selected annotation / paste a copy of it |
| `Ctrl+Z` / `Ctrl+Y` | Undo / redo (configurable) |
| `Ctrl+Shift+Z` | Remove all annotations, as one undo step (configurable) |
| `Enter` or click outside | Commit the text box (`Shift+Enter` for a new line; `Esc` commits too) |
| Wheel over the text box | Font size |
| Middle-click in the text box | Paste the clipboard's text |
| `Ctrl+C` / `Ctrl+Insert` | Copy to clipboard (`Ctrl+C` configurable) |
| `Ctrl+S` / `Ctrl+Shift+S` | Save as / quick save (configurable; `Shift`+click on Save quick-saves too) |
| `Ctrl+T` | Pin to screen (configurable) |
| `Ctrl+P` | Print (configurable) |
| `Ctrl+Alt+C` | Copy as a PNG file |
| `V` | Copy the area's size as text (`800x600`) |
| `Enter` | Copy and close (configurable in Settings → "Enter & mouse actions") |
| Double-click in the area (no tool) | Copy and close (configurable) |
| Middle-click in the area | Pin to screen (configurable) |
| Right-click | Step back one level: finish the line strip or the text; with the shape, blur or eraser tool, mark the UI element under the cursor (outside the area: all of it); deselect; drop the tool; then discard the area to frame a new one (asks first when annotations would be lost) |
| `Esc` | Cancel a magnifier-frame drag → finish the line strip → deselect → leave the capture (asks first; can be disabled) |

### Pins

| Keys | Action |
|---|---|
| Drag | Move the pin (`W` `A` `S` `D` while held: one pixel) |
| `Shift` + drag | Snap its edges to other pins, windows and screen edges |
| Drag the border | Resize, keeping the shape |
| Mouse wheel | Zoom around the cursor (Settings → "Pins" can keep the top-left corner fixed instead) |
| `Ctrl` + wheel | Opacity |
| Middle-click | Reset to 100% size and opacity; again to go back |
| Double-click | Close, recoverably (asks the first time; Settings → "Pins" or the pin menu can change it) |
| `Shift` + double-click | Thumbnail on / off |
| Right-drag | Shrink the pin to the part dragged over (a thumbnail) |
| Right-click / `Menu` key | Pin menu |
| `Ctrl` + click | Add the pin to the selection, or take it out; selected pins move, fade and close together |
| `Esc` / `Ctrl+W` | Close, recoverably (`F3` brings it back) |
| `Shift+Esc` | Destroy: it cannot be brought back |
| `Esc` with no pin focused | Close every shown pin, recoverably |
| `1` / `2` | Rotate clockwise / counter-clockwise |
| `3` / `4` | Flip horizontally / vertically |
| `5` / `6` | Grayscale / invert colours |
| `+` / `-` | Zoom in / out |
| `Ctrl++` / `Ctrl+-` | Opacity up / down |
| Arrow keys | Move one pixel (faster when held) |
| `Ctrl+C` / `Ctrl+Insert` | Copy (`Ctrl+C` configurable) |
| `Ctrl+Alt+C` | Copy as a PNG file |
| `Ctrl+S` / `Ctrl+Shift+S` | Save as / quick save (configurable) |
| `Ctrl+P` | Print (configurable) |
| `Ctrl+V` | Show the clipboard's content in this pin instead |
| `Ctrl+Shift+C` | Copy the text a text pin was made from |
| `Ctrl+A` | Select every shown pin |
| `Ctrl+Shift+P` | Open Settings |
| `Alt` tap | Loupe on / off: `C` copies the colour under the cursor, a `Shift` tap cycles HEX → RGB → HSV, `W` `A` `S` `D` move the cursor one pixel |
| Drop an image file | Replace the pin's image (`Shift`: open it as a new pin) |

### Recording editor

| Keys | Action |
|---|---|
| `Space` | Play / pause |
| `←` / `→` | Frame step |
| `Home` / `End` | Jump to trim start / end |
| `I` / `O` | Set trim in / out at the playhead |
| `Ctrl+C` | Quick-export and copy the file to the clipboard |

All settings (hotkeys, tool defaults, theme, run at startup, recording format / frame rate, and so on) can be adjusted from the tray menu → "Settings…" window.

> Building from source: run `scripts/fetch-ffmpeg.ps1` once to download the bundled `ffmpeg.exe` (CI does this automatically on release).
