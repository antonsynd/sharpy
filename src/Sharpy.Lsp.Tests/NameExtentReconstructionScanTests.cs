using FluentAssertions;
using Xunit;
using SCG = System.Collections.Generic;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Standing scan guard: no LSP consumer may reconstruct a name extent from
/// <c>.Name.Length</c> — every site must read the parser's recorded <c>NameColumnEnd</c> or
/// route through <see cref="Handlers.SymbolExtents"/> (#1454, plan-80eee2 Design Decision 7).
/// </summary>
/// <remarks>
/// Each allowlist entry documents a verified non-extent use. If the production code changes,
/// a stale entry fails the staleness check and must be removed. If a new <c>.Name.Length</c>
/// appears, it fails the scan and must either be converted or added with a rationale.
/// </remarks>
public class NameExtentReconstructionScanTests
{
    private const string ScanTarget = ".Name.Length";

    /// <summary>
    /// Verified non-extent uses of <c>.Name.Length</c>. Each entry is
    /// <c>(fileName, rationaleSubstring)</c> — the substring must appear on the same line as
    /// <c>.Name.Length</c> for the allowlist match to hold.
    /// </summary>
    private static readonly (string FileName, string RationaleSubstring)[] Allowlist =
    {
        // HoverService.cs: emptiness test — `decorator.Name.Length == 0`
        ("HoverService.cs", "== 0"),
        // SymbolExtents.cs: the ONE fallback for CLR imports with no parsed node
        ("SymbolExtents.cs", "symbol.Name.Length"),
    };

    [Fact]
    public void No_Name_Length_extent_reconstruction_in_LSP_consumers()
    {
        var serverDirectory = FindServerSourceDirectory();

        var offenders = new SCG.List<string>();
        var allowlistHits = new bool[Allowlist.Length];

        foreach (var file in Directory.EnumerateFiles(serverDirectory, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var fileName = Path.GetFileName(file);
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!line.Contains(ScanTarget, StringComparison.Ordinal))
                    continue;

                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal)
                    || trimmed.StartsWith("///", StringComparison.Ordinal)
                    || trimmed.StartsWith("*", StringComparison.Ordinal))
                {
                    continue;
                }

                var allowed = false;
                for (var a = 0; a < Allowlist.Length; a++)
                {
                    if (string.Equals(fileName, Allowlist[a].FileName, StringComparison.Ordinal)
                        && line.Contains(Allowlist[a].RationaleSubstring, StringComparison.Ordinal))
                    {
                        allowlistHits[a] = true;
                        allowed = true;
                        break;
                    }
                }

                if (!allowed)
                    offenders.Add($"{fileName}:{i + 1}: {trimmed}");
            }
        }

        offenders.Should().BeEmpty(
            "every name extent must come from a recorded NameColumnEnd, EffectiveNameColumnEnd, or "
            + "SymbolExtents — never reconstructed from Name.Length (#1454).\n"
            + "Offending sites:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void Allowlist_entries_are_not_stale()
    {
        var serverDirectory = FindServerSourceDirectory();

        for (var a = 0; a < Allowlist.Length; a++)
        {
            var (fileName, rationale) = Allowlist[a];
            var found = false;

            foreach (var file in Directory.EnumerateFiles(serverDirectory, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.Equals(Path.GetFileName(file), fileName, StringComparison.Ordinal))
                    continue;

                var lines = File.ReadAllLines(file);
                foreach (var line in lines)
                {
                    if (line.Contains(ScanTarget, StringComparison.Ordinal)
                        && line.Contains(rationale, StringComparison.Ordinal))
                    {
                        var trimmed = line.TrimStart();
                        if (!trimmed.StartsWith("//", StringComparison.Ordinal)
                            && !trimmed.StartsWith("///", StringComparison.Ordinal)
                            && !trimmed.StartsWith("*", StringComparison.Ordinal))
                        {
                            found = true;
                            break;
                        }
                    }
                }
            }

            found.Should().BeTrue(
                $"allowlist entry ({fileName}, \"{rationale}\") must match at least one non-comment "
                + $"call site — remove it if the code was converted (#1454)");
        }
    }

    private static string FindServerSourceDirectory()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "src", "Sharpy.Lsp");
            if (Directory.Exists(candidate))
                return candidate;

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException(
            "Could not locate src/Sharpy.Lsp. This scan is only meaningful against real sources: a "
            + "walk of the wrong directory passes vacuously.");
    }
}
