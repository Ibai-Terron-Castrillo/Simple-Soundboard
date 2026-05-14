using System.Windows;
using LocalSoundboard.Infrastructure;
using LocalSoundboard.Services;
using LocalSoundboard.ViewModels;

namespace LocalSoundboard;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _isClosing;
    private bool _isDisposed;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel(
            new SettingsService(),
            new AudioLibraryService(),
            new PlaybackService());
        _viewModel.AttachRemoteServer(new RemoteControlServer(_viewModel));
        _viewModel.PropertyChanged += HandleViewModelPropertyChanged;
        DataContext = _viewModel;
        Loaded += HandleLoaded;
        Closing += HandleClosing;
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
            Close();
        }
    }

    private void HandleViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsDarkMode))
        {
            ThemeManager.Apply(this, _viewModel.IsDarkMode);
        }
    }
}
