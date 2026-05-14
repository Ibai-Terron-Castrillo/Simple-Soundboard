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

    IReadOnlyList<SoundItem> GetSoundSnapshot();

    Task<bool> PlayByIdAsync(string id);

    void StopAll();

    void PauseAll();

    void ResumeAll();

    Task<bool> ToggleFavoriteByIdAsync(string id);

    Task RescanFromRemoteAsync();
}
