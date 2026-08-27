using System.Globalization;
using System.Reflection;
using System.Text;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// Describes an internal-compiler-error crash for the minimal-repro bundle writer.
/// All fields are optional and best-effort: the writer degrades gracefully when any are absent.
/// </summary>
public sealed class CrashBundleRequest
{
    /// <summary>The exception that escaped a compilation phase, if any.</summary>
    public Exception? Exception { get; init; }

    /// <summary>The phase the compiler had reached when it crashed (from the diagnostic bag's high-water mark).</summary>
    public CompilerPhase? Phase { get; init; }

    /// <summary>The producer/validator active when the crash occurred, if any.</summary>
    public string? Producer { get; init; }

    /// <summary>The compiler component (from <see cref="InternalCompilerErrorException.Component"/>), if known.</summary>
    public string? Component { get; init; }

    /// <summary>
    /// Source files to embed, keyed by file path. Values are file contents. When a path exists on
    /// disk but no content is supplied, the writer reads it. Large files are excerpted around the span.
    /// </summary>
    public IReadOnlyDictionary<string, string>? SourceFiles { get; init; }

    /// <summary>The nearest AST node to the crash, if known. Used to render a span and a source excerpt.</summary>
    public Node? Node { get; init; }

    /// <summary>The nearest source span to the crash, if known. Falls back to <see cref="Node"/>'s span.</summary>
    public TextSpan? Span { get; init; }

    /// <summary>1-based line of the nearest span, if known.</summary>
    public int? Line { get; init; }

    /// <summary>1-based column of the nearest span, if known.</summary>
    public int? Column { get; init; }

    /// <summary>File path the span refers to, if known.</summary>
    public string? SpanFilePath { get; init; }

    /// <summary>Short kind label for the bundle (e.g. "internal-compiler-error", "unexpected-unknown-type").</summary>
    public string Kind { get; init; } = "internal-compiler-error";
}

/// <summary>
/// Writes a minimal-repro crash bundle when the compiler's last-chance handler catches an
/// unexpected exception (SPY0909). The bundle captures the source file(s) or a minimal excerpt,
/// the compiler version/commit, the failing phase and producer, the exception and stack trace,
/// and the nearest AST span — everything a maintainer needs to reproduce the crash from a bug report.
/// </summary>
/// <remarks>
/// Bundle writing is failure-proof by contract: every public entry point swallows its own
/// exceptions and returns <c>null</c> rather than throwing, because it runs inside a catch block
/// where a second throw would mask the original crash.
/// </remarks>
public static class CrashBundleWriter
{
    /// <summary>Name of the crash-bundle root directory created under the chosen output location.</summary>
    public const string CrashRootDirectoryName = ".sharpy-crash";

    /// <summary>
    /// Path segments that hold build output or compiler scratch rather than source.
    /// A source glob matching <c>*.spy</c> with <see cref="SearchOption.AllDirectories"/> must
    /// exclude paths under any of these segments, or stale build output and crash-bundle copies
    /// are treated as sources (#1660).
    /// </summary>
    private static readonly string[] NonSourceSegments = ["bin", "obj", CrashRootDirectoryName];

