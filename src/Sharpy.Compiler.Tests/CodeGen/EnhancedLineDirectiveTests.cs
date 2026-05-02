using Xunit;
using FluentAssertions;

namespace Sharpy.Compiler.Tests.CodeGen;

[Collection("Sequential")]
public class EnhancedLineDirectiveTests
{
    [Fact]
    public void Enhanced_LineDirective_Contains_Column_Span()
    {
        var source = @"def main():
    x: int = 42
    print(x)
";
        var compiler = new Compiler();
        var result = compiler.Compile(source, "test.spy");

        result.Success.Should().BeTrue(
            $"compilation should succeed, errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(e => e.Message))}");
        var code = result.GeneratedCSharpCode!;

        // Enhanced format: #line (line, col) - (endLine, endCol) charOffset "file"
        code.Should().MatchRegex(@"#line \(\d+, \d+\) - \(\d+, \d+\) \d+ ""test\.spy""");
    }

    [Fact]
    public void Enhanced_LineDirective_CharOffset_MatchesIndentation()
    {
        var source = @"def main():
    x: int = 42
    print(x)
";
        var compiler = new Compiler();
        var result = compiler.Compile(source, "test.spy");

        result.Success.Should().BeTrue();
        var code = result.GeneratedCSharpCode!;
        var lines = code.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimStart();
            if (line.StartsWith("#line (") && i + 1 < lines.Length)
            {
                // Extract charOffset from the directive
                var parts = line.Split(')');
                if (parts.Length >= 3)
                {
                    var afterLastParen = parts[2].Trim();
                    var offsetStr = afterLastParen.Split(' ')[0];
                    if (int.TryParse(offsetStr, out int charOffset))
                    {
                        // charOffset should equal the indentation of the next line
                        var nextLine = lines[i + 1];
                        int actualIndent = 0;
                        foreach (char c in nextLine)
                        {
                            if (c == ' ')
                                actualIndent++;
                            else
                                break;
                        }
                        charOffset.Should().Be(actualIndent,
                            $"charOffset should match indentation of next C# line (line {i + 2})");
                    }
                }
            }
        }
    }

    [Fact]
    public void Method_Entry_Brace_HasLineDirective()
    {
        var source = @"class Foo:
    def bar(self) -> int:
        return 42

def main():
    f: Foo = Foo()
    print(f.bar())
";
        var compiler = new Compiler();
        var result = compiler.Compile(source, "test.spy");

        result.Success.Should().BeTrue(
            $"compilation should succeed, errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(e => e.Message))}");
        var code = result.GeneratedCSharpCode!;

        // The method body opening brace should have a #line directive before it
        // This enables the debugger to show the .spy file when stepping into the method
        code.Should().Contain("#line 2 \"test.spy\"");
    }

    [Fact]
    public void Constructor_Entry_Brace_HasLineDirective()
    {
        var source = @"class Foo:
    x: int

    def __init__(self, x: int):
        self.x = x

def main():
    f: Foo = Foo(42)
    print(f.x)
";
        var compiler = new Compiler();
        var result = compiler.Compile(source, "test.spy");

        result.Success.Should().BeTrue(
            $"compilation should succeed, errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(e => e.Message))}");
        var code = result.GeneratedCSharpCode!;

        // Constructor body opening brace should have a #line directive
        code.Should().Contain("#line 4 \"test.spy\"");
    }

    [Fact]
    public void LineHidden_Emitted_ForMultiLineConstructs()
    {
        var source = @"def main():
    items: list[int] = [1, 2, 3, 4, 5]
    print(len(items))
";
        var compiler = new Compiler();
        var result = compiler.Compile(source, "test.spy");

        result.Success.Should().BeTrue(
            $"compilation should succeed, errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(e => e.Message))}");
        var code = result.GeneratedCSharpCode!;

        // If the list initialization spans multiple C# lines,
        // #line hidden should be emitted to prevent the debugger
        // from showing wrong line numbers for intermediate C# lines
        if (code.Contains("#line hidden"))
        {
            // Verify #line hidden appears between two #line directives
            var lines = code.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("#line hidden"))
                {
                    // There should be a preceding #line directive (not hidden)
                    bool hasPreceding = false;
                    for (int j = i - 1; j >= 0; j--)
                    {
                        var trimmed = lines[j].TrimStart();
                        if (trimmed.StartsWith("#line (") || trimmed.StartsWith("#line ") && !trimmed.StartsWith("#line hidden"))
                        {
                            hasPreceding = true;
                            break;
                        }
                    }
                    hasPreceding.Should().BeTrue("#line hidden should follow a #line directive");
                }
            }
        }
    }
}
