# Product Improvement Ideas

These ideas are intentionally kept out of the user README. They are product and engineering notes for future versions.

## High Value

- Global hotkeys for triggering sounds without focusing the app.
- Sound banks or pages for organizing large collections.
- Per-sound volume and playback behavior.
- Import/export of settings and favorites.
- A compact performance mode for streamers with only large sound buttons.
- A first-run setup screen that explains output devices and remote control.

## Audio Workflow

- Optional microphone + soundboard mixing helper documentation.
- Output health checks when the selected audio device disappears.
- File validation during scan, with clearer corrupt-file warnings.
- Optional fade-out when stopping long sounds.

## Remote Control

- Larger tablet layouts for landscape mode.
- Favorite-only remote page shortcut.
- Optional QR code for the remote URL.
- Better PIN entry flow with a visible locked state.

## UX Polish

- Empty states for no folder, no results, and no favorites.
- Keyboard navigation in the desktop list.
- Better tag editing with chips.
- Remember window size and position.
- Theme support after the visual language is stable.

## Engineering

- Unit tests for library scanning, settings normalization, and route authorization.
- Integration tests for the Minimal API using a fake soundboard controller.
- CI build matrix for Debug build, Release build, and publish package.
- Structured logging for playback and remote server errors.
