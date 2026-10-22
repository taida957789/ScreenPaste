![ScreenPaste](screenshots/banner.png)

<a href='https://ko-fi.com/M6T122R1AH' target='_blank'><img height='36' style='border:0px;height:36px;' src='https://storage.ko-fi.com/cdn/kofi6.png?v=6' border='0' alt='Buy Me a Coffee at ko-fi.com' /></a>

[繁體中文](README.md) | [English](README.en.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | **Deutsch** | [Español](README.es.md)

# ScreenPaste

Ein leichtgewichtiges Windows-Tool für Screenshots, Anmerkungen und Bildschirmaufnahmen: mit einem Tastendruck aufnehmen, sofort beschriften, am Bildschirm anheften und beliebige Bereiche als GIF / MP4 / WebP aufzeichnen.

## Funktionen

### Aufnahme
- Der gesamte Bildschirm (Multi-Monitor-Unterstützung)
- **Automatische Fenster- und UI-Element-Erkennung**: Fahren Sie über ein Fenster oder ein Interface-Element (Schaltflächen, Panels, Webseiten-Blöcke), um es automatisch zu umranden, und klicken Sie zum Aufnehmen; das **Mausrad** wechselt die Erkennungsebene (Element ⇄ Fenster)
- Ziehen zum Auswählen eines eigenen Bereichs, mit Größenanzeige; eine **Pixel-Lupe** folgt dem Cursor durchgehend (Auswahl- und Bearbeitungsmodus) mit Fadenkreuz, Koordinaten und Pixelfarbe — `C` kopiert den Farbwert, kurzes Antippen von `Umschalt` schaltet HEX / RGB / HSV durch
- **Nach der Auswahl anpassbar**: Ziehen Sie die Griffe an Ecken und Kantenmitten zum Ändern der Größe; ohne aktives Werkzeug verschiebt Ziehen im Inneren den Bereich — vorhandene Anmerkungen bleiben am Inhalt verankert

### Anmerkungen
Nach der Aufnahme erscheint neben dem Cursor eine **Symbol-Werkzeugleiste** (verschiebbar, weicht Bildschirmrändern automatisch aus):

- **Marker** — deckende Striche; Stärke / Farbe / Deckkraft einstellbar
- **Textmarker** — halbtransparente Überlagerung; Stärke / Farbe / Deckkraft einstellbar
- **Text** — Schriftart / Größe / Farbe / Stil (normal, fett, kursiv, durchgestrichen); ein Klick außerhalb des Felds bestätigt den Text
- **Formen** — Rechteck / abgerundetes Rechteck / Ellipse, Umriss oder gefüllt, Linienstärke und Farbe einstellbar
- **Linie / Pfeil** — zum Zeichnen ziehen; jedes Ende kann einzeln eine Pfeilspitze erhalten; Stärke und Farbe einstellbar; mit `Umschalt` an 45°-Winkeln einrasten
- **Eingefügte Bilder** — PNG / JPEG / WebP einfügen, ziehen zum Verschieben, Mausrad zum Skalieren
- **Weichzeichnen** — Gaußscher Weichzeichner / Mosaik, Stärke einstellbar; liegt über allen Anmerkungen und verdeckt damit auch Striche und Formen darunter, und lässt sich frei verschieben (der Bereich tastet danach die neue Stelle ab); die Art (Gauß / Mosaik) hat einen in den Einstellungen wählbaren Standard, und ein Wechsel in der Werkzeugleiste wird für das nächste Mal gemerkt
- **Lupe** — den zu vergrößernden Bereich aufziehen; die vergrößerte Ansicht wird direkt daneben platziert: Rechteck / abgerundet / Kreis, einstellbarer Zoom (1,2–10×), Rahmenstärke und -farbe, Verbindungslinie, Schatten, Glätten (aus zeigt das harte Pixelraster) sowie die Wahl, ob andere Anmerkungen mit vergrößert werden; die Ansicht **und der Rahmen des Quellbereichs lassen sich jederzeit verschieben** — den Rahmen zu verschieben ändert das Vergrößerte, die Verbindungslinie richtet sich neu aus; eine ausgewählte Lupe zeigt **8 Anfasser am Rahmen zum Ändern der Größe** (die vergrößerte Ansicht skaliert mit), das Mausrad ändert den Zoom
- **Direktes Auswählen / Verschieben** — fahren Sie über eine platzierte Anmerkung und **ziehen Sie sie direkt**; `Entf` löscht sie; Verschieben und Löschen sind rückgängig machbar
- **Farbwähler** — Hex-Eingabe, RGB und Deckkraft (transluzente Farben werden über einem Schachbrett angezeigt); eigene Farben werden sitzungsübergreifend gespeichert, Rechtsklick auf ein Farbfeld entfernt es
- **Rückgängig / Wiederholen** — per Schaltfläche und Tastenkürzel; jeder Schieberegler zeigt den aktuellen Wert an

### Ausgabe
- In die Zwischenablage kopieren
- Als PNG / JPG speichern (Speichern unter oder Schnellspeichern in einen Standardordner)
- **Am Bildschirm anheften**: Das Bild schwebt im Vordergrund; ziehen zum Verschieben, Mausrad zum Zoomen um den Mauszeiger, Rechtsklick für das Menü; Doppelklick, `Esc` oder `Strg+W` schließen es, `F3` kann es zurückholen (`Umschalt+Esc` vernichtet es); bei mehreren Pins schließt Esc zuerst den fokussierten, ohne Fokus alle. `F3` heftet auch den Inhalt der Zwischenablage an

### Bereichsaufnahme
- Ein eigener globaler Hotkey (standardmäßig **F2**): Bereich per Ziehen auswählen oder **ein Fenster anklicken zur automatischen Erkennung** (Rad wechselt Fenster-/Element-Ebenen, wie beim Screenshot), erneut drücken (oder Stopp klicken) zum Beenden
- Während der Aufnahme markiert ein roter Rahmen den Bereich, dazu eine kleine Leiste mit Timer und Stopp-Schaltfläche
- Nach der Aufnahme öffnet sich ein **Editor**: Schleifenvorschau, Trimm-Griffe auf der Zeitleiste (`Leertaste` Wiedergabe, `←`/`→` Einzelbild, `I`/`O` für Start-/Endpunkt), Export mit Fortschrittsbalken
- **Ziehen Sie den Rand des roten Rahmens, um den Bereich während der Aufnahme zu verschieben** — das Innere bleibt voll bedienbar
- Der Editor hat dieselben **Anmerkungswerkzeuge** wie Aufnahmen (Text / Formen / Linie-Pfeil / Bilder / Gauß-Weichzeichner / Mosaik, mit Optionszeilen; Anmerkungen lassen sich direkt ziehen, `Entf` löscht); `Strg+C` exportiert schnell und legt die Datei in die Zwischenablage
- Export als **GIF / MP4 / WebP**, direkt im Editor umschaltbar; eine Einstellung überspringt den Editor und speichert sofort
- Optionale Mauszeiger-Aufnahme, Bildrate 10–30 fps
- **Audio**: Systemklang (Loopback) / Mikrofon / beides gemischt, in den Einstellungen wählbar; Ton wird nur in **MP4** gespeichert (GIF und WebP sind stumm). Ohne nutzbares Audiogerät (bei Remotedesktop häufig) wird ohne Ton aufgenommen
- Kodierung durch das mitgelieferte `ffmpeg` — keine separate Installation nötig

### Sonstiges
- Globale Hotkeys (**F1** Aufnahme, **Strg+F1** aufnehmen und kopieren, **Umschalt+F1** benutzerdefinierte Aufnahme, **F2** Aufzeichnung, **F3** einfügen / Bild zurückholen, **Umschalt+F3** angeheftete Bilder aus-/einblenden, alle konfigurierbar) und Start über das **Infobereich**-Symbol
- **Mehrsprachig**: 繁體中文 / English / 日本語 / 한국어 / Français / Deutsch / Español (folgt standardmäßig dem System)
- **Hell / Dunkel / System**-Design, moderne abgerundete Oberfläche und dunkle Titelleisten
- Zentrales **Einstellungsfenster**: Sprache, Hotkeys, Design, Autostart, Speicherordner, Anmerkungs-Standards (Weichzeichner-Typ), Aufnahmeformat / Bildrate (Hotkey-Felder werden durch einfaches Drücken der Kombination gesetzt; auch jede Taste der Aufnahme und der angehefteten Bilder lässt sich neu belegen)
- Optionaler **Autostart mit Windows**
- **Auto-Update**: prüft über GitHub Releases auf neue Versionen (in den Einstellungen umschaltbar, auch manuell); Download und Installation mit einem Klick, mit Fortschritt und Geschwindigkeit

## Installation

Download unter [Releases](https://github.com/taida957789/ScreenPaste/releases):

- **`ScreenPaste-<version>-setup.exe`** — Installer (keine Administratorrechte nötig, installiert nach `%APPDATA%\ScreenPaste`, mit Startmenü-Verknüpfung und Deinstallation)
- **`ScreenPaste-<version>-win-x64-portable.zip`** — portable Version ohne Installation

Beide sind eigenständig — **kein separates .NET Runtime erforderlich**, und `ffmpeg` (für die Bereichsaufnahme) ist enthalten.

## Schnellstart

![Werkzeugleiste](screenshots/ui.png)

1. Nach dem Start bleibt das Tool im Infobereich; **F1** (oder Doppelklick auf das Symbol) startet eine Aufnahme.
2. Über ein Fenster oder UI-Element fahren, um es zu umranden, und klicken — oder einen Bereich per Ziehen auswählen.
3. Ein Werkzeug aus der Leiste wählen, Parameter anpassen und die Auswahl beschriften.
4. **Kopieren / Speichern / Anheften** zur Ausgabe; `Esc` verlässt die Aufnahme (fragt nach — mit „Nicht mehr fragen“-Option).

Aufzeichnung: **F2** drücken und den Bereich per Ziehen wählen; erneut **F2** (oder Stopp) beendet, danach im Editor prüfen, trimmen und als GIF/MP4/WebP exportieren (per Einstellung auch Sofortspeichern).

## Tastenkürzel

Die Kürzel folgen Snipaste, wo es dort eine Entsprechung gibt; die Tabellen zeigen die Standardbelegung. Globale Hotkeys werden unter Einstellungen → „Tastenkürzel“ geändert; jede Taste der Aufnahme und der angehefteten Bilder lässt sich unter Einstellungen → „Kürzel in der App“ → „Tasten anpassen…“ neu belegen (bis zu zwei Tasten je Aktion, mit Warnung bei Überschneidungen). Fest sind nur die eigenen Bearbeitungstasten des Textfelds und Zusatztasten, die während eines Ziehens gehalten werden (etwa `Umschalt`).

> **Neu in dieser Version**
> - Ein **Doppelklick auf ein angeheftetes Bild schließt es** (beim ersten Mal mit Nachfrage). Unter Einstellungen → „Angeheftete Bilder“ kann er stattdessen die Miniaturansicht umschalten oder nichts tun.
> - `Esc` / `Strg+W` schließen ein angeheftetes Bild **wiederherstellbar**: `F3` holt es an seinen Platz zurück, solange sich die Zwischenablage seit dem letzten Einfügen nicht geändert hat. `Umschalt+Esc` **vernichtet** es endgültig.
> - Das Mausrad zoomt ein angeheftetes Bild **um den Mauszeiger** (unter Einstellungen → „Angeheftete Bilder“ lässt sich die linke obere Ecke festhalten).
> - Das Farbformat der Lupe (HEX → RGB → HSV) wechselt bei einem **kurzen Antippen von `Umschalt`** (allein gedrückt und losgelassen); `Umschalt`+Ziehen und `Umschalt`+Klick ändern es nicht mehr.

### Global

| Tasten | Aktion |
|---|---|
| `F1` | Aufnahme starten (konfigurierbar) |
| `Strg+F1` | Aufnehmen und kopieren: der gewählte Bereich kommt direkt in die Zwischenablage (konfigurierbar) |
| `Umschalt+F1` | Benutzerdefinierte Aufnahme: Bereich und Verzögerung in einem Dialog festlegen (konfigurierbar) |
| `F2` | Bereichsaufnahme starten / stoppen (konfigurierbar) |
| `F3` | Inhalt der Zwischenablage (Bild, Bilddatei, Farbwert oder Text) am Mauszeiger anheften; erneut gedrückt bei unveränderter Zwischenablage das zuletzt geschlossene Bild zurückholen (konfigurierbar) |
| `Umschalt+F3` | Alle angehefteten Bilder aus- / einblenden (konfigurierbar) |
| Nicht belegt | Bild klickdurchlässig (das Bild unter dem Mauszeiger lässt Klicks durch), aufnehmen und anheften, aufnehmen, kopieren und anheften, letzten Bereich erneut kopieren, ganzen Bildschirm kopieren, aktives Fenster aufnehmen, Bildschirm unter dem Mauszeiger aufnehmen, verzögerte Aufnahme, Aufnahmeverlauf löschen: unter Einstellungen → „Tastenkürzel“ / „Weitere Tastenkürzel“ belegen |
| Infobereich-Symbol | Doppelklick startet eine Aufnahme; das Menü bietet außerdem benutzerdefinierte Aufnahme, aus Zwischenablage einfügen, angeheftete Bilder aus-/einblenden, „Alle angehefteten Bilder wieder anklickbar machen“ und „Aufnahme-Hotkey (F1)“, das den Aufnahme-Hotkey aus- und wieder einschaltet, ohne ihn zu vergessen (etwa um F1 für ein Spiel freizugeben) |

### Aufnahme: Bereich auswählen

| Tasten | Aktion |
|---|---|
| Klick / Ziehen | Hervorgehobenes Fenster oder UI-Element aufnehmen / eigenen Bereich aufziehen |
| Mausrad | Erkennungsebene wechseln (UI-Element ⇄ Fenster) |
| `Tab` | Zwischen UI-Element- und Fenstererkennung wechseln |
| `Leertaste` | Hervorgehobenen Bereich übernehmen |
| `Strg+A` | Bildschirm unter dem Mauszeiger, dann alle Bildschirme übernehmen |
| `Umschalt` + Ziehen | Quadrat aufziehen |
| `Leertaste` beim Ziehen gedrückt halten | Bereich verschieben statt die Größe zu ändern |
| `W` `A` `S` `D` / Pfeiltasten | Mauszeiger um ein Pixel bewegen (gehalten schneller); ein Ziehen folgt ihm |
| `Alt` antippen | Fadenkreuz-Hilfslinien ein- / ausblenden (wird gemerkt) |
| `C` | Farbwert des Pixels unter der Lupe kopieren |
| `Umschalt` antippen | Farbanzeige HEX → RGB → HSV durchschalten |
| `R` / `Umschalt+R` | Bereiche früherer Aufnahmen zurück / vor durchgehen |
| `,` / `.` | Aufnahmeverlauf zurück / vor durchgehen (frühere Aufnahmen, Anmerkungen ins Bild übernommen; nur im Speicher) |
| `` ` `` oder `!` | Mauszeiger in der Aufnahme ein- / ausblenden (wird gemerkt) |
| `F5` | Bildschirm unter dem Overlay neu aufnehmen |
| Rechtsklick | Den gerade aufgezogenen Bereich verwerfen; sonst die Aufnahme verlassen |
| `Esc` | Aufnahme verlassen |

### Aufnahme: Bearbeiten und Anmerken

| Tasten | Aktion |
|---|---|
| Griff ziehen | Bereichsgröße ändern (`Strg`: um die Mitte; `Leertaste` gehalten: stattdessen verschieben) |
| Im Bereich ziehen (ohne Werkzeug) | Bereich verschieben (`Umschalt`: nur auf einer Achse) |
| Pfeiltasten | Bereich um ein Pixel verschieben, oder die ausgewählte Anmerkung |
| `Strg` / `Umschalt` + Pfeiltasten | Bereich auf dieser Seite vergrößern / verkleinern |
| `Strg+A` | Bereich = Bildschirm unter dem Mauszeiger, dann alle Bildschirme |
| `W` `A` `S` `D` | Mauszeiger um ein Pixel bewegen (auch beim Zeichnen) |
| `C` / `Umschalt` antippen | Farbe der Lupe kopieren / Format wechseln, wie beim Auswählen |
| `R` / `Umschalt+R`, `,` / `.`, `` ` `` / `!`, `F5` | Wie beim Auswählen; die Anmerkungen bleiben am Bildinhalt (`,` / `.` fragen vorher nach) |
| `Leertaste` | Werkzeugleiste ein- / ausblenden |
| `Strg+1` … `Strg+9`, `Strg+0` | Form, Linie / Pfeil, Marker, Textmarker, Weichzeichnen / Mosaik, Text, Nummernmarke, Lupe, Radierer, Bild einfügen |
| `T` / `B` / `M` / `E` | Text / Marker / Weichzeichnen / Radierer |
| `Strg+Tab` / `Strg+Umschalt+Tab` | Nächstes / vorheriges Werkzeug |
| `Tab` | Variante des Werkzeugs wechseln: Rechteck → abgerundet → Ellipse, Linie ⇄ Pfeil, Gauß ⇄ Mosaik, Form der Lupe |
| `1` / `2`, `[` / `]`, Mausrad | Dünner / dicker (beim Textwerkzeug die Schriftgröße) |
| `Alt+C` | Farbe unter dem Mauszeiger aufnehmen |
| `+` / `-` | Nummernmarke: nächste Nummer höher / niedriger |
| Mausrad auf einem Farbfeld | Deckkraft des Werkzeugs ändern |
| `Umschalt` + Ziehen beim Zeichnen | Quadratische Formen, Weichzeichner- und Lupenrahmen; gerade Stiftstriche; Linien in 45°-Schritten |
| Klick (Linienwerkzeug) | Linienzug beginnen: jeder Klick fügt einen Punkt hinzu; Rechtsklick, `Esc`, `Eingabe` oder Doppelklick beendet ihn |
| Anmerkung ziehen | Verschieben (`Umschalt`: nur auf einer Achse) |
| `Strg` + Anmerkung ziehen | Eine Kopie herausziehen |
| `Umschalt` + Mausrad | Ausgewähltes Rechteck / ausgewählte Ellipse skalieren |
| Mausrad auf einer ausgewählten Lupe | Vergrößerung ändern |
| `Entf` | Ausgewählte Anmerkung löschen |
| `Strg+Umschalt+C` / `Strg+Umschalt+V` | Ausgewählte Anmerkung kopieren / eine Kopie einfügen |
| `Strg+Z` / `Strg+Y` | Rückgängig / wiederholen |
| `Strg+Umschalt+Z` | Alle Anmerkungen entfernen, als ein Rückgängig-Schritt |
| `Eingabe` oder Klick außerhalb | Text bestätigen (`Umschalt+Eingabe` für Zeilenumbruch; `Esc` bestätigt ebenfalls) |
| Mausrad über dem Textfeld | Schriftgröße |
| Mittelklick im Textfeld | Text aus der Zwischenablage einfügen |
| `Strg+C` / `Strg+Einfg` | In die Zwischenablage kopieren |
| `Strg+S` / `Strg+Umschalt+S` | Speichern unter / Schnellspeichern (`Umschalt`+Klick auf Speichern speichert ebenfalls schnell) |
| `Strg+T` | Am Bildschirm anheften |
| `Strg+P` | Drucken |
| `Strg+Alt+C` | Als PNG-Datei kopieren |
| `V` | Bereichsgröße als Text kopieren (`800x600`) |
| `Eingabe` | Kopieren und schließen (konfigurierbar unter Einstellungen → „Eingabetaste & Maus“) |
| Doppelklick im Bereich (ohne Werkzeug) | Kopieren und schließen (konfigurierbar unter Einstellungen → „Eingabetaste & Maus“) |
| Mittelklick im Bereich | Am Bildschirm anheften (konfigurierbar unter Einstellungen → „Eingabetaste & Maus“) |
| Rechtsklick | Eine Ebene zurück: Linienzug oder Text beenden; mit Form-, Weichzeichner- oder Radierer-Werkzeug das UI-Element unter dem Mauszeiger markieren (außerhalb des Bereichs: den ganzen Bereich); Auswahl aufheben; Werkzeug ablegen; zuletzt den Bereich verwerfen und neu aufziehen (mit Nachfrage, wenn Anmerkungen verloren gingen) |
| `Esc` | Ziehen eines Lupenrahmens abbrechen → Linienzug beenden → Auswahl aufheben → Aufnahme verlassen (mit Nachfrage, abschaltbar) |

### Angeheftete Bilder

| Tasten | Aktion |
|---|---|
| Ziehen | Bild verschieben (`W` `A` `S` `D` beim Ziehen: um ein Pixel) |
| `Umschalt` + Ziehen | Kanten an anderen angehefteten Bildern, Fenstern und Bildschirmrändern einrasten |
| Rand ziehen | Größe ändern, Seitenverhältnis bleibt |
| Mausrad | Um den Mauszeiger zoomen (unter Einstellungen → „Angeheftete Bilder“ lässt sich die linke obere Ecke festhalten) |
| `Strg` + Mausrad | Deckkraft |
| Mittelklick | Auf 100 % Größe und Deckkraft zurücksetzen; erneut zum Zurückkehren |
| Doppelklick | Schließen, wiederherstellbar (beim ersten Mal mit Nachfrage; änderbar unter Einstellungen → „Angeheftete Bilder“ oder im Bildmenü) |
| `Umschalt` + Doppelklick | Miniaturansicht ein / aus |
| Mit rechter Taste ziehen | Bild auf den überzogenen Teil verkleinern (eine Miniaturansicht) |
| Rechtsklick / `Menütaste` | Bildmenü |
| `Strg` + Klick | Bild zur Auswahl hinzufügen oder daraus entfernen; ausgewählte Bilder werden gemeinsam verschoben, ausgeblendet und geschlossen |
| `Esc` / `Strg+W` | Schließen, wiederherstellbar (`F3` holt es zurück) |
| `Umschalt+Esc` | Vernichten: es lässt sich nicht mehr zurückholen |
| `Esc` ohne fokussiertes Bild | Alle angezeigten Bilder schließen, wiederherstellbar |
| `1` / `2` | Im / gegen den Uhrzeigersinn drehen |
| `3` / `4` | Horizontal / vertikal spiegeln |
| `5` / `6` | Graustufen / Farben umkehren |
| `+` / `-` | Vergrößern / verkleinern |
| `Strg++` / `Strg+-` | Deckkraft erhöhen / verringern |
| Pfeiltasten | Um ein Pixel verschieben (gehalten schneller) |
| `Strg+C` / `Strg+Einfg` | Kopieren |
| `Strg+Alt+C` | Als PNG-Datei kopieren |
| `Strg+S` / `Strg+Umschalt+S` | Speichern unter / Schnellspeichern |
| `Strg+P` | Drucken |
| `Strg+V` | Stattdessen den Inhalt der Zwischenablage zeigen |
| `Strg+Umschalt+C` | Den Text kopieren, aus dem ein Textbild erstellt wurde |
| `Strg+A` | Alle angezeigten Bilder auswählen |
| `Strg+Umschalt+P` | Einstellungen öffnen |
| `Alt` antippen | Lupe ein / aus: `C` kopiert die Farbe unter dem Mauszeiger, `Umschalt` antippen schaltet HEX → RGB → HSV durch, `W` `A` `S` `D` bewegen den Mauszeiger um ein Pixel |
| Bilddatei ablegen | Bild ersetzen (`Umschalt`: als neues angeheftetes Bild öffnen) |

### Aufnahme-Editor

| Tasten | Aktion |
|---|---|
| `Leertaste` | Wiedergabe / Pause |
| `←` / `→` | Einzelbild vor / zurück |
| `Pos1` / `Ende` | Zum Trimm-Anfang / -Ende springen |
| `I` / `O` | Trimm-Anfang / -Ende an der Wiedergabeposition setzen |
| `Strg+C` | Schnellexport und Datei in die Zwischenablage kopieren |

Alle Einstellungen (Hotkeys, Werkzeug-Standardwerte, Design, Autostart, Aufnahmeformat / Bildrate usw.) lassen sich über das Infobereich-Menü → „Einstellungen…“ anpassen.

> Beim Bauen aus dem Quellcode: einmal `scripts/fetch-ffmpeg.ps1` ausführen, um das mitgelieferte `ffmpeg.exe` herunterzuladen (die CI erledigt das beim Release automatisch).
