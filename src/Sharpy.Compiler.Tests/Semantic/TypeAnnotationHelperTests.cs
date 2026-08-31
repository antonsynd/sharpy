using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// The #1714 contract at the annotation surface: a diagnostic that quotes a user's type
/// annotation must reproduce the user's own spelling — `T?`, `T | None`, and `T !E` are three
/// different annotations and must render as three different strings. Before the fix,
/// GetName dropped IsCSharpNullable and IsResult, quoting `int` for `int | None`.
/// </summary>
public class TypeAnnotationHelperTests
{
    private static TypeAnnotation Int(bool optional = false, bool nullable = false, TypeAnnotation? error = null) =>
        new() { Name = "int", IsOptional = optional, IsCSharpNullable = nullable, ErrorType = error };

    [Fact]
    public void ModifierShapes_PairwiseDistinct()
    {
        var shapes = new (string Label, TypeAnnotation Ann)[]
        {
            ("plain", Int()),
            ("optional", Int(optional: true)),
            ("nullable", Int(nullable: true)),
            ("result", Int(error: new TypeAnnotation { Name = "ValueError" })),
        };

        var seen = new Dictionary<string, string>();
        foreach (var (label, ann) in shapes)
        {
            var name = TypeAnnotationHelper.GetName(ann);
            Assert.False(seen.ContainsKey(name),
                $"'{name}' renders both '{label}' and '{seen.GetValueOrDefault(name)}'");
            seen[name] = label;
        }
    }

    [Fact]
    public void NullableAnnotation_QuotesUnionWithNone()
    {
        Assert.Equal("int | None", TypeAnnotationHelper.GetName(Int(nullable: true)));
    }

    [Fact]
    public void OptionalAnnotation_QuotesQuestionMark()
    {
        Assert.Equal("int?", TypeAnnotationHelper.GetName(Int(optional: true)));
    }

    [Fact]
    public void ResultAnnotation_QuotesErrorType()
    {
        Assert.Equal("int !ValueError",
            TypeAnnotationHelper.GetName(Int(error: new TypeAnnotation { Name = "ValueError" })));
    }

    [Fact]
    public void GenericArguments_QuoteTheirOwnModifiers()
    {
        var listOfNullable = new TypeAnnotation
        {
            Name = "list",
            TypeArguments = System.Collections.Immutable.ImmutableArray.Create(Int(nullable: true)),
        };
        Assert.Equal("list[int | None]", TypeAnnotationHelper.GetName(listOfNullable));
    }

    [Fact]
    public void NullAnnotation_QuotesVoid()
    {
        Assert.Equal("void", TypeAnnotationHelper.GetName(null));
    }
}
