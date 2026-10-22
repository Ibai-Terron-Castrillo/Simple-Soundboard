# LocalSoundboard

**LocalSoundboard** es una aplicación de escritorio para Windows que permite reproducir archivos de audio locales como soundboard en Discord, OBS, videojuegos, llamadas o directos.

La aplicación se ejecuta en el PC. Una tablet o móvil puede usarse como mando remoto en la misma red local, pero no envía audio. Solo manda comandos al PC, y el PC reproduce el sonido.

English version: [README.md](README.md)

## Qué Puedes Hacer

- Elegir una carpeta local de sonidos.
- Escanear subcarpetas automáticamente.
- Usar archivos `.mp3`, `.wav`, `.ogg`, `.flac` y `.m4a`.
- Buscar por nombre, carpeta o etiqueta.
- Filtrar por carpeta, favoritos y sonidos recientes.
- Reproducir un sonido con un clic.
- Pausar, reanudar y cambiar de posición sin reiniciar el sonido.
- Ver el tiempo transcurrido y controlar cada sonido por separado en modo mezcla.
- Crear bancos de sonidos y ordenar sus elementos.
- Asignar atajos globales de teclado para sonidos, pausa y parada.
- Detener todo al instante.
- Elegir el dispositivo de salida de audio.
- Enviar audio a VB-Audio Virtual Cable.
- Usar modo exclusivo o mezclar varios sonidos a la vez.
- Cambiar entre modo claro y modo oscuro.
- Controlar la app desde una tablet o móvil en la misma red local.
- Proteger el mando remoto con un PIN opcional.

## Descargar y Ejecutar

