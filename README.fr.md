![ScreenPaste](screenshots/banner.png)

<a href='https://ko-fi.com/M6T122R1AH' target='_blank'><img height='36' style='border:0px;height:36px;' src='https://storage.ko-fi.com/cdn/kofi6.png?v=6' border='0' alt='Buy Me a Coffee at ko-fi.com' /></a>

[繁體中文](README.md) | [English](README.en.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | **Français** | [Deutsch](README.de.md) | [Español](README.es.md)

# ScreenPaste

Un outil Windows léger de capture d'écran, d'annotation et d'enregistrement : capturez d'une touche, annotez instantanément, épinglez à l'écran et enregistrez n'importe quelle zone en GIF / MP4 / WebP.

## Fonctionnalités

### Capture
- L'écran entier (multi-écrans pris en charge)
- **Détection automatique des fenêtres et des éléments d'interface** : survolez une fenêtre ou un élément (bouton, panneau, bloc de page web) pour l'encadrer automatiquement, puis cliquez pour capturer ; la **molette** change le niveau de détection (élément ⇄ fenêtre)
- Glissez pour sélectionner une zone personnalisée, avec affichage des dimensions et loupe pour des bords au pixel près
- **Ajustable après la sélection** : faites glisser les poignées des coins et des milieux de côtés pour redimensionner ; sans outil actif, glissez l'intérieur de la zone pour la déplacer — les annotations existantes restent fixées au contenu

### Annotation
Après la capture, une **barre d'outils** apparaît près du curseur (déplaçable, elle évite les bords de l'écran) :

