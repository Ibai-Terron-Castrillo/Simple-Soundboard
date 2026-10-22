using System.IO;

namespace LocalSoundboard.Services;

public static class RemoteWebPage
{
    public static string Html { get; } = Read("index.html");
    public static string Css { get; } = Read("styles.css");
    public static string Script { get; } = Read("app.js");

    private static string Read(string file)
    {
        using var stream = typeof(RemoteWebPage).Assembly.GetManifestResourceStream($"LocalSoundboard.Web.{file}")
            ?? throw new InvalidOperationException($"Missing web resource: {file}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
