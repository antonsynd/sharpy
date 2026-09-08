using FluentAssertions;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The ONE classifier for dunder-driven interface synthesis is annotation-level:
/// <see cref="SynthesisAnalyzer.ClassifyDundersFromAst"/> reads a class body's dunder
/// declarations and yields the rows <c>NameResolver</c> enqueues into the supertype closure at
/// inheritance resolution (#1746, plan-499995 Design Decision 4). These tests drive it from
/// Sharpy SOURCE, so each row is pinned by the spelling a user writes, and the row ORDER is
/// pinned too — the emitted base list follows it and is snapshot-pinned.
/// </summary>
public class SynthesisAnalyzerTests
{
    /// <summary>The seven dunder → interface rows the classifier knows; a new row must be placed here.</summary>
    private const int RowCount = 7;

    private static List<(string InterfaceName, string Namespace, string[] TypeArgs, string TriggeringDunder)> Classify(string body)
    {
        var source = "class C:\n" + string.Join("\n", body.Split('\n').Select(l => "    " + l)) + "\n";
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var parser = new global::Sharpy.Compiler.Parser.Parser(lexer.TokenizeAll(), NullLogger.Instance);
        var module = parser.ParseModule();
        var classDef = module.Body.OfType<ClassDef>().Single();
        return SynthesisAnalyzer.ClassifyDundersFromAst(classDef.Body)
            .Select(r => (r.InterfaceName, r.Namespace, r.TypeArgAnnotations.Select(Spell).ToArray(), r.TriggeringDunder))
            .ToList();
    }

    private static string Spell(TypeAnnotation annotation)
    {
        var name = annotation.TypeArguments.Length == 0
            ? annotation.Name
            : $"{annotation.Name}[{string.Join(", ", annotation.TypeArguments.Select(Spell))}]";
        if (annotation.IsOptional)
            name += "?";
        else if (annotation.IsCSharpNullable)
            name += " | None";
        return name;
    }

    // ── one row per dunder ────────────────────────────────────────────────────────────────

    public static IEnumerable<object[]> SingleRows()
    {
        yield return new object[] { "def __len__(self) -> int:\n    return 0", "ISized", "Sharpy", Array.Empty<string>(), DunderNames.Len };
        yield return new object[] { "def __bool__(self) -> bool:\n    return True", "IBoolConvertible", "Sharpy", Array.Empty<string>(), DunderNames.Bool };
        yield return new object[] { "def __reversed__(self) -> str:\n    return \"\"", "IReverseEnumerable", "Sharpy", new[] { "str" }, DunderNames.Reversed };
        yield return new object[] { "def __reversed__(self):\n    return \"\"", "IReverseEnumerable", "Sharpy", new[] { "object" }, DunderNames.Reversed };
        yield return new object[] { "def __next__(self) -> int:\n    return 0", "IEnumerator", "System.Collections.Generic", new[] { "int" }, DunderNames.Next };
        yield return new object[] { "def __next__(self):\n    return 0", "IEnumerator", "System.Collections.Generic", new[] { "object" }, DunderNames.Next };
        yield return new object[] { "def __eq__(self, other: Point) -> bool:\n    return True", "IEquatable", "System", new[] { "Point" }, DunderNames.Eq };
        yield return new object[] { "def __eq__(self, other: int) -> bool:\n    return True", "IEquatable", "System", new[] { "int" }, DunderNames.Eq };
        yield return new object[] { "def __eq__(self, other: int?) -> bool:\n    return True", "IEquatable", "System", new[] { "int?" }, DunderNames.Eq };
        yield return new object[] { "def __iter__(self) -> int:\n    yield 1", "IEnumerable", "System.Collections.Generic", new[] { "int" }, DunderNames.Iter };
    }

    [Theory]
    [MemberData(nameof(SingleRows))]
    public void SingleDunder_YieldsItsRow(string body, string interfaceName, string ns, string[] typeArgs, string via)
    {
        var rows = Classify(body);
        rows.Should().ContainSingle();
        rows[0].InterfaceName.Should().Be(interfaceName);
        rows[0].Namespace.Should().Be(ns);
        rows[0].TypeArgs.Should().Equal(typeArgs);
        rows[0].TriggeringDunder.Should().Be(via);
    }

