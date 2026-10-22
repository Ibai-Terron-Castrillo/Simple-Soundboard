using System.IO;
using LocalSoundboard.Models;
using LocalSoundboard.Services;
using Xunit;

namespace LocalSoundboard.Tests;

public sealed class SettingsTests
{
    [Fact]
    public async Task ExistingSettingsLoadWithNewFieldsAndBanksPersistInOrder()
    {
        var directory = Path.Combine(Path.GetTempPath(), "LocalSoundboard-settings-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var service = new SettingsService(directory);
            await File.WriteAllTextAsync(service.SettingsPath, "{\"Volume\":0.5,\"IsDarkMode\":true,\"FavoritePaths\":[]}");
            var settings = await service.LoadAsync();
            Assert.Equal(0.5, settings.Volume);
            Assert.True(settings.IsDarkMode);
            Assert.Empty(settings.Banks);
            var first = Path.Combine(directory, "first.wav");
            var second = Path.Combine(directory, "second.wav");
            settings.Banks.Add(new() { Name = "Live", SoundPaths = [second, first, second] });
            settings.Sounds.Add(new() { Path = first, Alias = "Intro", Hotkey = "Ctrl+Alt+I" });
            await service.SaveAsync(settings);
            var loaded = await service.LoadAsync();
            Assert.Equal(new[] { second, first }, loaded.Banks.Single().SoundPaths);
            Assert.Equal("Intro", loaded.Sounds.Single().Alias);
            Assert.Equal("Ctrl+Alt+I", loaded.Sounds.Single().Hotkey);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void SnapshotDoesNotShareMutableLists()
    {
        var settings = new AppSettings { Banks = [new() { Name = "Live", SoundPaths = ["original.wav"] }], Sounds = [new() { Path = "original.wav", Tags = ["old"] }] };
        var snapshot = SettingsService.Snapshot(settings);
        settings.Banks[0].SoundPaths.Add("new.wav");
        settings.Sounds[0].Tags.Add("new");
        Assert.Single(snapshot.Banks[0].SoundPaths);
        Assert.Single(snapshot.Sounds[0].Tags);
    }

    [Theory]
    [InlineData("Ctrl+Alt+Space", true)]
    [InlineData("Ctrl+Shift+F12", true)]
    [InlineData("A", false)]
    [InlineData("Shift+A", false)]
    [InlineData("something invalid", false)]
    public void HotkeysRequireAValidModifiedKey(string text, bool valid)
        => Assert.Equal(valid, GlobalHotkeyService.TryParse(text, out _, out _));
}
