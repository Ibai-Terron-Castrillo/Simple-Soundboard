using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using LocalSoundboard.Infrastructure;
using LocalSoundboard.Models;
using LocalSoundboard.Services;
using Forms = System.Windows.Forms;

namespace LocalSoundboard.ViewModels;

public sealed partial class MainViewModel : ObservableObject, IAsyncDisposable, IRemoteSoundboardController
{
    private const string AllCategories = "All folders";
    private const string AllSoundsFilter = "All";
    private const string FavoritesFilter = "Favorites";
    private const string RecentFilter = "Recent";

    private readonly SettingsService _settingsService;
    private readonly AudioLibraryService _libraryService;
    private readonly PlaybackService _playbackService;
    private readonly SemaphoreSlim _remoteGate = new(1, 1);
    private readonly SemaphoreSlim _settingsGate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCts = new();
    private Task _loadTask = Task.CompletedTask;
    private Task _scanTask = Task.CompletedTask;
    private Task _playTask = Task.CompletedTask;
    private Task? _shutdownTask;
    private System.Windows.Threading.DispatcherTimer? _settingsSaveTimer;
    private AppSettings _settings = new();
    private RemoteControlServer? _remoteControlServer;
    private string _libraryPath = string.Empty;
    private string _searchText = string.Empty;
    private string _selectedCategory = AllCategories;
    private string _selectedQuickFilter = AllSoundsFilter;
    private SoundItem? _selectedSound;
    private OutputDeviceInfo? _selectedOutputDevice;
    private double _volume = 0.85;
    private PlaybackModeOption _selectedPlaybackModeOption;
    private string _statusMessage = "Choose a sound folder to start.";
    private bool _isDarkMode;
    private bool _isBusy;
    private bool _remoteServerEnabled;
    private bool _startRemoteServerAutomatically;
    private int _serverPort = 5050;
    private string _remotePin = string.Empty;
    private string _remoteStatusText = "Remote server inactive";
    private string _remoteUrl = "http://localhost:5050";
    private bool _isLoaded;
    private bool _isDisposed;

    public MainViewModel(
        SettingsService settingsService,
        AudioLibraryService libraryService,
        PlaybackService playbackService)
    {
        _settingsService = settingsService;
        _libraryService = libraryService;
        _playbackService = playbackService;
        _playbackService.PlaybackError += HandlePlaybackError;
        InitializePlayer();

        SoundsView = CollectionViewSource.GetDefaultView(Sounds);
        SoundsView.Filter = FilterSound;
        SoundsView.SortDescriptions.Add(new SortDescription(nameof(SoundItem.BankOrder), ListSortDirection.Ascending));
        SoundsView.SortDescriptions.Add(new SortDescription(nameof(SoundItem.Category), ListSortDirection.Ascending));
        SoundsView.SortDescriptions.Add(new SortDescription(nameof(SoundItem.DisplayName), ListSortDirection.Ascending));

        _selectedPlaybackModeOption = PlaybackModeOptions[0];

        BrowseFolderCommand = new AsyncRelayCommand(BrowseFolderAsync);
        RescanCommand = new AsyncRelayCommand(() => RescanLibraryAsync(), () => !IsBusy);
        PlaySoundCommand = new AsyncRelayCommand(parameter => PlaySoundAsync(parameter as SoundItem), parameter => parameter is SoundItem sound && sound.IsAvailable);
        StopAllCommand = new RelayCommand(StopAll);
        TogglePauseCommand = new RelayCommand(TogglePauseResume);
        ToggleFavoriteCommand = new AsyncRelayCommand(parameter => ToggleFavoriteAsync(parameter as SoundItem), parameter => parameter is SoundItem);
        SaveTagsCommand = new AsyncRelayCommand(SaveSelectedTagsAsync, () => SelectedSound is not null);
        RefreshDevicesCommand = new RelayCommand(RefreshOutputDevices);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        InitializeOrganization();
    }

    public ObservableCollection<SoundItem> Sounds { get; } = [];

    public ICollectionView SoundsView { get; }

    public ObservableCollection<string> Categories { get; } = [AllCategories];

    public ObservableCollection<string> QuickFilters { get; } = [AllSoundsFilter, FavoritesFilter, RecentFilter];

    public ObservableCollection<OutputDeviceInfo> OutputDevices { get; } = [];

    public ObservableCollection<PlaybackModeOption> PlaybackModeOptions { get; } =
    [
        new("Exclusive", PlaybackMode.Exclusive),
        new("Mix", PlaybackMode.Mix)
    ];

    public AsyncRelayCommand BrowseFolderCommand { get; }

    public AsyncRelayCommand RescanCommand { get; }

