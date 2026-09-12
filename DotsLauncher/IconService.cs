using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace DotsLauncher;

public static class IconService
{
    private static readonly Dictionary<string, IImage> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static IImage Get(string executablePath, string? customPath = null)
    {
        string key = customPath ?? executablePath;
        if (Cache.TryGetValue(key, out var cached)) return cached;
        IImage? image = null;
        try
        {
            if (customPath is not null && File.Exists(customPath)) image = new Bitmap(customPath);
            else if (OperatingSystem.IsWindows() && File.Exists(executablePath)) image = GetWindowsIcon(executablePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or PlatformNotSupportedException) { }
        if (image is null && customPath is not null) return Get(executablePath);
        image ??= new Bitmap(AssetLoader.Open(new Uri("avares://DotsLauncher/Assets/DotsLauncher.png")));
        Cache[key] = image;
        return image;
    }

    public static void Invalidate()
    {
        foreach (var bitmap in Cache.Values.OfType<IDisposable>()) bitmap.Dispose();
        Cache.Clear();
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static Bitmap? GetWindowsIcon(string path)
    {
        using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
        if (icon is null) return null;
        using var drawingBitmap = icon.ToBitmap();
        using var stream = new MemoryStream();
        drawingBitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
        stream.Position = 0;
        return new Bitmap(stream);
    }
}
