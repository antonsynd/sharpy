using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Closure matrix for SPY0607: a type that reaches a generic interface at two distinct
/// instantiations is refused; the same instantiation via multiple paths is accepted.
/// Sources: explicit, inherited, synthesized, transitive, diamond, CLR-exempt.
/// (#1717, R-H ruling)
/// </summary>
[Collection("HeavyCompilation")]
public class InterfaceInstantiationClosureMatrixTests : IntegrationTestBase
{
    public InterfaceInstantiationClosureMatrixTests(ITestOutputHelper output) : base(output) { }

    private const string SPY0607 = DiagnosticCodes.SemanticOverflow.ConflictingInterfaceInstantiation;

    public static IEnumerable<object[]> ConflictCases => new[]
    {
        new object[]
        {
            "ExplicitDirect_vs_ExplicitAncestor",
            @"interface IA[T]:
    def get(self) -> T: ...

class B(IA[int]):
    def get(self) -> int:
        return 1

class D(B, IA[str]):
    def get(self) -> str:
        return ""hello""

def main():
    pass"
        },
        new object[]
        {
            "SynthesizedDirect_vs_SynthesizedAncestor",
            @"class Base:
    def __eq__(self, other: str) -> bool:
        return False

class Derived(Base):
    def __eq__(self, other: int) -> bool:
        return False

def main():
    pass"
        },
        new object[]
        {
            "InterfaceDiamond_Distinct",
            @"interface IA[T]:
    def get(self) -> T: ...

interface IB(IA[int]):
    pass

interface IC(IA[str]):
    pass

class D(IB, IC):
    def get(self) -> int:
        return 1

def main():
    pass"
        },
        new object[]
        {
            "Transitive_ViaBaseChain",
            @"interface IA[T]:
    def get(self) -> T: ...

class C(IA[int]):
    def get(self) -> int:
        return 1

class B(C):
    pass

class D(B, IA[str]):
    def get(self) -> str:
        return ""hello""

def main():
    pass"
        },
        new object[]
        {
            "IteratorConflict_Synthesized",
            @"class StringIterator:
    _items: list[str]
    _index: int

    def __init__(self, items: list[str]):
        self._items = items
        self._index = 0

    def __next__(self) -> str:
        if self._index < len(self._items):
            result: str = self._items[self._index]
            self._index = self._index + 1
            return result
        raise StopIteration()

    def __iter__(self) -> StringIterator:
        return self

class IntIterator(StringIterator):
    def __init__(self, items: list[str]):
        super().__init__(items)

    def __next__(self) -> int:
        return 0

    def __iter__(self) -> IntIterator:
        return self

def main():
    pass"
        },
    };

    [Theory]
    [MemberData(nameof(ConflictCases))]
    public void Distinct_Instantiations_Produce_SPY0607(string source, string code)
    {
        var result = CompileAndExecute(code);
        result.Success.Should().BeFalse($"{source}: distinct instantiations must produce SPY0607");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == SPY0607,
            $"{source}: expected SPY0607 for conflicting interface instantiation");
    }

    public static IEnumerable<object[]> AcceptedCases => new[]
    {
        new object[]
        {
            "SameInstantiation_Diamond",
            @"interface IA[T]:
    def get(self) -> T: ...

interface IB(IA[int]):
    pass

interface IC(IA[int]):
    pass

class D(IB, IC):
    def get(self) -> int:
        return 42

def main():
    d = D()
    print(d.get())"
        },
        new object[]
        {
            "SameInstantiation_Inherited",
            @"interface IA[T]:
    def get(self) -> T: ...

class B(IA[int]):
    def get(self) -> int:
        return 1

class D(B, IA[int]):
    pass

def main():
    d = D()
    print(d.get())"
        },
        new object[]
        {
            "NonGeneric_NeverConflicts",
            @"interface IA:
    def get(self) -> int: ...

class B(IA):
    def get(self) -> int:
        return 1

class D(B, IA):
    pass

def main():
    d = D()
    print(d.get())"
        },
    };

    [Theory]
    [MemberData(nameof(AcceptedCases))]
    public void Same_Instantiation_Accepted(string source, string code)
    {
        var result = CompileAndExecute(code);
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == SPY0607,
            $"{source}: same instantiation must not produce SPY0607");
        result.Success.Should().BeTrue($"{source}: same instantiation must compile and run");
    }
}
