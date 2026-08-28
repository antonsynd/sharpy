using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// Ratchet on <see cref="Sharpy.Compiler.Semantic.ErrorRecoveryReason.DeliberatelyPermissive"/>
/// call sites across the Semantic/ directory. Each site represents a member access or expression
/// whose semantic type the compiler does not (yet) track — the type flows to C# as
/// <c>object</c> and any mismatch surfaces as SPY0908, not SPY0220.
///
/// <para>
/// The allowlist records the current per-file counts with an issue reference. Any INCREASE
/// (a new DP site) fails CI — the author must either resolve the type (preferred) or add a
/// justified allowlist entry with an issue reference. Any DECREASE (a fixed site) must drain
/// the corresponding allowlist count, or the stale entry fails CI.
/// </para>
/// </summary>
public class DeliberatelyPermissiveRatchetTests
{
    private readonly ITestOutputHelper _output;

    public DeliberatelyPermissiveRatchetTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void DeliberatelyPermissiveSiteCount_NeverIncreases()
    {
        var semanticDir = FindSemanticSourceDirectory();
        Assert.True(Directory.Exists(semanticDir),
            $"Semantic source directory should exist at {semanticDir}");

        var needle = "ErrorRecoveryReason.DeliberatelyPermissive(";
        var sitesByFile = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.GetFiles(semanticDir, "*.cs", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(file);
            var lines = File.ReadAllLines(file);
            var count = 0;
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var commentStart = line.IndexOf("//", StringComparison.Ordinal);
                var code = commentStart >= 0 ? line.Substring(0, commentStart) : line;
                if (code.Contains(needle, StringComparison.Ordinal))
                    count++;
            }

            if (count > 0)
                sitesByFile[fileName] = count;
        }

        var allowlist = LoadAllowlist();
        var violations = new List<string>();

        foreach (var (file, count) in sitesByFile)
        {
            if (!allowlist.TryGetValue(file, out var entry))
            {
                violations.Add($"NEW  {file}: {count} site(s) — not on the allowlist; resolve the type or add an entry with an issue reference");
                continue;
            }

            if (count > entry.Count)
                violations.Add($"GREW {file}: {count} site(s), allowlist says {entry.Count} — resolve the new site(s) or update the allowlist with an issue reference");
        }

        var stale = new List<string>();
        foreach (var (file, entry) in allowlist)
        {
            if (!sitesByFile.TryGetValue(file, out var actual))
            {
                stale.Add($"GONE {file}: allowlist says {entry.Count}, file has 0 — delete the entry (the sites were resolved)");
                continue;
            }

            if (actual < entry.Count)
                stale.Add($"DRAINED {file}: {actual} site(s) left, allowlist says {entry.Count} — update the count (sites were resolved)");
        }

        _output.WriteLine($"Files with DP sites: {sitesByFile.Count}  Allowlist entries: {allowlist.Count}");
        foreach (var (file, count) in sitesByFile.OrderByDescending(kv => kv.Value))
            _output.WriteLine($"  {file}: {count}");

        Assert.True(violations.Count == 0,
            "DeliberatelyPermissive ratchet: new or grown site counts are not allowed without " +
            "a justified allowlist update referencing an issue.\n" +
            string.Join("\n", violations.Select(v => "  " + v)));

        Assert.True(stale.Count == 0,
            "DeliberatelyPermissive ratchet: resolved sites must drain the allowlist count.\n" +
            string.Join("\n", stale.Select(s => "  " + s)));
    }

    private sealed record AllowlistEntry(int Count, string IssueRef);

    private static Dictionary<string, AllowlistEntry> LoadAllowlist()
    {
        var path = FindAllowlistPath();
        var result = new Dictionary<string, AllowlistEntry>(StringComparer.OrdinalIgnoreCase);
        if (path == null || !File.Exists(path))
            return result;

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                continue;

            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
                continue;

            var file = parts[0];
            if (!int.TryParse(parts[1], out var count))
                continue;
            var issueRef = parts[2];

            result[file] = new AllowlistEntry(count, issueRef);
        }

        return result;
    }

    private static string? FindAllowlistPath()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var candidate = Path.Combine(current, "src", "Sharpy.Compiler.Tests",
                "Conformance", "deliberately-permissive-allowlist.txt");
            if (File.Exists(candidate))
                return candidate;
            var dir = Path.Combine(current, "src", "Sharpy.Compiler.Tests", "Conformance");
            if (Directory.Exists(dir))
                return candidate;
            current = Directory.GetParent(current)?.FullName;
        }

        return null;
    }

    private static string FindSemanticSourceDirectory()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var candidate = Path.Combine(current, "src", "Sharpy.Compiler", "Semantic");
            if (Directory.Exists(candidate))
                return candidate;
            current = Directory.GetParent(current)?.FullName;
        }

        return Path.Combine(AppContext.BaseDirectory, "src", "Sharpy.Compiler", "Semantic");
    }
}
