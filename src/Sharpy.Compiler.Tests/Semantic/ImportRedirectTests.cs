using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Tests.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

public class ImportRedirectTests : IntegrationTestBase
{
    public ImportRedirectTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void FromTypingImport_ProducesSPY0310_NotSPY0300()
    {
        var result = CompileAndExecute("from typing import Optional\n\ndef main() -> None:\n    pass\n");
        Assert.False(result.Success);
        Assert.Contains(result.RawDiagnostics, d => d.Code == DiagnosticCodes.Semantic.TypingModuleRedirect);
        Assert.DoesNotContain(result.RawDiagnostics, d => d.Code == DiagnosticCodes.Semantic.ModuleNotFound);
    }

    [Fact]
    public void FromTypingImportUnknown_ProducesSPY0310_WithFallback()
    {
        var result = CompileAndExecute("from typing import FooBar\n\ndef main() -> None:\n    pass\n");
        Assert.False(result.Success);
        var redirect = Assert.Single(result.RawDiagnostics, d => d.Code == DiagnosticCodes.Semantic.TypingModuleRedirect);
        Assert.Contains("typing", redirect.Message);
    }

    [Fact]
    public void FromDataclassesImport_ProducesSPY0311_NotSPY0300()
    {
        var result = CompileAndExecute("from dataclasses import dataclass\n\ndef main() -> None:\n    pass\n");
        Assert.False(result.Success);
        Assert.Contains(result.RawDiagnostics, d => d.Code == DiagnosticCodes.Semantic.DataclassesModuleRedirect);
        Assert.DoesNotContain(result.RawDiagnostics, d => d.Code == DiagnosticCodes.Semantic.ModuleNotFound);
    }

    [Fact]
    public void ImportTyping_ProducesSPY0310()
    {
        var result = CompileAndExecute("import typing\n\ndef main() -> None:\n    pass\n");
        Assert.False(result.Success);
        Assert.Contains(result.RawDiagnostics, d => d.Code == DiagnosticCodes.Semantic.TypingModuleRedirect);
        Assert.DoesNotContain(result.RawDiagnostics, d => d.Code == DiagnosticCodes.Semantic.ModuleNotFound);
    }

    [Fact]
    public void ImportDataclasses_ProducesSPY0311()
    {
        var result = CompileAndExecute("import dataclasses\n\ndef main() -> None:\n    pass\n");
        Assert.False(result.Success);
        Assert.Contains(result.RawDiagnostics, d => d.Code == DiagnosticCodes.Semantic.DataclassesModuleRedirect);
        Assert.DoesNotContain(result.RawDiagnostics, d => d.Code == DiagnosticCodes.Semantic.ModuleNotFound);
    }

    [Fact]
    public void FromTypingImport_ErrorRecovery_NoCascadingErrors()
    {
        var result = CompileAndExecute("from typing import Optional\n\ndef main() -> None:\n    x: Optional = None\n");
        Assert.False(result.Success);
        Assert.DoesNotContain(result.RawDiagnostics,
            d => d.Code == DiagnosticCodes.Semantic.UndefinedVariable && d.Message.Contains("Optional"));
    }

    [Fact]
    public void FromTypingImportAliased_ProducesSPY0310()
    {
        var result = CompileAndExecute("from typing import Optional as Opt\n\ndef main() -> None:\n    pass\n");
        Assert.False(result.Success);
        Assert.Contains(result.RawDiagnostics, d => d.Code == DiagnosticCodes.Semantic.TypingModuleRedirect);
    }

    [Fact]
    public void FromTypingImportMultiple_ProducesMultipleSPY0310()
    {
        var result = CompileAndExecute("from typing import List, Dict, Optional\n\ndef main() -> None:\n    pass\n");
        Assert.False(result.Success);
        var redirectCount = result.RawDiagnostics.Count(d => d.Code == DiagnosticCodes.Semantic.TypingModuleRedirect);
        Assert.Equal(3, redirectCount);
    }

    [Fact]
    public void ExistingValidImports_Unaffected()
    {
        var result = CompileAndExecute("def main() -> None:\n    print(\"hello\")\n");
        Assert.True(result.Success);
        Assert.Equal("hello\n", result.StandardOutput);
    }
}
