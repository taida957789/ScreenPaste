![ScreenPaste](screenshots/banner.png)

<a href='https://ko-fi.com/M6T122R1AH' target='_blank'><img height='36' style='border:0px;height:36px;' src='https://storage.ko-fi.com/cdn/kofi6.png?v=6' border='0' alt='Buy Me a Coffee at ko-fi.com' /></a>

[繁體中文](README.md) | [English](README.en.md) | [日本語](README.ja.md) | [한국어](README.ko.md) | [Français](README.fr.md) | [Deutsch](README.de.md) | **Español**

# ScreenPaste

Una herramienta ligera de Windows para capturas de pantalla, anotaciones y grabación: captura con una tecla, anota al instante, fija en pantalla y graba cualquier zona en GIF / MP4 / WebP.

## Características

### Captura
- Toda la pantalla (compatible con varios monitores)
- **Detección automática de ventanas y elementos de la interfaz**: pasa el ratón sobre una ventana o un elemento (botones, paneles, bloques de páginas web) para delinearlo automáticamente y haz clic para capturar; la **rueda del ratón** cambia el nivel de detección (elemento ⇄ ventana)
- Arrastra para seleccionar una zona personalizada, con indicador de tamaño; una **lupa de píxeles** sigue el cursor en todo momento (selección y edición) con retícula, coordenadas y el color del píxel — `C` copia el valor, una pulsación breve de `Mayús` recorre HEX / RGB / HSV
- **Ajustable tras seleccionar**: arrastra los tiradores de las esquinas y los puntos medios de los lados para redimensionar; sin herramienta activa, arrastra el interior de la zona para moverla — las anotaciones existentes permanecen fijadas al contenido

### Anotación
Tras capturar, aparece una **barra de herramientas** junto al cursor (arrastrable y que evita los bordes de la pantalla):

- **Marcador** — trazos sólidos; grosor / color / opacidad ajustables
- **Resaltador** — color semitransparente; grosor / color / opacidad ajustables
- **Texto** — fuente / tamaño / color / estilo (normal, negrita, cursiva, tachado); un clic fuera del cuadro lo confirma
- **Formas** — rectángulo / rectángulo redondeado / elipse, contorno o relleno, grosor y color ajustables
- **Línea / flecha** — arrastra para dibujar; cada extremo puede convertirse en punta de flecha; grosor y color ajustables; mantén `Mayús` para ajustar a ángulos de 45°
- **Imágenes pegadas** — pega PNG / JPEG / WebP, arrastra para mover, rueda para redimensionar
- **Desenfoque** — desenfoque gaussiano / mosaico, intensidad ajustable; se dibuja por encima de todas las anotaciones, así que tapa los trazos y las formas que haya debajo, y se puede arrastrar a otro sitio (vuelve a muestrear lo que cubre ahora); el tipo (gaussiano / mosaico) tiene un valor predeterminado configurable en los ajustes, y cambiarlo en la barra de herramientas se recuerda para la próxima vez
- **Lupa** — arrastra para encuadrar el área que quieres ampliar y la vista ampliada se coloca al lado: rectángulo / redondeado / círculo, zoom ajustable (1,2–10×), grosor y color del borde, conector, sombra, suavizado (desactivado muestra la cuadrícula de píxeles) y si el contenido ampliado incluye las demás anotaciones; la vista **y el marco del área de origen se pueden arrastrar en cualquier momento** — mover el marco cambia lo que se amplía y el conector se reorienta; al seleccionar una lupa aparecen **8 tiradores en el marco para cambiar su tamaño** (la vista ampliada escala con él), y la rueda cambia el zoom
- **Selección / movimiento directo** — pasa el ratón sobre cualquier anotación colocada y **arrástrala directamente**; `Supr` la elimina; mover y eliminar se pueden deshacer
- **Selector de color** — entrada Hex, RGB y opacidad (los colores translúcidos se previsualizan sobre un damero); los colores personalizados se recuerdan entre sesiones, clic derecho en una muestra para quitarla
- **Deshacer / Rehacer** — con botones y atajos; cada control deslizante muestra su valor en tiempo real

