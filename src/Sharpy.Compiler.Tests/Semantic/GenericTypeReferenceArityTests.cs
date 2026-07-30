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
}
