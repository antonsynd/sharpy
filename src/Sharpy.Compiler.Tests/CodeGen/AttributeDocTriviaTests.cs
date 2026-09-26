using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// #2039: an attribute list the emitter adds to an existing declaration must not come between the
/// declaration and its <c>///</c> doc comment. Prepended in front of the comment, the comment
/// follows the attribute, documents nothing, and a documentation build with TreatWarningsAsErrors
/// (Sharpy.Stdlib, whose spy-sourced modules are emitted C#) fails with CS1587. Every such site goes
/// through <c>RoslynEmitter.PrependAttributeList</c>, which moves the leading trivia onto the new
/// first list. The program below puts a docstring on every declaration kind that receives one:
/// the [SharpyModule] members class, and the [SharpyModuleType]-stamped siblings (class, struct,
/// interface, int enum with [SharpyFieldName] members, string enum, union), most of them
/// snake-named so the [SharpyName] stamp is there too; the delegate takes no docstring, but its
/// stamps must not raise CS1587 either.
/// </summary>
public class AttributeDocTriviaTests
{
    private const string Source = """"
        """Module doc."""

        class my_thing:
            """Class doc."""
            x: int

        struct my_pt:
            """Struct doc."""
            x: int

        interface IShape:
            """Interface doc."""
            def area(self) -> int: ...

        enum color:
            """Int enum doc."""
            red = 1
            DarkGreen = 2

        enum mood:
            """String enum doc."""
            HAPPY = "h"

        union shape:
            """Union doc."""
            case Circle(r: int)

        delegate cb(v: int) -> None
        """";

    /// <summary>Emitted type name → its docstring (written out, not read back from the source).</summary>
    private static readonly Dictionary<string, string> DocumentedTypes = new()
    {
        ["Module"] = "Module doc.",
        ["MyThing"] = "Class doc.",
        ["MyPt"] = "Struct doc.",
        ["IShape"] = "Interface doc.",
        ["Color"] = "Int enum doc.",
        ["Mood"] = "String enum doc.",
        ["Shape"] = "Union doc.",
    };

    private static SyntaxTree EmitLibrary()
        => CSharpSyntaxTree.ParseText(
            EmitterTestPipeline.CompileToCSharp(Source, isEntryPoint: false),
            new CSharpParseOptions(documentationMode: DocumentationMode.Diagnose));

    private static CSharpCompilation Compile(SyntaxTree tree)
        => CSharpCompilation.Create("DocTrivia", new[] { tree },
            new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    [Fact]
    public void EveryStampedDeclaration_KeepsItsDocComment_NoCS1587()
    {
        var tree = EmitLibrary();

        // CS1587 is a compilation diagnostic (the doc-comment pass), not a parse one: the syntax
        // tree's own diagnostics never carry it.
        var cs1587 = Compile(tree).GetDiagnostics().Where(d => d.Id == "CS1587").ToList();
        Assert.True(cs1587.Count == 0, string.Join("\n", cs1587));

        // Positive control: the stamps this sweep is about are in the output, and the members class
        // is stamped (a library module).
        var text = tree.GetRoot().ToFullString();
        Assert.Contains("SharpyModuleType(", text);
        Assert.Contains("SharpyName(", text);
        Assert.Contains("SharpyFieldName(", text);
        Assert.Contains("SharpyModule(", text);
    }

    [Fact]
    public void EveryDocumentedDeclaration_StillCarriesItsDocumentationXml()
    {
        var tree = EmitLibrary();
        var model = Compile(tree).GetSemanticModel(tree);

        var missing = new List<string>();
        foreach (var (name, doc) in DocumentedTypes)
        {
            var declaration = tree.GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
                .Single(d => d.Identifier.Text == name);
            var xml = model.GetDeclaredSymbol(declaration)?.GetDocumentationCommentXml() ?? "";
            if (!xml.Contains(doc, StringComparison.Ordinal))
                missing.Add($"{name}: expected its doc '{doc}', got '{xml}'");
        }

        Assert.True(missing.Count == 0, string.Join("\n", missing));
    }
}
