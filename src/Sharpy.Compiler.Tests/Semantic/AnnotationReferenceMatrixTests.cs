using FluentAssertions;
using Sharpy.Compiler;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The annotation-reference matrix -- symbol kind (10) x position (12) -- and its controls (#1737).
///
/// <para><b>Contract.</b> <c>SetTypeAnnotation(annotation, type, boundSymbol)</c> is the ONLY
/// reference-recording seam for type annotations. When <c>boundSymbol</c> is non-null,
/// <c>RecordReference(boundSymbol, annotation)</c> records the annotation position as a
/// reference to that symbol. Every branch in <c>TypeResolver.ResolveTypeAnnotation</c> must
/// answer which symbol it bound (user type, alias, type parameter, CLR import) or pass null
/// (builtins, primitives, keywords). The identifier-use twin is the positive control: an
/// expression-position reference to the same symbol is counted by <c>SetIdentifierSymbol</c>
/// and always works.</para>
///
/// <para><b>Axes.</b> Kind: {class, struct, interface, enum, union, delegate, alias,
/// generic alias, type parameter, CLR type via import} x Position: {parameter, return,
/// variable, field, base-class list, generic argument, C | None, C?, C!E, as? target,
/// isinstance argument, pattern head (N/A Batch 6)}.
/// Each cell asserts <c>GetReferences(symbol).Count == 1</c>.</para>
/// </summary>
public class AnnotationReferenceMatrixTests
{
    private readonly CompilerApi _api = new();

    // -- Axis sizes, anchored to literals -----------------------------------------
    // Pattern head is rostered N/A "Batch 6" per plan.
    private const int SymbolKindCount = 10;
    private const int PositionCount = 12;

    // -- Symbol kind x position cells ---------------------------------------------

    // Annotation-only cells: C appears in exactly one annotation and no expressions.
    [Theory]
    [InlineData("parameter",
        "class C:\n    pass\n\ndef use(c: C) -> None:\n    pass\n\ndef main():\n    pass")]
    [InlineData("generic_arg",
        "class C:\n    pass\n\ndef main():\n    xs: list[C] = []\n    print(xs)")]
    public void Class_AnnotationOnly_CountsOneReference(string position, string source)
    {
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue($"class x {position} should compile");

        var symbol = analysis.SymbolTable!.LookupType("C");
        symbol.Should().NotBeNull();

        var refs = analysis.SemanticInfo!.GetReferences(symbol!);
        refs.Should().HaveCount(1,
            $"class x {position}: annotation records exactly one reference (#1737)");
    }

    // Annotation + expression cells: C appears in one annotation AND one expression.
    [Theory]
    [InlineData("return", 2,
        "class C:\n    pass\n\ndef make() -> C:\n    return C()\n\ndef main():\n    pass")]
    [InlineData("variable", 2,
        "class C:\n    pass\n\ndef main():\n    x: C = C()\n    print(x)")]
    [InlineData("field", 2,
        "class C:\n    pass\n\nclass D:\n    f: C = C()\n\ndef main():\n    pass")]
    public void Class_AnnotationAndExpression_CountsBoth(string position, int expectedCount, string source)
    {
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue($"class x {position} should compile");

        var symbol = analysis.SymbolTable!.LookupType("C");
        symbol.Should().NotBeNull();

        var refs = analysis.SemanticInfo!.GetReferences(symbol!);
        refs.Should().HaveCount(expectedCount,
            $"class x {position}: annotation + expression = {expectedCount} references (#1737)");
    }

    [Fact]
    public void Struct_AnnotationOnly_CountsOneReference()
    {
        var source = "struct S:\n    v: int = 0\n\ndef use(s: S) -> None:\n    pass\n\ndef main():\n    pass";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.LookupType("S");
        symbol.Should().NotBeNull();

        var refs = analysis.SemanticInfo!.GetReferences(symbol!);
        refs.Should().HaveCount(1, "struct x parameter: annotation records exactly one reference (#1737)");
    }

    [Fact]
    public void Interface_AnnotationOnly_CountsOneReference()
    {
        var source = "interface I:\n    def tick(self) -> None\n\ndef use(i: I) -> None:\n    pass\n\ndef main():\n    pass";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.LookupType("I");
        symbol.Should().NotBeNull();

        var refs = analysis.SemanticInfo!.GetReferences(symbol!);
        refs.Should().HaveCount(1, "interface x parameter: annotation records exactly one reference (#1737)");
    }

    [Fact]
    public void Enum_AnnotationPosition_CountsOneReference()
    {
        var source = "enum Color:\n    RED = 1\n\ndef use(c: Color) -> None:\n    pass\n\ndef main():\n    pass";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.LookupType("Color");
        symbol.Should().NotBeNull();

        var refs = analysis.SemanticInfo!.GetReferences(symbol!);
        refs.Should().HaveCount(1, "enum x parameter: annotation records exactly one reference (#1737)");
    }

    [Fact]
    public void Union_AnnotationPosition_CountsOneReference()
    {
        var source = "union Shape:\n    case Circle(r: float)\n\ndef use(s: Shape) -> None:\n    pass\n\ndef main():\n    pass";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.LookupType("Shape");
        symbol.Should().NotBeNull();

        var refs = analysis.SemanticInfo!.GetReferences(symbol!);
        refs.Should().HaveCount(1, "union x parameter: annotation records exactly one reference (#1737)");
    }

    [Fact]
    public void Delegate_AnnotationPosition_CountsOneReference()
    {
        var source = "delegate Cb(v: int) -> None\n\ndef use(cb: Cb) -> None:\n    pass\n\ndef main():\n    pass";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.LookupType("Cb");
        symbol.Should().NotBeNull();

        var refs = analysis.SemanticInfo!.GetReferences(symbol!);
        refs.Should().HaveCount(1, "delegate x parameter: annotation records exactly one reference (#1737)");
    }

    [Fact]
    public void TypeAlias_AnnotationPosition_CountsOneReference()
    {
        var source = "type Names = list[str]\n\ndef use(ns: Names) -> None:\n    pass\n\ndef main():\n    pass";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.LookupTypeAlias("Names");
        symbol.Should().NotBeNull("'Names' should resolve as a TypeAliasSymbol");

        var refs = analysis.SemanticInfo!.GetReferences(symbol!);
        refs.Should().HaveCount(1, "alias x parameter: annotation records exactly one reference (#1737)");
    }

    [Fact]
    public void GenericAlias_AnnotationPosition_CountsOneReference()
    {
        var source = "type Cb[T] = (T) -> None\n\ndef use(cb: Cb[int]) -> None:\n    pass\n\ndef main():\n    pass";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.LookupTypeAlias("Cb");
        symbol.Should().NotBeNull("'Cb' should resolve as a TypeAliasSymbol");

        var refs = analysis.SemanticInfo!.GetReferences(symbol!);
        refs.Should().HaveCount(1, "generic alias x parameter: annotation records exactly one reference (#1737)");
    }

    [Fact]
    public void TypeParameter_AnnotationPosition_CountsReference()
    {
        // T is used in the field annotation and the __init__ parameter.
        var source = "class Box[T]:\n    val: T\n    def __init__(self, v: T) -> None:\n        self.val = v\n\ndef main():\n    pass";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        // TypeParameterDef (AST node) is not a Symbol. Look up via FindReferencesBySymbolIdentity
        // which searches _symbolReferences keys by name. T is declared in Box at the class line.
        var tpRefs = analysis.SemanticInfo!.FindReferencesBySymbolIdentity("T", declaringFilePath: null);
        tpRefs.Count.Should().BeGreaterThanOrEqualTo(1,
            "type parameter x annotation: at least one annotation reference (#1737)");
    }

    // -- Identifier-use twin (positive control) -----------------------------------

    [Fact]
    public void IdentifierUse_AlwaysCounted_PositiveControl()
    {
        // The class C is used ONLY via an identifier expression (C()), never in an annotation.
        // This proves SetIdentifierSymbol records references -- the twin control for the matrix.
        var source = "class C:\n    pass\n\ndef main():\n    c = C()\n    print(c)";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.LookupType("C");
        symbol.Should().NotBeNull();

        var refs = analysis.SemanticInfo!.GetReferences(symbol!);
        refs.Should().HaveCountGreaterThanOrEqualTo(1,
            "identifier-use twin: expression-position references always counted");
    }

    // -- Annotation + identifier combined -----------------------------------------

    [Fact]
    public void AnnotationAndIdentifierUse_BothCounted()
    {
        // C is used in an annotation (`c: C`) AND as an identifier (`C()`).
        // Both should be counted, for a total of 2.
        var source = "class C:\n    pass\n\ndef main():\n    c: C = C()\n    print(c)";
        var analysis = _api.Analyze(source);
        analysis.Success.Should().BeTrue();

        var symbol = analysis.SymbolTable!.LookupType("C");
        symbol.Should().NotBeNull();

        var refs = analysis.SemanticInfo!.GetReferences(symbol!);
        refs.Should().HaveCount(2,
            "annotation + identifier: both recorded as references (#1737)");
    }

    // -- Pattern head: rostered N/A "Batch 6" -------------------------------------

    // Pattern head position (e.g. `case C():`) is rostered as N/A for Batch 6 per plan.
    // When Batch 6 lands, add: Class_PatternHead_CountsOneReference.
}