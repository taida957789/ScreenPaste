![ScreenPaste](screenshots/banner.png)

<a href='https://ko-fi.com/M6T122R1AH' target='_blank'><img height='36' style='border:0px;height:36px;' src='https://storage.ko-fi.com/cdn/kofi6.png?v=6' border='0' alt='Buy Me a Coffee at ko-fi.com' /></a>

[繁體中文](README.md) | [English](README.en.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | **Français** | [Deutsch](README.de.md) | [Español](README.es.md)

# ScreenPaste

Un outil léger de capture d'écran et d'annotation pour Windows : capturez d'une pression, annotez instantanément et épinglez à l'écran.

## Fonctionnalités

### Capture
- L'écran entier (prise en charge multi-écran)
- **Détection automatique des fenêtres** : survolez une fenêtre pour en tracer automatiquement le contour, puis cliquez pour capturer
- Sélectionnez une zone personnalisée par glisser-déposer, avec l'affichage de la taille et une loupe pour des bords au pixel près

### Annotation
Après la capture, une **barre d'outils à icônes** apparaît près du curseur (déplaçable par glissement, et elle évite automatiquement les bords de l'écran) :

- **Marqueur** — traits pleins, épaisseur / couleur / opacité réglables
- **Surligneur** — couleur superposée semi-transparente, épaisseur / couleur / opacité réglables
- **Texte** — choix de la police / taille / couleur / style (normal, gras, italique, barré)
- **Formes** — rectangle / rectangle arrondi / ellipse, contour ou rempli, épaisseur et couleur du trait réglables
- **Autocollants** — collez des images PNG / JPEG / WebP, déplacez par glissement, redimensionnez à la molette
- **Flou** — flou gaussien / mosaïque, intensité réglable
- **Sélecteur de couleur** — prend en charge la saisie Hex, le RVB et le réglage de l'opacité
- **Annuler / Rétablir**

### Sortie
- Copier dans le presse-papiers
- Enregistrer en PNG / JPG (enregistrer sous, ou enregistrement rapide dans un dossier par défaut)
- **Épingler à l'écran** : l'image flotte au premier plan ; déplacez-la, redimensionnez à la molette, menu par clic droit ; avec plusieurs épingles, Échap ferme d'abord celle qui a le focus, ou les ferme toutes si aucune n'a le focus

### Divers
- Raccourci global (**F1** par défaut) et lancement depuis l'icône de la **zone de notification**
- **Multilingue** : chinois traditionnel / English / 日本語 / 한국어 / Français / Deutsch / Español (suit le système par défaut)
- Thème **Clair / Sombre / Suivre le système**
- Une **fenêtre de paramètres** centralisée : langue, raccourci, thème, lancement au démarrage, dossier d'enregistrement (le champ du raccourci se configure simplement en appuyant sur la combinaison de touches)
- **Lancement automatique au démarrage** en option
- **Mise à jour automatique** : recherche de nouvelles versions via GitHub Releases (activable dans les paramètres, ou vérification manuelle à la demande) ; en cas de mise à jour, elle vous le demande puis la télécharge et l'installe en un clic

## Installation

Téléchargez depuis les [Releases](https://github.com/taida957789/ScreenPaste/releases) :

- **`ScreenPaste-<version>-setup.exe`** — version avec installateur (aucun droit administrateur requis, s'installe dans `%APPDATA%\ScreenPaste`, avec un raccourci dans le menu Démarrer et un désinstalleur)
- **`ScreenPaste-<version>-win-x64-portable.zip`** — version portable, sans installation

Les deux sont autonomes, donc **aucune installation séparée du .NET Runtime n'est nécessaire**.

## Démarrage rapide

![Barre d'outils](screenshots/ui.png)

1. Une fois lancé, il reste dans la zone de notification ; appuyez sur **F1** (ou double-cliquez sur l'icône) pour démarrer une capture.
2. Survolez une fenêtre pour en tracer automatiquement le contour et cliquez pour capturer, ou glissez pour sélectionner une zone personnalisée.
3. Choisissez un outil dans la barre d'outils qui apparaît, réglez ses paramètres et annotez sur la sélection.
4. Appuyez sur **Copier / Enregistrer / Épingler** pour la sortie ; `Échap` revient à une nouvelle sélection ou annule.

Tous les paramètres (raccourci, valeurs par défaut des outils, thème, lancement au démarrage, etc.) se règlent depuis le menu de la zone de notification → fenêtre « Paramètres… ».