    /// <summary>
    /// Whether <paramref name="relativePath"/> lies under a build-output or scratch directory.
    /// The path must already be relative to the project/corpus root. Shared by the compiler
    /// (<see cref="Project.ProjectConfig.ResolveGlobPattern"/>), the formatter, and the test
    /// infrastructure (<c>FixtureDiscoveryHelper</c>) so every source glob applies one predicate
    /// (#1660). <c>InternalsVisibleTo("Sharpy.TestInfrastructure")</c> makes this accessible.
    /// </summary>
    internal static bool IsNonSourceSegment(string relativePath)
        => relativePath
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => NonSourceSegments.Contains(segment, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Key under which the shared span descriptor is stamped onto a diagnostic's
    /// <see cref="CompilerDiagnostic.Data"/> so span info is carried in the same format the bundle uses.
    /// </summary>
    public const string SpanDataKey = "crash-span";

    /// <summary>
    /// Writes a crash bundle under <c>&lt;outputDirectory&gt;/.sharpy-crash/&lt;timestamp&gt;/</c>.
    /// Returns the bundle directory path on success, or <c>null</c> if writing failed for any reason
    /// (the caller must treat a null result as "no bundle" and never let it interrupt error reporting).
    /// </summary>
    public static string? TryWrite(string? outputDirectory, CrashBundleRequest request)
    {
        try
        {
            var root = ResolveCrashRoot(outputDirectory);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            var bundleDir = Path.Combine(root, timestamp);

            // Extremely unlikely collision guard (two crashes in the same millisecond).
            var suffix = 0;
            var candidate = bundleDir;
            while (Directory.Exists(candidate) && suffix < 100)
            {
                suffix++;
                candidate = $"{bundleDir}-{suffix}";
            }
            bundleDir = candidate;
            Directory.CreateDirectory(bundleDir);

            WriteReport(bundleDir, request);
            WriteSources(bundleDir, request);

            return bundleDir;
        }
        catch
        {
            // Never throw out of the last-chance handler.
            return null;
        }
    }

    /// <summary>
    /// Formats a span into the canonical one-line descriptor used across crash bundles and
    /// SPY0907 diagnostics, so span information is rendered identically wherever it appears.
    /// Returns null when no location information is available.
    /// </summary>
    public static string? DescribeSpan(TextSpan? span, int? line, int? column, string? filePath)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(filePath))
            parts.Add(filePath!);
        if (line.HasValue)
            parts.Add(column.HasValue
                ? $"line {line.Value}, col {column.Value}"
                : $"line {line.Value}");
        if (span.HasValue)
            parts.Add($"offset {span.Value.Start}..{span.Value.End}");

