using System.Collections.Immutable;
using FluentAssertions;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Verifies that the TypeChecker records the operator lowering facts the emitter reads
/// (<see cref="OperatorLowering"/> on binary/unary/augmented nodes, <see cref="ComparisonChainLowering"/>
/// per chain link) — the semantic side of plan-c6ae1b Phase 7 (#1623, #1642). The emitter side is
/// <c>RoslynEmitterOperatorLoweringTests</c>; the executing fixtures live under
/// <c>TestFixtures/operators/</c>.
/// </summary>
public class OperatorLoweringRecordingTests
{
    private static (Module module, SemanticInfo info, IReadOnlyList<string> errors) Analyze(string source)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new global::Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();

        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance);
        // Not an entry point: generic-function sources have no main(), and SPY0403 is not the subject.
        typeChecker.CheckModule(module, isEntryPoint: false);

        var errors = typeChecker.Diagnostics.GetErrors().Select(e => $"{e.Code}: {e.Message}").ToList();
        return (module, semanticInfo, errors);
    }

    private static IEnumerable<T> Find<T>(Node node) where T : Node
    {
        foreach (var child in node.GetChildNodes())
        {
            if (child is T match)
                yield return match;
            foreach (var descendant in Find<T>(child))
                yield return descendant;
        }
    }

    private static ComparisonChain SingleChain(Module module) => Find<ComparisonChain>(module).Single();

    private static BinaryOp SingleBinaryOp(Module module, BinaryOperator op)
        => Find<BinaryOp>(module).Single(b => b.Operator == op);

    #region Comparison chains (#1642)

    [Fact]
    public void StringChain_RecordsStringOrdinalCompare_OnEveryLink()
    {
        var (module, info, errors) = Analyze(@"
def main() -> None:
    a: str = ""x""
    b: str = ""y""
    c: str = ""z""
    r: bool = a < b <= c
");
        errors.Should().BeEmpty();
        var lowering = info.GetComparisonChainLowering(SingleChain(module));
        lowering.Should().NotBeNull();
        lowering!.Links.Should().HaveCount(2);
        lowering.Links.Should().AllSatisfy(link =>
        {
            link.Kind.Should().Be(OperatorLoweringKind.StringOrdinalCompare);
            link.Equality.Should().BeNull();
        });
    }

    [Fact]
    public void MixedChain_EqualityLinkFromEqualityAuthority_OrderingLinkOrdinal()
    {
        // `a == a < b`: link 0 is a str equality (native), link 1 a str ordering (ordinal).
        var (module, info, errors) = Analyze(@"
def main() -> None:
    a: str = ""x""
    b: str = ""y""
    r: bool = a == a < b
");
        errors.Should().BeEmpty();
        var links = info.GetComparisonChainLowering(SingleChain(module))!.Links;
        links.Should().HaveCount(2);
        links[0].Should().Be(new ComparisonLinkLowering(OperatorLoweringKind.Native, BinaryOpLowering.NativeOperator));
        links[1].Should().Be(new ComparisonLinkLowering(OperatorLoweringKind.StringOrdinalCompare, null));
    }

    [Fact]
    public void TupleEqualityChain_RecordsEqualsCallInstance_OnEveryLink()
    {
        var (module, info, errors) = Analyze(@"
def main() -> None:
    t1: tuple[int, int] = (1, 2)
    t2: tuple[int, int] = (1, 2)
    r: bool = t1 == t2 != t1
");
        errors.Should().BeEmpty();
        var links = info.GetComparisonChainLowering(SingleChain(module))!.Links;
        links.Should().HaveCount(2);
        links.Should().AllSatisfy(link =>
        {
            link.Kind.Should().Be(OperatorLoweringKind.Native);
            link.Equality.Should().Be(BinaryOpLowering.EqualsCallInstance);
        });
    }

    [Fact]
    public void NoneLiteralLinks_RecordNoneCheck_OnEitherSide()
    {
        var (module, info, errors) = Analyze(@"
class Dog:
    n: int

    def __init__(self, n: int) -> None:
        self.n = n

def main() -> None:
    d: Dog = Dog(1)
    e: Dog = Dog(2)
    r: bool = d == None != e
");
        errors.Should().BeEmpty();
        var links = info.GetComparisonChainLowering(SingleChain(module))!.Links;
        links.Should().HaveCount(2);
        links[0].Equality.Should().Be(BinaryOpLowering.NoneCheck);
        links[1].Equality.Should().Be(BinaryOpLowering.NoneCheck);
    }

    [Fact]
    public void TypeParameterChain_RecordsTypeParameterCompareTo_OnEveryLink()
    {
        var (module, info, errors) = Analyze(@"
def mid[T: IComparable[T]](a: T, b: T, c: T) -> bool:
    return a < b < c
");
        errors.Should().BeEmpty();
        var links = info.GetComparisonChainLowering(SingleChain(module))!.Links;
        links.Should().HaveCount(2);
        links.Should().AllSatisfy(link =>
            link.Should().Be(new ComparisonLinkLowering(OperatorLoweringKind.TypeParameterCompareTo, null)));
    }

    [Fact]
    public void TypeParameterEqualityChain_RecordsEqualityComparerDefault_OnEveryLink()
    {
        var (module, info, errors) = Analyze(@"
def same[T](a: T, b: T, c: T) -> bool:
    return a == b == c
");
        errors.Should().BeEmpty();
        var links = info.GetComparisonChainLowering(SingleChain(module))!.Links;
        links.Should().HaveCount(2);
        links.Should().AllSatisfy(link =>
            link.Equality.Should().Be(BinaryOpLowering.EqualityComparerDefault));
    }

    [Fact]
    public void IntChain_RecordsNativeLinks()
    {
        var (module, info, errors) = Analyze(@"
def main() -> None:
    x: int = 1
    r: bool = 0 < x < 2 == 2
");
        errors.Should().BeEmpty();
        var links = info.GetComparisonChainLowering(SingleChain(module))!.Links;
        links.Should().HaveCount(3);
        links[0].Should().Be(new ComparisonLinkLowering(OperatorLoweringKind.Native, null));
        links[1].Should().Be(new ComparisonLinkLowering(OperatorLoweringKind.Native, null));
        links[2].Should().Be(new ComparisonLinkLowering(OperatorLoweringKind.Native, BinaryOpLowering.NativeOperator));
    }

    [Fact]
    public void UnknownOperandChain_StillRecordsOneNativeLinkPerOperator()
    {
        // An undefined name types as Unknown (and reports); the chain still gets a total record so
        // the emitter never has to fall back.
        var (module, info, _) = Analyze(@"
def main() -> None:
    a: str = ""x""
    r: bool = a < undefined_name < a
");
        var links = info.GetComparisonChainLowering(SingleChain(module))!.Links;
        links.Should().HaveCount(2);
        links.Should().AllSatisfy(link =>
            link.Should().Be(new ComparisonLinkLowering(OperatorLoweringKind.Native, null)));
    }

    /// <summary>
    /// Class contract (#1642): for every operand-type × operator cell, chain link i is classified
    /// exactly as the binary form <c>Operands[i] op Operands[i+1]</c> — one classifier, two readers.
    /// </summary>
    [Theory]
    [InlineData("def main() -> None:", "    a: str = \"x\"\n    b: str = \"y\"\n    c: str = \"z\"", "<",
        OperatorLoweringKind.StringOrdinalCompare, null)]
    [InlineData("def main() -> None:", "    a: str = \"x\"\n    b: str = \"y\"\n    c: str = \"z\"", ">=",
        OperatorLoweringKind.StringOrdinalCompare, null)]
    [InlineData("def main() -> None:", "    a: str = \"x\"\n    b: str = \"y\"\n    c: str = \"z\"", "==",
        OperatorLoweringKind.Native, BinaryOpLowering.NativeOperator)]
    [InlineData("def main() -> None:", "    a: int = 1\n    b: int = 2\n    c: int = 3", "<",
        OperatorLoweringKind.Native, null)]
    [InlineData("def main() -> None:", "    a: int = 1\n    b: int = 2\n    c: int = 3", "!=",
        OperatorLoweringKind.Native, BinaryOpLowering.NativeOperator)]
    [InlineData("def main() -> None:", "    a: tuple[int, int] = (1, 2)\n    b: tuple[int, int] = (1, 2)\n    c: tuple[int, int] = (1, 2)", "==",
        OperatorLoweringKind.Native, BinaryOpLowering.EqualsCallInstance)]
    [InlineData("def f[T: IComparable[T]](a: T, b: T, c: T) -> None:", "    pass", "<",
        OperatorLoweringKind.TypeParameterCompareTo, null)]
    [InlineData("def f[T](a: T, b: T, c: T) -> None:", "    pass", "==",
        OperatorLoweringKind.Native, BinaryOpLowering.EqualityComparerDefault)]
    public void ChainLink_IsClassifiedExactlyAsItsBinaryForm(
        string header, string decls, string op,
        OperatorLoweringKind expectedKind, BinaryOpLowering? expectedEquality)
    {
        var (module, info, errors) = Analyze($@"
{header}
{decls}
    r1: bool = a {op} b
    r2: bool = a {op} b {op} c
");
        errors.Should().BeEmpty();

        var binOp = Find<BinaryOp>(module).Single();
        var binaryKind = info.GetOperatorLowering(binOp)?.Kind ?? OperatorLoweringKind.Native;
        var binaryEquality = info.GetBinaryOpLoweringForIr(binOp);

        var links = info.GetComparisonChainLowering(SingleChain(module))!.Links;
        links.Should().HaveCount(2);
        foreach (var link in links)
        {
            link.Kind.Should().Be(expectedKind);
            link.Equality.Should().Be(expectedEquality);
            // Parity with the binary form of the same operand pair.
            link.Kind.Should().Be(binaryKind);
            (link.Equality ?? BinaryOpLowering.NativeOperator).Should().Be(binaryEquality);
        }
    }

    [Fact]
    public void BinaryOrderingComparison_RecordsSameKindsTheChainDoes()
    {
        var (module, info, errors) = Analyze(@"
def main() -> None:
    a: str = ""x""
    b: str = ""y""
    r: bool = a < b
");
        errors.Should().BeEmpty();
        info.GetOperatorLowering(SingleBinaryOp(module, BinaryOperator.LessThan))!.Kind
            .Should().Be(OperatorLoweringKind.StringOrdinalCompare);
    }

    #endregion

    #region String repeat (#1623)

    [Theory]
    [InlineData("r: str = \"ab\" * 3", OperatorLoweringKind.StringRepeatStrLeft)]
    [InlineData("r: str = 3 * \"ab\"", OperatorLoweringKind.StringRepeatStrRight)]
    public void StringRepeat_RecordsWhichOperandIsTheString(string statement, OperatorLoweringKind expected)
    {
        var (module, info, errors) = Analyze($@"
def main() -> None:
    {statement}
");
        errors.Should().BeEmpty();
        info.GetOperatorLowering(SingleBinaryOp(module, BinaryOperator.Multiply))!.Kind.Should().Be(expected);
    }

    [Fact]
    public void AugmentedStringRepeat_RecordsStrLeft_OnTheAssignment()
    {
        var (module, info, errors) = Analyze(@"
def main() -> None:
    s: str = ""a""
    s *= 3
");
        errors.Should().BeEmpty();
        var assignment = Find<Assignment>(module).Single(a => a.Operator == AssignmentOperator.StarAssign);
        info.GetOperatorLowering(assignment)!.Kind.Should().Be(OperatorLoweringKind.StringRepeatStrLeft);
    }

    [Fact]
    public void IntMultiply_RecordsNoStringRepeat()
    {
        var (module, info, errors) = Analyze(@"
def main() -> None:
    r: int = 3 * 4
");
        errors.Should().BeEmpty();
        info.GetOperatorLowering(SingleBinaryOp(module, BinaryOperator.Multiply)).Should().BeNull();
    }

    #endregion

    #region Power (#1623)

    [Theory]
    [InlineData("n: int = 3", "n ** 2", OperatorLoweringKind.IntegerPowInt)]
    [InlineData("n: long = 3", "n ** 2", OperatorLoweringKind.IntegerPowLong)]
    [InlineData("n: int = 3\n    k: long = 2", "n ** k", OperatorLoweringKind.IntegerPowLong)]
    [InlineData("n: long = 3\n    k: long = 2", "n ** k", OperatorLoweringKind.IntegerPowLong)]
    [InlineData("f: float = 2.0", "f ** 2", OperatorLoweringKind.FloatPow)]
    [InlineData("n: int = 3", "n ** 0.5", OperatorLoweringKind.FloatPow)]
    [InlineData("d: decimal = 2.5m", "d ** 2", OperatorLoweringKind.DecimalPow)]
    [InlineData("n: int = 3\n    e: int = -1", "n ** e", OperatorLoweringKind.IntegerPowInt)]
    public void Power_RecordsItsFamily(string decls, string expr, OperatorLoweringKind expected)
    {
        var (module, info, errors) = Analyze($@"
def main() -> None:
    {decls}
    r = {expr}
");
        errors.Should().BeEmpty();
        info.GetOperatorLowering(SingleBinaryOp(module, BinaryOperator.Power))!.Kind.Should().Be(expected);
    }

    [Theory]
    [InlineData("a: int = 2", "a **= 3", OperatorLoweringKind.IntegerPowInt)]
    [InlineData("a: long = 2", "a **= 3", OperatorLoweringKind.IntegerPowLong)]
    [InlineData("a: long = 2\n    k: long = 3", "a **= k", OperatorLoweringKind.IntegerPowLong)]
    [InlineData("a: float = 2.0", "a **= 0.5", OperatorLoweringKind.FloatPow)]
    public void AugmentedPower_RecordsTheSameFamilyAsItsBinaryForm(string decls, string statement, OperatorLoweringKind expected)
    {
        var (module, info, errors) = Analyze($@"
def main() -> None:
    {decls}
    {statement}
");
        errors.Should().BeEmpty();
        var assignment = Find<Assignment>(module).Single(a => a.Operator == AssignmentOperator.PowerAssign);
        info.GetOperatorLowering(assignment)!.Kind.Should().Be(expected);
    }

    #endregion

    #region Floor division and modulo (#1658)

    // The five tags are classified by ONE TypeChecker helper from operand TYPES (literals included —
    // `a // 3`, `7.0 // a`, `d % 3m`); the emitter switches on the tag and has no operand-type
    // fallback, so an unrecorded `//` is an emitter ICE and an unrecorded `%` is the native operator.

    [Theory]
    // `//` — integer operands (int/long and the widened CLR integers) → IntegerFloorDivide
    [InlineData("a: int = 7\n    b: int = 3", "a // b", OperatorLoweringKind.IntegerFloorDivide)]
    [InlineData("a: long = 7\n    b: int = 3", "a // b", OperatorLoweringKind.IntegerFloorDivide)]
    [InlineData("a: int = 7\n    b: long = 3", "a // b", OperatorLoweringKind.IntegerFloorDivide)]
    [InlineData("a: int = 7", "a // 3", OperatorLoweringKind.IntegerFloorDivide)]
    [InlineData("a: int = 7", "-7 // a", OperatorLoweringKind.IntegerFloorDivide)]
    [InlineData("u: uint8 = 7\n    a: int = 3", "u // a", OperatorLoweringKind.IntegerFloorDivide)]
    [InlineData("u: uint8 = 7\n    a: int = 3", "a // u", OperatorLoweringKind.IntegerFloorDivide)]
    // `//` — a float32/float64 operand on either side → FloatFloorDivide
    [InlineData("a: int = 7\n    f: float = 2.0", "a // f", OperatorLoweringKind.FloatFloorDivide)]
    [InlineData("a: int = 7\n    f: float = 2.0", "f // a", OperatorLoweringKind.FloatFloorDivide)]
    [InlineData("a: int = 7", "a // 2.0", OperatorLoweringKind.FloatFloorDivide)]
    [InlineData("a: int = 7", "7.5 // a", OperatorLoweringKind.FloatFloorDivide)]
    [InlineData("f: float = 7.5\n    g: float = 2.0", "f // g", OperatorLoweringKind.FloatFloorDivide)]
    [InlineData("s: float32 = 7.5f\n    a: int = 2", "s // a", OperatorLoweringKind.FloatFloorDivide)]
    // `//` — a decimal operand on either side → DecimalFloorDivide
    [InlineData("d: decimal = 7m\n    a: int = 3", "d // a", OperatorLoweringKind.DecimalFloorDivide)]
    [InlineData("d: decimal = 7m\n    a: int = 3", "a // d", OperatorLoweringKind.DecimalFloorDivide)]
    [InlineData("d: decimal = 7m\n    e: decimal = 3m", "d // e", OperatorLoweringKind.DecimalFloorDivide)]
    [InlineData("d: decimal = 7m", "d // 3m", OperatorLoweringKind.DecimalFloorDivide)]
    [InlineData("d: decimal = 7m", "-7m // d", OperatorLoweringKind.DecimalFloorDivide)]
    public void FloorDivide_RecordsItsFamily(string decls, string expr, OperatorLoweringKind expected)
    {
        var (module, info, errors) = Analyze($@"
def main() -> None:
    {decls}
    r = {expr}
");
        errors.Should().BeEmpty();
        info.GetOperatorLowering(SingleBinaryOp(module, BinaryOperator.FloorDivide))!.Kind.Should().Be(expected);
    }

    [Theory]
    // `%` — both operands in {int, long, float32, float64} → FlooredModulo
    [InlineData("a: int = 7\n    b: int = 3", "a % b", OperatorLoweringKind.FlooredModulo)]
    [InlineData("a: long = 7\n    b: int = 3", "a % b", OperatorLoweringKind.FlooredModulo)]
    [InlineData("a: int = 7", "a % 3", OperatorLoweringKind.FlooredModulo)]
    [InlineData("a: int = 7", "-7 % a", OperatorLoweringKind.FlooredModulo)]
    [InlineData("a: int = 7\n    f: float = 2.0", "a % f", OperatorLoweringKind.FlooredModulo)]
    [InlineData("a: int = 7\n    f: float = 2.0", "f % a", OperatorLoweringKind.FlooredModulo)]
    [InlineData("a: int = 7", "a % 2.0", OperatorLoweringKind.FlooredModulo)]
    [InlineData("f: float = 7.5\n    g: float = 2.0", "f % g", OperatorLoweringKind.FlooredModulo)]
    [InlineData("s: float32 = 7.5f\n    t: float32 = 2.0f", "s % t", OperatorLoweringKind.FlooredModulo)]
    // `%` — a decimal operand on either side → DecimalModulo
    [InlineData("d: decimal = 7m\n    a: int = 3", "d % a", OperatorLoweringKind.DecimalModulo)]
    [InlineData("d: decimal = 7m\n    a: int = 3", "a % d", OperatorLoweringKind.DecimalModulo)]
    [InlineData("d: decimal = 7m\n    e: decimal = 3m", "d % e", OperatorLoweringKind.DecimalModulo)]
    [InlineData("d: decimal = 7m", "d % 3m", OperatorLoweringKind.DecimalModulo)]
    public void Modulo_RecordsItsFamily(string decls, string expr, OperatorLoweringKind expected)
    {
        var (module, info, errors) = Analyze($@"
def main() -> None:
    {decls}
    r = {expr}
");
        errors.Should().BeEmpty();
        info.GetOperatorLowering(SingleBinaryOp(module, BinaryOperator.Modulo))!.Kind.Should().Be(expected);
    }

    [Theory]
    // #1662: every integer width floors — narrow CLR integers widen to the (int, int) overload,
    // uint64 has its own overload.
    [InlineData("u: uint8 = 7\n    a: int = 3", "u % a", OperatorLoweringKind.FlooredModulo)]
    [InlineData("u: uint8 = 7\n    a: int = 3", "a % u", OperatorLoweringKind.FlooredModulo)]
    [InlineData("u: uint8 = 7\n    v: uint8 = 3", "u % v", OperatorLoweringKind.FlooredModulo)]
    public void Modulo_NarrowClrInteger_RecordsFlooredModulo(string decls, string expr, OperatorLoweringKind expected)
    {
        var (module, info, errors) = Analyze($@"
def main() -> None:
    {decls}
    r = {expr}
");
        errors.Should().BeEmpty();
        info.GetOperatorLowering(SingleBinaryOp(module, BinaryOperator.Modulo))!.Kind.Should().Be(expected);
    }

    [Fact]
    public void Modulo_UserDunder_RecordsNothing_ForBinaryAndAugmented()
    {
        var (module, info, errors) = Analyze(@"
class Wrap:
    value: int

    def __init__(self, value: int) -> None:
        self.value = value

    def __mod__(self, other: Wrap) -> Wrap:
        return Wrap(self.value + other.value)


def main() -> None:
    a: Wrap = Wrap(10)
    b: Wrap = Wrap(3)
    c: Wrap = a % b
    a %= b
");
        errors.Should().BeEmpty();
        // The `%` inside __mod__ would be int % int; the user-type operands are the subject.
        var binary = Find<BinaryOp>(module).Single(b => b.Operator == BinaryOperator.Modulo
            && b.Left is Identifier { Name: "a" });
        info.GetOperatorLowering(binary).Should().BeNull("user __mod__ maps to operator %, native");
        var augmented = Find<Assignment>(module).Single(a => a.Operator == AssignmentOperator.PercentAssign);
        info.GetOperatorLowering(augmented).Should().BeNull("augmented %= on a user type is native too");
    }

    [Fact]
    public void FloorDivide_UserType_IsRefused_NotRecorded()
    {
        // There is no __floordiv__ mapping (arithmetic_operators.md): a user type under `//` is SPY0222,
        // so the "unrecorded `//`" cell can never reach the emitter's throw from a program that type-checks.
        var (module, info, errors) = Analyze(@"
class Wrap:
    value: int

    def __init__(self, value: int) -> None:
        self.value = value


def main() -> None:
    a: Wrap = Wrap(10)
    b: Wrap = Wrap(3)
    c = a // b
");
        errors.Should().ContainSingle(e => e.StartsWith("SPY0222"));
        info.GetOperatorLowering(SingleBinaryOp(module, BinaryOperator.FloorDivide)).Should().BeNull();
    }

    [Theory]
    [InlineData("a: int = 7", "a //= 3", AssignmentOperator.DoubleSlashAssign, OperatorLoweringKind.IntegerFloorDivide)]
    [InlineData("a: long = 7", "a //= 3", AssignmentOperator.DoubleSlashAssign, OperatorLoweringKind.IntegerFloorDivide)]
    [InlineData("u: uint8 = 7\n    v: uint8 = 3", "u //= v", AssignmentOperator.DoubleSlashAssign, OperatorLoweringKind.IntegerFloorDivide)]
    [InlineData("f: float = 7.5", "f //= 2", AssignmentOperator.DoubleSlashAssign, OperatorLoweringKind.FloatFloorDivide)]
    [InlineData("f: float = 7.5", "f //= 2.0", AssignmentOperator.DoubleSlashAssign, OperatorLoweringKind.FloatFloorDivide)]
    [InlineData("s: float32 = 7.5f", "s //= 2", AssignmentOperator.DoubleSlashAssign, OperatorLoweringKind.FloatFloorDivide)]
    [InlineData("d: decimal = 7m", "d //= 3", AssignmentOperator.DoubleSlashAssign, OperatorLoweringKind.DecimalFloorDivide)]
    [InlineData("d: decimal = 7m", "d //= 3m", AssignmentOperator.DoubleSlashAssign, OperatorLoweringKind.DecimalFloorDivide)]
    [InlineData("a: int = 7", "a %= 3", AssignmentOperator.PercentAssign, OperatorLoweringKind.FlooredModulo)]
    [InlineData("a: long = 7", "a %= 3", AssignmentOperator.PercentAssign, OperatorLoweringKind.FlooredModulo)]
    [InlineData("f: float = 7.5", "f %= 2", AssignmentOperator.PercentAssign, OperatorLoweringKind.FlooredModulo)]
    [InlineData("f: float = 7.5", "f %= 2.0", AssignmentOperator.PercentAssign, OperatorLoweringKind.FlooredModulo)]
    [InlineData("s: float32 = 7.5f", "s %= 2.0f", AssignmentOperator.PercentAssign, OperatorLoweringKind.FlooredModulo)]
    [InlineData("d: decimal = 7m", "d %= 3", AssignmentOperator.PercentAssign, OperatorLoweringKind.DecimalModulo)]
    [InlineData("d: decimal = 7m", "d %= 3m", AssignmentOperator.PercentAssign, OperatorLoweringKind.DecimalModulo)]
    public void AugmentedFloorDivideAndModulo_RecordTheSameFamilyAsTheBinaryForm(
        string decls, string statement, AssignmentOperator op, OperatorLoweringKind expected)
    {
        var (module, info, errors) = Analyze($@"
def main() -> None:
    {decls}
    {statement}
");
        errors.Should().BeEmpty();
        var assignment = Find<Assignment>(module).Single(a => a.Operator == op);
        info.GetOperatorLowering(assignment)!.Kind.Should().Be(expected);
    }

    [Fact]
    public void AugmentedModulo_NarrowClrInteger_RecordsFlooredModulo()
    {
        var (module, info, errors) = Analyze(@"
def main() -> None:
    u: uint8 = 7
    v: uint8 = 3
    u %= v
");
        errors.Should().BeEmpty();
        var assignment = Find<Assignment>(module).Single(a => a.Operator == AssignmentOperator.PercentAssign);
        info.GetOperatorLowering(assignment)!.Kind.Should().Be(OperatorLoweringKind.FlooredModulo);
    }

    [Theory]
    // Binary/augmented parity over the operand-type matrix: the two sites call ONE classifier, so a
    // cell can never be tagged differently by its spelling. Left type is the augmented target.
    [InlineData("int", "7", "int", "3", "//")]
    [InlineData("int", "7", "int", "3", "%")]
    [InlineData("long", "7", "int", "3", "//")]
    [InlineData("long", "7", "int", "3", "%")]
    [InlineData("float", "7.5", "int", "2", "//")]
    [InlineData("float", "7.5", "int", "2", "%")]
    [InlineData("float", "7.5", "float", "2.0", "//")]
    [InlineData("float", "7.5", "float", "2.0", "%")]
    [InlineData("decimal", "7m", "int", "3", "//")]
    [InlineData("decimal", "7m", "int", "3", "%")]
    [InlineData("decimal", "7m", "decimal", "3m", "//")]
    [InlineData("decimal", "7m", "decimal", "3m", "%")]
    [InlineData("uint8", "7", "uint8", "3", "//")]
    [InlineData("uint8", "7", "uint8", "3", "%")]
    public void FlooredArithmetic_BinaryAndAugmented_AgreeOnTheTag(
        string leftType, string leftInit, string rightType, string rightInit, string op)
    {
        var (module, info, errors) = Analyze($@"
def main() -> None:
    a: {leftType} = {leftInit}
    b: {rightType} = {rightInit}
    r = a {op} b
    a {op}= b
");
        errors.Should().BeEmpty();
        var binaryOperator = op == "//" ? BinaryOperator.FloorDivide : BinaryOperator.Modulo;
        var assignmentOperator = op == "//" ? AssignmentOperator.DoubleSlashAssign : AssignmentOperator.PercentAssign;
        var binaryKind = info.GetOperatorLowering(SingleBinaryOp(module, binaryOperator))?.Kind;
        var augmentedKind = info.GetOperatorLowering(
            Find<Assignment>(module).Single(a => a.Operator == assignmentOperator))?.Kind;
        augmentedKind.Should().Be(binaryKind);
    }

    #endregion

    #region Negated integer literal (#1304, #1623)

    [Theory]
    [InlineData("-5", OperatorLoweringKind.NegateLiteralInt)]
    [InlineData("-2147483648", OperatorLoweringKind.NegateLiteralInt)]
    [InlineData("-2147483649", OperatorLoweringKind.NegateLiteralLong)]
    [InlineData("-9223372036854775808", OperatorLoweringKind.NegateLiteralLong)]
    [InlineData("-1L", OperatorLoweringKind.NegateLiteralLong)]
    public void NegatedIntegerLiteral_RecordsItsWidth(string literal, OperatorLoweringKind expected)
    {
        var (module, info, errors) = Analyze($@"
def main() -> None:
    r = {literal}
");
        errors.Should().BeEmpty();
        var unary = Find<UnaryOp>(module).Single();
        info.GetOperatorLowering(unary)!.Kind.Should().Be(expected);
    }

    [Theory]
    [InlineData("x: int = 5\n    r = -x")]
    [InlineData("r = -(5)")]
    public void NegatedNonLiteral_RecordsNoWidthTag(string body)
    {
        var (module, info, errors) = Analyze($@"
def main() -> None:
    {body}
");
        errors.Should().BeEmpty();
        var unary = Find<UnaryOp>(module).Single();
        info.GetOperatorLowering(unary).Should().BeNull();
    }

    #endregion

    #region Super-dunder lowering (#1740)

    /// <summary>
    /// Like <see cref="Analyze"/>, but also returns the <see cref="SymbolTable"/> and the
    /// <see cref="SemanticBinding"/> the checker wrote symbol-keyed facts into (#1740's
    /// RequiresInstanceImpl is a SemanticBinding mark bridged onto CodeGenInfo only at
    /// MaterializeCodeGenInfo — reading it directly here needs no CodeGenInfoComputer pass).
    /// </summary>
    private static (Module module, SemanticInfo info, SymbolTable symbols, SemanticBinding binding,
        IReadOnlyList<string> errors) AnalyzeWithBinding(string source)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var tokens = lexer.TokenizeAll();
        var parser = new global::Sharpy.Compiler.Parser.Parser(tokens, NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var semanticBinding = new SemanticBinding();

        // A super() call needs the base-class LINK materialized onto TypeSymbol.BaseType, not just
        // computed into SemanticBinding's staging — the plain Analyze() helper never exercises
        // inheritance, so this is the one addition its class-free specimens never needed.
        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance, semanticBinding);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();
        semanticBinding.MaterializeInheritance();

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance)
        {
            SemanticBinding = semanticBinding
        };
        typeChecker.CheckModule(module, isEntryPoint: false);

        var errors = typeChecker.Diagnostics.GetErrors().Select(e => $"{e.Code}: {e.Message}").ToList();
        return (module, semanticInfo, symbolTable, semanticBinding, errors);
    }

    private static FunctionCall SingleSuperCall(Module module, string member)
        => Find<FunctionCall>(module).Single(c =>
            c.Function is MemberAccess { Object: SuperExpression, Member: var m } && m == member);

    /// <summary>
    /// An OPERATOR dunder called via <c>super()</c> — the shape <c>super().__add__(other)</c> —
    /// records the CALL node with <see cref="OperatorLoweringKind.SuperOperatorApplication"/>,
    /// narrowed to the resolved base type. P4.2's emitter reads this to lower the call to a
    /// cast-based operator application (<c>((Base)receiver) op args</c>) rather than <c>base + other</c>
    /// (never valid C# outside a member-access qualifier).
    /// </summary>
    [Fact]
    public void SuperOperatorDunderCall_RecordsSuperOperatorApplication_NarrowedToBaseType()
    {
        var (module, info, _, _, errors) = AnalyzeWithBinding(@"
class Base:
    def __add__(self, other: Base) -> Base:
        return other

class Derived(Base):
    def __add__(self, other: Base) -> Base:
        return super().__add__(other)
");
        errors.Should().BeEmpty();

        var superCall = SingleSuperCall(module, "__add__");
        var lowering = info.GetOperatorLowering(superCall);

        lowering.Should().NotBeNull();
        lowering!.Kind.Should().Be(OperatorLoweringKind.SuperOperatorApplication);
        lowering.NarrowTo.Should().BeOfType<UserDefinedType>()
            .Which.Name.Should().Be("Base");
    }

    /// <summary>The unary operator family tags the same way: the classification reads
    /// <see cref="OperatorRegistry.IsOperatorDunder"/>, not the arity.</summary>
    [Fact]
    public void SuperUnaryOperatorDunderCall_AlsoRecordsSuperOperatorApplication()
    {
        var (module, info, _, _, errors) = AnalyzeWithBinding(@"
class Base:
    def __neg__(self) -> Base:
        return self

class Derived(Base):
    def __neg__(self) -> Base:
        return super().__neg__()
");
        errors.Should().BeEmpty();

        var superCall = SingleSuperCall(module, "__neg__");
        var lowering = info.GetOperatorLowering(superCall);
        lowering.Should().NotBeNull();
        lowering!.Kind.Should().Be(OperatorLoweringKind.SuperOperatorApplication);
    }

    /// <summary>The comparison operator family tags the same way (s11-s14, Defect Class).</summary>
    [Fact]
    public void SuperComparisonOperatorDunderCall_AlsoRecordsSuperOperatorApplication()
    {
        var (module, info, _, _, errors) = AnalyzeWithBinding(@"
class Base:
    def __lt__(self, other: Base) -> bool:
        return False

class Derived(Base):
    def __lt__(self, other: Base) -> bool:
        return super().__lt__(other)
");
        errors.Should().BeEmpty();

        var superCall = SingleSuperCall(module, "__lt__");
        var lowering = info.GetOperatorLowering(superCall);
        lowering.Should().NotBeNull();
        lowering!.Kind.Should().Be(OperatorLoweringKind.SuperOperatorApplication);
    }

    /// <summary>
    /// A super call to a NON-operator dunder (<c>__str__</c>) never records
    /// <see cref="OperatorLowering"/> on its own call node — it needs <c>base.Method()</c>, not a
    /// cast-based application, because a cast alone would still virtual-dispatch to the override.
    /// </summary>
    [Fact]
    public void SuperNonOperatorDunderCall_RecordsNoOperatorLowering()
    {
        var (module, info, _, _, errors) = AnalyzeWithBinding(@"
class Base:
    def __str__(self) -> str:
        return ""base""

class Derived(Base):
    def __lt__(self, other: Derived) -> bool:
        return super().__str__() == ""base""
");
        errors.Should().BeEmpty();

        var superCall = SingleSuperCall(module, "__str__");
        info.GetOperatorLowering(superCall).Should().BeNull();
    }

    /// <summary>
    /// s21 (Defect Class, plan-d35e69): <c>super().__str__()</c> called INSIDE the OPERATOR dunder
    /// <c>__lt__</c> marks <c>__lt__</c> — the ENCLOSING method — as
    /// <see cref="SemanticBinding.RequiresInstanceImpl"/>, so P4.2's emitter keeps the instance
    /// <c>_Impl</c> split for it: <c>base.__str__()</c> is legal only inside an instance method, and
    /// <c>__lt__</c> is emitted as a static C# operator.
    /// </summary>
    [Fact]
    public void SuperNonOperatorDunderCall_MarksEnclosingOperatorMethod_RequiresInstanceImpl()
    {
        var (_, _, symbols, binding, errors) = AnalyzeWithBinding(@"
class Base:
    def __str__(self) -> str:
        return ""base""

class Derived(Base):
    def __lt__(self, other: Derived) -> bool:
        return super().__str__() == ""base""
");
        errors.Should().BeEmpty();

        var derived = symbols.Lookup("Derived") as TypeSymbol;
        derived.Should().NotBeNull();
        var ltMethod = derived!.Methods.Single(m => m.Name == "__lt__");

        binding.RequiresInstanceImpl(ltMethod).Should().BeTrue();
    }

    /// <summary>
    /// The positive control for the mark above: an operator dunder whose OWN super() call is an
    /// operator application (not method-lowered) is never marked — the fact is specific to a
    /// method-lowered call inside it, not to being an operator dunder or having any super() call.
    /// </summary>
    [Fact]
    public void OperatorMethod_WithOnlyOperatorLoweredSuperCall_IsNotMarkedRequiresInstanceImpl()
    {
        var (_, _, symbols, binding, errors) = AnalyzeWithBinding(@"
class Base:
    def __add__(self, other: Base) -> Base:
        return other

class Derived(Base):
    def __add__(self, other: Base) -> Base:
        return super().__add__(other)
");
        errors.Should().BeEmpty();

        var derived = symbols.Lookup("Derived") as TypeSymbol;
        var addMethod = derived!.Methods.Single(m => m.Name == "__add__");

        binding.RequiresInstanceImpl(addMethod).Should().BeFalse(
            "the super call inside __add__ is OPERATOR-lowered, not method-lowered");
    }

    #endregion
}
