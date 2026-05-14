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
- Detener todo al instante.
- Elegir el dispositivo de salida de audio.
- Enviar audio a VB-Audio Virtual Cable.
- Usar modo exclusivo o mezclar varios sonidos a la vez.
- Controlar la app desde una tablet o móvil en la misma red local.
- Proteger el mando remoto con un PIN opcional.

## Descargar y Ejecutar

1. Abre la última GitHub Release.
2. Descarga `LocalSoundboard-win-x64.zip`.
3. Extrae el ZIP en una carpeta, por ejemplo `C:\Apps\LocalSoundboard`.
4. Ejecuta `LocalSoundboard.exe`.

El ZIP de release es self-contained, así que no necesitas instalar .NET por separado.

## Primer Uso

1. Pulsa `Choose Folder`.
2. Selecciona la carpeta donde guardas tus sonidos.
3. Espera a que termine el escaneo.
4. Elige un dispositivo de salida en el panel derecho.
5. Ajusta el volumen principal.
6. Pulsa `Play` en cualquier sonido.

Si borras o mueves un archivo, refresca la biblioteca. LocalSoundboard lo mostrará como no disponible hasta que lo restaures o se actualice el estado guardado.

## Salida de Audio

Usa el selector de salida para decidir dónde suenan los audios.

Opciones habituales:

- `Windows default output`: reproduce por la salida actual de Windows.
- Tus altavoces o auriculares.
- `CABLE Input`: envía audio a VB-Audio Virtual Cable.

LocalSoundboard recuerda el dispositivo elegido. Si el dispositivo se desconecta o desaparece, la app vuelve a la salida predeterminada de Windows.

## Modos de Reproducción

- `Exclusive`: al iniciar un sonido, se detiene el anterior.
- `Mix`: permite reproducir varios sonidos al mismo tiempo.

El modo exclusivo suele ser mejor para llamadas de voz. El modo mezcla va bien para efectos superpuestos.

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

1. Activa `Remote server`.
2. Deja el puerto `5050` o elige otro.
3. Opcional: configura un PIN.
4. Mira la URL que muestra la app, por ejemplo `http://192.168.1.34:5050`.
5. Abre esa URL en una tablet o móvil conectado a la misma red.

La página remota permite:

- Buscar sonidos.
- Reproducir sonidos.
- Parar toda la reproducción.
- Filtrar favoritos.
- Marcar favoritos.
- Reescanear la biblioteca.

La página remota no sube ni transmite audio. El audio siempre se reproduce en el PC.

## API Local

El mando remoto usa estos endpoints:

- `GET /api/status`
- `GET /api/sounds`
- `POST /api/play/{id}`
- `POST /api/stop`
- `POST /api/favorite/{id}`
- `POST /api/rescan`

Si hay un PIN configurado, las peticiones a la API salvo `/api/status` deben incluir:

```http
X-Soundboard-Pin: 1234
```

La API no puede reproducir rutas arbitrarias, borrar archivos ni acceder a audio fuera de la biblioteca seleccionada.

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
- Modo de reproducción.
- Puerto del servidor remoto.
- Opción de inicio del servidor remoto.
- PIN remoto opcional.

## Solución de Problemas

### No Aparece VB-Audio Virtual Cable

1. Comprueba que VB-Audio Virtual Cable está instalado.
2. Reinicia Windows.
3. Pulsa `Refresh devices` en LocalSoundboard.
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

## Compilar Desde Código

Requisitos:

- Windows 10 u 11.
- .NET SDK 8 o superior.

```powershell
dotnet restore LocalSoundboard.sln
dotnet build LocalSoundboard.sln -c Release
dotnet run --project LocalSoundboard/LocalSoundboard.csproj
```

## Crear un ZIP Portable

```powershell
dotnet publish LocalSoundboard/LocalSoundboard.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o artifacts/publish `
  /p:PublishSingleFile=false

Compress-Archive -Path artifacts/publish/* -DestinationPath artifacts/LocalSoundboard-win-x64.zip -Force
```