### Salida
- Copiar al portapapeles
- Guardar como PNG / JPG (guardar como, o guardado rápido en una carpeta predeterminada)
- **Fijar en pantalla**: la imagen flota encima de todo; arrástrala, usa la rueda para hacer zoom alrededor del cursor, clic derecho para el menú; un doble clic, `Esc` o `Ctrl+W` la cierran y `F3` puede recuperarla (`Mayús+Esc` la destruye); con varias fijadas, Esc cierra primero la enfocada, o todas si ninguna tiene el foco. `F3` también fija el contenido del portapapeles

### Grabación de zona
- Un atajo global dedicado (**F2** por defecto): arrastra para elegir la zona a grabar, o **haz clic en una ventana para detectarla automáticamente** (la rueda alterna niveles de ventana/elemento, igual que en la captura), pulsa de nuevo (o haz clic en Detener) para terminar
- Un marco rojo marca la zona durante la grabación, con una pequeña barra con el cronómetro y un botón de detener
- Al terminar se abre un **editor**: vista previa en bucle, tiradores de recorte en la línea de tiempo (`Espacio` reproducir, `←`/`→` fotograma a fotograma, `I`/`O` para fijar inicio/fin), exportación con barra de progreso
- **Arrastra el borde del marco rojo para mover la zona durante la grabación** — el interior sigue siendo totalmente interactivo
- El editor tiene las mismas **herramientas de anotación** que las capturas (texto / formas / línea-flecha / imágenes / desenfoque gaussiano / mosaico, con sus opciones; las anotaciones se arrastran directamente, `Supr` las elimina); `Ctrl+C` exporta rápido y pone el archivo en el portapapeles
- Exporta como **GIF / MP4 / WebP**, cambiable en el propio editor; un ajuste permite saltarse el editor y guardar inmediatamente
- Captura opcional del cursor, tasa de fotogramas de 10–30 fps
- **Audio**: sonido del sistema (loopback) / micrófono / ambos mezclados, seleccionable en ajustes; el sonido solo se guarda en **MP4** (GIF y WebP son silenciosos). Sin un dispositivo de audio utilizable (habitual en escritorio remoto) se graba sin sonido
- Codificación con el `ffmpeg` incluido — sin instalaciones adicionales

### Más
- Atajos globales (**F1** captura, **Ctrl+F1** capturar y copiar, **Mayús+F1** captura personalizada, **F2** grabación, **F3** pegar / recuperar una imagen, **Mayús+F3** ocultar / mostrar imágenes fijadas, todos configurables) y arranque desde el icono de la **bandeja del sistema**
- **Varios idiomas**: 繁體中文 / English / 日本語 / 한국어 / Français / Deutsch / Español (sigue el sistema por defecto)
- Tema **claro / oscuro / según el sistema**, interfaz moderna con esquinas redondeadas y barras de título oscuras
- **Ventana de ajustes** centralizada: idioma, atajos, tema, inicio automático, carpeta de guardado, valores predeterminados de anotación (tipo de desenfoque), formato / tasa de grabación (los campos de atajo se fijan pulsando la combinación)
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
4. Pulsa **Copiar / Guardar / Fijar** para la salida; `Esc` sale de la captura (pregunta primero, con opción de no volver a preguntar).

Grabación: pulsa **F2** y arrastra para elegir la zona; pulsa **F2** de nuevo (o Detener) para terminar, y luego previsualiza, recorta y exporta como GIF/MP4/WebP en el editor (un ajuste permite guardar inmediatamente en su lugar).

## Atajos de teclado

Los atajos siguen los de Snipaste cuando tiene uno. Las teclas marcadas *(configurable)* se cambian en Ajustes → «Atajos» / «Atajos en la aplicación»; las demás son fijas.

