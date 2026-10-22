# Development

Windows 10/11 and .NET SDK 8 or later are required. The desktop app uses WPF, NAudio, local JSON settings, and an embedded ASP.NET Core server. The remote UI uses plain HTML/CSS/JavaScript embedded in the executable.

## Build and Test

```powershell
dotnet restore LocalSoundboard.sln
dotnet build LocalSoundboard.sln -c Release
dotnet test LocalSoundboard.Tests/LocalSoundboard.Tests.csproj -c Release --no-build
dotnet run --project LocalSoundboard/LocalSoundboard.csproj
```

The automated suite covers playback position, paused seeking, mixed sessions, exclusive starts, failed decoders/drivers, file release, settings migration, bank ordering, hotkey registration/conflicts, remote lifecycle, PIN enforcement, and streaming shutdown. Driver-independent audio tests use NAudio's actual WAV reader and a controllable output clock.

Desktop layout tests render light/dark PNGs at minimum and desktop sizes into `artifacts/ui-tests/`.

## Manual Browser Smoke Test

With Playwright and its Chromium browser installed:

```powershell
dotnet build LocalSoundboard/LocalSoundboard.csproj -c Debug
./scripts/Start-SmokeApp.ps1
node ./scripts/WebSmoke.cjs
```

`PLAYWRIGHT_MODULE_PATH` can point at a bundled Playwright module. `PLAYWRIGHT_CHROMIUM_EXECUTABLE` can select an existing Chromium executable. The smoke test checks four viewport sizes, both themes, search/clear, favorites, bank filtering, failed playback, and live multi-client pause/seek/resume when an audio output is available.

The launcher uses isolated settings and silent WAV files at zero volume. It writes the started process ID and URL to `artifacts/smoke/current.json`. Close that test instance normally after testing. It does not change the user's real settings.

## Project Layout

| Path | Purpose |
| --- | --- |
| `LocalSoundboard/MainWindow.xaml` | Desktop workspace and transport |
| `LocalSoundboard/Themes/Controls.xaml` | Shared themed controls |
| `LocalSoundboard/ViewModels/` | Desktop state, playback views, banks and shortcuts |
| `LocalSoundboard/Services/PlaybackService.cs` | Serialized audio session ownership and transport |
| `LocalSoundboard/Services/RemoteControlServer.cs` | LAN API and event stream |
| `LocalSoundboard/Web/` | Embedded remote UI |
| `LocalSoundboard.Tests/` | Playback, desktop, settings, and HTTP regression tests |
| `scripts/` | Isolated app/browser smoke checks |

## Local API

All commands operate on library sound IDs or active session IDs. There is no endpoint to accept arbitrary file paths, upload audio, or delete files.

| Endpoint | Purpose |
| --- | --- |
| `GET /api/status` | Library, theme, and current playback snapshot |
| `GET /api/sounds?search=...&favorites=true&bank=...` | Filter library sounds |
| `GET /api/banks` | Bank IDs and names |
| `GET /api/events` | Server-sent playback/theme/library revision events |
| `POST /api/play/{soundId}` | Start a sound; failures return HTTP 422 |
| `POST /api/pause`, `POST /api/resume`, `POST /api/stop` | Global transport |
| `POST /api/sessions/{sessionId}/pause` | Pause one session |
| `POST /api/sessions/{sessionId}/resume` | Resume one session |
| `POST /api/sessions/{sessionId}/stop` | Stop one session |
| `POST /api/sessions/{sessionId}/seek` | Seek with JSON `{ "seconds": 15 }` |
| `POST /api/favorite/{soundId}` | Toggle favorite |
| `POST /api/rescan` | Refresh library |

If configured, send `X-Soundboard-Pin` on all API requests except `/api/status`, including event streams. The web UI uses streaming fetch so the PIN stays in the header. Streams close if their PIN is no longer valid. POST requests with an unrelated browser origin are rejected. HTTP is intended for a trusted private LAN, with no internet port forwarding.

Playback snapshots contain `sessions`, `hasPlayback`, and `isPaused`. Each session contains `id`, `soundId`, `name`, `isPaused`, `positionSeconds`, and `durationSeconds`. Global `isPaused` is true only when all active sessions are paused. Starting a sound in mix mode does not resume other sessions. Seeking recreates that session's output to flush old buffers while preserving its identity and paused state.

## Portable Package and Releases

```powershell
dotnet publish LocalSoundboard/LocalSoundboard.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish /p:DebugType=None /p:DebugSymbols=false
Compress-Archive -Path artifacts/publish/* -DestinationPath artifacts/LocalSoundboard-win-x64.zip -Force
```

The ZIP contains the app, .NET runtime, ASP.NET Core runtime, and dependencies. Extract it before running `LocalSoundboard.exe`.

The CI workflow builds and tests changes on `develop`, `main`, and pull requests. The release workflow also runs tests before publishing. After promoting a verified commit to `main`, create and push an unused version tag such as `v0.3.0`. The release workflow creates the GitHub Release and attaches `LocalSoundboard-win-x64.zip`. `workflow_dispatch` builds a downloadable Actions artifact without creating a Release.

## Current Limits

- Windows output device names may be truncated or duplicated by the legacy waveOut API; unique endpoint IDs remain a future improvement.
- Playback supports local files. FLAC/M4A decoding can depend on Windows codecs.
- No microphone mixing, pre-listen output, non-destructive trim, or per-sound gain yet.
- Banks use add/remove and up/down controls; drag-and-drop and color customization are not implemented.
- Global shortcuts require Ctrl, Alt, or Windows and can conflict with OS/game shortcuts.
- Settings are JSON, scoped to the Windows user, rather than an exported portable library.

Product ideas belong in [IMPROVEMENTS.md](IMPROVEMENTS.md), separate from the user guides.
