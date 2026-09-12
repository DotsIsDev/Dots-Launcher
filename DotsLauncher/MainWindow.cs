using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DotsLauncher.Core;

namespace DotsLauncher;

public sealed class MainWindow : Window
{
    static readonly IBrush Bg=B("#141719"), Panel=B("#1C2023"), Border=B("#353B40"), Text=B("#F1F3F4"), Muted=B("#9DA7AE"), Accent=B("#A8D5C0");
    readonly LibraryStore store; readonly LibraryData data; readonly List<FileSystemWatcher> watchers=[]; readonly HashSet<string> selected=[];
    readonly StackPanel host=new(){Spacing=18}; readonly TextBox search=new(){PlaceholderText="Search your library",Width=260}; readonly TextBlock folders=new(){Foreground=Muted}; readonly TextBlock status=new(){Foreground=Muted,FontSize=12};
    readonly Border selectionBar=new(){IsVisible=false,Background=Panel,BorderBrush=Border,BorderThickness=new(1),Padding=new(12)}; readonly TextBlock selectionCount=new(){VerticalAlignment=VerticalAlignment.Center,FontWeight=FontWeight.SemiBold};
    readonly DispatcherTimer debounce=new(){Interval=TimeSpan.FromMilliseconds(900)}; Button grid=null!,list=null!,refresh=null!,removeSelected=null!; bool selecting,scanning,closed;

    public MainWindow(LibraryStore store)
    {
        this.store=store; data=store.Load(); Title="Dots Launcher"; Width=1140; Height=840; MinWidth=800; MinHeight=560; Background=Bg; WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Icon=new WindowIcon(Avalonia.Platform.AssetLoader.Open(new Uri("avares://DotsLauncher/Assets/DotsLauncher.png"))); Content=Build(); search.TextChanged+=(_,_)=>RefreshView();
        debounce.Tick+=async(_,_)=>{debounce.Stop();await Scan();}; Opened+=async(_,_)=>{RefreshView();Watch();await Scan();if(store.RecoveryMessage is not null)await UiDialogs.ShowMessage(this,store.RecoveryMessage);};
        Closed+=(_,_)=>{closed=true;debounce.Stop();foreach(var w in watchers)w.Dispose();IconService.Invalidate();}; KeyDown+=async(_,e)=>{if(e.Key==Key.F5){e.Handled=true;await Scan();}else if(e.Key==Key.F&&e.KeyModifiers.HasFlag(KeyModifiers.Control)){search.Focus();search.SelectAll();}else if(e.Key==Key.Escape&&selecting)ClearSelection();};
    }

