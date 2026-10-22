param([string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$workspace = Split-Path $PSScriptRoot -Parent
$testRoot = Join-Path $workspace ('artifacts/smoke/' + [guid]::NewGuid().ToString('N'))
$library = Join-Path $testRoot 'audio'
$settingsDirectory = Join-Path $testRoot 'appdata/LocalSoundboard'
New-Item -ItemType Directory -Force -Path $library, $settingsDirectory | Out-Null
foreach ($name in @('Stream intro', 'Ambient')) {
    $path = Join-Path $library ($name + '.wav')
    $writer = [System.IO.BinaryWriter]::new([System.IO.File]::Create($path))
    try {
        $dataSize = 8000 * 2 * 120
        $writer.Write([Text.Encoding]::ASCII.GetBytes('RIFF'))
        $writer.Write([int](36 + $dataSize))
        $writer.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt '))
        $writer.Write([int]16)
        $writer.Write([int16]1)
        $writer.Write([int16]1)
        $writer.Write([int]8000)
        $writer.Write([int]16000)
        $writer.Write([int16]2)
        $writer.Write([int16]16)
        $writer.Write([Text.Encoding]::ASCII.GetBytes('data'))
        $writer.Write([int]$dataSize)
        $writer.Write([byte[]]::new($dataSize))
    } finally { $writer.Dispose() }
}
[System.IO.File]::WriteAllText((Join-Path $library 'Corrupt.wav'), 'Not an audio file')
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = $listener.LocalEndpoint.Port
$listener.Stop()
@{
    LibraryPath = $library; IsDarkMode = $true; Volume = 0;
    PlaybackMode = 'Mix'; ServerPort = $port;
    RemoteServerEnabled = $true; StartRemoteServerAutomatically = $true;
    FavoritePaths = @((Join-Path $library 'Stream intro.wav'));
    Banks = @(@{ Id = 'live'; Name = 'Live'; SoundPaths = @((Join-Path $library 'Ambient.wav'), (Join-Path $library 'Stream intro.wav')) })
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $settingsDirectory 'settings.json') -Encoding utf8
$previousOverride = $env:LOCAL_SOUNDBOARD_APPDATA
try {
    $env:LOCAL_SOUNDBOARD_APPDATA = Join-Path $testRoot 'appdata'
    $exe = Join-Path $workspace "LocalSoundboard/bin/$Configuration/net8.0-windows/LocalSoundboard.exe"
    $appProcess = Start-Process -FilePath $exe -WindowStyle Hidden -PassThru
} finally { $env:LOCAL_SOUNDBOARD_APPDATA = $previousOverride }
$baseUrl = "http://127.0.0.1:$port"
$ready = $false
for ($attempt = 0; $attempt -lt 40; $attempt++) {
    Start-Sleep -Milliseconds 250
    if ($appProcess.HasExited) { throw "App exited during startup: $($appProcess.ExitCode)" }
    try { Invoke-RestMethod "$baseUrl/api/status" -TimeoutSec 1 | Out-Null; $ready = $true; break } catch { }
}
if (-not $ready) { $appProcess.Kill(); throw 'App server did not start.' }
$result = @{ processId = $appProcess.Id; baseUrl = $baseUrl; testRoot = $testRoot }
$result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $workspace 'artifacts/smoke/current.json')
$result | ConvertTo-Json
