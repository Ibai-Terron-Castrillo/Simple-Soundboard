using System.Windows;
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
        DataContext = _viewModel;
        Loaded += HandleLoaded;
        Closing += HandleClosing;
    }

    private async void HandleLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.LoadAsync();
    }

    private void HandleClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _viewModel.Dispose();
    }
}
