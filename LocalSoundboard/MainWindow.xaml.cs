using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using TextBox = System.Windows.Controls.TextBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using LocalSoundboard.Infrastructure;
using LocalSoundboard.Services;
using LocalSoundboard.ViewModels;

namespace LocalSoundboard;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _isClosing;
    private bool _isDisposed;
    private GlobalHotkeyService? _hotkeys;

    public MainWindow() : this(new MainViewModel(new SettingsService(), new AudioLibraryService(), new PlaybackService()))
    {
    }

    internal MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _viewModel.AttachRemoteServer(new RemoteControlServer(_viewModel));
        _viewModel.PropertyChanged += HandleViewModelPropertyChanged;
        DataContext = _viewModel;
        Loaded += HandleLoaded;
        Closing += HandleClosing;
        SourceInitialized += (_, _) =>
        {
            _hotkeys = new GlobalHotkeyService(new WindowInteropHelper(this).Handle);
            _viewModel.HotkeysChanged += RefreshHotkeys;
        };
        ThemeManager.Apply(this, isDarkMode: false);
    }

    private async void HandleLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.LoadAsync();
            if (!_isClosing)
            {
                ThemeManager.Apply(this, _viewModel.IsDarkMode);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async void HandleClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_isDisposed)
        {
            return;
        }

        e.Cancel = true;
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        _viewModel.HotkeysChanged -= RefreshHotkeys;
        _hotkeys?.Dispose();
        IsEnabled = false;
        _viewModel.PropertyChanged -= HandleViewModelPropertyChanged;

        try
        {
            await _viewModel.DisposeAsync();
        }
        finally
        {
            _isDisposed = true;
            Closing -= HandleClosing;
            _ = Dispatcher.BeginInvoke(Close);
        }
    }

    private void HandleViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsDarkMode))
        {
            ThemeManager.Apply(this, _viewModel.IsDarkMode);
        }
    }

    private void RefreshHotkeys(object? sender, EventArgs e)
    {
        if (_isClosing || _hotkeys is null) return;
        var errors = _hotkeys.Apply(_viewModel.GetHotkeyBindings());
        _viewModel.HotkeyStatusText = errors.Count > 0 ? string.Join(Environment.NewLine, errors)
            : _viewModel.HotkeysEnabled ? "Global shortcuts active" : "Global shortcuts disabled";
    }

    private void BeginSeek(object sender, MouseButtonEventArgs e)
    {
        if (sender is Slider { DataContext: PlaybackSessionViewModel session }) session.IsSeeking = true;
    }

    private void EndSeek(object sender, MouseButtonEventArgs e) => CommitSeek(sender);
    private void BeginKeySeek(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right or Key.Home or Key.End or Key.PageDown or Key.PageUp
            && sender is Slider { DataContext: PlaybackSessionViewModel session }) session.IsSeeking = true;
    }
    private void KeySeek(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Left or Key.Right or Key.Home or Key.End or Key.PageDown or Key.PageUp) CommitSeek(sender);
    }

    private void CommitSeek(object sender)
    {
        if (sender is not Slider { DataContext: PlaybackSessionViewModel session }) return;
        session.IsSeeking = false;
        session.SeekCommand.Execute(null);
    }

    private void CaptureHotkey(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Tab) return;
        e.Handled = true;
        if (key is Key.Escape or Key.Back or Key.Delete) textBox.Text = string.Empty;
        else if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) != 0
            && key is not (Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin))
        {
            var gesture = new KeyGesture(key, Keyboard.Modifiers);
            textBox.Text = new KeyGestureConverter().ConvertToInvariantString(gesture) ?? string.Empty;
        }
        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }
}