    Control Build()
    {
        var add=Btn("Add executable");add.Click+=AddExecutable;var addFolder=Primary("Add folder");addFolder.Click+=AddFolder;
        var logo=new StackPanel{Orientation=Orientation.Horizontal,Spacing=13,VerticalAlignment=VerticalAlignment.Center,Children={new Image{Source=IconService.Get(""),Width=40,Height=40},new TextBlock{Text="DOTS",FontSize=23,FontWeight=FontWeight.Bold,VerticalAlignment=VerticalAlignment.Center},new TextBlock{Text="LAUNCHER",FontSize=11,Foreground=Muted,VerticalAlignment=VerticalAlignment.Center}}};
        var topActions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10,Children={add,addFolder}};var top=new Grid{ColumnDefinitions=new("*,Auto"),Margin=new(32,22)};top.Children.Add(logo);top.Children.Add(topActions);Grid.SetColumn(topActions,1);
        grid=Btn("Grid");list=Btn("List");grid.Click+=(_,_)=>SetView("Grid");list.Click+=(_,_)=>SetView("List");var views=new StackPanel{Orientation=Orientation.Horizontal,Spacing=6,Children={grid,list,search}};
        var intro=new Grid{ColumnDefinitions=new("*,Auto")};intro.Children.Add(new StackPanel{Spacing=5,Children={new TextBlock{Text="Your collection",FontSize=25,FontWeight=FontWeight.SemiBold},new TextBlock{Text="Everything you run. One place to find it.",Foreground=Muted}}});intro.Children.Add(views);Grid.SetColumn(views,1);
        var manage=Btn("Manage folders");manage.Background=Brushes.Transparent;manage.BorderThickness=new(0);manage.Click+=ManageFolders;refresh=Btn("Refresh");refresh.Click+=async(_,_)=>{Watch();await Scan();};var fa=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8,Children={manage,refresh}};var fg=new Grid{ColumnDefinitions=new("*,Auto")};fg.Children.Add(folders);fg.Children.Add(fa);Grid.SetColumn(fa,1);var folderBar=new Border{Background=Panel,BorderBrush=Border,BorderThickness=new(1),Padding=new(14,9),Child=fg};
        var all=Btn("Select all shown");all.Click+=(_,_)=>{selecting=true;foreach(var p in Filtered())selected.Add(p.Id);RefreshView();};var cancel=Btn("Cancel");cancel.Click+=(_,_)=>ClearSelection();removeSelected=Btn("Remove from Launcher");removeSelected.Foreground=B("#FFB3AA");removeSelected.Click+=RemoveMany;var sa=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8,Children={all,cancel,removeSelected}};var sg=new Grid{ColumnDefinitions=new("*,Auto")};sg.Children.Add(selectionCount);sg.Children.Add(sa);Grid.SetColumn(sa,1);selectionBar.Child=sg;
        var body=new Grid{RowDefinitions=new("Auto,Auto,Auto,*"),RowSpacing=16,Margin=new(32,26,32,0),Children={intro,folderBar,selectionBar,new ScrollViewer{Content=host}}};Grid.SetRow(folderBar,1);Grid.SetRow(selectionBar,2);Grid.SetRow(body.Children[3],3);
        var root=new Grid{RowDefinitions=new("Auto,*,Auto")};root.Children.Add(new Border{BorderBrush=Border,BorderThickness=new(0,0,0,1),Child=top});root.Children.Add(body);Grid.SetRow(body,1);var foot=new Border{BorderBrush=Border,BorderThickness=new(0,1,0,0),Padding=new(32,10),Child=status};root.Children.Add(foot);Grid.SetRow(foot,2);return root;
    }

    void RefreshView()
    {
        selected.RemoveWhere(id=>!data.Profiles.Any(p=>p.Id==id));var profiles=Filtered().OrderBy(p=>p.Title,StringComparer.CurrentCultureIgnoreCase).ToList();host.Children.Clear();var fav=profiles.Where(p=>p.IsFavourite).ToList();if(data.Profiles.Any(p=>p.IsFavourite))Section("FAVOURITES",fav);Section("LIBRARY",profiles);if(profiles.Count==0)host.Children.Add(Empty());
        folders.Text=data.Folders.Count switch{0=>"No folders connected",1=>"1 connected folder  /  "+data.Folders[0].Path,_=>$"{data.Folders.Count} connected folders  /  Watching for new executables"};selectionBar.IsVisible=selecting;selectionCount.Text=selected.Count switch{0=>"Select items",1=>"1 item selected",_=>$"{selected.Count} items selected"};removeSelected.IsEnabled=selected.Count>0;Active(grid,data.ViewMode=="Grid");Active(list,data.ViewMode=="List");
    }
    void Section(string name,List<ExecutableProfile> profiles){host.Children.Add(new StackPanel{Orientation=Orientation.Horizontal,Spacing=12,Children={new TextBlock{Text=name,FontSize=12,FontWeight=FontWeight.SemiBold},new TextBlock{Text=profiles.Count.ToString("D2"),FontSize=12,Foreground=Muted}}});Panel panel=data.ViewMode=="Grid"?new WrapPanel():new StackPanel{Spacing=10};foreach(var p in profiles)panel.Children.Add(data.ViewMode=="Grid"?Card(p):Row(p));host.Children.Add(panel);}

    Control Card(ExecutableProfile p)
    {
        var surface=Surface(p,236,174,new(0,0,16,16));var gridbox=new Grid();var center=new StackPanel{Spacing=12,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Children={new Image{Source=IconService.Get(p.Path,p.IconPath),Width=44,Height=44,HorizontalAlignment=HorizontalAlignment.Center},new TextBlock{Text=p.Title,FontSize=16,FontWeight=FontWeight.SemiBold,TextAlignment=TextAlignment.Center,MaxWidth=195,TextWrapping=TextWrapping.Wrap},new TextBlock{Text=Available(p)?Path.GetFileName(p.Path):"FILE UNAVAILABLE",FontSize=11,Foreground=Muted,TextAlignment=TextAlignment.Center,MaxWidth=195,TextTrimming=TextTrimming.CharacterEllipsis}}};gridbox.Children.Add(LaunchButton(center,p));gridbox.Children.Add(MenuButton(p));var check=Check(p);gridbox.Children.Add(check);Hover(surface,check);surface.Child=gridbox;return surface;
    }
    Control Row(ExecutableProfile p)
    {
        var surface=Surface(p,double.NaN,76,new(0,0,0,10));var gridbox=new Grid();var row=new Grid{ColumnDefinitions=new("72,46,*,56"),Margin=new(10)};var image=new Image{Source=IconService.Get(p.Path,p.IconPath),Width=40,Height=40};var words=new StackPanel{Spacing=3,VerticalAlignment=VerticalAlignment.Center,Children={new TextBlock{Text=p.Title,FontWeight=FontWeight.SemiBold},new TextBlock{Text=p.Path,FontSize=11,Foreground=Muted,TextTrimming=TextTrimming.CharacterEllipsis}}};row.Children.Add(image);Grid.SetColumn(image,1);row.Children.Add(words);Grid.SetColumn(words,2);gridbox.Children.Add(LaunchButton(row,p));var menu=MenuButton(p);menu.VerticalAlignment=VerticalAlignment.Center;gridbox.Children.Add(menu);var check=Check(p);gridbox.Children.Add(check);Hover(surface,check);surface.Child=gridbox;return surface;
    }
    Border Surface(ExecutableProfile p,double width,double height,Thickness margin)=>new(){Width=width,Height=height,Margin=margin,Background=selected.Contains(p.Id)?B("#26332E"):Panel,BorderBrush=selected.Contains(p.Id)?Accent:Border,BorderThickness=new(1)};
    Button LaunchButton(Control content,ExecutableProfile p){var b=Btn(content);b.Tag=p;b.Background=Brushes.Transparent;b.BorderThickness=new(0);b.HorizontalContentAlignment=HorizontalAlignment.Stretch;b.VerticalContentAlignment=VerticalAlignment.Stretch;ToolTip.SetTip(b,p.Path);b.Click+=(_,_)=>{if(selecting)Toggle(p);else Launch(p);};return b;}
    Button MenuButton(ExecutableProfile p){var b=Btn("⋯");b.FontSize=23;b.Width=40;b.Height=36;b.Padding=new(0);b.HorizontalAlignment=HorizontalAlignment.Right;b.VerticalAlignment=VerticalAlignment.Top;b.Margin=new(0,9,9,0);b.Background=Brushes.Transparent;b.BorderThickness=new(0);b.Click+=(_,_)=>Menu(b,p);return b;}
    CheckBox Check(ExecutableProfile p){var c=new CheckBox{Content="Select",IsChecked=selected.Contains(p.Id),IsVisible=selecting,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,Margin=new(11),Background=B("#242A2E")};c.IsCheckedChanged+=(_,_)=>{selecting=true;if(c.IsChecked==true)selected.Add(p.Id);else selected.Remove(p.Id);RefreshView();};return c;}
    void Hover(Border b,CheckBox c){b.PointerEntered+=(_,_)=>c.IsVisible=true;b.PointerExited+=(_,_)=>{if(!selecting)c.IsVisible=false;};}

    void Menu(Control target,ExecutableProfile p)
    {
        var m=new ContextMenu();m.Items.Add(MI("Launch Executable",()=>Launch(p)));m.Items.Add(MI(p.IsFavourite?"Remove From Favourites":"Add To Favourites",()=>{p.IsFavourite=!p.IsFavourite;Save();RefreshView();}));m.Items.Add(MI("Browse Local File",()=>Browse(p)));m.Items.Add(MI("Edit Profile",async()=>{if(await UiDialogs.EditProfile(this,p,store.DirectoryPath)){Save();IconService.Invalidate();RefreshView();}}));var remove=MI("Remove from Launcher",()=>{LibraryOperations.RemoveExecutable(data,p);Save();RefreshView();});remove.BorderBrush=B("#6A747B");remove.BorderThickness=new(0,1,0,0);m.Items.Add(remove);m.Open(target);
    }
    static MenuItem MI(string text,Action action){var i=new MenuItem{Header=text};i.Click+=(_,_)=>action();return i;}

    async void AddExecutable(object? s,Avalonia.Interactivity.RoutedEventArgs e){var files=await StorageProvider.OpenFilePickerAsync(new(){Title="Add executables",AllowMultiple=true,FileTypeFilter=OperatingSystem.IsWindows()?[new FilePickerFileType("Windows executables"){Patterns=["*.exe"]}]:[FilePickerFileTypes.All]});int n=0;foreach(var f in files){var p=f.TryGetLocalPath();if(p is not null&&LibraryOperations.AddExecutable(data,p,true)is not null)n++;}Save();search.Clear();RefreshView();status.Text=$"Added {n} executable{(n==1?"":"s")}.";}
    async void AddFolder(object? s,Avalonia.Interactivity.RoutedEventArgs e){var result=await StorageProvider.OpenFolderPickerAsync(new(){Title="Add a folder",AllowMultiple=false});if(result.Count==0)return;var p=result[0].TryGetLocalPath();if(p is null)return;var old=data.Folders.FirstOrDefault(f=>LibraryOperations.SamePath(f.Path,p));if(old is null)data.Folders.Add(new(){Path=p,IncludeSubfolders=true});else old.IncludeSubfolders=true;Save();RefreshView();Watch();await Scan();}
    async void ManageFolders(object? s,Avalonia.Interactivity.RoutedEventArgs e){var result=await UiDialogs.ManageFolders(this,data.Folders);if(result is null)return;data.Folders=result;Save();RefreshView();Watch();await Scan();}
    IEnumerable<ExecutableProfile> Filtered(){var q=search.Text?.Trim()??"";return data.Profiles.Where(p=>p.Title.Contains(q,StringComparison.OrdinalIgnoreCase)||Path.GetFileName(p.Path).Contains(q,StringComparison.OrdinalIgnoreCase));}
    async Task Scan(){if(closed||scanning)return;scanning=true;refresh.IsEnabled=false;status.Text="Scanning connected folders...";try{var result=await Task.Run(()=>FolderScanner.Scan(data.Folders));int n=0;foreach(var p in result.Executables)if(data.Folders.Any(f=>Covers(f,p))&&LibraryOperations.AddExecutable(data,p)is not null)n++;if(n>0)Save();IconService.Invalidate();RefreshView();status.Text=$"{data.Profiles.Count} executable{(data.Profiles.Count==1?"":"s")} in your library. Up to date.";}catch(Exception ex){status.Text="Scan failed. "+ex.Message;}finally{scanning=false;if(!closed)refresh.IsEnabled=true;}}
    static bool Covers(SourceFolder f,string p){var root=f.Path.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);var comparison=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;return p.StartsWith(root+Path.DirectorySeparatorChar,comparison)||LibraryOperations.SamePath(Path.GetDirectoryName(p)??"",root);}
    void Watch(){foreach(var w in watchers)w.Dispose();watchers.Clear();foreach(var f in data.Folders.Where(f=>Directory.Exists(f.Path)))try{var w=new FileSystemWatcher(f.Path){IncludeSubdirectories=f.IncludeSubfolders,EnableRaisingEvents=true};w.Created+=Changed;w.Deleted+=Changed;w.Renamed+=Changed;watchers.Add(w);}catch{}}
    void Changed(object s,FileSystemEventArgs e)=>Dispatcher.UIThread.Post(()=>{debounce.Stop();debounce.Start();});
    void Launch(ExecutableProfile p){if(!Available(p)){_=UiDialogs.ShowMessage(this,"This executable is unavailable:\n\n"+p.Path);return;}try{ProcessStartInfo i=OperatingSystem.IsMacOS()&&Directory.Exists(p.Path)?new("open",$"-a \"{p.Path}\" --args {p.Arguments}"){UseShellExecute=false}:new(){FileName=p.Path,Arguments=p.Arguments??"",WorkingDirectory=Path.GetDirectoryName(p.Path)??Environment.CurrentDirectory,UseShellExecute=true};Process.Start(i)?.Dispose();status.Text="Launched "+p.Title+".";}catch(Exception ex){_=UiDialogs.ShowMessage(this,ex.Message);}}
    void Browse(ExecutableProfile p){try{ProcessStartInfo i=OperatingSystem.IsWindows()?new("explorer.exe","/select,\""+p.Path+"\""):OperatingSystem.IsMacOS()?new("open","-R \""+p.Path+"\""):new("xdg-open","\""+(Path.GetDirectoryName(p.Path)??p.Path)+"\"");i.UseShellExecute=true;Process.Start(i)?.Dispose();}catch(Exception ex){_=UiDialogs.ShowMessage(this,ex.Message);}}
    void Toggle(ExecutableProfile p){if(!selected.Add(p.Id))selected.Remove(p.Id);RefreshView();}void ClearSelection(){selected.Clear();selecting=false;RefreshView();}void RemoveMany(object? s,Avalonia.Interactivity.RoutedEventArgs e){var ps=data.Profiles.Where(p=>selected.Contains(p.Id)).ToList();foreach(var p in ps)LibraryOperations.RemoveExecutable(data,p);selected.Clear();selecting=false;Save();RefreshView();status.Text=$"Removed {ps.Count} item{(ps.Count==1?"":"s")}.";}void SetView(string v){data.ViewMode=v;Save();RefreshView();}
    bool Save(){try{store.Save(data);return true;}catch(Exception ex){_=UiDialogs.ShowMessage(this,"Could not save.\n\n"+ex.Message);return false;}}Control Empty(){var add=Primary("Add your first folder");add.Click+=AddFolder;return new Border{BorderBrush=Border,BorderThickness=new(1),Padding=new(36,48),Child=new StackPanel{Spacing=14,HorizontalAlignment=HorizontalAlignment.Center,Children={new TextBlock{Text="A home for your executables",FontSize=23,FontWeight=FontWeight.SemiBold},new TextBlock{Text="Connect a folder or add an executable to start your library.",Foreground=Muted},add}}};}
    static bool Available(ExecutableProfile p)=>File.Exists(p.Path)||Directory.Exists(p.Path);static void Active(Button b,bool on){b.Background=on?Accent:Panel;b.Foreground=on?B("#101B16"):Text;}static Button Btn(object content)=>new(){Content=content};static Button Primary(string s)=>new(){Content=s,Background=Accent,Foreground=B("#101B16"),BorderBrush=Accent,FontWeight=FontWeight.SemiBold};static IBrush B(string c)=>Brush.Parse(c);
}