    // ── shapes that yield nothing ─────────────────────────────────────────────────────────

    public static IEnumerable<object[]> EmptyShapes()
    {
        yield return new object[] { "pass" };
        yield return new object[] { "def __str__(self) -> str:\n    return \"\"" };
        yield return new object[] { "def __eq__(self, other: object) -> bool:\n    return True" };
        yield return new object[] { "def __eq__(self, other) -> bool:\n    return True" };
        yield return new object[] { "def __eq__(self) -> bool:\n    return True" };
        // __iter__ without __next__ and without a yield is a plain method, not a generator.
        yield return new object[] { "def __iter__(self) -> int:\n    return 0" };
    }

    [Theory]
    [MemberData(nameof(EmptyShapes))]
    public void NonSynthesizingShape_YieldsNothing(string body)
    {
        Classify(body).Should().BeEmpty();
    }

    // ── combinations and order ────────────────────────────────────────────────────────────

    [Fact]
    public void IterAndNext_YieldEnumeratorThenEnumerable_AtNextsElementType()
    {
        var rows = Classify("def __iter__(self):\n    return self\ndef __next__(self) -> str:\n    return \"\"");
        rows.Select(r => r.InterfaceName).Should().Equal("IEnumerator", "IEnumerable");
        rows.Should().OnlyContain(r => r.TypeArgs.SequenceEqual(new[] { "str" }));
        rows[0].TriggeringDunder.Should().Be(DunderNames.Next);
        rows[1].TriggeringDunder.Should().Be(DunderNames.Iter);
    }

    [Fact]
    public void AllSevenRows_ClassifyInBaseListOrder()
    {
        var body = string.Join("\n",
            "def __eq__(self, other: int) -> bool:",
            "    return True",
            "def __next__(self) -> str:",
            "    return \"\"",
            "def __iter__(self):",
            "    return self",
            "def __reversed__(self) -> str:",
            "    return \"\"",
            "def __bool__(self) -> bool:",
            "    return True",
            "def __len__(self) -> int:",
            "    return 0");
        var rows = Classify(body);

        // Declaration order does not matter; the roster order does (the emitted base list is
        // snapshot-pinned on it).
        rows.Select(r => r.InterfaceName).Should().Equal(
            "ISized", "IBoolConvertible", "IReverseEnumerable", "IEnumerator", "IEnumerable", "IEquatable");
        rows.Should().HaveCount(6, "the generator-__iter__ row is mutually exclusive with __next__, so six of the seven rows fit in one class");
    }

    [Fact]
    public void OnlyTheFirstDeclaration_OfARepeatedDunder_Classifies()
    {
        var rows = Classify("def __eq__(self, other: int) -> bool:\n    return True\ndef __eq__(self, other: object) -> bool:\n    return True");
        rows.Should().ContainSingle();
        rows[0].TypeArgs.Should().Equal("int");
    }

    // ── rosters ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SynthesizableSharpyCoreInterfaces_AreTheProtocolRegistrysNonGenericRows()
    {
        SynthesisAnalyzer.SynthesizableSharpyCoreInterfaces.Should().BeEquivalentTo(new[] { "ISized", "IBoolConvertible" });
        SynthesisAnalyzer.SynthesizableSharpyCoreInterfaces.Should().NotContain("IReverseEnumerable");
    }

    [Fact]
    public void ClrDefinitionFor_CoversEveryRow_AndNothingElse()
    {
        var names = new[] { "ISized", "IBoolConvertible", "IReverseEnumerable", "IEnumerator", "IEnumerable", "IEquatable" };
        names.Length.Should().Be(RowCount - 1, "IEnumerable is one interface reached by two dunder rows");
        foreach (var name in names)
        {
            var definition = SynthesisAnalyzer.ClrDefinitionFor(name);
            definition.Should().NotBeNull(name);
            definition!.IsInterface.Should().BeTrue(name);
            definition.Name.Should().StartWith(name);
        }
        SynthesisAnalyzer.ClrDefinitionFor("IComparable").Should().BeNull();
        SynthesisAnalyzer.ClrDefinitionFor("ISizedd").Should().BeNull();
    }
}
