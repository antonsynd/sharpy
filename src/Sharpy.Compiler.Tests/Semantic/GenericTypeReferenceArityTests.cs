using System.IO;
using System.Linq;
using Xunit;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Tests.Helpers;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #1192-A: a generic TYPE reference with explicit type arguments (<c>Box[int]</c>,
/// <c>genlib.Holder[int]</c>, <c>Outer.Inner[int]</c>) is arity-checked at the same seam and with the
/// same PEP-696 default filling the annotation position uses. Before this, only the function-shaped
/// callee kinds arity-checked, so <c>Box[int, str](5)</c> silently emitted
/// <c>new Box&lt;int, string&gt;(5)</c> and leaked CS0305 through the SPY0908 net.
///
/// <para>The parity assertion matters as much as the rejection: <c>Box[int, str]</c> must read
/// identically whether it is written as an annotation or as an expression, because they are the same
/// question about the same declaration.</para>
/// </summary>
public class GenericTypeReferenceArityTests
{
    private static DiagnosticBag Analyze(string source)
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

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance);
        typeChecker.CheckModule(module, isEntryPoint: false);

        return typeChecker.Diagnostics;
    }

    private static string[] ArityErrors(DiagnosticBag diagnostics) =>
        diagnostics.GetErrors()
            .Where(d => d.Code == DiagnosticCodes.Semantic.WrongArgumentCount)
            .Select(d => d.Message)
            .ToArray();

    private const string BoxDecl = @"
class Box[T]:
    value: T

    def __init__(self, value: T):
        self.value = value
";

    private const string PairDecl = @"
class Pair[K, V]:
    key: K
    value: V

    def __init__(self, key: K, value: V):
        self.key = key
        self.value = value
";

    private const string DefaultedPairDecl = @"
class Pair[K, V = str]:
    key: K
    value: V

    def __init__(self, key: K, value: V):
        self.key = key
        self.value = value
";

    private const string NestedDecl = @"
class Outer:
    @public
    class Inner[T]:
        value: T

        def __init__(self, value: T):
            self.value = value

    @public
    class Pair[K, V]:
        key: K
        value: V

        def __init__(self, key: K, value: V):
            self.key = key
            self.value = value
";

    private const string DefaultedNestedDecl = @"
class Outer:
    @public
    class Pair[K, V = str]:
        key: K
        value: V

        def __init__(self, key: K, value: V):
            self.key = key
            self.value = value