> **Cambios en esta versión**
> - Un **doble clic en una imagen fijada la cierra** (la primera vez pregunta antes). En Ajustes → «Imágenes fijadas» puede pasar a alternar la miniatura o a no hacer nada.
> - `Esc` / `Ctrl+W` cierran una imagen fijada **de forma recuperable**: `F3`, si el portapapeles no ha cambiado desde el último pegado, la devuelve a su sitio. `Mayús+Esc` la **destruye** para siempre.
> - La rueda del ratón amplía una imagen fijada **alrededor del cursor** (en Ajustes → «Imágenes fijadas» se puede mantener fija la esquina superior izquierda).
> - El formato de color de la lupa (HEX → RGB → HSV) cambia con una **pulsación breve de `Mayús`** (pulsada y soltada sola); `Mayús`+arrastrar y `Mayús`+clic ya no lo cambian.

### Global

| Teclas | Acción |
|---|---|
| `F1` | Iniciar captura (configurable) |
| `Ctrl+F1` | Capturar y copiar: el área elegida va directa al portapapeles (configurable) |
| `Mayús+F1` | Captura personalizada: área y retardo en un cuadro de diálogo (configurable) |
| `F2` | Iniciar / detener grabación de zona (configurable) |
| `F3` | Fijar el portapapeles (imagen, archivo de imagen, valor de color o texto) en el cursor; pulsado de nuevo con el portapapeles sin cambios, recupera la última imagen cerrada (configurable) |
| `Mayús+F3` | Ocultar / mostrar todas las imágenes fijadas (configurable) |
| Sin asignar | Imagen fijada traspasable (la que está bajo el cursor deja pasar los clics), capturar y fijar, capturar, copiar y fijar, copiar de nuevo la última área, copiar toda la pantalla, capturar la ventana activa, capturar la pantalla bajo el cursor, captura retardada, borrar historial de capturas: se asignan en Ajustes → «Atajos» / «Más atajos» |
| Icono de la bandeja | Doble clic inicia una captura; el menú también ofrece captura personalizada, pegar del portapapeles, ocultar / mostrar imágenes fijadas y «Hacer clicables todas las imágenes fijadas» |

### Captura: selección del área

