using System.Linq;
using Xunit;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Semantic.Validation;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

/// <summary>
/// A bracket attribute naming a type that is in scope nowhere used to be resolved by nothing:
/// the mangled name reached Roslyn verbatim and came back as CS0246 wrapped in SPY0908, an
/// "internal compiler error" for an ordinary typo (#1427). These tests pin the refusal AND —
/// the direction that matters more — every spelling that must keep resolving.
/// </summary>
public class BracketAttributeResolutionTests
{
    private static (Module module, SemanticContext context) Parse(string code)
    {
        var lexer = new Sharpy.Compiler.Lexer.Lexer(code);
        var tokens = lexer.TokenizeAll();
        var parser = new Sharpy.Compiler.Parser.Parser(tokens);
        var module = parser.ParseModule();

        var builtinRegistry = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtinRegistry);
        var semanticInfo = new SemanticInfo();
        var typeResolver = new TypeResolver(symbolTable, semanticInfo);

        var nameResolver = new NameResolver(symbolTable);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();

        var context = new SemanticContext(symbolTable, semanticInfo, typeResolver);
        return (module, context);
    }

    private static SemanticContext Validate(string code)
        => Validate(code, ReferenceClosure.Empty);

    private static SemanticContext Validate(string code, ReferenceClosure references)
    {
        var (module, context) = Parse(code);
        context.ReferenceClosure = references;
        new DecoratorValidator().Validate(module, context);
        return context;
    }

    private static void AssertNoUnknownBracketAttribute(SemanticContext context)
    {
        Assert.DoesNotContain(context.Diagnostics.GetErrors(),
            e => e.Code == DiagnosticCodes.Validation.UnknownBracketAttribute);
    }

    private static CompilerDiagnostic SingleUnknownBracketAttribute(SemanticContext context)
    {
        return Assert.Single(context.Diagnostics.GetErrors(),
            e => e.Code == DiagnosticCodes.Validation.UnknownBracketAttribute);
    }

    #region Refusals

    [Fact]
    public void UnknownName_OnClass_IsRefused()
    {
        var context = Validate(@"
@[no_such_attr]
class Widget:
    pass
");

        var error = SingleUnknownBracketAttribute(context);
        Assert.Contains("@[no_such_attr]", error.Message);
        // The message names the attempted resolution — both spellings C# itself would try.
        Assert.Contains("'NoSuchAttr' or 'NoSuchAttrAttribute'", error.Message);
    }

    [Fact]
    public void UnknownName_OnMethod_IsRefused()
    {
        var context = Validate(@"
class Service:
    @[no_such_attr]
    def process(self) -> str:
        return ""done""
");

        SingleUnknownBracketAttribute(context);
    }

    [Fact]
    public void UnknownName_OnField_IsRefused()
    {
        var context = Validate(@"
class Widget:
    @[no_such_attr]
    value: int
");

        SingleUnknownBracketAttribute(context);
    }

    [Fact]
    public void UnknownQualifiedName_IsRefused_AndNamesTheQualifiedSpelling()
    {
        var context = Validate(@"
@[system.no_such_attr]
class Widget:
    pass
");

        var error = SingleUnknownBracketAttribute(context);
        Assert.Contains("'System.NoSuchAttr' or 'System.NoSuchAttrAttribute'", error.Message);
    }

    [Fact]
    public void AbstractName_SteersToTheDecorator()
    {
        // #1373: bracket syntax deliberately does NOT mean the @abstract decorator, and the two
        // are one keystroke apart — the refusal has to say which one the user probably wanted.
        var context = Validate(@"
class Widget:
    @[abstract]
    def render(self) -> str:
        return ""widget""
");

        var error = SingleUnknownBracketAttribute(context);
        Assert.Contains("Did you mean the '@abstract' decorator?", error.Message);
        Assert.Contains("never a Sharpy modifier", error.Message);
    }

    [Fact]
    public void OtherModifierSpellings_AlsoSteerToTheirDecorator()
    {
        foreach (var modifier in new[] { "final", "override", "static", "virtual" })
        {
            var context = Validate($@"
@[{modifier}]
class Widget:
    pass
");

            var error = SingleUnknownBracketAttribute(context);
            Assert.Contains($"Did you mean the '@{modifier}' decorator?", error.Message);
        }
    }

    #endregion

    #region Spellings that must keep resolving

    [Fact]
    public void ClrAttribute_ResolvedThroughTheAttributeSuffix()
    {
        // 'Obsolete' is not a type; 'ObsoleteAttribute' is. The suffix is C#'s own attribute-name
        // rule, so the resolver has to try both before it may refuse anything.
        AssertNoUnknownBracketAttribute(Validate(@"
@[obsolete(""old api"")]
def legacy() -> int:
    return 1
"));
    }

    [Fact]
    public void ClrAttribute_WithoutSuffix_Resolves()
    {
        AssertNoUnknownBracketAttribute(Validate(@"
@[serializable]
class Config:
    pass
"));
    }

    [Fact]
    public void QualifiedClrAttribute_Resolves()
    {
        AssertNoUnknownBracketAttribute(Validate(@"
class Widget:
    @[system.component_model.default_value(42)]
    value: int
"));
    }

    [Fact]
    public void BacktickEscapedName_Resolves_Verbatim()
    {
        AssertNoUnknownBracketAttribute(Validate(@"
@[`SerializableAttribute`]
class RawName:
    pass
"));
    }

    [Fact]
    public void AttributeClassDeclaredInTheProgram_Resolves()
    {
        // The bracket spelling mangles to the declared class name, so the symbol table answers
        // for Sharpy-authored attributes without any reflection.
        AssertNoUnknownBracketAttribute(Validate(@"
class TraceAttribute:
    pass

class Service:
    @[trace_attribute]
    def process(self) -> str:
        return ""done""
"));
    }

    [Fact]
    public void SourceGeneratorBracketAttribute_IsNotRefused()
    {
        // A generator trigger names a user generator class, not a CLR attribute; the exemption
        // that hands it to SourceGeneratorValidator (Order 65) must survive this check.
        var (module, context) = Parse(@"
class GenA:
    def generate(self, context: GeneratorContext) -> GeneratorOutput:
        return GeneratorOutput()

@[GenA(1 + 2)]
class Target:
    pass
");
        var symbol = context.SymbolTable.LookupType("GenA");
        Assert.NotNull(symbol);
        symbol!.IsSourceGenerator = true;

        new DecoratorValidator().Validate(module, context);

        AssertNoUnknownBracketAttribute(context);
        Assert.DoesNotContain(context.Diagnostics.GetErrors(),
            e => e.Code == DiagnosticCodes.Validation.NonConstantDecoratorArgument);
    }

    [Fact]
    public void ImportedNamespace_BringsAnUnqualifiedAttributeIntoScope()
    {
        // The paired control for the import path: the ONLY difference between these two programs
        // is the import, and it is what decides whether 'DllImportAttribute' is in scope — the
        // same rule the generated C# lives by.
        const string withoutImport = @"
@[dll_import(""user32.dll"")]
def message_box() -> int:
    return 0
";
        const string withImport = @"
import system.runtime.interop_services

@[dll_import(""user32.dll"")]
def message_box() -> int:
    return 0
";

        SingleUnknownBracketAttribute(Validate(withoutImport));
        AssertNoUnknownBracketAttribute(Validate(withImport));
    }

    [Fact]
    public void DecoratedImport_StillCounts()
    {
        // A decorated import is wrapped in a DecoratedStatement, and a scan that type-tests the
        // import kinds without unwrapping stops seeing it — the namespace would drop out of scope
        // and a working attribute would be refused (the #1124/#1125 wrapper class).
        AssertNoUnknownBracketAttribute(Validate(@"
@suppress(""SPY0452"")
import system.runtime.interop_services

@[dll_import(""user32.dll"")]
def message_box() -> int:
    return 0
"));
    }

    #endregion

    #region Resolver candidate spellings

    [Fact]
    public void CandidateTypeNames_TryBothSpellings_InEveryNamespaceInScope()
    {
        var candidates = ClrAttributeResolver.CandidateTypeNames("Obsolete", new[] { "My.Ns" });

        Assert.Equal("Obsolete", candidates[0]);
        Assert.Equal("ObsoleteAttribute", candidates[1]);
        Assert.Contains("System.Obsolete", candidates);
        Assert.Contains("System.ObsoleteAttribute", candidates);
        Assert.Contains("My.Ns.Obsolete", candidates);
        Assert.Contains("My.Ns.ObsoleteAttribute", candidates);
    }

    [Fact]
    public void ResolvesToClrType_IsFalseForANameNothingDeclares()
    {
        // Instance-scoped since #1493: absence verdicts must not outlive the compilation that
        // reached them. One resolver here IS one compilation.
        var resolver = new ClrAttributeResolver();
        Assert.False(resolver.ResolvesToClrType("NoSuchAttrXyz"));
        Assert.True(resolver.ResolvesToClrType("Obsolete"));
    }

    #endregion

    #region Reference closure (#1492)

    // The absence proof used to consult loaded assemblies and the shared framework only. A
    // project's references are consumed in Phase 7, long after Phase 5 has refused — so an
    // attribute living ONLY in a .spyproj <Reference> was declared absent by a proof that had
    // never looked where it lives.
    //
    // These two cells are a PAIR and must be read together. The first is worthless without the
    // second: any blanket "this project has references, so stop refusing" satisfies it while
    // reopening the #1146 leak (CS0246 behind SPY0908) for every project with any reference.

    /// <summary>
    /// Builds a real assembly declaring one attribute, and returns its path — the shape of a
    /// <c>.spyproj</c> &lt;Reference&gt; pointing at something the compiler has not loaded.
    /// </summary>
    private static string EmitReferenceAssembly(string namespaceName, string attributeName, string dir)
    {
        var source = $@"
namespace {namespaceName}
{{
    public sealed class {attributeName} : System.Attribute {{ }}
}}";
        // File.Exists, not just a non-empty Location: this suite runs alongside tests that build
        // and delete assemblies in temp directories, and a stale Location makes CreateFromFile
        // throw FileNotFound. Measured — it flaked exactly that way.
        var references = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location) && File.Exists(a.Location))
            .Select(a => Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(a.Location))
            .ToList<Microsoft.CodeAnalysis.MetadataReference>();

        var assemblyName = $"SharpyRef{Guid.NewGuid():N}";
        var compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
            assemblyName,
            new[] { Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(source) },
            references,
            new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(
                Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));

        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, assemblyName + ".dll");
        using var stream = File.Create(path);
        var result = compilation.Emit(stream);
        Assert.True(result.Success,
            "the reference assembly must build, or the positive cell measures nothing: "
            + string.Join("; ", result.Diagnostics.Where(
                d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)));
        return path;
    }

    /// <summary>
    /// (a) THE POSITIVE. An attribute whose type lives ONLY in a referenced assembly resolves.
    /// The assembly is emitted to disk and never loaded into this process, so the pre-#1492 proof
    /// could not have seen it — which is what makes the cell non-vacuous.
    /// </summary>
    [Fact]
    public void AnAttributeLivingOnlyInAProjectReference_IsNotRefused()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sharpy_ref_{Guid.NewGuid():N}");
        try
        {
            var ns = "Sharpy.RefProbe";
            var path = EmitReferenceAssembly(ns, "WidgetAttribute", dir);

            // Control: nothing has loaded it, so without the closure it IS refused. This is the
            // measurement that proves the cell below is about the reference probe and not about
            // the attribute having been resident all along.
            var withoutReferences = Validate($@"
@[{ns}.Widget]
class Thing:
    pass
");
            Assert.Single(withoutReferences.Diagnostics.GetErrors(),
                e => e.Code == DiagnosticCodes.Validation.UnknownBracketAttribute);

            var withReferences = Validate($@"
@[{ns}.Widget]
class Thing:
    pass
", new ReferenceClosure(new[] { path }, HasUnprobedReferences: false));

            AssertNoUnknownBracketAttribute(withReferences);
        }
        finally
        {
            try
            { Directory.Delete(dir, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// (b) THE DISCRIMINATING NEGATIVE. A genuinely absent name, in a project that HAS a
    /// reference — and a probed one, so the absence really was proved. Still refused.
    ///
    /// <para>This is the cell a blanket pass-through fails. Without it, "the project has
    /// references" would be enough to stop refusing anything, and #1427's whole point — a typo
    /// gets a clean diagnostic instead of CS0246 behind SPY0908 — would be undone for every
    /// project that references anything.</para>
    /// </summary>
    [Fact]
    public void AGenuinelyAbsentName_InAProjectWithProbedReferences_IsStillRefused()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sharpy_ref_{Guid.NewGuid():N}");
        try
        {
            var path = EmitReferenceAssembly("Sharpy.RefProbe", "WidgetAttribute", dir);

            var context = Validate(@"
@[definitely_no_such_attr_xyz]
class Thing:
    pass
", new ReferenceClosure(new[] { path }, HasUnprobedReferences: false));

            var error = SingleUnknownBracketAttribute(context);
            Assert.Contains("names no attribute type that is in scope", error.Message);
        }
        finally
        {
            try
            { Directory.Delete(dir, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// The scoped fallback, and its boundary. A project carrying an UNPROBED reference (an
    /// unresolved PackageReference) cannot prove absence, so it does not refuse — while the
    /// negative cell above shows a project whose references were all probed still does.
    /// </summary>
    [Fact]
    public void AnUnprobedReference_SuspendsTheRefusal_ForThatProjectOnly()
    {
        var unprobed = Validate(@"
@[definitely_no_such_attr_xyz]
class Thing:
    pass
", new ReferenceClosure(Array.Empty<string>(), HasUnprobedReferences: true));

        AssertNoUnknownBracketAttribute(unprobed);

        // Same source, nothing unprobed: refused. The downgrade is a property of the PROJECT's
        // reference shape, not a general softening of the check.
        var proved = Validate(@"
@[definitely_no_such_attr_xyz]
class Thing:
    pass
");
        SingleUnknownBracketAttribute(proved);
    }

    #endregion
}
