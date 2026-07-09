using System.Reflection;
using System.Text.RegularExpressions;
using CsCheck;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Tests.Properties.Generators;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Properties.CodeGen;

/// <summary>
/// Property tests for the bidirectional <see cref="NameMangler"/> authority (#1040).
///
/// The Sharpy→C#→Sharpy round-trip is identity only on the <em>well-formed</em> snake_case
/// domain (see <see cref="GenIdentifier.WellFormedSnakeCaseIdentifier"/>): multi-word names
/// whose every segment is ≥2 lowercase letters. Two lossy classes are expected and are only
/// asserted to be <em>stable</em> (idempotent after one round), never identity:
/// <list type="bullet">
///   <item>single-letter segment fusion — <c>x_y_z → XYZ → xyz</c> (the split boundary is lost);</item>
///   <item>acronym collapse — <c>HTTP → http</c>, and capital+digit-only segments such as
///     <c>a1_b2 → A1B2 → a1b2</c> (no re-split boundary survives).</item>
/// </list>
/// </summary>
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

    // --- Bidirectional round-trip properties (#1040) ---

    /// <summary>
    /// (a) Identity round-trip on the well-formed snake_case domain: mangling to
    /// PascalCase/camelCase and demangling back reproduces the original exactly.
    /// </summary>
    [Fact]
    public void RoundTrip_WellFormedSnakeCase_IsIdentity()
    {
        GenIdentifier.WellFormedSnakeCaseIdentifier.Sample(name =>
        {
            Assert.Equal(name, NameMangler.ToSnakeCase(NameMangler.ToPascalCase(name)));
            Assert.Equal(name, NameMangler.ToSnakeCase(NameMangler.ToCamelCase(name)));
        },
        print: name => $"input={name}, pascal={NameMangler.ToPascalCase(name)}, camel={NameMangler.ToCamelCase(name)}",
        iter: 500);
    }

    /// <summary>
    /// (b) One-round idempotence on arbitrary (possibly lossy) inputs: the demangle∘mangle
    /// round-trip reaches a fixed point after a single application, so the transform is
    /// stable even where it is not identity.
    /// </summary>
    [Fact]
    public void RoundTrip_MixedForm_IsStableAfterOneRound()
    {
        GenIdentifier.MixedFormIdentifier.Sample(name =>
        {
            var once = NameMangler.ToSnakeCase(NameMangler.ToPascalCase(name));
            var twice = NameMangler.ToSnakeCase(NameMangler.ToPascalCase(once));
            Assert.Equal(once, twice);
        },
        print: name =>
        {
            var once = NameMangler.ToSnakeCase(NameMangler.ToPascalCase(name));
            var twice = NameMangler.ToSnakeCase(NameMangler.ToPascalCase(once));
            return $"input={name}, once={once}, twice={twice}";
        },
        iter: 500);
    }

    /// <summary>
    /// (c) Every reverse transform emits an identifier of valid shape (no spaces, no special
    /// characters, no leading digit).
    /// </summary>
    [Fact]
    public void ReverseTransforms_ProduceValidIdentifiers()
    {
        var validIdentifierPattern = new Regex(@"^@?[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        GenIdentifier.MixedFormIdentifier.Sample(name =>
        {
            Assert.Matches(validIdentifierPattern, NameMangler.ToSnakeCase(name));
            Assert.Matches(validIdentifierPattern, NameMangler.ToScreamingSnakeCase(name));
            foreach (var ctx in Enum.GetValues<ReverseNameContext>())
                Assert.Matches(validIdentifierPattern, NameMangler.ToSharpyName(name, ctx));
        },
        print: name => $"input={name}, snake={NameMangler.ToSnakeCase(name)}, screaming={NameMangler.ToScreamingSnakeCase(name)}",
        iter: 300);
    }

    /// <summary>
    /// (c') Every <see cref="AcronymPolicy"/> of <see cref="NameMangler.ToModuleIdentifier"/>
    /// emits an identifier of valid shape.
    /// </summary>
    [Fact]
    public void ToModuleIdentifier_ProducesValidIdentifiers()
    {
        var validIdentifierPattern = new Regex(@"^@?[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        GenIdentifier.SnakeCaseIdentifier.Sample(name =>
        {
            foreach (var policy in Enum.GetValues<AcronymPolicy>())
                Assert.Matches(validIdentifierPattern, NameMangler.ToModuleIdentifier(name, policy));
        },
        print: name => $"input={name}, firstChar={NameMangler.ToModuleIdentifier(name, AcronymPolicy.FirstCharOnly)}, io={NameMangler.ToModuleIdentifier(name, AcronymPolicy.IoOnly)}, ns={NameMangler.ToModuleIdentifier(name, AcronymPolicy.NamespaceAcronyms)}",
        iter: 300);
    }

    /// <summary>
    /// (d) <see cref="NameMangler.ToSharpyName"/> is deterministic across every
    /// <see cref="ReverseNameContext"/> value.
    /// </summary>
    [Fact]
    public void ToSharpyName_IsDeterministic_AcrossContexts()
    {
        var contexts = Enum.GetValues<ReverseNameContext>();

        GenIdentifier.MixedFormIdentifier.Sample(name =>
        {
            foreach (var ctx in contexts)
            {
                var first = NameMangler.ToSharpyName(name, ctx);
                var second = NameMangler.ToSharpyName(name, ctx);
                Assert.Equal(first, second);
            }
        },
        print: name => $"input={name}",
        iter: 200);
    }
}
