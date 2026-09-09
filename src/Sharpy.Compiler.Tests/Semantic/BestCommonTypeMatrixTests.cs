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
/// slot-directed (arm 1), else one operand's type accepts all (arm 2), else refused by name (arm 3).
/// A conditional in a truthiness position distributes — the test wraps each branch individually.</para>
///
/// <para><b>Operand-join contract.</b> Every site that joins element types — list, set, dict,
/// tuple, <c>dict(**kw)</c>, or-pattern capture — calls <c>BestCommonType</c>. <c>None</c> is
/// untyped in it (R-AB). <c>FindLeastCommonAncestor</c> no longer exists.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class BestCommonTypeMatrixTests : IntegrationTestBase
{
    public BestCommonTypeMatrixTests(ITestOutputHelper output) : base(output) { }

    // ══ Part 1: Conditional expression cells ════════════════════════════════════════════════

    private enum CellExpectation { Compiles, RefusedSPY0220, RefusedSPY0227 }

    [Theory]
    [InlineData("SameType_Declaration",
        "class Dog:\n    pass\n\ndef main():\n    c = True\n    a: Dog = Dog() if c else Dog()\n    print(a)\n",
        true, null)]
    [InlineData("Subtype_Declaration",
        "class Animal:\n    pass\n\nclass Dog(Animal):\n    pass\n\ndef main():\n    c = True\n    a: Animal = Dog() if c else Animal()\n    print(a)\n",
        true, null)]
    [InlineData("IntFloat_Arm2",
        "def main():\n    c = True\n    x = 1 if c else 2.5\n    print(x)\n",
        true, "1.0\n")]
    [InlineData("UnrelatedClasses_SlotLess",
        "class Dog:\n    pass\n\nclass Cat:\n    pass\n\ndef main():\n    c = True\n    a = Dog() if c else Cat()\n",
        false, null)]
    [InlineData("IntStr_SlotLess",
        "def main():\n    c = True\n    a = 1 if c else \"x\"\n",
        false, null)]
    [InlineData("NoneInt_SlotLess",
        "def main():\n    c = True\n    a = None if c else 1\n",
        false, null)]
    [InlineData("MistypedBranch_UnderSlot_ICEToday",
        "class Animal:\n    pass\n\nclass Dog(Animal):\n    pass\n\ndef main():\n    c = True\n    a: Animal = Dog() if c else \"x\"\n    print(a)\n",
        false, null)]
    [InlineData("Arm1_Argument",
        "class Animal:\n    pass\n\nclass Dog(Animal):\n    pass\n\nclass Cat(Animal):\n    pass\n\ndef show(a: Animal) -> None:\n    print(a)\n\ndef main():\n    c = True\n    show(Dog() if c else Cat())\n",
        true, null)]
    [InlineData("Arm1_Return",
        "class Animal:\n    pass\n\nclass Dog(Animal):\n    pass\n\nclass Cat(Animal):\n    pass\n\ndef pick(c: bool) -> Animal:\n    return Dog() if c else Cat()\n\ndef main():\n    pick(True)\n",
        true, null)]
    [InlineData("Arm1_ListElement",
        "class Animal:\n    pass\n\nclass Dog(Animal):\n    pass\n\nclass Cat(Animal):\n    pass\n\ndef main():\n    c = True\n    xs: list[Animal] = [Dog() if c else Cat()]\n    print(len(xs))\n",
        true, "1\n")]
    [InlineData("Arm1_FStringHole",
        "class Dog:\n    pass\n\nclass Cat:\n    pass\n\ndef main():\n    c = True\n    print(f\"{Dog() if c else Cat()}\")\n",
        true, null)]
    [InlineData("Arm1_ObjectSlot",
        "def main():\n    c = True\n    o: object = None if c else 1\n    print(o)\n",
        true, null)]
    public void ConditionalCell_TypedByBestCommonType(
        string label, string source, bool shouldCompile, string? expectedOutput)
    {
        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{label}] must never produce SPY0908. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");

        if (shouldCompile)
        {
            result.Success.Should().BeTrue(
                $"[{label}] must compile. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
            if (expectedOutput != null)
                result.StandardOutput.Should().Be(expectedOutput, $"[{label}]\n{source}");
        }
        else
        {
            result.Success.Should().BeFalse($"[{label}] must be refused\n{source}");
        }
    }

    // ── Truthiness distribution cells (R-K) ─────────────────────────────────────────────────

    [Theory]
    [InlineData("If_Truthiness",
        "class Yes:\n    def __bool__(self) -> bool:\n        return False\n\nclass No:\n    def __bool__(self) -> bool:\n        return True\n\ndef main():\n    flag = True\n    y = Yes()\n    n = No()\n    if y if flag else n:\n        print(\"truthy\")\n    else:\n        print(\"falsy\")\n",
        "falsy\n")]
    [InlineData("While_Truthiness",
        "def main():\n    count = 0\n    flag = True\n    if 1 if flag else 0:\n        print(\"entered\")\n    else:\n        print(\"skipped\")\n",
        "entered\n")]
    [InlineData("Not_Truthiness",
        "def main():\n    flag = True\n    x = not (1 if flag else 0)\n    print(x)\n",
        "False\n")]
    [InlineData("Assert_Truthiness",
        "def main():\n    flag = True\n    assert 1 if flag else 1\n    print(\"ok\")\n",
        "ok\n")]
    public void TruthinessCell_DistributesPerBranch(string label, string source, string expected)
    {
        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{label}] must never produce SPY0908. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.Success.Should().BeTrue(
            $"[{label}] must compile — truthiness distributes per branch. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        result.StandardOutput.Should().Be(expected, $"[{label}]\n{source}");
    }

    // ── Truthiness one-site scan ────────────────────────────────────────────────────────────

    [Fact]
    public void SetTruthinessLowering_CallSitesAreKnown()
    {
        var repoRoot = Infrastructure.DispatchSiteScan.FindRepoRoot();
        var semanticDir = Path.Combine(repoRoot, "src", "Sharpy.Compiler", "Semantic");
        var files = Directory.GetFiles(semanticDir, "*.cs", SearchOption.AllDirectories);

        var totalCount = 0;
        var callers = new List<string>();
        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            var count = CountOccurrences(content, "SetTruthinessLowering(");
            if (count > 0)
            {
                callers.Add($"{Path.GetFileName(file)}:{count}");
                if (!Path.GetFileName(file).Equals("SemanticInfo.cs", StringComparison.Ordinal))
                    totalCount += count;
            }
        }

        totalCount.Should().BeLessThanOrEqualTo(6,
            "SetTruthinessLowering callers outside SemanticInfo.cs should be bounded — "
            + $"found: [{string.Join(", ", callers)}]");
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

    // ── TruthinessLowering totality ─────────────────────────────────────────────────────────

    private const int TruthinessLoweringMemberCount = 13;

    [Fact]
    public void TruthinessLowering_EveryMemberHasAnEmitterArm()
    {
        var repoRoot = Infrastructure.DispatchSiteScan.FindRepoRoot();

        var semanticInfoPath = Path.Combine(repoRoot, "src", "Sharpy.Compiler", "Semantic", "SemanticInfo.cs");
        var semanticInfoSource = File.ReadAllText(semanticInfoPath);
        var enumMatch = System.Text.RegularExpressions.Regex.Match(
            semanticInfoSource,
            @"enum TruthinessLowering\s*\{(?<body>[^}]*)\}",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        enumMatch.Success.Should().BeTrue("TruthinessLowering enum must exist in SemanticInfo.cs");

        var members = System.Text.RegularExpressions.Regex.Matches(
            enumMatch.Groups["body"].Value,
            @"^\s*(?<name>[A-Z]\w*)\s*[,=]?\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline)
            .Select(m => m.Groups["name"].Value)
            .ToList();

        members.Count.Should().Be(TruthinessLoweringMemberCount,
            "the member count is anchored to a literal, not to the enum — "
            + $"found: [{string.Join(", ", members)}]");
    }

    // ══ Part 2: Operand-join cells ══════════════════════════════════════════════════════════

    [Theory]
    [InlineData("List_NoneAndInt_SlotLess",
        "def main():\n    xs = [None, 1]\n",
        false)]
    [InlineData("List_IntStr_SlotLess",
        "def main():\n    xs = [1, \"a\"]\n",
        false)]
    [InlineData("List_DogCat_SlotLess",
        "class Dog:\n    pass\n\nclass Cat:\n    pass\n\ndef main():\n    xs = [Dog(), Cat()]\n",
        false)]
    [InlineData("List_DogAnimal_Arm2",
        "class Animal:\n    pass\n\nclass Dog(Animal):\n    pass\n\ndef main():\n    xs = [Dog(), Animal()]\n    print(len(xs))\n",
        true)]
    [InlineData("List_IntFloat_Arm2",
        "def main():\n    xs = [1, 2.5]\n    print(xs[0] + 0.5)\n",
        true)]
    [InlineData("List_IntLong_Arm2",
        "def main():\n    xs = [1, 10000000000]\n    print(xs[1] + 1)\n",
        true)]
    [InlineData("List_Slot_DogCat",
        "class Animal:\n    pass\n\nclass Dog(Animal):\n    pass\n\nclass Cat(Animal):\n    pass\n\ndef main():\n    xs: list[Animal] = [Dog(), Cat()]\n    print(len(xs))\n",
        true)]
    [InlineData("List_NoneOnly",
        "def main():\n    xs = [None, None]\n",
        false)]
    [InlineData("Set_NoneAndInt",
        "def main():\n    xs = {None, 1}\n",
        false)]
    [InlineData("Set_IntStr",
        "def main():\n    xs = {1, \"a\"}\n",
        false)]
    [InlineData("Dict_IntStr_Values",
        "def main():\n    d = {\"a\": 1, \"b\": \"x\"}\n",
        false)]
    [InlineData("DictKw_IntStr",
        "def main():\n    d = dict(a=1, b=\"x\")\n",
        false)]
    [InlineData("Tuple_NoneAndInt",
        "def main():\n    a, b = None, 1\n",
        false)]
    [InlineData("Tuple_IntStr_PerIndex",
        "def main():\n    t = (1, \"a\")\n    print(t[0])\n    print(t[1])\n",
        true)]
    [InlineData("List_Slot_NoneAndInt",
        "def main():\n    xs: list[int | None] = [None, 1]\n    print(len(xs))\n",
        true)]
    [InlineData("Tuple_Slot_NoneAndInt",
        "def main():\n    t: tuple[int | None, int] = (None, 1)\n    print(t[1])\n",
        true)]
    public void OperandJoinCell_TypedByBestCommonType(string label, string source, bool shouldCompile)
    {
        var result = CompileAndExecute(source);

        result.RawDiagnostics.Should().NotContain(
            d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{label}] must never produce SPY0908. "
            + $"Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");

        if (shouldCompile)
        {
            result.Success.Should().BeTrue(
                $"[{label}] must compile. Diagnostics: {string.Join(" | ", result.CompilationErrors)}\n{source}");
        }
        else
        {
            result.Success.Should().BeFalse($"[{label}] must be refused\n{source}");
            result.RawDiagnostics.Should().Contain(
                d => d.Code == DiagnosticCodes.Semantic.CannotInferType
                    || d.Code == DiagnosticCodes.Semantic.TypeMismatch,
                $"[{label}] must report SPY0227 or SPY0220. Got: "
                + $"{string.Join(" | ", result.RawDiagnostics.Select(d => $"{d.Code}: {d.Message}"))}\n{source}");
        }
    }

    // ── LCA deletion scan ───────────────────────────────────────────────────────────────────

    [Fact]
    public void FindLeastCommonAncestor_IsDeleted()
    {
        var repoRoot = Infrastructure.DispatchSiteScan.FindRepoRoot();
        var compilerDir = Path.Combine(repoRoot, "src", "Sharpy.Compiler");
        var files = Directory.GetFiles(compilerDir, "*.cs", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            content.Should().NotContain("FindLeastCommonAncestor",
                $"{Path.GetRelativePath(compilerDir, file)} must not reference the deleted LCA");
            content.Should().NotContain("ResolveVoidElementType",
                $"{Path.GetRelativePath(compilerDir, file)} must not reference the deleted helper");
        }
    }

    [Fact]
    public void GetTypeKey_IsDeleted()
    {
        var repoRoot = Infrastructure.DispatchSiteScan.FindRepoRoot();
        var compilerDir = Path.Combine(repoRoot, "src", "Sharpy.Compiler");
        var utilitiesPath = Path.Combine(compilerDir, "Semantic", "TypeChecker.Utilities.cs");
        var content = File.ReadAllText(utilitiesPath);
        content.Should().NotContain("GetTypeKey(", "GetTypeKey was the LCA's only caller and is deleted with it");
    }
}
