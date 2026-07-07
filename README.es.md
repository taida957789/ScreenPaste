![ScreenPaste](screenshots/banner.png)

<a href='https://ko-fi.com/M6T122R1AH' target='_blank'><img height='36' style='border:0px;height:36px;' src='https://storage.ko-fi.com/cdn/kofi6.png?v=6' border='0' alt='Buy Me a Coffee at ko-fi.com' /></a>

[繁體中文](README.md) | [English](README.en.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | **Español**

# ScreenPaste

Una herramienta ligera de captura y anotación de pantalla para Windows: captura con una pulsación, anota al instante y fija en la pantalla.

## Funciones

### Captura
- La pantalla completa (compatible con varios monitores)
- **Detección automática de ventanas**: pasa el ratón sobre una ventana para delinearla automáticamente y haz clic para capturar
- Arrastra para seleccionar una región personalizada, con indicador de tamaño y una lupa para bordes exactos al píxel

### Anotación
Tras la captura, aparece una **barra de herramientas con iconos** junto al cursor (se puede arrastrar y evita automáticamente los bordes de la pantalla):

- **Rotulador** — trazos sólidos, con grosor / color / opacidad ajustables
- **Marcador** — color superpuesto semitransparente, con grosor / color / opacidad ajustables
- **Texto** — elige fuente / tamaño / color / estilo (normal, negrita, cursiva, tachado)
- **Formas** — rectángulo / rectángulo redondeado / elipse, con contorno o relleno, grosor y color de línea ajustables
- **Pegatinas** — pega imágenes PNG / JPEG / WebP, arrastra para mover, usa la rueda para redimensionar
- **Desenfoque** — desenfoque gaussiano / mosaico, con intensidad ajustable
- **Selector de color** — admite entrada Hex, RGB y ajuste de opacidad
- **Deshacer / Rehacer**

### Salida
- Copiar al portapapeles
- Guardar como PNG / JPG (guardar como, o guardado rápido en una carpeta predeterminada)
- **Fijar en la pantalla**: la imagen flota en primer plano; arrástrala, redimensiona con la rueda, menú con clic derecho; con varias fijaciones, Esc cierra primero la que tiene el foco, o las cierra todas si ninguna lo tiene

### Más
- Tecla de acceso rápido global (**F1** de forma predeterminada) e inicio desde el icono de la **bandeja del sistema**
- **Varios idiomas**: chino tradicional / English / 日本語 / 한국어 / Français / Deutsch / Español (sigue el sistema de forma predeterminada)
- Tema **Claro / Oscuro / Seguir el sistema**
- Una **ventana de ajustes** centralizada: idioma, tecla de acceso rápido, tema, iniciar con el sistema, carpeta de guardado (el campo de la tecla se configura simplemente pulsando la combinación de teclas)
- **Inicio automático con el sistema** opcional
- **Actualización automática**: busca nuevas versiones a través de GitHub Releases (se activa en los ajustes o se comprueba manualmente cuando quieras); si hay una actualización, te lo pregunta y luego la descarga e instala con un clic

## Instalación

Descarga desde [Releases](https://github.com/taida957789/ScreenPaste/releases):

- **`ScreenPaste-<versión>-setup.exe`** — versión con instalador (no requiere permisos de administrador, se instala en `%APPDATA%\ScreenPaste`, incluye acceso directo en el menú Inicio y un desinstalador)
- **`ScreenPaste-<versión>-win-x64-portable.zip`** — versión portátil, sin instalación

Ambas son autónomas, por lo que **no se necesita instalar el .NET Runtime por separado**.

## Inicio rápido

![Barra de herramientas](screenshots/ui.png)

1. Una vez iniciada, permanece en la bandeja del sistema; pulsa **F1** (o haz doble clic en el icono) para empezar una captura.
2. Pasa el ratón sobre una ventana para delinearla automáticamente y haz clic para capturar, o arrastra para seleccionar una región personalizada.
3. Elige una herramienta en la barra que aparece, ajusta sus parámetros y anota sobre la selección.
4. Pulsa **Copiar / Guardar / Fijar** para la salida; `Esc` vuelve a una nueva selección o cancela.

Todos los ajustes (tecla de acceso rápido, valores predeterminados de las herramientas, tema, iniciar con el sistema, etc.) se pueden ajustar desde el menú de la bandeja → ventana «Ajustes…».
