using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.TestInfrastructure.Integration;
using FluentAssertions;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Emission-level pins for function-style event stubs (#1239).
///
/// <para>
/// Both shapes below used to emit C# that could not compile — CS8712 for an abstract event given
/// accessor syntax, CS0102 for an interface event whose add and remove halves emitted as two
/// members. That is why no runnable event-stub fixture had ever existed, and why the
/// <c>EventValidator</c> stub conversion from #1214 could only be asserted at validator level.
/// </para>
///
/// <para>
/// The <c>events/event_{abstract,interface}_stub*</c> fixtures cover <b>running</b> these shapes.
/// These tests cover the two things running cannot see: the C# shape actually emitted, and #1214's
/// transparency contract at this seam — the <c>(...)</c> spelling emitting <b>byte-identical</b> C#
/// to <c>...</c>, not merely being accepted alongside it.
/// </para>
/// </summary>
public class EventStubEmissionTests
{
    private const string AbstractStubFixture = "event_abstract_stub_0001.spy";
    private const string AbstractStubParenFixture = "event_abstract_stub_paren_ellipsis_0001.spy";
    private const string InterfaceStubFixture = "event_interface_stub_pair_0001.spy";
    private const string InterfaceStubParenFixture = "event_interface_stub_pair_paren_ellipsis_0001.spy";

    private const string AbstractStubInlineFixture = "event_abstract_stub_inline_0001.spy";
    private const string AbstractStubInlineParenFixture = "event_abstract_stub_inline_paren_ellipsis_0001.spy";
    private const string InterfaceStubInlineFixture = "event_interface_stub_pair_inline_0001.spy";
    private const string InterfaceStubInlineParenFixture = "event_interface_stub_pair_inline_paren_ellipsis_0001.spy";

    [Fact]
    public void AbstractEventStub_EmitsBareAbstractEventDeclaration_NotAccessorSyntax()
    {
        var unit = EmitFixture(AbstractStubFixture);

        var baseClass = unit.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .SingleOrDefault(c => c.Identifier.Text == "Base");
        baseClass.Should().NotBeNull(
            $"{AbstractStubFixture} declares an abstract class 'Base'; without it every assertion below is vacuous");

        // The bare form: `public abstract event SimpleHandler OnAction;`
        var eventFields = baseClass!.Members.OfType<EventFieldDeclarationSyntax>()
            .Where(e => e.Declaration.Variables.Any(v => v.Identifier.Text == "OnAction"))
            .ToList();
        eventFields.Should().ContainSingle(
            "an abstract event lowers to one bare event field declaration");
        eventFields[0].Modifiers.Should().Contain(m => m.IsKind(SyntaxKind.AbstractKeyword),
            "without the abstract modifier the declaration is a concrete auto-event with a backing field");

        // The accessor form is what C# forbids here (CS8712)
        baseClass.Members.OfType<EventDeclarationSyntax>()
            .Should().BeEmpty("C# rejects accessor syntax on an abstract event (CS8712)");
    }

    [Fact]
    public void InterfaceEventStubPair_EmitsOneEventDeclaration_NotOnePerAccessor()
    {
        var unit = EmitFixture(InterfaceStubFixture);

        var notifier = unit.DescendantNodes().OfType<InterfaceDeclarationSyntax>()
            .SingleOrDefault(i => i.Identifier.Text == "INotifier");
        notifier.Should().NotBeNull(
            $"{InterfaceStubFixture} declares an interface 'INotifier'; without it every assertion below is vacuous");

        // The fixture declares `event add on_action` and `event remove on_action` — two AST nodes,
        // one C# member. Two members is CS0102.
        var onActionMembers = notifier!.Members
            .Where(m => m is EventFieldDeclarationSyntax f
                    && f.Declaration.Variables.Any(v => v.Identifier.Text == "OnAction")
                || m is EventDeclarationSyntax d && d.Identifier.Text == "OnAction")
            .ToList();
        onActionMembers.Should().ContainSingle(
            "the add and remove halves share one name and merge into a single interface event");
        onActionMembers[0].Should().BeOfType<EventFieldDeclarationSyntax>(
            "an interface event carries no accessors");
    }

    /// <summary>
    /// An undecorated ellipsis-bodied accessor pair in an abstract class is abstract by the
    /// implicit-stub rule, and codegen now takes that answer from the merged <c>EventSymbol</c>
    /// rather than re-deriving it (#1267). No fixture carries this shape — every event stub fixture
    /// spells <c>@abstract</c> — so this is the only place the symbol's implicit arm is pinned at
    /// emission. The concrete twin differs only in the owner's <c>@abstract</c>, which is what makes
    /// the abstract assertion mean something: an emitter that answered "abstract" unconditionally,
    /// or never, fails one half or the other.
    /// </summary>
    [Fact]
    public void ImplicitlyAbstractEventPair_EmitsBareAbstractEvent_OnlyWhenTheOwnerIsAbstract()
    {
        const string abstractOwner = """
            delegate SimpleHandler() -> None


            @abstract
            class Base:
                event add on_action(self, handler: SimpleHandler):
                    ...

                event remove on_action(self, handler: SimpleHandler):
                    ...
            """;

        const string concreteOwner = """
            delegate SimpleHandler() -> None


            class Base:
                event add on_action(self, handler: SimpleHandler):
                    ...

                event remove on_action(self, handler: SimpleHandler):
                    ...
            """;

        // The two sources must differ only in the owner's abstractness, or the contrast below
        // measures something other than the implicit-stub rule.
        abstractOwner.Should().Contain("@abstract", "the abstract twin carries the class-level decorator");
        concreteOwner.Should().NotContain("@abstract", "the concrete twin carries no decorator at all");
        Tokens(abstractOwner.Replace("@abstract", "")).Should().Be(Tokens(concreteOwner),
            "the twins differ in exactly one token — the class-level @abstract");

        var abstractBase = ClassNamed(EmitUnit(abstractOwner), "Base");
        var abstractEvents = abstractBase.Members.OfType<EventFieldDeclarationSyntax>()
            .Where(e => e.Declaration.Variables.Any(v => v.Identifier.Text == "OnAction"))
            .ToList();
        abstractEvents.Should().ContainSingle(
            "an implicitly abstract pair lowers to one bare event field declaration, as the decorated pair does");
        abstractEvents[0].Modifiers.Should().Contain(m => m.IsKind(SyntaxKind.AbstractKeyword),
            "the merged EventSymbol classifies an ellipsis-bodied accessor in an abstract class as abstract");
        abstractBase.Members.OfType<EventDeclarationSyntax>()
            .Should().BeEmpty("C# rejects accessor syntax on an abstract event (CS8712)");

        var concreteBase = ClassNamed(EmitUnit(concreteOwner), "Base");
        concreteBase.Members.OfType<EventFieldDeclarationSyntax>()
            .Should().BeEmpty("the same pair in a concrete class is a real implementation, not a declaration");
        concreteBase.Members.OfType<EventDeclarationSyntax>()
            .Should().ContainSingle(e => e.Identifier.Text == "OnAction",
                "a concrete function-style event keeps its add/remove accessor list");
    }

    [Fact]
    public void AbstractEventStub_BothSpellings_EmitIdenticalCSharp()
        => AssertSpellingTwinsEmitIdenticalCSharp(AbstractStubFixture, AbstractStubParenFixture);

    [Fact]
    public void InterfaceEventStubPair_BothSpellings_EmitIdenticalCSharp()
        => AssertSpellingTwinsEmitIdenticalCSharp(InterfaceStubFixture, InterfaceStubParenFixture);

    /// <summary>
    /// All four ways of writing the same event stub — block or inline position, <c>...</c> or
    /// <c>(...)</c> — must emit the same C#. The inline position only began parsing at all in #1238,
    /// so this is where that parser change meets #1239's lowering; neither batch's own fixtures cover
    /// the combination, and the inline interface pair is the shape that was still emitting CS0102
    /// after the parser fix landed and before the grouping fix did.
    /// </summary>
    [Theory]
    [InlineData(AbstractStubFixture, AbstractStubParenFixture, AbstractStubInlineFixture, AbstractStubInlineParenFixture)]
    [InlineData(InterfaceStubFixture, InterfaceStubParenFixture, InterfaceStubInlineFixture, InterfaceStubInlineParenFixture)]
    public void EventStub_BlockAndInlinePositions_AllSpellingsEmitIdenticalCSharp(
        string blockPlain, string blockParen, string inlinePlain, string inlineParen)
    {
        var sources = new[] { blockPlain, blockParen, inlinePlain, inlineParen }
            .Select(f => (Fixture: f, Source: ReadFixture(f)))
            .ToList();

        // Guard the arrangement: four fixtures that were secretly the same text, or that all used one
        // spelling, would compare equal without testing anything.
        sources.Select(s => s.Source).Distinct().Should().HaveCount(4,
            "the four fixtures must differ in source for their agreeing on one emission to mean anything");
        sources.Where(s => s.Source.Contains("(...)")).Should().HaveCount(2,
            "exactly the two parenthesized twins carry the (...) spelling");
        sources.Where(s => s.Source.Contains("): ...")).Should().HaveCount(1,
            "exactly the inline plain-ellipsis twin writes a stub body on the declaration line");

        var emitted = sources.Select(s => (s.Fixture, Code: Emit(s.Source))).ToList();
        emitted[0].Code.Should().Contain("OnAction",
            "an emission that dropped the event entirely would compare equal for the wrong reason");

        foreach (var (fixture, code) in emitted.Skip(1))
        {
            code.Should().Be(emitted[0].Code,
                $"{fixture} writes the same stub as {blockPlain}, only spelled differently");
        }
    }

    /// <summary>
    /// #1214's contract at the event seam: <c>(...)</c> is the same stub as <c>...</c>, so the two
    /// spellings must produce the same C#, character for character.
    /// </summary>
    private static void AssertSpellingTwinsEmitIdenticalCSharp(string plainFixture, string parenFixture)
    {
        var plainSource = ReadFixture(plainFixture);
        var parenSource = ReadFixture(parenFixture);

        // Guard the arrangement: comparing a file with itself, or two files that happen to use the
        // same spelling, would pass without testing anything.
        plainSource.Should().NotContain("(...)",
            $"{plainFixture} is the plain-ellipsis twin; a (...) in it makes the comparison vacuous");
        parenSource.Should().Contain("(...)",
            $"{parenFixture} is the parenthesized twin; without a (...) the comparison is vacuous");
        parenSource.Should().NotBe(plainSource, "the twins must differ in source to differ meaningfully");

        var plainCode = Emit(plainSource);
        var parenCode = Emit(parenSource);

        plainCode.Should().Contain("OnAction",
            "an emission that dropped the event entirely would compare equal for the wrong reason");
        parenCode.Should().Be(plainCode);
    }

    /// <summary>Whitespace-insensitive view of a source, so the twin guard survives line-ending and
    /// blank-line differences that carry no meaning.</summary>
    private static string Tokens(string source)
        => string.Join(" ", source.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static ClassDeclarationSyntax ClassNamed(CompilationUnitSyntax unit, string name)
    {
        var declaration = unit.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .SingleOrDefault(c => c.Identifier.Text == name);
        declaration.Should().NotBeNull(
            $"the source declares class '{name}'; without it every assertion about it is vacuous");
        return declaration!;
    }

    private static string ReadFixture(string fileName)
    {
        var path = Path.Combine(FixtureRoots.CompilerTests.Path, "events", fileName);
        File.Exists(path).Should().BeTrue($"fixture '{path}' must exist");
        return File.ReadAllText(path);
    }

    private static CompilationUnitSyntax EmitFixture(string fileName) => EmitUnit(ReadFixture(fileName));

    private static string Emit(string source) => EmitUnit(source).NormalizeWhitespace().ToFullString();

    private static CompilationUnitSyntax EmitUnit(string source)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var parser = new global::Sharpy.Compiler.Parser.Parser(lexer.TokenizeAll(), NullLogger.Instance);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();

        var nameResolver = new NameResolver(symbolTable, NullLogger.Instance);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, NullLogger.Instance);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, NullLogger.Instance);
        typeChecker.CheckModule(module);

        var emitter = new RoslynEmitter(new CodeGenContext(symbolTable, builtinRegistry));
        return emitter.GenerateCompilationUnit(module);
    }
}
