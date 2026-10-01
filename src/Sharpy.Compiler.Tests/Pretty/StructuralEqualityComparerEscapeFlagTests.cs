using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Pretty;
using Xunit;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.PrettyTests;

/// <summary>
/// P22b Phase 3 Task 2 (#2157): the structural comparer sees every backtick-escape flag, so a
/// formatter round-trip that drops an escape is an inequality, not a silent pass. One cell per flag:
/// the escaped spelling and the plain spelling of the same program parse to ASTs the comparer calls
/// DIFFERENT; the positive control (the escaped spelling parsed twice is EQUAL) proves each cell
/// is not red for an unrelated reason.
/// </summary>
public class StructuralEqualityComparerEscapeFlagTests
{
    private static Module Parse(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        lexer.Diagnostics.HasErrors.Should().BeFalse("the source must lex: " + source);
        var parser = new ParserNs.Parser(tokens);
        var module = parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeFalse(
            "the source must parse: " + source + " — "
            + string.Join(" | ", parser.Diagnostics.GetErrors().Select(d => $"{d.Code} {d.Message}")));
        return AstNormalizer.Instance.NormalizeModule(module);
    }

    public static TheoryData<string, string, string> Pairs => new()
    {
        { "call kwarg (KeywordArgument)", "f(`x`=1)\n", "f(x=1)\n" },
        { "decorator kwarg (KeywordArgument)", "@d(`k`=1)\ndef g():\n    pass\n", "@d(k=1)\ndef g():\n    pass\n" },
        { "decorator name part (Decorator.BacktickEscapedParts)", "@a.`d`\ndef g():\n    pass\n", "@a.d\ndef g():\n    pass\n" },
        { "import part (ImportAlias.BacktickEscapedParts)", "import a.`b`\n", "import a.b\n" },
        { "import #713 one escaped dotted token (ImportAlias.NameParts)", "import `a.b`\n", "import a.b\n" },
        { "import as (ImportAlias.IsAsNameBacktickEscaped)", "import m as `x`\n", "import m as x\n" },
        { "from module part (FromImportStatement.BacktickEscapedParts)", "from .`a` import x\n", "from .a import x\n" },
        { "from-import name (ImportAlias.BacktickEscapedParts)", "from a import `x`\n", "from a import x\n" },
        { "from-import as (ImportAlias.IsAsNameBacktickEscaped)", "from a import x as `y`\n", "from a import x as y\n" },
        { "except as (ExceptHandler.IsNameBacktickEscaped)", "try:\n    pass\nexcept E as `e`:\n    pass\n", "try:\n    pass\nexcept E as e:\n    pass\n" },
        { "type alias (TypeAlias.IsNameBacktickEscaped)", "type `T` = int\n", "type T = int\n" },
        { "union case name (UnionCaseDef.IsNameBacktickEscaped)", "union U:\n    case `K`(x: int)\n", "union U:\n    case K(x: int)\n" },
        { "union case field (UnionCaseField.IsNameBacktickEscaped)", "union U:\n    case K(`x`: int)\n", "union U:\n    case K(x: int)\n" },
        { "observer parameter (PropertyObserver.IsParamNameBacktickEscaped)", "class C:\n    property h: int\n        after_set(`o`):\n            pass\n", "class C:\n    property h: int\n        after_set(o):\n            pass\n" },
        { "keyword pattern field (PropertyPatternField.IsNameBacktickEscaped)", "match p:\n    case P(`x`=v):\n        pass\n", "match p:\n    case P(x=v):\n        pass\n" },
        { "member-access pattern part (MemberAccessPattern.BacktickEscapedParts)", "match p:\n    case E.`A`:\n        pass\n", "match p:\n    case E.A:\n        pass\n" },
        { "member access (MemberAccess.IsMemberBacktickEscaped)", "o.`m`\n", "o.m\n" },
        { "type annotation (TypeAnnotation.IsNameBacktickEscaped)", "x: `T` = 1\n", "x: T = 1\n" },
        { "dotted type annotation segment (TypeAnnotation.BacktickEscapedParts)", "x: a.`B` = 1\n", "x: a.B = 1\n" },
        { "dotted class-pattern head segment (TypeAnnotation.BacktickEscapedParts)", "match p:\n    case lib.`C`():\n        pass\n", "match p:\n    case lib.C():\n        pass\n" },
        { "tuple type element name (TypeAnnotation.TupleElementNamesBacktickEscaped)", "x: tuple[`a`: int] = t\n", "x: tuple[a: int] = t\n" },
        { "named tuple literal element name (TupleLiteral.ElementNamesBacktickEscaped)", "t = (`a`=1, b=2)\n", "t = (a=1, b=2)\n" },
        { "as-pattern capture (AsPattern.Name.IsNameBacktickEscaped)", "match p:\n    case int() as `n`:\n        pass\n", "match p:\n    case int() as n:\n        pass\n" },
        { "explicit-interface qualifier (PropertyDef.IsExplicitInterfaceBacktickEscaped)", "class C(I):\n    property get `I`.x(self) -> int:\n        return 3\n", "class C(I):\n    property get I.x(self) -> int:\n        return 3\n" },
        { "explicit-interface qualifier, auto property (PropertyDef.IsExplicitInterfaceBacktickEscaped)", "class C(I):\n    property `I`.x: int = 3\n", "class C(I):\n    property I.x: int = 3\n" },
    };

