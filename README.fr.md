![ScreenPaste](screenshots/banner.png)

<a href='https://ko-fi.com/M6T122R1AH' target='_blank'><img height='36' style='border:0px;height:36px;' src='https://storage.ko-fi.com/cdn/kofi6.png?v=6' border='0' alt='Buy Me a Coffee at ko-fi.com' /></a>

[繁體中文](README.md) | [English](README.en.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | **Français** | [Deutsch](README.de.md) | [Español](README.es.md)

# ScreenPaste

Un outil Windows léger de capture d'écran, d'annotation et d'enregistrement : capturez d'une touche, annotez instantanément, épinglez à l'écran et enregistrez n'importe quelle zone en GIF / MP4 / WebP.

## Fonctionnalités

### Capture
- L'écran entier (multi-écrans pris en charge)
- **Détection automatique des fenêtres et des éléments d'interface** : survolez une fenêtre ou un élément (bouton, panneau, bloc de page web) pour l'encadrer automatiquement, puis cliquez pour capturer ; la **molette** change le niveau de détection (élément ⇄ fenêtre)
- Glissez pour sélectionner une zone personnalisée, avec affichage des dimensions ; une **loupe pixel** suit le curseur en permanence (sélection et édition) avec un réticule, les coordonnées et la couleur du pixel — `C` copie la valeur, un appui bref sur `Maj` fait défiler HEX / RGB / HSV
- **Ajustable après la sélection** : faites glisser les poignées des coins et des milieux de côtés pour redimensionner ; sans outil actif, glissez l'intérieur de la zone pour la déplacer — les annotations existantes restent fixées au contenu

### Annotation
Après la capture, une **barre d'outils** apparaît près du curseur (déplaçable, elle évite les bords de l'écran) :

