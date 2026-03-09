using System.Globalization;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// Renders compiler diagnostics with source context, underlines, and optional ANSI colors.
///
/// Output format (with span):
/// <code>
/// error[SPY0201]: Type 'str' is not assignable to 'int'
///   --> file.spy:3:5
///    |
///  3 |     x: int = "hello"
///    |              ^^^^^^^ expected 'int', found 'str'
///    |
/// </code>
///
/// Output format (with line/column only):
/// <code>
/// error[SPY0201]: Type 'str' is not assignable to 'int'
///   --> file.spy:3:5
///    |
///  3 |     x: int = "hello"
///    |     ^
///    |
/// </code>
///
/// Output format (no location):
/// <code>
/// error[SPY0201]: Type 'str' is not assignable to 'int'
/// </code>
/// </summary>
internal class DiagnosticRenderer
{
    private readonly bool _useColor;

    /// <summary>
    /// Creates a new DiagnosticRenderer.
    /// </summary>
    /// <param name="useColor">Whether to use ANSI color codes in output.</param>
    public DiagnosticRenderer(bool useColor = false)
    {
        _useColor = useColor;
    }

    /// <summary>
    /// Detects whether the current terminal supports color output.
    /// </summary>
    public static bool IsColorSupported()
    {
        // NO_COLOR convention: https://no-color.org/
        if (Environment.GetEnvironmentVariable("NO_COLOR") != null)
            return false;

        // Check if stdout is redirected (piped)
        if (Console.IsOutputRedirected)
            return false;

        // Check TERM environment variable
        var term = Environment.GetEnvironmentVariable("TERM");
        if (term == "dumb")
            return false;

        return true;
    }

