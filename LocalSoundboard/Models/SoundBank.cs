namespace LocalSoundboard.Models;

public sealed class SoundBank
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public List<string> SoundPaths { get; set; } = [];
    public override string ToString() => Name;
}

public sealed record SoundBankSnapshot(string Id, string Name, IReadOnlyList<string> SoundIds);
