using System.Collections.Immutable;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Xunit;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// P22b Phase 3 Task 1 (#2157): every name-carrying node records whether its name was written
/// backtick-escaped, copied from the consumed token's <c>IsBacktickEscaped</c>. The flags are
/// syntax facts for the unparser and the structural comparer only — the joined <c>Name</c>/
/// <c>Module</c> spelling every Semantic/LSP consumer reads is unchanged, and each cell asserts it.
/// One positive/negative pair per record: the escaped spelling sets the flag, the plain spelling
/// does not (the negative cell is the control that the flag is not simply always true).
/// </summary>
public class ParserBacktickEscapeCarrierTests
{
    private static Module Parse(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        lexer.Diagnostics.HasErrors.Should().BeFalse("the source must lex cleanly: " + source);
        var parser = new ParserNs.Parser(tokens);
        var module = parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeFalse(
            "the source must parse: " + source + " — "
            + string.Join(" | ", parser.Diagnostics.GetErrors().Select(d => $"{d.Code} {d.Message}")));
        return module;
    }

    private static FunctionCall Call(string source) =>
        Parse(source).Body.Single().Should().BeOfType<ExpressionStatement>().Subject
            .Expression.Should().BeOfType<FunctionCall>().Subject;

    private static Pattern CasePattern(string source) =>
        Parse(source).Body.Single().Should().BeOfType<MatchStatement>().Subject.Cases[0].Pattern;

    // --- KeywordArgument ---------------------------------------------------------------------

    [Fact]
    public void CallKwarg_Escaped_SetsFlag()
    {
        var kw = Call("f(`class`=7)\n").KeywordArguments.Single();
        kw.Name.Should().Be("class");
        kw.IsNameBacktickEscaped.Should().BeTrue();
    }

    [Fact]
    public void CallKwarg_Plain_LeavesFlagClear()
    {
        var kw = Call("f(x=7)\n").KeywordArguments.Single();
        kw.Name.Should().Be("x");
        kw.IsNameBacktickEscaped.Should().BeFalse();
    }

    [Fact]
    public void DecoratorKwarg_EscapedAndPlain_EachCarriesItsOwnFlag()
    {
        var def = Parse("@d(`k`=1, j=2)\ndef g():\n    pass\n").Body.Single()
            .Should().BeOfType<FunctionDef>().Subject;
        var kwargs = def.Decorators.Single().KeywordArguments;
        kwargs.Select(k => (k.Name, k.IsNameBacktickEscaped))
            .Should().Equal(("k", true), ("j", false));
    }

    // --- ImportAlias (import) ------------------------------------------------------------------

    [Fact]
    public void Import_EscapedParts_AndAlias_SetFlags()
    {
        var alias = Parse("import a.`b`.c as `m`\n").Body.Single()
            .Should().BeOfType<ImportStatement>().Subject.Names.Single();
        alias.Name.Should().Be("a.b.c");
        alias.NameParts.Should().Equal("a", "b", "c");
        alias.BacktickEscapedParts.Should().Equal(false, true, false);
        alias.AsName.Should().Be("m");
        alias.IsAsNameBacktickEscaped.Should().BeTrue();
    }

    [Fact]
    public void Import_Plain_LeavesFlagsClear()
    {
        var alias = Parse("import a.b.c as m\n").Body.Single()
            .Should().BeOfType<ImportStatement>().Subject.Names.Single();
        alias.Name.Should().Be("a.b.c");
        alias.NameParts.Should().Equal("a", "b", "c");
        alias.BacktickEscapedParts.Should().Equal(false, false, false);
        alias.IsAsNameBacktickEscaped.Should().BeFalse();
    }

    [Fact]
    public void Import_SingleEscapedDottedToken_IsOnePart()
    {
        // #713: one backtick token that already contains dots is the whole dotted path.
        var alias = Parse("import `System.Collections.Generic`\n").Body.Single()
            .Should().BeOfType<ImportStatement>().Subject.Names.Single();
        alias.Name.Should().Be("System.Collections.Generic");
        alias.NameParts.Should().Equal("System.Collections.Generic");
        alias.BacktickEscapedParts.Should().Equal(true);
        alias.AsName.Should().BeNull();
        alias.IsAsNameBacktickEscaped.Should().BeFalse();
    }

