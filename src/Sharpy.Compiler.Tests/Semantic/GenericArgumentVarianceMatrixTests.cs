using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Generic argument variance matrix (#1748, #1701 Decision 4).
///
/// <para><b>Contract.</b> <c>IsAssignable</c> is the one assignability authority, and it is
/// variance-correct: a type-argument position compares by IDENTITY unless the definition DECLARES
/// variance, and a declared-variant position admits an identity or an implicit REFERENCE conversion —
/// never a value-type widening, which .NET's variance does not cover. <c>list[T]</c>/<c>set[T]</c>/
/// <c>dict[K,V]</c> declare none (their CLR types are invariant classes), so <c>list[Dog]</c> is not a
/// <c>list[Animal]</c> and <c>list[int8]</c> is not a <c>list[int]</c> — refused BY NAME at every
/// route, not by Roslyn behind SPY0908. Every route formerly in <c>assignability-allowlist.txt</c> is
/// drained.</para>
///
/// <para><b>Axes.</b> Variance {invariant <c>list[T]</c>, invariant <c>set[T]</c>, invariant
/// <c>dict[str, T]</c>, covariant <c>IEnumerable[T]</c>, contravariant <c>(T) -&gt; None</c> parameter}
/// × Pair {<c>int8</c>/<c>int</c> widening, <c>Dog</c>/<c>Animal</c> derived, identical} × Route {call
/// argument, pipe, assignment, return, field store, augmented mutation, overload signature match,
/// generic type-parameter inference}. Not every combination exists — see
/// <see cref="NotApplicableCells"/>.</para>
///
/// <para><b>Why each cell EXECUTES.</b> A refusal cell asserts its SPY code is PRESENT in
/// <c>RawDiagnostics</c>. That is deliberately not an absence assertion: <c>CompileAndExecute</c>
/// leaves <c>RawDiagnostics</c> EMPTY when the generated C# fails to compile
/// (<c>IntegrationTestBase</c>), so a cell that regresses to SPY0908 loses its diagnostic and the cell
/// goes red. An acceptance cell asserts stdout, which no diagnostic-level check can fake.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class GenericArgumentVarianceMatrixTests : IntegrationTestBase
{
    public GenericArgumentVarianceMatrixTests(ITestOutputHelper output) : base(output) { }

    private const int VarianceCount = 5;
    private const int PairCount = 3;
    private const int RouteCount = 8;
    private const int NotApplicableCellCount = 15;

    private static readonly string[] Containers =
    {
        "InvariantList",
        "InvariantSet",
        "InvariantDictValue",
        "CovariantIEnumerable",
        "ContravariantCallback",
    };

    private static readonly string[] Pairs =
    {
        "Widening",   // int8 → int: an implicit numeric conversion, never a variance conversion
        "Derived",    // Dog → Animal: a reference conversion
        "Identical",  // int → int: the identity conversion every position admits
    };

    private static readonly string[] Routes =
    {
        "CallArgument",
        "Pipe",
        "Assignment",
        "Return",
        "FieldStore",
        "AugmentedMutation",
        "OverloadSignatureMatch",
        "TypeParameterInference",
    };

    private enum CellExpectation
    {
        Compiles,
        RefusedSPY0220,   // store/argument/pipe type mismatch
        RefusedSPY0260,   // return type mismatch
        RefusedSPY0222,   // augmented collection mutation: element not variance-convertible
        NotApplicable,
    }

    private const string ClassPrelude =
        "class Animal:\n"
        + "    def __init__(self):\n"
        + "        pass\n"
        + "\n"
        + "class Dog(Animal):\n"
        + "    def __init__(self):\n"
        + "        super().__init__()\n"
        + "\n";

    /// <summary>Source element, target element, element literal, and whether Animal/Dog are needed.</summary>
    private static (string Source, string Target, string Literal, bool NeedsClasses) PairSpelling(string pair) => pair switch
    {
        "Widening" => ("int8", "int", "1", false),
        "Derived" => ("Dog", "Animal", "Dog()", true),
        "Identical" => ("int", "int", "1", false),
        _ => throw new ArgumentOutOfRangeException(nameof(pair), pair, "unknown pair"),
    };

    private static string Spell(string container, string element) => container switch
    {
        "InvariantList" => $"list[{element}]",
        "InvariantSet" => $"set[{element}]",
        "InvariantDictValue" => $"dict[str, {element}]",
        "CovariantIEnumerable" => $"IEnumerable[{element}]",
        "ContravariantCallback" => $"({element}) -> None",
        _ => throw new ArgumentOutOfRangeException(nameof(container), container, "unknown container"),
    };

    private static string Literal(string container, string element) => container switch
    {
        "InvariantList" or "CovariantIEnumerable" => $"[{element}]",
        "InvariantSet" => "{" + element + "}",
        "InvariantDictValue" => "{\"k\": " + element + "}",
        _ => throw new ArgumentOutOfRangeException(nameof(container), container, "no literal form"),
    };

    private static string MutationOperator(string container) => container switch
    {
        "InvariantList" => "+=",
        "InvariantSet" => "|=",
        "InvariantDictValue" => "|=",
        _ => throw new ArgumentOutOfRangeException(nameof(container), container, "no mutator"),
    };

    private static (string Source, CellExpectation Expectation, string? ExpectedOutput) ComposeCell(
        string container, string pair, string route)
    {
        if (NotApplicableCells.ContainsKey(Key(container, pair, route)))
            return ("", CellExpectation.NotApplicable, null);

        var (sourceElement, targetElement, elementLiteral, needsClasses) = PairSpelling(pair);
        var head = needsClasses ? ClassPrelude : "";
        var isCallback = container == "ContravariantCallback";

        // A contravariant position runs the pair BACKWARDS: the callback that accepts the WIDER element
        // is the one a slot expecting the narrower element admits.
        var sourceType = isCallback ? Spell(container, targetElement) : Spell(container, sourceElement);
        var targetType = isCallback ? Spell(container, sourceElement) : Spell(container, targetElement);

        string prelude;
        string local;
        string expression;
        if (isCallback)
        {
            prelude = $"def sink(v: {targetElement}) -> None:\n    print(\"ok\")\n\n";
            local = "";
            expression = "sink";
        }
        else if (container == "CovariantIEnumerable")
        {
            // An `IEnumerable[E]` value comes from a list: a bare literal in that slot types as
            // `list[int32]` and is refused for its own reasons, which would measure the wrong thing.
            prelude = "";
            local = $"    tmp: list[{sourceElement}] = [{elementLiteral}]\n    src: {sourceType} = tmp\n";
            expression = "src";
        }
        else
        {
            prelude = "";
            local = $"    src: {sourceType} = {Literal(container, elementLiteral)}\n";
            expression = "src";
        }

        var expectation = Expectation(container, pair, route);

        switch (route)
        {
            case "CallArgument":
                return (head + prelude
                    + $"def f(p: {targetType}) -> None:\n    print(\"ok\")\n\n"
                    + $"def main():\n{local}    f({expression})\n", expectation, "ok\n");

            case "Pipe":
                return (head + prelude
                    + $"def f(p: {targetType}) -> int:\n    return 7\n\n"
                    + $"def main():\n{local}    print({expression} |> f)\n", expectation, "7\n");

            case "Assignment":
                return (head + prelude
                    + $"def main():\n{local}    dst: {targetType} = {expression}\n    print(\"ok\")\n",
                    expectation, "ok\n");

            case "Return":
                return (head + prelude
                    + $"def g(v: {sourceType}) -> {targetType}:\n    return v\n\n"
                    + $"def main():\n{local}    g({expression})\n    print(\"ok\")\n", expectation, "ok\n");

            case "FieldStore":
                return (head + prelude
                    + $"class Holder:\n    val: {targetType}\n\n"
                    + $"    def __init__(self, v: {sourceType}) -> None:\n        self.val = v\n\n"
                    + $"def main():\n{local}    h: Holder = Holder({expression})\n    print(\"ok\")\n",
                    expectation, "ok\n");

            case "AugmentedMutation":
                {
                    // The mutator's receiver holds the TARGET element; the operand holds the source's.
                    var receiverLiteral = Literal(container, pair == "Derived" ? "Animal()" : elementLiteral);
                    return (head
                        + $"def main():\n    dst: {targetType} = {receiverLiteral}\n{local}"
                        + $"    dst {MutationOperator(container)} src\n    print(\"ok\")\n",
                        expectation, "ok\n");
                }

            case "OverloadSignatureMatch":
                return (head
                    + $"def f(p: {sourceType}) -> None:\n    print(\"ok\")\n\n"
                    + $"def main():\n    g: ({targetType}) -> None = f\n    print(\"ok\")\n",
                    expectation, "ok\n");

            case "TypeParameterInference":
                return (head + prelude
                    + $"def f[T](p: {Spell(container, "T")}) -> None:\n    print(\"ok\")\n\n"
                    + $"def main():\n{local}    f({expression})\n", expectation, "ok\n");

            default:
                throw new ArgumentOutOfRangeException(nameof(route), route, "unknown route");
        }
    }

    /// <summary>
    /// The contract, stated as a rule rather than a table of 105 literals: an identical pair is admitted
    /// everywhere; a generic type parameter INFERS the source's element, so that route always runs; an
    /// invariant container refuses both non-identical pairs at every store route; a declared-covariant
    /// container admits the reference pair and refuses the value-type pair.
    /// </summary>
    private static CellExpectation Expectation(string container, string pair, string route)
    {
        if (pair == "Identical" || route == "TypeParameterInference")
            return CellExpectation.Compiles;

        var isInvariant = container is "InvariantList" or "InvariantSet" or "InvariantDictValue";
        var isReferencePair = pair == "Derived";

        if (route == "AugmentedMutation")
        {
            // The mutators bind a COVARIANT `IEnumerable<T>`/`IReadOnlyDictionary<K,V>`: a reference
            // conversion on the element still copies in (list/set), while `dict |=` needs exact key AND
            // value types because its interface is invariant in both (#1682).
            if (isReferencePair && container is "InvariantList" or "InvariantSet")
                return CellExpectation.Compiles;
            return CellExpectation.RefusedSPY0222;
        }

        // A declared-variant container admits the reference pair — that is what `out T` means.
        if (!isInvariant && isReferencePair)
            return CellExpectation.Compiles;

        return route == "Return" ? CellExpectation.RefusedSPY0260 : CellExpectation.RefusedSPY0220;
    }

    private static string Key(string container, string pair, string route) => $"{container}×{pair}×{route}";

    private static readonly Dictionary<string, string> NotApplicableCells = new(StringComparer.Ordinal)
    {
        // ── No such route for the shape ──
        ["CovariantIEnumerable×Widening×AugmentedMutation"] = "IEnumerable is read-only: no in-place mutator to augment",
        ["CovariantIEnumerable×Derived×AugmentedMutation"] = "IEnumerable is read-only: no in-place mutator to augment",
        ["CovariantIEnumerable×Identical×AugmentedMutation"] = "IEnumerable is read-only: no in-place mutator to augment",
        ["ContravariantCallback×Widening×AugmentedMutation"] = "a callback is not a collection: no augmented mutator",
        ["ContravariantCallback×Derived×AugmentedMutation"] = "a callback is not a collection: no augmented mutator",
        ["ContravariantCallback×Identical×AugmentedMutation"] = "a callback is not a collection: no augmented mutator",
        ["ContravariantCallback×Widening×OverloadSignatureMatch"] = "the slot would be a nested function type ((T) -> None) -> None, a different construct",
        ["ContravariantCallback×Derived×OverloadSignatureMatch"] = "the slot would be a nested function type ((T) -> None) -> None, a different construct",
        ["ContravariantCallback×Identical×OverloadSignatureMatch"] = "the slot would be a nested function type ((T) -> None) -> None, a different construct",

        // ── #1833: the FUNCTION-TYPE arms of IsAssignable are not variance-correct ──
        // These six cells are SPY0908 at 311252e33 and at fd5c1321a alike (pre-existing, not a
        // regression from the variance work). They drain when #1833 lands.
        ["ContravariantCallback×Widening×CallArgument"] = "#1833: Action<int> accepted for Action<sbyte> — .NET has no variance over value types (CS1503)",
        ["ContravariantCallback×Widening×Pipe"] = "#1833: Action<int> accepted for Action<sbyte> (CS1503)",
        ["ContravariantCallback×Widening×Assignment"] = "#1833: Action<int> accepted for Action<sbyte> (CS1503)",
        ["ContravariantCallback×Widening×Return"] = "#1833: Action<int> accepted for Action<sbyte> (CS0029)",
        ["ContravariantCallback×Widening×FieldStore"] = "#1833: Action<int> accepted for Action<sbyte> (CS0029)",
        ["CovariantIEnumerable×Derived×OverloadSignatureMatch"] = "#1833: delegate parameter positions are compared in EITHER direction, so an unsound narrowing is accepted (CS0123)",
    };

    public static IEnumerable<object[]> ApplicableCells =>
        from c in Containers
        from p in Pairs
        from r in Routes
        where !NotApplicableCells.ContainsKey(Key(c, p, r))
        select new object[] { c, p, r };

    [Theory]
    [MemberData(nameof(ApplicableCells))]
    public void Cell_GenericArgumentVarianceIsDecidedByName(string container, string pair, string route)
    {
        var (source, expectation, expectedOutput) = ComposeCell(container, pair, route);
        expectation.Should().NotBe(CellExpectation.NotApplicable);

        var result = CompileAndExecute(source);
        var label = Key(container, pair, route);
        var seen = string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"));

        if (expectation == CellExpectation.Compiles)
        {
            result.Success.Should().BeTrue(
                $"[{label}] must compile and run. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
            result.StandardOutput.Should().Be(expectedOutput, $"[{label}]\n{source}");
            return;
        }

        var expectedCode = expectation switch
        {
            CellExpectation.RefusedSPY0220 => DiagnosticCodes.Semantic.TypeMismatch,
            CellExpectation.RefusedSPY0260 => DiagnosticCodes.Semantic.MissingReturnValue,
            CellExpectation.RefusedSPY0222 => DiagnosticCodes.Semantic.InvalidBinaryOperation,
            _ => throw new InvalidOperationException($"[{label}] unhandled expectation {expectation}"),
        };

        result.Success.Should().BeFalse(
            $"[{label}] must be refused — a variance-incorrect generic argument\n{source}");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == expectedCode,
            $"[{label}] must report {expectedCode} by NAME (an empty diagnostic list means it regressed to "
            + $"a generated-C# failure). Got: {seen}\n{source}");
    }

    [Fact]
    public void Matrix_IsTotalOverItsAxes()
    {
        Containers.Length.Should().Be(VarianceCount);
        Pairs.Length.Should().Be(PairCount);
        Routes.Length.Should().Be(RouteCount);
        Containers.Should().OnlyHaveUniqueItems();
        Pairs.Should().OnlyHaveUniqueItems();
        Routes.Should().OnlyHaveUniqueItems();

        var product = VarianceCount * PairCount * RouteCount;
        var applicable = ApplicableCells.Count();
        var notApplicable = NotApplicableCells.Count;

        notApplicable.Should().Be(NotApplicableCellCount, "every N/A cell is written down with its reason");
        var allKeys = (from c in Containers from p in Pairs from r in Routes select Key(c, p, r)).ToHashSet();
        NotApplicableCells.Keys.Should().OnlyContain(k => allKeys.Contains(k), "an N/A key must name a real cell");
        (applicable + notApplicable).Should().Be(product,
            $"applicable ({applicable}) + N/A ({notApplicable}) must be the whole product "
            + $"({VarianceCount} × {PairCount} × {RouteCount})");
    }

    /// <summary>
    /// The two cells #1748 names, asserted on their MESSAGES rather than their codes: the call route
    /// (n35) and the pipe route (n36), which regressed from this exact message to SPY0908 CS1503 when
    /// `list` was declared covariant.
    /// </summary>
    [Fact]
    public void ListElementWidening_IsRefusedByNameAtBothTheCallAndThePipeRoute()
    {
        const string callSite =
            "def first(xs: list[int]) -> int:\n    return xs[0]\n\n"
            + "def main():\n    ys: list[int8] = [1, 2]\n    print(first(ys))\n";
        const string pipeSite =
            "def first(xs: list[int]) -> int:\n    return xs[0]\n\n"
            + "def main():\n    ys: list[int8] = [1, 2]\n    print(ys |> first)\n";

        var call = CompileAndExecute(callSite);
        call.Success.Should().BeFalse();
        call.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.TypeMismatch
                && d.Message.Contains("list[int8]", StringComparison.Ordinal)
                && d.Message.Contains("list[int32]", StringComparison.Ordinal),
            "n35: the call route names both element spellings. Got: "
            + string.Join(" | ", call.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}")));

        var pipe = CompileAndExecute(pipeSite);
        pipe.Success.Should().BeFalse();
        pipe.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.TypeMismatch
                && d.Message.Contains("Cannot pipe value", StringComparison.Ordinal)
                && d.Message.Contains("list[int8]", StringComparison.Ordinal),
            "n36: the pipe route keeps its own message. Got: "
            + string.Join(" | ", pipe.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}")));
    }

    /// <summary>
    /// The registry declares no variance for <c>list</c>/<c>set</c>: their CLR types are invariant
    /// classes. A re-added override would make the invariant arm unreachable for exactly the two
    /// containers #1748 is about, which is how the covariance override hid it.
    /// </summary>
    [Fact]
    public void BuiltinRegistry_DeclaresNoVarianceForListOrSet()
    {
        var registry = new Sharpy.Compiler.Semantic.Registry.BuiltinRegistry();

        foreach (var name in new[] { "list", "set" })
        {
            var symbol = registry.GetType(name);
            symbol.Should().NotBeNull($"'{name}' must be registered");
            symbol!.TypeParameters.Should().NotBeEmpty($"'{name}' is generic");
            symbol.TypeParameters.Should().OnlyContain(
                tp => tp.Variance == Sharpy.Compiler.Parser.Ast.TypeParameterVariance.None,
                $"'{name}' is invariant — Sharpy.{(name == "list" ? "List" : "Set")}<T> is an invariant class in C# "
                + "(generic_variance.md, Axiom 1)");
        }

        // Positive control: a type that IS declared variant reads as variant through the same call, so
        // this test cannot pass by reading nothing.
        var enumerable = registry.GetType("IEnumerable");
        enumerable.Should().NotBeNull();
        enumerable!.TypeParameters.Should().Contain(
            tp => tp.Variance == Sharpy.Compiler.Parser.Ast.TypeParameterVariance.Covariant,
            "IEnumerable[T] is covariant — the registry reads CLR variance, and this is the control that it does");
    }

    [Fact]
    public void AssignabilityAllowlist_IsEmpty()
    {
        var repoRoot = Infrastructure.DispatchSiteScan.FindRepoRoot();
        var path = Path.Combine(repoRoot, "src", "Sharpy.Compiler.Tests",
            "Conformance", "assignability-allowlist.txt");
        var lines = File.ReadAllLines(path)
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith('#'))
            .ToList();
        lines.Should().BeEmpty(
            "every assignability-allowlist row was drained by routing through IsAssignable (#1748)");
    }
}
