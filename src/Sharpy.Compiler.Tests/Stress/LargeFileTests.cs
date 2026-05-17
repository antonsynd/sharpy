using System.Text;
using FluentAssertions;
using Sharpy.Compiler.Tests.Helpers;
using Sharpy.Compiler.Tests.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Stress;

/// <summary>
/// Stress tests for large-scale compiler inputs (Phase 6.1).
/// These tests verify the compiler handles extreme inputs without crashing or timing out.
/// </summary>
[Collection("HeavyCompilation")]
public class LargeFileTests : IntegrationTestBase
{
    public LargeFileTests(ITestOutputHelper output) : base(output) { }

    private static string GetErrorMessage(ExecutionResult result)
    {
        var parts = new List<string>();
        if (result.CompilationErrors.Count > 0)
            parts.Add("Compilation: " + string.Join("; ", result.CompilationErrors.Take(5)));
        if (!string.IsNullOrWhiteSpace(result.StandardError))
            parts.Add("Stderr: " + result.StandardError);
        return string.Join(" | ", parts);
    }

    [Fact]
    public void Handles_LargeFile_5000Variables()
    {
        // Note: Reduced from 10,000 to 5,000 for reasonable test runtime
        var sb = new StringBuilder();
        sb.AppendLine("def main():");
        for (int i = 0; i < 5000; i++)
        {
            sb.AppendLine($"    x{i}: int = {i}");
        }
        sb.AppendLine("    print(x4999)");

        var result = CompileAndExecute(sb.ToString());
        result.Success.Should().BeTrue(GetErrorMessage(result));
        result.StandardOutput.Should().Be("4999\n");
    }

    [Fact]
    public void Handles_DeeplyNestedExpressions_100Levels()
    {
        // (((((...1...)))))
        var expr = "1";
        for (int i = 0; i < 100; i++)
        {
            expr = $"({expr})";
        }

        var source = $@"
def main():
    x = {expr}
    print(x)
";

        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(GetErrorMessage(result));
        result.StandardOutput.Should().Be("1\n");
    }

    [Fact]
    public void Handles_DeeplyNestedBlocks_50Levels()
    {
        var sb = new StringBuilder();
        sb.AppendLine("def main():");
        var indent = "    ";

        for (int i = 0; i < 50; i++)
        {
            sb.AppendLine($"{indent}if True:");
            indent += "    ";
        }

        sb.AppendLine($"{indent}print(\"deep\")");

        var result = CompileAndExecute(sb.ToString());
        result.Success.Should().BeTrue(GetErrorMessage(result));
        result.StandardOutput.Should().Be("deep\n");
    }

    [Fact]
    public void Handles_ManyFunctions_500()
    {
        // Note: Reduced from 1,000 to 500 for reasonable test runtime
        var sb = new StringBuilder();
        for (int i = 0; i < 500; i++)
        {
            sb.AppendLine($"def func{i}() -> int:");
            sb.AppendLine($"    return {i}");
            sb.AppendLine();
        }

        sb.AppendLine("def main():");
        sb.AppendLine("    print(func499())");

        var result = CompileAndExecute(sb.ToString());
        result.Success.Should().BeTrue(GetErrorMessage(result));
        result.StandardOutput.Should().Be("499\n");
    }

    [Fact]
    public void Handles_ManyClasses_200()
    {
        // Note: Reduced from 500 to 200 for reasonable test runtime
        var sb = new StringBuilder();
        for (int i = 0; i < 200; i++)
        {
            sb.AppendLine($"class Class{i}:");
            sb.AppendLine($"    value: int = {i}");
            sb.AppendLine();
        }

        sb.AppendLine("def main():");
        sb.AppendLine("    obj = Class199()");
        sb.AppendLine("    print(obj.value)");

        var result = CompileAndExecute(sb.ToString());
        result.Success.Should().BeTrue(GetErrorMessage(result));
        result.StandardOutput.Should().Be("199\n");
    }

    [Fact]
    public void Handles_ManyImports_50Files()
    {
        // Note: Reduced from 100 to 50 for reasonable test runtime
        using var helper = new ProjectCompilationHelper(Output);

        // Create 50 library files
        for (int i = 0; i < 50; i++)
        {
            helper.AddSourceFile($"lib{i}.spy", $"VALUE_{i}: int = {i}");
        }

        // Create main that imports all
        var sb = new StringBuilder();
        for (int i = 0; i < 50; i++)
        {
            sb.AppendLine($"from lib{i} import VALUE_{i}");
        }
        sb.AppendLine();
        sb.AppendLine("def main():");
        sb.AppendLine("    total = 0");
        for (int i = 0; i < 50; i++)
        {
            sb.AppendLine($"    total += VALUE_{i}");
        }
        sb.AppendLine("    print(total)");

        helper.AddSourceFile("main.spy", sb.ToString());
        helper.CreateProjectFile();

        var result = helper.CompileAndExecute();
        var errorMsg = result.CompilationErrors.Count > 0
            ? "Compilation: " + string.Join("; ", result.CompilationErrors.Take(5))
            : result.StandardError;
        result.Success.Should().BeTrue(errorMsg);
        // Sum of 0..49 = 1225
        result.StandardOutput.Should().Be("1225\n");
    }

