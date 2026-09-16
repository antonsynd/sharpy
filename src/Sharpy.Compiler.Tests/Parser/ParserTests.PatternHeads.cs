using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Explicit type arguments in pattern heads — arm 2 of the reification ruling (#1708/#1619).
/// <c>case list[int](xs)</c>, <c>case Box[int]()</c>, <c>case list[int]([1, 2])</c> and their
/// nested/dotted/or/as/match-expression spellings parse into a <see cref="TypeAnnotation"/> whose
/// <see cref="TypeAnnotation.TypeArguments"/> are populated. SPY0125 is retired: the head is no
/// longer refused by the parser.
/// </summary>
public partial class ParserTests
{
    #region Pattern-head type arguments (#1708, #1619, #1702)

    private static (Module Module, ParserNs.Parser Parser) ParseWithParser(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        var module = parser.ParseModule();
        return (module, parser);
    }

    /// <summary>The first pattern in the module, whether a match statement or a match expression.</summary>
    private static Pattern FirstMatchPattern(Module module)
    {
        foreach (var stmt in module.Body)
        {
            if (stmt is MatchStatement ms)
                return ms.Cases[0].Pattern;
            if (stmt is VariableDeclaration { InitialValue: MatchExpression me })
                return me.Arms[0].Pattern;
        }

        throw new Xunit.Sdk.XunitException("no match statement or expression found in module");
    }

    /// <summary>
    /// The <see cref="TypeAnnotation"/> of the innermost pattern head carrying explicit type
    /// arguments, walking through the structural pattern nodes a head can be nested in.
    /// </summary>
    private static TypeAnnotation? FindGenericHeadType(Pattern p) => p switch
    {
        PositionalPattern { Type: { TypeArguments.Length: > 0 } t } => t,
        TypePattern tp when tp.Type.TypeArguments.Length > 0 => tp.Type,
        PropertyPattern { Type: { TypeArguments.Length: > 0 } t } => t,
        PositionalPattern pos => pos.Elements.Select(FindGenericHeadType).FirstOrDefault(t => t != null),
        ListPattern lp => lp.Elements.Select(FindGenericHeadType).FirstOrDefault(t => t != null),
        TuplePattern tup => tup.Elements.Select(FindGenericHeadType).FirstOrDefault(t => t != null),
        OrPattern op => op.Alternatives.Select(FindGenericHeadType).FirstOrDefault(t => t != null),
        AsPattern ap => FindGenericHeadType(ap.Inner),
        StarPattern { Capture: { } c } => FindGenericHeadType(c),
        _ => null,
    };

    public static IEnumerable<object[]> PatternHeadTypeArgumentCells()
    {
        // spelling × head-position matrix. Each cell: name, source, outer-pattern kind, arg count.
        // Every cell asserts the head node type carries populated TypeArguments and NO diagnostics.

        // bare generic × top positional
        yield return new object[] { "bare-generic-top-positional", @"
match x:
    case list[int](xs):
        pass
", "positional", 1 };

        // user generic × top zero-arg (TypePattern)
        yield return new object[] { "user-generic-top-zeroarg", @"
match x:
    case Box[int]():
        pass
", "type", 1 };

        // dotted generic × top zero-arg (TypePattern)
        yield return new object[] { "dotted-generic-top-zeroarg", @"
match x:
    case lib.Box[int]():
        pass
", "type", 1 };

        // nested generic argument (list[list[int]]) × top positional
        yield return new object[] { "nested-generic-argument-top", @"
match x:
    case list[list[int]](xs):
        pass
", "positional", 1 };

        // tuple argument (dict[str, tuple[int, int]]) × top positional (arity 2)
        yield return new object[] { "tuple-argument-top", @"
match x:
    case dict[str, tuple[int, int]](d):
        pass
", "positional", 2 };

        // type-parameter argument (list[T]) × top positional
        yield return new object[] { "typeparameter-argument-top", @"
match x:
    case list[T](xs):
        pass
", "positional", 1 };

        // bare generic × sequence element (ListPattern with nested positional head)
        yield return new object[] { "bare-generic-sequence-element", @"
match x:
    case [list[int](inner), *rest]:
        pass
", "list", 1 };

        // bare generic × class positional element (Box(list[int](xs)))
        yield return new object[] { "bare-generic-class-positional-element", @"
match x:
    case Box(list[int](xs)):
        pass
", "positional", 1 };

        // bare generic × or-pattern alternative
        yield return new object[] { "bare-generic-or-alternative", @"
match x:
    case list[int](xs) | Box():
        pass
", "or", 1 };

        // bare generic × as-inner
        yield return new object[] { "bare-generic-as-inner", @"
match x:
    case list[int](xs) as n:
        pass
", "as", 1 };

        // property form (Box[int](value=v))
        yield return new object[] { "user-generic-property-form", @"
match x:
    case Box[int](value=v):
        pass
", "property", 1 };

        // sequence-pattern steer spelling (R-D): list[int]([1, 2]) — [ after ] is a sub-pattern
        yield return new object[] { "sequence-steer-spelling", @"
match x:
    case list[int]([1, 2]):
        pass
", "positional", 1 };

        // match expression head
        yield return new object[] { "bare-generic-match-expression", @"
v: int = match x:
    case list[int](xs): 1
    case _: 0
", "positional", 1 };
    }

    [Theory]
    [MemberData(nameof(PatternHeadTypeArgumentCells))]
    public void PatternHead_TypeArguments_Parse(
        string name, string source, string outerKind, int expectedArgCount)
    {
        _ = name;

        var (module, parser) = ParseWithParser(source);

        parser.Diagnostics.HasErrors.Should().BeFalse(
            "an explicit type-argument pattern head parses without diagnostics (SPY0125 retired); errors: "
            + string.Join(" | ", parser.Diagnostics.GetAll().Select(d => d.Message)));

        var pattern = FirstMatchPattern(module);

        switch (outerKind)
        {
            case "positional": pattern.Should().BeOfType<PositionalPattern>(); break;
            case "type": pattern.Should().BeOfType<TypePattern>(); break;
            case "property": pattern.Should().BeOfType<PropertyPattern>(); break;
            case "list": pattern.Should().BeOfType<ListPattern>(); break;
            case "or": pattern.Should().BeOfType<OrPattern>(); break;
            case "as": pattern.Should().BeOfType<AsPattern>(); break;
            default: throw new Xunit.Sdk.XunitException($"unknown outer kind '{outerKind}'");
        }

        var head = FindGenericHeadType(pattern);
        head.Should().NotBeNull("the pattern head must carry explicit type arguments");
        head!.TypeArguments.Length.Should().Be(
            expectedArgCount, $"`{name}` head names {expectedArgCount} type argument(s)");
    }

    [Fact]
    public void PatternHead_DottedGeneric_KeepsQualifiedName()
    {
        var (module, parser) = ParseWithParser(@"
match x:
    case lib.Box[int]():
        pass
");
        parser.Diagnostics.HasErrors.Should().BeFalse();
        var head = FindGenericHeadType(FirstMatchPattern(module));
        head!.Name.Should().Be("lib.Box", "the module qualifier is preserved on the annotation");
        head.TypeArguments.Should().ContainSingle().Which.Name.Should().Be("int");
    }

    [Fact]
    public void PatternHead_NestedGenericArgument_IsItselfGeneric()
    {
        var (module, parser) = ParseWithParser(@"
match x:
    case list[list[int]](xs):
        pass
");
        parser.Diagnostics.HasErrors.Should().BeFalse();
        var head = FindGenericHeadType(FirstMatchPattern(module));
        var inner = head!.TypeArguments.Should().ContainSingle().Subject;
        inner.Name.Should().Be("list");
        inner.TypeArguments.Should().ContainSingle().Which.Name.Should().Be("int");
    }

    [Fact]
    public void PatternHead_TypeArguments_SpanCoversBrackets()
    {
        // #1454 recorded-extent rule: the head's TypeAnnotation extent must include `[int]` so hover
        // and the reference seam see one node whose range covers the arguments.
        var (module, _) = ParseWithParser("match x:\n    case list[int](xs):\n        pass\n");
        var head = FindGenericHeadType(FirstMatchPattern(module));
        // `list` starts at column 9 (1-based lexer col), `]` closes the arguments; the annotation
        // must end at the `]`, past the bare name.
        head!.ColumnEnd.Should().BeGreaterThan(
            head.ColumnStart + "list".Length,
            "the annotation extent must include the `[int]` arguments, not stop at the name");
    }

    // Positive controls: malformed explicit spellings stay parse errors with a location.
    public static IEnumerable<object[]> MalformedPatternHeadCells()
    {
        // missing `]` in the argument list
        yield return new object[] { "missing-close-bracket", "match x:\n    case list[int(xs):\n        pass\n" };
        // empty argument list with no close
        yield return new object[] { "unterminated-arguments", "match x:\n    case dict[str, (d):\n        pass\n" };
    }

    [Theory]
    [MemberData(nameof(MalformedPatternHeadCells))]
    public void PatternHead_Malformed_ReportsError(string name, string source)
    {
        _ = name;
        var (_, parser) = ParseWithParser(source);
        parser.Diagnostics.HasErrors.Should().BeTrue(
            "a malformed explicit type-argument head is still a parse error");
        parser.Diagnostics.GetErrors().Should().Contain(
            d => d.Line > 0 && d.Column > 0, "the parse error carries a source location");
    }

    #endregion
}