        return parts.Count == 0 ? null : string.Join(" @ ", parts);
    }

    private static string ResolveCrashRoot(string? outputDirectory)
    {
        var baseDir = !string.IsNullOrWhiteSpace(outputDirectory)
            ? outputDirectory!
            : Directory.GetCurrentDirectory();
        return Path.Combine(baseDir, CrashRootDirectoryName);
    }

    private static void WriteReport(string bundleDir, CrashBundleRequest request)
    {
        var sb = new StringBuilder();
        // Local helper: append a pre-built string + newline. Building the interpolated string first
        // and appending it as a plain string keeps culture-sensitive formatting explicit (we format
        // the only IFormattable values below with InvariantCulture) and out of StringBuilder.AppendLine.
        void Line(string text = "") => sb.Append(text).Append('\n');

        Line("# Sharpy internal compiler error (SPY0909)");
        Line();
        Line("An unexpected exception escaped a compilation phase. This is a compiler bug.");
        Line("Please report it at https://github.com/antonsynd/sharpy/issues and attach this bundle.");
        Line();

        Line("## Environment");
        Line("- Timestamp (UTC): " + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        Line("- Compiler version: " + GetCompilerVersion());
        var commit = GetCompilerCommit();
        if (commit != null)
            Line("- Compiler commit: " + commit);
        Line("- .NET runtime: " + Environment.Version.ToString());
        Line("- OS: " + Environment.OSVersion.ToString());
        Line();

        Line("## Crash context");
        Line("- Kind: " + request.Kind);
        Line("- Phase: " + (request.Phase?.ToString() ?? "(unknown)"));
        Line("- Producer: " + (request.Producer ?? "(none)"));
        Line("- Component: " + (request.Component ?? "(unknown)"));
        var spanDesc = DescribeSpan(request.Span ?? request.Node?.Span, request.Line, request.Column, request.SpanFilePath);
        Line("- Nearest AST span: " + (spanDesc ?? "(unknown)"));
        if (request.Node != null)
            Line("- Nearest AST node: " + request.Node.GetType().Name);
        Line();

        Line("## Exception");
        if (request.Exception != null)
        {
            var ex = request.Exception;
            Line("- Type: " + ex.GetType().FullName);
            Line("- Message: " + ex.Message);
            Line();
            Line("```");
            Line(ex.ToString());
            Line("```");
        }
        else
        {
            Line("(no exception captured)");
        }
        Line();

        var excerpt = BuildExcerpt(request);
        if (excerpt != null)
        {
            Line("## Source excerpt (near crash span)");
            Line("```");
            Line(excerpt);
            Line("```");
            Line();
        }

        File.WriteAllText(Path.Combine(bundleDir, "report.md"), sb.ToString());
    }

    private static void WriteSources(string bundleDir, CrashBundleRequest request)
    {
        if (request.SourceFiles == null || request.SourceFiles.Count == 0)
            return;

        var sourcesDir = Path.Combine(bundleDir, "sources");
        Directory.CreateDirectory(sourcesDir);

        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, content) in request.SourceFiles)
        {
            try
            {
                var text = content;
                if (string.IsNullOrEmpty(text) && File.Exists(path))
                    text = File.ReadAllText(path);
                if (text == null)
                    continue;

                var fileName = MakeUniqueFileName(path, usedNames);
                File.WriteAllText(Path.Combine(sourcesDir, fileName), text);
            }
            catch
            {
                // Skip any file that can't be embedded; the report still stands.
            }
        }
    }

    private static string MakeUniqueFileName(string path, HashSet<string> used)
    {
        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(name))
            name = "source.spy";
        var candidate = name;
        var counter = 1;
        while (!used.Add(candidate))
        {
            var stem = Path.GetFileNameWithoutExtension(name);
            var ext = Path.GetExtension(name);
            candidate = $"{stem}.{counter}{ext}";
            counter++;
        }
        return candidate;
    }

    /// <summary>
    /// Builds a small source excerpt centered on the crash line, when a source with a known line
    /// is available. Returns null when there is nothing to excerpt.
    /// </summary>
    private static string? BuildExcerpt(CrashBundleRequest request)
    {
        var line = request.Line;
        if (line == null || line.Value <= 0 || request.SourceFiles == null)
            return null;

        // Pick the source that matches the span's file path, else the first source.
        string? content = null;
        if (request.SpanFilePath != null)
        {
            foreach (var (path, text) in request.SourceFiles)
            {
                if (string.Equals(path, request.SpanFilePath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Path.GetFileName(path), Path.GetFileName(request.SpanFilePath), StringComparison.OrdinalIgnoreCase))
                {
                    content = ResolveContent(path, text);
                    break;
                }
            }
        }
        content ??= request.SourceFiles.Count == 1
            ? ResolveContent(request.SourceFiles.First().Key, request.SourceFiles.First().Value)
            : null;

        if (string.IsNullOrEmpty(content))
            return null;

        var lines = content!.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        const int contextRadius = 3;
        var start = Math.Max(0, line.Value - 1 - contextRadius);
        var end = Math.Min(lines.Length - 1, line.Value - 1 + contextRadius);

        var sb = new StringBuilder();
        for (var i = start; i <= end; i++)
        {
            var marker = (i == line.Value - 1) ? ">" : " ";
            var lineNo = (i + 1).ToString(CultureInfo.InvariantCulture).PadLeft(5);
            sb.Append(marker).Append(' ').Append(lineNo).Append(": ").Append(lines[i]).Append('\n');
        }
        return sb.ToString().TrimEnd('\n');
    }

    private static string? ResolveContent(string path, string? text)
    {
        if (!string.IsNullOrEmpty(text))
            return text;
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch
        {
            return null;
        }
    }

    private static string GetCompilerVersion()
    {
        try
        {
            var asm = typeof(CrashBundleWriter).Assembly;
            var informational = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            return informational ?? asm.GetName().Version?.ToString() ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    private static string? GetCompilerCommit()
    {
        try
        {
            var informational = typeof(CrashBundleWriter).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            // SourceLink stamps informational version as "1.2.3+<40-char-sha>".
            if (informational != null)
            {
                var plus = informational.IndexOf('+', StringComparison.Ordinal);
                if (plus >= 0 && plus < informational.Length - 1)
                    return informational.Substring(plus + 1);
            }
            return null;
        }
        catch
        {
            return null;
        }
    }
}
