using FluentAssertions;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// A comprehension takes the same contextual type a collection LITERAL takes (#1671, sibling FORM
/// axis).
///
/// <para>The defect class: #1671 taught the three collection literals to record the expected type
/// when every element is assignable to its element type, but the comprehension nodes — the other
/// way to write a collection whose elements are checked in place — kept recording the produced
/// element type. <c>xs: list[Base] = [Derived() for _ in range(2)]</c> therefore recorded
/// <c>list[Derived]</c> and emitted <c>List&lt;Derived&gt;</c> into a <c>List&lt;Base&gt;</c>
/// slot: CS0029 behind SPY0908 for list and set, SPY0220 for dict — while the literal spelling of
/// the very same value passed. Form is not a typing input.</para>
///
/// <para>The matrix is form {list, set, dict} × context {declaration, assign-to-existing, return,
/// call argument} × direction {covariant, exact, mistyped}. The covariant declaration and
/// assign-to-existing cells are discriminated by a follow-up store of a <c>Base()</c> into the
/// collection: that only compiles if the RECORDED type is the expected one, so the cell cannot
/// pass merely because the assignment was tolerated. The mistyped cells are the refusal control —
/// contextual typing must not swallow a genuine type error.</para>
///
/// <para>MUTATION-TESTED: see this file's commit body. With <c>ContextualElementType</c> reverted
/// to returning the produced type, every covariant cell fails and the exact/mistyped cells stay
/// green.</para>
/// </summary>
[Collection("HeavyCompilation")]
public class ComprehensionContextualTypingTests : IntegrationTestBase
{
    public ComprehensionContextualTypingTests(ITestOutputHelper output) : base(output) { }

    private const string Preamble = @"
class Base:
    def kind(self) -> str:
        return ""base""

class Derived(Base):
    def kind(self) -> str:
        return ""derived""
";

    /// <summary>
    /// (annotation, comprehension producing Derived, comprehension producing Base,
    /// a store of a plain Base() into a collection named 'c').
    /// </summary>
    private static (string Annotation, string Derived, string Exact, string StoreBase) Form(string form) => form switch
    {
        "list" => ("list[Base]", "[Derived() for _ in range(2)]", "[Base() for _ in range(2)]", "c.append(Base())"),
        "set" => ("set[Base]", "{Derived() for _ in range(2)}", "{Base() for _ in range(2)}", "c.add(Base())"),
        "dict" => ("dict[str, Base]", "{str(i): Derived() for i in range(2)}",
            "{str(i): Base() for i in range(2)}", "c[\"z\"] = Base()"),
        _ => throw new System.ArgumentOutOfRangeException(nameof(form), form, "unknown comprehension form")
    };

    private static string FormatErrors(ExecutionResult result)
        => string.Join("\n", result.CompilationErrors);

    private void ShouldRun(string source, string expected)
    {
        var result = CompileAndExecute(source);
        result.Success.Should().BeTrue(FormatErrors(result));
        result.StandardOutput.Trim().Should().Be(expected);
    }

    // ── Context: declaration ──

    [Theory]
    [InlineData("list")]
    [InlineData("set")]
    [InlineData("dict")]
    public void Declaration_Covariant_RecordsTheExpectedType(string form)
    {
        var (annotation, derived, _, storeBase) = Form(form);
        ShouldRun(Preamble + $@"
def main() -> None:
    c: {annotation} = {derived}
    {storeBase}
    print(len(c))
", "3");
    }

    [Theory]
    [InlineData("list")]
    [InlineData("set")]
    [InlineData("dict")]
    public void Declaration_Exact_StillWorks(string form)
    {
        var (annotation, _, exact, storeBase) = Form(form);
        ShouldRun(Preamble + $@"
def main() -> None:
    c: {annotation} = {exact}
    {storeBase}
    print(len(c))
", "3");
    }

    [Theory]
    [InlineData("list")]
    [InlineData("set")]
    [InlineData("dict")]
    public void Declaration_Mistyped_IsStillRefused(string form)
    {
        var (annotation, derived, _, _) = Form(form);
        var mistyped = annotation.Replace("Base", "int");
        var result = CompileAndExecute(Preamble + $@"
def main() -> None:
    c: {mistyped} = {derived}
    print(len(c))
");
        result.Success.Should().BeFalse(
            "a comprehension whose elements are not assignable to the expected element type is a "
            + "type error, and contextual typing must not swallow it");
        result.RawDiagnostics.Should().Contain(d => d.Code == "SPY0220",
            $"expected SPY0220 for {form}, got: {FormatErrors(result)}");
    }

    // ── Context: assignment to an existing binding ──

    [Theory]
    [InlineData("list")]
    [InlineData("set")]
    [InlineData("dict")]
    public void AssignToExisting_Covariant_RecordsTheExpectedType(string form)
    {
        var (annotation, derived, exact, storeBase) = Form(form);
        ShouldRun(Preamble + $@"
def main() -> None:
    c: {annotation} = {exact}
    c = {derived}
    {storeBase}
    print(len(c))
", "3");
    }

    // ── Context: return position ──

    [Theory]
    [InlineData("list")]
    [InlineData("set")]
    [InlineData("dict")]
    public void Return_Covariant_RecordsTheExpectedType(string form)
    {
        var (annotation, derived, _, storeBase) = Form(form);
        ShouldRun(Preamble + $@"
def make() -> {annotation}:
    return {derived}

def main() -> None:
    c: {annotation} = make()
    {storeBase}
    print(len(c))
", "3");
    }

    // ── Context: call argument ──

    [Theory]
    [InlineData("list")]
    [InlineData("set")]
    [InlineData("dict")]
    public void CallArgument_Covariant_RecordsTheExpectedType(string form)
    {
        var (annotation, derived, _, _) = Form(form);
        ShouldRun(Preamble + $@"
def take(c: {annotation}) -> int:
    return len(c)

def main() -> None:
    print(take({derived}))
", "2");
    }

    [Theory]
    [InlineData("list")]
    [InlineData("set")]
    [InlineData("dict")]
    public void CallArgument_Mistyped_IsStillRefused(string form)
    {
        var (annotation, derived, _, _) = Form(form);
        var mistyped = annotation.Replace("Base", "int");
        var result = CompileAndExecute(Preamble + $@"
def take(c: {mistyped}) -> int:
    return len(c)

def main() -> None:
    print(take({derived}))
");
        result.Success.Should().BeFalse(
            $"a mistyped comprehension argument stays refused for {form}");
    }

    // ── The literal twin: same value, other spelling, same recorded type ──

    [Theory]
    [InlineData("list[Base]", "[Derived(), Derived()]", "c.append(Base())")]
    [InlineData("set[Base]", "{Derived()}", "c.add(Base())")]
    [InlineData("dict[str, Base]", "{\"a\": Derived()}", "c[\"z\"] = Base()")]
    public void LiteralTwin_AlreadyRecordsTheExpectedType(string annotation, string literal, string storeBase)
    {
        // The positive control for the whole file: the literal spelling passed before this fix, so
        // a comprehension cell that fails is a FORM difference, not a broken expectation rule.
        var result = CompileAndExecute(Preamble + $@"
def main() -> None:
    c: {annotation} = {literal}
    {storeBase}
    print(len(c))
");
        result.Success.Should().BeTrue(FormatErrors(result));
    }
}
