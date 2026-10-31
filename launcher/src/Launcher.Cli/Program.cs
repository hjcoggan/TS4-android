using TopSpin4.Launcher.Core;

// topspin4-setup: prepares the player's disc image and TU1 beside the tool (or --cache), then the shader pack, and prints
// where the runtime finds them. The launcher UI (ticket 30) replaces it for players.
const string Usage =
    "Usage: topspin4-setup [--disc <image.iso>] [--tu <TU1 package>] [--cache <folder>] [--game-exe <topspin4.exe>] [--xenosrecomp <XenosRecomp.exe>]\n" +
    "Checks that the disc and Title Update 1 are the Reference build and prepares them once, then prepares the shader pack\n" +
    "(the shaders of the game's archives and Modules, converted with XenosRecomp); later runs reuse what is prepared and\n" +
    "need neither input. The game exe is the one beside this tool, XenosRecomp the one in the release's xenosrecomp folder.";

string? disc = null, tu = null, cache = null, gameExe = null, xenosRecomp = null;
for (var i = 0; i < args.Length; i++)
{
    var value = i + 1 < args.Length ? args[i + 1] : null;
    switch (args[i])
    {
        case "--disc" when value is not null: disc = value; i++; break;
        case "--tu" when value is not null: tu = value; i++; break;
        case "--cache" when value is not null: cache = value; i++; break;
        case "--game-exe" when value is not null: gameExe = value; i++; break;
        case "--xenosrecomp" when value is not null: xenosRecomp = value; i++; break;
        case "-h" or "--help":
            Console.WriteLine(Usage);
            return 0;
        default:
            Console.Error.WriteLine($"Unknown or incomplete option: {args[i]}\n{Usage}");
            return 64;
    }
}

using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, stop) =>
{
    stop.Cancel = true;
    cancel.Cancel();
};

// The command line wins, then the place the Launcher recorded for the game files, then the default folder.
// The default folders are beside this tool (ADR-0009), which sits beside the game exe.
var root = new GameFilesLocation(UserDirectories.UserDataRoot(AppContext.BaseDirectory))
    .Resolve(cache, UserDirectories.PreparedDataRoot(AppContext.BaseDirectory));
var preparation = new Preparation(root);
var lastLine = "";
void Say(string line)
{
    if (line == lastLine) return;
    lastLine = line;
    Console.WriteLine(line);
}

var progress = new SyncProgress(report => Say(report.Step switch
{
    SetupStep.Extracting or SetupStep.UnpackingTitleUpdate =>
        $"{Describe(report.Step)} {(int)(report.Fraction * 100),3}%" + (report.TimeLeft is { } left ? ", " + SetupProgress.TimeLeftText(left) : ""),
    _ => Describe(report.Step),
}));

PreparedData prepared;
try
{
    prepared = preparation.Prepare(disc, tu, progress, cancel.Token);
    Console.WriteLine(prepared.Reused ? $"Reused the prepared data in {root}" : $"Prepared the Reference build into {root}");
    Console.WriteLine($"  game data:   {prepared.GameDataRoot}");
    Console.WriteLine($"  update data: {prepared.UpdateDataRoot}");
}
catch (SetupException error)
{
    Console.Error.WriteLine($"{error.Error}: {error.Message}");
    // One exit code per outcome, so scripts can tell a wrong disc from a missing TU1.
    return 10 + (int)error.Error;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Stopped. The next run resumes where this one left off.");
    return 1;
}

// The shader step: the same one the Launcher window runs. A failure leaves the game files set up, and Play starts the game on
// Classic, so it has its own exit code and the runtime flags are printed all the same.
const int ShaderStepFailed = 40;
var exitCode = 0;
var exe = gameExe ?? LaunchArguments.FindGameExe(AppContext.BaseDirectory) ?? Path.Combine(AppContext.BaseDirectory, LaunchArguments.GameExeName);
var shaders = new ShaderPreparation(
    xenosRecomp is not null ? new XenosRecompConverter(xenosRecomp) : XenosRecompConverter.ShippedWith(AppContext.BaseDirectory),
    ShaderConverter.Id,
    new GameModuleShaders(exe, preparation, new StreamingPrepareRunner(TimeSpan.FromMinutes(10), "Reading the game's Module shaders")));
var status = shaders.StatusOf(preparation.ShaderCacheDirectory);
if (status.Pack.Ready)
{
    Console.WriteLine($"Reused the shader pack ({status.Pack.Detail})");
}
else
{
    try
    {
        var result = shaders.Run(prepared.GameDataRoot, preparation.ShaderCacheDirectory,
            new SyncProgress<ShaderProgress>(report => Say($"{DescribeShader(report.Step)} {(int)(report.Fraction * 100),3}%")), cancel.Token);
        Console.WriteLine($"Prepared the shader pack in {preparation.ShaderCacheDirectory}: {result.Converted} shaders");
        if (result.Skipped.Count > 0)
            Console.WriteLine($"  XenosRecomp cannot convert {result.Skipped.Count} shaders, left out: {string.Join(", ", result.Skipped)}");
    }
    catch (ShaderPreparationException error)
    {
        Console.Error.WriteLine(error.Message);
        if (error.InnerException is { } inner) Console.Error.WriteLine(inner.Message);
        exitCode = ShaderStepFailed;
    }
    catch (OperationCanceledException)
    {
        Console.Error.WriteLine("Stopped. The next run resumes where this one left off.");
        return 1;
    }
}
Console.WriteLine($"Runtime flags: --game_data_root=\"{prepared.GameDataRoot}\" --update_data_root=\"{prepared.UpdateDataRoot}\"");
return exitCode;

static string Describe(SetupStep step) => step switch
{
    SetupStep.CheckingDisc => "Checking the disc image",
    SetupStep.CheckingTitleUpdate => "Checking Title Update 1",
    SetupStep.CheckingExistingFiles => "Checking the files of an earlier, unfinished preparation",
    SetupStep.Extracting => "Extracting the disc",
    SetupStep.UnpackingTitleUpdate => "Unpacking Title Update 1",
    _ => "Done",
};

static string DescribeShader(ShaderStep step) => step switch
{
    ShaderStep.ReadingGameFiles => "Preparing shaders: reading the archives",
    ShaderStep.ReadingModules => "Preparing shaders: reading the Modules",
    ShaderStep.Converting => "Preparing shaders: converting",
    ShaderStep.Screening => "Preparing shaders: finding the ones XenosRecomp cannot convert",
    ShaderStep.WritingPack => "Preparing shaders: writing the pack",
    _ => "Preparing shaders: done",
};

internal sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}
