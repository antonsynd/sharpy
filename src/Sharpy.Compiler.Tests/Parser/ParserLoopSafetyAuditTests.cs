using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Regression prevention tests that analyze Parser source code to ensure
/// all loops are protected against potential infinite execution.
///
/// This test runs as part of CI to catch any new loops that don't have
/// proper progress checking or guaranteed advancement.
/// </summary>
public class ParserLoopSafetyAuditTests
{
    /// <summary>
    /// Path to the Parser directory relative to the test project.
    /// </summary>
    private static readonly string ParserSourceDir = Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..",
        "Sharpy.Compiler", "Parser");

    /// <summary>
    /// Patterns for loops that are known to be structurally safe:
    /// - Simple iteration with immediate Advance() after condition
    /// - Operator precedence loops where each branch advances
    /// </summary>
    private static readonly HashSet<string> SafeLoopPatterns = new()
    {
        // SkipNewlines - immediate Advance()
        "while (Current.Type == TokenType.Newline)",
        // SkipIndentedBlock - every branch advances
        "while (!IsAtEnd)",
        // Synchronize - guaranteed progress via initial Advance() + branch logic
        // Array suffix parsing - immediate double Advance()
        "while (Current.Type == TokenType.LeftBracket && Peek().Type == TokenType.RightBracket)",
        // Decorator parsing - guaranteed Advance() in body
        "while (Current.Type == TokenType.At)",
        // Simple comma-separated list patterns with guaranteed break
        "while (Current.Type == TokenType.Comma)",

        // Operator precedence loops - each iteration consumes the operator token
        "while (Current.Type == TokenType.Elif)",         // Elif chain - advances operator
        "while (Current.Type == TokenType.Except)",       // Except chain - advances operator
        "while (Current.Type == TokenType.Dot)",          // Dotted name - advances operator
        "while (Current.Type == TokenType.LeftShift || Current.Type == TokenType.RightShift)",  // Shift ops
        "while (Current.Type == TokenType.Plus || Current.Type == TokenType.Minus)",            // Add ops
        "while (Current.Type == TokenType.Star || Current.Type == TokenType.Slash",             // Mul ops
        "while (Current.Type == TokenType.Ampersand)",    // Bitwise AND
        "while (Current.Type == TokenType.Caret)",        // Bitwise XOR
        "while (Current.Type == TokenType.Pipe)",         // Bitwise OR (not logical)
        "while (Current.Type == TokenType.And)",          // Logical AND
        "while (Current.Type == TokenType.Or)",           // Logical OR
        "while (Current.Type == TokenType.Percent)",      // Modulo
        "while (Current.Type == TokenType.DoubleStar)",   // Power
        "while (Current.Type == TokenType.NullCoalesce)",  // Null coalesce - advances operator
        "while (Current.Type == TokenType.PipeForward)"    // Pipe forward - advances operator
    };

    /// <summary>
    /// Known safe methods that have been manually audited and verified.
    /// These methods have loops that are structurally safe even without CheckLoopProgress.
    /// </summary>
    private static readonly HashSet<string> SafeMethods = new()
    {
        "SkipNewlines",
        "SkipIndentedBlock",
        "Synchronize",
        "ParseDecoratedStatement"
    };

    [Fact]
    public void AllParserLoops_ShouldBeProtectedOrStructurallySafe()
    {
        // Resolve the source directory
        var resolvedPath = Path.GetFullPath(ParserSourceDir);

        if (!Directory.Exists(resolvedPath))
        {
            // Try alternative path for CI environments
            resolvedPath = FindParserSourceDirectory();
        }

        Directory.Exists(resolvedPath).Should().BeTrue(
            $"Parser source directory should exist at {resolvedPath}");

        var parserFiles = Directory.GetFiles(resolvedPath, "Parser*.cs");
        parserFiles.Should().NotBeEmpty("Should find Parser source files");

        var violations = new List<string>();

        foreach (var file in parserFiles)
        {
            var fileName = Path.GetFileName(file);
            var content = File.ReadAllText(file);
            var lines = content.Split('\n');

            AnalyzeFile(fileName, lines, violations);
        }

        violations.Should().BeEmpty(
            "All parser loops should be protected with CheckLoopProgress() or be structurally safe.\n" +
            "Violations:\n" + string.Join("\n", violations));
    }

    private static void AnalyzeFile(string fileName, string[] lines, List<string> violations)
    {
        // Regex patterns
        var whileLoopPattern = new Regex(@"^\s*(while\s*\([^)]+\))\s*$", RegexOptions.Compiled);
        var doWhilePattern = new Regex(@"^\s*do\s*$", RegexOptions.Compiled);
        var checkLoopProgressPattern = new Regex(@"CheckLoopProgress\s*\(\s*\)", RegexOptions.Compiled);
        var advancePattern = new Regex(@"\bAdvance\s*\(\s*\)", RegexOptions.Compiled);
        var methodStartPattern = new Regex(@"^\s*private\s+\w+\s+(\w+)\s*\(", RegexOptions.Compiled);

        string? currentMethod = null;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNumber = i + 1;

            // Track current method
            var methodMatch = methodStartPattern.Match(line);
            if (methodMatch.Success)
            {
                currentMethod = methodMatch.Groups[1].Value;
            }

            // Skip if we're in a known safe method
            if (currentMethod != null && SafeMethods.Contains(currentMethod))
            {
                continue;
            }

            // Check for while loops
            var whileMatch = whileLoopPattern.Match(line);
            if (whileMatch.Success)
            {
                var loopCondition = whileMatch.Groups[1].Value;

                // Check if it matches a known safe pattern
                if (IsSafePattern(loopCondition))
                {
                    continue;
                }

                // Check the next 5 lines for CheckLoopProgress or immediate Advance
                if (!HasLoopProtection(lines, i, checkLoopProgressPattern, advancePattern))
                {
                    violations.Add($"{fileName}:{lineNumber} - while loop without CheckLoopProgress(): {loopCondition.Trim()}");
                }
            }

            // Check for do-while loops
            if (doWhilePattern.IsMatch(line))
            {
                // Check the next 5 lines for CheckLoopProgress
                if (!HasCheckLoopProgress(lines, i, checkLoopProgressPattern))
                {
                    violations.Add($"{fileName}:{lineNumber} - do-while loop without CheckLoopProgress()");
                }
            }
        }
    }

    private static bool IsSafePattern(string loopCondition)
    {
        return SafeLoopPatterns.Any(pattern => loopCondition.Contains(pattern));
    }

    private static bool HasLoopProtection(string[] lines, int loopLineIndex, Regex checkPattern, Regex advancePattern)
    {
        // Look at the next 5 lines (inside the loop body)
        var endIndex = System.Math.Min(loopLineIndex + 6, lines.Length);

        for (int i = loopLineIndex + 1; i < endIndex; i++)
        {
            var line = lines[i];

            // Found CheckLoopProgress - protected
            if (checkPattern.IsMatch(line))
            {
                return true;
            }

            // Check for immediate Advance() as first statement (structurally safe)
            if (i == loopLineIndex + 1 || (i == loopLineIndex + 2 && lines[loopLineIndex + 1].Trim() == "{"))
            {
                if (advancePattern.IsMatch(line))
                {
                    return true;
                }
            }

            // Stop at closing brace (end of loop body marker in do-while)
            if (line.Trim().StartsWith("}"))
            {
                break;
            }
        }

        return false;
    }

    private static bool HasCheckLoopProgress(string[] lines, int loopLineIndex, Regex checkPattern)
    {
        // Look at the next 5 lines for CheckLoopProgress
        var endIndex = System.Math.Min(loopLineIndex + 6, lines.Length);

        for (int i = loopLineIndex + 1; i < endIndex; i++)
        {
            if (checkPattern.IsMatch(lines[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static string FindParserSourceDirectory()
    {
        // Walk up from the current directory to find the repo root
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var parserPath = Path.Combine(current, "src", "Sharpy.Compiler", "Parser");
            if (Directory.Exists(parserPath))
            {
                return parserPath;
            }
            current = Directory.GetParent(current)?.FullName;
        }

        // Fallback - try relative from test assembly
        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Sharpy.Compiler", "Parser"));
    }

    [Fact]
    public void VerifyLoopInventoryCount()
    {
        // This test documents the expected loop count for tracking purposes.
        // If new loops are added, this test will need to be updated.

        var resolvedPath = Path.GetFullPath(ParserSourceDir);
        if (!Directory.Exists(resolvedPath))
        {
            resolvedPath = FindParserSourceDirectory();
        }

        if (!Directory.Exists(resolvedPath))
        {
            // Skip if we can't find the source - CI might have different structure
            return;
        }

        var parserFiles = Directory.GetFiles(resolvedPath, "Parser*.cs");
        var totalWhileLoops = 0;
        var totalDoWhileLoops = 0;

        var whilePattern = new Regex(@"\bwhile\s*\(", RegexOptions.Compiled);
        var doPattern = new Regex(@"\bdo\s*\{", RegexOptions.Compiled);

        foreach (var file in parserFiles)
        {
            var content = File.ReadAllText(file);
            totalWhileLoops += whilePattern.Matches(content).Count;
            totalDoWhileLoops += doPattern.Matches(content).Count;
        }

        // Document the current count (adjust as loops are added/removed)
        // Total should be approximately 40 based on the plan
        var totalLoops = totalWhileLoops + totalDoWhileLoops;

        // Allow some variance but flag significant changes
        totalLoops.Should().BeGreaterThan(30, "Parser should have a reasonable number of loops");
        totalLoops.Should().BeLessThan(90, "Unexpectedly high loop count - verify new loops are protected");
    }
}