    /// <summary>
    /// Renders a diagnostic with source context.
    /// </summary>
    /// <param name="diagnostic">The diagnostic to render.</param>
    /// <param name="sourceText">The source text for context rendering. Can be null.</param>
    /// <returns>The formatted diagnostic string.</returns>
    public string Render(CompilerDiagnostic diagnostic, SourceText? sourceText = null)
    {
        var lines = new List<string>();

        // Line 1: severity[code]: message
        lines.Add(RenderHeader(diagnostic));

        // Determine location info
        int? line = diagnostic.Line;
        int? column = diagnostic.Column;
        string? filePath = diagnostic.FilePath;

        // If we have a span and source text, derive line/column from span
        if (diagnostic.Span.HasValue && sourceText != null)
        {
            var span = diagnostic.Span.Value;
            if (span.Start >= 0 && span.Start <= sourceText.Length)
            {
                var pos = sourceText.GetLineAndColumn(span.Start);
                line = pos.Line;
                column = pos.Column;
            }
        }

        // Use source text file path if diagnostic doesn't have one
        if (string.IsNullOrEmpty(filePath) && sourceText?.FilePath != null)
        {
            filePath = sourceText.FilePath;
        }

        // Line 2: location arrow
        if (line.HasValue)
        {
            var location = FormatLocation(filePath, line.Value, column);
            lines.Add($"  {Colorize("-->", AnsiColor.Cyan)} {location}");
        }
        else if (!string.IsNullOrEmpty(filePath))
        {
            lines.Add($"  {Colorize("-->", AnsiColor.Cyan)} {filePath}");
        }
        else
        {
            // No location info at all - just return the header
            return string.Join(Environment.NewLine, lines);
        }

        // Source context: only if we have a line number
        if (line.HasValue && sourceText != null && line.Value >= 1 && line.Value <= sourceText.LineCount)
        {
            var lineNumberWidth = line.Value.ToString(CultureInfo.InvariantCulture).Length;
            // Ensure at least 2 chars for gutter width for visual consistency
            if (lineNumberWidth < 2)
                lineNumberWidth = 2;

            var gutter = new string(' ', lineNumberWidth);
            var pipe = Colorize("|", AnsiColor.Cyan);

            // Empty gutter line
            lines.Add($" {gutter} {pipe}");

            // Source line (expand tabs for consistent display)
            var sourceLineText = sourceText.GetLineText(line.Value);
            var displayLine = ExpandTabs(sourceLineText);
            var lineNumStr = line.Value.ToString(CultureInfo.InvariantCulture).PadLeft(lineNumberWidth);
            lines.Add($" {Colorize(lineNumStr, AnsiColor.Cyan)} {pipe} {displayLine}");

            // Underline/caret line
            var underline = RenderUnderline(diagnostic, sourceText, sourceLineText, line.Value, column);
            if (underline != null)
            {
                lines.Add($" {gutter} {pipe} {underline}");
            }

            // Trailing empty gutter line
            lines.Add($" {gutter} {pipe}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Renders the header line: severity[code]: message
    /// </summary>
    private string RenderHeader(CompilerDiagnostic diagnostic)
    {
        var (severityText, severityColor) = diagnostic.Severity switch
        {
            CompilerDiagnosticSeverity.Error => ("error", AnsiColor.Red),
            CompilerDiagnosticSeverity.Warning => ("warning", AnsiColor.Yellow),
            CompilerDiagnosticSeverity.Info => ("info", AnsiColor.Blue),
            CompilerDiagnosticSeverity.Hint => ("hint", AnsiColor.Green),
            _ => ("diagnostic", AnsiColor.White)
        };

        string header;
        if (!string.IsNullOrEmpty(diagnostic.Code))
        {
            header = $"{Colorize($"{severityText}[{diagnostic.Code}]", severityColor, bold: true)}: {Colorize(diagnostic.Message, AnsiColor.White, bold: true)}";
        }
        else
        {
            header = $"{Colorize(severityText, severityColor, bold: true)}: {Colorize(diagnostic.Message, AnsiColor.White, bold: true)}";
        }

        return header;
    }

    /// <summary>
    /// Renders the underline or caret for the error location.
    /// </summary>
    private string? RenderUnderline(
        CompilerDiagnostic diagnostic,
        SourceText sourceText,
        string sourceLineText,
        int lineNumber,
        int? column)
    {
        if (diagnostic.Span.HasValue)
        {
            return RenderSpanUnderline(diagnostic.Span.Value, sourceText, sourceLineText, lineNumber, diagnostic.Severity);
        }

        if (column.HasValue && column.Value >= 1)
        {
            return RenderCaretAtColumn(column.Value, sourceLineText);
        }

        return null;
    }

    /// <summary>
    /// Renders underline markers (^^^) for a text span on a single source line.
    /// For multi-line spans, underlines only the portion on the first line.
    /// Tab characters are accounted for in the display position calculation.
    /// </summary>
    private string RenderSpanUnderline(TextSpan span, SourceText sourceText, string sourceLineText, int lineNumber, CompilerDiagnosticSeverity severity)
    {
        var lineStartPos = sourceText.GetPosition(lineNumber, 1);

        // Calculate the underline start and end (0-based character offsets within the line)
        var charStart = System.Math.Max(0, span.Start - lineStartPos);
        var charEnd = System.Math.Min(sourceLineText.Length, span.End - lineStartPos);

        // Clamp charStart to line length
        if (charStart > sourceLineText.Length)
        {
            charStart = sourceLineText.Length;
            charEnd = charStart + 1;
        }

        // Convert character offsets to display columns (accounting for tabs)
        var displayStart = ExpandedColumn(sourceLineText, charStart);
        var displayEnd = ExpandedColumn(sourceLineText, System.Math.Min(charEnd, sourceLineText.Length));

        // Ensure underline length is at least 1
        var underlineLength = System.Math.Max(1, displayEnd - displayStart);

        var padding = new string(' ', displayStart);
        var markers = new string('^', underlineLength);

        var severityColor = severity switch
        {
            CompilerDiagnosticSeverity.Error => AnsiColor.Red,
            CompilerDiagnosticSeverity.Warning => AnsiColor.Yellow,
            _ => AnsiColor.Blue
        };

        return $"{padding}{Colorize(markers, severityColor, bold: true)}";
    }

    /// <summary>
    /// Renders a single caret (^) at the specified column.
    /// Tab characters are accounted for in the display position calculation.
    /// </summary>
    private string RenderCaretAtColumn(int column, string sourceLineText)
    {
        // Column is 1-based, convert to 0-based character offset
        var charOffset = System.Math.Min(System.Math.Max(0, column - 1), sourceLineText.Length);
        // Convert to display position (accounting for tabs)
        var displayCol = ExpandedColumn(sourceLineText, charOffset);
        var padding = new string(' ', displayCol);
        return $"{padding}{Colorize("^", AnsiColor.Red, bold: true)}";
    }

    private const int TabWidth = 4;

    /// <summary>
    /// Expands tab characters to spaces using fixed-width tab stops.
    /// This ensures consistent display regardless of terminal tab settings.
    /// </summary>
    private static string ExpandTabs(string line)
    {
        if (!line.Contains('\t'))
            return line;

        var sb = new System.Text.StringBuilder(line.Length + 8);
        foreach (var ch in line)
        {
            if (ch == '\t')
            {
                var spaces = TabWidth - (sb.Length % TabWidth);
                sb.Append(' ', spaces);
            }
            else
            {
                sb.Append(ch);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Converts a 0-based character offset within a line to the corresponding
    /// display column, accounting for tab expansion.
    /// </summary>
    private static int ExpandedColumn(string line, int charOffset)
    {
        int display = 0;
        var limit = System.Math.Min(charOffset, line.Length);
        for (int i = 0; i < limit; i++)
        {
            if (line[i] == '\t')
            {
                display += TabWidth - (display % TabWidth);
            }
            else
            {
                display++;
            }
        }
        return display;
    }

    /// <summary>
    /// Formats a file:line:column location string.
    /// </summary>
    private static string FormatLocation(string? filePath, int line, int? column)
    {
        var file = !string.IsNullOrEmpty(filePath) ? filePath : "<source>";

        if (column.HasValue)
            return $"{file}:{line}:{column}";
        return $"{file}:{line}";
    }

    // ANSI color codes
    private enum AnsiColor
    {
        Red,
        Yellow,
        Blue,
        Green,
        Cyan,
        White
    }

    private string Colorize(string text, AnsiColor color, bool bold = false)
    {
        if (!_useColor)
            return text;

        var colorCode = color switch
        {
            AnsiColor.Red => "31",
            AnsiColor.Yellow => "33",
            AnsiColor.Blue => "34",
            AnsiColor.Green => "32",
            AnsiColor.Cyan => "36",
            AnsiColor.White => "37",
            _ => "37"
        };

        var boldCode = bold ? "1;" : "";
        return $"\x1b[{boldCode}{colorCode}m{text}\x1b[0m";
    }
}
