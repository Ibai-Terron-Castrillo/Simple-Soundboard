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
- Pause, resume, and seek without restarting a sound.
- See elapsed time and control each sound separately in mix mode.
- Organize sounds into custom banks and reorder them.
- Assign global keyboard shortcuts, including pause and stop.
- Stop everything instantly.
- Choose the audio output device.
- Send audio to VB-Audio Virtual Cable.
- Use exclusive playback or mix multiple sounds at once.
- Switch between light mode and dark mode.
- Control the app from a phone or tablet on the same local network.
- Protect the remote control with an optional PIN.

## Download and Run

1. Open the [latest GitHub Release](https://github.com/Ibai-Terron-Castrillo/Simple-Soundboard/releases/latest).
2. Download `LocalSoundboard-win-x64.zip`.
3. Extract the ZIP to a folder such as `C:\Apps\LocalSoundboard`.
4. Run `LocalSoundboard.exe`.

The release ZIP is self-contained, so you do not need to install .NET separately.

## First Setup

1. Click `Choose Folder`.
2. Select the folder where your sound files are stored.
3. Wait for the scan to finish.
4. Open the `Audio` tab on the right and pick an output device.
5. Adjust the main volume.
6. Click the play icon beside any sound.

If a file is deleted or moved, refresh the library. LocalSoundboard will show it as unavailable until it is restored or removed from the saved library state.

## Audio Output

Use the output selector to decide where sounds are played.

Common choices:

- `Windows default output`: plays through your current Windows output device.
- Your speakers or headphones.
- `CABLE Input`: sends audio into VB-Audio Virtual Cable.

LocalSoundboard remembers your selected output device. If it is missing when the app starts or you reload devices, the app falls back to the Windows default output. If it disappears during playback, reload devices and select an available output.

## Playback Modes

- `Exclusive`: starting a sound stops the previous one.
- `Mix`: several sounds can play at the same time.

Exclusive mode is usually better for voice calls. Mix mode is useful for layered effects.

## Playback Controls

The player at the bottom shows every active sound, its elapsed time, and its duration.

- Use the pause icon to pause a sound and the play icon to resume from the same position.
- Drag a sound's progress slider to move forward or backward. Seeking while paused keeps it paused.
- Use the square stop icon to finish a sound. Playing it again starts from the beginning.
- The controls at the top right of the player pause/resume or stop all sounds.

In mix mode, starting another sound leaves paused sounds paused. Each sound has its own controls.

## Sound Banks

Banks are custom collections; they never move or delete your audio files.

1. Select a sound and open the `Sound` tab.
2. Enter a bank name and click the plus icon to create it.
3. Select the target bank and click `Add Sound`.
4. Use the `Bank` filter above the library to open a collection.
5. Select a sound and use the up/down arrows in the `Sound` tab to change its order in that bank.

You can add a sound to several banks. The pencil and trash icons rename or remove the selected target bank. `Remove Sound` only removes membership in that bank.

## Global Shortcuts

1. Open `Audio` and enable `Global shortcuts`.
2. Click a shortcut field and press a combination containing Ctrl, Alt, or the Windows key.
3. Set the global pause/resume and stop shortcuts there.
4. To assign a sound shortcut, select the sound, open `Sound`, capture the shortcut, and click `Save Sound`.

Escape clears a shortcut field. The X beside a sound shortcut also clears it; click `Save Sound` to apply that change. Conflicts with Windows, other apps, or another sound are shown in the `Audio` tab. Available shortcuts continue working if another shortcut conflicts. Some games or elevated apps may intercept shortcuts.

The `Sound` tab also lets you set a display name and tags. Click `Save Sound` to remember them. This does not rename the original file.

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

1. Open the `Remote` tab and enable `Remote server`.
2. Keep port `5050` or choose another port.
3. Optional: set a PIN.
4. Use the `Tablet / phone address`, such as `http://192.168.1.34:5050`. If the PC has several network adapters, select the address for the Wi-Fi or Ethernet network shared with your tablet.
5. Open that URL on a phone or tablet connected to the same network.

`This PC` is a separate address for testing on the computer itself. Do not use `localhost` on your tablet: it points to the tablet, not the PC. If you change networks, use the refresh icon next to the tablet address. An inactive server has no tablet address; enable `Remote server` first.

The remote page lets you:

- Search sounds.
- Play sounds.
- Stop all playback.
- Pause, resume, and seek individual sounds or pause/resume everything.
- See playback changes made on the PC or another remote device automatically.
- Select a sound bank.
- Clear the search with the X inside the search field.
- Filter favorites.
- Mark favorites.
- Rescan the library.

The remote page does not upload or stream audio. Audio always plays on the PC.

The connection indicator shows when the PC disconnects and the page tries to reconnect automatically. Enable `Start automatically` if you want an enabled remote server to start again next time you open the PC app. Closing the PC app stops playback and the remote server.

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
- Display names, sound banks, bank order, and keyboard shortcuts.
- Playback mode.
- Theme preference.
- Remote server port.
- Remote server startup setting.
- Optional remote PIN.

## Troubleshooting

### VB-Audio Virtual Cable Does Not Appear

1. Make sure VB-Audio Virtual Cable is installed.
2. Restart Windows.
3. Open `Audio` and click `Reload Devices` in LocalSoundboard.
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
5. If the page opens on the PC but not on the tablet, check that Windows Firewall allows LocalSoundboard on your private network. Do not disable the firewall or open a port on your router.

### A Sound Does Not Play

1. Check that the file still exists.
2. Try opening it in another media player.
3. If the file is corrupt, convert it again to `.wav` or `.mp3`.
4. For `.flac` and `.m4a`, Windows media support may depend on installed codecs.
