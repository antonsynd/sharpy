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
    {
        var (module, context) = Parse(code);
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
        Assert.False(ClrAttributeResolver.ResolvesToClrType("NoSuchAttrXyz"));
        Assert.True(ClrAttributeResolver.ResolvesToClrType("Obsolete"));
    }

    #endregion
}
