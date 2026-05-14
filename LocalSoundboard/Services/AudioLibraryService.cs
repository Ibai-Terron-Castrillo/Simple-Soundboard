using System.IO;
using System.Security.Cryptography;
using System.Text;
using LocalSoundboard.Models;

namespace LocalSoundboard.Services;

public sealed class AudioLibraryService
{
    public static readonly string[] SupportedExtensions = [".mp3", ".wav", ".ogg", ".flac", ".m4a"];

    private static readonly HashSet<string> SupportedExtensionSet = new(SupportedExtensions, StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<SoundItem>> ScanAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<SoundItem>>(() => Scan(settings, cancellationToken), cancellationToken);
    }

    public SoundItem? FindById(IEnumerable<SoundItem> sounds, string id)
    {
        return sounds.FirstOrDefault(sound => string.Equals(sound.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsSupportedAudioFile(string path)
    {
        return SupportedExtensionSet.Contains(Path.GetExtension(path));
    }

    public static string NormalizePath(string path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : Path.GetFullPath(path.Trim());
    }

    public static bool IsPathInsideLibrary(string libraryPath, string candidatePath)
    {
        if (string.IsNullOrWhiteSpace(libraryPath) || string.IsNullOrWhiteSpace(candidatePath))
        {
            return false;
        }

        var root = NormalizePath(libraryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var candidate = NormalizePath(candidatePath);

        return string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<SoundItem> Scan(AppSettings settings, CancellationToken cancellationToken)
    {
        settings.LibraryPath = settings.LibraryPath?.Trim() ?? string.Empty;
        var libraryExists = Directory.Exists(settings.LibraryPath);
        var root = libraryExists ? NormalizePath(settings.LibraryPath) : string.Empty;

        var metadataByPath = settings.Sounds
            .Where(sound => !string.IsNullOrWhiteSpace(sound.Path))
            .GroupBy(sound => NormalizePath(sound.Path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var favoritePaths = settings.FavoritePaths
            .Select(NormalizePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var discoveredPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<SoundItem>();

        if (libraryExists)
        {
            foreach (var path in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                         .Where(IsSupportedAudioFile)
                         .Select(NormalizePath)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                discoveredPaths.Add(path);

                if (!metadataByPath.TryGetValue(path, out var metadata))
                {
                    metadata = new SoundMetadata { Path = path };
                    metadataByPath[path] = metadata;
                }

                items.Add(CreateSoundItem(root, metadata, isAvailable: true, favoritePaths.Contains(path)));
            }
        }

        foreach (var metadata in metadataByPath.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var normalizedPath = NormalizePath(metadata.Path);
            if (libraryExists && !IsPathInsideLibrary(root, normalizedPath))
            {
                continue;
            }

            if (discoveredPaths.Contains(normalizedPath))
            {
                continue;
            }

            items.Add(CreateSoundItem(root, metadata, isAvailable: false, favoritePaths.Contains(normalizedPath)));
        }

        settings.Sounds = metadataByPath.Values
            .OrderBy(sound => sound.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return items
            .OrderByDescending(sound => sound.IsAvailable)
            .ThenBy(sound => sound.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(sound => sound.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static SoundItem CreateSoundItem(string libraryRoot, SoundMetadata metadata, bool isAvailable, bool isFavorite)
    {
        var path = NormalizePath(metadata.Path);
        var relativePath = CreateRelativePath(libraryRoot, path);
        var category = CreateCategory(relativePath);

        var item = new SoundItem
        {
            Id = CreateId(path),
            Name = Path.GetFileNameWithoutExtension(path),
            FileName = Path.GetFileName(path),
            Extension = Path.GetExtension(path).TrimStart('.').ToUpperInvariant(),
            FullPath = path,
            RelativePath = relativePath,
            Category = category,
            IsAvailable = isAvailable,
            IsFavorite = isFavorite,
            LastPlayedUtc = metadata.LastPlayedUtc
        };

        item.TagsText = string.Join(", ", metadata.Tags ?? []);
        return item;
    }

    private static string CreateRelativePath(string libraryRoot, string path)
    {
        if (string.IsNullOrWhiteSpace(libraryRoot) || !IsPathInsideLibrary(libraryRoot, path))
        {
            return Path.GetFileName(path);
        }

        return Path.GetRelativePath(libraryRoot, path);
    }

    private static string CreateCategory(string relativePath)
    {
        var directory = Path.GetDirectoryName(relativePath);
        return string.IsNullOrWhiteSpace(directory) ? "Root" : directory;
    }

    private static string CreateId(string normalizedPath)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath.ToUpperInvariant()));
        return Convert.ToHexString(bytes[..8]).ToLowerInvariant();
    }
}
