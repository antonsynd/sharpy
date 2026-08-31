using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

public class SemanticTypeDisplayUniquenessTests
{
    private static readonly BuiltinType Int64 = new() { Name = "int64" };

    [Fact]
    public void WrapperTypes_PairwiseDistinctDisplayNames()
    {
        var wrappers = new (string Label, SemanticType Type)[]
        {
            ("OptionalType", new OptionalType { UnderlyingType = Int64 }),
            ("NullableType", new NullableType { UnderlyingType = Int64 }),
            ("ResultType", new ResultType { OkType = Int64, ErrorType = Int64 }),
            ("TaskType", new TaskType { ResultType = Int64 }),
            ("GenericType(list)", new GenericType { Name = "list", TypeArguments = new List<SemanticType> { Int64 } }),
        };

        var seen = new Dictionary<string, string>();
        var collisions = new List<string>();

        foreach (var (label, type) in wrappers)
        {
            var display = type.GetDisplayName();
            if (seen.TryGetValue(display, out var existing))
            {
                collisions.Add($"'{display}' collides: {existing} vs {label}");
            }
            else
            {
                seen[display] = label;
            }
        }

        Assert.Empty(collisions);
    }

    [Fact]
    public void NullableType_DisplaysAsUnionWithNone()
    {
        var nullable = new NullableType { UnderlyingType = Int64 };
        Assert.Equal("int64 | None", nullable.GetDisplayName());
    }

    [Fact]
    public void OptionalType_DisplaysWithQuestionMark()
    {
        var optional = new OptionalType { UnderlyingType = Int64 };
        Assert.Equal("int64?", optional.GetDisplayName());
    }
}
