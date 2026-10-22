using System.IO;
using LocalSoundboard.Models;
using LocalSoundboard.Services;
using NAudio.Wave;
using Xunit;

namespace LocalSoundboard.Tests;

public sealed class PlaybackTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "LocalSoundboard-tests-" + Guid.NewGuid());
    private readonly List<FakeOutput> _outputs = [];
    private readonly PlaybackService _player;
    private readonly SoundItem _sound;

    public PlaybackTests()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "silence.wav");
        using (var writer = new WaveFileWriter(path, new WaveFormat(8000, 16, 1))) writer.Write(new byte[8000 * 2 * 30], 0, 8000 * 2 * 30);
        _sound = new() { Id = "test", Name = "Silence", FileName = "silence.wav", FullPath = path, IsAvailable = true };
        _player = new PlaybackService(_ =>
        {
            var output = new FakeOutput();
            _outputs.Add(output);
            return output;
        }, path => new AudioFileReader(path));
    }

    [Fact]
    public async Task PausePreservesPositionAndResumeContinues()
    {
        Assert.True((await _player.PlayAsync(_sound, PlaybackMode.Exclusive)).Success);
        _outputs[0].Advance(3);
        Assert.Equal(3, _player.GetSnapshot().Sessions.Single().PositionSeconds, 2);
        Assert.True(_player.PauseAll());
        _outputs[0].Advance(8);
        Assert.True(_player.IsPaused);
        Assert.Equal(3, _player.GetSnapshot().Sessions.Single().PositionSeconds, 2);
        Assert.True(_player.ResumeAll());
        _outputs[0].Advance(2);
        Assert.False(_player.IsPaused);
        Assert.Equal(5, _player.GetSnapshot().Sessions.Single().PositionSeconds, 2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SeekFlushesOldOutputAndPreservesPausedState(bool paused)
    {
        await _player.PlayAsync(_sound, PlaybackMode.Exclusive);
        if (paused) _player.PauseAll();
        var id = _player.GetSnapshot().Sessions.Single().Id;
        Assert.True(await _player.SeekAsync(id, 12));
        var session = _player.GetSnapshot().Sessions.Single();
        Assert.Equal(id, session.Id);
        Assert.Equal(12, session.PositionSeconds, 2);
        Assert.Equal(paused, session.IsPaused);
        Assert.True(_outputs[0].Disposed);
        Assert.Equal(paused ? 0 : 1, _outputs[1].PlayCount);
    }

    [Fact]
    public async Task MixDoesNotResumePausedSessionsWhenNewSoundStarts()
    {
        await _player.PlayAsync(_sound, PlaybackMode.Mix);
        _player.PauseAll();
        await _player.PlayAsync(_sound, PlaybackMode.Mix);
        var sessions = _player.GetSnapshot().Sessions;
        Assert.Equal(2, sessions.Count);
        Assert.True(sessions[0].IsPaused);
        Assert.False(sessions[1].IsPaused);
        Assert.False(_player.IsPaused);
        _player.Stop(sessions[1].Id);
        Assert.True(_player.IsPaused);
    }

    [Fact]
    public async Task IndividualStopAndPauseOnlyAffectTheirSession()
    {
        await _player.PlayAsync(_sound, PlaybackMode.Mix);
        await _player.PlayAsync(_sound, PlaybackMode.Mix);
        var ids = _player.GetSnapshot().Sessions.Select(session => session.Id).ToArray();
        Assert.True(_player.SetPaused(ids[0], true));
        Assert.False(_player.GetSnapshot().Sessions[1].IsPaused);
        Assert.True(_player.Stop(ids[0]));
        Assert.Equal(ids[1], _player.GetSnapshot().Sessions.Single().Id);
    }

    [Fact]
    public async Task InvalidFileReportsFailureWithoutStoppingExistingSound()
    {
        await _player.PlayAsync(_sound, PlaybackMode.Exclusive);
        var path = Path.Combine(_directory, "corrupt.wav");
        File.WriteAllText(path, "not audio");
        var result = await _player.PlayAsync(new() { FullPath = path, FileName = "corrupt.wav", IsAvailable = true }, PlaybackMode.Exclusive);
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Single(_player.GetSnapshot().Sessions);
    }

    [Fact]
    public async Task DriverFailureCannotLeaveAPausedOrPlayingGhostSession()
    {
        await _player.PlayAsync(_sound, PlaybackMode.Exclusive);
        _outputs[0].FailPause = true;
        Assert.False(_player.PauseAll());
        Assert.False(_player.IsPaused);
        Assert.Empty(_player.GetSnapshot().Sessions);
        Assert.True(_outputs[0].Disposed);
    }

    [Fact]
    public async Task NaturalCompletionRemovesSession()
    {
        await _player.PlayAsync(_sound, PlaybackMode.Exclusive);
        _outputs[0].Finish();
        Assert.True(SpinWait.SpinUntil(() => !_player.GetSnapshot().HasPlayback, TimeSpan.FromSeconds(2)));
        Assert.False(_player.IsPaused);
    }

    [Fact]
    public async Task ExclusiveConcurrentStartsLeaveOnlyOneSession()
    {
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => _player.PlayAsync(_sound, PlaybackMode.Exclusive)));
        Assert.Single(_player.GetSnapshot().Sessions);
        Assert.Equal(7, _outputs.Count(output => output.Disposed));
    }

    [Fact]
    public async Task StopAndDisposeReleaseAllStreamsAndRejectFurtherPlayback()
    {
        await _player.PlayAsync(_sound, PlaybackMode.Mix);
        await _player.PlayAsync(_sound, PlaybackMode.Mix);
        _player.PauseAll();
        _player.StopAll();
        Assert.Empty(_player.GetSnapshot().Sessions);
        Assert.False(_player.ResumeAll());
        _player.Dispose();
        Assert.False((await _player.PlayAsync(_sound, PlaybackMode.Exclusive)).Success);
        Assert.All(_outputs, output => Assert.True(output.Disposed));
        using var file = File.Open(_sound.FullPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public async Task SeekRejectsInvalidPositions(double seconds)
    {
        await _player.PlayAsync(_sound, PlaybackMode.Exclusive);
        Assert.False(await _player.SeekAsync(_player.GetSnapshot().Sessions.Single().Id, seconds));
    }

    public void Dispose()
    {
        _player.Dispose();
        Directory.Delete(_directory, true);
    }

    private sealed class FakeOutput : IWavePlayer, IWavePosition
    {
        private long _position;
        public bool FailPause { get; set; }
        public bool Disposed { get; private set; }
        public int PlayCount { get; private set; }
        public event EventHandler<StoppedEventArgs>? PlaybackStopped;
        public PlaybackState PlaybackState { get; private set; }
        public float Volume { get; set; }
        public WaveFormat OutputWaveFormat { get; private set; } = new WaveFormat(8000, 16, 1);
        public void Init(IWaveProvider provider) => OutputWaveFormat = provider.WaveFormat;
        public void Play() { PlayCount++; PlaybackState = PlaybackState.Playing; }
        public void Pause()
        {
            if (FailPause) throw new InvalidOperationException("Device removed");
            PlaybackState = PlaybackState.Paused;
        }
        public void Stop() => PlaybackState = PlaybackState.Stopped;
        public long GetPosition() => _position;
        public void Advance(double seconds)
        {
            if (PlaybackState == PlaybackState.Playing) _position += (long)(seconds * OutputWaveFormat.AverageBytesPerSecond);
        }
        public void Finish()
        {
            Stop();
            PlaybackStopped?.Invoke(this, new StoppedEventArgs());
        }
        public void Dispose() { Disposed = true; Stop(); }
    }
}