";

    // ── bare identifier references ──

    [Fact]
    public void BareTypeReference_ExactArity_NoDiagnostic()
    {
        var diagnostics = Analyze(BoxDecl + @"
def use() -> None:
    b = Box[int](5)
");

        ArityErrors(diagnostics).Should().BeEmpty();
    }

    [Fact]
    public void BareTypeReference_ExcessArity_ReportsWrongTypeArgumentCount()
    {
        var diagnostics = Analyze(BoxDecl + @"
def use() -> None:
    b = Box[int, str](5)
");

        ArityErrors(diagnostics).Should().ContainSingle()
            .Which.Should().Be("Type 'Box' expects 1 type arguments but got 2");
    }

    [Fact]
    public void BareTypeReference_DeficientArityWithoutDefaults_ReportsWrongTypeArgumentCount()
    {
        var diagnostics = Analyze(PairDecl + @"
def use() -> None:
    p = Pair[int](1, ""a"")
");

        ArityErrors(diagnostics).Should().ContainSingle()
            .Which.Should().Be("Type 'Pair' expects 2 type arguments but got 1");
    }

    [Fact]
    public void BareTypeReference_DeficientArityWithDefaults_FillsAndAccepts()
    {
        var diagnostics = Analyze(DefaultedPairDecl + @"
def use() -> None:
    p = Pair[int](1, ""a"")
");

        ArityErrors(diagnostics).Should().BeEmpty(
            "PEP-696 lets the trailing type argument come from its declared default");
    }

    [Fact]
    public void BareTypeReference_ExcessArityWithDefaults_StillReports()
    {
        var diagnostics = Analyze(DefaultedPairDecl + @"
def use() -> None:
    p = Pair[int, str, bool](1, ""a"")
");

        ArityErrors(diagnostics).Should().ContainSingle()
            .Which.Should().Be("Type 'Pair' expects 2 type arguments but got 3",
                "a default can fill a missing argument, never absorb an extra one");
    }

    // ── nested type references (Outer.Inner[...]) ──

    [Fact]
    public void NestedTypeReference_ExactArity_NoDiagnostic()
    {
        var diagnostics = Analyze(NestedDecl + @"
def use() -> None:
    i = Outer.Inner[int](5)
");

        ArityErrors(diagnostics).Should().BeEmpty();
    }

    [Fact]
    public void NestedTypeReference_ExcessArity_ReportsWrongTypeArgumentCount()
    {
        var diagnostics = Analyze(NestedDecl + @"
def use() -> None:
    i = Outer.Inner[int, str](5)
");

        ArityErrors(diagnostics).Should().ContainSingle()
            .Which.Should().Be("Type 'Inner' expects 1 type arguments but got 2");
    }

    [Fact]
    public void NestedTypeReference_DeficientArityWithoutDefaults_ReportsWrongTypeArgumentCount()
    {
        var diagnostics = Analyze(NestedDecl + @"
def use() -> None:
    p = Outer.Pair[int](1, ""a"")
");

        ArityErrors(diagnostics).Should().ContainSingle()
            .Which.Should().Be("Type 'Pair' expects 2 type arguments but got 1");
    }

    [Fact]
    public void NestedTypeReference_DeficientArityWithDefaults_FillsAndAccepts()
    {
        var diagnostics = Analyze(DefaultedNestedDecl + @"
def use() -> None:
    p = Outer.Pair[int](1, ""a"")
");

        ArityErrors(diagnostics).Should().BeEmpty();
    }

    // ── module-qualified type references (genlib.Holder[...]) ──

    private static string[] ModuleQualifiedArityErrors(string mainBody)
    {
        using var helper = new ProjectCompilationHelper()
            .WithRootNamespace("TypeRefArity")
            .WithOutputType("library");

        helper.AddSourceFile("genlib.spy", @"
class Holder[T]:
    value: T

    def __init__(self, value: T):
        self.value = value


class Slot[K, V = str]:
    key: K
    value: V

    def __init__(self, key: K, value: V):
        self.key = key
        self.value = value


class PairBox[A, B]:
    first: A
    second: B

    def __init__(self, first: A, second: B):
        self.first = first
        self.second = second
");
        helper.AddSourceFile("main.spy", "import genlib\n\n" + mainBody);
        helper.CreateProjectFile();

        var projectPath = Path.Combine(helper.ProjectDirectory, $"{helper.Options.RootNamespace}.spyproj");
        var result = new CompilerApi().AnalyzeProject(ProjectFileParser.Load(projectPath));

        return result.Diagnostics.GetErrors()
            .Where(d => d.Code == DiagnosticCodes.Semantic.WrongArgumentCount)
            .Select(d => d.Message)
            .ToArray();
    }

    [Fact]
    public void ModuleQualifiedTypeReference_ExactArity_NoDiagnostic()
    {
        ModuleQualifiedArityErrors(@"
def use() -> None:
    h = genlib.Holder[int](5)
").Should().BeEmpty();
    }

    [Fact]
    public void ModuleQualifiedTypeReference_ExcessArity_ReportsWrongTypeArgumentCount()
    {
        ModuleQualifiedArityErrors(@"
def use() -> None:
    h = genlib.Holder[int, str](5)
").Should().ContainSingle()
            .Which.Should().Be("Type 'Holder' expects 1 type arguments but got 2");
    }

    [Fact]
    public void ModuleQualifiedTypeReference_DeficientArityWithDefaults_FillsAndAccepts()
    {
        ModuleQualifiedArityErrors(@"
def use() -> None:
    s = genlib.Slot[int](1, ""a"")
").Should().BeEmpty();
    }

    [Fact]
    public void ModuleQualifiedTypeReference_DeficientArityWithoutDefaults_ReportsWrongTypeArgumentCount()
    {
        ModuleQualifiedArityErrors(@"
def use() -> None:
    p = genlib.PairBox[int](1, ""a"")
").Should().ContainSingle()
            .Which.Should().Be("Type 'PairBox' expects 2 type arguments but got 1");
    }

    // ── annotation-position parity ──

    [Fact]
    public void ExpressionPosition_MatchesAnnotationPosition_SameCodeAndMessage()
    {
        var annotationDiagnostics = Analyze(BoxDecl + @"
def use(b: Box[int, str]) -> None:
    pass
");
        var expressionDiagnostics = Analyze(BoxDecl + @"
def use() -> None:
    b = Box[int, str](5)
");

        var annotationErrors = annotationDiagnostics.GetErrors()
            .Where(d => d.Code == DiagnosticCodes.Semantic.WrongArgumentCount).ToList();
        var expressionErrors = expressionDiagnostics.GetErrors()
            .Where(d => d.Code == DiagnosticCodes.Semantic.WrongArgumentCount).ToList();

        annotationErrors.Should().ContainSingle();
        expressionErrors.Should().ContainSingle();
        expressionErrors[0].Message.Should().Be(annotationErrors[0].Message,
            "the same wrong-arity question about the same declaration must read the same in both positions");
    }

    [Fact]
    public void AnnotationPositionDefaultFilling_Unchanged()
    {
        var diagnostics = Analyze(DefaultedPairDecl + @"
def use(p: Pair[int]) -> None:
    pass
");

        ArityErrors(diagnostics).Should().BeEmpty();
    }

    // ===========================
    // #1219 — the FUNCTION seam is PEP-696 default-aware too
    //
    // The type seam has filled trailing defaults since #1192; the function seam was a strict count
    // check, so `def pair[K, V = str]` then `pair[int](...)` was rejected where `Pair[int](...)`
    // filled. Both now share one fill routine and keep their own (user-visible, pinned) wording.
    // ===========================

    private const string DefaultedPairFunctionDecl = @"
def pair_fn[K, V = str](k: K, v: V) -> str:
    return ""paired""
";

    [Fact]
    public void FunctionReference_DeficientArityWithDefault_FillsAndReportsNoError()
    {
        var diagnostics = Analyze(DefaultedPairFunctionDecl + @"
def use() -> None:
    p = pair_fn[int](1, ""a"")
");

        ArityErrors(diagnostics).Should().BeEmpty();
    }

    [Fact]
    public void FunctionReference_ExactArity_ReportsNoError()
    {
        var diagnostics = Analyze(DefaultedPairFunctionDecl + @"
def use() -> None:
    p = pair_fn[int, str](1, ""a"")
");

        ArityErrors(diagnostics).Should().BeEmpty();
    }

    [Fact]
    public void FunctionReference_ExcessArity_KeepsItsOwnWording()
    {
        // The two seams deliberately do NOT share a diagnostic string: "type argument(s)" with the
        // parenthesized plural here, "type arguments" on the type side. Sharing the fill routine
        // must not collapse the messages.
        var diagnostics = Analyze(DefaultedPairFunctionDecl + @"
def use() -> None:
    p = pair_fn[int, str, bool](1, ""a"")
");

        ArityErrors(diagnostics).Should().ContainSingle()
            .Which.Should().Be("Generic function 'pair_fn' expects 2 type argument(s) but got 3");
    }

    [Fact]
    public void FunctionReference_DeficientArityWithoutDefaults_StillReportsWrongCount()
    {
        var diagnostics = Analyze(@"
def plain_fn[K, V](k: K, v: V) -> str:
    return ""plain""

def use() -> None:
    p = plain_fn[int](1, ""a"")
");

        ArityErrors(diagnostics).Should().ContainSingle()
            .Which.Should().Be("Generic function 'plain_fn' expects 2 type argument(s) but got 1");
    }

    [Fact]
    public void FunctionReference_AllDefaulted_EmptyVectorIsUnaffected()
    {
        // An all-defaulted function called with no brackets never reaches the arity seam at all —
        // ordinary inference types it. Pinned so the fill cannot start claiming this case.
        var diagnostics = Analyze(@"
def all_def_fn[A = int, B = str](a: A, b: B) -> str:
    return ""alldef""

def use() -> None:
    p = all_def_fn(3, ""c"")
");

        ArityErrors(diagnostics).Should().BeEmpty();
    }

    [Fact]
    public void FunctionReference_UncalledWithFilledDefault_IsAValuePositionErrorNotAnArityError()
    {
        // #1219's own example. After the fix `f = pair_fn[int]` stops being an SPY0224 arity error
        // and becomes SPY0335 — a generic function reference must be called. That is the correct
        // outcome: the arity rejection was the bug, the value-position rule is not.
        var diagnostics = Analyze(DefaultedPairFunctionDecl + @"
def use() -> None:
    f = pair_fn[int]
");

        ArityErrors(diagnostics).Should().BeEmpty();
        diagnostics.GetErrors().Should().Contain(d =>
            d.Code == DiagnosticCodes.Semantic.GenericFunctionReferenceNotCalled);
    }

    [Fact]
    public void BothSeams_SelfReferentialDefault_FailIdentically()
    {
        // `V = K` does not resolve on EITHER seam — ResolveTypeAnnotation has no type-parameter
        // scope for a default. Pre-existing on the type side (#1192) and now shared rather than
        // hidden behind the function seam's arity rejection. Pinned as a matched pair so a future
        // fix moves both together.
        var functionDiagnostics = Analyze(@"
def dup_fn[K, V = K](k: K, v: V) -> str:
    return ""dup""

def use() -> None:
    p = dup_fn[int](1, 2)
");
        var typeDiagnostics = Analyze(@"
class Dup[K, V = K]:
    a: K
    b: V

    def __init__(self, a: K, b: V):
        self.a = a
        self.b = b

def use() -> None:
    d = Dup[int](1, 2)
");

        functionDiagnostics.GetErrors().Should().Contain(d => d.Code == DiagnosticCodes.Semantic.UndefinedType);
        typeDiagnostics.GetErrors().Should().Contain(d => d.Code == DiagnosticCodes.Semantic.UndefinedType);
    }
}
