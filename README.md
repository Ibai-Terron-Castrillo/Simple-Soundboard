# LocalSoundboard

LocalSoundboard es una aplicacion de escritorio para Windows pensada para usar tus propios archivos de audio como soundboard en Discord, OBS, videojuegos o llamadas de voz.

La idea es sencilla: el PC reproduce el audio, y opcionalmente una tablet o movil en la misma red local funciona como mando remoto. La tablet no envia audio ni necesita tener los archivos.

## Funciones principales

- Biblioteca local desde una carpeta de audios.
- Escaneo recursivo de `.mp3`, `.wav`, `.ogg`, `.flac` y `.m4a`.
- Busqueda rapida por nombre, carpeta o etiqueta.
- Filtros por carpeta, favoritos y recientes.
- Favoritos persistentes.
- Etiquetas simples separadas por coma.
- Reproduccion en modo exclusivo o modo mezcla.
- Boton `Stop All`.
- Volumen general.
- Selector de dispositivo de salida, incluido VB-Audio Virtual Cable si esta instalado.
- Servidor remoto local opcional para controlar la soundboard desde tablet/movil.
- PIN opcional para proteger el mando remoto dentro de la red local.
- Configuracion persistente en JSON local.
- Workflow de GitHub Actions para generar un ZIP portable Windows x64.

## Instalacion rapida

1. Descarga el ZIP `LocalSoundboard-win-x64.zip` desde GitHub Releases.
2. Extrae el ZIP en una carpeta, por ejemplo `C:\Apps\LocalSoundboard`.
3. Ejecuta `LocalSoundboard.exe`.
4. Pulsa `Carpeta` y selecciona la carpeta donde guardas tus audios.
5. Elige el dispositivo de salida y ajusta el volumen.

No hace falta instalar .NET si usas el ZIP generado por el workflow, porque el paquete es self-contained.

## Uso basico

1. Abre LocalSoundboard.
2. Pulsa `Carpeta`.
3. Selecciona una carpeta principal de audios.
4. Usa `Buscar` para encontrar sonidos al instante.
5. Pulsa `Play` para reproducir.
6. Pulsa la estrella para marcar favoritos.
7. Usa el filtro `Vista` para alternar entre todos, favoritos y recientes.
8. Usa `Stop All` para detener cualquier reproduccion activa.

Si borras o mueves un archivo, al refrescar la biblioteca se marcara como no disponible.

## Dispositivos de salida

En el panel `Salida de audio` puedes elegir donde suena la soundboard.

Opciones comunes:

1. `Salida predeterminada de Windows`: reproduce por el dispositivo actual del sistema.
2. Altavoces o auriculares concretos.
3. `CABLE Input` de VB-Audio Virtual Cable, si lo tienes instalado.

El dispositivo elegido se recuerda para futuras ejecuciones. Si el dispositivo ya no existe, la app vuelve a la salida predeterminada.

## Modo exclusivo y modo mezcla

- `Exclusivo`: al reproducir un sonido, se corta el anterior.
- `Mezcla`: permite varios sonidos a la vez.

Para directos y llamadas suele ser mas limpio empezar con `Exclusivo`. Para efectos superpuestos, usa `Mezcla`.

## Usarlo con Discord

La forma mas flexible es usar VB-Audio Virtual Cable.

1. Instala VB-Audio Virtual Cable desde su web oficial.
2. Reinicia Windows si el instalador lo pide.
3. En LocalSoundboard, elige `CABLE Input` como dispositivo de salida.
4. En Discord, ve a `Ajustes de usuario > Voz y video`.
5. En `Dispositivo de entrada`, elige `CABLE Output`.
6. Ajusta volumen y sensibilidad en Discord.

Importante: con esta configuracion Discord escucha lo que LocalSoundboard envia al cable virtual. Si tambien quieres que escuche tu micro, necesitaras mezclar micro + cable con una herramienta como VoiceMeeter o una configuracion equivalente.

## Usarlo con OBS

Tienes dos opciones habituales.

### Opcion A: capturar el dispositivo virtual

1. En LocalSoundboard, elige `CABLE Input`.
2. En OBS, crea una fuente `Captura de entrada de audio`.
3. Selecciona `CABLE Output`.
4. Ajusta filtros, monitorizacion y volumen en OBS.

### Opcion B: reproducir por el dispositivo de escritorio

1. En LocalSoundboard, elige tu salida normal de Windows.
2. En OBS, captura el audio de escritorio.

La opcion A da mas control porque la soundboard queda en una pista separada.

## Mando remoto desde tablet o movil

LocalSoundboard puede levantar un servidor local. Esta pensado solo para redes privadas, por ejemplo tu Wi-Fi de casa o estudio.

