using LocalSoundboard.Infrastructure;

namespace LocalSoundboard.Models;

public sealed class SoundItem : ObservableObject
{
    private bool _isFavorite;
    private bool _isAvailable;
    private DateTimeOffset? _lastPlayedUtc;
    private List<string> _tags = [];

    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string FileName { get; init; } = string.Empty;

    public string Extension { get; init; } = string.Empty;

    public string FullPath { get; init; } = string.Empty;

    public string RelativePath { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetProperty(ref _isFavorite, value);
    }

    public bool IsAvailable
    {
        get => _isAvailable;
        set => SetProperty(ref _isAvailable, value);
    }

    public DateTimeOffset? LastPlayedUtc
    {
        get => _lastPlayedUtc;
        set
        {
            if (SetProperty(ref _lastPlayedUtc, value))
            {
                OnPropertyChanged(nameof(IsRecent));
            }
        }
    }

    public bool IsRecent => LastPlayedUtc.HasValue;

    public IReadOnlyList<string> Tags => _tags;

    public string TagsText
    {
        get => string.Join(", ", _tags);
        set
        {
            var parsed = ParseTags(value);
            if (_tags.SequenceEqual(parsed, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            _tags = parsed;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SearchText));
        }
    }

    public string SearchText => $"{Name} {FileName} {Category} {TagsText}".Trim();

    public static List<string> ParseTags(string value)
    {
        return value
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