    // --- FromImportStatement + its ImportAlias items -------------------------------------------

    [Fact]
    public void FromImport_EscapedModulePart_Name_AndAlias_SetFlags()
    {
        var from = Parse("from ..a.`b` import `x` as `y`\n").Body.Single()
            .Should().BeOfType<FromImportStatement>().Subject;
        from.Module.Should().Be("..a.b");
        from.ModuleParts.Should().Equal("a", "b");
        from.BacktickEscapedParts.Should().Equal(false, true);
        var item = from.Names.Single();
        item.Name.Should().Be("x");
        item.NameParts.Should().Equal("x");
        item.BacktickEscapedParts.Should().Equal(true);
        item.AsName.Should().Be("y");
        item.IsAsNameBacktickEscaped.Should().BeTrue();
    }

    [Fact]
    public void FromImport_Plain_LeavesFlagsClear()
    {
        var from = Parse("from ..a.b import x as y\n").Body.Single()
            .Should().BeOfType<FromImportStatement>().Subject;
        from.Module.Should().Be("..a.b");
        from.ModuleParts.Should().Equal("a", "b");
        from.BacktickEscapedParts.Should().Equal(false, false);
        var item = from.Names.Single();
        item.BacktickEscapedParts.Should().Equal(false);
        item.IsAsNameBacktickEscaped.Should().BeFalse();
    }

    [Fact]
    public void FromImport_DotsOnlyModule_HasNoParts()
    {
        var from = Parse("from . import x\n").Body.Single()
            .Should().BeOfType<FromImportStatement>().Subject;
        from.Module.Should().Be(".");
        from.ModuleParts.Should().BeEmpty();
        from.BacktickEscapedParts.Should().BeEmpty();
    }

    // --- UnionCaseField ------------------------------------------------------------------------

    [Fact]
    public void UnionCaseField_EscapedAndPlain_EachCarriesItsOwnFlag()
    {
        var union = Parse("union Shape:\n    case K(`class`: int, radius: float)\n").Body.Single()
            .Should().BeOfType<UnionDef>().Subject;
        union.Cases.Single().Fields.Select(f => (f.Name, f.IsNameBacktickEscaped))
            .Should().Equal(("class", true), ("radius", false));
    }

    // --- PropertyObserver ----------------------------------------------------------------------

    [Fact]
    public void ObserverParam_Escaped_SetsFlag()
    {
        var obs = Parse("class C:\n    property health: int\n        after_set(`class`):\n            print(`class`)\n")
            .Body.OfType<ClassDef>().Single().Body.OfType<PropertyDef>().Single().Observers.Single();
        obs.ParamName.Should().Be("class");
        obs.IsParamNameBacktickEscaped.Should().BeTrue();
    }

    [Fact]
    public void ObserverParam_Plain_LeavesFlagClear()
    {
        var obs = Parse("class C:\n    property health: int\n        after_set(old):\n            print(old)\n")
            .Body.OfType<ClassDef>().Single().Body.OfType<PropertyDef>().Single().Observers.Single();
        obs.ParamName.Should().Be("old");
        obs.IsParamNameBacktickEscaped.Should().BeFalse();
    }

    // --- PropertyPatternField ------------------------------------------------------------------

    [Fact]
    public void KeywordPatternField_EscapedAndPlain_EachCarriesItsOwnFlag()
    {
        var pattern = CasePattern("match p:\n    case P(`class`=v, y=0):\n        pass\n")
            .Should().BeOfType<PropertyPattern>().Subject;
        pattern.Fields.Select(f => (f.Name, f.IsNameBacktickEscaped))
            .Should().Equal(("class", true), ("y", false));
    }

    // --- MemberAccessPattern -------------------------------------------------------------------

