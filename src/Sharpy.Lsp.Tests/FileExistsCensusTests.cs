using FluentAssertions;
using Xunit;
using SCG = System.Collections.Generic;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Standing guard: every <c>File.Exists(</c> call site in <c>src/Sharpy.Lsp/</c> must appear in
/// the rationale'd allowlist. A silent capability drop (the #1541 class) is an un-allowlisted
/// site that tests <c>File.Exists</c> and degrades functionality when the file is absent without
/// any signal to the operator.
/// </summary>
/// <remarks>
/// Modeled on <see cref="AnalysisLatencyLogCallSiteTests"/>. The allowlist entries are the
/// complete census at planning (2026-08-21 at HEAD 9455e7799); a new site must either surface its
/// absence loudly (and join the allowlist with a rationale) or be flagged as a silent drop.
/// </remarks>
public class FileExistsCensusTests
{
    private const string SearchPattern = "File.Exists(";

    /// <summary>
    /// Every allowlisted site with its rationale. The key is "filename:line" (1-indexed) where
    /// the line is approximate — the scan matches by file name only and checks that the line
    /// content contains the pattern, so a small drift is tolerated.
    /// </summary>
    /// <remarks>
    /// The allowlist uses file-name-only matching (not full path) because production source paths
    /// vary by build environment. The <c>rationale</c> value is documentation, not tested text.
    /// </remarks>
    private static readonly SCG.Dictionary<string, string> AllowedFiles = new(StringComparer.Ordinal)
    {
        // Surfaced by design — this is the Phase 1 fix. Warns loudly to stderr and logger.
        ["DefaultReferenceSet.cs"] = "surfaced by design (#1541) — warns loudly to stderr/logger",

        // Pure routing: the File.Exists here decides which document-fetch path runs (in-memory
        // overlay vs disk read). Neither path drops a capability — the file is either open in the
        // editor or on disk, and the answer is always served.
        ["LanguageService.cs"] = "routing (open-in-editor vs disk); no capability lost",

        // Links only for existing navigable files. A dead link is worse than no link — the handler
        // deliberately omits links to files it cannot resolve.
        ["DocumentLinkHandler.cs"] = "documented design — links only for existing navigable files",
    };

    [Fact]
    public void Every_FileExists_call_is_in_the_rationale_allowlist()
    {
        var serverDirectory = FindServerSourceDirectory();

        var offenders = new SCG.List<string>();
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
                if (!lines[i].Contains(SearchPattern, StringComparison.Ordinal))
                    continue;

                var trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal)
                    || trimmed.StartsWith("///", StringComparison.Ordinal)
                    || trimmed.StartsWith("*", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!AllowedFiles.ContainsKey(fileName))
                    offenders.Add($"{fileName}:{i + 1}: {trimmed}");
            }
        }

        offenders.Should().BeEmpty(
            "every File.Exists call site in the LSP server must be in the rationale'd allowlist. "
            + "A silent capability drop (the #1541 class) appears as an un-allowlisted site. "
            + "If you added a new File.Exists call, either surface the absence loudly and add it "
            + "to the allowlist with a rationale, or handle the absent case without degrading "
            + "functionality.\nOffending call sites:\n"
            + string.Join("\n", offenders));
    }

    [Fact]
    public void Allowlist_entries_have_at_least_one_matching_call_site()
    {
        var serverDirectory = FindServerSourceDirectory();

        var matched = new SCG.HashSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(serverDirectory, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var fileName = Path.GetFileName(file);
            if (!AllowedFiles.ContainsKey(fileName))
                continue;

            var content = File.ReadAllText(file);
            if (content.Contains(SearchPattern, StringComparison.Ordinal))
                matched.Add(fileName);
        }

        var stale = AllowedFiles.Keys.Except(matched).ToList();
        stale.Should().BeEmpty(
            "every allowlist entry must match at least one File.Exists call site — stale entries "
            + "mask the census. Remove entries for files that no longer call File.Exists.");
    }

    private static string FindServerSourceDirectory()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "src", "Sharpy.Lsp");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "Program.cs")))
                return candidate;

            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException(
            "Could not locate src/Sharpy.Lsp. This scan is only meaningful against real sources: "
            + "a walk of the wrong directory passes exactly like a clean tree.");
    }
}
