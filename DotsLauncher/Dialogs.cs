using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using DotsLauncher.Core;

namespace DotsLauncher;

public static class UiDialogs
{
    private static readonly IBrush Panel = Brush.Parse("#1C2023");
    private static readonly IBrush Border = Brush.Parse("#353B40");
    private static readonly IBrush Muted = Brush.Parse("#9DA7AE");

    public static async Task ShowMessage(Window owner, string message, string title = "Dots Launcher")
    {
        var window = BaseWindow(title, 470, 230);
        var ok = new Button { Content = "OK", HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 84 };
        ok.Click += (_, _) => window.Close();
        window.Content = new StackPanel { Margin = new Thickness(24), Spacing = 22, Children = { new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap }, ok } };
        await window.ShowDialog(owner);
    }

    public static async Task<bool> EditProfile(Window owner, ExecutableProfile profile, string dataDirectory)
    {
        var window = BaseWindow("Edit Profile", 560, 390);
        var title = new TextBox { Text = profile.Title };
        var arguments = new TextBox { Text = profile.Arguments, PlaceholderText = "Launch arguments" };
        var iconPath = new TextBox { Text = profile.IconPath ?? "", IsReadOnly = true, PlaceholderText = "Use executable icon" };
        var browse = new Button { Content = "Browse icon" };
        browse.Click += async (_, _) =>
        {
            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose an icon",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Images") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.ico"] }]
            });
            if (files.Count > 0) iconPath.Text = files[0].TryGetLocalPath() ?? "";
        };
        var clear = new Button { Content = "Use default" };
        clear.Click += (_, _) => iconPath.Clear();
        var cancel = new Button { Content = "Cancel" };
        cancel.Click += (_, _) => window.Close(false);
        var save = new Button { Content = "Save", Background = Brush.Parse("#A8D5C0"), Foreground = Brush.Parse("#101B16") };
        save.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(title.Text)) return;
            profile.Title = title.Text.Trim();
            profile.Arguments = arguments.Text ?? "";
            string? selected = string.IsNullOrWhiteSpace(iconPath.Text) ? null : iconPath.Text;
            if (selected is not null && File.Exists(selected) && (profile.IconPath is null || !Path.GetFullPath(selected).Equals(Path.GetFullPath(profile.IconPath), StringComparison.OrdinalIgnoreCase)))
            {
                string iconDir = Path.Combine(dataDirectory, "icons");
                Directory.CreateDirectory(iconDir);
                string target = Path.Combine(iconDir, profile.Id + Path.GetExtension(selected).ToLowerInvariant());
                File.Copy(selected, target, true);
                selected = target;
            }
            profile.IconPath = selected;
            window.Close(true);
        };
        var iconRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 8 };
        iconRow.Children.Add(iconPath); iconRow.Children.Add(browse); iconRow.Children.Add(clear);
        Grid.SetColumn(browse, 1); Grid.SetColumn(clear, 2);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Children = { cancel, save } };
        window.Content = new StackPanel
        {
            Margin = new Thickness(24), Spacing = 9,
            Children = { Label("Title"), title, Label("Launch options"), arguments, Label("Icon"), iconRow, new TextBlock { Text = "PNG, JPEG, BMP, or ICO", Foreground = Muted, FontSize = 12 }, actions }
        };
        return await window.ShowDialog<bool>(owner);
    }

    public static async Task<List<SourceFolder>?> ManageFolders(Window owner, IEnumerable<SourceFolder> source)
    {
        var result = source.Select(f => new SourceFolder { Path = f.Path, IncludeSubfolders = f.IncludeSubfolders }).ToList();
        var window = BaseWindow("Manage Folders", 680, 440);
        var list = new StackPanel { Spacing = 8 };
        void Render()
        {
            list.Children.Clear();
            foreach (var folder in result.ToList())
            {
                var recursive = new CheckBox { Content = "Include subfolders", IsChecked = folder.IncludeSubfolders, VerticalAlignment = VerticalAlignment.Center };
                recursive.IsCheckedChanged += (_, _) => folder.IncludeSubfolders = recursive.IsChecked == true;
                var remove = new Button { Content = "Remove" };
                remove.Click += (_, _) => { result.Remove(folder); Render(); };
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 12 };
                row.Children.Add(new TextBlock { Text = folder.Path, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
                row.Children.Add(recursive); row.Children.Add(remove); Grid.SetColumn(recursive, 1); Grid.SetColumn(remove, 2);
                list.Children.Add(new Border { Background = Panel, BorderBrush = Border, BorderThickness = new Thickness(1), Padding = new Thickness(12), Child = row });
            }
        }
        Render();
        var cancel = new Button { Content = "Cancel" }; cancel.Click += (_, _) => window.Close(null);
        var save = new Button { Content = "Save", Background = Brush.Parse("#A8D5C0"), Foreground = Brush.Parse("#101B16") }; save.Click += (_, _) => window.Close(result);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Children = { cancel, save } };
        window.Content = new Grid
        {
            Margin = new Thickness(24), RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 14,
            Children = { new TextBlock { Text = "Connected folders", FontSize = 20, FontWeight = FontWeight.SemiBold }, new ScrollViewer { Content = list }, actions }
        };
        Grid.SetRow((Control)((Grid)window.Content).Children[1], 1); Grid.SetRow(actions, 2);
        return await window.ShowDialog<List<SourceFolder>?>(owner);
    }

    private static TextBlock Label(string text) => new() { Text = text, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 5, 0, 0) };
    private static Window BaseWindow(string title, double width, double height) => new()
    {
        Title = title, Width = width, Height = height, MinWidth = 420, WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Background = Brush.Parse("#141719"), Icon = new WindowIcon(Avalonia.Platform.AssetLoader.Open(new Uri("avares://DotsLauncher/Assets/DotsLauncher.png")))
    };
}
