using System.Linq;
using FluentAssertions;
using Xunit;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Pretty;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Parser;

/// <summary>
/// Parser tests for property observer clauses (<c>before_set</c>/<c>after_set</c>, #416). The
/// clauses are always parsed regardless of the experimental <c>property_observers</c> flag;
/// gating is enforced later in semantic analysis, so these tests assert only on the AST shape
/// and on round-trip stability (the #1075 lesson).
/// </summary>
public class PropertyObserverParserTests
{
    private static Module Parse(string source)
    {
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        var module = parser.ParseModule();
        parser.Diagnostics.HasErrors.Should().BeFalse(
            "observer source should parse cleanly: "
            + string.Join("\n", parser.Diagnostics.GetErrors().Select(d => d.Message)));
        return module;
    }

    private static PropertyDef FirstProperty(Module module)
    {
        var cls = module.Body.OfType<ClassDef>().First();
        return cls.Body.OfType<PropertyDef>().First();
    }

    [Fact]
    public void AutoProperty_WithoutObservers_HasEmptyObservers()
    {
        var module = Parse("class C:\n    property health: int = 100\n");
        var prop = FirstProperty(module);

        prop.IsFunctionStyle.Should().BeFalse();
        prop.Observers.Should().BeEmpty();
    }

    [Fact]
    public void BeforeSet_ParsesWithExplicitParameter()
    {
        var module = Parse(
            "class C:\n    property health: int\n        before_set(new_value):\n            assert new_value >= 0\n");
        var prop = FirstProperty(module);

        prop.Observers.Should().ContainSingle();
        var observer = prop.Observers[0];
        observer.Kind.Should().Be(ObserverKind.BeforeSet);
        observer.ParamName.Should().Be("new_value");
        observer.Body.Should().ContainSingle()
            .Which.Should().BeOfType<AssertStatement>();
    }

    [Fact]
    public void AfterSet_ParsesWithExplicitParameter()
    {
        var module = Parse(
            "class C:\n    property health: int\n        after_set(old_value):\n            print(old_value)\n");
        var prop = FirstProperty(module);

        prop.Observers.Should().ContainSingle();
        var observer = prop.Observers[0];
        observer.Kind.Should().Be(ObserverKind.AfterSet);
        observer.ParamName.Should().Be("old_value");
    }

    [Fact]
    public void BothObservers_ParseInDeclaredOrder()
    {
        var module = Parse(
            "class C:\n    property health: int = 100\n"
            + "        before_set(new_value):\n            assert new_value >= 0\n"
            + "        after_set(old_value):\n            print(old_value)\n");
        var prop = FirstProperty(module);

        prop.DefaultValue.Should().BeOfType<IntegerLiteral>();
        prop.Observers.Should().HaveCount(2);
        prop.Observers[0].Kind.Should().Be(ObserverKind.BeforeSet);
        prop.Observers[1].Kind.Should().Be(ObserverKind.AfterSet);
    }

    [Fact]
    public void ObserverBodies_AreExposedAsChildNodes()
    {
        var module = Parse(
            "class C:\n    property health: int\n"
            + "        before_set(new_value):\n            assert new_value >= 0\n"
            + "        after_set(old_value):\n            print(old_value)\n");
        var prop = FirstProperty(module);

        var children = prop.GetChildNodes().ToList();
        children.Should().Contain(prop.Observers[0].Body[0]);
        children.Should().Contain(prop.Observers[1].Body[0]);
    }

    [Fact]
    public void UnknownObserverName_ReportsError()
    {
        var source = "class C:\n    property health: int\n        willset(v):\n            pass\n";
        var lexer = new LexerNs.Lexer(source);
        var tokens = lexer.TokenizeAll();
        var parser = new ParserNs.Parser(tokens);
        _ = parser.ParseModule();

        parser.Diagnostics.HasErrors.Should().BeTrue();
        parser.Diagnostics.GetErrors().Select(d => d.Message)
            .Should().Contain(m => m.Contains("before_set") && m.Contains("after_set"));
    }

    [Fact]
    public void Observers_RoundTripThroughUnparser()
    {
        var module = Parse(
            "class C:\n    property health: int = 100\n"
            + "        before_set(new_value):\n            assert new_value >= 0\n"
            + "        after_set(old_value):\n            print(old_value)\n");

        var unparsed = Unparser.Unparse(module);
        var reparsed = Parse(unparsed);

        var normalizedA = AstNormalizer.Instance.NormalizeModule(module);
        var normalizedB = AstNormalizer.Instance.NormalizeModule(reparsed);
        StructuralEqualityComparer.Instance.Equals(normalizedA, normalizedB).Should().BeTrue(
            "unparsed observers must reparse to a structurally identical AST; got:\n" + unparsed);
    }
}