| Teclas | Acción |
|---|---|
| Clic / arrastrar | Capturar la ventana o el elemento resaltado / trazar un área personalizada |
| Rueda del ratón | Cambiar nivel de detección (elemento ⇄ ventana) |
| `Tab` | Alternar entre detección de elementos y de ventanas |
| `Espacio` | Tomar el área resaltada |
| `Ctrl+A` | Tomar la pantalla bajo el cursor y, después, todas las pantallas |
| `Mayús` + arrastrar | Trazar un cuadrado |
| `Espacio` mantenido al arrastrar | Mover el área en lugar de cambiar su tamaño |
| `W` `A` `S` `D` / flechas | Mover el cursor un píxel (más rápido si se mantiene); el arrastre lo sigue |
| Pulsación breve de `Alt` | Mostrar / ocultar las guías en cruz (se recuerda) |
| `C` | Copiar el color del píxel bajo la lupa |
| Pulsación breve de `Mayús` | Recorrer la lectura del color HEX → RGB → HSV |
| `R` / `Mayús+R` | Recorrer hacia atrás / adelante las áreas de capturas anteriores |
| `,` / `.` | Recorrer hacia atrás / adelante el historial de capturas (capturas anteriores, con las anotaciones integradas; solo en memoria) |
| `` ` `` o `!` | Mostrar / ocultar el puntero del ratón en la captura (se recuerda) |
| `F5` | Volver a capturar la pantalla bajo la superposición |
| Clic derecho | Descartar el área que se está trazando; si no hay ninguna, salir de la captura |
| `Esc` | Salir de la captura |

### Captura: edición y anotación

| Teclas | Acción |
|---|---|
| Arrastrar un tirador | Cambiar el tamaño del área (`Ctrl`: desde el centro; `Espacio` mantenido: moverla) |
| Arrastrar dentro del área (sin herramienta) | Mover el área (`Mayús`: en un solo eje) |
| Flechas | Mover el área un píxel, o la anotación seleccionada |
| `Ctrl` / `Mayús` + flechas | Agrandar / reducir el área por ese lado |
| `Ctrl+A` | Área = la pantalla bajo el cursor y, después, todas las pantallas |
| `W` `A` `S` `D` | Mover el cursor un píxel (también al dibujar) |
| `C` / pulsación breve de `Mayús` | Copiar el color de la lupa / cambiar su formato, como en la selección |
| `R` / `Mayús+R`, `,` / `.`, `` ` `` / `!`, `F5` | Como en la selección; las anotaciones siguen al contenido (`,` / `.` preguntan antes) |
| `Espacio` | Mostrar / ocultar la barra de herramientas |
| `Ctrl+1` … `Ctrl+9`, `Ctrl+0` | Forma, línea / flecha, marcador, resaltador, desenfoque / mosaico, texto, marcador numérico, lupa, borrador, pegar imagen |
| `T` / `B` / `M` / `E` | Texto / marcador / desenfoque / borrador |
| `Ctrl+Tab` / `Ctrl+Mayús+Tab` | Herramienta siguiente / anterior |
| `Tab` | Cambiar la variante de la herramienta: rectángulo → redondeado → elipse, línea ⇄ flecha, gaussiano ⇄ mosaico, forma de la lupa |
| `1` / `2`, `[` / `]`, rueda del ratón | Más fino / más grueso (tamaño de fuente con la herramienta de texto) |
| `Alt+C` | Tomar el color bajo el cursor |
| `+` / `-` | Marcador numérico: siguiente número arriba / abajo |
| Rueda sobre una muestra de color | Ajustar la opacidad de la herramienta |
| `Mayús` + arrastrar al dibujar | Formas, desenfoque y marcos de lupa cuadrados; trazos de lápiz rectos; líneas a 45° |
| Clic (herramienta de línea) | Empezar una polilínea: cada clic añade un punto; clic derecho, `Esc`, `Intro` o doble clic la terminan |
| Arrastrar una anotación | Moverla (`Mayús`: en un solo eje) |
| `Ctrl` + arrastrar una anotación | Sacar una copia de ella |
| `Mayús` + rueda | Escalar el rectángulo / la elipse seleccionada |
| Rueda sobre una lupa seleccionada | Cambiar su aumento |
| `Supr` | Eliminar la anotación seleccionada |
| `Ctrl+Mayús+C` / `Ctrl+Mayús+V` | Copiar la anotación seleccionada / pegar una copia |
| `Ctrl+Z` / `Ctrl+Y` | Deshacer / rehacer (configurable) |
| `Ctrl+Mayús+Z` | Quitar todas las anotaciones, en un solo paso de deshacer (configurable) |
| `Intro` o clic fuera | Confirmar el texto (`Mayús+Intro` para salto de línea; `Esc` también confirma) |
| Rueda sobre el cuadro de texto | Tamaño de fuente |
| Clic central en el cuadro de texto | Pegar el texto del portapapeles |
| `Ctrl+C` / `Ctrl+Insert` | Copiar al portapapeles (`Ctrl+C` configurable) |
| `Ctrl+S` / `Ctrl+Mayús+S` | Guardar como / guardado rápido (configurable; `Mayús`+clic en Guardar también guarda rápido) |
| `Ctrl+T` | Fijar en pantalla (configurable) |
| `Ctrl+P` | Imprimir (configurable) |
| `Ctrl+Alt+C` | Copiar como archivo PNG |
| `V` | Copiar el tamaño del área como texto (`800x600`) |
| `Intro` | Copiar y cerrar (configurable en Ajustes → «Intro y ratón») |
| Doble clic en el área (sin herramienta) | Copiar y cerrar (configurable) |
| Clic central en el área | Fijar en pantalla (configurable) |
| Clic derecho | Retroceder un nivel: terminar la polilínea o el texto; con la herramienta de forma, desenfoque o borrador, marcar el elemento bajo el cursor (fuera del área: toda el área); deseleccionar; soltar la herramienta; por último descartar el área para trazar otra (pregunta antes si se perderían anotaciones) |
| `Esc` | Cancelar el arrastre de un marco de lupa → terminar la polilínea → deseleccionar → salir de la captura (pregunta primero; desactivable) |

