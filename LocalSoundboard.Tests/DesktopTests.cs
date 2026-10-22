using System.IO;
using System.Net.Http;
using System.Windows;
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
        try
        {
            await vm.LoadAsync();
            vm.Sounds.Add(new() { Id = "preview", Name = "Stream intro", FullPath = "intro.wav", RelativePath = "Stream/intro.wav", Extension = "WAV", IsAvailable = true });
            vm.SelectedSound = vm.Sounds[0];
            using var renderPlayer = new PlaybackService();
            vm.ActiveSessions.Add(new(new(Guid.NewGuid(), "preview", "Stream intro", false, 12, 90), renderPlayer));
            vm.ActiveSessions.Add(new(new(Guid.NewGuid(), "preview2", "A longer ambient sound for the stream", true, 32, 120), renderPlayer));
            var root = (FrameworkElement)window.Content;
            var output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../artifacts/ui-tests"));
            Directory.CreateDirectory(output);
            foreach (var dark in new[] { false, true })
            foreach (var size in new[] { new Size(1000, 680), new Size(1240, 820) })
            {
                ThemeManager.Apply(window, dark);
                root.Measure(size);
                root.Arrange(new Rect(size));
                root.UpdateLayout();
                var bitmap = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(root);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(output, $"desktop-{(dark ? "dark" : "light")}-{size.Width}.png"));
                encoder.Save(stream);
                Assert.Equal(size.Width, root.ActualWidth);
            }
        }
        finally { await vm.DisposeAsync(); window.Close(); Directory.Delete(directory, true); }
    });

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
