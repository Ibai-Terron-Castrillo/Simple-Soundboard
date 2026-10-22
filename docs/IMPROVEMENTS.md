# Product Improvement Ideas

Product and engineering notes are kept here, separate from the user guides.

## Implemented for 0.3.0

- Reliable playback results and a single audio state shared with desktop and remote controls.
- Tested pause/resume, seeking, independent mixed sessions, and output/file disposal.
- Remote lifecycle fixes, stream cancellation, and normal application shutdown.
- Persistent player with elapsed time, duration, and per-session controls.
- Sound banks with add/remove, rename/delete, and custom order.
- Display names that do not rename files.
- Optional global shortcuts with Windows registration and conflict reporting.
- Live remote synchronization, reconnect state, bank filters, and clear search.
- Desktop layout tests and regression tests before release packaging.

## Next Audio Features

- Pre-listen through headphones independently of the main virtual-cable output.
- Per-sound gain, repeat, and non-destructive start/end trim.
- Optional fade-out and a peak meter or limiter.
- Stable Windows endpoint IDs and device-disconnect notifications.

## Organization and Remote UX

- Drag-and-drop ordering and optional bank colors.
- A compact board layout with large performance pads.
- QR code for the LAN URL and a stronger paired-device login flow.
- Import/export of banks, shortcuts, favorites, and settings.
- Remember window placement and optional minimize-to-tray behavior.
- A first-run audio-routing wizard and optional UI language selection.

## Engineering

- Structured, rotating local logs for decoder and device failures.
- Benchmarks with large libraries and an incremental directory watcher.
- Additional physical-device and codec coverage across Windows versions.
- Installer, code signing, and an opt-in update check.