1. Abre la [última GitHub Release](https://github.com/Ibai-Terron-Castrillo/Simple-Soundboard/releases/latest).
2. Descarga `LocalSoundboard-win-x64.zip`.
3. Extrae el ZIP en una carpeta, por ejemplo `C:\Apps\LocalSoundboard`.
4. Ejecuta `LocalSoundboard.exe`.

El ZIP de release es self-contained, así que no necesitas instalar .NET por separado.

## Primer Uso

1. Pulsa `Choose Folder`.
2. Selecciona la carpeta donde guardas tus sonidos.
3. Espera a que termine el escaneo.
4. Abre la pestaña `Audio` del panel derecho y elige un dispositivo de salida.
5. Ajusta el volumen principal.
6. Pulsa el icono de reproducción de cualquier sonido.

Si borras o mueves un archivo, refresca la biblioteca. LocalSoundboard lo mostrará como no disponible hasta que lo restaures o se actualice el estado guardado.

## Salida de Audio

Usa el selector de salida para decidir dónde suenan los audios.

Opciones habituales:

- `Windows default output`: reproduce por la salida actual de Windows.
- Tus altavoces o auriculares.
- `CABLE Input`: envía audio a VB-Audio Virtual Cable.

LocalSoundboard recuerda el dispositivo elegido. Si no está disponible al iniciar la app o recargar dispositivos, se selecciona la salida predeterminada de Windows. Si desaparece durante la reproducción, recarga dispositivos y elige una salida disponible.

## Modos de Reproducción

- `Exclusive`: al iniciar un sonido, se detiene el anterior.
- `Mix`: permite reproducir varios sonidos al mismo tiempo.

El modo exclusivo suele ser mejor para llamadas de voz. El modo mezcla va bien para efectos superpuestos.

## Controles de Reproducción

El reproductor inferior muestra cada sonido activo, el tiempo transcurrido y su duración.

- El icono de pausa detiene temporalmente un sonido; el de reproducción lo reanuda desde la misma posición.
- Arrastra la barra de progreso para avanzar o retroceder. Si el sonido estaba pausado, seguirá pausado.
- El cuadrado termina un sonido. Al reproducirlo otra vez, empezará desde el principio.
- Los controles de la esquina superior derecha del reproductor pausan, reanudan o detienen todos los sonidos.

En modo mezcla, iniciar otro sonido mantiene pausados los que ya lo estaban.

## Bancos de Sonidos

Los bancos son colecciones personalizadas. Nunca mueven ni borran los archivos originales.

1. Selecciona un sonido y abre la pestaña `Sound`.
2. Escribe un nombre de banco y pulsa el icono de suma para crearlo.
3. Selecciona el banco de destino y pulsa `Add Sound`.
4. Abre la colección desde el filtro `Bank` situado sobre la biblioteca.
5. Selecciona un sonido y usa las flechas de la pestaña `Sound` para cambiar su orden en ese banco.

Un sonido puede estar en varios bancos. El lápiz renombra el banco de destino; la papelera lo elimina. `Remove Sound` solo retira el sonido del banco seleccionado.

## Atajos Globales

1. Abre `Audio` y activa `Global shortcuts`.
2. Pulsa un campo de atajo e introduce una combinación que incluya Ctrl, Alt o la tecla Windows.
3. Configura ahí la pausa/reanudación y la parada globales.
4. Para asignar un atajo a un sonido, selecciónalo, abre `Sound`, introduce la combinación y pulsa `Save Sound`.

Escape limpia un campo de atajo. La X junto al atajo de un sonido también lo limpia; pulsa `Save Sound` para aplicar ese cambio. Los conflictos con Windows, otras aplicaciones u otros sonidos aparecen en `Audio`. Los atajos disponibles siguen funcionando aunque otro tenga un conflicto. Algunos juegos o aplicaciones elevadas pueden interceptarlos.

En `Sound` también puedes elegir un nombre visible y etiquetas. `Save Sound` guarda estos cambios sin renombrar el archivo original.

## Usarlo Con Discord

La configuración más común para Discord usa VB-Audio Virtual Cable.

1. Instala VB-Audio Virtual Cable.
2. Reinicia Windows si el instalador lo pide.
3. En LocalSoundboard, selecciona `CABLE Input` como dispositivo de salida.
4. En Discord, abre `User Settings > Voice & Video`.
5. Cambia el dispositivo de entrada a `CABLE Output`.
6. Prueba un sonido en LocalSoundboard.

Si también quieres que Discord escuche tu micrófono, necesitarás mezclar el micrófono y el cable virtual con una herramienta como VoiceMeeter o una configuración equivalente.

## Usarlo Con OBS

Puedes capturar el cable virtual o capturar el audio de escritorio.

### Opción 1: Cable Virtual

1. En LocalSoundboard, selecciona `CABLE Input`.
2. En OBS, añade una fuente `Audio Input Capture`.
3. Selecciona `CABLE Output`.
4. Ajusta volumen y filtros en OBS.

Esta opción es la recomendada si quieres tener la soundboard en su propio canal del mezclador.

### Opción 2: Audio de Escritorio

1. En LocalSoundboard, selecciona tu salida normal de Windows.
2. En OBS, captura el audio de escritorio.

Es más simple, pero da menos control.

## Mando Remoto Desde Tablet o Móvil

LocalSoundboard puede iniciar un pequeño servidor web local. Está pensado solo para redes privadas, como tu Wi-Fi de casa o estudio.

1. Abre la pestaña `Remote` y activa `Remote server`.
2. Deja el puerto `5050` o elige otro.
3. Opcional: configura un PIN.
4. Mira la URL que muestra la app, por ejemplo `http://192.168.1.34:5050`.
5. Abre esa URL en una tablet o móvil conectado a la misma red.

La página remota permite:

- Buscar sonidos.
- Reproducir sonidos.
- Parar toda la reproducción.
- Pausar, reanudar y cambiar la posición de cada sonido.
- Ver los cambios hechos desde el PC u otro dispositivo sin recargar.
- Elegir un banco de sonidos.
- Limpiar la búsqueda con la X del buscador.
- Filtrar favoritos.
- Marcar favoritos.
- Reescanear la biblioteca.

La página remota no sube ni transmite audio. El audio siempre se reproduce en el PC.

El indicador de conexión avisa si el PC se desconecta y la página intenta reconectar automáticamente. Activa `Start automatically` para que un servidor remoto habilitado vuelva a iniciarse al abrir la app. Al cerrar la app se detienen el audio y el servidor.

## Configuración

La configuración se guarda aquí:

```text
%APPDATA%\LocalSoundboard\settings.json
```

LocalSoundboard recuerda:

- Carpeta de biblioteca.
- Dispositivo de salida.
- Volumen.
- Favoritos.
- Sonidos recientes.
- Etiquetas.
- Nombres visibles, bancos, orden de los sonidos y atajos de teclado.
- Modo de reproducción.
- Preferencia de tema.
- Puerto del servidor remoto.
- Opción de inicio del servidor remoto.
- PIN remoto opcional.

## Solución de Problemas

### No Aparece VB-Audio Virtual Cable

1. Comprueba que VB-Audio Virtual Cable está instalado.
2. Reinicia Windows.
3. Abre `Audio` y pulsa `Reload Devices` en LocalSoundboard.
4. Busca un dispositivo parecido a `CABLE Input`.

### Discord No Escucha la Soundboard

1. Selecciona `CABLE Input` en LocalSoundboard.
2. Selecciona `CABLE Output` como entrada en Discord.
3. Comprueba que el volumen de LocalSoundboard no está a cero.
4. Ajusta temporalmente la cancelación de ruido o sensibilidad de entrada de Discord si corta sonidos cortos.

### La Tablet o el Móvil No Conecta

1. Asegúrate de que el PC y el dispositivo remoto están en la misma red.
2. Usa la IP que muestra LocalSoundboard, no `localhost`.
3. Permite LocalSoundboard en el Firewall de Windows si aparece el aviso.
4. Comprueba que el puerto elegido no está ocupado.

### Un Sonido No Reproduce

1. Comprueba que el archivo existe.
2. Prueba a abrirlo en otro reproductor.
3. Si está corrupto, conviértelo de nuevo a `.wav` o `.mp3`.
4. Para `.flac` y `.m4a`, el soporte puede depender de los codecs instalados en Windows.

## Desarrollo y Contribuciones

Las instrucciones para compilar, ejecutar pruebas, usar la API local y generar releases están en la [guía de desarrollo](docs/DEVELOPMENT.md), en inglés.
