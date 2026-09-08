using System.Text.RegularExpressions;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Visibility matrix for dunder-synthesized interfaces (#1746, plan-499995 Design Decision 4):
/// the synthesized interface joins the supertype closure at INHERITANCE RESOLUTION, so the type
/// checker sees it (a `Bag` with <c>__len__</c> passes where an <c>ISized</c> is expected and
/// <c>isinstance</c> answers on a typed receiver), the emitted base list carries it EXACTLY once,
/// and an explicit base naming the same interface yields one entry, not two (the CS0528 the
/// range's first cut emitted). Rows: all seven dunder → interface mappings; hosts: class, struct,
/// generic <c>Box[T]</c>. Consumers that call <c>len()</c> on an interface-typed receiver are
/// deliberately absent — that is #1808 (SPY0320), excluded from this plan.
/// </summary>
[Collection("HeavyCompilation")]
public class SynthesizedInterfaceVisibilityTests : IntegrationTestBase
{
    public SynthesizedInterfaceVisibilityTests(ITestOutputHelper output) : base(output) { }

    private const int RowCount = 7;
    private const int VisibilityCellCount = 12;
    private const int OverlapCellCount = 3;

    /// <summary>(name, source, expected stdout, emitted base-list entry the class must carry exactly once)</summary>
    public static IEnumerable<object[]> VisibilityCells => new[]
    {
        new object[] { "Len_Class_ParameterAndIsinstance", @"class Bag:
    items: list[int]
    def __init__(self, items: list[int]) -> None:
        self.items = items
    def __len__(self) -> int:
        return len(self.items)

def size(s: ISized) -> str:
    return ""sized""

def main():
    b = Bag([1, 2, 3])
    print(size(b))
    print(isinstance(b, ISized))
    print(len(b))", "sized\nTrue\n3\n", "Sharpy.ISized" },

        new object[] { "Len_Struct_Parameter", @"struct Pair:
    a: int
    b: int
    def __len__(self) -> int:
        return 2

def size(s: ISized) -> str:
    return ""sized""

def main():
    p = Pair(1, 2)
    print(size(p))
    print(len(p))", "sized\n2\n", "Sharpy.ISized" },

        new object[] { "Bool_Class_Parameter", @"class Flag:
    on: bool
    def __init__(self, on: bool) -> None:
        self.on = on
    def __bool__(self) -> bool:
        return self.on

def truthy(b: IBoolConvertible) -> str:
    return ""convertible""

def main():
    f = Flag(True)
    print(truthy(f))
    print(bool(f))", "convertible\nTrue\n", "Sharpy.IBoolConvertible" },

        new object[] { "Bool_Struct_Parameter", @"struct Flag:
    on: bool
    def __bool__(self) -> bool:
        return self.on

def truthy(b: IBoolConvertible) -> str:
    return ""convertible""

def main():
    print(truthy(Flag(False)))
    print(bool(Flag(False)))", "convertible\nFalse\n", "Sharpy.IBoolConvertible" },

        new object[] { "Reversed_Class_ElementTypeFromAnnotation", @"class Countdown:
    n: int
    def __init__(self, n: int) -> None:
        self.n = n
    def __reversed__(self) -> int:
        yield self.n
        yield self.n - 1

def take(r: IReverseEnumerable[int]) -> str:
    return ""reversible""

def main():
    c = Countdown(5)
    print(take(c))
    for x in reversed(c):
        print(x)", "reversible\n5\n4\n", "Sharpy.IReverseEnumerable<int>" },

        new object[] { "Reversed_GenericBox_ElementIsT", @"class Box[T]:
    v: T
    def __init__(self, v: T) -> None:
        self.v = v
    def __reversed__(self) -> T:
        yield self.v

def main():
    b = Box[int](5)
    for x in reversed(b):
        print(x)", "5\n", "Sharpy.IReverseEnumerable<T>" },

        new object[] { "Next_Class_Enumerator_PassesAsImportedIEnumerator", @"from System.Collections.Generic import IEnumerator

class Counter:
    i: int
    def __init__(self) -> None:
        self.i = 0
    def __next__(self) -> int:
        self.i = self.i + 1
        if self.i > 2:
            raise StopIteration()
        return self.i

def take(e: IEnumerator[int]) -> str:
    return ""enumerator""

def main():
    print(take(Counter()))", "enumerator\n", "System.Collections.Generic.IEnumerator<int>" },

        new object[] { "IterAndNext_Class_EnumerableAndEnumerator", @"class Counter:
    i: int
    def __init__(self) -> None:
        self.i = 0
    def __iter__(self) -> Counter:
        return self
    def __next__(self) -> int:
        self.i = self.i + 1
        if self.i > 3:
            raise StopIteration()
        return self.i

def main():
    total: int = 0
    for x in Counter():
        total = total + x
    print(total)", "6\n", "System.Collections.Generic.IEnumerable<int>" },

        new object[] { "GeneratorIter_Class_Enumerable", @"class Evens:
    def __iter__(self) -> int:
        yield 2
        yield 4

def main():
    total: int = 0
    for x in Evens():
        total = total + x
    print(total)", "6\n", "System.Collections.Generic.IEnumerable<int>" },

        new object[] { "Eq_Class_IEquatable", @"class P:
    v: int
    def __init__(self, v: int) -> None:
        self.v = v
    def __eq__(self, other: P) -> bool:
        return self.v == other.v
    def __eq__(self, other: object) -> bool:
        return isinstance(other, P) and self.v == other.v
    def __hash__(self) -> int:
        return self.v

def main():
    print(P(1) == P(1))
    print(P(1) == P(2))", "True\nFalse\n", "System.IEquatable<P>" },

        new object[] { "Eq_Struct_IEquatable", @"struct Q:
    v: int
    def __eq__(self, other: Q) -> bool:
        return self.v == other.v
    def __eq__(self, other: object) -> bool:
        return isinstance(other, Q) and self.v == other.v
    def __hash__(self) -> int:
        return self.v

def main():
    print(Q(1) == Q(1))", "True\n", "System.IEquatable<Q>" },

        new object[] { "Eq_GenericBox_IEquatableOfT", @"class Box[T]:
    v: T
    def __init__(self, v: T) -> None:
        self.v = v
    def __eq__(self, other: T) -> bool:
        print(""called"")
        return True

def main():
    b = Box[int](1)
    print(b == 1)", "called\nTrue\n", "System.IEquatable<T>" },
    };

    [Fact]
    public void VisibilityCells_AreAnchored_AndCoverEveryRow()
    {
        VisibilityCells.Count().Should().Be(VisibilityCellCount);
        var rows = VisibilityCells.Select(c => ((string)c[3]).Split('<')[0].Split('.').Last()).Distinct().ToList();
        // ISized, IBoolConvertible, IReverseEnumerable, IEnumerator, IEnumerable, IEquatable — six
        // interfaces reached by the seven dunder rows (IEnumerable is reached by two rows).
        rows.Should().HaveCount(RowCount - 1);
    }

    [Theory]
    [MemberData(nameof(VisibilityCells))]
    public void SynthesizedInterface_IsVisibleToTheChecker_AndEmittedOnce(string name, string code, string expected, string baseListEntry)
    {
        var result = CompileAndExecute(code);
        result.Success.Should().BeTrue($"{name}: {string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message))}");
        result.StandardOutput.Should().Be(expected, name);
        result.RawDiagnostics.Should().Contain(d => d.Code == DiagnosticCodes.Info.ImplicitInterfaceSynthesis,
            $"{name}: synthesis is announced by SPY1001");

        var baseList = FirstTypeBaseList(result.GeneratedCSharp!);
        Regex.Matches(baseList, Regex.Escape(baseListEntry)).Count
            .Should().Be(1, $"{name}: the base list must carry {baseListEntry} exactly once; got: {baseList}");
    }

    /// <summary>(name, source, expected stdout, the interface that must appear exactly once)</summary>
    public static IEnumerable<object[]> OverlapCells => new[]
    {
        new object[] { "Explicit_ISized_Plus_Len", @"class Bag(ISized):
    items: list[int]
    def __init__(self, items: list[int]) -> None:
        self.items = items
    def __len__(self) -> int:
        return len(self.items)

def size(s: ISized) -> str:
    return ""sized""

def main():
    b = Bag([1])
    print(size(b))
    print(len(b))", "sized\n1\n", "ISized" },

        new object[] { "Explicit_ImportedIEquatable_Plus_SameEq", @"from system import IEquatable

class Foo(IEquatable[Foo]):
    v: int
    def __init__(self, v: int) -> None:
        self.v = v
    def __eq__(self, other: Foo) -> bool:
        return self.v == other.v
    def __eq__(self, other: object) -> bool:
        return isinstance(other, Foo) and self.v == other.v
    def __hash__(self) -> int:
        return self.v

def main():
    print(Foo(1) == Foo(1))", "True\n", "IEquatable<Foo>" },

        new object[] { "Explicit_ImportedIEquatable_Plus_SameEq_Struct", @"from system import IEquatable

struct Bar(IEquatable[Bar]):
    v: int
    def __eq__(self, other: Bar) -> bool:
        return self.v == other.v
    def __eq__(self, other: object) -> bool:
        return isinstance(other, Bar) and self.v == other.v
    def __hash__(self) -> int:
        return self.v

def main():
    print(Bar(2) == Bar(2))", "True\n", "IEquatable<Bar>" },
    };

    [Fact]
    public void OverlapCells_AreAnchored() => OverlapCells.Count().Should().Be(OverlapCellCount);

    [Theory]
    [MemberData(nameof(OverlapCells))]
    public void ExplicitPlusSynthesized_IsOneBaseListEntry(string name, string code, string expected, string interfaceEntry)
    {
        var result = CompileAndExecute(code);
        result.Success.Should().BeTrue($"{name}: {string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message))}");
        result.StandardOutput.Should().Be(expected, name);

        var baseList = FirstTypeBaseList(result.GeneratedCSharp!);
        Regex.Matches(baseList, Regex.Escape(interfaceEntry)).Count
            .Should().Be(1, $"{name}: one entry, never two (CS0528); got: {baseList}");
    }

    /// <summary>The base list of the first class/struct declaration in the emitted C#.</summary>
    private static string FirstTypeBaseList(string csharp)
    {
        var match = Regex.Match(csharp, @"(?:class|struct)\s+\w+(?:<[^>]*>)?\s*:\s*([^\r\n{]+)");
        match.Success.Should().BeTrue("the emitted C# must declare the type with a base list");
        return match.Groups[1].Value;
    }
}
