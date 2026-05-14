using System.Windows;
using LocalSoundboard.Infrastructure;
using LocalSoundboard.Services;
using LocalSoundboard.ViewModels;

namespace LocalSoundboard;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

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
        await _viewModel.LoadAsync();
        ThemeManager.Apply(this, _viewModel.IsDarkMode);
    }

    private void HandleClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _viewModel.PropertyChanged -= HandleViewModelPropertyChanged;
        _viewModel.Dispose();
    }

    private void HandleViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsDarkMode))
        {
            ThemeManager.Apply(this, _viewModel.IsDarkMode);
        }
    }
}
