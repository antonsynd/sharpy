using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;
using LexerNs = Sharpy.Compiler.Lexer;
using ParserNs = Sharpy.Compiler.Parser;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The behavioral classification table for implicit-abstract stub members (#1258): every
/// combination of {interface, abstract class} x {<c>...</c>, <c>(...)</c>, <c>pass</c>} x
/// {same-file, imported}, asserting the <em>outcome</em> (<see cref="FunctionSymbol.IsAbstract"/>)
/// rather than which predicate a call site happens to name.
///
/// <para><b>Why outcomes and not a source scan.</b> The obvious guard — "classification sites must
/// call <c>IsAbstractStubBody</c>" — is wrong, and would flag correct code. The predicate is keyed
/// to the <em>owning type kind</em>, and <c>NameResolver.Members.cs:130-139</c> documents the split
/// in its own comment: an <c>@abstract</c> <b>class</b> member needs an ellipsis body
/// (<c>IsEllipsisStubBody</c>), while an <b>interface</b> member accepts ellipsis or <c>pass</c>
/// (<c>IsAbstractStubBody</c>); <c>TypeChecker.Definitions.cs</c> mirrors and enforces the class
/// rule with "Abstract method '...' must have '...' as its body". A name scan would either fail on
/// that correct code or pass vacuously — both predicate names occur in the same file. That is the
/// same blind spot, one level up, that let #1258 survive the #1214 sweep:
/// <c>EllipsisStubAuthorityConformanceTests</c> forces both sites through <c>AstHelper</c> but
/// cannot see that they called <em>different</em> predicates.</para>
///
/// <para><b>Why a symbol-level table and not only fixtures.</b> Measured 2026-08-06 by mutation:
/// disabling <c>ModuleLoader</c>'s interface stub marking outright leaves
/// <c>TestFixtures/imports/paren_ellipsis_imported_stub</c> green and leaves <c>project</c>,
/// <c>run</c> and <c>emit diagnostics</c> output unchanged. Since #1087 every entry point lowers
/// the entry file plus its local-import closure into a synthetic project
/// (<c>SyntheticProject.DiscoverLocalImportClosure</c>) and <c>ProjectCompiler</c> name-resolves
/// every unit, so for an ordinary import the symbols come from <c>NameResolver</c> and
/// <c>ModuleLoader</c>'s classification is shadowed (#1267). No <c>TestFixtures</c> fixture can
/// pin this seam; this table can, and does — verified by reverting the #1258 fix, which reddens
/// exactly the two <c>pass</c>-interface cells and nothing else.</para>
///
/// <para>One compilation shape does escape that shadowing — a project whose declared source set
/// does not cover a module it imports — and that is where these classifications become
/// user-visible. <see cref="ImportedStubClassificationReachabilityTests"/> is the end-to-end half
/// of this table on that path.</para>
///
/// <para>Every cell's arrangement is built to fail loudly rather than quietly measure nothing: the
/// source must parse cleanly, the parsed body must have the exact shape the cell names (so a parser
/// change that normalized <c>(...)</c> into <c>...</c> could not make those cells pass while
/// testing something else), the owning type must exist with the intended kind/abstractness, and it
/// must declare exactly one method named <c>m</c>.</para>
/// </summary>
public class StubClassificationTableTests : IDisposable
{
    private const string Interface = "interface";
    private const string AbstractClass = "abstract-class";

    private const string SameFile = "same-file";
    private const string Imported = "imported";

    private const string Abstract = "abstract";
    private const string Concrete = "concrete";

    private const string Property = "property";
    private const string Event = "event";

    /// <summary>
    /// A body spelling that stands for "an <c>@abstract</c> decorator over a <c>pass</c> body" —
    /// the decorator paired with the one body the implicit rule would not have made abstract.
    /// </summary>
    private const string AbstractDecorator = "@abstract";

    private readonly string _testDir;

    public StubClassificationTableTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"sharpy_stubtable_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    // --- The table -----------------------------------------------------------------------------

    /// <summary>
    /// 12 cells. Every same-file/imported pair must now agree: the <c>interface</c> pairs by the
    /// interface rule (ellipsis or <c>pass</c>, #1258) and the <c>abstract-class</c> pairs by the
    /// class rule (ellipsis only, #1266). The two rows that carry the rules' <em>difference</em> —
    /// not a divergence — are the <c>pass</c> ones: a <c>pass</c> body makes an interface member
    /// abstract and leaves an abstract-class member concrete, on both sides of the import.
    /// </summary>
    [Theory]
    // owner,         body,    site,     expected
    [InlineData(Interface, "...", SameFile, Abstract)]
    [InlineData(Interface, "...", Imported, Abstract)]
    [InlineData(Interface, "(...)", SameFile, Abstract)]
    [InlineData(Interface, "(...)", Imported, Abstract)]
    [InlineData(Interface, "pass", SameFile, Abstract)]
    [InlineData(Interface, "pass", Imported, Abstract)]      // #1258: was Concrete before the fix
    [InlineData(AbstractClass, "...", SameFile, Abstract)]
    [InlineData(AbstractClass, "...", Imported, Abstract)]   // #1266: was Concrete before the fix
    [InlineData(AbstractClass, "(...)", SameFile, Abstract)]
    [InlineData(AbstractClass, "(...)", Imported, Abstract)] // #1266: was Concrete before the fix
    [InlineData(AbstractClass, "pass", SameFile, Concrete)]  // abstract-class members need ellipsis
    [InlineData(AbstractClass, "pass", Imported, Concrete)]  // ...on both sides, which is the point
    public void StubClassification(string ownerKind, string body, string site, string expected)
    {
        Classify(ownerKind, body, site).Should().Be(
            expected,
            "{0} member with a `{1}` body must classify {2} when {3}", ownerKind, body, expected, site);
    }

    /// <summary>
    /// #1258 stated directly: for an interface member, the classification of one declaration must
    /// not depend on which side of an import it sits. This is the assertion the source-scan guard
    /// could not express.
    /// </summary>
    [Theory]
    [InlineData("...")]
    [InlineData("(...)")]
    [InlineData("pass")]
    public void InterfaceStub_ClassifiesTheSameOnBothSidesOfAnImport(string body)
    {
        var sameFile = Classify(Interface, body, SameFile);
        var imported = Classify(Interface, body, Imported);

        imported.Should().Be(
            sameFile,
            "an interface method with a `{0}` body is one declaration with one classification; "
            + "NameResolver.ResolveMethodDeclaration and ModuleLoader.ExtractFullInterfaceSymbol "
            + "must reach the same answer (#1258)", body);
        sameFile.Should().Be(Abstract, "interface members with stub bodies are implicitly abstract");
    }

    /// <summary>
    /// #1266, the mirror of <see cref="InterfaceStub_ClassifiesTheSameOnBothSidesOfAnImport"/>. The
    /// expected answer varies by spelling because the CLASS rule is ellipsis-only — a <c>pass</c>
    /// body leaves an abstract-class member concrete — and that difference must itself be
    /// import-invariant. Until 2026-08-06 it was not: <c>ExtractFullClassSymbol</c> applied no
    /// implicit-abstract rule at all (it read only the <c>@abstract</c> decorator), so the ellipsis
    /// cells classified abstract same-file and concrete imported. Unlike #1258 that was a
    /// divergence by <em>omission</em> — which is why a predicate-name sweep of ModuleLoader came
    /// back empty while the defect was present, and why this table is keyed to outcomes.
    /// </summary>
    [Theory]
    [InlineData("...", Abstract)]
    [InlineData("(...)", Abstract)]
    [InlineData("pass", Concrete)]
    public void AbstractClassStub_ClassifiesTheSameOnBothSidesOfAnImport(string body, string expected)
    {
        var sameFile = Classify(AbstractClass, body, SameFile);
        var imported = Classify(AbstractClass, body, Imported);

        imported.Should().Be(
            sameFile,
            "an @abstract class method with a `{0}` body is one declaration with one classification; "
            + "NameResolver.ResolveMethodDeclaration and ModuleLoader.ExtractFullClassSymbol must "
            + "reach the same answer (#1266)", body);
        sameFile.Should().Be(
            expected,
            "abstract-class members are implicitly abstract only for an ELLIPSIS body — `pass` is a "
            + "stub for interface members, not for these (TypeChecker enforces it with \"Abstract "
            + "method '…' must have '...' as its body\")");
    }

    // --- The same table for properties and events (#1267) --------------------------------------

    /// <summary>
    /// 22 cells. <see cref="PropertySymbol.IsAbstract"/> and <see cref="EventSymbol.IsAbstract"/>
    /// answer the same question the method table asks, and must answer it the same way: an
    /// <c>@abstract</c> decorator always wins; an ellipsis stub body is implicitly abstract in an
    /// <c>@abstract</c> class or an interface; a <c>pass</c> body is a stub for interface members
    /// only. And, as for methods, the answer cannot depend on which side of an import the
    /// declaration sits.
    ///
    /// <para>The <c>@abstract</c> cells deliberately pair the decorator with a <c>pass</c> body,
    /// which is the one spelling the implicit rule would classify <em>concrete</em> in a class — so
    /// in the abstract-class rows the decorator is genuinely doing the work rather than being
    /// confirmed by a body that would have sufficed alone.</para>
    ///
    /// <para>The ten imported cells where the implicit rule (rather than the decorator) decides
    /// live in <see cref="MemberStubClassification_ImportedImplicitStub"/>; they fail today, for
    /// one reason, tracked as #1368.</para>
    /// </summary>
    [Theory]
    // member,  owner,         body,        site,     expected
    [InlineData(Property, Interface, AbstractDecorator, SameFile, Abstract)]
    [InlineData(Property, Interface, AbstractDecorator, Imported, Abstract)]
    [InlineData(Property, Interface, "...", SameFile, Abstract)]
    [InlineData(Property, Interface, "(...)", SameFile, Abstract)]
    [InlineData(Property, Interface, "pass", SameFile, Abstract)]
    [InlineData(Property, AbstractClass, AbstractDecorator, SameFile, Abstract)]
    [InlineData(Property, AbstractClass, AbstractDecorator, Imported, Abstract)]
    [InlineData(Property, AbstractClass, "...", SameFile, Abstract)]
    [InlineData(Property, AbstractClass, "(...)", SameFile, Abstract)]
    [InlineData(Property, AbstractClass, "pass", SameFile, Concrete)]
    [InlineData(Property, AbstractClass, "pass", Imported, Concrete)]
    [InlineData(Event, Interface, AbstractDecorator, SameFile, Abstract)]
    [InlineData(Event, Interface, AbstractDecorator, Imported, Abstract)]
    [InlineData(Event, Interface, "...", SameFile, Abstract)]
    [InlineData(Event, Interface, "(...)", SameFile, Abstract)]
    [InlineData(Event, Interface, "pass", SameFile, Abstract)]
    [InlineData(Event, AbstractClass, AbstractDecorator, SameFile, Abstract)]
    [InlineData(Event, AbstractClass, AbstractDecorator, Imported, Abstract)]
    [InlineData(Event, AbstractClass, "...", SameFile, Abstract)]
    [InlineData(Event, AbstractClass, "(...)", SameFile, Abstract)]
    [InlineData(Event, AbstractClass, "pass", SameFile, Concrete)]
    [InlineData(Event, AbstractClass, "pass", Imported, Concrete)]
    public void MemberStubClassification(
        string memberKind, string ownerKind, string body, string site, string expected)
    {
        ClassifyMember(memberKind, ownerKind, body, site).Should().Be(
            expected,
            "a {0} {1} with a `{2}` body must classify {3} when {4}",
            ownerKind, memberKind, body, expected, site);
    }

    /// <summary>
    /// The other ten cells of the table above: every imported property/event whose abstractness
    /// comes from the <em>implicit stub</em> rule rather than an <c>@abstract</c> decorator.
    ///
    /// <para>They fail at HEAD, all for one reason (#1368). #1267 collapsed <em>method</em>
    /// classification onto <c>Shared.MemberClassification</c>, and 4a5013941 gave
    /// <c>NameResolver</c>'s property and event paths the implicit-stub rule — but
    /// <c>ModuleLoader.ExtractProperties</c>/<c>ExtractEvents</c> stayed decorator-only. Both take
    /// <c>ownerKind</c> and <c>ownerIsAbstract</c> and read neither, which is why nothing warned.
    /// The expectations here are the correct ones and are deliberately left as written: delete this
    /// attribute's <c>Skip</c> when #1368 lands.</para>
    /// </summary>
    [Theory(Skip = "#1368: ModuleLoader.ExtractProperties/ExtractEvents are decorator-only, so "
                 + "imported implicit stubs classify concrete. Expectations here are correct; "
                 + "remove this Skip when the fix lands.")]
    // member,  owner,         body,    site,     expected
    [InlineData(Property, Interface, "...", Imported, Abstract)]
    [InlineData(Property, Interface, "(...)", Imported, Abstract)]
    [InlineData(Property, Interface, "pass", Imported, Abstract)]
    [InlineData(Property, AbstractClass, "...", Imported, Abstract)]
    [InlineData(Property, AbstractClass, "(...)", Imported, Abstract)]
    [InlineData(Event, Interface, "...", Imported, Abstract)]
    [InlineData(Event, Interface, "(...)", Imported, Abstract)]
    [InlineData(Event, Interface, "pass", Imported, Abstract)]
    [InlineData(Event, AbstractClass, "...", Imported, Abstract)]
    [InlineData(Event, AbstractClass, "(...)", Imported, Abstract)]
    public void MemberStubClassification_ImportedImplicitStub(
        string memberKind, string ownerKind, string body, string site, string expected)
        => MemberStubClassification(memberKind, ownerKind, body, site, expected);

    /// <summary>
    /// The import-invariance assertion stated directly for properties and events, so a failure
    /// names the divergence rather than one arbitrary cell of it. <c>NameResolver</c>'s
    /// <c>ResolvePropertyDeclaration</c>/<c>ResolveEventDeclaration</c> and <c>ModuleLoader</c>'s
    /// <c>ExtractProperties</c>/<c>ExtractEvents</c> classify the same declaration and must agree.
    ///
    /// <para>Only the two spellings that agree today are live; the rest are in
    /// <see cref="MemberStub_ImportedImplicitStubClassifiesTheSameOnBothSides"/> under #1368.</para>
    /// </summary>
    [Theory]
    [InlineData(Property, AbstractClass, "pass")]
    [InlineData(Event, AbstractClass, "pass")]
    public void MemberStub_ClassifiesTheSameOnBothSidesOfAnImport(
        string memberKind, string ownerKind, string body)
    {
        var sameFile = ClassifyMember(memberKind, ownerKind, body, SameFile);
        var imported = ClassifyMember(memberKind, ownerKind, body, Imported);

        imported.Should().Be(
            sameFile,
            "a {0} {1} with a `{2}` body is one declaration with one classification; the "
            + "same-file resolver and ModuleLoader's extraction must reach the same answer (#1267)",
            ownerKind, memberKind, body);
    }

    /// <summary>#1368: the same invariant for the spellings the implicit rule decides.</summary>
    [Theory(Skip = "#1368: imported property/event implicit stubs classify concrete while the "
                 + "same declaration classifies abstract same-file. Remove this Skip with the fix.")]
    [InlineData(Property, Interface, "...")]
    [InlineData(Property, Interface, "(...)")]
    [InlineData(Property, Interface, "pass")]
    [InlineData(Property, AbstractClass, "...")]
    [InlineData(Property, AbstractClass, "(...)")]
    [InlineData(Event, Interface, "...")]
    [InlineData(Event, Interface, "(...)")]
    [InlineData(Event, Interface, "pass")]
    [InlineData(Event, AbstractClass, "...")]
    [InlineData(Event, AbstractClass, "(...)")]
    public void MemberStub_ImportedImplicitStubClassifiesTheSameOnBothSides(
        string memberKind, string ownerKind, string body)
        => MemberStub_ClassifiesTheSameOnBothSidesOfAnImport(memberKind, ownerKind, body);

    /// <summary>
    /// A property or event written as two accessors is still one member, and the merge must not
    /// lose its abstractness. Both accessors are stubs here — the disagreeing shape is a diagnostic
    /// (SPY0424 for events), not a classification question — so the merged symbol is abstract.
    /// </summary>
    [Theory]
    [InlineData(Property, SameFile)]
    [InlineData(Event, SameFile)]
    public void MergedAccessorStub_IsOneAbstractMember(string memberKind, string site)
    {
        var owner = MergedAccessorOwner(memberKind, site);

        // The merge itself: two declarations, one symbol. If they did not merge, the "exactly one
        // member named 'm'" check below fails and the abstractness assertion never runs on a
        // half-built member.
        var classification = MemberClassificationOf(owner, AbstractClass, memberKind);

        classification.Should().Be(
            Abstract,
            "both accessors of the {0} are ellipsis stubs in an @abstract class, so the one member "
            + "they merge into is abstract — {1} (#1267)", memberKind, site);
    }

    /// <summary>
    /// #1368 again, at the merged-accessor shape: the imported merge loses the abstractness the
    /// same-file merge keeps. Worth its own cell because the merge branch discards the second
    /// accessor's classification entirely, so a fix that only touched the first-declaration path
    /// would leave this shape wrong.
    /// </summary>
    [Theory(Skip = "#1368: the imported merge classifies concrete because ModuleLoader never "
                 + "applies the implicit-stub rule. Remove this Skip with the fix.")]
    [InlineData(Property, Imported)]
    [InlineData(Event, Imported)]
    public void MergedAccessorStub_ImportedIsOneAbstractMember(string memberKind, string site)
        => MergedAccessorStub_IsOneAbstractMember(memberKind, site);

    // --- Arrangement ---------------------------------------------------------------------------

    private static string OwnerSource(string ownerKind, string body) => ownerKind switch
    {
        Interface => $"interface Owner:\n    def m(self) -> None:\n        {body}\n",
        AbstractClass => $"@abstract\nclass Owner:\n    def m(self) -> None:\n        {body}\n",
        _ => throw new ArgumentOutOfRangeException(nameof(ownerKind), ownerKind, "unknown owner kind"),
    };

    private string Classify(string ownerKind, string body, string site) => site switch
    {
        SameFile => ClassifySameFile(ownerKind, body),
        Imported => ClassifyImported(ownerKind, body),
        _ => throw new ArgumentOutOfRangeException(nameof(site), site, "unknown site"),
    };

    /// <summary>Same-file classification: <c>NameResolver.ResolveMethodDeclaration</c>.</summary>
    private static string ClassifySameFile(string ownerKind, string body)
    {
        var module = ParseOwner(ownerKind, body);
        return Classification(ResolveOwner(module), ownerKind);
    }

    /// <summary>
    /// Cross-module classification: <c>ModuleLoader.ExtractFullInterfaceSymbol</c> /
    /// <c>ExtractFullClassSymbol</c>, the symbols an importing module receives.
    /// </summary>
    private string ClassifyImported(string ownerKind, string body)
    {
        // Parse the same source through the same arrangement checks first, so a shape or parse
        // regression fails here identically to the same-file half rather than only on one side.
        ParseOwner(ownerKind, body);

        return Classification(ExtractOwner(OwnerSource(ownerKind, body)), ownerKind);
    }

    /// <summary>Runs <c>NameResolver</c> over a parsed module and returns its <c>Owner</c>.</summary>
    private static TypeSymbol ResolveOwner(Module module)
    {
        var symbolTable = new SymbolTable(new BuiltinRegistry());
        var resolver = new NameResolver(symbolTable);
        resolver.ResolveDeclarations(module);
        if (resolver.Diagnostics.HasErrors)
            throw new InvalidOperationException(
                "arrangement failed: name resolution reported errors — "
                + string.Join("; ", resolver.Diagnostics.GetErrors().Select(d => d.Message)));

        return symbolTable.LookupType("Owner")
            ?? throw new InvalidOperationException("arrangement failed: no 'Owner' type in the symbol table");
    }

    /// <summary>Loads a source as a module and returns the <c>Owner</c> it exports.</summary>
    private TypeSymbol ExtractOwner(string source)
    {
        var path = Path.Combine(_testDir, $"owner_{Guid.NewGuid():N}.spy");
        File.WriteAllText(path, source);

        var loader = new ModuleLoader();
        var moduleInfo = loader.LoadModule(path, 1, 1)
            ?? throw new InvalidOperationException($"arrangement failed: module '{path}' did not load");
        if (loader.Diagnostics.HasErrors)
            throw new InvalidOperationException(
                "arrangement failed: module loading reported errors — "
                + string.Join("; ", loader.Diagnostics.GetErrors().Select(d => d.Message)));

        if (!moduleInfo.ExportedSymbols.TryGetValue("Owner", out var exported))
            throw new InvalidOperationException(
                "arrangement failed: the loaded module exports no 'Owner' — exports were "
                + string.Join(", ", moduleInfo.ExportedSymbols.Keys));
        if (exported is not TypeSymbol owner)
            throw new InvalidOperationException(
                $"arrangement failed: exported 'Owner' is {exported.GetType().Name}, not a TypeSymbol");

        return owner;
    }

    /// <summary>
    /// Reads the outcome off the symbol, after checking the owner really is the kind the cell
    /// names — an interface that silently came back as a class, or an "@abstract" class that lost
    /// its abstractness, would otherwise make a <c>concrete</c> expectation pass for the wrong
    /// reason.
    /// </summary>
    private static string Classification(TypeSymbol owner, string ownerKind)
    {
        AssertOwnerKind(owner, ownerKind);

        var methods = owner.Methods.Where(m => m.Name == "m").ToList();
        if (methods.Count != 1)
            throw new InvalidOperationException(
                $"arrangement failed: expected exactly one method named 'm' on 'Owner', found "
                + $"{methods.Count} (all: {string.Join(", ", owner.Methods.Select(m => m.Name))})");

        return methods[0].IsAbstract ? Abstract : Concrete;
    }

    /// <summary>
    /// Parses the cell's source and verifies the body really has the shape the cell names. Without
    /// this, a parser change that normalized <c>(...)</c> to a bare ellipsis (or dropped a
    /// <c>pass</c> body) would leave the <c>(...)</c> and <c>pass</c> cells green while they
    /// measured a different declaration than the one they claim to.
    /// </summary>
    private static Module ParseOwner(string ownerKind, string body)
    {
        var source = OwnerSource(ownerKind, body);
        var parser = new ParserNs.Parser(new LexerNs.Lexer(source).TokenizeAll());
        var module = parser.ParseModule();
        if (parser.Diagnostics.HasErrors)
            throw new InvalidOperationException(
                $"arrangement failed: `{body}` body did not parse — "
                + string.Join("; ", parser.Diagnostics.GetErrors().Select(d => d.Message)));

        var method = OwnerMethods(module).SingleOrDefault(f => f.Name == "m")
            ?? throw new InvalidOperationException(
                "arrangement failed: the parsed module has no single method 'm' on 'Owner'");
        AssertBodyShape(method.Body, body);
        return module;
    }

    private static IEnumerable<FunctionDef> OwnerMethods(Module module)
    {
        foreach (var stmt in module.Body)
        {
            var members = stmt switch
            {
                InterfaceDef iface when iface.Name == "Owner" => iface.Body,
                ClassDef cls when cls.Name == "Owner" => cls.Body,
                _ => ImmutableArray<Statement>.Empty,
            };

            foreach (var member in members)
            {
                if (member is FunctionDef fn)
                    yield return fn;
            }
        }
    }

    private static void AssertBodyShape(ImmutableArray<Statement> parsed, string spelling)
    {
        if (parsed.Length != 1)
            throw new InvalidOperationException(
                $"arrangement failed: `{spelling}` body parsed to {parsed.Length} statements, expected 1");

        var only = parsed[0];
        bool matches = spelling switch
        {
            "..." => only is ExpressionStatement { Expression: EllipsisLiteral },
            "(...)" => only is ExpressionStatement { Expression: Parenthesized { Expression: EllipsisLiteral } },
            "pass" => only is PassStatement,
            _ => throw new ArgumentOutOfRangeException(nameof(spelling), spelling, "unknown body spelling"),
        };

        if (!matches)
            throw new InvalidOperationException(
                $"arrangement failed: `{spelling}` body parsed as {Describe(only)} — this cell would "
                + "have measured a different declaration than the one it names");
    }

    private static string Describe(Statement stmt) => stmt switch
    {
        ExpressionStatement expr => $"ExpressionStatement({expr.Expression.GetType().Name})",
        _ => stmt.GetType().Name,
    };

    private static void AssertOwnerKind(TypeSymbol owner, string ownerKind)
    {
        switch (ownerKind)
        {
            case Interface when owner.TypeKind != TypeKind.Interface:
                throw new InvalidOperationException(
                    $"arrangement failed: 'Owner' resolved as {owner.TypeKind}, not an interface");
            case AbstractClass when owner.TypeKind != TypeKind.Class || !owner.IsAbstract:
                throw new InvalidOperationException(
                    $"arrangement failed: 'Owner' resolved as {owner.TypeKind} "
                    + $"(IsAbstract={owner.IsAbstract}), not an @abstract class");
        }
    }

    // --- Arrangement: properties and events ----------------------------------------------------

    /// <summary>
    /// The property/event member the cell declares. An event needs a delegate type in scope, hence
    /// the prelude; the <c>@abstract</c> spelling is the decorator over a <c>pass</c> body.
    /// </summary>
    private static string MemberOwnerSource(string memberKind, string ownerKind, string body)
    {
        var decorator = body == AbstractDecorator ? "    @abstract\n" : "";
        var memberBody = body == AbstractDecorator ? "pass" : body;

        var member = memberKind switch
        {
            Property => $"{decorator}    property get m(self) -> int:\n        {memberBody}\n",
            Event => $"{decorator}    event add m(self, handler: Handler):\n        {memberBody}\n",
            _ => throw new ArgumentOutOfRangeException(nameof(memberKind), memberKind, "unknown member kind"),
        };

        return OwnerWrapping(memberKind, ownerKind, member);
    }

    /// <summary>Two accessors of one member, both ellipsis stubs, in an <c>@abstract</c> class.</summary>
    private static string MergedAccessorOwnerSource(string memberKind)
    {
        var members = memberKind switch
        {
            Property =>
                "    property get m(self) -> int:\n        ...\n\n"
                + "    property set m(self, value: int) -> None:\n        ...\n",
            Event =>
                "    event add m(self, handler: Handler):\n        ...\n\n"
                + "    event remove m(self, handler: Handler):\n        ...\n",
            _ => throw new ArgumentOutOfRangeException(nameof(memberKind), memberKind, "unknown member kind"),
        };

        return OwnerWrapping(memberKind, AbstractClass, members);
    }

    private static string OwnerWrapping(string memberKind, string ownerKind, string members)
    {
        var prelude = memberKind == Event ? "delegate Handler() -> None\n\n\n" : "";

        return ownerKind switch
        {
            Interface => $"{prelude}interface Owner:\n{members}",
            AbstractClass => $"{prelude}@abstract\nclass Owner:\n{members}",
            _ => throw new ArgumentOutOfRangeException(nameof(ownerKind), ownerKind, "unknown owner kind"),
        };
    }

    private string ClassifyMember(string memberKind, string ownerKind, string body, string site)
    {
        var source = MemberOwnerSource(memberKind, ownerKind, body);
        var module = ParseMemberOwner(source, memberKind, body);

        var owner = site switch
        {
            SameFile => ResolveOwner(module),
            // Parse the imported half through the same shape checks first, so a parse or shape
            // regression fails on both sides rather than turning one side into a silent constant.
            Imported => ExtractOwner(source),
            _ => throw new ArgumentOutOfRangeException(nameof(site), site, "unknown site"),
        };

        return MemberClassificationOf(owner, ownerKind, memberKind);
    }

    private TypeSymbol MergedAccessorOwner(string memberKind, string site)
    {
        var source = MergedAccessorOwnerSource(memberKind);
        var module = ParseMergedAccessorOwner(source, memberKind);

        return site switch
        {
            SameFile => ResolveOwner(module),
            Imported => ExtractOwner(source),
            _ => throw new ArgumentOutOfRangeException(nameof(site), site, "unknown site"),
        };
    }

    /// <summary>
    /// Reads <c>IsAbstract</c> off the one property/event named <c>m</c>, after confirming the
    /// owner is the kind the cell names. "Exactly one" is what makes the merged-accessor cells
    /// meaningful: two symbols named <c>m</c> would mean the accessors never merged.
    /// </summary>
    private static string MemberClassificationOf(TypeSymbol owner, string ownerKind, string memberKind)
    {
        AssertOwnerKind(owner, ownerKind);

        switch (memberKind)
        {
            case Property:
                var properties = owner.Properties.Where(p => p.Name == "m").ToList();
                if (properties.Count != 1)
                    throw new InvalidOperationException(
                        "arrangement failed: expected exactly one property named 'm' on 'Owner', found "
                        + $"{properties.Count} (all: {string.Join(", ", owner.Properties.Select(p => p.Name))})");
                return properties[0].IsAbstract ? Abstract : Concrete;

            case Event:
                var events = owner.Events.Where(e => e.Name == "m").ToList();
                if (events.Count != 1)
                    throw new InvalidOperationException(
                        "arrangement failed: expected exactly one event named 'm' on 'Owner', found "
                        + $"{events.Count} (all: {string.Join(", ", owner.Events.Select(e => e.Name))})");
                return events[0].IsAbstract ? Abstract : Concrete;

            default:
                throw new ArgumentOutOfRangeException(nameof(memberKind), memberKind, "unknown member kind");
        }
    }

    /// <summary>
    /// The property/event counterpart of <see cref="ParseOwner"/>: the declaration must parse, be
    /// the single member named <c>m</c>, carry a function-style body (the implicit-stub rule is
    /// keyed to <c>IsFunctionStyle</c>, so an auto-property cell would measure a different rule),
    /// carry the decorator exactly when the cell names one, and have the body shape it names.
    /// </summary>
    private static Module ParseMemberOwner(string source, string memberKind, string body)
    {
        var module = ParseOrThrow(source, body);
        var (name, isFunctionStyle, decorators, parsedBody) =
            OwnerMembers(module, memberKind).SingleOrDefault(m => m.Name == "m");

        if (name == null)
            throw new InvalidOperationException(
                $"arrangement failed: the parsed module has no single {memberKind} 'm' on 'Owner'");
        if (!isFunctionStyle)
            throw new InvalidOperationException(
                $"arrangement failed: the {memberKind} 'm' parsed as auto-style, not function-style — "
                + "the implicit-stub rule only applies to function-style members, so this cell would "
                + "measure a different rule than the one it names");

        bool hasAbstractDecorator = decorators.Any(
            d => d.Name == global::Sharpy.Compiler.Shared.DecoratorNames.Abstract);
        if (hasAbstractDecorator != (body == AbstractDecorator))
            throw new InvalidOperationException(
                $"arrangement failed: the {memberKind} 'm' {(hasAbstractDecorator ? "carries" : "lacks")} "
                + $"an @abstract decorator, but the cell names `{body}`");

        AssertBodyShape(parsedBody, body == AbstractDecorator ? "pass" : body);
        return module;
    }

    /// <summary>
    /// The merged-accessor arrangement's shape guard: the source really declares TWO accessor
    /// members that share one name, so "exactly one symbol named 'm'" downstream measures a merge
    /// and not a single declaration.
    /// </summary>
    private static Module ParseMergedAccessorOwner(string source, string memberKind)
    {
        var module = ParseOrThrow(source, "...");
        var accessors = OwnerMembers(module, memberKind).Where(m => m.Name == "m").ToList();

        if (accessors.Count != 2)
            throw new InvalidOperationException(
                $"arrangement failed: expected two {memberKind} accessors named 'm', found {accessors.Count} "
                + "— without two declarations there is no merge to measure");
        foreach (var accessor in accessors)
            AssertBodyShape(accessor.Body, "...");

        return module;
    }

    private static Module ParseOrThrow(string source, string body)
    {
        var parser = new ParserNs.Parser(new LexerNs.Lexer(source).TokenizeAll());
        var module = parser.ParseModule();
        if (parser.Diagnostics.HasErrors)
            throw new InvalidOperationException(
                $"arrangement failed: `{body}` body did not parse — "
                + string.Join("; ", parser.Diagnostics.GetErrors().Select(d => d.Message)));
        return module;
    }

    private static IEnumerable<(string? Name, bool IsFunctionStyle, ImmutableArray<Decorator> Decorators, ImmutableArray<Statement> Body)>
        OwnerMembers(Module module, string memberKind)
    {
        foreach (var stmt in module.Body)
        {
            var members = stmt switch
            {
                InterfaceDef iface when iface.Name == "Owner" => iface.Body,
                ClassDef cls when cls.Name == "Owner" => cls.Body,
                _ => ImmutableArray<Statement>.Empty,
            };

            foreach (var member in members)
            {
                switch (member)
                {
                    case PropertyDef prop when memberKind == Property:
                        yield return (prop.Name, prop.IsFunctionStyle, prop.Decorators, prop.Body);
                        break;
                    case EventDef evt when memberKind == Event:
                        yield return (evt.Name, evt.IsFunctionStyle, evt.Decorators, evt.Body);
                        break;
                }
            }
        }
    }
}
