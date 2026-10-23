using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

/// <summary>
/// #2348: every Core <c>str</c> operation whose notion of whitespace is Python's
/// (<c>isspace</c>, no-argument <c>split</c>/<c>rsplit</c>, no-argument
/// <c>strip</c>/<c>lstrip</c>/<c>rstrip</c>) agrees with CPython on all 65,536 UTF-16
/// code units. No astral code point is whitespace, so the BMP is the whole universe.
/// </summary>
public class StrWhitespaceTableTests
{
    // python3 -c "print([hex(c) for c in range(0x10000) if chr(c).isspace()])"   (3.12)
    private static readonly HashSet<int> PythonSpaces = new()
    {
        0x9, 0xa, 0xb, 0xc, 0xd, 0x1c, 0x1d, 0x1e, 0x1f, 0x20, 0x85, 0xa0, 0x1680,
        0x2000, 0x2001, 0x2002, 0x2003, 0x2004, 0x2005, 0x2006, 0x2007, 0x2008, 0x2009, 0x200a,
        0x2028, 0x2029, 0x202f, 0x205f, 0x3000,
    };

    private static IEnumerable<char> EveryCodeUnit() =>
        Enumerable.Range(0, 0x10000).Select(c => (char)c);

    private static string Describe(char c) => $"U+{(int)c:X4}";

    [Fact]
    public void Isspace_MatchesPythonOnEveryCodeUnit()
    {
        var mismatches = EveryCodeUnit()
            .Where(c => c.ToString().Isspace() != PythonSpaces.Contains(c))
            .Select(Describe)
            .ToList();

        mismatches.Should().BeEmpty("str.isspace must be CPython's predicate on every code unit");
    }

    [Fact]
    public void SplitAndStrip_UsePythonsWhitespaceOnEveryCodeUnit()
    {
        var mismatches = new List<string>();
        foreach (var c in EveryCodeUnit())
        {
            bool space = PythonSpaces.Contains(c);
            string inner = "a" + c + "b";
            string wrapped = c + "a" + c;

            if (((ISized)StringExtensions.Split(inner)).Count != (space ? 2 : 1))
                mismatches.Add($"{Describe(c)} split()");
            if (((ISized)StringExtensions.Rsplit(inner)).Count != (space ? 2 : 1))
                mismatches.Add($"{Describe(c)} rsplit()");
            if (wrapped.Strip() != (space ? "a" : wrapped))
                mismatches.Add($"{Describe(c)} strip()");
            if (wrapped.Lstrip() != (space ? "a" + c : wrapped))
                mismatches.Add($"{Describe(c)} lstrip()");
            if (wrapped.Rstrip() != (space ? c + "a" : wrapped))
                mismatches.Add($"{Describe(c)} rstrip()");
        }

        mismatches.Should().BeEmpty("no-argument split/strip must strip exactly CPython's whitespace");
    }

    [Theory]
    // python3: "\x1c".isspace() -> True; "a\x1cb\x1fc".split() -> ['a', 'b', 'c']; "\x1cab\x1f".strip() -> 'ab'
    [InlineData("a\u001Cb\u001Fc", new[] { "a", "b", "c" })]
    [InlineData("  a b\u0085", new[] { "a", "b" })]
    public void Split_IssueCells(string s, string[] expected)
    {
        StringExtensions.Split(s).Should().Equal(expected);
        "\u001Cab\u001F".Strip().Should().Be("ab");
        "\u001F".Isspace().Should().BeTrue();
    }

    // A whitespace test spelled with .NET's predicate inside the str method surface.
    private static readonly Regex DotNetWhitespace =
        new(@"char\.IsWhiteSpace\(|\.Trim(Start|End)?\(\s*\)", RegexOptions.Compiled);

    [Fact]
    public void Census_NoStrMethodTestsWhitespaceWithDotNetsPredicate()
    {
        // Positive control: the scan recognises every spelling it bans.
        DotNetWhitespace.IsMatch("if (!char.IsWhiteSpace(c))").Should().BeTrue();
        DotNetWhitespace.IsMatch("=> s.Trim();").Should().BeTrue();
        DotNetWhitespace.IsMatch("=> s.TrimStart( );").Should().BeTrue();
        DotNetWhitespace.IsMatch("return s.TrimEnd(chars.ToCharArray());").Should().BeFalse();

        var files = Directory.GetFiles(Path.Combine(FindRepositoryRoot(), "src", "Sharpy.Core"), "StringExtensions*.cs");
        files.Should().NotBeEmpty("the census must scan the str method surface");

        var violations = new List<string>();
        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var code = lines[i];
                var comment = code.IndexOf("//", StringComparison.Ordinal);
                if (comment >= 0)
                    code = code.Substring(0, comment);
                if (DotNetWhitespace.IsMatch(code))
                    violations.Add($"{Path.GetFileName(file)}:{i + 1}: {lines[i].Trim()}");
            }
        }

        violations.Should().BeEmpty("every str whitespace site reads StringHelpers.IsPythonSpace (#2348)");
    }

    private static string FindRepositoryRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            if (File.Exists(Path.Combine(current, "src", "Sharpy.Core", "StringHelpers.cs")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException("repository root not found from " + AppContext.BaseDirectory);
    }
}