- **Marqueur** — traits pleins ; épaisseur / couleur / opacité réglables
- **Surligneur** — couleur semi-transparente ; épaisseur / couleur / opacité réglables
- **Texte** — police / taille / couleur / style (normal, gras, italique, barré) ; un clic hors de la zone valide le texte
- **Formes** — rectangle / rectangle arrondi / ellipse, contour ou rempli, épaisseur et couleur réglables
- **Ligne / flèche** — glissez pour tracer ; chaque extrémité peut devenir une pointe de flèche ; épaisseur et couleur réglables ; maintenez `Maj` pour aligner sur des angles de 45°
- **Images collées** — collez des PNG / JPEG / WebP, glissez pour déplacer, molette pour redimensionner
- **Flou** — flou gaussien / mosaïque, intensité réglable ; dessiné au-dessus de toutes les annotations, il masque donc les tracés et les formes en dessous, et se déplace par glisser (la zone ré-échantillonne ce qu'elle recouvre alors) ; le type (gaussien / mosaïque) a une valeur par défaut réglable dans les paramètres, et le changer dans la barre d’outils est mémorisé pour la fois suivante
- **Loupe** — glissez pour cadrer la zone à agrandir : une vue agrandie se place juste à côté ; rectangle / arrondi / cercle, zoom réglable (1,2–10×), épaisseur et couleur de bordure, trait de liaison, ombre, lissage (désactivé, la grille de pixels reste nette) et inclusion ou non des autres annotations dans l'agrandissement ; la vue **et le cadre de la zone source se déplacent tous deux à tout moment** — déplacer le cadre change ce qui est agrandi et le trait de liaison se réoriente ; sélectionner une loupe fait apparaître **8 poignées de redimensionnement sur le cadre** (la vue agrandie suit selon le zoom), et la molette change le zoom
- **Sélection / déplacement direct** — survolez n'importe quelle annotation posée et **déplacez-la directement** ; `Suppr` la supprime ; déplacements et suppressions sont annulables
- **Sélecteur de couleurs** — saisie Hex, RGB et opacité (les couleurs translucides s'affichent sur un damier) ; les couleurs personnalisées sont mémorisées d'une session à l'autre, clic droit sur une pastille pour la retirer
- **Annuler / Rétablir** — par boutons et raccourcis ; chaque curseur affiche sa valeur en temps réel

### Sortie
- Copier dans le presse-papiers
- Enregistrer en PNG / JPG (enregistrer sous, ou enregistrement rapide dans un dossier par défaut)
- **Épingler à l'écran** : l'image flotte au premier plan ; glissez-la, molette pour zoomer autour du curseur, clic droit pour le menu ; un double-clic, `Échap` ou `Ctrl+W` la ferme et `F3` peut la rappeler (`Maj+Échap` la détruit) ; avec plusieurs épingles, Échap ferme d'abord celle qui a le focus, ou toutes s'il n'y en a aucune. `F3` épingle aussi le contenu du presse-papiers

### Enregistrement de zone
- Un raccourci global dédié (**F2** par défaut) : glissez pour choisir la zone à enregistrer, ou **cliquez sur une fenêtre pour la détecter automatiquement** (la molette parcourt les niveaux fenêtre/élément, comme pour la capture), appuyez à nouveau (ou cliquez sur Stop) pour terminer
- Un cadre rouge marque la zone pendant l'enregistrement, avec une petite barre affichant le chrono et un bouton Stop
- Après l'enregistrement, un **éditeur** s'ouvre : aperçu en boucle, poignées de découpe sur la timeline (`Espace` lecture, `←`/`→` image par image, `I`/`O` pour définir début/fin), export avec barre de progression
- **Faites glisser le bord du cadre rouge pour déplacer la zone en cours d'enregistrement** — l'intérieur reste entièrement interactif
- L'éditeur offre les mêmes **outils d'annotation** que les captures (texte / formes / ligne-flèche / images / flou gaussien / mosaïque, avec leurs options ; les annotations se déplacent directement, `Suppr` les supprime) ; `Ctrl+C` exporte rapidement et place le fichier dans le presse-papiers
- Export en **GIF / MP4 / WebP**, commutable dans l'éditeur ; une option permet de sauter l'éditeur et d'enregistrer immédiatement
- Capture du curseur optionnelle, fréquence d'images 10–30 fps
- **Audio** : son système (loopback) / microphone / les deux mixés, au choix dans les réglages ; le son n'est enregistré qu'en **MP4** (GIF et WebP sont muets). Sans périphérique audio utilisable (fréquent en bureau à distance), l'enregistrement se fait sans son
- Encodage par le `ffmpeg` fourni — aucune installation supplémentaire

### Divers
- Raccourcis globaux (**F1** capture, **Ctrl+F1** capturer et copier, **Maj+F1** capture personnalisée, **F2** enregistrement, **F3** coller / rappeler une image, **Maj+F3** masquer / afficher les images épinglées, tous configurables) et lancement depuis la **barre d'état système**
- **Multilingue** : 繁體中文 / English / 日本語 / 한국어 / Français / Deutsch / Español (suit le système par défaut)
- Thème **clair / sombre / système**, interface moderne aux coins arrondis et barres de titre sombres
- **Fenêtre de paramètres** centralisée : langue, raccourcis, thème, lancement au démarrage, dossier d'enregistrement, valeurs par défaut des annotations (type de flou), format / fréquence d'enregistrement (les champs de raccourci se règlent en appuyant simplement sur la combinaison)
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
4. **Copier / Enregistrer / Épingler** pour la sortie ; `Échap` quitte la capture (avec confirmation et option « ne plus demander »).

Enregistrement : appuyez sur **F2** et glissez pour choisir la zone ; appuyez à nouveau sur **F2** (ou Stop) pour terminer, puis prévisualisez, découpez et exportez en GIF/MP4/WebP dans l'éditeur (une option enregistre immédiatement à la place).

## Raccourcis

Les raccourcis reprennent ceux de Snipaste dès qu'il en a un. Les touches marquées *(configurable)* se modifient dans Paramètres → « Raccourcis » / « Raccourcis dans l'application » ; les autres sont fixes.

> **Nouveautés de cette version**
> - Un **double-clic sur une image épinglée la ferme** (la première fois, une confirmation est demandée). Paramètres → « Images épinglées » permet de lui faire basculer la miniature ou de ne rien faire.
> - `Échap` / `Ctrl+W` ferment une image épinglée **de façon récupérable** : `F3`, si le presse-papiers n'a pas changé depuis le dernier collage, la rappelle à sa place. `Maj+Échap` la **détruit** définitivement.
> - La molette zoome une image épinglée **autour du curseur** (Paramètres → « Images épinglées » permet de garder le coin supérieur gauche fixe).
> - Le format de couleur de la loupe (HEX → RGB → HSV) change sur un **appui bref sur `Maj`** (pressée puis relâchée seule) ; `Maj`+glisser et `Maj`+clic ne le changent plus.

### Global

| Touches | Action |
|---|---|
| `F1` | Démarrer une capture (configurable) |
| `Ctrl+F1` | Capturer et copier : la zone choisie va directement dans le presse-papiers (configurable) |
| `Maj+F1` | Capture personnalisée : zone et délai réglés dans une boîte de dialogue (configurable) |
| `F2` | Démarrer / arrêter l'enregistrement de zone (configurable) |
| `F3` | Épingler le presse-papiers (image, fichier image, valeur de couleur ou texte) au curseur ; pressée à nouveau sans que le presse-papiers ait changé, rappelle la dernière image fermée (configurable) |
| `Maj+F3` | Masquer / afficher toutes les images épinglées (configurable) |
| Non attribués | Image épinglée traversable (celle sous le curseur laisse passer les clics), capturer et épingler, capturer, copier et épingler, recopier la dernière zone, copier tout l'écran, capturer la fenêtre active, capturer l'écran sous le curseur, capture différée, effacer l'historique des captures : à attribuer dans Paramètres → « Raccourcis » / « Plus de raccourcis » |
| Icône de la barre d'état | Double-clic : démarrer une capture ; le menu propose aussi la capture personnalisée, coller le presse-papiers, masquer / afficher les images épinglées et « Rendre toutes les images épinglées cliquables » |

### Capture : sélection de la zone

| Touches | Action |
|---|---|
| Clic / glisser | Capturer la fenêtre ou l'élément d'interface en surbrillance / tracer une zone personnalisée |
| Molette | Changer le niveau de détection (élément ⇄ fenêtre) |
| `Tab` | Basculer entre détection d'éléments et de fenêtres |
| `Espace` | Prendre la zone en surbrillance |
| `Ctrl+A` | Prendre l'écran sous le curseur, puis tous les écrans |
| `Maj` + glisser | Tracer un carré |
| `Espace` maintenue en glissant | Déplacer la zone au lieu de la redimensionner |
| `W` `A` `S` `D` / flèches | Déplacer le curseur d'un pixel (plus vite en maintenant) ; un glisser le suit |
| Appui bref sur `Alt` | Afficher / masquer les lignes de repère en croix (mémorisé) |
| `C` | Copier la couleur du pixel sous la loupe |
| Appui bref sur `Maj` | Faire défiler l'affichage de la couleur HEX → RGB → HSV |
| `R` / `Maj+R` | Reprendre la zone d'une capture précédente / revenir vers une plus récente |
| `,` / `.` | Parcourir l'historique des captures en arrière / en avant (captures précédentes, annotations fusionnées ; en mémoire seulement) |
| `` ` `` ou `!` | Afficher / masquer le pointeur de la souris dans la capture (mémorisé) |
| `F5` | Recapturer l'écran sous la superposition |
| Clic droit | Abandonner la zone en cours de tracé ; sinon, quitter la capture |
| `Échap` | Quitter la capture |

### Capture : édition et annotation

| Touches | Action |
|---|---|
| Glisser une poignée | Redimensionner la zone (`Ctrl` : autour du centre ; `Espace` maintenue : la déplacer) |
| Glisser dans la zone (sans outil) | Déplacer la zone (`Maj` : sur un seul axe) |
| Flèches | Déplacer la zone d'un pixel, ou l'annotation sélectionnée |
| `Ctrl` / `Maj` + flèches | Agrandir / réduire la zone de ce côté |
| `Ctrl+A` | Zone = l'écran sous le curseur, puis tous les écrans |
| `W` `A` `S` `D` | Déplacer le curseur d'un pixel (aussi en dessinant) |
| `C` / appui bref sur `Maj` | Copier la couleur de la loupe / changer son format, comme pendant la sélection |
| `R` / `Maj+R`, `,` / `.`, `` ` `` / `!`, `F5` | Comme pendant la sélection ; les annotations restent sur le contenu (`,` / `.` demandent d'abord) |
| `Espace` | Afficher / masquer la barre d'outils |
| `Ctrl+1` … `Ctrl+9`, `Ctrl+0` | Forme, ligne / flèche, marqueur, surligneur, flou / mosaïque, texte, numérotation, loupe, gomme, coller une image |
| `T` / `B` / `M` / `E` | Texte / marqueur / flou / gomme |
| `Ctrl+Tab` / `Ctrl+Maj+Tab` | Outil suivant / précédent |
| `Tab` | Changer la variante de l'outil : rectangle → arrondi → ellipse, ligne ⇄ flèche, gaussien ⇄ mosaïque, forme de la loupe |
| `1` / `2`, `[` / `]`, molette | Plus fin / plus épais (taille de police avec l'outil texte) |
| `Alt+C` | Prélever la couleur sous le curseur |
| `+` / `-` | Numérotation : numéro suivant plus / moins |
| Molette sur une pastille de couleur | Régler l'opacité de l'outil |
| `Maj` + glisser en dessinant | Formes, flou et cadre de loupe carrés ; traits de stylo droits ; lignes à 45° |
| Clic (outil ligne) | Commencer une ligne brisée : chaque clic ajoute un point ; clic droit, `Échap`, `Entrée` ou double-clic pour terminer |
| Glisser une annotation | La déplacer (`Maj` : sur un seul axe) |
| `Ctrl` + glisser une annotation | En tirer une copie |
| `Maj` + molette | Mettre à l'échelle le rectangle / l'ellipse sélectionné |
| Molette sur une loupe sélectionnée | Changer son grossissement |
| `Suppr` | Supprimer l'annotation sélectionnée |
| `Ctrl+Maj+C` / `Ctrl+Maj+V` | Copier l'annotation sélectionnée / en coller une copie |
| `Ctrl+Z` / `Ctrl+Y` | Annuler / rétablir (configurable) |
| `Ctrl+Maj+Z` | Supprimer toutes les annotations, en une seule étape d'annulation (configurable) |
| `Entrée` ou clic à l'extérieur | Valider le texte (`Maj+Entrée` pour un saut de ligne ; `Échap` valide aussi) |
| Molette sur la zone de texte | Taille de police |
| Clic milieu dans la zone de texte | Coller le texte du presse-papiers |
| `Ctrl+C` / `Ctrl+Inser` | Copier dans le presse-papiers (`Ctrl+C` configurable) |
| `Ctrl+S` / `Ctrl+Maj+S` | Enregistrer sous / enregistrement rapide (configurable ; `Maj`+clic sur Enregistrer fait aussi un enregistrement rapide) |
| `Ctrl+T` | Épingler à l'écran (configurable) |
| `Ctrl+P` | Imprimer (configurable) |
| `Ctrl+Alt+C` | Copier en tant que fichier PNG |
| `V` | Copier la taille de la zone en texte (`800x600`) |
| `Entrée` | Copier et fermer (configurable dans Paramètres → « Entrée et souris ») |
| Double-clic dans la zone (sans outil) | Copier et fermer (configurable) |
| Clic milieu dans la zone | Épingler à l'écran (configurable) |
| Clic droit | Revenir d'un niveau : terminer la ligne brisée ou le texte ; avec l'outil forme, flou ou gomme, marquer l'élément d'interface sous le curseur (hors de la zone : toute la zone) ; désélectionner ; lâcher l'outil ; enfin abandonner la zone pour en tracer une autre (avec confirmation si des annotations seraient perdues) |
| `Échap` | Annuler le glisser d'un cadre de loupe → terminer la ligne brisée → désélectionner → quitter la capture (avec confirmation, désactivable) |

### Images épinglées

| Touches | Action |
|---|---|
| Glisser | Déplacer l'image (`W` `A` `S` `D` pendant le glisser : d'un pixel) |
| `Maj` + glisser | Aimanter ses bords aux autres images épinglées, aux fenêtres et aux bords d'écran |
| Glisser la bordure | Redimensionner en gardant les proportions |
| Molette | Zoomer autour du curseur (Paramètres → « Images épinglées » permet de garder le coin supérieur gauche fixe) |
| `Ctrl` + molette | Opacité |
| Clic milieu | Revenir à 100 % de taille et d'opacité ; à nouveau pour revenir en arrière |
| Double-clic | Fermer, de façon récupérable (confirmation la première fois ; modifiable dans Paramètres → « Images épinglées » ou le menu de l'image) |
| `Maj` + double-clic | Miniature activée / désactivée |
| Glisser avec le bouton droit | Réduire l'image à la partie survolée (une miniature) |
| Clic droit / touche `Menu` | Menu de l'image |
| `Ctrl` + clic | Ajouter l'image à la sélection ou l'en retirer ; les images sélectionnées se déplacent, s'estompent et se ferment ensemble |
| `Échap` / `Ctrl+W` | Fermer, de façon récupérable (`F3` la rappelle) |
| `Maj+Échap` | Détruire : elle ne peut plus être rappelée |
| `Échap` sans image active | Fermer toutes les images affichées, de façon récupérable |
| `1` / `2` | Rotation horaire / antihoraire |
| `3` / `4` | Retourner horizontalement / verticalement |
| `5` / `6` | Niveaux de gris / inverser les couleurs |
| `+` / `-` | Zoom avant / arrière |
| `Ctrl++` / `Ctrl+-` | Augmenter / diminuer l'opacité |
| Flèches | Déplacer d'un pixel (plus vite en maintenant) |
| `Ctrl+C` / `Ctrl+Inser` | Copier (`Ctrl+C` configurable) |
| `Ctrl+Alt+C` | Copier en tant que fichier PNG |
| `Ctrl+S` / `Ctrl+Maj+S` | Enregistrer sous / enregistrement rapide (configurable) |
| `Ctrl+P` | Imprimer (configurable) |
| `Ctrl+V` | Afficher à la place le contenu du presse-papiers |
| `Ctrl+Maj+C` | Copier le texte d'origine d'une image créée à partir de texte |
| `Ctrl+A` | Sélectionner toutes les images affichées |
| `Ctrl+Maj+P` | Ouvrir les paramètres |
| Appui bref sur `Alt` | Loupe activée / désactivée : `C` copie la couleur sous le curseur, un appui bref sur `Maj` fait défiler HEX → RGB → HSV, `W` `A` `S` `D` déplacent le curseur d'un pixel |
| Déposer un fichier image | Remplacer l'image (`Maj` : l'ouvrir dans une nouvelle image épinglée) |

### Éditeur d'enregistrement

| Touches | Action |
|---|---|
| `Espace` | Lecture / pause |
| `←` / `→` | Image par image |
| `Début` / `Fin` | Aller au début / à la fin de la découpe |
| `I` / `O` | Définir le début / la fin de la découpe à la position de lecture |
| `Ctrl+C` | Export rapide et copie du fichier dans le presse-papiers |

Tous les paramètres (raccourcis, valeurs par défaut des outils, thème, lancement au démarrage, format / fréquence d'enregistrement, etc.) se règlent depuis le menu de la barre d'état → « Paramètres… ».

> Compilation depuis les sources : exécutez une fois `scripts/fetch-ffmpeg.ps1` pour télécharger le `ffmpeg.exe` fourni (la CI le fait automatiquement à la publication).
