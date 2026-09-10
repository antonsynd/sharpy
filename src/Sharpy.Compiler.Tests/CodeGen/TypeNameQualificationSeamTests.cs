using Xunit;
using Xunit.Abstractions;
using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// #1139/#1146/#1765: type-name qualification lives in <c>TypeSyntaxMapper.QualifyFromSymbol</c>,
/// which both seams call. Every CLR-backed type is emitted <c>global::</c>-qualified from the
/// reflected <c>System.Type</c> so no <c>using</c> set can make it ambiguous.
/// </summary>
[Collection("HeavyCompilation")]
public class TypeNameQualificationSeamTests : IntegrationTestBase
{
    public TypeNameQualificationSeamTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void RawBclGeneric_AnnotationAndConstruction_BothQualifyFromClrName()
    {
        var result = CompileAndExecute(@"
from system.collections.generic import List

def main() -> None:
    xs: List[int] = List[int]()
    xs.add(1)
    print(xs.count)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.NotNull(result.GeneratedCSharp);

        // Both positions must produce global::-qualified CLR names (#1765).
        Assert.Contains("global::System.Collections.Generic.List<int> xs", result.GeneratedCSharp);
        Assert.Contains("new global::System.Collections.Generic.List<int>()", result.GeneratedCSharp);
        Assert.DoesNotContain("new List<int>()", result.GeneratedCSharp);
    }

    [Fact]
    public void RawBclGeneric_ModuleAliasedImport_QualifiesThroughTheSameSeam()
    {
        var result = CompileAndExecute(@"
import system.collections.generic as scg

def main() -> None:
    xs: scg.List[int] = scg.List[int]()
    xs.add(2)
    print(xs.count)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.NotNull(result.GeneratedCSharp);
        Assert.Contains("global::System.Collections.Generic.List<int> xs", result.GeneratedCSharp);
        Assert.Contains("new global::System.Collections.Generic.List<int>()", result.GeneratedCSharp);
        Assert.DoesNotContain("new List<int>()", result.GeneratedCSharp);
    }

    [Fact]
    public void CurrentFileType_IsInstance_KeepsShortName()
    {
        var result = CompileAndExecute(@"
class Box:
    n: int

    def __init__(self) -> None:
        self.n = 1

def main() -> None:
    b = Box()
    print(str(isinstance(b, Box)))
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.NotNull(result.GeneratedCSharp);
        Assert.Contains("new Box()", result.GeneratedCSharp);
        Assert.Contains("b is Box", result.GeneratedCSharp);
    }

    [Fact]
    public void ExceptionType_GlobalQualified()
    {
        // Exception types from the builtin registry must also be global::-qualified (#1765).
        var result = CompileAndExecute(@"
def main() -> None:
    try:
        raise Exception(""boom"")
    except Exception as e:
        print(str(e))
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.NotNull(result.GeneratedCSharp);
        Assert.Contains("global::System.Exception", result.GeneratedCSharp);
    }
}