    [Theory]
    [MemberData(nameof(Pairs))]
    public void EscapedAndPlainSpellings_AreNotEqual(string cell, string escaped, string plain)
    {
        StructuralEqualityComparer.Instance.Equals(Parse(escaped), Parse(plain))
            .Should().BeFalse($"{cell}: the escape is part of what the user wrote, so dropping it must be an inequality");
    }

    [Theory]
    [MemberData(nameof(Pairs))]
    public void EscapedSpelling_EqualsItself(string cell, string escaped, string plain)
    {
        _ = plain;
        StructuralEqualityComparer.Instance.Equals(Parse(escaped), Parse(escaped))
            .Should().BeTrue($"{cell}: positive control — the same spelling parsed twice is equal");
    }

    /// <summary>
    /// Non-escape structural facts the unparser writes but the comparer used to ignore (P22b
    /// structure fixes): each pair is two different programs, so a round-trip that swapped one for
    /// the other must be an inequality; the first source parsed twice is the positive control.
    /// </summary>
    public static TheoryData<string, string, string> StructurePairs => new()
    {
        { "bracket attribute vs decorator (Decorator.IsBracketAttribute)", "@[d]\ndef g():\n    pass\n", "@d\ndef g():\n    pass\n" },
        { "union with vs without a method (UnionDef.Body)", "union U:\n    case K(x: int)\n\n    def f(self) -> int:\n        return 1\n", "union U:\n    case K(x: int)\n" },
        { "explicit-interface auto property vs public (PropertyDef.ExplicitInterface)", "class C(I):\n    property I.x: int = 3\n", "class C(I):\n    property x: int = 3\n" },
        { "as? vs as! (TypeCoercion.Mode)", "y = x as? int\n", "y = x as! int\n" },
        { "list-display vs tuple store target (TupleLiteral.IsListDisplay)", "[a, b] = t\n", "(a, b) = t\n" },
    };

    [Theory]
    [MemberData(nameof(StructurePairs))]
    public void StructurallyDifferentPrograms_AreNotEqual(string cell, string first, string second)
    {
        StructuralEqualityComparer.Instance.Equals(Parse(first), Parse(second))
            .Should().BeFalse($"{cell}: the two spellings are different programs");
        StructuralEqualityComparer.Instance.Equals(Parse(first), Parse(first))
            .Should().BeTrue($"{cell}: positive control — the same spelling parsed twice is equal");
    }

    [Fact]
    public void HandBuiltImport_EqualsItsParsedSpelling()
    {
        // A node not built by the parser (NameParts/ModuleParts empty) still equals the parse of
        // its unescaped spelling: a missing parts array reads as the joined name, unescaped.
        var handBuilt = new ImportStatement { Names = [new ImportAlias { Name = "a.b", AsName = "m" }] };
        var handBuiltFrom = new FromImportStatement { Module = "..a.b", Names = [new ImportAlias { Name = "x" }] };
        StructuralEqualityComparer.Instance.Equals(handBuilt, Parse("import a.b as m\n").Body[0]).Should().BeTrue();
        StructuralEqualityComparer.Instance.Equals(handBuiltFrom, Parse("from ..a.b import x\n").Body[0]).Should().BeTrue();
    }
}
