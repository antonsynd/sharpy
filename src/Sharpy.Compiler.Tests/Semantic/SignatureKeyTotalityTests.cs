using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Totality test for <see cref="TypeAnnotationKey"/> (#1721).
/// Constructs one annotation per grammar production and asserts pairwise-distinct keys.
/// The count is anchored to a literal — adding a grammar production without a cell fails.
/// </summary>
public class SignatureKeyTotalityTests
{
    [Fact]
    public void AllProductionSpellings_ProducePairwiseDistinctKeys()
    {
        var spellings = BuildAnnotationSpellings();

        Assert.Equal(ExpectedSpellingCount, spellings.Length);

        var seen = new Dictionary<string, string>();
        var collisions = new List<string>();

        foreach (var (label, annotation) in spellings)
        {
            var key = TypeAnnotationKey.Of(annotation);
            if (seen.TryGetValue(key, out var existing))
                collisions.Add($"'{key}' collides: {existing} vs {label}");
            else
                seen[key] = label;
        }

        Assert.Empty(collisions);
    }

    [Fact]
    public void NullAnnotation_ProducesUnderscore()
    {
        Assert.Equal("_", TypeAnnotationKey.Of(null));
    }

    [Fact]
    public void Int_VsIntOptional_AreDifferentKeys()
    {
        var bare = new TypeAnnotation { Name = "int" };
        var optional = new TypeAnnotation { Name = "int", IsOptional = true };

        Assert.NotEqual(TypeAnnotationKey.Of(bare), TypeAnnotationKey.Of(optional));
    }

    [Fact]
    public void Int_VsIntNullable_AreDifferentKeys()
    {
        var bare = new TypeAnnotation { Name = "int" };
        var nullable = new TypeAnnotation { Name = "int", IsCSharpNullable = true };

        Assert.NotEqual(TypeAnnotationKey.Of(bare), TypeAnnotationKey.Of(nullable));
    }

    [Fact]
    public void IntOptional_VsIntNullable_AreDifferentKeys()
    {
        var optional = new TypeAnnotation { Name = "int", IsOptional = true };
        var nullable = new TypeAnnotation { Name = "int", IsCSharpNullable = true };

        Assert.NotEqual(TypeAnnotationKey.Of(optional), TypeAnnotationKey.Of(nullable));
    }

    [Fact]
    public void BacktickEscaped_VsBare_AreDifferentKeys()
    {
        var bare = new TypeAnnotation { Name = "int" };
        var escaped = new TypeAnnotation { Name = "int", IsNameBacktickEscaped = true };

        Assert.NotEqual(TypeAnnotationKey.Of(bare), TypeAnnotationKey.Of(escaped));
    }

    [Fact]
    public void ResultType_VsBareType_AreDifferentKeys()
    {
        var bare = new TypeAnnotation { Name = "int" };
        var result = new TypeAnnotation
        {
            Name = "int",
            ErrorType = new TypeAnnotation { Name = "str" }
        };

        Assert.NotEqual(TypeAnnotationKey.Of(bare), TypeAnnotationKey.Of(result));
    }

    [Fact]
    public void NamedTuple_VsUnnamedTuple_AreDifferentKeys()
    {
        var unnamed = new TypeAnnotation
        {
            Name = "tuple",
            TypeArguments = ImmutableArray.Create(
                new TypeAnnotation { Name = "int" },
                new TypeAnnotation { Name = "str" })
        };

        var named = new TypeAnnotation
        {
            Name = "tuple",
            TypeArguments = ImmutableArray.Create(
                new TypeAnnotation { Name = "int" },
                new TypeAnnotation { Name = "str" }),
            TupleElementNames = ImmutableArray.Create<string?>("x", "y")
        };

        Assert.NotEqual(TypeAnnotationKey.Of(unnamed), TypeAnnotationKey.Of(named));
    }

    [Fact]
    public void Callable_DifferentParams_AreDifferentKeys()
    {
        var intToStr = new TypeAnnotation
        {
            Name = "function",
            TypeArguments = ImmutableArray.Create(
                new TypeAnnotation { Name = "int" },
                new TypeAnnotation { Name = "str" })
        };

        var strToStr = new TypeAnnotation
        {
            Name = "function",
            TypeArguments = ImmutableArray.Create(
                new TypeAnnotation { Name = "str" },
                new TypeAnnotation { Name = "str" })
        };

        Assert.NotEqual(TypeAnnotationKey.Of(intToStr), TypeAnnotationKey.Of(strToStr));
    }

    private const int ExpectedSpellingCount = 12;

    private static (string Label, TypeAnnotation Annotation)[] BuildAnnotationSpellings()
    {
        return new (string, TypeAnnotation)[]
        {
            ("int", new TypeAnnotation { Name = "int" }),
            ("int?", new TypeAnnotation { Name = "int", IsOptional = true }),
            ("int|None", new TypeAnnotation { Name = "int", IsCSharpNullable = true }),
            ("int!str", new TypeAnnotation
            {
                Name = "int",
                ErrorType = new TypeAnnotation { Name = "str" }
            }),
            ("`int`", new TypeAnnotation { Name = "int", IsNameBacktickEscaped = true }),
            ("list[int?]", new TypeAnnotation
            {
                Name = "list",
                TypeArguments = ImmutableArray.Create(
                    new TypeAnnotation { Name = "int", IsOptional = true })
            }),
            ("tuple[int,str]", new TypeAnnotation
            {
                Name = "tuple",
                TypeArguments = ImmutableArray.Create(
                    new TypeAnnotation { Name = "int" },
                    new TypeAnnotation { Name = "str" })
            }),
            ("tuple[x:int,y:str]", new TypeAnnotation
            {
                Name = "tuple",
                TypeArguments = ImmutableArray.Create(
                    new TypeAnnotation { Name = "int" },
                    new TypeAnnotation { Name = "str" }),
                TupleElementNames = ImmutableArray.Create<string?>("x", "y")
            }),
            ("(int)->str", new TypeAnnotation
            {
                Name = "function",
                TypeArguments = ImmutableArray.Create(
                    new TypeAnnotation { Name = "int" },
                    new TypeAnnotation { Name = "str" })
            }),
            ("(str)->str", new TypeAnnotation
            {
                Name = "function",
                TypeArguments = ImmutableArray.Create(
                    new TypeAnnotation { Name = "str" },
                    new TypeAnnotation { Name = "str" })
            }),
            ("str", new TypeAnnotation { Name = "str" }),
            ("list[int]", new TypeAnnotation
            {
                Name = "list",
                TypeArguments = ImmutableArray.Create(
                    new TypeAnnotation { Name = "int" })
            }),
        };
    }
}
