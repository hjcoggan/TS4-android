// Dumps every entry of a game .arc that starts with a given magic (default: Lua bytecode).
// Usage: arcdump <out dir> <archive>... [--magic hex]
using TopSpin4.Launcher.Core;
var outDir = args[0];
var magic = new byte[] { 0x1b, (byte)'L', (byte)'u', (byte)'a' };
var files = args.Skip(1).ToList();
var m = files.IndexOf("--magic");
if (m >= 0) { magic = Convert.FromHexString(files[m + 1]); files.RemoveRange(m, 2); }
foreach (var path in files)
{
    using var stream = File.OpenRead(path);
    var archive = GameArchive.Open(stream);
    var dir = Path.Combine(outDir, Path.GetFileNameWithoutExtension(path));
    Directory.CreateDirectory(dir);
    int found = 0, skipped = 0;
    for (var i = 0; i < archive.Entries.Count; i++)
    {
        byte[]? data;
        try { data = GameArchive.Read(stream, archive.Entries[i], s => s.StartsWith(magic), 64 << 20); }
        catch (Exception) { skipped++; continue; }
        if (data == null) continue;
        File.WriteAllBytes(Path.Combine(dir, $"{i:D5}.luac"), data);
        found++;
    }
    Console.WriteLine($"{path}: {found} entries ({skipped} unreadable)");
}
