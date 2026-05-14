using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using LocalSoundboard.Infrastructure;
using LocalSoundboard.Models;
using LocalSoundboard.Services;
using Forms = System.Windows.Forms;

namespace LocalSoundboard.ViewModels;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private const string AllCategories = "Todas";
    private const string AllSoundsFilter = "Todos";
    private const string FavoritesFilter = "Favoritos";
    private const string RecentFilter = "Recientes";

    private readonly SettingsService _settingsService;
    private readonly AudioLibraryService _libraryService;
    private readonly PlaybackService _playbackService;
    private AppSettings _settings = new();
    private string _libraryPath = string.Empty;
    private string _searchText = string.Empty;
    private string _selectedCategory = AllCategories;
    private string _selectedQuickFilter = AllSoundsFilter;
    private SoundItem? _selectedSound;
    private OutputDeviceInfo? _selectedOutputDevice;
    private double _volume = 0.85;
    private PlaybackModeOption _selectedPlaybackModeOption;
    private string _statusMessage = "Selecciona una carpeta de audios para empezar.";
    private bool _isBusy;
    private bool _remoteServerEnabled;
    private bool _startRemoteServerAutomatically;
    private int _serverPort = 5050;
    private string _remotePin = string.Empty;
    private string _remoteStatusText = "Servidor remoto inactivo";
    private string _remoteUrl = "http://localhost:5050";

    public MainViewModel(
        SettingsService settingsService,
        AudioLibraryService libraryService,
        PlaybackService playbackService)
    {
        _settingsService = settingsService;
        _libraryService = libraryService;
        _playbackService = playbackService;
        _playbackService.PlaybackError += HandlePlaybackError;

        SoundsView = CollectionViewSource.GetDefaultView(Sounds);
        SoundsView.Filter = FilterSound;
        SoundsView.SortDescriptions.Add(new SortDescription(nameof(SoundItem.Category), ListSortDirection.Ascending));
        SoundsView.SortDescriptions.Add(new SortDescription(nameof(SoundItem.Name), ListSortDirection.Ascending));

        _selectedPlaybackModeOption = PlaybackModeOptions[0];

        BrowseFolderCommand = new AsyncRelayCommand(BrowseFolderAsync);
        RescanCommand = new AsyncRelayCommand(RescanLibraryAsync, () => !IsBusy);
        PlaySoundCommand = new AsyncRelayCommand(parameter => PlaySoundAsync(parameter as SoundItem), parameter => parameter is SoundItem sound && sound.IsAvailable);
        StopAllCommand = new RelayCommand(() => _playbackService.StopAll());
        ToggleFavoriteCommand = new AsyncRelayCommand(parameter => ToggleFavoriteAsync(parameter as SoundItem), parameter => parameter is SoundItem);
        SaveTagsCommand = new AsyncRelayCommand(SaveSelectedTagsAsync, () => SelectedSound is not null);
        RefreshDevicesCommand = new RelayCommand(RefreshOutputDevices);
    }

    public ObservableCollection<SoundItem> Sounds { get; } = [];

    public ICollectionView SoundsView { get; }

    public ObservableCollection<string> Categories { get; } = [AllCategories];

    public ObservableCollection<string> QuickFilters { get; } = [AllSoundsFilter, FavoritesFilter, RecentFilter];

    public ObservableCollection<OutputDeviceInfo> OutputDevices { get; } = [];

    public ObservableCollection<PlaybackModeOption> PlaybackModeOptions { get; } =
    [
        new("Exclusivo", PlaybackMode.Exclusive),
        new("Mezcla", PlaybackMode.Mix)
    ];

    public AsyncRelayCommand BrowseFolderCommand { get; }

    public AsyncRelayCommand RescanCommand { get; }

    public AsyncRelayCommand PlaySoundCommand { get; }

    public RelayCommand StopAllCommand { get; }

    public AsyncRelayCommand ToggleFavoriteCommand { get; }

    public AsyncRelayCommand SaveTagsCommand { get; }

    public RelayCommand RefreshDevicesCommand { get; }

    public string LibraryPath
    {
        get => _libraryPath;
        private set
        {
            if (SetProperty(ref _libraryPath, value))
            {
                OnPropertyChanged(nameof(LibraryStatusText));
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                SoundsView.Refresh();
            }
        }
    }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                SoundsView.Refresh();
            }
        }
    }

    public string SelectedQuickFilter
    {
        get => _selectedQuickFilter;
        set
        {
            if (SetProperty(ref _selectedQuickFilter, value))
            {
                SoundsView.Refresh();
            }
        }
    }

    public SoundItem? SelectedSound
    {
        get => _selectedSound;
        set
        {
            if (SetProperty(ref _selectedSound, value))
            {
                SaveTagsCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public OutputDeviceInfo? SelectedOutputDevice
    {
        get => _selectedOutputDevice;
        set
        {
            if (SetProperty(ref _selectedOutputDevice, value) && value is not null)
            {
                _playbackService.SetOutputDevice(value);
                _settings.OutputDeviceNumber = value.DeviceNumber;
                _settings.OutputDeviceName = value.Name;
                _ = SaveSettingsAsync();
            }
        }
    }

    public double Volume
    {
        get => _volume;
        set
        {
            var rounded = Math.Round(Math.Clamp(value, 0, 1), 2);
            if (SetProperty(ref _volume, rounded))
            {
                _playbackService.Volume = rounded;
                _settings.Volume = rounded;
                OnPropertyChanged(nameof(VolumePercentText));
                _ = SaveSettingsAsync();
            }
        }
    }

    public string VolumePercentText => $"{Volume:P0}";

    public PlaybackModeOption SelectedPlaybackModeOption
    {
        get => _selectedPlaybackModeOption;
        set
        {
            if (SetProperty(ref _selectedPlaybackModeOption, value))
            {
                _settings.PlaybackMode = value.Mode;
                _ = SaveSettingsAsync();
            }
        }
    }

    public bool RemoteServerEnabled
    {
        get => _remoteServerEnabled;
        set
        {
            if (SetProperty(ref _remoteServerEnabled, value))
            {
                _settings.RemoteServerEnabled = value;
                _ = SaveSettingsAsync();
            }
        }
    }

    public bool StartRemoteServerAutomatically
    {
        get => _startRemoteServerAutomatically;
        set
        {
            if (SetProperty(ref _startRemoteServerAutomatically, value))
            {
                _settings.StartRemoteServerAutomatically = value;
                _ = SaveSettingsAsync();
            }
        }
    }

    public int ServerPort
    {
        get => _serverPort;
        set
        {
            var port = Math.Clamp(value, 1024, 65535);
            if (SetProperty(ref _serverPort, port))
            {
                _settings.ServerPort = port;
                RemoteUrl = $"http://localhost:{port}";
                _ = SaveSettingsAsync();
            }
        }
    }

    public string RemotePin
    {
        get => _remotePin;
        set
        {
            if (SetProperty(ref _remotePin, value.Trim()))
            {
                _settings.RemotePin = _remotePin;
                _ = SaveSettingsAsync();
            }
        }
    }

    public string RemoteStatusText
    {
        get => _remoteStatusText;
        set => SetProperty(ref _remoteStatusText, value);
    }

    public string RemoteUrl
    {
        get => _remoteUrl;
        set => SetProperty(ref _remoteUrl, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RescanCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public int TotalSoundCount => Sounds.Count;

    public int AvailableSoundCount => Sounds.Count(sound => sound.IsAvailable);

    public int FavoriteSoundCount => Sounds.Count(sound => sound.IsFavorite);

    public string LibraryStatusText => string.IsNullOrWhiteSpace(LibraryPath)
        ? "Sin carpeta cargada"
        : LibraryPath;

    public async Task LoadAsync()
    {
        _settings = await _settingsService.LoadAsync();
        LibraryPath = _settings.LibraryPath;
        RemoteServerEnabled = _settings.RemoteServerEnabled;
        StartRemoteServerAutomatically = _settings.StartRemoteServerAutomatically;
        ServerPort = _settings.ServerPort;
        RemotePin = _settings.RemotePin;

        _volume = Math.Clamp(_settings.Volume, 0, 1);
        _playbackService.Volume = _volume;
        OnPropertyChanged(nameof(Volume));
        OnPropertyChanged(nameof(VolumePercentText));

        SelectedPlaybackModeOption = PlaybackModeOptions.FirstOrDefault(option => option.Mode == _settings.PlaybackMode)
            ?? PlaybackModeOptions[0];

        RefreshOutputDevices();

        if (!string.IsNullOrWhiteSpace(LibraryPath))
        {
            await RescanLibraryAsync();
        }
    }

    public IReadOnlyList<SoundItem> GetSoundSnapshot()
    {
        return Sounds.ToList();
    }

    public async Task<bool> PlayByIdAsync(string id)
    {
        var sound = _libraryService.FindById(Sounds, id);
        if (sound is null || !sound.IsAvailable)
        {
            return false;
        }

        await PlaySoundAsync(sound);
        return true;
    }

    public async Task<bool> ToggleFavoriteByIdAsync(string id)
    {
        var sound = _libraryService.FindById(Sounds, id);
        if (sound is null)
        {
            return false;
        }

        await ToggleFavoriteAsync(sound);
        return true;
    }

    public async Task RescanFromRemoteAsync()
    {
        await RescanLibraryAsync();
    }

    public void StopAll()
    {
        _playbackService.StopAll();
    }

    public void Dispose()
    {
        _playbackService.PlaybackError -= HandlePlaybackError;
        _playbackService.Dispose();
    }

    private async Task BrowseFolderAsync()
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Selecciona la carpeta principal de audios",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(LibraryPath) ? LibraryPath : string.Empty
        };

        if (dialog.ShowDialog() != Forms.DialogResult.OK)
        {
            return;
        }

        LibraryPath = dialog.SelectedPath;
        _settings.LibraryPath = LibraryPath;
        await SaveSettingsAsync();
        await RescanLibraryAsync();
    }

    private async Task RescanLibraryAsync()
    {
        IsBusy = true;
        try
        {
            StatusMessage = "Escaneando biblioteca...";
            var sounds = await _libraryService.ScanAsync(_settings);

            Sounds.Clear();
            foreach (var sound in sounds)
            {
                Sounds.Add(sound);
            }

            RefreshCategories();
            RefreshCounts();
            SoundsView.Refresh();
            await SaveSettingsAsync();
            StatusMessage = $"{AvailableSoundCount} audios disponibles de {TotalSoundCount} encontrados.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"No se pudo escanear la biblioteca: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PlaySoundAsync(SoundItem? sound)
    {
        if (sound is null)
        {
            return;
        }

        await _playbackService.PlayAsync(sound, _settings.PlaybackMode);
        sound.LastPlayedUtc = DateTimeOffset.UtcNow;
        UpdateMetadata(sound);

        _settings.RecentPaths.RemoveAll(path => string.Equals(path, sound.FullPath, StringComparison.OrdinalIgnoreCase));
        _settings.RecentPaths.Insert(0, sound.FullPath);
        _settings.RecentPaths = _settings.RecentPaths.Take(100).ToList();

        StatusMessage = $"Reproduciendo: {sound.FileName}";
        RefreshCounts();
        SoundsView.Refresh();
        await SaveSettingsAsync();
    }

    private async Task ToggleFavoriteAsync(SoundItem? sound)
    {
        if (sound is null)
        {
            return;
        }

        sound.IsFavorite = !sound.IsFavorite;
        if (sound.IsFavorite)
        {
            if (!_settings.FavoritePaths.Contains(sound.FullPath, StringComparer.OrdinalIgnoreCase))
            {
                _settings.FavoritePaths.Add(sound.FullPath);
            }
        }
        else
        {
            _settings.FavoritePaths.RemoveAll(path => string.Equals(path, sound.FullPath, StringComparison.OrdinalIgnoreCase));
        }

        RefreshCounts();
        SoundsView.Refresh();
        await SaveSettingsAsync();
    }

    private async Task SaveSelectedTagsAsync()
    {
        if (SelectedSound is null)
        {
            return;
        }

        UpdateMetadata(SelectedSound);
        SoundsView.Refresh();
        StatusMessage = $"Etiquetas guardadas para {SelectedSound.FileName}.";
        await SaveSettingsAsync();
    }

    private void RefreshOutputDevices()
    {
        OutputDevices.Clear();
        foreach (var device in _playbackService.GetOutputDevices())
        {
            OutputDevices.Add(device);
        }

        SelectedOutputDevice = _playbackService.ResolveSavedDevice(_settings);
    }

    private void RefreshCategories()
    {
        var selected = SelectedCategory;
        Categories.Clear();
        Categories.Add(AllCategories);

        foreach (var category in Sounds.Select(sound => sound.Category).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(category => category))
        {
            Categories.Add(category);
        }

        SelectedCategory = Categories.Contains(selected) ? selected : AllCategories;
    }

    private void RefreshCounts()
    {
        OnPropertyChanged(nameof(TotalSoundCount));
        OnPropertyChanged(nameof(AvailableSoundCount));
        OnPropertyChanged(nameof(FavoriteSoundCount));
    }

    private bool FilterSound(object item)
    {
        if (item is not SoundItem sound)
        {
            return false;
        }

        if (!string.Equals(SelectedCategory, AllCategories, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(sound.Category, SelectedCategory, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(SelectedQuickFilter, FavoritesFilter, StringComparison.OrdinalIgnoreCase) && !sound.IsFavorite)
        {
            return false;
        }

        if (string.Equals(SelectedQuickFilter, RecentFilter, StringComparison.OrdinalIgnoreCase) && !sound.IsRecent)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        return sound.SearchText.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateMetadata(SoundItem sound)
    {
        var metadata = _settings.Sounds.FirstOrDefault(item =>
            string.Equals(item.Path, sound.FullPath, StringComparison.OrdinalIgnoreCase));

        if (metadata is null)
        {
            metadata = new SoundMetadata { Path = sound.FullPath };
            _settings.Sounds.Add(metadata);
        }

        metadata.Tags = sound.Tags.ToList();
        metadata.LastPlayedUtc = sound.LastPlayedUtc;
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            await _settingsService.SaveAsync(_settings);
        }
        catch (Exception ex)
        {
            StatusMessage = $"No se pudo guardar la configuracion: {ex.Message}";
        }
    }

    private void HandlePlaybackError(object? sender, string message)
    {
        StatusMessage = message;
    }
}
