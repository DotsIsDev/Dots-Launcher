// Harmless integration fixture: records how the launcher started this process, then exits.
using System.Text.Json;

File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "launch-result.json"),
    JsonSerializer.Serialize(new { Arguments = args, WorkingDirectory = Environment.CurrentDirectory }));
