using LocalSoundboard.Models;

namespace LocalSoundboard.Services;

public interface IRemoteSoundboardController
{
    string LibraryStatusText { get; }

    int AvailableSoundCount { get; }

    int TotalSoundCount { get; }

    string RemotePin { get; }

    IReadOnlyList<SoundItem> GetSoundSnapshot();

    Task<bool> PlayByIdAsync(string id);

    void StopAll();

    Task<bool> ToggleFavoriteByIdAsync(string id);

    Task RescanFromRemoteAsync();
}
