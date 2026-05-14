# LocalSoundboard

**LocalSoundboard** is a Windows desktop soundboard for people who want to play local audio files into Discord, OBS, games, calls, streams, or any other audio workflow.

The app runs on your PC. Your tablet or phone can be used as a local remote control, but it never sends audio. It only sends commands to the PC, and the PC plays the selected sound.

Spanish version: [README.es.md](README.es.md)

## What You Can Do

- Pick a local folder with your sounds.
- Scan subfolders automatically.
- Use `.mp3`, `.wav`, `.ogg`, `.flac`, and `.m4a` files.
- Search sounds by name, folder, or tag.
- Filter by folder, favorites, and recently played sounds.
- Play a sound with one click.
- Stop everything instantly.
- Choose the audio output device.
- Send audio to VB-Audio Virtual Cable.
- Use exclusive playback or mix multiple sounds at once.
- Control the app from a phone or tablet on the same local network.
- Protect the remote control with an optional PIN.

## Download and Run

1. Open the latest GitHub Release.
2. Download `LocalSoundboard-win-x64.zip`.
3. Extract the ZIP to a folder such as `C:\Apps\LocalSoundboard`.
4. Run `LocalSoundboard.exe`.

The release ZIP is self-contained, so you do not need to install .NET separately.

## First Setup

1. Click `Choose Folder`.
2. Select the folder where your sound files are stored.
3. Wait for the scan to finish.
4. Pick an output device in the right panel.
5. Adjust the main volume.
6. Click `Play` on any sound.

If a file is deleted or moved, refresh the library. LocalSoundboard will show it as unavailable until it is restored or removed from the saved library state.

## Audio Output

Use the output selector to decide where sounds are played.

Common choices:

- `Windows default output`: plays through your current Windows output device.
- Your speakers or headphones.
- `CABLE Input`: sends audio into VB-Audio Virtual Cable.

LocalSoundboard remembers your selected output device. If the device is unplugged or removed, the app falls back to the Windows default output.

## Playback Modes

- `Exclusive`: starting a sound stops the previous one.
- `Mix`: several sounds can play at the same time.

Exclusive mode is usually better for voice calls. Mix mode is useful for layered effects.

## Use With Discord

For Discord, the most common setup is VB-Audio Virtual Cable.

1. Install VB-Audio Virtual Cable.
2. Restart Windows if the installer asks you to.
3. In LocalSoundboard, select `CABLE Input` as the output device.
4. In Discord, open `User Settings > Voice & Video`.
5. Set the input device to `CABLE Output`.
6. Test a sound in LocalSoundboard.

If you also want Discord to hear your microphone, you will need to mix your microphone and the virtual cable with a tool such as VoiceMeeter or an equivalent audio routing setup.

## Use With OBS

You can either capture the virtual cable or capture your desktop audio.

### Option 1: Virtual Cable

1. In LocalSoundboard, select `CABLE Input`.
2. In OBS, add an `Audio Input Capture` source.
3. Select `CABLE Output`.
4. Adjust the volume and filters in OBS.

This is the recommended option if you want the soundboard on its own mixer channel.

### Option 2: Desktop Audio

1. In LocalSoundboard, select your normal Windows output.
2. In OBS, capture desktop audio.

This is simpler, but gives you less control.

## Phone or Tablet Remote

LocalSoundboard can start a small local web server for remote control. This is designed for private local networks only, such as your home or studio Wi-Fi.

1. Enable `Remote server`.
2. Keep port `5050` or choose another port.
3. Optional: set a PIN.
4. Look at the URL shown in the app, such as `http://192.168.1.34:5050`.
5. Open that URL on a phone or tablet connected to the same network.

The remote page lets you:

- Search sounds.
- Play sounds.
- Stop all playback.
- Filter favorites.
- Mark favorites.
- Rescan the library.

The remote page does not upload or stream audio. Audio always plays on the PC.

## Local API

The remote control uses these endpoints:

- `GET /api/status`
- `GET /api/sounds`
- `POST /api/play/{id}`
- `POST /api/stop`
- `POST /api/favorite/{id}`
- `POST /api/rescan`

If a PIN is configured, API requests except `/api/status` must include:

```http
X-Soundboard-Pin: 1234
```

The API cannot play arbitrary file paths, delete files, or access audio outside the selected library folder.

## Settings Location

Settings are saved here:

```text
%APPDATA%\LocalSoundboard\settings.json
```

LocalSoundboard remembers:

- Library folder.
- Output device.
- Volume.
- Favorites.
- Recently played sounds.
- Tags.
- Playback mode.
- Remote server port.
- Remote server startup setting.
- Optional remote PIN.

## Troubleshooting

### VB-Audio Virtual Cable Does Not Appear

1. Make sure VB-Audio Virtual Cable is installed.
2. Restart Windows.
3. Click `Refresh devices` in LocalSoundboard.
4. Look for a device named similar to `CABLE Input`.

### Discord Cannot Hear the Soundboard

1. Select `CABLE Input` in LocalSoundboard.
2. Select `CABLE Output` as the Discord input device.
3. Check that LocalSoundboard volume is not muted.
4. Temporarily adjust Discord noise suppression or input sensitivity if short sounds are being cut off.

### The Phone or Tablet Cannot Connect

1. Make sure the PC and the remote device are on the same network.
2. Use the IP address shown in LocalSoundboard, not `localhost`.
3. Allow LocalSoundboard through Windows Firewall if prompted.
4. Check that the selected port is not already in use.

### A Sound Does Not Play

1. Check that the file still exists.
2. Try opening it in another media player.
3. If the file is corrupt, convert it again to `.wav` or `.mp3`.
4. For `.flac` and `.m4a`, Windows media support may depend on installed codecs.

## Build From Source

Requirements:

- Windows 10 or Windows 11.
- .NET SDK 8 or later.

```powershell
dotnet restore LocalSoundboard.sln
dotnet build LocalSoundboard.sln -c Release
dotnet run --project LocalSoundboard/LocalSoundboard.csproj
```

## Create a Portable ZIP

```powershell
dotnet publish LocalSoundboard/LocalSoundboard.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o artifacts/publish `
  /p:PublishSingleFile=false

Compress-Archive -Path artifacts/publish/* -DestinationPath artifacts/LocalSoundboard-win-x64.zip -Force
```

## Release Builds

The GitHub Actions workflow at `.github/workflows/release.yml` builds a Windows x64 portable ZIP.

To create a release:

```powershell
git tag v0.1.0
git push origin v0.1.0
```

When the tag workflow succeeds, GitHub creates a Release and attaches `LocalSoundboard-win-x64.zip`.
