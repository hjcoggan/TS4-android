using System.Globalization;
using System.Text;

namespace TopSpin4.Launcher.Core;

/// <summary>
/// The player's Settings file (<c>topspin4.settings.toml</c>), written with the game's own rules
/// (src/settings/config_file.h, which this mirrors): a top-level <c>version</c>, a line-preserving
/// single-key rewrite, a temporary file and a rename, and a file of a newer build left alone.
/// The migrations list is empty in both; when the game gains a migration, add it here too.
/// </summary>
public static class SettingsFile
{
    public const string FileName = "topspin4.settings.toml";

    /// <summary>The version this build writes: one more than its migrations (none yet).</summary>
    public const long CurrentVersion = 1;

    public static string PathIn(string userDataRoot) => Path.Combine(userDataRoot, FileName);

    // Each line split into its body and line end ("\n", "\r\n" or nothing at the end). fn returns false to stop.
    private static void ForEachLine(string text, Func<string, string, bool> fn)
    {
        var start = 0;
        while (start < text.Length)
        {
            var newline = text.IndexOf('\n', start);
            var end = newline < 0 ? text.Length : newline + 1;
            var current = text[start..end];
            var ending = current.EndsWith("\r\n", StringComparison.Ordinal) ? "\r\n"
                : current.EndsWith('\n') ? "\n" : "";
            if (!fn(current[..^ending.Length], ending)) return;
            start = end;
        }
    }

    // Whether the line is `key = ...` (blanks before the key allowed); rest is what follows the equals sign.
    private static bool IsKeyLine(string body, string key, out string rest)
    {
        rest = "";
        var first = body.AsSpan().IndexOfAnyExcept(" \t");
        if (first < 0 || !body.AsSpan(first).StartsWith(key, StringComparison.Ordinal)) return false;
        var after = body.AsSpan(first + key.Length);
        var equals = after.IndexOfAnyExcept(" \t");
        if (equals < 0 || after[equals] != '=') return false;
        rest = after[(equals + 1)..].ToString();
        return true;
    }

    /// <summary>The text with key set to the TOML literal: its line replaced (keeping the line end), or appended.</summary>
    public static string SetKey(string text, string key, string literal)
    {
        var line = key + " = " + literal;
        var output = new StringBuilder();
        var found = false;
        ForEachLine(text, (body, ending) =>
        {
            if (!found && IsKeyLine(body, key, out _))
            {
                output.Append(line).Append(ending);
                found = true;
            }
            else
            {
                output.Append(body).Append(ending);
            }
            return true;
        });
        if (!found)
        {
            if (output.Length > 0 && output[^1] != '\n') output.Append('\n');
            output.Append(line).Append('\n');
        }
        return output.ToString();
    }

    /// <summary>The file's top-level integer <c>version</c>; null when it has none (counts as 1) or it is not an integer.</summary>
    public static long? Version(string text)
    {
        long? version = null;
        ForEachLine(text, (body, _) =>
        {
            if (!IsKeyLine(body, "version", out var rest)) return true;
            rest = rest.TrimStart(' ', '\t');
            var digits = rest.TakeWhile(char.IsAsciiDigit).Count();
            if (digits == 0) return false;
            var after = rest[digits..].TrimStart(' ', '\t').TrimEnd('\r');
            if (after.Length == 0 || after[0] == '#')
                version = long.Parse(rest[..digits], CultureInfo.InvariantCulture);
            return false;
        });
        return version;
    }

    /// <summary>Whether a newer build wrote the file. Such a file is read as well as possible and never rewritten.</summary>
    public static bool IsNewerThanBuild(string text) => (Version(text) ?? 1) > CurrentVersion;

    /// <summary>The text with the version line set, or added first (a top-level key must come before any table).</summary>
    public static string WithVersion(string text, long version)
    {
        var hasLine = false;
        ForEachLine(text, (body, _) =>
        {
            hasLine = IsKeyLine(body, "version", out _);
            return !hasLine;
        });
        return hasLine
            ? SetKey(text, "version", version.ToString(CultureInfo.InvariantCulture))
            : "version = " + version.ToString(CultureInfo.InvariantCulture) + "\n" + text;
    }

    /// <summary>The text brought to this build's version (no migrations yet, so the version line is set).</summary>
    public static string Migrate(string text) =>
        (Version(text) ?? 1) > CurrentVersion ? text : WithVersion(text, CurrentVersion);

    /// <summary>The value written after <paramref name="key"/> with quotes and trailing comment removed, or null when the key is absent.</summary>
    public static string? ValueOf(string text, string key)
    {
        string? value = null;
        ForEachLine(text, (body, _) =>
        {
            if (!IsKeyLine(body, key, out var rest)) return true;
            value = Unquote(rest.Trim());
            return false;
        });
        return value;
    }

    private static string Unquote(string literal)
    {
        if (literal.Length > 0 && literal[0] == '\'')
        {
            var close = literal.IndexOf('\'', 1);
            return close < 0 ? literal[1..] : literal[1..close];
        }
        if (literal.Length > 0 && literal[0] == '"')
        {
            var value = new StringBuilder();
            for (var i = 1; i < literal.Length && literal[i] != '"'; i++)
            {
                if (literal[i] == '\\' && i + 1 < literal.Length) i++;
                value.Append(literal[i]);
            }
            return value.ToString();
        }
        var comment = literal.IndexOf('#');
        return (comment < 0 ? literal : literal[..comment]).Trim();
    }

    /// <summary>The TOML literal for a value: bare for a bool, a basic string otherwise.</summary>
    public static string Literal(SettingDescription setting, string value) =>
        setting.IsBool ? value : "\"" + value + "\"";

    /// <summary>
    /// Writes key = literal into the file at <paramref name="path"/> (an older or unversioned file is brought to this
    /// version first, a new one created at it) through a temporary file and a rename. Only that line changes otherwise.
    /// A file of a newer build is left alone: false.
    /// </summary>
    public static bool Save(string path, string key, string literal)
    {
        var text = File.Exists(path) ? File.ReadAllText(path) : "";
        if (IsNewerThanBuild(text)) return false;
        var result = SetKey(Migrate(text), key, literal);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        try
        {
            File.WriteAllBytes(temporary, new UTF8Encoding(false).GetBytes(result));
            File.Move(temporary, path, overwrite: true);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            try { File.Delete(temporary); } catch (IOException) { }
            return false;
        }
    }
}
