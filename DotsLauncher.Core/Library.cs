using System.Text.Json;
using System.Text.RegularExpressions;

namespace DotsLauncher.Core;

public sealed class ExecutableProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Path { get; set; } = "";
    public string Title { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string? IconPath { get; set; }
    public bool IsFavourite { get; set; }
}

public sealed class SourceFolder
{
    public string Path { get; set; } = "";
    public bool IncludeSubfolders { get; set; } = true;
}

public sealed class LibraryData
{
    public List<ExecutableProfile> Profiles { get; set; } = [];
    public List<SourceFolder> Folders { get; set; } = [];
    public List<string> IgnoredPaths { get; set; } = [];
    public string ViewMode { get; set; } = "Grid";
}

public static partial class LibraryOperations
{
    public static string NormalizePath(string path) => System.IO.Path.GetFullPath(path.Trim());
    public static StringComparer PathComparer { get; } = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    public static bool SamePath(string a, string b) => PathComparer.Equals(a, b);

    public static string CreateTitle(string path)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(path);
        name = AcronymBoundary().Replace(name, "$1 $2");
        name = WordBoundary().Replace(name, "$1 $2");
        name = Separators().Replace(name, " ");
        return Whitespace().Replace(name, " ").Trim();
    }

    public static ExecutableProfile? AddExecutable(LibraryData data, string path, bool explicitlyAdded = false)
    {
        path = NormalizePath(path);
        if (!PlatformExecutables.IsLaunchable(path))
            return null;
        if (explicitlyAdded) data.IgnoredPaths.RemoveAll(p => SamePath(p, path));
        if (data.Profiles.Any(p => SamePath(p.Path, path)) || data.IgnoredPaths.Any(p => SamePath(p, path)))
            return null;
        var profile = new ExecutableProfile { Path = path, Title = CreateTitle(path) };
        data.Profiles.Add(profile);
        return profile;
    }

    public static void RemoveExecutable(LibraryData data, ExecutableProfile profile)
    {
        data.Profiles.Remove(profile);
        if (!data.IgnoredPaths.Any(p => SamePath(p, profile.Path))) data.IgnoredPaths.Add(profile.Path);
    }

    [GeneratedRegex("([A-Z]+)([A-Z][a-z])")] private static partial Regex AcronymBoundary();
    [GeneratedRegex("([a-z0-9])([A-Z])")] private static partial Regex WordBoundary();
    [GeneratedRegex("[_-]+")] private static partial Regex Separators();
    [GeneratedRegex(@"\s+")] private static partial Regex Whitespace();
}

public sealed record ScanResult(List<string> Executables, List<string> UnavailableFolders);

public static class FolderScanner
{
    public static ScanResult Scan(IEnumerable<SourceFolder> folders)
    {
        var files = new HashSet<string>(LibraryOperations.PathComparer);
        var unavailable = new List<string>();
        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder.Path)) { unavailable.Add(folder.Path); continue; }
            try
            {
                var options = new EnumerationOptions
                {
                    RecurseSubdirectories = folder.IncludeSubfolders,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.ReparsePoint,
                    MatchCasing = MatchCasing.CaseInsensitive
                };
                foreach (var path in Directory.EnumerateFiles(folder.Path, "*", options))
                    if (PlatformExecutables.IsLaunchable(path) && !PlatformExecutables.IsInsideMacApp(path))
                        files.Add(LibraryOperations.NormalizePath(path));
                if (OperatingSystem.IsMacOS())
                    foreach (var path in Directory.EnumerateDirectories(folder.Path, "*.app", options))
                        files.Add(LibraryOperations.NormalizePath(path));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                unavailable.Add(folder.Path);
            }
        }
        return new ScanResult(files.Order(LibraryOperations.PathComparer).ToList(), unavailable);
    }
}

public static class PlatformExecutables
{
    public static bool IsLaunchable(string path)
    {
        if (OperatingSystem.IsWindows())
            return File.Exists(path) && System.IO.Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase);
        if (OperatingSystem.IsMacOS() && Directory.Exists(path) && System.IO.Path.GetExtension(path).Equals(".app", StringComparison.OrdinalIgnoreCase))
            return true;
        if (!File.Exists(path)) return false;
        try
        {
            var mode = File.GetUnixFileMode(path);
            return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException) { return false; }
    }

    public static bool IsInsideMacApp(string path) => path.Contains(".app" + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}

public sealed class LibraryStore(string directory)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string DirectoryPath { get; } = directory;
    public string FilePath => System.IO.Path.Combine(DirectoryPath, "library.json");
    public string? RecoveryMessage { get; private set; }

    public LibraryData Load()
    {
        if (!File.Exists(FilePath)) return new();
        try { return Read(FilePath); }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // Keep the damaged file available for recovery before any subsequent save.
            var preserved = FilePath + ".recovered-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff");
            File.Copy(FilePath, preserved, false);
            if (File.Exists(FilePath + ".bak"))
            {
                try
                {
                    var backup = Read(FilePath + ".bak");
                    RecoveryMessage = "Library restored from its backup. The original file was preserved.";
                    return backup;
                }
                catch (Exception backupError) when (backupError is JsonException or IOException or UnauthorizedAccessException) { }
            }
            RecoveryMessage = "The saved library could not be read. The original file was preserved in " + DirectoryPath;
            return new();
        }
    }

    private static LibraryData Read(string path)
    {
        var result = JsonSerializer.Deserialize<LibraryData>(File.ReadAllText(path), JsonOptions)
            ?? throw new JsonException("Empty library.");
        if (result.Profiles is null || result.Folders is null || result.IgnoredPaths is null)
            throw new JsonException("Invalid library lists.");
        if (result.Profiles.Any(p => p is null || string.IsNullOrWhiteSpace(p.Path) || p.Title is null) ||
            result.Folders.Any(p => p is null || string.IsNullOrWhiteSpace(p.Path)) || result.IgnoredPaths.Any(p => p is null))
            throw new JsonException("Invalid library entries.");
        if (result.ViewMode is not ("Grid" or "List")) result.ViewMode = "Grid";
        return result;
    }

    public void Save(LibraryData data)
    {
        Directory.CreateDirectory(DirectoryPath);
        var tempPath = FilePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(data, JsonOptions));
        if (File.Exists(FilePath)) File.Replace(tempPath, FilePath, FilePath + ".bak");
        else File.Move(tempPath, FilePath);
    }
}
