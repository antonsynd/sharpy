using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Post-processes generated C# source to:
/// 1. Fix up charOffset in enhanced #line directives (set to actual indentation)
/// 2. Insert #line hidden for multi-line C# constructs between #line directives
/// </summary>
internal static class LineDirectivePostProcessor
{
    private static readonly Regex EnhancedDirectiveRegex = new(
        @"^(\s*)#line \((\d+), (\d+)\) - \((\d+), (\d+)\) 1 (""[^""]*"")$",
        RegexOptions.Compiled);

    private static readonly Regex AnyLineDirectiveRegex = new(
        @"^\s*#line\b",
        RegexOptions.Compiled);

    public static string Process(string csharpCode)
    {
        var newline = csharpCode.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = csharpCode.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        var result = new StringBuilder(csharpCode.Length + 256);

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var enhancedMatch = EnhancedDirectiveRegex.Match(line);

            if (enhancedMatch.Success)
            {
                int charOffset = 1;
                if (i + 1 < lines.Length)
                {
                    charOffset = Math.Max(1, CountLeadingSpaces(lines[i + 1]));
                }

                result.Append(enhancedMatch.Groups[1].Value);
                result.Append("#line (");
                result.Append(enhancedMatch.Groups[2].Value);
                result.Append(", ");
                result.Append(enhancedMatch.Groups[3].Value);
                result.Append(") - (");
                result.Append(enhancedMatch.Groups[4].Value);
                result.Append(", ");
                result.Append(enhancedMatch.Groups[5].Value);
                result.Append(") ");
                result.Append(charOffset);
                result.Append(' ');
                result.Append(enhancedMatch.Groups[6].Value);
                result.Append(newline);

                if (i + 1 < lines.Length)
                {
                    result.Append(lines[i + 1]);
                    result.Append(newline);
                    i++;

                    if (NeedsLineHidden(lines, i, out int nextDirectiveIndex))
                    {
                        var indent = enhancedMatch.Groups[1].Value;
                        result.Append(indent);
                        result.Append("#line hidden");
                        result.Append(newline);

                        for (int j = i + 1; j < nextDirectiveIndex; j++)
                        {
                            result.Append(lines[j]);
                            result.Append(newline);
                        }
                        i = nextDirectiveIndex - 1;
                    }
                }
            }
            else
            {
                result.Append(line);
                if (i < lines.Length - 1)
                    result.Append(newline);
            }
        }

        return result.ToString();
    }

    private static bool NeedsLineHidden(string[] lines, int codeLineIndex, out int nextDirectiveIndex)
    {
        nextDirectiveIndex = codeLineIndex + 1;
        int codeLineCount = 0;

        for (int j = codeLineIndex + 1; j < lines.Length; j++)
        {
            if (AnyLineDirectiveRegex.IsMatch(lines[j]))
            {
                nextDirectiveIndex = j;
                return codeLineCount > 0;
            }

            if (!string.IsNullOrWhiteSpace(lines[j]))
            {
                codeLineCount++;
            }
        }

        nextDirectiveIndex = lines.Length;
        return codeLineCount > 0;
    }

    private static int CountLeadingSpaces(string line)
    {
        int count = 0;
        foreach (char c in line)
        {
            if (c == ' ')
                count++;
            else if (c == '\t')
                count += 4;
            else
                break;
        }
        return count;
    }
}
