using System.Reflection;
using FluentAssertions;
using Sharpy.Compiler.Semantic;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Guards that every CLR type has exactly one <see cref="BuiltinType"/> singleton (#1356).
/// </summary>
public class OneSingletonPerClrTypeTests
{
    [Fact]
    public void OneSingletonPerClrType_NoDuplicateClrTypes()
    {
        var singletons = typeof(SemanticType)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (Field: f.Name, Value: f.GetValue(null)))
            .Where(x => x.Value is BuiltinType)
            .Select(x => (x.Field, Type: (BuiltinType)x.Value!))
            .ToList();

        singletons.Should().NotBeEmpty("reflection must find the BuiltinType singletons");

        var grouped = singletons
            .GroupBy(s => s.Type.ClrType)
            .Where(g => g.DistinctBy(x => x.Type, ReferenceEqualityComparer.Instance).Count() > 1)
            .ToList();

        grouped.Should().BeEmpty(
            "each CLR type must map to exactly one BuiltinType INSTANCE — multiple distinct "
            + "instances with the same ClrType make BuiltinType record equality depend on "
            + "which producer ran first (#1356). Alias fields (Float = Double) are the same "
            + "object and pass; a field that constructs a SECOND record fails.\n"
            + string.Join("\n", grouped.Select(g =>
                $"  {g.Key}: [{string.Join(", ", g.Select(s => $"{s.Field}='{s.Type.Name}'"))}]")));
    }
}
