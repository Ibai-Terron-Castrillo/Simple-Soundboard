using System.Collections.ObjectModel;
using LocalSoundboard.Infrastructure;
using LocalSoundboard.Models;
using LocalSoundboard.Services;

namespace LocalSoundboard.ViewModels;

public sealed partial class MainViewModel
{
    private readonly SoundBank _allSoundsBank = new() { Id = string.Empty, Name = "All sounds" };
    private SoundBank? _selectedBank;
    private SoundBank? _targetBank;
    private string _bankName = string.Empty;
    private string _hotkeyStatusText = "Global shortcuts disabled";
    private long _libraryRevision;

    public ObservableCollection<SoundBank> Banks { get; } = [];
    public ObservableCollection<SoundBank> BankFilters { get; } = [];
    public event EventHandler? HotkeysChanged;
    public RelayCommand CreateBankCommand { get; private set; } = null!;
    public RelayCommand RenameBankCommand { get; private set; } = null!;
    public RelayCommand DeleteBankCommand { get; private set; } = null!;
    public RelayCommand AddToBankCommand { get; private set; } = null!;
    public RelayCommand RemoveFromBankCommand { get; private set; } = null!;
    public RelayCommand MoveSoundUpCommand { get; private set; } = null!;
    public RelayCommand MoveSoundDownCommand { get; private set; } = null!;
    public RelayCommand ClearHotkeyCommand { get; private set; } = null!;
    public RelayCommand ClearSearchCommand { get; private set; } = null!;
    public long LibraryRevision => Interlocked.Read(ref _libraryRevision);

    public SoundBank? SelectedBank
    {
        get => _selectedBank;
        set
        {
            if (SetProperty(ref _selectedBank, value))
            {
                RefreshBankOrder();
                RefreshOrganizationCommands();
            }
        }
    }

    public SoundBank? TargetBank
    {
        get => _targetBank;
        set
        {
            if (SetProperty(ref _targetBank, value))
            {
                BankName = value?.Name ?? string.Empty;
                RefreshOrganizationCommands();
            }
        }
    }
    public string BankName
    {
        get => _bankName;
        set { if (SetProperty(ref _bankName, value)) RefreshOrganizationCommands(); }
    }
    public bool HotkeysEnabled
    {
        get => _settings.HotkeysEnabled;
        set
        {
            if (_settings.HotkeysEnabled == value) return;
            _settings.HotkeysEnabled = value;
            OnPropertyChanged();
            HotkeysChanged?.Invoke(this, EventArgs.Empty);
            _ = SaveSettingsAsync();
        }
    }
    public string PauseHotkey
    {
        get => _settings.PauseHotkey;
        set
        {
            if (_settings.PauseHotkey == value) return;
            _settings.PauseHotkey = value;
            OnPropertyChanged();
            HotkeysChanged?.Invoke(this, EventArgs.Empty);
            _ = SaveSettingsAsync();
        }
    }
    public string StopHotkey
    {
        get => _settings.StopHotkey;
        set
        {
            if (_settings.StopHotkey == value) return;
            _settings.StopHotkey = value;
            OnPropertyChanged();
            HotkeysChanged?.Invoke(this, EventArgs.Empty);
            _ = SaveSettingsAsync();
        }
    }
    public string HotkeyStatusText { get => _hotkeyStatusText; set => SetProperty(ref _hotkeyStatusText, value); }

    private void InitializeOrganization()
    {
        CreateBankCommand = new RelayCommand(() =>
        {
            var name = BankName.Trim();
            if (Banks.Any(bank => string.Equals(bank.Name, name, StringComparison.OrdinalIgnoreCase)))
            { StatusMessage = "A bank with that name already exists."; return; }
            var bank = new SoundBank { Name = name };
            _settings.Banks.Add(bank);
            Banks.Add(bank);
            BankFilters.Add(bank);
            TargetBank = bank;
            OrganizationChanged();
        }, () => !string.IsNullOrWhiteSpace(BankName));
        RenameBankCommand = new RelayCommand(() =>
        {
            if (Banks.Any(bank => bank != TargetBank && string.Equals(bank.Name, BankName.Trim(), StringComparison.OrdinalIgnoreCase)))
            { StatusMessage = "A bank with that name already exists."; return; }
            TargetBank!.Name = BankName.Trim();
            var selected = SelectedBank;
            var target = TargetBank;
            ReloadBanks();
            TargetBank = Banks.FirstOrDefault(bank => bank.Id == target.Id);
            SelectedBank = BankFilters.FirstOrDefault(bank => bank.Id == selected?.Id) ?? _allSoundsBank;
            OrganizationChanged();
        }, () => TargetBank is not null && !string.IsNullOrWhiteSpace(BankName));
        DeleteBankCommand = new RelayCommand(() =>
        {
            _settings.Banks.Remove(TargetBank!);
            ReloadBanks();
            OrganizationChanged();
        }, () => TargetBank is not null);
        AddToBankCommand = new RelayCommand(() =>
        {
            if (!TargetBank!.SoundPaths.Contains(SelectedSound!.FullPath, StringComparer.OrdinalIgnoreCase))
                TargetBank.SoundPaths.Add(SelectedSound.FullPath);
            OrganizationChanged();
        }, () => TargetBank is not null && SelectedSound is not null);
        RemoveFromBankCommand = new RelayCommand(() =>
        {
            TargetBank!.SoundPaths.RemoveAll(path => string.Equals(path, SelectedSound!.FullPath, StringComparison.OrdinalIgnoreCase));
            OrganizationChanged();
        }, () => TargetBank is not null && SelectedSound is not null
            && TargetBank.SoundPaths.Contains(SelectedSound.FullPath, StringComparer.OrdinalIgnoreCase));
        MoveSoundUpCommand = new RelayCommand(() => MoveSound(-1), () => CanMoveSound(-1));
        MoveSoundDownCommand = new RelayCommand(() => MoveSound(1), () => CanMoveSound(1));
        ClearHotkeyCommand = new RelayCommand(() => { if (SelectedSound is not null) SelectedSound.Hotkey = string.Empty; });
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
        BankFilters.Add(_allSoundsBank);
        SelectedBank = _allSoundsBank;
    }

