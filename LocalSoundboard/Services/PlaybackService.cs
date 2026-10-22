using System.IO;
using LocalSoundboard.Models;
using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace LocalSoundboard.Services;

public sealed class PlaybackService : IDisposable
{
    private readonly object _gate = new();
    private readonly List<Session> _sessions = [];
    private readonly Func<int, IWavePlayer> _createOutput;
    private readonly Func<string, WaveStream> _createReader;
    private double _volume = 0.85;
    private bool _disposed;

    public PlaybackService() : this(
        device => new WaveOutEvent { DeviceNumber = device, DesiredLatency = 80 },
        path => Path.GetExtension(path).Equals(".ogg", StringComparison.OrdinalIgnoreCase)
            ? new VorbisWaveReader(path) : new AudioFileReader(path)) { }

    internal PlaybackService(Func<int, IWavePlayer> createOutput, Func<string, WaveStream> createReader)
    {
        _createOutput = createOutput;
        _createReader = createReader;
    }

    public event EventHandler<string>? PlaybackError;
    public event EventHandler? StateChanged;
    public int DeviceNumber { get; private set; } = -1;
    public bool IsPaused => GetSnapshot().IsPaused;

    public double Volume
    {
        get { lock (_gate) return _volume; }
        set
        {
            lock (_gate)
            {
                _volume = double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0.85;
                foreach (var session in _sessions) session.Volume.Volume = (float)_volume;
            }
        }
    }

    public IReadOnlyList<OutputDeviceInfo> GetOutputDevices()
    {
        var devices = new List<OutputDeviceInfo> { new(-1, "Windows default output") };
        for (var index = 0; index < WaveOut.DeviceCount; index++)
        {
            try { devices.Add(new OutputDeviceInfo(index, WaveOut.GetCapabilities(index).ProductName)); }
            catch { /* Devices can disappear during enumeration. */ }
        }
        return devices;
    }

    public OutputDeviceInfo ResolveSavedDevice(AppSettings settings)
    {
        var devices = GetOutputDevices();
        // Device numbers can change after Windows restarts or reconnects a device.
        return devices.FirstOrDefault(device => string.Equals(device.Name, settings.OutputDeviceName, StringComparison.OrdinalIgnoreCase))
            ?? (string.IsNullOrWhiteSpace(settings.OutputDeviceName)
                ? devices.FirstOrDefault(device => device.DeviceNumber == settings.OutputDeviceNumber) : null)
            ?? devices[0];
    }

    public void SetOutputDevice(OutputDeviceInfo? device)
    {
        lock (_gate) DeviceNumber = device?.DeviceNumber ?? -1;
    }

    public Task<PlaybackResult> PlayAsync(SoundItem sound, PlaybackMode mode, CancellationToken cancellationToken = default)
        => Task.Run(() => Play(sound, mode, cancellationToken), cancellationToken);

    private PlaybackResult Play(SoundItem sound, PlaybackMode mode, CancellationToken cancellationToken)
    {
        string? error = null;
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_disposed) return new(false, "The player is closing.");
            if (!sound.IsAvailable || !File.Exists(sound.FullPath))
                return new(false, $"The file is not available: {sound.FileName}");

