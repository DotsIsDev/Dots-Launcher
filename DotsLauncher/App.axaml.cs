using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DotsLauncher.Core;

namespace DotsLauncher;

public partial class App : Application
{
    private Mutex? instanceMutex;
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            string dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DotsLauncher");
            string[] args = desktop.Args ?? [];
            if (args.Contains("--portable") || File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.flag")))
                dataDirectory = Path.Combine(AppContext.BaseDirectory, "data");
            int index = Array.IndexOf(args, "--data-dir");
            if (index >= 0 && index + 1 < args.Length) dataDirectory = Path.GetFullPath(args[index + 1]);
            string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(dataDirectory)))[..20];
            instanceMutex = new Mutex(true, "DotsLauncher-" + key, out bool created);
            if (created) desktop.MainWindow = new MainWindow(new LibraryStore(dataDirectory));
            else desktop.Shutdown();
            desktop.Exit += (_, _) => instanceMutex?.Dispose();
        }
        base.OnFrameworkInitializationCompleted();
    }
}
