using LocalSoundboard.Infrastructure;
using LocalSoundboard.Models;
using LocalSoundboard.Services;

namespace LocalSoundboard.ViewModels;

public sealed class PlaybackSessionViewModel : ObservableObject
{
    private readonly PlaybackService _player;
    private double _positionSeconds;
    private bool _isPaused;

    public PlaybackSessionViewModel(PlaybackSessionSnapshot snapshot, PlaybackService player)
    {
        _player = player;
        Id = snapshot.Id;
        Name = snapshot.Name;
        DurationSeconds = snapshot.DurationSeconds;
        TogglePauseCommand = new RelayCommand(() => _player.SetPaused(Id, !IsPaused));
        StopCommand = new RelayCommand(() => _player.Stop(Id));
        SeekCommand = new AsyncRelayCommand(async () => await _player.SeekAsync(Id, PositionSeconds));
        Update(snapshot);
    }

    public Guid Id { get; }
    public string Name { get; }
    public double DurationSeconds { get; }
    public bool IsSeeking { get; set; }
    public RelayCommand TogglePauseCommand { get; }
    public RelayCommand StopCommand { get; }
    public AsyncRelayCommand SeekCommand { get; }
    public bool IsPaused
    {
        get => _isPaused;
        private set
        {
            if (SetProperty(ref _isPaused, value))
            {
                OnPropertyChanged(nameof(PauseText));
                OnPropertyChanged(nameof(PauseGlyph));
            }
        }
    }
    public string PauseText => IsPaused ? "Resume" : "Pause";
    public string PauseGlyph => IsPaused ? "\uE768" : "\uE769";
    public double PositionSeconds
    {
        get => _positionSeconds;
        set
        {
            if (SetProperty(ref _positionSeconds, value)) OnPropertyChanged(nameof(TimeText));
        }
    }
    public string TimeText => $"{FormatTime(PositionSeconds)} / {FormatTime(DurationSeconds)}";

    public void Update(PlaybackSessionSnapshot snapshot)
    {
        if (!IsSeeking) PositionSeconds = snapshot.PositionSeconds;
        IsPaused = snapshot.IsPaused;
    }

    private static string FormatTime(double seconds)
    {
        var time = TimeSpan.FromSeconds(seconds);
        return time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");
    }
}
