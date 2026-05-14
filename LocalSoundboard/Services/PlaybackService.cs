using System.IO;
using LocalSoundboard.Models;
using NAudio.Vorbis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace LocalSoundboard.Services;

public sealed class PlaybackService : IDisposable
{
    private readonly object _gate = new();
    private readonly List<PlaybackSession> _sessions = [];
    private double _volume = 0.85;

    public event EventHandler<string>? PlaybackError;

    public int DeviceNumber { get; private set; } = -1;

    public double Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 1);
            lock (_gate)
            {
                foreach (var session in _sessions)
                {
                    session.VolumeProvider.Volume = (float)_volume;
                }
            }
        }
    }

    public IReadOnlyList<OutputDeviceInfo> GetOutputDevices()
    {
        var devices = new List<OutputDeviceInfo>
        {
            new(-1, "Salida predeterminada de Windows")
        };

        for (var index = 0; index < WaveOut.DeviceCount; index++)
        {
            try
            {
                var capabilities = WaveOut.GetCapabilities(index);
                devices.Add(new OutputDeviceInfo(index, capabilities.ProductName));
            }
            catch
            {
                // Windows can temporarily reject a device query while audio devices are changing.
            }
        }

        return devices;
    }

    public OutputDeviceInfo ResolveSavedDevice(AppSettings settings)
    {
        var devices = GetOutputDevices();

        if (settings.OutputDeviceNumber.HasValue)
        {
            var byNumber = devices.FirstOrDefault(device => device.DeviceNumber == settings.OutputDeviceNumber.Value);
            if (byNumber is not null)
            {
                return byNumber;
            }
        }

        if (!string.IsNullOrWhiteSpace(settings.OutputDeviceName))
        {
            var byName = devices.FirstOrDefault(device =>
                string.Equals(device.Name, settings.OutputDeviceName, StringComparison.OrdinalIgnoreCase));

            if (byName is not null)
            {
                return byName;
            }
        }

        return devices[0];
    }

    public void SetOutputDevice(OutputDeviceInfo? device)
    {
        DeviceNumber = device?.DeviceNumber ?? -1;
    }

    public Task PlayAsync(SoundItem sound, PlaybackMode mode, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => Play(sound, mode), cancellationToken);
    }

    public void StopAll()
    {
        List<PlaybackSession> sessions;
        lock (_gate)
        {
            sessions = [.. _sessions];
            _sessions.Clear();
        }

        foreach (var session in sessions)
        {
            session.Output.PlaybackStopped -= HandlePlaybackStopped;
            try
            {
                session.Output.Stop();
            }
            catch
            {
                // Some drivers throw when a stream is already stopped or the device disappeared.
            }

            session.Dispose();
        }
    }

    public void Dispose()
    {
        StopAll();
    }

    private void Play(SoundItem sound, PlaybackMode mode)
    {
        if (!sound.IsAvailable || !File.Exists(sound.FullPath))
        {
            RaisePlaybackError($"El archivo no está disponible: {sound.FileName}");
            return;
        }

        if (mode == PlaybackMode.Exclusive)
        {
            StopAll();
        }

        PlaybackSession? session = null;
        try
        {
            var reader = CreateReader(sound.FullPath);
            var volumeProvider = new VolumeSampleProvider(reader.ToSampleProvider())
            {
                Volume = (float)_volume
            };

            var output = new WaveOutEvent
            {
                DeviceNumber = DeviceNumber,
                DesiredLatency = 80
            };

            session = new PlaybackSession(output, reader, volumeProvider, sound.FileName);
            output.PlaybackStopped += HandlePlaybackStopped;
            output.Init(volumeProvider);

            lock (_gate)
            {
                _sessions.Add(session);
            }

            output.Play();
        }
        catch (Exception ex)
        {
            session?.Dispose();
            RaisePlaybackError($"No se pudo reproducir {sound.FileName}: {ex.Message}");
        }
    }

    private void HandlePlaybackStopped(object? sender, StoppedEventArgs e)
    {
        PlaybackSession? session = null;

        lock (_gate)
        {
            session = _sessions.FirstOrDefault(current => ReferenceEquals(current.Output, sender));
            if (session is not null)
            {
                _sessions.Remove(session);
            }
        }

        if (session is null)
        {
            return;
        }

        session.Output.PlaybackStopped -= HandlePlaybackStopped;
        session.Dispose();

        if (e.Exception is not null)
        {
            RaisePlaybackError($"La reproducción se detuvo con error en {session.FileName}: {e.Exception.Message}");
        }
    }

    private static WaveStream CreateReader(string path)
    {
        return Path.GetExtension(path).Equals(".ogg", StringComparison.OrdinalIgnoreCase)
            ? new VorbisWaveReader(path)
            : new AudioFileReader(path);
    }

    private void RaisePlaybackError(string message)
    {
        PlaybackError?.Invoke(this, message);
    }

    private sealed class PlaybackSession : IDisposable
    {
        public PlaybackSession(WaveOutEvent output, WaveStream reader, VolumeSampleProvider volumeProvider, string fileName)
        {
            Output = output;
            Reader = reader;
            VolumeProvider = volumeProvider;
            FileName = fileName;
        }

        public WaveOutEvent Output { get; }

        public WaveStream Reader { get; }

        public VolumeSampleProvider VolumeProvider { get; }

        public string FileName { get; }

        public void Dispose()
        {
            Output.Dispose();
            Reader.Dispose();
        }
    }
}
