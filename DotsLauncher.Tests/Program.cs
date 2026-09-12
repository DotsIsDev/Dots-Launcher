using DotsLauncher.Core;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}

string root = Path.Combine(Path.GetTempPath(), "DotsLauncherTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    Check(LibraryOperations.CreateTitle("TheFarmerWasReplaced.exe") == "The Farmer Was Replaced", "Camel case titles");
    Check(LibraryOperations.CreateTitle("HTTPServerGUI.exe") == "HTTP Server GUI", "Acronym boundaries");
    Check(LibraryOperations.CreateTitle("my_game-launcher.exe") == "my game launcher", "Filename separators");
    Check(LibraryOperations.CreateTitle("Game2Launcher.exe") == "Game2 Launcher", "Number and word boundary");
    string source = Path.Combine(root, "Programs with spaces");
    string nested = Path.Combine(source, "Nested");
    Directory.CreateDirectory(nested);
    string exe = Path.Combine(source, "TheFarmerWasReplaced.exe");
    string nestedExe = Path.Combine(nested, "HTTPServerGUI.EXE");
    File.WriteAllText(exe, "fixture");
    File.WriteAllText(nestedExe, "fixture");
    if (!OperatingSystem.IsWindows())
    {
        File.SetUnixFileMode(exe, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.SetUnixFileMode(nestedExe, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }
    File.WriteAllText(Path.Combine(source, "Readme.txt"), "fixture");
    var shallow = FolderScanner.Scan([new SourceFolder { Path = source, IncludeSubfolders = false }]);
    Check(shallow.Executables.Count == 1 && shallow.Executables[0] == exe, "Non-recursive EXE-only scan");
    var recursive = FolderScanner.Scan([new SourceFolder { Path = source }, new SourceFolder { Path = nested }]);
    Check(recursive.Executables.Count == 2, "Recursive scan deduplicates overlapping folders and accepts uppercase extension");
    var missing = FolderScanner.Scan([new SourceFolder { Path = Path.Combine(root, "Missing") }]);
    Check(missing.Executables.Count == 0 && missing.UnavailableFolders.Count == 1, "Unavailable source is reported");
    var library = new LibraryData();
    var profile = LibraryOperations.AddExecutable(library, exe)!;
    Check(profile.Title == "The Farmer Was Replaced", "Import fills the title");
    string duplicatePath = OperatingSystem.IsWindows() ? exe.ToUpperInvariant() : exe;
    Check(LibraryOperations.AddExecutable(library, duplicatePath) is null, "Paths deduplicate with platform casing rules");
    Check(LibraryOperations.AddExecutable(library, Path.Combine(source, "Readme.txt")) is null, "Non-executables rejected");
    profile.Title = "My custom title";
    profile.Arguments = "--windowed --name \"two words\"";
    profile.IsFavourite = true;
    profile.IconPath = Path.Combine(root, "custom.png");
    library.Folders.Add(new SourceFolder { Path = source, IncludeSubfolders = false });
    library.ViewMode = "List";
    LibraryOperations.AddExecutable(library, exe);
    Check(profile.Title == "My custom title" && profile.IsFavourite, "Reimport preserves profile edits");
    var store = new LibraryStore(Path.Combine(root, "Data"));
    store.Save(library);
    var restored = store.Load();
    Check(restored.Profiles[0].Title == profile.Title && restored.Profiles[0].Arguments == profile.Arguments && restored.Profiles[0].IsFavourite && restored.Profiles[0].IconPath == profile.IconPath && !restored.Folders[0].IncludeSubfolders && restored.ViewMode == "List", "All profile, folder, and view settings survive restart");
    LibraryOperations.RemoveExecutable(library, profile);
    Check(File.Exists(exe) && library.Profiles.Count == 0, "Removal keeps executable on disk");
    Check(LibraryOperations.AddExecutable(library, exe) is null, "Automatic scan respects removed entries");
    store.Save(library);
    Check(LibraryOperations.AddExecutable(store.Load(), exe) is null, "Removed exclusions survive restart");
    Check(LibraryOperations.AddExecutable(library, exe, explicitlyAdded: true) is not null && library.IgnoredPaths.Count == 0, "Explicit add restores removed entry");
    Check(File.Exists(store.FilePath + ".bak"), "Atomic save keeps a backup");
    File.WriteAllText(store.FilePath, "{broken-json");
    var recovered = store.Load();
    Check(recovered.Profiles.Count == 1 && store.RecoveryMessage is not null, "Corrupt library restores last backup");
    Check(Directory.GetFiles(store.DirectoryPath, "library.json.recovered-*").Length == 1, "Corrupt original is preserved");
    Console.WriteLine($"\n{passed} checks passed.");
}
finally { Directory.Delete(root, true); }
