using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Closure matrix for SPY0607 (#1717, R-H): a type that reaches ONE generic interface at TWO
/// distinct instantiations is refused before materialization, naming both instantiations and
/// both paths; the same instantiation via several paths runs. Axes: pair source {explicit in one
/// base list, interface-parent diamond, base class + direct, generic host, transitive chain,
/// walker-converter modifiers, nested modifiers, synthesized vs synthesized (Base/Derived, four
/// dunders), synthesized vs explicit, user-typed arguments} × host {class, struct, interface} ×
/// {distinct, same} × {single file, cross-module three files, warm --incremental}. Counts are
/// literal anchors; the CLR-internal exemption is exercised by hand-built symbols with a positive
/// control (no nameable BCL shape reaches one generic interface at two instantiations through a
/// Sharpy base list alone).
/// </summary>
[Collection("HeavyCompilation")]
public class InterfaceInstantiationClosureMatrixTests : IntegrationTestBase, IDisposable
{
    private const int ConflictCellCount = 15;
    private const int AcceptedCellCount = 8;

    private readonly string _tempDir;

    public InterfaceInstantiationClosureMatrixTests(ITestOutputHelper output) : base(output)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sharpy-closure-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        { Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    private const string SPY0607 = DiagnosticCodes.SemanticOverflow.ConflictingInterfaceInstantiation;

    private const string IaDef = @"interface IA[T]:
    def get(self) -> T: ...
";

    // ── conflict cells: (name, source, first arm, second arm) ────────────────────────────

    public static IEnumerable<object[]> ConflictCases => new[]
    {
        new object[] { "ExplicitDirect_vs_ExplicitAncestor", IaDef + @"
class B(IA[int]):
    def get(self) -> int:
        return 1

class D(B, IA[str]):
    def get(self) -> str:
        return ""hello""

def main():
    pass", "'IA[str]' (explicit)", "'IA[int32]' (inherited from B)" },

        new object[] { "DirectTwice_ClassHost", IaDef + @"
class H(IA[int], IA[str]):
    def get(self) -> int:
        return 1

def main():
    pass", "'IA[int32]' (explicit)", "'IA[str]' (explicit)" },

        new object[] { "DirectTwice_StructHost", IaDef + @"
struct S(IA[int], IA[str]):
    x: int
    def get(self) -> int:
        return self.x

def main():
    pass", "'IA[int32]' (explicit)", "'IA[str]' (explicit)" },

        new object[] { "DirectTwice_InterfaceHost_FiresAtTheIntroducingDeclaration", IaDef + @"
interface ID(IA[int], IA[str]):
    pass

def main():
    pass", "'IA[int32]' (explicit)", "'IA[str]' (explicit)" },

        new object[] { "GenericHost_OwnParameter_vs_Closed", IaDef + @"
class G[T](IA[T], IA[int]):
    v: T
    def __init__(self, v: T) -> None:
        self.v = v
    def get(self) -> T:
        return self.v

def main():
    pass", "'IA[T]' (explicit)", "'IA[int32]' (explicit)" },

        new object[] { "InterfaceDiamond_OptionalVsNullable", IaDef + @"
interface IB(IA[int?]):
    pass

interface IC(IA[int | None]):
    pass

class D(IB, IC):
    def get(self) -> int?:
        return Some(1)

def main():
    pass", "'IA[int32?]' (via IB)", "'IA[int32 | None]' (via IC)" },

        new object[] { "WalkerConverter_ParentParameterKeepsModifier", IaDef + @"
interface IB[T](IA[T?]):
    pass

interface IC[T](IA[T]):
    pass

class D(IB[int], IC[int]):
    def get(self) -> int:
        return 1

def main():
    pass", "'IA[int32?]' (via IB)", "'IA[int32]' (via IC)" },

        new object[] { "WalkerConverter_NestedModifier", IaDef + @"
interface IB(IA[list[int]?]):
    pass

interface IC(IA[list[int]]):
    pass

class D(IB, IC):
    def get(self) -> list[int]:
        return [1]

def main():
    pass", "'IA[list[int32]?]' (via IB)", "'IA[list[int32]]' (via IC)" },

        new object[] { "Transitive_ViaBaseChain", IaDef + @"
class C(IA[int]):
    def get(self) -> int:
        return 1

class B(C):
    pass

class D(B, IA[str]):
    def get(self) -> str:
        return ""hello""

def main():
    pass", "'IA[str]' (explicit)", "'IA[int32]' (inherited from C)" },

        new object[] { "Synthesized_Eq_BaseDerived_Builtins", @"class Base:
    def __eq__(self, other: str) -> bool:
        return False

class Derived(Base):
    def __eq__(self, other: int) -> bool:
        return False

def main():
    pass", "'IEquatable[int32]' (synthesized via __eq__)", "'IEquatable[str]' (inherited from Base, synthesized via __eq__)" },

        new object[] { "Synthesized_Eq_BaseDerived_UserTypes", @"class B:
    def __eq__(self, other: B) -> bool:
        return True
    def __eq__(self, other: object) -> bool:
        return False
    def __hash__(self) -> int:
        return 1

class D(B):
    def __eq__(self, other: D) -> bool:
        return True
    def __eq__(self, other: object) -> bool:
        return False
    def __hash__(self) -> int:
        return 2

def main():
    pass", "'IEquatable[D]' (synthesized via __eq__)", "'IEquatable[B]' (inherited from B, synthesized via __eq__)" },

        new object[] { "Synthesized_Reversed_BaseDerived", @"class B:
    def __reversed__(self) -> int:
        return 1

class D(B):
    def __reversed__(self) -> str:
        return ""a""

def main():
    pass", "'IReverseEnumerable[str]' (synthesized via __reversed__)", "'IReverseEnumerable[int32]' (inherited from B, synthesized via __reversed__)" },

        new object[] { "Synthesized_Next_BaseDerived", @"class StringIterator:
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
    pass", "'IEnumerator[int32]' (synthesized via __next__)", "'IEnumerator[str]' (inherited from StringIterator, synthesized via __next__)" },

        new object[] { "Synthesized_Iter_BaseDerived_Generators", @"class B:
    def __iter__(self) -> int:
        yield 1

class D(B):
    def __iter__(self) -> str:
        yield ""a""

def main():
    pass", "'IEnumerable[str]' (synthesized via __iter__)", "'IEnumerable[int32]' (inherited from B, synthesized via __iter__)" },

        new object[] { "Explicit_vs_Synthesized_ImportedIEquatable", @"from system import IEquatable

class Foo(IEquatable[str]):
    v: int

    def __init__(self, v: int) -> None:
        self.v = v

    def __eq__(self, other: Foo) -> bool:
        return self.v == other.v

    def __eq__(self, other: str) -> bool:
        return str(self.v) == other

    def __eq__(self, other: object) -> bool:
        return False

    def __hash__(self) -> int:
        return self.v

def main():
    pass", "'IEquatable[str]' (explicit)", "'IEquatable[Foo]' (synthesized via __eq__)" },
    };

    [Fact]
    public void ConflictCells_AreAnchored() => ConflictCases.Count().Should().Be(ConflictCellCount);

    [Theory]
    [MemberData(nameof(ConflictCases))]
    public void Distinct_Instantiations_Produce_SPY0607_NamingBothPaths(string name, string code, string firstArm, string secondArm)
    {
        var result = CompileAndExecute(code);
        result.Success.Should().BeFalse($"{name}: distinct instantiations must be refused");
        var diagnostic = result.RawDiagnostics.FirstOrDefault(d => d.Code == SPY0607);
        diagnostic.Should().NotBeNull($"{name}: expected SPY0607, got: {string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message))}");
        diagnostic!.Message.Should().Contain(firstArm, name);
        diagnostic.Message.Should().Contain(secondArm, name);
        diagnostic.Message.Should().NotContain("'T'", $"{name}: a closed instantiation never renders a bare type parameter");
        result.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"{name}: the refusal must be by name, never an ICE behind SPY0908");
    }

    // ── accepted cells: the same instantiation, or one that C# and Sharpy both express ────

    public static IEnumerable<object[]> AcceptedCases => new[]
    {
        new object[] { "SameInstantiation_Diamond", IaDef + @"
interface IB(IA[int]):
    pass

interface IC(IA[int]):
    pass

class D(IB, IC):
    def get(self) -> int:
        return 42

def main():
    d = D()
    print(d.get())", "42\n" },

        new object[] { "SameInstantiation_Inherited", IaDef + @"
class B(IA[int]):
    def get(self) -> int:
        return 1

class D(B, IA[int]):
    pass

def main():
    d = D()
    print(d.get())", "1\n" },

        new object[] { "SameInstantiation_StructHost", IaDef + @"
struct S(IA[int]):
    x: int
    def get(self) -> int:
        return self.x

def main():
    s = S(3)
    print(s.get())", "3\n" },

        new object[] { "NonGeneric_NeverConflicts", @"interface IA:
    def get(self) -> int: ...

class B(IA):
    def get(self) -> int:
        return 1

class D(B, IA):
    pass

def main():
    d = D()
    print(d.get())", "1\n" },

        new object[] { "Explicit_ISized_Plus_Len_IsOneEntry", @"class Bag(ISized):
    items: list[int]

    def __init__(self, items: list[int]) -> None:
        self.items = items

    def __len__(self) -> int:
        return len(self.items)

def size(s: ISized) -> str:
    return ""sized""

def main():
    b = Bag([1, 2, 3])
    print(len(b))
    print(size(b))", "3\nsized\n" },

        new object[] { "Explicit_ImportedIEquatable_Plus_SameEq_IsOneEntry", @"from system import IEquatable

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
    print(Foo(1) == Foo(1))
    print(Foo(1) == Foo(2))", "True\nFalse\n" },

        new object[] { "Synthesized_Reversed_BaseDerived_SameInstantiation", @"class B:
    def __reversed__(self) -> int:
        yield 1

class D(B):
    def __reversed__(self) -> int:
        yield 2

def main():
    for x in reversed(D()):
        print(x)", "2\n" },

        new object[] { "SameClass_EqOverloads_IntAndOptionalInt_Declare", @"class D:
    v: int
    def __init__(self, v: int) -> None:
        self.v = v
    def __eq__(self, other: int) -> bool:
        return self.v == other
    def __eq__(self, other: int?) -> bool:
        return other is not None and self.v == other
    def __eq__(self, other: object) -> bool:
        return False
    def __hash__(self) -> int:
        return self.v

def main():
    d = D(1)
    print(d == 1)
    print(d == Some(2))", "True\nFalse\n" },
    };

    [Fact]
    public void AcceptedCells_AreAnchored() => AcceptedCases.Count().Should().Be(AcceptedCellCount);

    [Theory]
    [MemberData(nameof(AcceptedCases))]
    public void Same_Instantiation_Accepted_AndPrints(string name, string code, string expected)
    {
        var result = CompileAndExecute(code);
        result.RawDiagnostics.Should().NotContain(d => d.Code == SPY0607, $"{name}: no conflict here");
        result.Success.Should().BeTrue($"{name}: must compile and run; diagnostics: {string.Join(" | ", result.RawDiagnostics.Select(d => d.Code + " " + d.Message))}");
        result.StandardOutput.Should().Be(expected, name);
    }

    // ── cross-module, cold and warm ───────────────────────────────────────────────────────

    private const string LibIa = "interface IA[T]:\n    def get(self) -> T: ...\n";
    private const string LibB = "from ia import IA\n\nclass B(IA[int]):\n    def get(self) -> int:\n        return 1\n";
    private const string MainDistinct = "from ia import IA\nfrom b import B\n\nclass D(B, IA[str]):\n    def get(self) -> str:\n        return \"s\"\n\ndef main():\n    print(D().get())\n";
    private const string MainSame = "from ia import IA\nfrom b import B\n\nclass D(B, IA[int]):\n    pass\n\ndef main():\n    print(D().get())\n";

    [Fact]
    public void CrossModule_ThreeFiles_Distinct_IsRefused_ColdAndWarm()
    {
        var config = Project("ia.spy", LibIa, "b.spy", LibB, "main.spy", MainDistinct);

        var cold = Build(config);
        var warm = Build(config);

        foreach (var (label, result) in new[] { ("cold", cold), ("warm", warm) })
        {
            var diagnostic = result.Diagnostics.GetAll().FirstOrDefault(d => d.Code == SPY0607);
            diagnostic.Should().NotBeNull($"{label}: the gate must see the instantiation inherited from another file");
            diagnostic!.Message.Should().Contain("'IA[str]' (explicit)").And.Contain("'IA[int32]' (inherited from B)");
        }
    }

    [Fact]
    public void CrossModule_ThreeFiles_Same_Runs_ColdAndWarm()
    {
        var config = Project("ia.spy", LibIa, "b.spy", LibB, "main.spy", MainSame);

        var cold = Build(config);
        var warm = Build(config);

        cold.Diagnostics.GetAll().Should().NotContain(d => d.Code == SPY0607);
        warm.Diagnostics.GetAll().Should().NotContain(d => d.Code == SPY0607);
        cold.Success.Should().BeTrue(Diagnostics(cold));
        warm.Success.Should().BeTrue(Diagnostics(warm));
    }

    // ── a synthesized row served from the cache ───────────────────────────────────────────

    private const string LibFooEq = "class Foo:\n    v: int\n    def __init__(self, v: int) -> None:\n        self.v = v\n    def __eq__(self, other: Foo) -> bool:\n        return self.v == other.v\n    def __eq__(self, other: object) -> bool:\n        return isinstance(other, Foo) and self.v == other.v\n    def __hash__(self) -> int:\n        return self.v\n";
    private const string MainFooEqV1 = "from foo import Foo\n\ndef main():\n    print(Foo(1) == Foo(1))\n";
    private const string MainFooEqV2 = "from foo import Foo\n\nclass Bar(Foo):\n    def __eq__(self, other: int) -> bool:\n        return False\n\ndef main():\n    print(Foo(1) == Foo(1))\n";

    /// <summary>
    /// A dunder-synthesized row on a type SERVED FROM THE CACHE must reach the closure gate of a
    /// recompiled dependent (#1746, #1717). The row's definition is CLR-backed, so it has no
    /// registry id and travels by name; a cache that dropped it left the restored <c>Foo</c>
    /// without its <c>IEquatable[Foo]</c>, and a warm <c>Bar(Foo)</c> adding <c>__eq__(int)</c>
    /// was ACCEPTED where the cold build refuses it — the warm≠cold divergence class. The first
    /// build is the positive control (the cold base list carries the row once); the second edits
    /// only <c>main.spy</c>, so <c>foo.spy</c> is restored and the conflict must still be named
    /// with the inherited arm's synthesized path.
    /// </summary>
    [Fact]
    public void CrossModule_SynthesizedRow_OnARestoredType_ReachesTheGate()
    {
        var config = Project("foo.spy", LibFooEq, "main.spy", MainFooEqV1);

        var cold = Build(config);
        cold.Success.Should().BeTrue(Diagnostics(cold));
        var coldFoo = cold.GeneratedCSharpFiles.Single(kv => Path.GetFileName(kv.Key) == "foo.cs").Value;
        System.Text.RegularExpressions.Regex.Matches(coldFoo, "IEquatable<Foo>").Count
            .Should().Be(1, "positive control: the cold base list carries the synthesized row exactly once");

        File.WriteAllText(Path.Combine(_tempDir, "main.spy"), MainFooEqV2);
        var warm = Build(config);

        warm.Metrics!.SkippedFiles.Select(Path.GetFileName).Should().BeEquivalentTo(new[] { "foo.spy" },
            "foo.spy is unchanged and must be restored, or the wire is not under test");
        var diagnostic = warm.Diagnostics.GetAll().FirstOrDefault(d => d.Code == SPY0607);
        diagnostic.Should().NotBeNull("the restored Foo must still contribute IEquatable[Foo]; diagnostics: " + Diagnostics(warm));
        diagnostic!.Message.Should().Contain("'IEquatable[int32]' (synthesized via __eq__)")
            .And.Contain("'IEquatable[Foo]' (inherited from Foo, synthesized via __eq__)");
    }

    private ProjectConfig Project(params string[] namesAndContents)
    {
        var files = new List<string>();
        for (int i = 0; i < namesAndContents.Length; i += 2)
        {
            var path = Path.Combine(_tempDir, namesAndContents[i]);
            File.WriteAllText(path, namesAndContents[i + 1]);
            files.Add(path);
        }
        return new ProjectConfig
        {
            ProjectFilePath = Path.Combine(_tempDir, "closure.spyproj"),
            ProjectDirectory = _tempDir,
            RootNamespace = "Closure",
            SourceFiles = files,
            Configuration = "Debug",
        };
    }

    private static ProjectCompilationResult Build(ProjectConfig config)
        => new Compiler(new CompilerOptions { Incremental = true }, NullLogger.Instance).CompileProject(config);

    private static string Diagnostics(ProjectCompilationResult result)
        => string.Join("\n", result.Diagnostics.GetAll().Select(d => $"{d.Severity}:{d.Code} {d.Message}"));

    // ── the CLR-internal exemption, by hand ───────────────────────────────────────────────

    /// <summary>
    /// A conflict whose EVERY contributing path is declared by a reflected CLR symbol is not
    /// something Sharpy source can fix, so the gate is silent on it; the moment a source-declared
    /// path joins (positive control), the same definition group is refused.
    /// </summary>
    [Fact]
    public void ClrInternalConflict_IsExempt_UntilASourcePathJoins()
    {
        var symbolTable = new SymbolTable(new BuiltinRegistry());
        var semanticInfo = new SemanticInfo();
        var binding = new SemanticBinding();
        var resolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);

        var iface = new TypeSymbol
        {
            Name = "IEquatable",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Interface,
            ClrType = typeof(IEquatable<>),
            TypeParameters = new List<TypeParameterDef> { new() { Name = "T0" } },
        };
        var clrBase = new TypeSymbol
        {
            Name = "ClrBase",
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Class,
            ClrType = typeof(object),
        };
        clrBase.Interfaces.Add(new InterfaceReference { Definition = iface, ResolvedTypeArguments = System.Collections.Immutable.ImmutableArray.Create<SemanticType>(SemanticType.Int) });
        clrBase.Interfaces.Add(new InterfaceReference { Definition = iface, ResolvedTypeArguments = System.Collections.Immutable.ImmutableArray.Create<SemanticType>(SemanticType.Str) });

        var derived = new TypeSymbol { Name = "D", Kind = SymbolKind.Type, TypeKind = TypeKind.Class, DefiningFilePath = "/d.spy" };
        binding.SetBaseType(derived, clrBase);

        var bag = new DiagnosticBag();
        InterfaceInstantiationGate.Check(derived, binding, resolver, bag);
        bag.GetAll().Should().NotContain(d => d.Code == SPY0607, "both paths are CLR-internal");

        // Positive control: a source-declared third instantiation joins the same group.
        binding.AddInterface(derived, new InterfaceReference { Definition = iface, ResolvedTypeArguments = System.Collections.Immutable.ImmutableArray.Create<SemanticType>(SemanticType.Bool) });
        var bag2 = new DiagnosticBag();
        InterfaceInstantiationGate.Check(derived, binding, resolver, bag2);
        bag2.GetAll().Should().Contain(d => d.Code == SPY0607, "a source-declared path now contributes");
    }
}
