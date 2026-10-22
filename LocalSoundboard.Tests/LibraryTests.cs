using System.IO;
using LocalSoundboard.Models;
using LocalSoundboard.Services;
using Xunit;

namespace LocalSoundboard.Tests;

public sealed class LibraryTests
{
    [Fact]
    public async Task ScanFindsNestedFormatsAndKeepsMissingFilesUnavailable()
    {
        var directory = Path.Combine(Path.GetTempPath(), "LocalSoundboard-library-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(directory, "nested"));
        try
        {
            foreach (var extension in AudioLibraryService.SupportedExtensions)
                await File.WriteAllTextAsync(Path.Combine(directory, "nested", "test" + extension.ToUpperInvariant()), "fixture");
            await File.WriteAllTextAsync(Path.Combine(directory, "ignored.txt"), "fixture");
            var missing = Path.Combine(directory, "missing.wav");
            var settings = new AppSettings { LibraryPath = directory, Sounds = [new() { Path = missing }] };
            var sounds = await new AudioLibraryService().ScanAsync(settings);
            Assert.Equal(5, sounds.Count(sound => sound.IsAvailable));
            Assert.Single(sounds, sound => !sound.IsAvailable);
            Assert.All(sounds, sound => Assert.True(AudioLibraryService.IsPathInsideLibrary(directory, sound.FullPath)));
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new AudioLibraryService().ScanAsync(settings, cancelled.Token));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void LibraryContainmentRejectsSiblingPrefixAndTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), "sound-library");
        Assert.True(AudioLibraryService.IsPathInsideLibrary(root, Path.Combine(root, "nested", "test.wav")));
        Assert.False(AudioLibraryService.IsPathInsideLibrary(root, root + "-other/test.wav"));
        Assert.False(AudioLibraryService.IsPathInsideLibrary(root, Path.Combine(root, "..", "outside.wav")));
    }
}