    [Fact]
    public void Handles_DeeplyNestedMethodCalls_30Levels()
    {
        var sb = new StringBuilder();

        // Create a chain of functions that call each other
        for (int i = 0; i < 30; i++)
        {
            sb.AppendLine($"def f{i}(n: int) -> int:");
            if (i == 29)
            {
                sb.AppendLine("    return n");
            }
            else
            {
                sb.AppendLine($"    return f{i + 1}(n + 1)");
            }
            sb.AppendLine();
        }

        sb.AppendLine("def main():");
        sb.AppendLine("    result = f0(0)");
        sb.AppendLine("    print(result)");

        var result = CompileAndExecute(sb.ToString());
        result.Success.Should().BeTrue(GetErrorMessage(result));
        result.StandardOutput.Should().Be("29\n");
    }

    [Fact]
    public void Handles_LargeListLiteral_1000Elements()
    {
        var elements = string.Join(", ", Enumerable.Range(0, 1000));
        var source = $@"
def main():
    data: list[int] = [{elements}]
    print(len(data))
    print(data[999])
";

        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(GetErrorMessage(result));
        result.StandardOutput.Should().Be("1000\n999\n");
    }

    [Fact]
    public void Handles_LargeDictLiteral_500Elements()
    {
        var elements = string.Join(", ", Enumerable.Range(0, 500).Select(i => $"\"{i}\": {i}"));
        var source = $@"
def main():
    data: dict[str, int] = {{{elements}}}
    print(len(data))
    print(data[""499""])
";

        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(GetErrorMessage(result));
        result.StandardOutput.Should().Be("500\n499\n");
    }

    [Fact]
    public void Handles_LongExpressionChain_100Operators()
    {
        // 1 + 2 + 3 + ... + 100
        var expr = string.Join(" + ", Enumerable.Range(1, 100));
        var source = $@"
def main():
    result = {expr}
    print(result)
";

        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(GetErrorMessage(result));
        // Sum of 1..100 = 5050
        result.StandardOutput.Should().Be("5050\n");
    }

    [Fact]
    public void Handles_ManyMethodsInClass_100Methods()
    {
        var sb = new StringBuilder();
        sb.AppendLine("class BigClass:");

        for (int i = 0; i < 100; i++)
        {
            sb.AppendLine($"    def method{i}(self) -> int:");
            sb.AppendLine($"        return {i}");
            sb.AppendLine();
        }

        sb.AppendLine("def main():");
        sb.AppendLine("    obj = BigClass()");
        sb.AppendLine("    print(obj.method99())");

        var result = CompileAndExecute(sb.ToString());
        result.Success.Should().BeTrue(GetErrorMessage(result));
        result.StandardOutput.Should().Be("99\n");
    }

    [Fact]
    public void Handles_DeepInheritanceChain_20Levels()
    {
        var sb = new StringBuilder();

        // Base class
        sb.AppendLine("class Base0:");
        sb.AppendLine("    depth: int = 0");
        sb.AppendLine();

        // 19 more classes in inheritance chain
        for (int i = 1; i < 20; i++)
        {
            sb.AppendLine($"class Base{i}(Base{i - 1}):");
            sb.AppendLine($"    depth: int = {i}");
            sb.AppendLine();
        }

        sb.AppendLine("def main():");
        sb.AppendLine("    obj = Base19()");
        sb.AppendLine("    print(obj.depth)");

        var result = CompileAndExecute(sb.ToString());
        result.Success.Should().BeTrue(GetErrorMessage(result));
        result.StandardOutput.Should().Be("19\n");
    }

    [Fact]
    public void Handles_ManyParameters_20Params()
    {
        var paramList = string.Join(", ", Enumerable.Range(0, 20).Select(i => $"p{i}: int"));
        var argList = string.Join(", ", Enumerable.Range(0, 20));
        var sumExpr = string.Join(" + ", Enumerable.Range(0, 20).Select(i => $"p{i}"));

        var source = $@"
def big_func({paramList}) -> int:
    return {sumExpr}

def main():
    result = big_func({argList})
    print(result)
";

        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(GetErrorMessage(result));
        // Sum of 0..19 = 190
        result.StandardOutput.Should().Be("190\n");
    }
}