- **Marqueur** — traits pleins ; épaisseur / couleur / opacité réglables
- **Surligneur** — couleur semi-transparente ; épaisseur / couleur / opacité réglables
- **Texte** — police / taille / couleur / style (normal, gras, italique, barré) ; un clic hors de la zone valide le texte
- **Formes** — rectangle / rectangle arrondi / ellipse, contour ou rempli, épaisseur et couleur réglables
- **Ligne / flèche** — glissez pour tracer ; chaque extrémité peut devenir une pointe de flèche ; épaisseur et couleur réglables ; maintenez `Maj` pour aligner sur des angles de 45°
- **Images collées** — collez des PNG / JPEG / WebP, glissez pour déplacer, molette pour redimensionner
- **Flou** — flou gaussien / mosaïque, intensité réglable
- **Sélection / déplacement direct** — survolez n'importe quelle annotation posée et **déplacez-la directement** ; `Suppr` la supprime ; déplacements et suppressions sont annulables
- **Sélecteur de couleurs** — saisie Hex, RGB et opacité (les couleurs translucides s'affichent sur un damier) ; les couleurs personnalisées sont mémorisées d'une session à l'autre, clic droit sur une pastille pour la retirer
- **Annuler / Rétablir** — par boutons et raccourcis ; chaque curseur affiche sa valeur en temps réel

### Sortie
- Copier dans le presse-papiers
- Enregistrer en PNG / JPG (enregistrer sous, ou enregistrement rapide dans un dossier par défaut)
- **Épingler à l'écran** : l'image flotte au premier plan ; glissez-la, molette pour zoomer, clic droit pour le menu ; avec plusieurs épingles, Échap ferme d'abord celle qui a le focus, ou toutes s'il n'y en a aucune

### Enregistrement de zone
- Un raccourci global dédié (**F2** par défaut) : glissez pour choisir la zone à enregistrer, appuyez à nouveau (ou cliquez sur Stop) pour terminer
- Un cadre rouge marque la zone pendant l'enregistrement, avec une petite barre affichant le chrono et un bouton Stop
- Après l'enregistrement, un **éditeur** s'ouvre : aperçu en boucle, poignées de découpe sur la timeline (`Espace` lecture, `←`/`→` image par image, `I`/`O` pour définir début/fin), export avec barre de progression
- Export en **GIF / MP4 / WebP**, commutable dans l'éditeur ; une option permet de sauter l'éditeur et d'enregistrer immédiatement
- Capture du curseur optionnelle, fréquence d'images 10–30 fps
- Encodage par le `ffmpeg` fourni — aucune installation supplémentaire

### Divers
- Raccourcis globaux (**F1** capture, **F2** enregistrement, tous deux configurables) et lancement depuis la **barre d'état système**
- **Multilingue** : 繁體中文 / English / 日本語 / 한국어 / Français / Deutsch / Español (suit le système par défaut)
- Thème **clair / sombre / système**, interface moderne aux coins arrondis et barres de titre sombres
- **Fenêtre de paramètres** centralisée : langue, raccourcis, thème, lancement au démarrage, dossier d'enregistrement, format / fréquence d'enregistrement (les champs de raccourci se règlent en appuyant simplement sur la combinaison)
- **Lancement automatique au démarrage** (optionnel)
- **Mise à jour automatique** : vérifie les nouvelles versions via GitHub Releases (activable dans les paramètres, ou vérification manuelle) ; téléchargement et installation en un clic

## Installation

Téléchargez depuis [Releases](https://github.com/taida957789/ScreenPaste/releases) :

- **`ScreenPaste-<version>-setup.exe`** — installateur (aucun droit administrateur requis, installe dans `%APPDATA%\ScreenPaste`, avec raccourci du menu Démarrer et désinstalleur)
- **`ScreenPaste-<version>-win-x64-portable.zip`** — version portable, sans installation

Les deux sont autonomes — **aucun .NET Runtime séparé n'est requis**, et `ffmpeg` (pour l'enregistrement) est inclus.

## Démarrage rapide

![Barre d'outils](screenshots/ui.png)

1. Une fois lancé, il reste dans la barre d'état ; appuyez sur **F1** (ou double-cliquez l'icône) pour capturer.
2. Survolez une fenêtre ou un élément d'interface pour l'encadrer et cliquez, ou glissez pour sélectionner une zone.
3. Choisissez un outil dans la barre, ajustez ses paramètres et annotez la sélection.
4. **Copier / Enregistrer / Épingler** pour la sortie ; `Échap` quitte la capture (confirmation si des annotations existent, avec option « ne plus demander »).

Enregistrement : appuyez sur **F2** et glissez pour choisir la zone ; appuyez à nouveau sur **F2** (ou Stop) pour terminer, puis prévisualisez, découpez et exportez en GIF/MP4/WebP dans l'éditeur (une option enregistre immédiatement à la place).

## Raccourcis

| Contexte | Touches | Action |
|---|---|---|
| Global | `F1` | Démarrer une capture (configurable) |
| Global | `F2` | Démarrer / arrêter l'enregistrement de zone (configurable) |
| Sélection | Molette | Changer le niveau de détection (élément ⇄ fenêtre) |
| Annotation | `Ctrl+Z` / `Ctrl+Y` | Annuler / rétablir (configurable) |
| Annotation | `Ctrl+C` | Copier dans le presse-papiers (configurable) |
| Annotation | `Ctrl+S` / `Ctrl+Maj+S` | Enregistrer sous / enregistrement rapide (configurable) |
| Annotation | `Suppr` | Supprimer l'annotation sélectionnée |
| Annotation | `Maj` + glisser (ligne) | Aligner sur 45° |
| Annotation | `Entrée` ou clic à l'extérieur | Valider le texte (`Maj+Entrée` pour un saut de ligne) |
| Annotation | `Échap` | Désélectionner → quitter la capture (confirmation si annotée) |
| Éditeur d'enregistrement | `Espace` | Lecture / pause |
| Éditeur d'enregistrement | `←` / `→` | Image par image |
| Éditeur d'enregistrement | `Début` / `Fin` | Aller au début / à la fin de la découpe |
| Éditeur d'enregistrement | `I` / `O` | Définir le début / la fin de la découpe à la position de lecture |
| Fenêtre épinglée | `Ctrl+C` / `Échap` | Copier / fermer |

Tous les paramètres (raccourcis, valeurs par défaut des outils, thème, lancement au démarrage, format / fréquence d'enregistrement, etc.) se règlent depuis le menu de la barre d'état → « Paramètres… ».

> Compilation depuis les sources : exécutez une fois `scripts/fetch-ffmpeg.ps1` pour télécharger le `ffmpeg.exe` fourni (la CI le fait automatiquement à la publication).