### Imágenes fijadas

| Teclas | Acción |
|---|---|
| Arrastrar | Mover la imagen (`W` `A` `S` `D` mientras se arrastra: un píxel) |
| `Mayús` + arrastrar | Ajustar sus bordes a otras imágenes fijadas, ventanas y bordes de pantalla |
| Arrastrar el borde | Cambiar el tamaño manteniendo la proporción |
| Rueda del ratón | Ampliar alrededor del cursor (en Ajustes → «Imágenes fijadas» se puede mantener fija la esquina superior izquierda) |
| `Ctrl` + rueda | Opacidad |
| Clic central | Volver al 100 % de tamaño y opacidad; otra vez para deshacerlo |
| Doble clic | Cerrar, de forma recuperable (pregunta la primera vez; se cambia en Ajustes → «Imágenes fijadas» o en el menú de la imagen) |
| `Mayús` + doble clic | Miniatura sí / no |
| Arrastrar con el botón derecho | Reducir la imagen a la parte arrastrada (una miniatura) |
| Clic derecho / tecla `Menú` | Menú de la imagen |
| `Ctrl` + clic | Añadir la imagen a la selección o quitarla; las imágenes seleccionadas se mueven, se atenúan y se cierran juntas |
| `Esc` / `Ctrl+W` | Cerrar, de forma recuperable (`F3` la recupera) |
| `Mayús+Esc` | Destruir: ya no se puede recuperar |
| `Esc` sin ninguna imagen enfocada | Cerrar todas las imágenes visibles, de forma recuperable |
| `1` / `2` | Girar en sentido horario / antihorario |
| `3` / `4` | Voltear horizontal / verticalmente |
| `5` / `6` | Escala de grises / invertir colores |
| `+` / `-` | Acercar / alejar |
| `Ctrl++` / `Ctrl+-` | Subir / bajar la opacidad |
| Flechas | Mover un píxel (más rápido si se mantiene) |
| `Ctrl+C` / `Ctrl+Insert` | Copiar (`Ctrl+C` configurable) |
| `Ctrl+Alt+C` | Copiar como archivo PNG |
| `Ctrl+S` / `Ctrl+Mayús+S` | Guardar como / guardado rápido (configurable) |
| `Ctrl+P` | Imprimir (configurable) |
| `Ctrl+V` | Mostrar en su lugar el contenido del portapapeles |
| `Ctrl+Mayús+C` | Copiar el texto del que se creó una imagen de texto |
| `Ctrl+A` | Seleccionar todas las imágenes visibles |
| `Ctrl+Mayús+P` | Abrir los ajustes |
| Pulsación breve de `Alt` | Lupa sí / no: `C` copia el color bajo el cursor, una pulsación breve de `Mayús` recorre HEX → RGB → HSV, `W` `A` `S` `D` mueven el cursor un píxel |
| Soltar un archivo de imagen | Reemplazar la imagen (`Mayús`: abrirla como una nueva imagen fijada) |

### Editor de grabación

| Teclas | Acción |
|---|---|
| `Espacio` | Reproducir / pausar |
| `←` / `→` | Fotograma a fotograma |
| `Inicio` / `Fin` | Ir al inicio / fin del recorte |
| `I` / `O` | Fijar inicio / fin del recorte en la posición actual |
| `Ctrl+C` | Exportación rápida y copia del archivo al portapapeles |

Todos los ajustes (atajos, valores por defecto de las herramientas, tema, inicio automático, formato / tasa de grabación, etc.) se pueden cambiar desde el menú de la bandeja → «Ajustes…».

> Compilación desde el código fuente: ejecuta una vez `scripts/fetch-ffmpeg.ps1` para descargar el `ffmpeg.exe` incluido (la CI lo hace automáticamente al publicar).
