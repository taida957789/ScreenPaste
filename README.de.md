![ScreenPaste](screenshots/banner.png)

<a href='https://ko-fi.com/M6T122R1AH' target='_blank'><img height='36' style='border:0px;height:36px;' src='https://storage.ko-fi.com/cdn/kofi6.png?v=6' border='0' alt='Buy Me a Coffee at ko-fi.com' /></a>

[繁體中文](README.md) | [English](README.en.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | **Deutsch** | [Español](README.es.md)

# ScreenPaste

Ein schlankes Screenshot- und Annotationswerkzeug für Windows: mit einem Tastendruck aufnehmen, sofort kommentieren und am Bildschirm anheften.

## Funktionen

### Aufnahme
- Der ganze Bildschirm (Multi-Monitor-Unterstützung)
- **Automatische Fenstererkennung**: Fahren Sie mit der Maus über ein Fenster, um es automatisch zu umranden, und klicken Sie zum Aufnehmen
- Ziehen Sie einen benutzerdefinierten Bereich auf – mit Größenanzeige und einer Lupe für pixelgenaue Kanten

### Annotation
Nach der Aufnahme erscheint neben dem Cursor eine **Symbol-Werkzeugleiste** (per Ziehen verschiebbar und weicht Bildschirmrändern automatisch aus):

- **Marker** — deckende Striche, Stärke / Farbe / Deckkraft einstellbar
- **Textmarker** — halbtransparente Überlagerungsfarbe, Stärke / Farbe / Deckkraft einstellbar
- **Text** — Schriftart / Größe / Farbe / Stil (normal, fett, kursiv, durchgestrichen) wählbar
- **Formen** — Rechteck / abgerundetes Rechteck / Ellipse, umrandet oder gefüllt, Linienstärke und Farbe einstellbar
- **Sticker** — fügen Sie PNG-/JPEG-/WebP-Bilder ein, per Ziehen verschieben, mit dem Rad skalieren
- **Weichzeichnen** — Gaußscher Weichzeichner / Mosaik, Stärke einstellbar
- **Farbwähler** — unterstützt Hex-Eingabe, RGB und Deckkraft-Anpassung
- **Rückgängig / Wiederherstellen**

### Ausgabe
- In die Zwischenablage kopieren
- Als PNG / JPG speichern (Speichern unter oder Schnellspeichern in einen Standardordner)
- **Am Bildschirm anheften**: Das Bild schwebt im Vordergrund; verschieben per Ziehen, skalieren per Rad, Rechtsklick-Menü; bei mehreren Anheftungen schließt Esc zuerst die fokussierte, oder alle, wenn keine fokussiert ist

### Mehr
- Globaler Hotkey (standardmäßig **F1**) und Start über das **Infobereich**-Symbol
- **Mehrsprachig**: Traditionelles Chinesisch / English / 日本語 / 한국어 / Français / Deutsch / Español (folgt standardmäßig dem System)
- Design **Hell / Dunkel / Dem System folgen**
- Ein zentrales **Einstellungsfenster**: Sprache, Hotkey, Design, Autostart, Speicherordner (das Hotkey-Feld lässt sich durch einfaches Drücken der Tastenkombination festlegen)
- Optionaler **automatischer Start beim Systemstart**
- **Automatische Aktualisierung**: sucht über GitHub Releases nach neuen Versionen (in den Einstellungen umschaltbar oder manuell bei Bedarf); ist ein Update verfügbar, fragt es nach und lädt es dann mit einem Klick herunter und installiert es

## Installation

Laden Sie von den [Releases](https://github.com/taida957789/ScreenPaste/releases) herunter:

- **`ScreenPaste-<Version>-setup.exe`** — Installationsversion (keine Administratorrechte erforderlich, installiert nach `%APPDATA%\ScreenPaste`, mit Startmenü-Verknüpfung und Deinstallationsprogramm)
- **`ScreenPaste-<Version>-win-x64-portable.zip`** — portable Version ohne Installation

Beide sind eigenständig, daher **ist keine separate Installation der .NET Runtime erforderlich**.

## Schnellstart

![Werkzeugleiste](screenshots/ui.png)

1. Nach dem Start bleibt es im Infobereich; drücken Sie **F1** (oder doppelklicken Sie auf das Symbol), um eine Aufnahme zu starten.
2. Fahren Sie über ein Fenster, um es automatisch zu umranden, und klicken Sie zum Aufnehmen, oder ziehen Sie, um einen benutzerdefinierten Bereich auszuwählen.
3. Wählen Sie ein Werkzeug aus der eingeblendeten Werkzeugleiste, passen Sie seine Parameter an und kommentieren Sie die Auswahl.
4. Drücken Sie **Kopieren / Speichern / Anheften** zur Ausgabe; mit `Esc` gelangen Sie zur erneuten Auswahl zurück oder brechen ab.

Alle Einstellungen (Hotkey, Werkzeug-Standardwerte, Design, Autostart usw.) lassen sich über das Infobereich-Menü → Fenster „Einstellungen…“ anpassen.
