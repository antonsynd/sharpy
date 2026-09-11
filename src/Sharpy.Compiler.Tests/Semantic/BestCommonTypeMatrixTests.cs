using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Best-common-type matrix — conditional branch pairs, truthiness distribution, and operand joins
/// (#1677, #1743, #1796, R-W, R-K, R-AB).
///
/// <para><b>Conditional contract.</b> A conditional expression is typed by <c>BestCommonType</c>:
/// arm 1, the SLOT decides and each branch is admitted or REFUSED AT ITS OWN SPAN
/// (<c>AdmitConditionalBranchesIntoSlot</c>); arm 2, one operand's type accepts every other; arm 3,
/// refused by name with an annotate steer. A conditional in a truthiness position distributes — the
/// test wraps each branch individually, and a nested conditional distributes too.</para>
///
/// <para><b>Operand-join contract.</b> Every site that joins element types — list, set, dict,
/// tuple (per index, <c>DecideTupleIndexTypes</c>), <c>dict(**kw)</c>, comprehension element,
/// or-pattern capture — calls <c>BestCommonType</c>. <c>None</c> is untyped in it (R-AB).
/// <c>FindLeastCommonAncestor</c> no longer exists.</para>
///
/// <para><b>Every runnable cell asserts stdout.</b> A join that compiles with the wrong type is
/// the defect these issues are about; only a value comparison catches it (contract §4).</para>
/// </summary>
[Collection("HeavyCompilation")]
public class BestCommonTypeMatrixTests : IntegrationTestBase
{
    public BestCommonTypeMatrixTests(ITestOutputHelper output) : base(output) { }

    /// <summary>
    /// Three classes whose <c>name()</c> is <c>@virtual</c>/<c>@override</c>, so a cell's printed
    /// value names the RUNTIME type. Sharpy methods are NOT virtual by default — without the
    /// decorators an <c>Animal</c>-slotted <c>Dog</c> prints "animal" and every cell below would
    /// print the same thing whichever type the rule decided, which proves nothing. The STATIC half
    /// of the claim is read separately, by <see cref="Arm2_JoinIsNotTheFirstOperand"/>.
    /// </summary>
    private const string AnimalPrelude =
        "class Animal:\n"
        + "    @virtual\n"
        + "    def name(self) -> str:\n"
        + "        return \"animal\"\n\n"
        + "    def __str__(self) -> str:\n"
        + "        return self.name()\n\n"
        + "class Dog(Animal):\n"
        + "    @override\n"
        + "    def name(self) -> str:\n"
        + "        return \"dog\"\n\n"
        + "    def fetch(self) -> str:\n"
        + "        return \"ball\"\n\n"
        + "class Cat(Animal):\n"
        + "    @override\n"
        + "    def name(self) -> str:\n"
        + "        return \"cat\"\n\n";

    /// <summary>Two <c>__bool__</c> classes with UNRELATED types — the CS0173 pair of #1743.</summary>
    private const string BoolPairPrelude =
        "class Yes:\n"
        + "    def __bool__(self) -> bool:\n"
        + "        return False\n\n"
        + "class No:\n"
        + "    def __bool__(self) -> bool:\n"
        + "        return True\n\n";

    // ══ Part 1: arm 1 — the slot decides, at every position ═════════════════════════════════

