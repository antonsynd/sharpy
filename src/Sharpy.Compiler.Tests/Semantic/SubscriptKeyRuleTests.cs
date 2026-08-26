using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Class guard for the subscript key rule (#1620, R6 of the plan-c6ae1b audit): the key of
/// <c>x[k]</c> is validated against the receiver's key contract <b>in every syntactic
/// position</b> — a READ against <c>__getitem__</c> (dict key type / int index / user overloads),
/// a STORE against <c>__setitem__</c>, an AUGMENTED store against both — and a receiver that
/// lacks the relevant dunder is refused ONCE, by the ProtocolValidator (SPY0320), never a second
/// time by the key check (SPY0220). The matrix is receiver kind × position × key, so a new
/// position cannot skip the check and a new receiver kind cannot double-fire.
///
/// <para>Mutation record (commit body): neutering the store arm in
/// <c>TypeChecker.CheckIndexAccessCore</c> (the <c>storeTarget != null</c> branch) turns the
/// <c>UserSetItemOnly/Store</c> and <c>UserBoth/Store</c> cells red — the __setitem__-only
/// class is refused on a correct key and the wrong key on a mismatched setter is accepted.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class SubscriptKeyRuleTests : IntegrationTestBase
{
    public SubscriptKeyRuleTests(ITestOutputHelper output) : base(output) { }

    public enum Receiver { Dict, Set, List, UserGetItemOnly, UserSetItemOnly, UserBoth }
    public enum Position { Read, Store, AugmentedStore }

    private const string UserGetItemOnly = @"
class G:
    def __getitem__(self, k: int) -> int:
        return 7
";

    private const string UserSetItemOnly = @"
class S:
    def __setitem__(self, k: int, v: str) -> None:
        print(v)
";

    private const string UserBoth = @"
class B:
    stored: int = 0
    def __getitem__(self, k: int) -> int:
        return self.stored
    def __setitem__(self, k: int, v: int) -> None:
        self.stored = v
";

    private static (string Prelude, string Decl, string OkKey, string WrongKey, string Value) Shape(Receiver receiver)
        => receiver switch
        {
            Receiver.Dict => ("", "x: dict[str, int] = {\"a\": 1}", "\"a\"", "1", "2"),
            Receiver.Set => ("", "x: set[int] = {1, 2}", "0", "\"a\"", "2"),
            Receiver.List => ("", "x: list[int] = [1, 2]", "0", "\"a\"", "2"),
            Receiver.UserGetItemOnly => (UserGetItemOnly, "x: G = G()", "1", "\"a\"", "2"),
            Receiver.UserSetItemOnly => (UserSetItemOnly, "x: S = S()", "1", "\"a\"", "\"v\""),
            Receiver.UserBoth => (UserBoth, "x: B = B()", "1", "\"a\"", "2"),
            _ => throw new ArgumentOutOfRangeException(nameof(receiver)),
        };

    private static string Source(Receiver receiver, Position position, bool keyOk)
    {
        var (prelude, decl, okKey, wrongKey, value) = Shape(receiver);
        var key = keyOk ? okKey : wrongKey;
        var statement = position switch
        {
            Position.Read => $"print(x[{key}])",
            Position.Store => $"x[{key}] = {value}\n    print(\"stored\")",
            Position.AugmentedStore => $"x[{key}] += {value}\n    print(x[{okKey}])",
            _ => throw new ArgumentOutOfRangeException(nameof(position)),
        };
        return $@"{prelude}
def main() -> None:
    {decl}
    {statement}
";
    }

    /// <summary>
    /// Expected outcome per cell: <c>Ok</c> runs and prints <c>stdout</c>; otherwise the compile
    /// refuses with <c>code</c> and — for the no-double-fire cells — must NOT also carry
    /// <c>forbidden</c>. <c>names</c> is a substring the diagnostic message must contain (which
    /// dunder the key was validated against).
    /// </summary>
    public static TheoryData<Receiver, Position, bool, string?, string?, string?, string?> Cells() => new()
    {
        // receiver, position, keyOk, stdout (null = refuse), code, forbidden code, message substring
        { Receiver.Dict, Position.Read, true, "1", null, null, null },
        { Receiver.Dict, Position.Read, false, null, "SPY0220", "SPY0320", "Dict key must be 'str'" },
        { Receiver.Dict, Position.Store, true, "stored", null, null, null },
        { Receiver.Dict, Position.Store, false, null, "SPY0220", "SPY0320", "Dict key must be 'str'" },
        { Receiver.Dict, Position.AugmentedStore, true, "3", null, null, null },
        { Receiver.Dict, Position.AugmentedStore, false, null, "SPY0220", "SPY0320", "Dict key must be 'str'" },

        // set is not subscriptable in any position: the presence refusal, and ONLY that one.
        { Receiver.Set, Position.Read, true, null, "SPY0320", "SPY0220", "missing '__getitem__'" },
        { Receiver.Set, Position.Read, false, null, "SPY0320", "SPY0220", "missing '__getitem__'" },
        { Receiver.Set, Position.Store, true, null, "SPY0320", "SPY0220", "missing '__setitem__'" },
        { Receiver.Set, Position.Store, false, null, "SPY0320", "SPY0220", "missing '__setitem__'" },
        { Receiver.Set, Position.AugmentedStore, true, null, "SPY0320", "SPY0220", "missing '__setitem__'" },
        { Receiver.Set, Position.AugmentedStore, false, null, "SPY0320", "SPY0220", "missing '__setitem__'" },

        { Receiver.List, Position.Read, true, "1", null, null, null },
        { Receiver.List, Position.Read, false, null, "SPY0220", "SPY0320", "Index must be 'int'" },
        { Receiver.List, Position.Store, true, "stored", null, null, null },
        { Receiver.List, Position.Store, false, null, "SPY0220", "SPY0320", "Index must be 'int'" },
        { Receiver.List, Position.AugmentedStore, true, "3", null, null, null },
        { Receiver.List, Position.AugmentedStore, false, null, "SPY0220", "SPY0320", "Index must be 'int'" },

        { Receiver.UserGetItemOnly, Position.Read, true, "7", null, null, null },
        { Receiver.UserGetItemOnly, Position.Read, false, null, "SPY0220", "SPY0320", "__getitem__ of 'G' does not accept a key of type 'str'" },
        // A plain store on a getter-only class is a missing-setter defect, not a getter key defect.
        { Receiver.UserGetItemOnly, Position.Store, true, null, "SPY0320", "SPY0220", "missing '__setitem__'" },
        { Receiver.UserGetItemOnly, Position.Store, false, null, "SPY0320", "SPY0220", "missing '__setitem__'" },
        { Receiver.UserGetItemOnly, Position.AugmentedStore, true, null, "SPY0320", "SPY0220", "missing '__setitem__'" },

        // The R6 regression cells: a __setitem__-only class stores on a correct key and is refused
        // ONCE on a read (positive control for the store flag scoping — the same node shape read
        // instead of written must not carry the SPY0220 key refusal).
        { Receiver.UserSetItemOnly, Position.Read, true, null, "SPY0320", "SPY0220", "missing '__getitem__'" },
        { Receiver.UserSetItemOnly, Position.Read, false, null, "SPY0320", "SPY0220", "missing '__getitem__'" },
        { Receiver.UserSetItemOnly, Position.Store, true, "v\nstored", null, null, null },
        { Receiver.UserSetItemOnly, Position.Store, false, null, "SPY0220", "SPY0320", "__setitem__ of 'S' does not accept a key of type 'str'" },
        { Receiver.UserSetItemOnly, Position.AugmentedStore, true, null, "SPY0320", "SPY0220", "missing '__getitem__'" },

        { Receiver.UserBoth, Position.Read, true, "0", null, null, null },
        { Receiver.UserBoth, Position.Read, false, null, "SPY0220", "SPY0320", "__getitem__ of 'B' does not accept a key of type 'str'" },
        { Receiver.UserBoth, Position.Store, true, "stored", null, null, null },
        { Receiver.UserBoth, Position.Store, false, null, "SPY0220", "SPY0320", "__setitem__ of 'B' does not accept a key of type 'str'" },
        { Receiver.UserBoth, Position.AugmentedStore, true, "2", null, null, null },
        { Receiver.UserBoth, Position.AugmentedStore, false, null, "SPY0220", "SPY0320", "does not accept a key of type 'str'" },
    };

    [Theory]
    [MemberData(nameof(Cells))]
    public void KeyIsValidatedAgainstThePositionsDunder(
        Receiver receiver, Position position, bool keyOk,
        string? stdout, string? code, string? forbidden, string? messageSubstring)
    {
        var source = Source(receiver, position, keyOk);
        var result = CompileAndExecute(source);

        if (stdout != null)
        {
            result.Success.Should().BeTrue($"{receiver}/{position}/{(keyOk ? "ok" : "wrong")} should compile and run:\n{source}\n{string.Join("; ", result.CompilationErrors)}");
            result.StandardOutput.Trim().Replace("\r\n", "\n").Should().Be(stdout);
            return;
        }

        result.Success.Should().BeFalse($"{receiver}/{position}/{(keyOk ? "ok" : "wrong")} should refuse:\n{source}");
        result.RawDiagnostics.Should().Contain(d => d.Code == code,
            $"expected {code} for {receiver}/{position}/{(keyOk ? "ok" : "wrong")}; got: {DescribeDiagnostics(result)}");
        if (forbidden != null)
        {
            result.RawDiagnostics.Should().NotContain(d => d.Code == forbidden,
                $"{forbidden} must not double-fire with {code} for {receiver}/{position}; got: {DescribeDiagnostics(result)}");
        }
        if (messageSubstring != null)
        {
            result.RawDiagnostics.Should().Contain(d => d.Code == code && d.Message.Contains(messageSubstring),
                $"the {code} message should name the dunder/contract; got: {DescribeDiagnostics(result)}");
        }
    }

    /// <summary>
    /// The store types the target as the selected <c>__setitem__</c> overload's VALUE parameter, so
    /// a wrong VALUE on a correct key is refused by the ordinary assignment check (a
    /// __setitem__-only class has no __getitem__ return type to borrow).
    /// </summary>
    [Fact]
    public void StoreValueIsCheckedAgainstTheSetterValueParameter()
    {
        var result = CompileAndExecute(UserSetItemOnly + @"
def main() -> None:
    x: S = S()
    x[1] = 5
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Semantic.TypeMismatch && d.Message.Contains("Cannot assign type 'int32' to 'str'"),
            DescribeDiagnostics(result));
        result.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Semantic.ProtocolMissingMethod);
    }

    /// <summary>
    /// A getter/setter pair with different key types: the store validates against the SETTER's key.
    /// Before the store arm this compiled and ran with the setter's key parameter silently retyped
    /// to the getter's (#1654).
    /// </summary>
    [Fact]
    public void StoreOnMismatchedSetterKeyIsRefusedNamingSetItem()
    {
        var result = CompileAndExecute(@"
class Grid:
    def __getitem__(self, k: int) -> str:
        return ""r""
    def __setitem__(self, k: str, v: str) -> None:
        print(v)

def main() -> None:
    g: Grid = Grid()
    g[1] = ""x""
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Semantic.TypeMismatch
            && d.Message.Contains("__setitem__ of 'Grid' does not accept a key of type 'int32'"),
            DescribeDiagnostics(result));
    }

    /// <summary>
    /// An unpacking element is a store position too (`b[k], y = …`). The positive half asserts the
    /// SEMANTIC outcome only (no diagnostic): the emitter currently drops non-identifier unpacking
    /// elements (#1655), so the executing assertion — stdout <c>v\n2</c> — is deferred to that issue
    /// and must be enabled when it closes.
    /// </summary>
    [Fact]
    public void UnpackingElementIsAStorePosition()
    {
        var ok = CompileAndExecute(UserSetItemOnly + @"
def main() -> None:
    x: S = S()
    y: int = 0
    x[1], y = (""v"", 2)
    print(y)
");
        ok.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Semantic.TypeMismatch
            || d.Code == DiagnosticCodes.Semantic.ProtocolMissingMethod,
            DescribeDiagnostics(ok));
        // TODO(#1655): ok.StandardOutput should be "v\n2" once the emitter stores unpacking elements.

        var wrong = CompileAndExecute(UserSetItemOnly + @"
def main() -> None:
    x: S = S()
    y: int = 0
    x[""a""], y = (""v"", 2)
");
        wrong.Success.Should().BeFalse();
        wrong.RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Semantic.TypeMismatch
            && d.Message.Contains("__setitem__ of 'S' does not accept a key of type 'str'"),
            DescribeDiagnostics(wrong));
        wrong.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Semantic.ProtocolMissingMethod);
    }

    /// <summary>Overloaded __getitem__: a key matching no overload lists the overloads.</summary>
    [Fact]
    public void GetItemKeyMatchingNoOverloadListsTheOverloads()
    {
        var result = CompileAndExecute(@"
class Box:
    def __getitem__(self, k: int) -> str:
        return ""int""
    def __getitem__(self, k: str) -> str:
        return ""str""

def main() -> None:
    b: Box = Box()
    print(b[1.5])
");
        result.Success.Should().BeFalse();
        result.RawDiagnostics.Should().Contain(d =>
            d.Code == DiagnosticCodes.Semantic.TypeMismatch
            && d.Message.Contains("__getitem__ of 'Box' does not accept a key of type 'float64' (overloads: (k: int32), (k: str))"),
            DescribeDiagnostics(result));
        result.RawDiagnostics.Should().NotContain(d => d.Code == DiagnosticCodes.Semantic.ProtocolMissingMethod);
    }

    private static string DescribeDiagnostics(ExecutionResult result)
        => string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"));
}
