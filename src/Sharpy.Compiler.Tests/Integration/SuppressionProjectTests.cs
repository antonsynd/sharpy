using System;
using System.Linq;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Project-mode coverage for scoped @suppress (#1024): per-file suppression must survive the
/// multi-file pipeline, and behavior must be identical when warnings are promoted to errors
/// (&lt;WarningsAsErrors&gt; stamps the original severity so promoted warnings stay suppressible).
/// </summary>
[Collection("HeavyCompilation")]
public class SuppressionProjectTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private ProjectCompilationHelper? _helper;

    public SuppressionProjectTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private ProjectCompilationHelper CreateHelper()
    {
        _helper = new ProjectCompilationHelper(_output);
        return _helper;
    }

    public void Dispose()
    {
        _helper?.Dispose();
    }

    private const string LibWithSuppress = @"
def risky() -> int:
    return 5

def poke() -> None:
    @suppress(""SPY0480"")
    try risky()
";

    private const string LibWithoutSuppress = @"
def risky() -> int:
    return 5

def poke() -> None:
    try risky()
";

    private const string Main = @"
from lib import poke

def main():
    poke()
";

    [Fact]
    public void Suppress_ProjectMode_SilencesWarningInOwningFile()
    {
        var helper = CreateHelper();
        helper.WithRootNamespace("SuppressProjectTest");
        helper.AddSourceFile("lib.spy", LibWithSuppress);
        helper.AddSourceFile("main.spy", Main);
        helper.WithEntryPoint("main.spy");

        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
        Assert.DoesNotContain(result.Diagnostics.GetAll(),
            d => d.Code == DiagnosticCodes.Validation.MustUseValueDiscarded);
    }

    [Fact]
    public void Suppress_WithWarningsAsErrors_StillSuppresses()
    {
        var helper = CreateHelper();
        helper.WithRootNamespace("SuppressWerrorTest");
        helper.Options.WarningsAsErrors = true;
        helper.AddSourceFile("lib.spy", LibWithSuppress);
        helper.AddSourceFile("main.spy", Main);
        helper.WithEntryPoint("main.spy");

        var result = helper.Compile();

        Assert.True(result.Success, $"Compilation failed: {string.Join(", ", result.Diagnostics.GetErrors().Select(d => d.Message))}");
        Assert.DoesNotContain(result.Diagnostics.GetAll(),
            d => d.Code == DiagnosticCodes.Validation.MustUseValueDiscarded);
    }

    [Fact]
    public void NoSuppress_WithWarningsAsErrors_FailsOnPromotedWarning()
    {
        var helper = CreateHelper();
        helper.WithRootNamespace("NoSuppressWerrorTest");
        helper.Options.WarningsAsErrors = true;
        helper.AddSourceFile("lib.spy", LibWithoutSuppress);
        helper.AddSourceFile("main.spy", Main);
        helper.WithEntryPoint("main.spy");

        var result = helper.Compile();

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics.GetErrors(),
            d => d.Code == DiagnosticCodes.Validation.MustUseValueDiscarded);
    }
}
