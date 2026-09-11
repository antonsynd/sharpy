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
    public void GenericClrType_BesideSharpyCollection_QualifiesTheClrNameInEveryPosition()
    {
        // #1765: 311252e33 exempted GENERIC types from qualification, so this file's annotation
        // emitted a bare `List<int>` (bound only by `using System.Collections.Generic`) while the
        // construction one line later emitted the global::-qualified name — the two-formula
        // divergence the seam exists to prevent. The cause was IEnumerable's registry entry naming
        // the NON-generic System.Collections.IEnumerable; the entry now names the open generic
        // definition and the exemption is gone.
        var result = CompileAndExecute(@"
from System.Collections.Generic import List

def count_clr(xs: List[int]) -> int:
    return xs.count

def main() -> None:
    clr: List[int] = List[int]()
    clr.add(7)
    print(count_clr(clr))
    sharpy: list[int] = [1, 2, 3]
    print(len(sharpy))
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("1\n3\n", result.StandardOutput.Replace("\r\n", "\n"));
        Assert.NotNull(result.GeneratedCSharp);

        // The PARAMETER position is the one the carve-out suppressed.
        Assert.Contains("CountClr(global::System.Collections.Generic.List<int> xs)", result.GeneratedCSharp);
        Assert.Contains("new global::System.Collections.Generic.List<int>()", result.GeneratedCSharp);

        // Sharpy's own collection keeps its short name (the Sharpy-namespace arm is unchanged),
        // which is also the positive control: the assertion above is not matching every generic.
        Assert.Contains("Sharpy.List<int> sharpy", result.GeneratedCSharp);
        Assert.DoesNotContain("global::Sharpy.List<int> sharpy", result.GeneratedCSharp);
    }

    [Fact]
    public void GenericSharpyMappedInterface_QualifiesFromTheGenericDefinition()
    {
        // IEnumerable is the entry that caused the carve-out: Sharpy maps the name to
        // System.Collections.Generic.IEnumerable<T>, and while the registry recorded the
        // non-generic System.Collections.IEnumerable, qualification produced
        // `System.Collections.IEnumerable<int>` (CS0308). Executing the program is what checks the
        // emitted name binds.
        var result = CompileAndExecute(@"
def total(xs: IEnumerable[int]) -> int:
    n: int = 0
    for x in xs:
        n = n + x
    return n

def main() -> None:
    print(total([1, 2, 3]))
");

        Assert.True(result.Success, string.Join("\n", result.CompilationErrors));
        Assert.Equal("6\n", result.StandardOutput.Replace("\r\n", "\n"));
        Assert.NotNull(result.GeneratedCSharp);
        Assert.Contains("global::System.Collections.Generic.IEnumerable<int>", result.GeneratedCSharp);
        Assert.DoesNotContain("System.Collections.IEnumerable<", result.GeneratedCSharp);
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