    [Theory]
    [InlineData("Declaration",
        "def main():\n    c = True\n    a: Animal = Dog() if c else Cat()\n    print(a.name())\n", "dog\n")]
    [InlineData("PlainStore",
        "def main():\n    c = True\n    a: Animal = Animal()\n    a = Dog() if c else Cat()\n    print(a.name())\n", "dog\n")]
    [InlineData("ArgumentPositional",
        "def show(a: Animal) -> None:\n    print(a.name())\n\ndef main():\n    c = True\n    show(Dog() if c else Cat())\n", "dog\n")]
    [InlineData("ArgumentKeyword",
        "def show(a: Animal) -> None:\n    print(a.name())\n\ndef main():\n    c = True\n    show(a=Dog() if c else Cat())\n", "dog\n")]
    [InlineData("Return",
        "def pick(c: bool) -> Animal:\n    return Dog() if c else Cat()\n\ndef main():\n    print(pick(True).name())\n", "dog\n")]
    [InlineData("Yield",
        "def gen(c: bool) -> Animal:\n    yield Dog() if c else Cat()\n\ndef main():\n    for a in gen(True):\n        print(a.name())\n", "dog\n")]
    [InlineData("ListElement",
        "def main():\n    c = True\n    xs: list[Animal] = [Dog() if c else Cat()]\n    print(xs[0].name())\n", "dog\n")]
    [InlineData("TupleElement",
        "def main():\n    c = True\n    t: tuple[Animal, int] = (Dog() if c else Cat(), 1)\n    print(t[0].name())\n    print(t[1])\n", "dog\n1\n")]
    [InlineData("DictValue",
        "def main():\n    c = True\n    d: dict[str, Animal] = {\"k\": Dog() if c else Cat()}\n    print(d[\"k\"].name())\n", "dog\n")]
    [InlineData("LambdaBody",
        "def main():\n    c = True\n    f: () -> Animal = lambda: Dog() if c else Cat()\n    print(f().name())\n", "dog\n")]
    [InlineData("ReceiverElement",
        "def main():\n    c = True\n    xs: list[Animal] = []\n    xs.append(Dog() if c else Cat())\n    print(xs[0].name())\n", "dog\n")]
    [InlineData("ObjectSlot",
        "def main():\n    c = True\n    o: object = None if c else 1\n    print(o)\n", "None\n")]
    // The hole's slot is `object` (Design Decision 3), so the conditional is admitted there and
    // the emitted interpolation prints the RUNTIME type's name. A conditional in the RECEIVER of a
    // member access is deliberately NOT this cell: a receiver is not a store, so that spelling is
    // slot-less and arm 3 refuses it (at this sha and at f84701e04 alike).
    [InlineData("FStringHole",
        "def main():\n    c = True\n    print(f\"{Dog() if c else Cat()}\")\n", "dog\n")]
    [InlineData("ConstantSlot",
        "def main():\n    c = True\n    x: int8 = 7 if c else 8\n    print(x)\n", "7\n")]
    [InlineData("NullableSlot",
        "def main():\n    c = True\n    s: str | None = (q := None) if c else \"a\"\n    print(s)\n", "None\n")]
    public void Arm1Cell_SlotDecidesAndTheValueSurvives(string label, string body, string expected)
    {
        var source = AnimalPrelude + body;
        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{label}] must never produce SPY0908. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.Success.Should().BeTrue(
            $"[{label}] must compile. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be(expected,
            $"[{label}] prints the value the slot decided — a recorded join that never reaches the "
            + $"output fails here\n{source}");
    }

    // ══ Part 2: arm 1 — a mistyped branch is refused AT ITS OWN SPAN ═════════════════════════
    // The cell that #1743's close criterion turns on. Each row pins the CODE, the LINE and the
    // COLUMN, because "refused at its span" is exactly the claim a code-only assertion cannot make.

    [Theory]
    [InlineData("Declaration_ElseBranch",
        "def main():\n    c = True\n    a: Animal = Dog() if c else \"x\"\n    print(a.name())\n",
        DiagnosticCodes.Semantic.TypeMismatch, "Cannot assign type 'str' to variable of type 'Animal'", 3, 33)]
    [InlineData("Declaration_ThenBranch",
        "def main():\n    c = True\n    a: Animal = \"x\" if c else Dog()\n    print(a.name())\n",
        DiagnosticCodes.Semantic.TypeMismatch, "Cannot assign type 'str' to variable of type 'Animal'", 3, 17)]
    [InlineData("ListElement_ElseBranch",
        "def main():\n    c = True\n    xs: list[Animal] = [Dog() if c else \"x\"]\n    print(len(xs))\n",
        DiagnosticCodes.Semantic.TypeMismatch, "Cannot assign type 'str' to 'Animal'", 3, 41)]
    [InlineData("Argument_ElseBranch",
        "def show(a: Animal) -> None:\n    print(a.name())\n\ndef main():\n    c = True\n    show(Dog() if c else \"x\")\n",
        DiagnosticCodes.Semantic.TypeMismatch, "Cannot pass argument of type 'str'", 6, 26)]
    [InlineData("Return_ElseBranch",
        "def pick(c: bool) -> Animal:\n    return Dog() if c else \"x\"\n\ndef main():\n    print(pick(True).name())\n",
        DiagnosticCodes.Semantic.MissingReturnValue, "Cannot return type 'str'", 2, 28)]
    [InlineData("DictValue_ElseBranch",
        "def main():\n    c = True\n    d: dict[str, Animal] = {\"k\": Dog() if c else \"x\"}\n    print(len(d))\n",
        DiagnosticCodes.Semantic.TypeMismatch, "Cannot assign type 'str' to 'Animal'", 3, 50)]
    public void MistypedBranchUnderSlot_IsRefusedAtTheBranchSpan(
        string label, string body, string code, string message, int line, int column)
    {
        var source = AnimalPrelude + body;
        var preludeLines = AnimalPrelude.Count(ch => ch == '\n');
        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{label}] must be refused at SEMANTIC time — an ICE is the defect, not the answer. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.Success.Should().BeFalse($"[{label}] must be refused\n{source}");

        var matching = result.RawDiagnostics.Where(d => d.Code == code).ToList();
        matching.Should().HaveCount(1,
            $"[{label}] must report {code} exactly ONCE — the branch's refusal, not also the "
            + $"enclosing store's. Got: "
            + $"{string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}@{d.Line}:{d.Column}: {d.Message}"))}\n{source}");
        matching[0].Message.Should().Contain(message, $"[{label}]\n{source}");
        matching[0].Line.Should().Be(preludeLines + line,
            $"[{label}] must report on the branch's own line\n{source}");
        matching[0].Column.Should().Be(column,
            $"[{label}] must report at the BRANCH's column, not the statement's\n{source}");
    }

    // ══ Part 3: arm 2 — one operand's type accepts every other ══════════════════════════════
    // The discriminating pair: the join is the BASE, so a base-typed read runs and a
    // DERIVED-only member is refused. A mutation that returns "the first operand's type" makes
    // the second cell compile, which no output comparison on the first cell would notice.

    [Fact]
    public void Arm2_JoinIsTheTypeThatAcceptsAll()
    {
        var source = AnimalPrelude
            + "def main():\n    c = True\n    a = Dog() if c else Animal()\n    print(a.name())\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(
            $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be("dog\n", source);
    }

    [Fact]
    public void Arm2_JoinIsNotTheFirstOperand()
    {
        var source = AnimalPrelude
            + "def main():\n    c = True\n    a = Dog() if c else Animal()\n    print(a.fetch())\n";
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse(
            "the join of Dog and Animal is Animal, so a Dog-only member is not on it — a join that "
            + $"returned the FIRST operand's type would compile this\n{source}");
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"the refusal is semantic, not an ICE\n{source}");
    }

    [Theory]
    [InlineData("IntFloat", "def main():\n    c = True\n    x = 1 if c else 2.5\n    print(x)\n", "1.0\n")]
    [InlineData("IntLong", "def main():\n    c = True\n    x = 1 if c else 10000000000\n    print(x)\n", "1\n")]
    [InlineData("SameType", "def main():\n    c = True\n    x = 1 if c else 2\n    print(x)\n", "1\n")]
    public void Arm2Cell_RunsAndPrints(string label, string body, string expected)
    {
        var result = CompileAndExecute(body);
        result.Success.Should().BeTrue(
            $"[{label}] Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{body}");
        result.StandardOutput.Should().Be(expected, $"[{label}]\n{body}");
    }

    // ══ Part 4: arm 3 — refused by name, with a steer that is real source ═══════════════════

    [Theory]
    [InlineData("UnrelatedClasses",
        "def main():\n    c = True\n    a = Dog() if c else Cat()\n", new[] { "'Dog'", "'Cat'" })]
    [InlineData("IntStr",
        "def main():\n    c = True\n    a = 1 if c else \"x\"\n", new[] { "'int32'", "'str'" })]
    [InlineData("NoneInt",
        "def main():\n    c = True\n    a = None if c else 1\n", new[] { "'int32'", "'None'" })]
    [InlineData("BoolPair",
        "def main():\n    c = True\n    a = Yes() if c else No()\n", new[] { "'Yes'", "'No'" })]
    public void Arm3Cell_RefusesByNameWithARealSteer(string label, string body, string[] typeNames)
    {
        var source = AnimalPrelude + BoolPairPrelude + body;
        var result = CompileAndExecute(source);

        result.Success.Should().BeFalse($"[{label}] must be refused\n{source}");
        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{label}] must be refused at semantic time, not by Roslyn (CS0173)\n{source}");

        var diagnostic = result.RawDiagnostics
            .FirstOrDefault(d => d.Code == DiagnosticCodes.Semantic.CannotInferType);
        diagnostic.Should().NotBeNull(
            $"[{label}] must report SPY0227. Got: "
            + $"{string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))}\n{source}");

        foreach (var name in typeNames)
        {
            diagnostic!.Message.Should().Contain(name,
                $"[{label}] must NAME the operand types the user has to reconcile\n{source}");
        }

        diagnostic!.Message.Should().Contain("annotate the target",
            $"[{label}] must steer to an annotation\n{source}");
        diagnostic.Message.Should().NotContain("conditional expression: T",
            $"[{label}] must not interpolate the site noun INTO the steer — "
            + $"'conditional expression: T = ...' is not a program\n{source}");
    }

    // ══ Part 5: truthiness distributes, at every truthiness position ════════════════════════

    [Theory]
    [InlineData("If", "def main():\n    flag = True\n    if Yes() if flag else No():\n        print(\"t\")\n    else:\n        print(\"f\")\n", "f\n")]
    [InlineData("Elif", "def main():\n    flag = True\n    if False:\n        print(\"x\")\n    elif Yes() if flag else No():\n        print(\"t\")\n    else:\n        print(\"f\")\n", "f\n")]
    [InlineData("While", "def main():\n    flag = True\n    while Yes() if flag else No():\n        print(\"never\")\n    print(\"done\")\n", "done\n")]
    [InlineData("Not", "def main():\n    flag = True\n    print(not (Yes() if flag else No()))\n", "True\n")]
    [InlineData("AndRhs", "def main():\n    flag = True\n    print(True and (Yes() if flag else No()))\n", "False\n")]
    [InlineData("OrRhs", "def main():\n    flag = True\n    print(False or (Yes() if flag else No()))\n", "False\n")]
    [InlineData("Assert", "def main():\n    flag = True\n    assert No() if flag else Yes()\n    print(\"ok\")\n", "ok\n")]
    [InlineData("TernaryTest", "def main():\n    flag = True\n    print(\"t\" if (Yes() if flag else No()) else \"f\")\n", "f\n")]
    [InlineData("ComprehensionCondition", "def main():\n    flag = True\n    xs = [i for i in range(2) if (Yes() if flag else No())]\n    print(len(xs))\n", "0\n")]
    [InlineData("Nested", "def main():\n    flag = True\n    g = False\n    if (\"a\" if g else 0) if flag else 1:\n        print(\"t\")\n    else:\n        print(\"f\")\n", "f\n")]
    public void TruthinessCell_DistributesPerBranch(string label, string body, string expected)
    {
        var source = BoolPairPrelude + body;
        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{label}] must never produce SPY0908 — an undistributed ternary is CS0173. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.Success.Should().BeTrue(
            $"[{label}] must compile — a conditional in a truthiness position needs no type of its "
            + $"own. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be(expected, $"[{label}] must agree with python3\n{source}");
    }

    // ── TruthinessLowering totality: one EXECUTING cell per enum member ─────────────────────
    // `WrapTruthinessIfNeeded` ends in `_ => expr`, so a member with no emitter arm passes the
    // expression through silently. Each cell's operand is a form C# cannot use as a condition on
    // its own, so a missing arm is a compile failure, not a wrong answer. The count is anchored to
    // a LITERAL and compared with the enum, so adding a member fails until its cell exists.

    private const int TruthinessLoweringMemberCount = 13;

    [Theory]
    [InlineData("NativeBool", "def main():\n    b: bool = False\n    if b:\n        print(\"t\")\n    else:\n        print(\"f\")\n", "f\n")]
    [InlineData("IntNotZero", "def main():\n    x: int = 0\n    if x:\n        print(\"t\")\n    else:\n        print(\"f\")\n", "f\n")]
    [InlineData("FloatNotZero", "def main():\n    x: float = 0.0\n    if x:\n        print(\"t\")\n    else:\n        print(\"f\")\n", "f\n")]
    [InlineData("LongNotZero", "def main():\n    x: int64 = 0\n    if x:\n        print(\"t\")\n    else:\n        print(\"f\")\n", "f\n")]
    [InlineData("StringNotEmpty", "def main():\n    s: str = \"\"\n    if s:\n        print(\"t\")\n    else:\n        print(\"f\")\n", "f\n")]
    [InlineData("BytesNotEmpty", "def main():\n    b: bytes = b\"\"\n    if b:\n        print(\"t\")\n    else:\n        print(\"f\")\n", "f\n")]
    [InlineData("CollectionNotEmpty", "def main():\n    xs: list[int] = []\n    if xs:\n        print(\"t\")\n    else:\n        print(\"f\")\n", "f\n")]
    [InlineData("OptionalIsSome", "def main():\n    o: int? = None()\n    if o:\n        print(\"t\")\n    else:\n        print(\"f\")\n", "f\n")]
    [InlineData("NullableNotNull", "def main():\n    n: int | None = None\n    if n:\n        print(\"t\")\n    else:\n        print(\"f\")\n", "f\n")]
    [InlineData("BoolConvertible", "def main():\n    y = Yes()\n    if y:\n        print(\"t\")\n    else:\n        print(\"f\")\n", "f\n")]
    [InlineData("SizedNotEmpty", "class Bag:\n    def __len__(self) -> int:\n        return 0\n\ndef main():\n    b = Bag()\n    if b:\n        print(\"t\")\n    else:\n        print(\"f\")\n", "f\n")]
    [InlineData("AlwaysFalse", "def main():\n    if None:\n        print(\"t\")\n    else:\n        print(\"f\")\n", "f\n")]
    [InlineData("Distributed", "def main():\n    flag = True\n    if Yes() if flag else No():\n        print(\"t\")\n    else:\n        print(\"f\")\n", "f\n")]
    public void TruthinessLoweringMember_HasAnExecutingCell(string member, string body, string expected)
    {
        var source = BoolPairPrelude + body;
        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{member}] a missing emitter arm passes the raw expression through and Roslyn refuses "
            + $"it. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.Success.Should().BeTrue(
            $"[{member}] Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be(expected, $"[{member}]\n{source}");
    }

    [Fact]
    public void TruthinessLowering_EveryMemberHasACell()
    {
        var members = Enum.GetNames(typeof(Sharpy.Compiler.Semantic.TruthinessLowering));
        members.Length.Should().Be(TruthinessLoweringMemberCount,
            "the member count is anchored to a LITERAL — adding a member is a decision that costs "
            + $"an executing cell. Found: [{string.Join(", ", members)}]");

        var celled = typeof(BestCommonTypeMatrixTests)
            .GetMethod(nameof(TruthinessLoweringMember_HasAnExecutingCell))!
            .GetCustomAttributes(typeof(InlineDataAttribute), false)
            .Cast<InlineDataAttribute>()
            .Select(d => (string)d.GetData(null!).First()[0]!)
            .ToList();

        celled.Should().BeEquivalentTo(members,
            "every TruthinessLowering member has exactly one EXECUTING cell, and every cell names a "
            + "real member — Design Decision 5. A member with no emitter arm falls through "
            + "WrapTruthinessIfNeeded's `_ => expr` default and the raw expression reaches Roslyn");
    }

    // ── Truthiness one-site scan: EXACTLY the callers the seam declares ─────────────────────

    private const int ExpectedSetTruthinessLoweringCallers = 5;

    [Fact]
    public void SetTruthinessLowering_HasExactlyTheSeamsOwnCallers()
    {
        var repoRoot = Infrastructure.DispatchSiteScan.FindRepoRoot();
        var semanticDir = Path.Combine(repoRoot, "src", "Sharpy.Compiler", "Semantic");
        var files = Directory.GetFiles(semanticDir, "*.cs", SearchOption.AllDirectories);
        files.Should().NotBeEmpty("positive control: the scan must read real files");

        var perFile = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            if (name.Equals("SemanticInfo.cs", StringComparison.Ordinal))
                continue;   // the declaration, not a caller

            var count = CountOccurrences(File.ReadAllText(file), "SetTruthinessLowering(");
            if (count > 0)
                perFile[name] = count;
        }

        // The seam is `CheckTruthinessTest` plus the conditional's own distribution. Both live in
        // exactly two files; a third file recording a truthiness fact is a position that bypassed
        // the seam, which is the class #1743 closed.
        perFile.Keys.Should().BeEquivalentTo(
            new[] { "TypeChecker.Expressions.Operators.cs", "TypeChecker.Utilities.cs" },
            "only the truthiness seam and the conditional's distribution record the fact — "
            + $"found: [{string.Join(", ", perFile.Select(kv => $"{kv.Key}:{kv.Value}"))}]");

        perFile.Values.Sum().Should().Be(ExpectedSetTruthinessLoweringCallers,
            "the caller count is EXACT, not a bound: a second call added at a position that should "
            + "go through the seam fails here. Found: "
            + $"[{string.Join(", ", perFile.Select(kv => $"{kv.Key}:{kv.Value}"))}]");
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int idx = 0;
        while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += pattern.Length;
        }
        return count;
    }

    // ══ Part 6: operand joins — every site, every operand family ════════════════════════════
    // Each row asserts stdout, so a join that compiles with the wrong element type fails here.

    [Theory]
    [InlineData("List_DogAnimal_Arm2",
        "def main():\n    xs = [Dog(), Animal()]\n    print(xs[0].name())\n    print(xs[1].name())\n", "dog\nanimal\n")]
    [InlineData("List_IntFloat_Arm2",
        "def main():\n    xs = [1, 2.5]\n    print(xs[0] + 0.5)\n    print(xs[1])\n", "1.5\n2.5\n")]
    [InlineData("List_IntLong_Arm2",
        "def main():\n    xs = [1, 10000000000]\n    print(xs[1] + 1)\n", "10000000001\n")]
    [InlineData("List_Slot_DogCat",
        "def main():\n    xs: list[Animal] = [Dog(), Cat()]\n    print(xs[0].name())\n    print(xs[1].name())\n", "dog\ncat\n")]
    [InlineData("List_Slot_NoneAndInt",
        "def main():\n    xs: list[int | None] = [None, 1]\n    print(xs[0])\n    print(xs[1])\n", "None\n1\n")]
    [InlineData("Tuple_IntStr_PerIndex",
        "def main():\n    t = (1, \"a\")\n    print(t[0])\n    print(t[1])\n", "1\na\n")]
    [InlineData("Tuple_Slot_NoneAndInt",
        "def main():\n    t: tuple[int | None, int] = (None, 1)\n    print(t[0])\n    print(t[1])\n", "None\n1\n")]
    [InlineData("Comprehension_Slot_None",
        "def main():\n    xs: list[str | None] = [None for i in range(2)]\n    print(xs[0])\n    print(len(xs))\n", "None\n2\n")]
    [InlineData("Dict_Slot_Values",
        "def main():\n    d: dict[str, object] = {\"a\": 1, \"b\": \"x\"}\n    print(d[\"a\"])\n    print(d[\"b\"])\n", "1\nx\n")]
    [InlineData("DictKw_Slot_Values",
        "def main():\n    d: dict[str, object] = dict(a=1, b=\"x\")\n    print(d[\"a\"])\n    print(d[\"b\"])\n", "1\nx\n")]
    [InlineData("OrPattern_Capture_Scrutinee",
        "def main():\n    o: object = 1.5\n    match o:\n        case float() as v:\n            print(v)\n        case list() as v:\n            print(v)\n        case _:\n            print(\"other\")\n", "1.5\n")]
    public void OperandJoinCell_RunsAndPrintsTheJoinedValues(string label, string body, string expected)
    {
        var source = AnimalPrelude + body;
        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{label}] must never produce SPY0908. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.Success.Should().BeTrue(
            $"[{label}] must compile. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be(expected,
            $"[{label}] prints the joined values — a join that compiles with the WRONG element type "
            + $"is the defect, and only this comparison sees it\n{source}");
    }

    [Theory]
    [InlineData("List_NoneAndInt", "def main():\n    xs = [None, 1]\n")]
    [InlineData("List_IntStr", "def main():\n    xs = [1, \"a\"]\n")]
    [InlineData("List_DogCat", "def main():\n    xs = [Dog(), Cat()]\n")]
    [InlineData("List_NoneOnly", "def main():\n    xs = [None, None]\n")]
    [InlineData("List_Nested", "def main():\n    xs = [[1], [\"a\"]]\n")]
    [InlineData("Set_NoneAndInt", "def main():\n    xs = {None, 1}\n")]
    [InlineData("Set_IntStr", "def main():\n    xs = {1, \"a\"}\n")]
    [InlineData("Dict_IntStr_Values", "def main():\n    d = {\"a\": 1, \"b\": \"x\"}\n")]
    [InlineData("DictKw_IntStr", "def main():\n    d = dict(a=1, b=\"x\")\n")]
    [InlineData("Tuple_NoneAndInt_Unpacked", "def main():\n    a, b = None, 1\n")]
    [InlineData("Tuple_NoneAndInt_Bound", "def main():\n    t = (None, 1)\n    print(t[1])\n")]
    [InlineData("Tuple_ForTarget", "def main():\n    for s, n in [(None, 1)]:\n        print(n)\n")]
    [InlineData("Comprehension_None", "def main():\n    xs = [None for i in range(2)]\n")]
    [InlineData("StarredUnpack_None", "def main():\n    a, *rest = None, 1, 2\n")]
    [InlineData("SlotLessConditionalElement", "def main():\n    c = True\n    xs = [Dog() if c else Cat()]\n")]
    public void OperandJoinCell_SlotLessAndUnrelated_IsRefusedByName(string label, string body)
    {
        var source = AnimalPrelude + body;
        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{label}] must be refused at SEMANTIC time — `void` in the emitted C# (SPY0599) and "
            + $"CS0173 are both the defect. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.Success.Should().BeFalse($"[{label}] must be refused\n{source}");
        result.RawDiagnostics.Should().Contain(
            d => d.Code == DiagnosticCodes.Semantic.CannotInferType,
            $"[{label}] must report SPY0227. Got: "
            + $"{string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))}\n{source}");
    }

    // ══ Part 7: the retired join, and the two named checks ══════════════════════════════════

    [Fact]
    public void FindLeastCommonAncestor_IsDeleted()
    {
        var (files, compilerDir) = CompilerSourceFiles();

        foreach (var name in new[] { "FindLeastCommonAncestor", "ResolveVoidElementType" })
        {
            var hits = files.Where(f => File.ReadAllText(f).Contains(name, StringComparison.Ordinal))
                .Select(f => Path.GetRelativePath(compilerDir, f))
                .ToList();
            hits.Should().BeEmpty($"'{name}' is retired by R-W and must have no references");
        }

        // Positive control: the SAME scan finds the rule that replaced them. Without it an
        // enumeration that returned nothing would report both absences as measurements.
        files.Where(f => File.ReadAllText(f).Contains("BestCommonType(", StringComparison.Ordinal))
            .Should().NotBeEmpty(
                "positive control: the scan reads real files, so the two absences above are "
                + "measurements rather than an empty enumeration");
    }

    [Fact]
    public void GetTypeKey_IsDeleted()
    {
        var (files, _) = CompilerSourceFiles();
        var utilities = files.Single(f => Path.GetFileName(f) == "TypeChecker.Utilities.cs");
        var content = File.ReadAllText(utilities);

        content.Should().NotContain("GetTypeKey(",
            "GetTypeKey was the LCA's only caller and is deleted with it");

        // Positive control on the SAME file: the identity authority that replaced it is there.
        content.Should().Contain("CanonicalKey",
            "positive control: the file was read, and it names the authority GetTypeKey was "
            + "replaced by");
    }

    /// <summary>
    /// The two checks the plan's close criteria require BY NAME (#1743's slot-directed arm and
    /// #1796's per-index tuple seam). A close criterion that greps to nothing is the "closed but
    /// unfinished" shape (contract §8) — and each name is reached by a cell above.
    /// </summary>
    [Theory]
    [InlineData("AdmitConditionalBranchesIntoSlot", "TypeChecker.Expressions.Operators.cs", "#1743")]
    [InlineData("DecideTupleIndexTypes", "TypeChecker.Expressions.Literals.cs", "#1796")]
    [InlineData("JoinCollectionOperands", "TypeChecker.BestCommonType.cs", "R-W arm 1")]
    [InlineData("NestedTargetSlot", "TypeChecker.Statements.Patterns.cs", "#1707")]
    public void NamedCheck_ExistsAndIsCalled(string name, string declaringFile, string issue)
    {
        var (files, compilerDir) = CompilerSourceFiles();

        var declaring = files.SingleOrDefault(f => Path.GetFileName(f) == declaringFile);
        declaring.Should().NotBeNull($"{declaringFile} must exist ({issue})");
        var declaringText = File.ReadAllText(declaring!);
        declaringText.Should().Contain($"{name}(",
            $"'{name}' is {issue}'s named check and must exist by name");

        var callers = files
            .Where(f => CountOccurrences(File.ReadAllText(f), $"{name}(") > 0)
            .Select(f => Path.GetRelativePath(compilerDir, f))
            .ToList();
        var totalMentions = files.Sum(f => CountOccurrences(File.ReadAllText(f), $"{name}("));

        totalMentions.Should().BeGreaterThan(1,
            $"'{name}' must have at least one CALLER besides its declaration — a helper with no "
            + $"callers is the unfinished-close tell (contract §8). Files: "
            + $"[{string.Join(", ", callers)}]");
    }

    private static (string[] Files, string CompilerDir) CompilerSourceFiles()
    {
        var repoRoot = Infrastructure.DispatchSiteScan.FindRepoRoot();
        var compilerDir = Path.Combine(repoRoot, "src", "Sharpy.Compiler");
        var files = Directory.GetFiles(compilerDir, "*.cs", SearchOption.AllDirectories);
        files.Should().NotBeEmpty("positive control: the compiler source tree must enumerate");
        return (files, compilerDir);
    }
}
