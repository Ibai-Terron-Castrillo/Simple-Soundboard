using System.Collections.ObjectModel;
using System.Windows.Threading;
using LocalSoundboard.Models;

namespace LocalSoundboard.ViewModels;

public sealed partial class MainViewModel
{
    private DispatcherTimer? _playerTimer;
    public ObservableCollection<PlaybackSessionViewModel> ActiveSessions { get; } = [];
    public bool HasPlayback => ActiveSessions.Count > 0;
    public string PlaybackSummary => HasPlayback ? $"Now playing ({ActiveSessions.Count})" : "Nothing playing";
    public string PauseResumeGlyph => IsPlaybackPaused ? "\uE768" : "\uE769";

    private void InitializePlayer()
    {
        _playerTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background,
            (_, _) => RefreshPlayer(), System.Windows.Threading.Dispatcher.CurrentDispatcher);
        _playbackService.StateChanged += OnPlayerStateChanged;
    }

    private void OnPlayerStateChanged(object? sender, EventArgs e) => PostToUi(RefreshPlayer);

    private void PostToUi(Action action)
    {
        if (_isDisposed) return;
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted) return;
        if (dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(() => { if (!_isDisposed) action(); });
    }

    private void RefreshPlayer()
    {
        if (_isDisposed) return;
        var snapshot = _playbackService.GetSnapshot();
        foreach (var existing in ActiveSessions.ToArray())
            if (!snapshot.Sessions.Any(session => session.Id == existing.Id)) ActiveSessions.Remove(existing);
        foreach (var session in snapshot.Sessions)
        {
            var existing = ActiveSessions.FirstOrDefault(current => current.Id == session.Id);
            if (existing is null) ActiveSessions.Add(new(session, _playbackService));
            else existing.Update(session);
        }
        OnPropertyChanged(nameof(IsPlaybackPaused));
        OnPropertyChanged(nameof(PauseResumeText));
        OnPropertyChanged(nameof(PauseResumeGlyph));
        OnPropertyChanged(nameof(HasPlayback));
        OnPropertyChanged(nameof(PlaybackSummary));
        TogglePauseCommand?.RaiseCanExecuteChanged();
    }

    private void StopPlayerUpdates()
    {
        _playerTimer?.Stop();
        _playbackService.StateChanged -= OnPlayerStateChanged;
    }

    public PlaybackSnapshot GetPlaybackSnapshot() => _playbackService.GetSnapshot();
    public Task<bool> SeekFromRemoteAsync(Guid sessionId, double seconds)
        => _isDisposed ? Task.FromResult(false) : _playbackService.SeekAsync(sessionId, seconds, _lifetimeCts.Token);
    public bool StopSession(Guid sessionId) => !_isDisposed && _playbackService.Stop(sessionId);
    public bool SetSessionPaused(Guid sessionId, bool paused) => !_isDisposed && _playbackService.SetPaused(sessionId, paused);
}
