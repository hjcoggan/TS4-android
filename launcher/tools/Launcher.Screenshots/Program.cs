// Renders the Launcher's pages to PNG with Avalonia's headless platform: no window opens and the
// game is never played (Settings reads the game's settings export, a short run of the exe that
// shows nothing). Copied from WWE All Stars' Launcher.Screenshots (ADR-0004).
//
//   dotnet run --project tools/Launcher.Screenshots -- <output folder> <game exe> <cache folder> <user data folder> [<empty folder>]
//
// The home, Settings (Graphics and Profile tabs) and Achievements pages are drawn from a real,
// set-up install (the developer's own: its disc art, settings and unlocks), so the output holds
// game art: keep it in an ignored folder and copy picked shots to docs/images (ADR-0012). The
// setup page is drawn from an empty folder (a temporary one unless given; the footer shows its
// path). Each page at 100 % and 200 % scaling.
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TopSpin4.Launcher;

if (args.Length is < 4 or > 5)
{
    Console.Error.WriteLine("usage: Launcher.Screenshots <output folder> <game exe> <cache folder> <user data folder> [<empty folder>]");
    return 2;
}
var output = Path.GetFullPath(args[0]);
var gameExe = Path.GetFullPath(args[1]);
var cache = Path.GetFullPath(args[2]);
var userData = Path.GetFullPath(args[3]);
Directory.CreateDirectory(output);

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

var installed = new LauncherOptions(CacheRoot: cache, GameExe: gameExe, UserDataRoot: userData);
Capture(installed, "home", _ => { });
Capture(installed, "settings", window => OpenSettings(window, tab: 0));
Capture(installed, "settings-profile", window => OpenSettings(window, tab: -1));
Capture(installed, "achievements", window => ClickNamed(window, "Achievements"));

var scratch = args.Length > 4 ? Path.GetFullPath(args[4]) : Directory.CreateTempSubdirectory("ts4-launcher-screens-").FullName;
if (Directory.Exists(scratch) && Directory.EnumerateFileSystemEntries(scratch).Any())
{
    Console.Error.WriteLine($"not empty: {scratch}");
    return 2;
}
try
{
    Capture(new LauncherOptions(CacheRoot: scratch, GameExe: gameExe,
        UserDataRoot: Path.Combine(scratch, "userdata")), "setup", _ => { });
}
finally
{
    Directory.Delete(scratch, recursive: true);
}
return 0;

void Capture(LauncherOptions options, string name, Action<MainWindow> prepare)
{
    App.Options = options;
    var window = new MainWindow();
    window.Show();
    // The art is read on a pool thread when the window opens, and the home page fades in; let both settle.
    Pump(TimeSpan.FromSeconds(2), () => false);
    prepare(window);
    Pump(TimeSpan.FromSeconds(3), () => false);
    // A render target draws the focus outline at the wrong place; screenshots show none.
    window.FocusManager?.ClearFocus();
    Dispatcher.UIThread.RunJobs();
    foreach (var scale in new[] { 1, 2 })
    {
        var size = new PixelSize((int)(window.Width * scale), (int)(window.Height * scale));
        using var bitmap = new RenderTargetBitmap(size, new Vector(96 * scale, 96 * scale));
        bitmap.Render(window);
        var path = Path.Combine(output, $"{name}-{scale * 100}.png");
        bitmap.Save(path);
        Console.WriteLine(path);
    }
    window.Close();
    Dispatcher.UIThread.RunJobs();
}

// Runs the UI thread's jobs until done() or the time is up.
static void Pump(TimeSpan time, Func<bool> done)
{
    var until = DateTime.UtcNow + time;
    while (DateTime.UtcNow < until && !done())
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(20);
    }
    Dispatcher.UIThread.RunJobs();
}

// Settings reads the game's export first; wait for its tabs, then pick one (-1: the last).
static void OpenSettings(Window window, int tab)
{
    ClickNamed(window, "Settings");
    TabControl? Tabs() => window.GetVisualDescendants().OfType<TabControl>().FirstOrDefault(t => t.IsVisible && t.ItemCount > 0);
    Pump(TimeSpan.FromSeconds(60), () => Tabs() is not null);
    if (Tabs() is not { } tabs) throw new InvalidOperationException("the Settings page did not load the game's settings");
    tabs.SelectedIndex = tab < 0 ? tabs.ItemCount - 1 : tab;
    Pump(TimeSpan.FromMilliseconds(300), () => false);
}

static void ClickNamed(Window window, string name)
{
    window.GetVisualDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b) == name)
        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Dispatcher.UIThread.RunJobs();
}