    public AsyncRelayCommand PlaySoundCommand { get; }

    public RelayCommand StopAllCommand { get; }

    public RelayCommand TogglePauseCommand { get; }

    public AsyncRelayCommand ToggleFavoriteCommand { get; }

    public AsyncRelayCommand SaveTagsCommand { get; }

    public RelayCommand RefreshDevicesCommand { get; }

    public RelayCommand ToggleThemeCommand { get; }

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
                RefreshOrganizationCommands();
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
                ScheduleSettingsSave();
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
                ScheduleSettingsSave();
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
                ScheduleSettingsSave();
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
                ScheduleSettingsSave();
                _ = ApplyRemoteServerStateAsync();
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
                ScheduleSettingsSave();
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
                ScheduleSettingsSave();
                if (RemoteServerEnabled)
                {
                    _ = ApplyRemoteServerStateAsync(restart: true);
                }
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
                ScheduleSettingsSave();
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

    public bool IsPlaybackPaused => _playbackService.IsPaused;

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (SetProperty(ref _isDarkMode, value))
            {
                _settings.IsDarkMode = value;
                OnPropertyChanged(nameof(ThemeToggleText));
                ScheduleSettingsSave();
            }
        }
    }

    public string ThemeToggleText => IsDarkMode ? "Dark" : "Light";

    public string PauseResumeText => IsPlaybackPaused ? "Resume" : "Pause";

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
        ? "No folder loaded"
        : LibraryPath;

    public void AttachRemoteServer(RemoteControlServer remoteControlServer)
    {
        _remoteControlServer = remoteControlServer;
    }

    public Task LoadAsync() => _loadTask = LoadCoreAsync();

    private async Task LoadCoreAsync()
    {
        var cancellationToken = _lifetimeCts.Token;
        _settings = await _settingsService.LoadAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        LibraryPath = _settings.LibraryPath;
        RemoteServerEnabled = _settings.RemoteServerEnabled && _settings.StartRemoteServerAutomatically;
        StartRemoteServerAutomatically = _settings.StartRemoteServerAutomatically;
        ServerPort = _settings.ServerPort;
        RemotePin = _settings.RemotePin;
        IsDarkMode = _settings.IsDarkMode;
        ReloadBanks();
        OnPropertyChanged(nameof(HotkeysEnabled));
        OnPropertyChanged(nameof(PauseHotkey));
        OnPropertyChanged(nameof(StopHotkey));

        _volume = Math.Clamp(_settings.Volume, 0, 1);
        _playbackService.Volume = _volume;
        OnPropertyChanged(nameof(Volume));
        OnPropertyChanged(nameof(VolumePercentText));

        SelectedPlaybackModeOption = PlaybackModeOptions.FirstOrDefault(option => option.Mode == _settings.PlaybackMode)
            ?? PlaybackModeOptions[0];

        RefreshOutputDevices();

        if (!string.IsNullOrWhiteSpace(LibraryPath))
        {
            await RescanLibraryAsync(cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        _isLoaded = true;
        HotkeysChanged?.Invoke(this, EventArgs.Empty);
        await SaveSettingsAsync(cancellationToken);
        await ApplyRemoteServerStateAsync(cancellationToken: cancellationToken);
    }

    public IReadOnlyList<SoundItem> GetSoundSnapshot()
    {
        var dispatcher = System.Windows.Application.Current.Dispatcher;
        return dispatcher.CheckAccess()
            ? Sounds.ToList()
            : dispatcher.Invoke(() => Sounds.ToList());
    }

    public async Task<bool> PlayByIdAsync(string id)
    {
        return await RunOnUiThreadAsync(() => PlayByIdCoreAsync(id));
    }

    public async Task<bool> ToggleFavoriteByIdAsync(string id)
    {
        return await RunOnUiThreadAsync(() => ToggleFavoriteByIdCoreAsync(id));
    }

    public async Task RescanFromRemoteAsync()
    {
        await RunOnUiThreadAsync(() => RescanLibraryAsync());
    }

    public void StopAll()
    {
        RunOnUiThread(StopAllPlayback);
    }

    public void PauseAll()
    {
        RunOnUiThread(PauseAllPlayback);
    }

    public void ResumeAll()
    {
        RunOnUiThread(ResumeAllPlayback);
    }

    private void PauseAllPlayback()
    {
        if (_playbackService.PauseAll())
        {
            RefreshPlayer();
            StatusMessage = "Playback paused.";
            return;
        }

        RefreshPlayer();
        StatusMessage = "No playback to pause.";
    }

    private void ResumeAllPlayback()
    {
        if (_playbackService.ResumeAll())
        {
            RefreshPlayer();
            StatusMessage = "Playback resumed.";
            return;
        }

        RefreshPlayer();
        StatusMessage = "No paused playback to resume.";
    }

    public ValueTask DisposeAsync() => new(_shutdownTask ??= ShutdownAsync());

    private async Task ShutdownAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        StopPlayerUpdates();
        _settingsSaveTimer?.Stop();
        await _lifetimeCts.CancelAsync();
        try { await Task.WhenAll(_loadTask, _scanTask, _playTask); }
        catch (OperationCanceledException) { }
        _playbackService.PlaybackError -= HandlePlaybackError;
        _playbackService.Dispose();

        var remoteGateTaken = false;
        try
        {
            await _remoteGate.WaitAsync();
            remoteGateTaken = true;
            if (_remoteControlServer is not null)
            {
                using var shutdownCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await _remoteControlServer.StopAsync(shutdownCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            if (remoteGateTaken)
            {
                _remoteGate.Release();
            }
        }

        if (_isLoaded) await SaveSettingsAsync(finalSave: true);
        _lifetimeCts.Dispose();
    }

    private async Task<bool> PlayByIdCoreAsync(string id)
    {
        var sound = _libraryService.FindById(Sounds, id);
        if (sound is null || !sound.IsAvailable)
        {
            return false;
        }

        return await PlaySoundAsync(sound);
    }

    private async Task<bool> ToggleFavoriteByIdCoreAsync(string id)
    {
        var sound = _libraryService.FindById(Sounds, id);
        if (sound is null)
        {
            return false;
        }

        await ToggleFavoriteAsync(sound);
        return true;
    }

    private async Task ApplyRemoteServerStateAsync(bool restart = false, CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            return;
        }

        if (!_isLoaded || _remoteControlServer is null)
        {
            RemoteStatusText = RemoteServerEnabled
                ? "Remote server waiting to start"
                : "Remote server inactive";
            return;
        }

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeCts.Token);
        var token = linkedCts.Token;

        var gateTaken = false;
        try
        {
            await _remoteGate.WaitAsync(token);
            gateTaken = true;
            if (_isDisposed)
            {
                return;
            }

            if (!RemoteServerEnabled)
            {
                await _remoteControlServer.StopAsync(token);
                RemoteStatusText = "Remote server inactive";
                RemoteUrl = $"http://localhost:{ServerPort}";
                return;
            }

            RemoteStatusText = restart ? "Restarting remote server..." : "Starting remote server...";
            await _remoteControlServer.StartAsync(ServerPort, token);
            RemoteUrl = _remoteControlServer.DisplayUrl;
            RemoteStatusText = "Remote server active on private LAN";
        }
        catch (OperationCanceledException) when (_isDisposed || token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _settings.RemoteServerEnabled = false;
            _remoteServerEnabled = false;
            OnPropertyChanged(nameof(RemoteServerEnabled));
            RemoteStatusText = $"Could not start the server: {ex.Message}";
        }
        finally
        {
            if (gateTaken) _remoteGate.Release();
        }
    }

    private void ToggleTheme()
    {
        IsDarkMode = !IsDarkMode;
    }

    private void TogglePauseResume()
    {
        if (IsPlaybackPaused)
        {
            ResumeAllPlayback();
            return;
        }

        PauseAllPlayback();
    }

    private void StopAllPlayback()
    {
        _playbackService.StopAll();
        RefreshPlayer();
        StatusMessage = "Playback stopped.";
    }

    private static Task RunOnUiThreadAsync(Func<Task> action)
    {
        var dispatcher = System.Windows.Application.Current.Dispatcher;
        return dispatcher.CheckAccess()
            ? action()
            : dispatcher.InvokeAsync(action).Task.Unwrap();
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = System.Windows.Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }

    private static Task<T> RunOnUiThreadAsync<T>(Func<Task<T>> action)
    {
        var dispatcher = System.Windows.Application.Current.Dispatcher;
        return dispatcher.CheckAccess()
            ? action()
            : dispatcher.InvokeAsync(action).Task.Unwrap();
    }

    private async Task BrowseFolderAsync()
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = "Choose the main sound folder",
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

    private Task RescanLibraryAsync(CancellationToken cancellationToken = default)
    {
        if (_isDisposed) return Task.CompletedTask;
        if (!_scanTask.IsCompleted) return _scanTask;
        return _scanTask = ScanCoreAsync(cancellationToken);
    }

    private async Task ScanCoreAsync(CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetimeCts.Token);
        var token = linkedCts.Token;
        IsBusy = true;
        try
        {
            StatusMessage = "Scanning library...";
            var scanSettings = SettingsService.Snapshot(_settings);
            var sounds = await _libraryService.ScanAsync(scanSettings, token);
            token.ThrowIfCancellationRequested();

            var metadataByPath = _settings.Sounds.ToDictionary(metadata => metadata.Path, StringComparer.OrdinalIgnoreCase);
            foreach (var metadata in scanSettings.Sounds)
            {
                if (metadataByPath.TryAdd(metadata.Path, metadata))
                    _settings.Sounds.Add(metadata);
            }
            var favorites = _settings.FavoritePaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var sound in sounds)
            {
                metadataByPath.TryGetValue(sound.FullPath, out var latest);
                sound.TagsText = string.Join(", ", latest?.Tags ?? []);
                sound.Alias = latest?.Alias ?? string.Empty;
                sound.Hotkey = latest?.Hotkey ?? string.Empty;
                sound.IsFavorite = favorites.Contains(sound.FullPath);
            }

            Sounds.Clear();
            foreach (var sound in sounds)
            {
                Sounds.Add(sound);
            }

            RefreshCategories();
            RefreshBankOrder();
            RefreshCounts();
            HotkeysChanged?.Invoke(this, EventArgs.Empty);
            SoundsView.Refresh();
            await SaveSettingsAsync(token);
            StatusMessage = $"{AvailableSoundCount} available sounds out of {TotalSoundCount} found.";
        }
        catch (OperationCanceledException) when (_isDisposed || token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not scan the library: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task<bool> PlaySoundAsync(SoundItem? sound)
    {
        var task = PlaySoundCoreAsync(sound);
        _playTask = Task.WhenAll(_playTask, task);
        return task;
    }

    private async Task<bool> PlaySoundCoreAsync(SoundItem? sound)
    {
        if (sound is null || _isDisposed)
        {
            return false;
        }

        PlaybackResult result;
        try { result = await _playbackService.PlayAsync(sound, _settings.PlaybackMode, _lifetimeCts.Token); }
        catch (OperationCanceledException) { return false; }
        if (_isDisposed) return false;
        RefreshPlayer();
        if (!result.Success)
        {
            StatusMessage = result.Error ?? "Could not play the sound.";
            return false;
        }
        sound.LastPlayedUtc = DateTimeOffset.UtcNow;
        UpdateMetadata(sound);

        _settings.RecentPaths.RemoveAll(path => string.Equals(path, sound.FullPath, StringComparison.OrdinalIgnoreCase));
        _settings.RecentPaths.Insert(0, sound.FullPath);
        _settings.RecentPaths = _settings.RecentPaths.Take(100).ToList();

        StatusMessage = $"Playing: {sound.FileName}";
        RefreshCounts();
        SoundsView.Refresh();
        await SaveSettingsAsync();
        return true;
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

        if (!string.IsNullOrWhiteSpace(SelectedSound.Hotkey)
            && !GlobalHotkeyService.TryParse(SelectedSound.Hotkey, out _, out _))
        { StatusMessage = "Use a shortcut with Ctrl, Alt, or Windows."; return; }

        UpdateMetadata(SelectedSound);
        SoundsView.Refresh();
        Interlocked.Increment(ref _libraryRevision);
        HotkeysChanged?.Invoke(this, EventArgs.Empty);
        StatusMessage = $"Sound settings saved for {SelectedSound.DisplayName}.";
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
        Interlocked.Increment(ref _libraryRevision);
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

        if (SelectedBank is { Id.Length: > 0 } && !_selectedBankPaths.Contains(sound.FullPath)) return false;

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
        metadata.Alias = sound.Alias.Trim();
        metadata.Hotkey = sound.Hotkey.Trim();
    }

    private void ScheduleSettingsSave()
    {
        if (!_isLoaded || _isDisposed) return;
        if (_settingsSaveTimer is null)
        {
            _settingsSaveTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _settingsSaveTimer.Tick += async (_, _) =>
            {
                _settingsSaveTimer.Stop();
                await SaveSettingsAsync();
            };
        }
        _settingsSaveTimer.Stop();
        _settingsSaveTimer.Start();
    }

    private async Task SaveSettingsAsync(CancellationToken cancellationToken = default, bool finalSave = false)
    {
        if (!_isLoaded || (_isDisposed && !finalSave)) return;
        var snapshot = SettingsService.Snapshot(_settings);
        try
        {
            await _settingsGate.WaitAsync(cancellationToken);
            try
            {
                await _settingsService.SaveAsync(snapshot, cancellationToken);
            }
            finally
            {
                _settingsGate.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (!_isDisposed)
            {
                StatusMessage = $"Could not save settings: {ex.Message}";
            }
        }
    }

    private void HandlePlaybackError(object? sender, string message)
    {
        PostToUi(() => StatusMessage = message);
    }
}