    [Fact]
    public void MemberAccessPattern_EscapedPart_SetsFlag()
    {
        var pattern = CasePattern("match e:\n    case `E`.`A`:\n        pass\n    case F.B:\n        pass\n")
            .Should().BeOfType<MemberAccessPattern>().Subject;
        pattern.Parts.Should().Equal("E", "A");
        pattern.BacktickEscapedParts.Should().Equal(true, true);
    }

    // --- TypeAnnotation: dotted name segments (syntax-only; resolution flag unchanged) ---------

    private static TypeAnnotation AnnotationOf(string source) =>
        Parse(source).Body.Single().Should().BeOfType<VariableDeclaration>().Subject.Type!;

    [Fact]
    public void DottedTypeAnnotation_EscapedSegments_RecordParts_ResolutionFlagStaysFalse()
    {
        var type = AnnotationOf("x: `a`.B.`C` = y\n");
        type.Name.Should().Be("a.B.C");
        type.NameParts.Should().Equal("a", "B", "C");
        type.BacktickEscapedParts.Should().Equal(true, false, true);
        // The dotted-name resolution rule (Types.cs: "Always false for dotted names") is untouched.
        type.IsNameBacktickEscaped.Should().BeFalse();
    }

    [Fact]
    public void DottedTypeAnnotation_Plain_RecordsUnescapedParts()
    {
        var type = AnnotationOf("x: a.B = y\n");
        type.NameParts.Should().Equal("a", "B");
        type.BacktickEscapedParts.Should().Equal(false, false);
    }

    [Fact]
    public void SingleSegmentTypeAnnotation_HasNoParts()
    {
        var type = AnnotationOf("x: `T` = y\n");
        type.NameParts.Should().BeEmpty();
        type.IsNameBacktickEscaped.Should().BeTrue();
    }

    [Fact]
    public void DottedClassPatternHead_RecordsParts()
    {
        var pattern = CasePattern("match p:\n    case lib.`Circle`():\n        pass\n");
        var type = pattern switch
        {
            PositionalPattern pp => pp.Type!,
            PropertyPattern prop => prop.Type!,
            TypePattern tp => tp.Type,
            _ => throw new Xunit.Sdk.XunitException($"unexpected pattern {pattern.GetType().Name}")
        };
        type.Name.Should().Be("lib.Circle");
        type.NameParts.Should().Equal("lib", "Circle");
        type.BacktickEscapedParts.Should().Equal(false, true);
    }

    // --- Tuple element names: tuple type and named-tuple literal -------------------------------

    [Fact]
    public void TupleTypeElementNames_EscapedAndPlain_EachCarriesItsOwnFlag()
    {
        var type = AnnotationOf("x: tuple[`a`: int, b: int] = y\n");
        type.TupleElementNames.Should().Equal("a", "b");
        type.TupleElementNamesBacktickEscaped.Should().Equal(true, false);
    }

    [Fact]
    public void TupleTypeElementNames_Plain_RecordNothing()
    {
        AnnotationOf("x: tuple[a: int, b: int] = y\n").TupleElementNamesBacktickEscaped.Should().BeEmpty();
    }

    [Fact]
    public void NamedTupleLiteral_EscapedAndPlain_EachCarriesItsOwnFlag()
    {
        var tuple = Parse("t = (`a`=1, b=2)\n").Body.Single().Should().BeOfType<Assignment>().Subject
            .Value.Should().BeOfType<TupleLiteral>().Subject;
        tuple.ElementNames.Should().Equal("a", "b");
        tuple.ElementNamesBacktickEscaped.Should().Equal(true, false);
    }

    [Fact]
    public void NamedTupleLiteral_Plain_RecordsNothing()
    {
        var tuple = Parse("t = (a=1, b=2)\n").Body.Single().Should().BeOfType<Assignment>().Subject
            .Value.Should().BeOfType<TupleLiteral>().Subject;
        tuple.ElementNamesBacktickEscaped.Should().BeEmpty();
    }

    // --- #713 single escaped dotted token in expression position ------------------------------

    private static MemberAccess ExpressionOf(string source) =>
        Parse(source).Body.Single().Should().BeOfType<Assignment>().Subject
            .Value.Should().BeOfType<MemberAccess>().Subject;

