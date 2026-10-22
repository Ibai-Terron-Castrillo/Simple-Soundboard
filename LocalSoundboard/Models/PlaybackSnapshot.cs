namespace LocalSoundboard.Models;

public sealed record PlaybackResult(bool Success, string? Error = null);

public sealed record PlaybackSessionSnapshot(Guid Id, string SoundId, string Name,
    bool IsPaused, double PositionSeconds, double DurationSeconds);

public sealed record PlaybackSnapshot(IReadOnlyList<PlaybackSessionSnapshot> Sessions)
{
    public bool HasPlayback => Sessions.Count > 0;
    public bool IsPaused => HasPlayback && Sessions.All(session => session.IsPaused);
}