1. Activa `Servidor remoto`.
2. Deja el puerto `5050` o elige otro.
3. Opcional: escribe un PIN.
4. Mira la URL que aparece en la app, por ejemplo `http://192.168.1.34:5050`.
5. Abre esa URL desde la tablet o movil conectado a la misma red.

Desde la web remota puedes:

- Ver sonidos.
- Buscar.
- Reproducir.
- Parar todo.
- Ver favoritos.
- Refrescar biblioteca.
- Marcar favoritos.

La web no transmite audio. Solo manda comandos al PC, y el PC reproduce el sonido.

## API local

Endpoints disponibles:

- `GET /api/status`
- `GET /api/sounds`
- `POST /api/play/{id}`
- `POST /api/stop`
- `POST /api/favorite/{id}`
- `POST /api/rescan`

Si configuras PIN, las rutas de API salvo `/api/status` esperan la cabecera:

```http
X-Soundboard-Pin: 1234
```

La API no permite reproducir rutas arbitrarias, borrar archivos ni acceder a archivos fuera de la biblioteca seleccionada.

## Configuracion persistente

La configuracion se guarda en:

```text
%APPDATA%\LocalSoundboard\settings.json
```

Se recuerdan:

- Carpeta de biblioteca.
- Dispositivo de salida.
- Volumen.
- Favoritos.
- Recientes.
- Etiquetas.
- Modo de reproduccion.
- Puerto remoto.
- Estado de servidor remoto.
- PIN remoto opcional.

## Compilar desde codigo

Requisitos:

- Windows 10/11.
- .NET SDK 8 o superior.

Comandos:

```powershell
dotnet restore LocalSoundboard.sln
dotnet build LocalSoundboard.sln -c Release
dotnet run --project LocalSoundboard/LocalSoundboard.csproj
```

## Generar ZIP portable localmente

```powershell
dotnet publish LocalSoundboard/LocalSoundboard.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o artifacts/publish `
  /p:PublishSingleFile=false

Compress-Archive -Path artifacts/publish/* -DestinationPath artifacts/LocalSoundboard-win-x64.zip -Force
```

## Release en GitHub

El workflow `.github/workflows/release.yml` genera el ZIP portable.

Puedes lanzarlo de dos formas:

1. Desde la pestaña `Actions`, ejecutando `Build Windows Release`.
2. Creando y subiendo un tag:

```powershell
git tag v0.1.0
git push origin v0.1.0
```

Cuando el workflow se ejecuta por tag, tambien crea una GitHub Release y adjunta `LocalSoundboard-win-x64.zip`.

## Estructura del proyecto

```text
LocalSoundboard/
  Infrastructure/   Comandos y base observable para WPF
  Models/           Configuracion, sonidos y dispositivos
  Services/         Escaneo, audio, settings y servidor remoto
  ViewModels/       Logica de la ventana principal
  App.xaml          Arranque WPF
  MainWindow.xaml   Interfaz de escritorio
.github/workflows/
  release.yml       Build y release Windows x64
```

## Solucion de problemas

### No aparece VB-Audio Virtual Cable

1. Comprueba que VB-Audio Virtual Cable esta instalado.
2. Reinicia Windows.
3. Pulsa el boton de refrescar dispositivos en LocalSoundboard.
4. Busca un dispositivo llamado parecido a `CABLE Input`.

### Discord no recibe sonido

1. En LocalSoundboard selecciona `CABLE Input`.
2. En Discord selecciona `CABLE Output` como entrada.
3. Desactiva temporalmente cancelacion de ruido o sensibilidad automatica si corta sonidos cortos.
4. Revisa que el volumen de LocalSoundboard no este a cero.

### La tablet no conecta

1. Asegurate de que PC y tablet estan en la misma red.
2. Usa la IP que muestra LocalSoundboard, no `localhost`.
3. Comprueba que el firewall de Windows permite la aplicacion.
4. Verifica que el puerto no esta ocupado por otra app.

### Un archivo no reproduce

1. Comprueba que el archivo existe.
2. Prueba a abrirlo con otro reproductor.
3. Si esta corrupto, conviertelo de nuevo a `.wav` o `.mp3`.
4. Si es `.flac` o `.m4a`, Windows debe tener soporte Media Foundation para ese formato.

## Limitaciones actuales

- No incluye instalador MSI, solo ZIP portable.
- El servidor remoto no esta pensado para Internet, solo LAN privada.
- No hay mezclador avanzado por sonido individual.
- No hay atajos globales de teclado todavia.
- Las etiquetas son simples y se editan como texto separado por comas.