    [Fact]
    public void SingleEscapedDottedToken_MarksTheChainEnd_Only()
    {
        // `a.b.c`.d — the token closes at `c`, so only that MemberAccess carries the mark; the
        // outer `.d` and the inner `.b` do not.
        var outer = ExpressionOf("x = `a.b.c`.d\n");
        outer.Member.Should().Be("d");
        outer.IsSingleEscapedTokenChain.Should().BeFalse();
        var tokenEnd = outer.Object.Should().BeOfType<MemberAccess>().Subject;
        tokenEnd.Member.Should().Be("c");
        tokenEnd.IsSingleEscapedTokenChain.Should().BeTrue();
        var inner = tokenEnd.Object.Should().BeOfType<MemberAccess>().Subject;
        inner.Member.Should().Be("b");
        inner.IsSingleEscapedTokenChain.Should().BeFalse();
        inner.Object.Should().BeOfType<Identifier>().Which.IsNameBacktickEscaped.Should().BeTrue();
    }

    [Fact]
    public void PerSegmentEscapes_AreNotASingleTokenChain()
    {
        var tokenEnd = ExpressionOf("x = `a`.`b`\n");
        tokenEnd.IsMemberBacktickEscaped.Should().BeTrue();
        tokenEnd.IsSingleEscapedTokenChain.Should().BeFalse();
    }

    // --- AsPattern capture (the one capture site that did not copy the escape) -----------------

    [Fact]
    public void AsPatternCapture_Escaped_SetsFlag()
    {
        var pattern = CasePattern("match p:\n    case int() as `n`:\n        pass\n")
            .Should().BeOfType<AsPattern>().Subject;
        pattern.Name.Name.Should().Be("n");
        pattern.Name.IsNameBacktickEscaped.Should().BeTrue();
    }

    [Fact]
    public void AsPatternCapture_Plain_LeavesFlagClear()
    {
        var pattern = CasePattern("match p:\n    case int() as n:\n        pass\n")
            .Should().BeOfType<AsPattern>().Subject;
        pattern.Name.IsNameBacktickEscaped.Should().BeFalse();
    }

    // --- PropertyDef explicit-interface qualifier ----------------------------------------------

    private static PropertyDef PropertyOf(string source) =>
        Parse(source).Body.OfType<ClassDef>().Single().Body.OfType<PropertyDef>().Single();

    [Fact]
    public void ExplicitInterfaceQualifier_Escaped_SetsFlag_BothPropertyForms()
    {
        var auto = PropertyOf("class C(IFoo):\n    property `IFoo`.x: int = 3\n");
        auto.ExplicitInterface.Should().Be("IFoo");
        auto.IsExplicitInterfaceBacktickEscaped.Should().BeTrue();

        var fn = PropertyOf("class C(IFoo):\n    property get `IFoo`.x(self) -> int:\n        return 3\n");
        fn.ExplicitInterface.Should().Be("IFoo");
        fn.IsExplicitInterfaceBacktickEscaped.Should().BeTrue();
    }

    [Fact]
    public void ExplicitInterfaceQualifier_Plain_LeavesFlagClear()
    {
        var auto = PropertyOf("class C(IFoo):\n    property IFoo.x: int = 3\n");
        auto.ExplicitInterface.Should().Be("IFoo");
        auto.IsExplicitInterfaceBacktickEscaped.Should().BeFalse();
    }

    [Fact]
    public void MemberAccessPattern_MixedAndPlain_CarryPerPartFlags()
    {
        var match = Parse("match e:\n    case E.`A`:\n        pass\n    case F.B:\n        pass\n").Body.Single()
            .Should().BeOfType<MatchStatement>().Subject;
        var mixed = match.Cases[0].Pattern.Should().BeOfType<MemberAccessPattern>().Subject;
        mixed.BacktickEscapedParts.Should().Equal(false, true);
        var plain = match.Cases[1].Pattern.Should().BeOfType<MemberAccessPattern>().Subject;
        plain.Parts.Should().Equal("F", "B");
        plain.BacktickEscapedParts.Should().Equal(false, false);
    }
}
