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

        var symbolTable = new SymbolTable(new BuiltinRegistry());
        var resolver = new NameResolver(symbolTable);
        resolver.ResolveDeclarations(module);
        if (resolver.Diagnostics.HasErrors)
            throw new InvalidOperationException(
                "arrangement failed: name resolution reported errors — "
                + string.Join("; ", resolver.Diagnostics.GetErrors().Select(d => d.Message)));

        var owner = symbolTable.LookupType("Owner")
            ?? throw new InvalidOperationException("arrangement failed: no 'Owner' type in the symbol table");

        return Classification(owner, ownerKind);
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

        var path = Path.Combine(_testDir, $"owner_{Guid.NewGuid():N}.spy");
        File.WriteAllText(path, OwnerSource(ownerKind, body));

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

        return Classification(owner, ownerKind);
    }

    /// <summary>
    /// Reads the outcome off the symbol, after checking the owner really is the kind the cell
    /// names — an interface that silently came back as a class, or an "@abstract" class that lost
    /// its abstractness, would otherwise make a <c>concrete</c> expectation pass for the wrong
    /// reason.
    /// </summary>
    private static string Classification(TypeSymbol owner, string ownerKind)
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
}
