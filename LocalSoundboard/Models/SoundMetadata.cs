namespace LocalSoundboard.Models;

public sealed class SoundMetadata
{
    public string Path { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    public DateTimeOffset? LastPlayedUtc { get; set; }

    public string Alias { get; set; } = string.Empty;

    public string Hotkey { get; set; } = string.Empty;
}
