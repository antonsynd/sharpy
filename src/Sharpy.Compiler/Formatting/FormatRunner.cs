using System.Text;
using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler.Formatting;

/// <summary>
/// Mode in which a <see cref="FormatRunner"/> operation is executed.
/// </summary>
public enum FormatMode
{
    /// <summary>Format files in place (or to the configured output).</summary>
    Write,

    /// <summary>Report whether any files would change; do not modify them.</summary>
    Check,

    /// <summary>Print a unified diff for each file that would change.</summary>
    Diff,
}

/// <summary>
/// Options consumed by <see cref="FormatRunner"/>.
/// </summary>
public record FormatRunnerOptions
{
    public FormatMode Mode { get; init; } = FormatMode.Write;
    public FormatOptions FormatOptions { get; init; } = FormatOptions.Default;

    /// <summary>
    /// When formatting a single file, write the formatted text to this path
    /// instead of overwriting the input. Ignored when the input is a directory.
    /// </summary>
    public string? OutputPath { get; init; }
}

/// <summary>
/// Result of formatting one file.
/// </summary>
public record FormatFileOutcome
{
    public string FilePath { get; init; } = "";
    public string OriginalText { get; init; } = "";
    public string FormattedText { get; init; } = "";
    public bool Changed { get; init; }
    public bool Wrote { get; init; }
    public string? Diff { get; init; }
    public IReadOnlyList<CompilerDiagnostic> Diagnostics { get; init; } = Array.Empty<CompilerDiagnostic>();
    public bool HasError { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Aggregate result of a formatter run over one input (file or directory).
/// </summary>
public record FormatRunResult
{
    public IReadOnlyList<FormatFileOutcome> Outcomes { get; init; } = Array.Empty<FormatFileOutcome>();
    public int ChangedCount { get; init; }
    public int ErrorCount { get; init; }
    public int ExitCode { get; init; }
}

/// <summary>
/// Driver that applies <see cref="FormatterService"/> to a file or directory.
/// Lives in <c>Sharpy.Compiler</c> so it can be exercised directly from tests
/// without invoking the CLI binary.
/// </summary>
public static class FormatRunner
{
    /// <summary>
    /// Discover Sharpy source files under <paramref name="directoryPath"/>.
    /// Returns paths sorted for deterministic ordering.
    /// </summary>
    public static IReadOnlyList<string> FindSharpyFiles(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return Array.Empty<string>();
        }

        var results = Directory.EnumerateFiles(directoryPath, "*.spy", SearchOption.AllDirectories)
            .ToList();
        results.Sort(StringComparer.Ordinal);
        return results;
    }

    /// <summary>
    /// Format a single file according to <paramref name="options"/>.
    /// Does not throw on I/O errors — they are reported via the returned outcome.
    /// </summary>
    public static FormatFileOutcome FormatFile(string filePath, FormatRunnerOptions options)
    {
        string source;
        try
        {
            source = File.ReadAllText(filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new FormatFileOutcome
            {
                FilePath = filePath,
                HasError = true,
                ErrorMessage = ex.Message,
            };
        }

        var result = FormatterService.Format(source, options.FormatOptions, filePath);

        // If the formatter reported diagnostics it has returned the source unchanged.
        if (result.Diagnostics.Any(d => d.IsError))
        {
            return new FormatFileOutcome
            {
                FilePath = filePath,
                OriginalText = source,
                FormattedText = source,
                Changed = false,
                Diagnostics = result.Diagnostics,
                HasError = true,
                ErrorMessage = "syntax errors prevented formatting",
            };
        }

        var changed = result.HasChanges;
        var diff = (changed && options.Mode == FormatMode.Diff)
            ? ComputeUnifiedDiff(source, result.FormattedText, filePath)
            : null;

        var wrote = false;
        if (changed && options.Mode == FormatMode.Write)
        {
            var destination = options.OutputPath ?? filePath;
            try
            {
                File.WriteAllText(destination, result.FormattedText);
                wrote = true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new FormatFileOutcome
                {
                    FilePath = filePath,
                    OriginalText = source,
                    FormattedText = result.FormattedText,
                    Changed = changed,
                    Diagnostics = result.Diagnostics,
                    HasError = true,
                    ErrorMessage = ex.Message,
                };
            }
        }
        else if (options.Mode == FormatMode.Write && options.OutputPath != null && !changed)
        {
            // Even when unchanged, --output should produce a file.
            try
            {
                File.WriteAllText(options.OutputPath, result.FormattedText);
                wrote = true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new FormatFileOutcome
                {
                    FilePath = filePath,
                    OriginalText = source,
                    FormattedText = result.FormattedText,
                    Changed = false,
                    Diagnostics = result.Diagnostics,
                    HasError = true,
                    ErrorMessage = ex.Message,
                };
            }
        }

        return new FormatFileOutcome
        {
            FilePath = filePath,
            OriginalText = source,
            FormattedText = result.FormattedText,
            Changed = changed,
            Wrote = wrote,
            Diff = diff,
            Diagnostics = result.Diagnostics,
        };
    }

    /// <summary>
    /// Run the formatter on a file or directory and aggregate results.
    /// </summary>
    public static FormatRunResult Run(string inputPath, FormatRunnerOptions options)
    {
        var outcomes = new List<FormatFileOutcome>();

        if (File.Exists(inputPath))
        {
            outcomes.Add(FormatFile(inputPath, options));
        }
        else if (Directory.Exists(inputPath))
        {
            // OutputPath does not make sense for directory input — ignore it.
            var dirOptions = options.OutputPath == null ? options : options with { OutputPath = null };
            foreach (var file in FindSharpyFiles(inputPath))
            {
                outcomes.Add(FormatFile(file, dirOptions));
            }
        }
        else
        {
            outcomes.Add(new FormatFileOutcome
            {
                FilePath = inputPath,
                HasError = true,
                ErrorMessage = $"path does not exist: {inputPath}",
            });
        }

        var changedCount = outcomes.Count(o => o.Changed && !o.HasError);
        var errorCount = outcomes.Count(o => o.HasError);
        var exitCode = ComputeExitCode(options.Mode, changedCount, errorCount);

        return new FormatRunResult
        {
            Outcomes = outcomes,
            ChangedCount = changedCount,
            ErrorCount = errorCount,
            ExitCode = exitCode,
        };
    }

    private static int ComputeExitCode(FormatMode mode, int changedCount, int errorCount)
    {
        if (errorCount > 0)
        {
            return 2;
        }

        if (mode == FormatMode.Check && changedCount > 0)
        {
            return 1;
        }

        return 0;
    }

    /// <summary>
    /// Compute a simple unified diff between <paramref name="original"/> and
    /// <paramref name="formatted"/>. Uses an LCS-based line diff and emits one
    /// hunk covering the entire file — adequate for the formatter's output and
    /// far cheaper than a full Myers implementation.
    /// </summary>
    public static string ComputeUnifiedDiff(string original, string formatted, string filePath)
    {
        var originalLines = SplitLines(original);
        var formattedLines = SplitLines(formatted);

        var sb = new StringBuilder();
        sb.Append("--- ").Append(filePath).Append('\n');
        sb.Append("+++ ").Append(filePath).Append('\n');
        sb.Append("@@ -1,").Append(originalLines.Count).Append(" +1,").Append(formattedLines.Count).Append(" @@\n");

        foreach (var op in LineDiff(originalLines, formattedLines))
        {
            switch (op.Kind)
            {
                case DiffOpKind.Equal:
                    sb.Append(' ').Append(op.Line).Append('\n');
                    break;
                case DiffOpKind.Delete:
                    sb.Append('-').Append(op.Line).Append('\n');
                    break;
                case DiffOpKind.Insert:
                    sb.Append('+').Append(op.Line).Append('\n');
                    break;
            }
        }

        return sb.ToString();
    }

    private enum DiffOpKind { Equal, Delete, Insert }

    private readonly record struct DiffOp(DiffOpKind Kind, string Line);

    private static List<string> SplitLines(string text)
    {
        // Split on \n; strip trailing \r so output is consistent regardless of
        // the source file's line endings.
        var raw = text.Split('\n');
        var result = new List<string>(raw.Length);
        for (var i = 0; i < raw.Length; i++)
        {
            var line = raw[i];
            if (line.Length > 0 && line[^1] == '\r')
            {
                line = line.Substring(0, line.Length - 1);
            }
            // Drop a synthetic empty trailing element produced by a final newline,
            // matching git/diff behaviour of treating the last \n as a terminator.
            if (i == raw.Length - 1 && line.Length == 0)
            {
                continue;
            }
            result.Add(line);
        }
        return result;
    }

    private static List<DiffOp> LineDiff(List<string> a, List<string> b)
    {
        var n = a.Count;
        var m = b.Count;

        // Standard LCS table (O(n*m) memory). Fine for typical source files.
        var lcs = new int[n + 1, m + 1];
        for (var i = n - 1; i >= 0; i--)
        {
            for (var j = m - 1; j >= 0; j--)
            {
                lcs[i, j] = a[i] == b[j]
                    ? lcs[i + 1, j + 1] + 1
                    : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            }
        }

        var ops = new List<DiffOp>();
        var x = 0;
        var y = 0;
        while (x < n && y < m)
        {
            if (a[x] == b[y])
            {
                ops.Add(new DiffOp(DiffOpKind.Equal, a[x]));
                x++;
                y++;
            }
            else if (lcs[x + 1, y] >= lcs[x, y + 1])
            {
                ops.Add(new DiffOp(DiffOpKind.Delete, a[x]));
                x++;
            }
            else
            {
                ops.Add(new DiffOp(DiffOpKind.Insert, b[y]));
                y++;
            }
        }
        while (x < n)
        {
            ops.Add(new DiffOp(DiffOpKind.Delete, a[x++]));
        }
        while (y < m)
        {
            ops.Add(new DiffOp(DiffOpKind.Insert, b[y++]));
        }
        return ops;
    }
}
