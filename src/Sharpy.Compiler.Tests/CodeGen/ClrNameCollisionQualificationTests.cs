using Xunit;
using Xunit.Abstractions;
using Sharpy.Compiler.Tests.Integration;
using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// #1765's original symptom (p03): a Sharpy stdlib type and a .NET type of the SAME short name in
/// one file. Separate from <see cref="TypeNameQualificationSeamTests"/> only because the cell needs
/// a stdlib module reference — <c>StdlibAwareIntegrationTestBase</c> — which neither
/// <c>IntegrationTestBase</c> nor the file-based fixtures provide ("Cannot find module 'pathlib'").
/// </summary>
[Collection("HeavyCompilation")]
public class ClrNameCollisionQualificationTests : StdlibAwareIntegrationTestBase
{
    public ClrNameCollisionQualificationTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void SharpyAndClrTypeOfTheSameName_InOneFile_QualifyWithoutAmbiguity()
    {
        // `Path` names both Sharpy's pathlib class and System.IO.Path, and this file was
        // `CS0104: 'Path' is an ambiguous reference between 'Sharpy.Path' and 'System.IO.Path'`
        // behind SPY0908 at 24ee7d6f6. The imported name binds the Sharpy class; the
        // module-qualified spelling reaches the .NET static class; qualification keeps them apart.
        var result = CompileAndExecute(@"
from pathlib import Path
import system.io

def main() -> None:
    p: Path = Path(""a"")
    print(str(p))
    print(system.io.Path.combine(""a"", ""b""))
    print(system.io.Path.get_extension(""x.txt""))
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("a\na/b\n.txt\n", result.StandardOutput.Replace("\r\n", "\n"));
        Assert.NotNull(result.GeneratedCSharp);
        Assert.Contains("global::System.IO.Path", result.GeneratedCSharp);
    }
}
