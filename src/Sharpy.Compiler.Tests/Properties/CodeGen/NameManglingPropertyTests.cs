using System.Reflection;
using System.Text.RegularExpressions;
using CsCheck;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Tests.Properties.Generators;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.CodeGen;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
public class NameManglingPropertyTests
{
    private readonly ITestOutputHelper _output;

    public NameManglingPropertyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ToPascalCase_IsInjective_ForSnakeCaseInputs()
    {
        Gen.Select(GenIdentifier.SnakeCaseIdentifier, GenIdentifier.SnakeCaseIdentifier)
            .Where(t => t.Item1 != t.Item2)
            .Sample(t =>
            {
                var (a, b) = t;
                var resultA = NameMangler.ToPascalCase(a);
                var resultB = NameMangler.ToPascalCase(b);
                Assert.NotEqual(resultA, resultB);
            }, print: t => $"a={t.Item1}, b={t.Item2}, ToPascalCase(a)={NameMangler.ToPascalCase(t.Item1)}, ToPascalCase(b)={NameMangler.ToPascalCase(t.Item2)}", iter: 200);
    }

    [Fact]
    public void ToCamelCase_IsInjective_ForSnakeCaseInputs()
    {
        Gen.Select(GenIdentifier.SnakeCaseIdentifier, GenIdentifier.SnakeCaseIdentifier)
            .Where(t => t.Item1 != t.Item2)
            .Sample(t =>
            {
                var (a, b) = t;
                var resultA = NameMangler.ToCamelCase(a);
                var resultB = NameMangler.ToCamelCase(b);
                Assert.NotEqual(resultA, resultB);
            }, print: t => $"a={t.Item1}, b={t.Item2}, ToCamelCase(a)={NameMangler.ToCamelCase(t.Item1)}, ToCamelCase(b)={NameMangler.ToCamelCase(t.Item2)}", iter: 200);
    }

    [Fact]
    public void ToPascalCase_ProducesValidCSharpIdentifier()
    {
        var validIdentifierPattern = new Regex(@"^@?[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        GenIdentifier.MixedFormIdentifier.Sample(name =>
        {
            var result = NameMangler.ToPascalCase(name);
            Assert.Matches(validIdentifierPattern, result);
        }, print: name => $"input={name}, ToPascalCase={NameMangler.ToPascalCase(name)}", iter: 200);
    }

    [Fact]
    public void Transform_IsConsistentAcrossContexts()
    {
        var contexts = Enum.GetValues<NameContext>();

        GenIdentifier.MixedFormIdentifier.Sample(name =>
        {
            foreach (var ctx in contexts)
            {
                // Should not throw
                var result1 = NameMangler.Transform(name, ctx);
                // Same input + same context = same output (deterministic)
                var result2 = NameMangler.Transform(name, ctx);
                Assert.Equal(result1, result2);
            }
        }, print: name => $"input={name}", iter: 100);
    }

    [Fact]
    public void DunderMappings_AreUnique()
    {
        // Access the private _dunderMethodMap via reflection
        var mapField = typeof(DunderNameMapping).GetField(
            "_dunderMethodMap",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(mapField);

        var map = (Dictionary<string, string>)mapField.GetValue(null)!;
        Assert.NotEmpty(map);

        // Check that no two different dunder names map to the same C# name
        var csharpNameToDunders = new Dictionary<string, List<string>>();
        foreach (var (dunderName, csharpName) in map)
        {
            if (!csharpNameToDunders.TryGetValue(csharpName, out var dunders))
            {
                dunders = new List<string>();
                csharpNameToDunders[csharpName] = dunders;
            }
            dunders.Add(dunderName);
        }

        var collisions = csharpNameToDunders
            .Where(kv => kv.Value.Count > 1)
            .ToList();

        foreach (var collision in collisions)
        {
            _output.WriteLine(
                $"Collision: C# name '{collision.Key}' is mapped from multiple dunders: " +
                string.Join(", ", collision.Value));
        }

        // __str__ and __repr__ both map to ToString — this is intentional.
        // Filter those out and assert no other collisions.
        var unexpectedCollisions = collisions
            .Where(c => !(c.Key == "ToString" &&
                c.Value.Count == 2 &&
                c.Value.Contains("__str__") &&
                c.Value.Contains("__repr__")))
            .ToList();

        Assert.Empty(unexpectedCollisions);
    }
}