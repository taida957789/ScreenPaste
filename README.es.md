![ScreenPaste](screenshots/banner.png)

<a href='https://ko-fi.com/M6T122R1AH' target='_blank'><img height='36' style='border:0px;height:36px;' src='https://storage.ko-fi.com/cdn/kofi6.png?v=6' border='0' alt='Buy Me a Coffee at ko-fi.com' /></a>

[繁體中文](README.md) | [English](README.en.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | **Español**

# ScreenPaste

Una herramienta ligera de Windows para capturas de pantalla, anotaciones y grabación: captura con una tecla, anota al instante, fija en pantalla y graba cualquier zona en GIF / MP4 / WebP.

## Características

### Captura
- Toda la pantalla (compatible con varios monitores)
- **Detección automática de ventanas y elementos de la interfaz**: pasa el ratón sobre una ventana o un elemento (botones, paneles, bloques de páginas web) para delinearlo automáticamente y haz clic para capturar; la **rueda del ratón** cambia el nivel de detección (elemento ⇄ ventana)
- Arrastra para seleccionar una zona personalizada, con indicador de tamaño y lupa para bordes al píxel

### Anotación
Tras capturar, aparece una **barra de herramientas** junto al cursor (arrastrable y que evita los bordes de la pantalla):

- **Marcador** — trazos sólidos; grosor / color / opacidad ajustables
- **Resaltador** — color semitransparente; grosor / color / opacidad ajustables
- **Texto** — fuente / tamaño / color / estilo (normal, negrita, cursiva, tachado); un clic fuera del cuadro lo confirma
- **Formas** — rectángulo / rectángulo redondeado / elipse, contorno o relleno, grosor y color ajustables
- **Línea / flecha** — arrastra para dibujar; cada extremo puede convertirse en punta de flecha; grosor y color ajustables; mantén `Mayús` para ajustar a ángulos de 45°
- **Imágenes pegadas** — pega PNG / JPEG / WebP, arrastra para mover, rueda para redimensionar
- **Desenfoque** — desenfoque gaussiano / mosaico, intensidad ajustable
- **Selección / movimiento directo** — pasa el ratón sobre cualquier anotación colocada y **arrástrala directamente**; `Supr` la elimina; mover y eliminar se pueden deshacer
- **Selector de color** — entrada Hex, RGB y opacidad (los colores translúcidos se previsualizan sobre un damero); los colores personalizados se recuerdan entre sesiones, clic derecho en una muestra para quitarla
- **Deshacer / Rehacer** — con botones y atajos; cada control deslizante muestra su valor en tiempo real

### Salida
- Copiar al portapapeles
- Guardar como PNG / JPG (guardar como, o guardado rápido en una carpeta predeterminada)
- **Fijar en pantalla**: la imagen flota encima de todo; arrástrala, usa la rueda para hacer zoom, clic derecho para el menú; con varias fijadas, Esc cierra primero la enfocada, o todas si ninguna tiene el foco

### Grabación de zona
- Un atajo global dedicado (**F2** por defecto): arrastra para elegir la zona a grabar, pulsa de nuevo (o haz clic en Detener) para terminar
- Un marco rojo marca la zona durante la grabación, con una pequeña barra con el cronómetro y un botón de detener
- Al terminar se abre un **editor**: vista previa en bucle, tiradores de recorte en la línea de tiempo (`Espacio` reproducir, `←`/`→` fotograma a fotograma, `I`/`O` para fijar inicio/fin), exportación con barra de progreso
- Exporta como **GIF / MP4 / WebP**, cambiable en el propio editor; un ajuste permite saltarse el editor y guardar inmediatamente
- Captura opcional del cursor, tasa de fotogramas de 10–30 fps
- Codificación con el `ffmpeg` incluido — sin instalaciones adicionales

### Más
- Atajos globales (**F1** captura, **F2** grabación, ambos configurables) y arranque desde el icono de la **bandeja del sistema**
- **Varios idiomas**: 繁體中文 / English / 日本語 / 한국어 / Français / Deutsch / Español (sigue el sistema por defecto)
- Tema **claro / oscuro / según el sistema**, interfaz moderna con esquinas redondeadas y barras de título oscuras
- **Ventana de ajustes** centralizada: idioma, atajos, tema, inicio automático, carpeta de guardado, formato / tasa de grabación (los campos de atajo se fijan pulsando la combinación)
- **Inicio automático con Windows** (opcional)
- **Actualización automática**: comprueba nuevas versiones vía GitHub Releases (activable en ajustes, o comprobación manual); descarga e instala con un clic

## Instalación

Descarga desde [Releases](https://github.com/taida957789/ScreenPaste/releases):

- **`ScreenPaste-<version>-setup.exe`** — instalador (sin permisos de administrador, se instala en `%APPDATA%\ScreenPaste`, con acceso directo del menú Inicio y desinstalador)
- **`ScreenPaste-<version>-win-x64-portable.zip`** — versión portable, sin instalación

Ambos son autocontenidos — **no se necesita .NET Runtime aparte**, y `ffmpeg` (para la grabación) viene incluido.

## Inicio rápido

![Barra de herramientas](screenshots/ui.png)

1. Una vez iniciado permanece en la bandeja del sistema; pulsa **F1** (o doble clic en el icono) para capturar.
2. Pasa el ratón sobre una ventana o elemento de la interfaz para delinearlo y haz clic, o arrastra para seleccionar una zona.
3. Elige una herramienta de la barra emergente, ajusta sus parámetros y anota sobre la selección.
4. Pulsa **Copiar / Guardar / Fijar** para la salida; `Esc` sale de la captura (pregunta primero si hay anotaciones, con opción de no volver a preguntar).

Grabación: pulsa **F2** y arrastra para elegir la zona; pulsa **F2** de nuevo (o Detener) para terminar, y luego previsualiza, recorta y exporta como GIF/MP4/WebP en el editor (un ajuste permite guardar inmediatamente en su lugar).

## Atajos de teclado

| Contexto | Teclas | Acción |
|---|---|---|
| Global | `F1` | Iniciar captura (configurable) |
| Global | `F2` | Iniciar / detener grabación de zona (configurable) |
| Selección | Rueda del ratón | Cambiar nivel de detección (elemento ⇄ ventana) |
| Anotación | `Ctrl+Z` / `Ctrl+Y` | Deshacer / rehacer (configurable) |
| Anotación | `Ctrl+C` | Copiar al portapapeles (configurable) |
| Anotación | `Ctrl+S` / `Ctrl+Mayús+S` | Guardar como / guardado rápido (configurable) |
| Anotación | `Supr` | Eliminar la anotación seleccionada |
| Anotación | `Mayús` + arrastrar (línea) | Ajustar a ángulos de 45° |
| Anotación | `Intro` o clic fuera | Confirmar el texto (`Mayús+Intro` para salto de línea) |
| Anotación | `Esc` | Deseleccionar → salir de la captura (pregunta si hay anotaciones) |
| Editor de grabación | `Espacio` | Reproducir / pausar |
| Editor de grabación | `←` / `→` | Fotograma a fotograma |
| Editor de grabación | `Inicio` / `Fin` | Ir al inicio / fin del recorte |
| Editor de grabación | `I` / `O` | Fijar inicio / fin del recorte en la posición actual |
| Ventana fijada | `Ctrl+C` / `Esc` | Copiar / cerrar |

Todos los ajustes (atajos, valores por defecto de las herramientas, tema, inicio automático, formato / tasa de grabación, etc.) se pueden cambiar desde el menú de la bandeja → «Ajustes…».

> Compilación desde el código fuente: ejecuta una vez `scripts/fetch-ffmpeg.ps1` para descargar el `ffmpeg.exe` incluido (la CI lo hace automáticamente al publicar).