    private void ReloadBanks()
    {
        var selectedId = SelectedBank?.Id;
        var targetId = TargetBank?.Id;
        Banks.Clear();
        BankFilters.Clear();
        BankFilters.Add(_allSoundsBank);
        foreach (var bank in _settings.Banks) { Banks.Add(bank); BankFilters.Add(bank); }
        SelectedBank = BankFilters.FirstOrDefault(bank => bank.Id == selectedId) ?? _allSoundsBank;
        TargetBank = Banks.FirstOrDefault(bank => bank.Id == targetId) ?? Banks.FirstOrDefault();
    }

    private void OrganizationChanged()
    {
        Interlocked.Increment(ref _libraryRevision);
        RefreshBankOrder();
        RefreshOrganizationCommands();
        _ = SaveSettingsAsync();
    }

    private void RefreshBankOrder()
    {
        foreach (var sound in Sounds)
            sound.BankOrder = SelectedBank is { Id.Length: > 0 } bank
                ? bank.SoundPaths.FindIndex(path => string.Equals(path, sound.FullPath, StringComparison.OrdinalIgnoreCase)) : 0;
        SoundsView.Refresh();
    }

    private bool CanMoveSound(int delta)
    {
        if (SelectedBank is not { Id.Length: > 0 } bank || SelectedSound is null) return false;
        var index = bank.SoundPaths.FindIndex(path => string.Equals(path, SelectedSound.FullPath, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + delta >= 0 && index + delta < bank.SoundPaths.Count;
    }

    private void MoveSound(int delta)
    {
        if (!CanMoveSound(delta)) return;
        var paths = SelectedBank!.SoundPaths;
        var index = paths.FindIndex(path => string.Equals(path, SelectedSound!.FullPath, StringComparison.OrdinalIgnoreCase));
        (paths[index], paths[index + delta]) = (paths[index + delta], paths[index]);
        OrganizationChanged();
    }

    private void RefreshOrganizationCommands()
    {
        foreach (var command in new[] { CreateBankCommand, RenameBankCommand, DeleteBankCommand,
            AddToBankCommand, RemoveFromBankCommand, MoveSoundUpCommand, MoveSoundDownCommand }) command?.RaiseCanExecuteChanged();
    }

    public IReadOnlyList<HotkeyBinding> GetHotkeyBindings()
    {
        if (!HotkeysEnabled) return [];
        var bindings = new List<HotkeyBinding>
        {
            new("Pause / resume", PauseHotkey, TogglePauseResume),
            new("Stop all", StopHotkey, StopAll)
        };
        foreach (var sound in Sounds.Where(sound => sound.IsAvailable && !string.IsNullOrWhiteSpace(sound.Hotkey)))
            bindings.Add(new(sound.DisplayName, sound.Hotkey, () => _ = PlaySoundAsync(sound)));
        return bindings;
    }

    public IReadOnlyList<SoundBankSnapshot> GetBankSnapshot()
    {
        var dispatcher = System.Windows.Application.Current.Dispatcher;
        if (!dispatcher.CheckAccess()) return dispatcher.Invoke(GetBankSnapshot);
        return Banks.Select(bank => new SoundBankSnapshot(bank.Id, bank.Name,
            bank.SoundPaths.Select(path => Sounds.FirstOrDefault(sound => string.Equals(sound.FullPath, path, StringComparison.OrdinalIgnoreCase))?.Id)
                .Where(id => id is not null).Cast<string>().ToArray())).ToArray();
    }
}
