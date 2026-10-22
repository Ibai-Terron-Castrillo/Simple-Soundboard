using LocalSoundboard.Models;

namespace LocalSoundboard.Services;

public interface IRemoteSoundboardController
{
    string LibraryStatusText { get; }

    int AvailableSoundCount { get; }

    int TotalSoundCount { get; }

    bool IsDarkMode { get; }

    bool IsPlaybackPaused { get; }

    string RemotePin { get; }

    long LibraryRevision { get; }

    PlaybackSnapshot GetPlaybackSnapshot();

    IReadOnlyList<SoundBankSnapshot> GetBankSnapshot();

    Task<bool> SeekFromRemoteAsync(Guid sessionId, double seconds);

    bool StopSession(Guid sessionId);

    bool SetSessionPaused(Guid sessionId, bool paused);

    IReadOnlyList<SoundItem> GetSoundSnapshot();

    Task<bool> PlayByIdAsync(string id);

    void StopAll();

    void PauseAll();

    void ResumeAll();

    Task<bool> ToggleFavoriteByIdAsync(string id);

    Task RescanFromRemoteAsync();
}
