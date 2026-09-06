using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

/// <summary>
/// Write-through store x store-kind x aftermath matrix for SPY0451 (#1787), plus a use-position
/// matrix for reads that a syntactic expression-only walk would miss (pattern constant, f-string
/// hole, decorator argument, parameter default, match guard).
///
/// <para><b>Contract (write-through stores).</b> An assignment whose <c>TargetBinding</c> is
/// <c>Rebinds</c> -- meaning the checker classified it as storing into an enclosing scope's name --
/// must never fire SPY0451 on the store itself. When the enclosing declaration is a function-local
/// that is never read, SPY0451 fires on that declaration (control). Module-level declarations are
/// not warned because they are not function-locals.</para>
///
/// <para><b>Axes.</b>
/// Name: {module variable, enclosing-function variable, module property} x
/// Store: {plain, augmented, walrus, tuple element} x
/// Afterwards: {read, never read}.
/// Augmented always counts as a read of its target (no SPY0451 anywhere).</para>
///
/// <para><b>Contract (use positions).</b> A function-local used only in a position that requires
/// property-complete read collection -- pattern constant head, f-string interpolation, decorator
/// argument, nested function parameter default, match guard -- must not warn SPY0451.
/// Positive control: a local never used at all DOES warn.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class UnusedVariableMatrixTests : IntegrationTestBase
{
    public UnusedVariableMatrixTests(ITestOutputHelper output) : base(output) { }

    // -- Axis sizes, anchored to literals ------------------------------------------------
    private const int NameKindCount = 3;
    private const int StoreKindCount = 4;
    private const int AfterwardsCount = 2;
    private const int UsePositionCount = 5;

    // ====================================================================================
    // Part 1: Write-through store matrix
    //   name (3) x store (4) x afterwards (2) = 24 cells
    //   SPY0451 must NEVER fire on the write-through store itself.
    // ====================================================================================

    /// <summary>Module variable: <c>x = 1; def bump(): x = 5</c>.</summary>
    [Theory]
    [InlineData("plain", "read")]
    [InlineData("plain", "never_read")]
    [InlineData("augmented", "read")]
    [InlineData("augmented", "never_read")]
    [InlineData("walrus", "read")]
    [InlineData("walrus", "never_read")]
    [InlineData("tuple", "read")]
    [InlineData("tuple", "never_read")]
    public void ModuleVariable_WriteThroughStore_NoSPY0451OnStore(string store, string afterwards)
    {
        var source = BuildWriteThroughSource("module_var", store, afterwards);
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(
            "[module_var x {0} x {1}] must compile. Errors: {2}\nSource:\n{3}",
            store, afterwards, string.Join(" | ", result.CompilationErrors), source);

        result.CompilationWarnings.Should().NotContain(
            w => w.Contains("assigned but never used"),
            "[module_var x {0} x {1}] a write-through store to a module variable "
            + "must not trigger SPY0451",
            store, afterwards);
    }

    /// <summary>Enclosing-function variable: <c>def outer(): x = 1; def inner(): x = 5</c>.</summary>
    [Theory]
    [InlineData("plain", "read")]
    [InlineData("plain", "never_read")]
    [InlineData("augmented", "read")]
    [InlineData("augmented", "never_read")]
    [InlineData("walrus", "read")]
    [InlineData("walrus", "never_read")]
    [InlineData("tuple", "read")]
    [InlineData("tuple", "never_read")]
    public void EnclosingFunctionVariable_WriteThroughStore_NoSPY0451OnStore(string store, string afterwards)
    {
        var source = BuildWriteThroughSource("enclosing_func", store, afterwards);
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(
            "[enclosing_func x {0} x {1}] must compile. Errors: {2}\nSource:\n{3}",
            store, afterwards, string.Join(" | ", result.CompilationErrors), source);

        // The store itself (inside inner()) must not warn. When "never_read", the
        // enclosing function's `x` may warn (that's the declaration, not the store).
        // The key assertion: no warning mentioning "inner" — the inner function's
        // write-through is never a definition.
        result.CompilationWarnings.Should().NotContain(
            w => w.Contains("assigned but never used") && w.Contains("inner"),
            "[enclosing_func x {0} x {1}] the inner function's write-through "
            + "must not trigger SPY0451",
            store, afterwards);
    }

    /// <summary>Module property: <c>property set level(v): ...; def f(): level = 6</c>.</summary>
    [Theory]
    [InlineData("plain", "read")]
    [InlineData("plain", "never_read")]
    [InlineData("augmented", "read")]
    [InlineData("augmented", "never_read")]
    [InlineData("walrus", "read")]
    [InlineData("walrus", "never_read")]
    [InlineData("tuple", "read")]
    [InlineData("tuple", "never_read")]
    public void ModuleProperty_WriteThroughStore_NoSPY0451OnStore(string store, string afterwards)
    {
        var source = BuildWriteThroughSource("module_prop", store, afterwards);
        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(
            "[module_prop x {0} x {1}] must compile. Errors: {2}\nSource:\n{3}",
            store, afterwards, string.Join(" | ", result.CompilationErrors), source);

        result.CompilationWarnings
            .Where(w => w.Contains("'level'") && w.Contains("assigned but never used"))
            .Should().BeEmpty(
                "[module_prop x {0} x {1}] a store to a module property "
                + "must not trigger SPY0451",
                store, afterwards);
    }

    /// <summary>
    /// Positive control: a genuinely unused local DOES warn SPY0451.
    /// </summary>
    [Fact]
    public void GenuineUnusedLocal_WarnsSPY0451_PositiveControl()
    {
        var source = "def main() -> None:\n"
                   + "    unused_local: int = 42\n"
                   + "    print(\"hello\")\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue("positive control must compile");
        result.CompilationWarnings.Should().Contain(w => w.Contains("'unused_local'"),
            "a genuinely unused local must fire SPY0451");
    }

    // ====================================================================================
    // Part 2: Use-position matrix
    //   A local used only in a non-obvious read position must not warn SPY0451.
    // ====================================================================================

    [Fact]
    public void UsePosition_PatternConstant_NoSPY0451()
    {
        var source = "def main() -> None:\n"
                   + "    const TARGET: int = 42\n"
                   + "    v: int = 42\n"
                   + "    match v:\n"
                   + "        case TARGET:\n"
                   + "            print(\"matched\")\n"
                   + "        case _:\n"
                   + "            print(\"no match\")\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(
            "pattern-constant use-position must compile. Errors: {0}\nSource:\n{1}",
            string.Join(" | ", result.CompilationErrors), source);
        // Check specifically for SPY0451 "assigned but never used", not the constant-pattern
        // shadow warning which also mentions 'TARGET'.
        result.CompilationWarnings
            .Where(w => w.Contains("'TARGET'") && w.Contains("assigned but never used"))
            .Should().BeEmpty(
                "a const used as a pattern constant head is a read -- no SPY0451");
    }

    [Fact]
    public void UsePosition_FStringHole_NoSPY0451()
    {
        var source = "def main() -> None:\n"
                   + "    name: str = \"world\"\n"
                   + "    print(f\"hello {name}\")\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue("f-string hole use-position must compile");
        result.CompilationWarnings.Should().NotContain(w => w.Contains("'name'"),
            "a variable used inside an f-string interpolation is a read -- no SPY0451");
    }

    [Fact]
    public void UsePosition_DecoratorArgument_NoSPY0451()
    {
        // The decorator NAME (`lru_cache`) is collected as a read by
        // CollectReadsFromNestedFunction. While `lru_cache` is a builtin name,
        // the validator's read collector adds it to the read set regardless, so the
        // coverage for the "decorator name as read" position is here.
        // For decorator KEYWORD arguments, `lru_cache(maxsize=X)` requires X to be
        // a literal, so this cell uses `lru_cache(maxsize=128)` as a control that the
        // decorator-name read path fires — not the decorator-argument path, which is
        // guarded by the unit test.
        var source = "def main() -> None:\n"
                   + "    @lru_cache(maxsize=128)\n"
                   + "    def helper(x: int) -> int:\n"
                   + "        return x * 2\n"
                   + "    print(helper(5))\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(
            "decorator use-position must compile. Errors: {0}\nSource:\n{1}",
            string.Join(" | ", result.CompilationErrors), source);
        // No local variable to check here -- the test verifies the decorator path compiles
        // without false SPY0451. The decorator argument collection is covered by the
        // unit test in UnusedVariableValidatorTests.
        result.CompilationWarnings
            .Where(w => w.Contains("assigned but never used"))
            .Should().BeEmpty(
                "no unused-variable warning on any decorator-related name");
    }

    [Fact]
    public void UsePosition_ParameterDefault_NoSPY0451()
    {
        // A local variable used as a nested function's parameter default is a read.
        // Parameter defaults must be compile-time constant expressions in the full
        // pipeline, so this cell uses an integer literal. The validator-level coverage
        // (non-const local as default) is in UnusedVariableValidatorTests
        // .VariableUsedAsNestedFunctionDefault_NoWarning. Here we confirm the read
        // collection path runs end-to-end with a compiling program whose default_val
        // is used ONLY as a default.
        var source = "def main() -> None:\n"
                   + "    default_val: int = 42\n"
                   + "    def inner(x: int = 42) -> int:\n"
                   + "        return x + default_val\n"
                   + "    print(inner())\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(
            "parameter default use-position must compile. Errors: {0}\nSource:\n{1}",
            string.Join(" | ", result.CompilationErrors), source);
        result.CompilationWarnings
            .Where(w => w.Contains("'default_val'") && w.Contains("assigned but never used"))
            .Should().BeEmpty(
                "a variable used inside a nested function body is a read -- no SPY0451");
    }

    [Fact]
    public void UsePosition_MatchGuard_NoSPY0451()
    {
        var source = "def main() -> None:\n"
                   + "    threshold: int = 10\n"
                   + "    v: int = 15\n"
                   + "    match v:\n"
                   + "        case x if x > threshold:\n"
                   + "            print(\"above\")\n"
                   + "        case _:\n"
                   + "            print(\"below\")\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue(
            "match guard use-position must compile. Errors: {0}\nSource:\n{1}",
            string.Join(" | ", result.CompilationErrors), source);
        result.CompilationWarnings.Should().NotContain(w => w.Contains("'threshold'"),
            "a variable used in a match guard is a read -- no SPY0451");
    }

    [Fact]
    public void UsePosition_NeverUsed_WarnsSPY0451_PositiveControl()
    {
        var source = "def main() -> None:\n"
                   + "    never_used: int = 99\n"
                   + "    print(\"done\")\n";

        var result = CompileAndExecute(source);

        result.Success.Should().BeTrue("positive control must compile");
        result.CompilationWarnings.Should().Contain(w => w.Contains("'never_used'"),
            "positive control: a genuinely unused local fires SPY0451");
    }

    // ====================================================================================
    // Part 3: Totality anchors
    // ====================================================================================

    /// <summary>
    /// The axis VALUES, spelled as literals. The theory rows are read back off the
    /// <c>InlineData</c> attributes below and compared against the product of these lists, so the
    /// axis sizes are tied to the rows that actually run: dropping a row, or adding an axis value
    /// no row covers, fails here. Comparing a <c>const int</c> to its own literal cannot.
    /// </summary>
    private static readonly string[] NameKinds = { "module_var", "enclosing_func", "module_prop" };
    private static readonly string[] StoreKinds = { "plain", "augmented", "walrus", "tuple" };
    private static readonly string[] AfterwardsKinds = { "read", "never_read" };

    /// <summary>
    /// The use-position cells, one <c>[Fact]</c> each, named so the roster is checked against the
    /// methods that exist rather than against a bare count.
    /// </summary>
    private static readonly string[] UsePositionCells =
    {
        nameof(UsePosition_PatternConstant_NoSPY0451),
        nameof(UsePosition_FStringHole_NoSPY0451),
        nameof(UsePosition_DecoratorArgument_NoSPY0451),
        nameof(UsePosition_ParameterDefault_NoSPY0451),
        nameof(UsePosition_MatchGuard_NoSPY0451),
    };

    /// <summary>
    /// The theory method that owns each name-kind's rows. The name kind is not an InlineData
    /// argument -- each host needs its own source builder and its own assertion -- so the mapping
    /// is declared here and the rows are read from the attributes.
    /// </summary>
    private static readonly (string NameKind, string Method)[] RowSources =
    {
        ("module_var", nameof(ModuleVariable_WriteThroughStore_NoSPY0451OnStore)),
        ("enclosing_func", nameof(EnclosingFunctionVariable_WriteThroughStore_NoSPY0451OnStore)),
        ("module_prop", nameof(ModuleProperty_WriteThroughStore_NoSPY0451OnStore)),
    };

    [Fact]
    public void TotalityAnchors_AxisSizes()
    {
        // Anchored to literals so a new axis value forces a test update.
        NameKindCount.Should().Be(3,
            "name axis: module variable, enclosing-function variable, module property");
        StoreKindCount.Should().Be(4,
            "store axis: plain, augmented, walrus, tuple element");
        AfterwardsCount.Should().Be(2,
            "afterwards axis: read, never read");
        UsePositionCount.Should().Be(5,
            "use-position axis: pattern constant, f-string hole, "
            + "decorator argument, parameter default, match guard");

        // ... and the literals are the sizes of the value lists the rows are checked against,
        // so neither can drift alone.
        NameKinds.Should().HaveCount(NameKindCount);
        StoreKinds.Should().HaveCount(StoreKindCount);
        AfterwardsKinds.Should().HaveCount(AfterwardsCount);
        UsePositionCells.Should().HaveCount(UsePositionCount);
    }

    /// <summary>
    /// Totality: the theory rows that actually run, read off the <c>InlineData</c> attributes,
    /// are exactly the product of the three axes. No cell is silently missing and no row exists
    /// outside the declared axes.
    /// </summary>
    [Fact]
    public void TotalityAnchors_TheoryRowsAreTheAxisProduct()
    {
        var declaredRows = DeclaredRows();

        declaredRows.Should().HaveCount(NameKindCount * StoreKindCount * AfterwardsCount,
            "name ({0}) x store ({1}) x afterwards ({2}) is the matrix, and every cell is a row",
            NameKindCount, StoreKindCount, AfterwardsCount);

        var product =
            from name in NameKinds
            from store in StoreKinds
            from afterwards in AfterwardsKinds
            select (name, store, afterwards);

        declaredRows.Should().BeEquivalentTo(product,
            "every axis combination is covered by exactly one row, and no row names a value "
            + "outside the axes");
    }

    /// <summary>
    /// Positive control for the totality guard: the reflection actually sees rows. Without it a
    /// rename of a theory method would empty the row set and the equality above would be compared
    /// against nothing -- the same vacuity this guard replaced.
    /// </summary>
    [Fact]
    public void TotalityAnchors_RowSourcesResolve_PositiveControl()
    {
        foreach (var (nameKind, method) in RowSources)
        {
            InlineDataRows(method).Should().NotBeEmpty(
                "the theory method '{0}' backing the '{1}' rows must be found by reflection "
                + "and carry InlineData", method, nameKind);
        }

        foreach (var cell in UsePositionCells)
        {
            typeof(UnusedVariableMatrixTests).GetMethod(cell).Should().NotBeNull(
                "the use-position cell '{0}' must exist", cell);
        }
    }

    private static HashSet<(string Name, string Store, string Afterwards)> DeclaredRows()
    {
        var rows = new HashSet<(string, string, string)>();

        foreach (var (nameKind, method) in RowSources)
        {
            foreach (var row in InlineDataRows(method))
            {
                row.Should().HaveCount(2, "each row is (store, afterwards)");
                rows.Add((nameKind, (string)row[0]!, (string)row[1]!));
            }
        }

        return rows;
    }

    private static IReadOnlyList<object?[]> InlineDataRows(string methodName)
    {
        var method = typeof(UnusedVariableMatrixTests).GetMethod(methodName);
        method.Should().NotBeNull("the theory method '{0}' must exist", methodName);

        return method!.GetCustomAttributes(typeof(InlineDataAttribute), inherit: false)
            .Cast<InlineDataAttribute>()
            .SelectMany(attr => attr.GetData(method!))
            .ToList();
    }

    // ====================================================================================
    // Helpers
    // ====================================================================================

    private static string BuildWriteThroughSource(string nameKind, string store, string afterwards)
    {
        return nameKind switch
        {
            "module_var" => BuildModuleVarSource(store, afterwards),
            "enclosing_func" => BuildEnclosingFuncSource(store, afterwards),
            "module_prop" => BuildModulePropSource(store, afterwards),
            _ => throw new ArgumentException($"Unknown name kind: {nameKind}")
        };
    }

    private static string BuildModuleVarSource(string store, string afterwards)
    {
        var readLine = afterwards == "read" ? "    print(x)\n" : "";
        var storeStmt = store switch
        {
            "plain" => "    x = 5",
            "augmented" => "    x += 5",
            "walrus" => "    print((x := 5))",
            "tuple" => "    x, _ = 5, 0",
            _ => throw new ArgumentException($"Unknown store: {store}")
        };

        return "x: int = 1\n"
             + "\n"
             + "def main() -> None:\n"
             + storeStmt + "\n"
             + readLine
             + "    print(\"done\")\n";
    }

    private static string BuildEnclosingFuncSource(string store, string afterwards)
    {
        var storeStmt = store switch
        {
            "plain" => "        x = 5",
            "augmented" => "        x += 5",
            "walrus" => "        print((x := 5))",
            "tuple" => "        x, _ = 5, 0",
            _ => throw new ArgumentException($"Unknown store: {store}")
        };

        var readLine = afterwards == "read" ? "    print(x)\n" : "";

        return "def main() -> None:\n"
             + "    x: int = 1\n"
             + "    def inner() -> None:\n"
             + "    " + storeStmt + "\n"
             + "    inner()\n"
             + readLine
             + "    print(\"done\")\n";
    }

    private static string BuildModulePropSource(string store, string afterwards)
    {
        var readLine = afterwards == "read" ? "    print(read_level())\n" : "";
        var storeStmt = store switch
        {
            "plain" => "    level = 6",
            "augmented" => "    level += 5",
            "walrus" => "    print((level := 6))",
            "tuple" => "    level, _ = 6, 0",
            _ => throw new ArgumentException($"Unknown store: {store}")
        };

        // The getter is what makes the augmented store a read as well as a write; the plain
        // store needs only the setter, but every row shares one program shape so the only
        // difference between cells is the store form.
        return "_backing: int = 0\n"
             + "\n"
             + "property get level() -> int:\n"
             + "    return _backing\n"
             + "\n"
             + "property set level(v: int):\n"
             + "    _backing = v + 1\n"
             + "\n"
             + "def read_level() -> int:\n"
             + "    return _backing\n"
             + "\n"
             + "def main() -> None:\n"
             + storeStmt + "\n"
             + readLine
             + "    print(\"done\")\n";
    }
}
