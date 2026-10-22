using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using LocalSoundboard.Models;

namespace LocalSoundboard.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public SettingsService(string? appDirectory = null)
    {
        AppDirectory = appDirectory ?? ResolveAppDirectory();
    }

    public string AppDirectory { get; }

    public string SettingsPath => Path.Combine(AppDirectory, "settings.json");

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SettingsPath))
        {
            return Normalize(new AppSettings());
        }

        try
        {
            await using var stream = File.OpenRead(SettingsPath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken);
            return Normalize(settings ?? new AppSettings());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return Normalize(new AppSettings());
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(AppDirectory);
        Normalize(settings);

        var tempPath = SettingsPath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
        }

        File.Move(tempPath, SettingsPath, true);
    }

    public static AppSettings Snapshot(AppSettings settings)
        => JsonSerializer.Deserialize<AppSettings>(JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions), JsonOptions)!;

    private static AppSettings Normalize(AppSettings settings)
    {
        settings.LibraryPath ??= string.Empty;
        settings.OutputDeviceName ??= string.Empty;
        settings.RemotePin ??= string.Empty;
        settings.FavoritePaths ??= [];
        settings.RecentPaths ??= [];
        settings.Sounds ??= [];
        settings.Banks ??= [];
        settings.PauseHotkey ??= string.Empty;
        settings.StopHotkey ??= string.Empty;
        settings.Volume = Math.Clamp(settings.Volume, 0, 1);

        if (settings.ServerPort is < 1024 or > 65535)
        {
            settings.ServerPort = 5050;
        }

        settings.FavoritePaths = settings.FavoritePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(AudioLibraryService.NormalizePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        settings.RecentPaths = settings.RecentPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(AudioLibraryService.NormalizePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .ToList();

        settings.Sounds = settings.Sounds
            .Where(sound => !string.IsNullOrWhiteSpace(sound.Path))
            .Select(sound =>
            {
                sound.Path = AudioLibraryService.NormalizePath(sound.Path);
                sound.Tags ??= [];
                sound.Alias ??= string.Empty;
                sound.Hotkey ??= string.Empty;
                sound.Tags = sound.Tags
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Select(tag => tag.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return sound;
            })
            .GroupBy(sound => sound.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(sound => sound.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        settings.Banks = settings.Banks
            .Where(bank => !string.IsNullOrWhiteSpace(bank.Id) && !string.IsNullOrWhiteSpace(bank.Name))
            .GroupBy(bank => bank.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .Select(bank =>
            {
                bank.Name = bank.Name.Trim();
                bank.SoundPaths = (bank.SoundPaths ?? [])
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(AudioLibraryService.NormalizePath)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                return bank;
            }).ToList();

        return settings;
    }

    private static string ResolveAppDirectory()
    {
        var overrideRoot = Environment.GetEnvironmentVariable("LOCAL_SOUNDBOARD_APPDATA");
        if (!string.IsNullOrWhiteSpace(overrideRoot))
        {
            return Path.Combine(AudioLibraryService.NormalizePath(overrideRoot), "LocalSoundboard");
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LocalSoundboard");
    }
}
