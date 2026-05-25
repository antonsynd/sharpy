using Xunit;
using FluentAssertions;

namespace Sharpy.Core.Tests;

public class Fnmatch_Tests
{
    // --- fnmatchcase (always case-sensitive) ---

    [Theory]
    [InlineData("foo.txt", "*.txt", true)]
    [InlineData("foo.py", "*.txt", false)]
    [InlineData("foo.TXT", "*.txt", false)]
    [InlineData("foo", "foo", true)]
    [InlineData("foo", "f?o", true)]
    [InlineData("fo", "f?o", false)]
    [InlineData("fooo", "f?o", false)]
    public void FnMatchCase_BasicPatterns(string name, string pat, bool expected)
    {
        Sharpy.FnmatchModule.Fnmatchcase(name, pat).Should().Be(expected);
    }

    [Theory]
    [InlineData("foo", "f[oa]o", true)]
    [InlineData("fbo", "f[oa]o", false)]
    public void FnMatchCase_CharacterClass(string name, string pat, bool expected)
    {
        Sharpy.FnmatchModule.Fnmatchcase(name, pat).Should().Be(expected);
    }

    [Theory]
    [InlineData("fxo", "f[!ab]o", true)]
    [InlineData("fao", "f[!ab]o", false)]
    [InlineData("fbo", "f[!ab]o", false)]
    public void FnMatchCase_NegatedCharacterClass(string name, string pat, bool expected)
    {
        Sharpy.FnmatchModule.Fnmatchcase(name, pat).Should().Be(expected);
    }

    [Fact]
    public void FnMatchCase_Wildcard_MatchesAnything()
    {
        Sharpy.FnmatchModule.Fnmatchcase("anything", "*").Should().BeTrue();
        Sharpy.FnmatchModule.Fnmatchcase("", "*").Should().BeTrue();
    }

    [Fact]
    public void FnMatchCase_QuestionMark_MatchesSingleChar()
    {
        Sharpy.FnmatchModule.Fnmatchcase("a", "?").Should().BeTrue();
        Sharpy.FnmatchModule.Fnmatchcase("", "?").Should().BeFalse();
        Sharpy.FnmatchModule.Fnmatchcase("ab", "?").Should().BeFalse();
    }

    [Fact]
    public void FnMatchCase_SpecialCharsEscaped()
    {
        Sharpy.FnmatchModule.Fnmatchcase("file.txt", "file.txt").Should().BeTrue();
        Sharpy.FnmatchModule.Fnmatchcase("fileatxt", "file.txt").Should().BeFalse();
    }

    [Fact]
    public void FnMatchCase_CaseSensitive()
    {
        Sharpy.FnmatchModule.Fnmatchcase("FOO.TXT", "*.txt").Should().BeFalse();
        Sharpy.FnmatchModule.Fnmatchcase("FOO.TXT", "*.TXT").Should().BeTrue();
    }

    // --- fnmatch (platform-dependent case sensitivity) ---

    [Fact]
    public void FnMatch_CaseSensitiveOnUnix()
    {
        if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
        {
            Sharpy.FnmatchModule.Fnmatch("FOO.TXT", "*.txt").Should().BeFalse();
        }
    }

    [Fact]
    public void FnMatch_BasicMatch()
    {
        Sharpy.FnmatchModule.Fnmatch("foo.txt", "*.txt").Should().BeTrue();
        Sharpy.FnmatchModule.Fnmatch("foo.py", "*.txt").Should().BeFalse();
    }

    // --- filter ---

    [Fact]
    public void Filter_ReturnsMatchingNames()
    {
        var names = new Sharpy.List<string>(
            new System.Collections.Generic.List<string> { "foo.txt", "bar.py", "baz.txt" });
        var result = Sharpy.FnmatchModule.Filter(names, "*.txt");
        result.Should().HaveCount(2);
        result[0].Should().Be("foo.txt");
        result[1].Should().Be("baz.txt");
    }

    [Fact]
    public void Filter_NoMatches_ReturnsEmptyList()
    {
        var names = new Sharpy.List<string>(
            new System.Collections.Generic.List<string> { "foo.py", "bar.py" });
        var result = Sharpy.FnmatchModule.Filter(names, "*.txt");
        result.Should().HaveCount(0);
    }

    [Fact]
    public void Filter_EmptyList_ReturnsEmptyList()
    {
        var names = new Sharpy.List<string>(new System.Collections.Generic.List<string>());
        var result = Sharpy.FnmatchModule.Filter(names, "*");
        result.Should().HaveCount(0);
    }

    // --- translate ---

    [Fact]
    public void Translate_Star_ToRegex()
    {
        var result = Sharpy.FnmatchModule.Translate("*.txt");
        result.Should().Be("\\A(?s:.*\\.txt)\\Z");
    }

    [Fact]
    public void Translate_QuestionMark_ToRegex()
    {
        var result = Sharpy.FnmatchModule.Translate("?.txt");
        result.Should().Be("\\A(?s:.\\.txt)\\Z");
    }

    [Fact]
    public void Translate_CharacterClass_ToRegex()
    {
        var result = Sharpy.FnmatchModule.Translate("[abc]");
        result.Should().Be("\\A(?s:[abc])\\Z");
    }

    [Fact]
    public void Translate_NegatedClass_ToRegex()
    {
        var result = Sharpy.FnmatchModule.Translate("[!abc]");
        result.Should().Be("\\A(?s:[^abc])\\Z");
    }

    [Fact]
    public void Translate_UnclosedBracket_TreatedAsLiteral()
    {
        var result = Sharpy.FnmatchModule.Translate("[abc");
        result.Should().Contain("\\[");
    }

    [Fact]
    public void Translate_StarOnly()
    {
        var result = Sharpy.FnmatchModule.Translate("*");
        result.Should().Be("\\A(?s:.*)\\Z");
    }
}
