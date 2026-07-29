using Xunit;
using Xunit.Abstractions;
using Sharpy.TestInfrastructure.Integration;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// #1139/#1146: type-name qualification used to exist as two textually independent copies — one in
/// <c>TypeSyntaxMapper</c> (annotation position), one in <c>RoslynEmitter</c> (construction position) —
/// and the raw-BCL rule below had to be hand-mirrored into both. It now lives only in
/// <c>TypeSyntaxMapper.QualifyFromSymbol</c>, which both seams call. These tests pin the rule at the
/// consolidated seam: a raw generic BCL type carries a ClrType but neither DefiningModule nor
/// DefiningFilePath, so without the rule it falls back to the bare short name and collides with
/// <c>Sharpy.List</c> (CS0104 → SPY0908). The fixtures cover execution; this covers the emitted names,
/// including that both positions agree.
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

        // Annotation position and construction position must produce the same qualified name.
        Assert.Contains("System.Collections.Generic.List<int> xs", result.GeneratedCSharp);
        Assert.Contains("new System.Collections.Generic.List<int>()", result.GeneratedCSharp);
        // The bare name is what collided with Sharpy.List.
        Assert.DoesNotContain("new List<int>()", result.GeneratedCSharp);
    }

    [Fact]
    public void RawBclGeneric_ModuleAliasedImport_QualifiesThroughTheSameSeam()
    {
        // The aliased form resolves the symbol by a different path (module-qualified name) but must
        // reach the same qualification rule.
        var result = CompileAndExecute(@"
import system.collections.generic as scg

def main() -> None:
    xs: scg.List[int] = scg.List[int]()
    xs.add(2)
    print(xs.count)
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.NotNull(result.GeneratedCSharp);
        Assert.Contains("System.Collections.Generic.List<int> xs", result.GeneratedCSharp);
        Assert.Contains("new System.Collections.Generic.List<int>()", result.GeneratedCSharp);
        Assert.DoesNotContain("new List<int>()", result.GeneratedCSharp);
    }

    [Fact]
    public void CurrentFileType_IsInstance_KeepsShortName()
    {
        // The construction position must keep the short name for a type declared in the file being
        // emitted. This is one of the branches where the two positions legitimately differ: the
        // reference position would derive a module namespace from the defining file path and qualify
        // it. The shared algorithm keeps that difference explicit, so this pins that consolidation did
        // not quietly adopt the reference behavior at an emitter call site.
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
}
