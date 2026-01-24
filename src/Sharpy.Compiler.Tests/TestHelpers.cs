using System.Text;
using System.Text.RegularExpressions;
using Sharpy.Compiler.Logging;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests;

/// <summary>
/// Shared test utilities and helper classes for compiler tests.
/// </summary>
public static class TestHelpers
{
    /// <summary>
    /// Wraps Sharpy source code in a main() function if it doesn't already have one.
    /// This is used to make test code compliant with the entry point rules
    /// that require a main() function in entry point files.
    /// </summary>
    /// <param name="source">The Sharpy source code.</param>
    /// <returns>The source code with a main() wrapper if needed.</returns>
    public static string WrapWithMainIfNeeded(string source)
    {
        // Check if source already has a main function definition
        if (Regex.IsMatch(source, @"^\s*def\s+main\s*\(", RegexOptions.Multiline))
        {
            return source;
        }

        // Separate declarations (at top level, unindented) from executable code
        var lines = source.Split('\n');
        var declarations = new StringBuilder();
        var executable = new StringBuilder();
        bool inDeclaration = false;
        int declarationIndent = 0;

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            var currentIndent = line.Length - line.TrimStart().Length;

            // Check if this line starts a declaration (or module-level statement that stays at module level)
            bool isDeclarationStart = trimmed.StartsWith("def ") ||
                                       trimmed.StartsWith("class ") ||
                                       trimmed.StartsWith("struct ") ||
                                       trimmed.StartsWith("interface ") ||
                                       trimmed.StartsWith("enum ") ||
                                       trimmed.StartsWith("const ") ||
                                       trimmed.StartsWith("import ") ||
                                       trimmed.StartsWith("from ") ||
                                       trimmed.StartsWith("@") || // Decorators stay with their declarations
                                       (trimmed.Contains(": ") && !trimmed.StartsWith("#") && currentIndent == 0 && !trimmed.Contains("("));

            // Handle continuation of declarations (indented body)
            if (inDeclaration && currentIndent > declarationIndent)
            {
                declarations.AppendLine(line);
                continue;
            }

            // End of declaration body when we return to original indent level
            if (inDeclaration && currentIndent <= declarationIndent && !string.IsNullOrWhiteSpace(trimmed))
            {
                inDeclaration = false;
            }

            if (isDeclarationStart && currentIndent == 0)
            {
                inDeclaration = true;
                declarationIndent = currentIndent;
                declarations.AppendLine(line);
            }
            else if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
            {
                // Comments and blank lines go to both (context-dependent, but simplest to duplicate)
                if (inDeclaration)
                    declarations.AppendLine(line);
                else
                    executable.AppendLine(line);
            }
            else if (currentIndent == 0 && !inDeclaration)
            {
                // Top-level executable code
                executable.AppendLine(line);
            }
            else
            {
                // Indented code (part of declaration body or other)
                if (inDeclaration)
                    declarations.AppendLine(line);
                else
                    executable.AppendLine(line);
            }
        }

        // Build the wrapped source
        var result = new StringBuilder();
        if (declarations.Length > 0)
        {
            result.Append(declarations);
            result.AppendLine();
        }

        var executableCode = executable.ToString().TrimEnd();
        // Check if there's any actual executable code (not just comments and whitespace)
        var hasExecutableCode = executableCode.Split('\n')
            .Any(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("#"));

        if (hasExecutableCode)
        {
            result.AppendLine("def main():");
            // Indent each line of executable code
            foreach (var line in executableCode.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line))
                    result.AppendLine();
                else
                    result.AppendLine("    " + line);
            }
        }
        else
        {
            // If there's no executable code, just add an empty main
            result.AppendLine("def main():");
            result.AppendLine("    pass");
        }

        return result.ToString();
    }

    /// <summary>
    /// Global lock for console I/O operations during test execution.
    /// Tests that redirect Console.Out/Console.Error should acquire this lock
    /// to prevent interference from parallel test execution.
    /// </summary>
    public static readonly object ConsoleLock = new object();
    /// <summary>
    /// Test logger that collects errors and warnings for assertion in tests.
    /// Useful for testing error reporting behavior without requiring ITestOutputHelper.
    /// </summary>
    public class CollectingTestLogger : ICompilerLogger
    {
        public List<(string Message, int Line, int Column)> Errors { get; } = new();
        public List<(string Message, int Line, int Column)> Warnings { get; } = new();

        public void LogError(string message, int line, int column)
        {
            Errors.Add((message, line, column));
        }

        public void LogWarning(string message, int line, int column)
        {
            Warnings.Add((message, line, column));
        }

        public void LogTokenRead(string tokenType, int line, int column, string value) { }
        public void LogIndentChange(int oldLevel, int newLevel) { }
        public void LogParseEnter(string rule, int tokenPosition) { }
        public void LogParseExit(string rule, bool success) { }
        public void LogInfo(string message) { }
        public void LogDebug(string message) { }
        public void LogTrace(string message) { }
        public void LogMetrics(string metricsOutput) { }
        public bool IsEnabled(CompilerLogLevel level) => level <= CompilerLogLevel.Warning;
    }

    /// <summary>
    /// Test logger that writes to xUnit test output.
    /// Useful for debugging and viewing compilation details during test runs.
    /// </summary>
    public class OutputTestLogger : ICompilerLogger
    {
        private readonly ITestOutputHelper _output;

        public OutputTestLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        public void LogTokenRead(string tokenType, int line, int column, string value)
        {
            // Don't log tokens during tests to avoid clutter
        }

        public void LogIndentChange(int oldLevel, int newLevel)
        {
            // Don't log indent changes during tests
        }

        public void LogParseEnter(string rule, int tokenPosition)
        {
            // Don't log parse enter during tests
        }

        public void LogParseExit(string rule, bool success)
        {
            // Don't log parse exit during tests
        }

        public void LogError(string message, int line, int column)
        {
            _output.WriteLine($"ERROR [{line},{column}]: {message}");
        }

        public void LogWarning(string message, int line, int column)
        {
            _output.WriteLine($"WARNING [{line},{column}]: {message}");
        }

        public void LogInfo(string message)
        {
            _output.WriteLine($"INFO: {message}");
        }

        public void LogDebug(string message)
        {
            _output.WriteLine($"DEBUG: {message}");
        }

        public void LogTrace(string message)
        {
            // Don't log trace during tests
        }

        public void LogMetrics(string metricsOutput)
        {
            // Don't log metrics during tests unless explicitly needed
        }

        public bool IsEnabled(CompilerLogLevel level)
        {
            return level <= CompilerLogLevel.Info;
        }
    }
}