            Session? session = null;
            try
            {
                session = CreateSession(sound, Guid.NewGuid(), DeviceNumber, 0);
                cancellationToken.ThrowIfCancellationRequested();
                // Validate the new file/device before replacing an existing sound.
                if (mode == PlaybackMode.Exclusive) StopCore();
                _sessions.Add(session);
                session.Output.Play();
            }
            catch (OperationCanceledException)
            {
                if (session is not null) RemoveCore(session);
                throw;
            }
            catch (Exception ex)
            {
                if (session is not null) RemoveCore(session);
                error = $"Could not play {sound.FileName}: {ex.Message}";
            }
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
        if (error is not null) PlaybackError?.Invoke(this, error);
        return new(error is null, error);
    }

    public PlaybackSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new(_sessions.Select(session => new PlaybackSessionSnapshot(
                session.Id, session.Sound.Id, session.Sound.DisplayName,
                session.IsPaused,
                session.PositionSeconds, session.Reader.TotalTime.TotalSeconds)).ToArray());
        }
    }

    public bool PauseAll() => SetPaused(null, true);
    public bool ResumeAll() => SetPaused(null, false);

    public bool SetPaused(Guid? sessionId, bool paused)
    {
        var changed = false;
        var errors = new List<string>();
        lock (_gate)
        {
            foreach (var session in _sessions.Where(session => sessionId is null || session.Id == sessionId).ToArray())
            {
                try
                {
                    if (paused)
                    {
                        if (!session.IsPaused) session.Output.Pause();
                        session.IsPaused |= session.Output.PlaybackState == PlaybackState.Paused;
                        changed |= session.IsPaused;
                    }
                    else
                    {
                        session.Output.Play();
                        session.IsPaused = session.Output.PlaybackState != PlaybackState.Playing;
                        changed |= !session.IsPaused;
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Could not {(paused ? "pause" : "resume")} {session.Sound.Name}: {ex.Message}");
                    RemoveCore(session);
                }
            }
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
        foreach (var error in errors) PlaybackError?.Invoke(this, error);
        return changed;
    }

    public bool Stop(Guid sessionId)
    {
        lock (_gate)
        {
            var session = _sessions.FirstOrDefault(session => session.Id == sessionId);
            if (session is null) return false;
            RemoveCore(session);
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void StopAll()
    {
        lock (_gate) StopCore();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task<bool> SeekAsync(Guid sessionId, double seconds, CancellationToken cancellationToken = default)
        => Task.Run(() => Seek(sessionId, seconds, cancellationToken), cancellationToken);

    private bool Seek(Guid sessionId, double seconds, CancellationToken cancellationToken)
    {
        if (!double.IsFinite(seconds) || seconds < 0) return false;
        string? error = null;
        lock (_gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var old = _sessions.FirstOrDefault(session => session.Id == sessionId);
            if (_disposed || old is null) return false;
            var paused = old.IsPaused;
            Session? replacement = null;
            try
            {
                if (seconds >= old.Reader.TotalTime.TotalSeconds) RemoveCore(old);
                else
                {
                    // Reopen the output to discard buffered samples from before the seek.
                    replacement = CreateSession(old.Sound, old.Id, old.Device, seconds);
                    RemoveCore(old);
                    _sessions.Add(replacement);
                    replacement.IsPaused = paused;
                    if (!paused) replacement.Output.Play();
                }
            }
            catch (Exception ex)
            {
                if (replacement is not null) RemoveCore(replacement);
                error = $"Could not seek {old.Sound.Name}: {ex.Message}";
            }
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
        if (error is not null) PlaybackError?.Invoke(this, error);
        return error is null;
    }

    private Session CreateSession(SoundItem sound, Guid id, int device, double seconds)
    {
        WaveStream? reader = null;
        IWavePlayer? output = null;
        try
        {
            reader = _createReader(sound.FullPath);
            if (reader.TotalTime <= TimeSpan.Zero) throw new InvalidDataException("The file has no audio.");
            reader.CurrentTime = TimeSpan.FromSeconds(seconds);
            var volume = new VolumeSampleProvider(reader.ToSampleProvider()) { Volume = (float)_volume };
            output = _createOutput(device);
            var session = new Session(id, sound, device, reader, output, volume, reader.CurrentTime.TotalSeconds);
            output.Init(volume.ToWaveProvider());
            output.PlaybackStopped += HandlePlaybackStopped;
            return session;
        }
        catch
        {
            output?.Dispose();
            reader?.Dispose();
            throw;
        }
    }

    private void HandlePlaybackStopped(object? sender, StoppedEventArgs e)
    {
        // Drivers can raise this callback while Stop/Dispose is waiting for their audio thread.
        ThreadPool.QueueUserWorkItem(_ =>
        {
            Session? session;
            lock (_gate)
            {
                session = _sessions.FirstOrDefault(current => ReferenceEquals(current.Output, sender));
                if (session is null) return;
                RemoveCore(session);
            }
            StateChanged?.Invoke(this, EventArgs.Empty);
            if (e.Exception is not null)
                PlaybackError?.Invoke(this, $"Playback stopped in {session.Sound.Name}: {e.Exception.Message}");
        });
    }

    private void StopCore()
    {
        foreach (var session in _sessions.ToArray()) RemoveCore(session);
    }

    private void RemoveCore(Session session)
    {
        _sessions.Remove(session);
        session.Output.PlaybackStopped -= HandlePlaybackStopped;
        try { session.Output.Stop(); }
        catch { /* A disconnected driver can reject Stop. Disposal still releases the reader. */ }
        try { session.Output.Dispose(); }
        finally { session.Reader.Dispose(); }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            StopCore();
        }
    }

    private sealed record Session(Guid Id, SoundItem Sound, int Device, WaveStream Reader,
        IWavePlayer Output, VolumeSampleProvider Volume, double StartSeconds)
    {
        public bool IsPaused { get; set; }
        public double PositionSeconds
        {
            get
            {
                try
                {
                    var position = Output is IWavePosition clock
                        ? StartSeconds + (double)clock.GetPosition() / Volume.WaveFormat.AverageBytesPerSecond
                        : Reader.CurrentTime.TotalSeconds;
                    return Math.Clamp(position, 0, Reader.TotalTime.TotalSeconds);
                }
                catch { return Math.Clamp(Reader.CurrentTime.TotalSeconds, 0, Reader.TotalTime.TotalSeconds); }
            }
        }
    }
}
