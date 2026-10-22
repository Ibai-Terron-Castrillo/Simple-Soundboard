using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Threading;
using LocalSoundboard.Infrastructure;
using LocalSoundboard.Models;
using LocalSoundboard.Services;
using LocalSoundboard.ViewModels;
using Xunit;

namespace LocalSoundboard.Tests;

[Collection("Desktop")]
public sealed class DesktopTests
{
    [Fact]
    public Task RemotePanelNeverOffersLocalhostAsTheTabletAddress() => StaHost.Run(async () =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "LocalSoundboard-network-" + Guid.NewGuid());
        var settings = new SettingsService(directory);
        var port = RemoteServerTests.FreePort();
        await settings.SaveAsync(new() { ServerPort = port });
        IReadOnlyList<string> addresses = [];
        var vm = new MainViewModel(settings, new AudioLibraryService(), new PlaybackService());
        var server = new RemoteControlServer(vm, _ => addresses);
        vm.AttachRemoteServer(server);
        try
        {
            await vm.LoadAsync();
            Assert.False(vm.IsRemoteServerRunning);
            Assert.Empty(vm.RemoteUrl);
            Assert.Empty(vm.RemoteUrls);
            vm.RemoteServerEnabled = true;
            await Until(() => vm.IsRemoteServerRunning);
            Assert.Empty(vm.RemoteUrls);
            Assert.Contains("No LAN IPv4", vm.RemoteStatusText);
            addresses = [$"http://192.168.1.10:{port}", $"http://192.168.56.1:{port}"];
            vm.RefreshRemoteAddressesCommand.Execute(null);
            await Until(() => vm.RemoteUrls.Count == 2);
            Assert.Equal(addresses[0], vm.RemoteUrl);
            vm.RemoteUrl = addresses[1];
            vm.RefreshRemoteAddressesCommand.Execute(null);
            await Until(() => vm.RefreshRemoteAddressesCommand.CanExecute(null));
            Assert.Equal(addresses[1], vm.RemoteUrl);
            vm.RemoteServerEnabled = false;
            await Until(() => vm.RemoteStatusText == "Remote server inactive");
            Assert.Empty(vm.RemoteUrl);
            Assert.Empty(vm.RemoteUrls);
            Assert.False(vm.IsRemoteServerRunning);
            vm.ServerPort = RemoteServerTests.FreePort();
            Assert.Equal($"http://localhost:{vm.ServerPort}", vm.RemoteLocalUrl);
            Assert.Empty(vm.RemoteUrl);
        }
        finally { await vm.DisposeAsync(); Directory.Delete(directory, true); }
    });

    [Fact]
    public Task GlobalHotkeysReportConflictsAndUnregisterOnDispose() => StaHost.Run(() =>
    {
        using var firstWindow = new HwndSource(new HwndSourceParameters("Hotkey test one") { Width = 1, Height = 1 });
        using var secondWindow = new HwndSource(new HwndSourceParameters("Hotkey test two") { Width = 1, Height = 1 });
        using var first = new GlobalHotkeyService(firstWindow.Handle);
        using var second = new GlobalHotkeyService(secondWindow.Handle);
        var binding = new HotkeyBinding("Test", "Ctrl+Alt+Shift+F23", () => { });
        Assert.Empty(first.Apply([binding]));
        Assert.NotEmpty(second.Apply([binding]));
        first.Dispose();
        Assert.Empty(second.Apply([binding]));
        return Task.CompletedTask;
    });

    [Fact]
    public Task ViewModelCanToggleServerChangePortAndShutdown() => StaHost.Run(async () =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "LocalSoundboard-lifecycle-" + Guid.NewGuid());
        var settings = new SettingsService(directory);
        var port = RemoteServerTests.FreePort();
        await settings.SaveAsync(new() { ServerPort = port, RemoteServerEnabled = true, StartRemoteServerAutomatically = true });
        var vm = new MainViewModel(settings, new AudioLibraryService(), new PlaybackService());
        var server = new RemoteControlServer(vm);
        vm.AttachRemoteServer(server);
        try
        {
            await vm.LoadAsync();
            Assert.True(server.IsRunning);
            vm.RemoteServerEnabled = false;
            await Until(() => !server.IsRunning);
            vm.RemoteServerEnabled = true;
            await Until(() => server.IsRunning);
            var secondPort = RemoteServerTests.FreePort();
            vm.ServerPort = secondPort;
            await Until(() => server.DisplayUrl.EndsWith($":{secondPort}"));
            using var client = new HttpClient();
            Assert.True((await client.GetAsync($"http://127.0.0.1:{secondPort}/api/status")).IsSuccessStatusCode);
            await vm.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
            Assert.False(server.IsRunning);
        }
        finally { await vm.DisposeAsync(); Directory.Delete(directory, true); }
    });

    [Fact]
    public Task BanksKeepSelectionOrderAndPersist() => StaHost.Run(async () =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "LocalSoundboard-banks-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "one.wav"), "fixture");
        await File.WriteAllTextAsync(Path.Combine(directory, "two.wav"), "fixture");
        var settings = new SettingsService(Path.Combine(directory, "settings"));
        await settings.SaveAsync(new() { LibraryPath = directory });
        await using var vm = new MainViewModel(settings, new AudioLibraryService(), new PlaybackService());
        try
        {
            await vm.LoadAsync();
            vm.BankName = "Live";
            vm.CreateBankCommand.Execute(null);
            vm.SelectedSound = vm.Sounds[1];
            vm.AddToBankCommand.Execute(null);
            vm.SelectedSound = vm.Sounds[0];
            vm.AddToBankCommand.Execute(null);
            vm.SelectedBank = vm.Banks.Single();
            Assert.Equal(vm.Sounds[1].Id, vm.SoundsView.Cast<SoundItem>().First().Id);
            vm.MoveSoundUpCommand.Execute(null);
            Assert.Equal(vm.Sounds[0].Id, vm.SoundsView.Cast<SoundItem>().First().Id);
            await vm.DisposeAsync();
            Assert.Equal("Live", (await settings.LoadAsync()).Banks.Single().Name);
        }
        finally { await vm.DisposeAsync(); Directory.Delete(directory, true); }
    });

    [Fact]
    public Task WorkspaceRendersLightDarkAtMinimumAndDesktopSize() => StaHost.Run(async () =>
    {
        var directory = Path.Combine(Path.GetTempPath(), "LocalSoundboard-render-" + Guid.NewGuid());
        var settings = new SettingsService(directory);
        await settings.SaveAsync(new());
        var vm = new MainViewModel(settings, new AudioLibraryService(), new PlaybackService());
        var window = new MainWindow(vm);
        var port = RemoteServerTests.FreePort();
        vm.AttachRemoteServer(new RemoteControlServer(vm, port => [$"http://192.168.100.100:{port}"]));
        try
        {
            await vm.LoadAsync();
            vm.ServerPort = port;
            vm.RemoteServerEnabled = true;
            await Until(() => vm.IsRemoteServerRunning);
            vm.Sounds.Add(new() { Id = "preview", Name = "Stream intro", FullPath = "intro.wav", RelativePath = "Stream/intro.wav", Extension = "WAV", IsAvailable = true });
            vm.SelectedSound = vm.Sounds[0];
            using var renderPlayer = new PlaybackService();
            vm.ActiveSessions.Add(new(new(Guid.NewGuid(), "preview", "Stream intro", false, 12, 90), renderPlayer));
            vm.ActiveSessions.Add(new(new(Guid.NewGuid(), "preview2", "A longer ambient sound for the stream", true, 32, 120), renderPlayer));
            var root = (FrameworkElement)window.Content;
            var tabs = Descendants<TabControl>(root).Single();
            var output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../artifacts/ui-tests"));
            Directory.CreateDirectory(output);
            foreach (var dark in new[] { false, true })
            foreach (var size in new[] { new Size(1000, 680), new Size(1240, 820) })
            foreach (var remote in new[] { false, true })
            {
                tabs.SelectedIndex = remote ? 2 : 0;
                ThemeManager.Apply(window, dark);
                root.Measure(size);
                root.Arrange(new Rect(size));
                root.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(root);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(output, $"desktop-{(remote ? "remote-" : "")}{(dark ? "dark" : "light")}-{size.Width}.png"));
                encoder.Save(stream);
                Assert.Equal(size.Width, root.ActualWidth);
                if (remote)
                {
                    var address = Descendants<ComboBox>(root).Single(combo => ReferenceEquals(combo.ItemsSource, vm.RemoteUrls));
                    Assert.Equal(vm.RemoteUrl, address.SelectedItem);
                    Assert.Contains(Descendants<TextBlock>(address), text => text.Text == vm.RemoteUrl);
                }
            }
        }
        finally { await vm.DisposeAsync(); window.Close(); Directory.Delete(directory, true); }
    });

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static async Task Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(20);
        Assert.True(condition());
    }

    private static class StaHost
    {
        private static readonly TaskCompletionSource<Dispatcher> Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        static StaHost()
        {
            var thread = new Thread(() =>
            {
                var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
                Ready.SetResult(app.Dispatcher);
                Dispatcher.Run();
            }) { IsBackground = true };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
        public static async Task Run(Func<Task> action) => await (await Ready.Task).InvokeAsync(action).Task.Unwrap();
    }
}
