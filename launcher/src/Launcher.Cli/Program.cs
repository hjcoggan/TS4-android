using TopSpin4.Launcher.Core;

// topspin4-setup: prepares the player's disc image and TU1 into the per-user cache and prints
// where the runtime finds them. The launcher UI (ticket 30) replaces it for players.
const string Usage =
    "Usage: topspin4-setup [--disc <image.iso>] [--tu <TU1 package>] [--cache <folder>]\n" +
    "Checks that the disc and Title Update 1 are the Reference build and prepares them once;\n" +
    "later runs reuse the prepared data and need neither input.";

string? disc = null, tu = null, cache = null;
for (var i = 0; i < args.Length; i++)
{
    var value = i + 1 < args.Length ? args[i + 1] : null;
    switch (args[i])
    {
        case "--disc" when value is not null: disc = value; i++; break;
        case "--tu" when value is not null: tu = value; i++; break;
        case "--cache" when value is not null: cache = value; i++; break;
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

var root = Path.GetFullPath(cache ?? UserDirectories.PreparedDataRoot());
var preparation = new Preparation(root);
var lastLine = "";
var progress = new Progress(report =>
{
    var line = report.Step switch
    {
        SetupStep.Extracting or SetupStep.UnpackingTitleUpdate =>
            $"{Describe(report.Step)} {(int)(report.Fraction * 100),3}%" + (report.TimeLeft is { } left ? $", {Minutes(left)} left" : ""),
        _ => Describe(report.Step),
    };
    if (line == lastLine) return;
    lastLine = line;
    Console.WriteLine(line);
});

try
{
    var prepared = preparation.Prepare(disc, tu, progress, cancel.Token);
    Console.WriteLine(prepared.Reused ? $"Reused the prepared data in {root}" : $"Prepared the Reference build into {root}");
    Console.WriteLine($"  game data:   {prepared.GameDataRoot}");
    Console.WriteLine($"  update data: {prepared.UpdateDataRoot}");
    Console.WriteLine($"Runtime flags: --game_data_root=\"{prepared.GameDataRoot}\" --update_data_root=\"{prepared.UpdateDataRoot}\"");
    return 0;
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

static string Describe(SetupStep step) => step switch
{
    SetupStep.CheckingDisc => "Checking the disc image",
    SetupStep.CheckingTitleUpdate => "Checking Title Update 1",
    SetupStep.CheckingExistingFiles => "Checking the files of an earlier, unfinished preparation",
    SetupStep.Extracting => "Extracting the disc",
    SetupStep.UnpackingTitleUpdate => "Unpacking Title Update 1",
    _ => "Done",
};

static string Minutes(TimeSpan left) => left.TotalMinutes >= 1 ? $"{(int)Math.Ceiling(left.TotalMinutes)} min" : "under a minute";

/// <summary>Reports on the reporting thread, unlike Progress&lt;T&gt;, which posts to the thread pool.</summary>
sealed class Progress(Action<SetupProgress> report) : IProgress<SetupProgress>
{
    public void Report(SetupProgress value) => report(value);
}
