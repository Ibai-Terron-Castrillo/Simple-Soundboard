namespace LocalSoundboard.Models;

public sealed class AppSettings
{
    public string LibraryPath { get; set; } = string.Empty;

    public string OutputDeviceName { get; set; } = string.Empty;

    public int? OutputDeviceNumber { get; set; }

    public double Volume { get; set; } = 0.85;

    public PlaybackMode PlaybackMode { get; set; } = PlaybackMode.Exclusive;

    public int ServerPort { get; set; } = 5050;

    public bool RemoteServerEnabled { get; set; }

    public bool StartRemoteServerAutomatically { get; set; }

    public string RemotePin { get; set; } = string.Empty;

    public List<string> FavoritePaths { get; set; } = [];

    public List<string> RecentPaths { get; set; } = [];

    public List<SoundMetadata> Sounds { get; set; } = [];
}
