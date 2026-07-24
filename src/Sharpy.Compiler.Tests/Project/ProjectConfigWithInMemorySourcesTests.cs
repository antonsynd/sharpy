using System.Reflection;
using Xunit;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// Guardrail for <see cref="ProjectConfig.WithInMemorySources"/> (#1099): the clone is a
/// hand-written property-by-property copy on a plain class (no record <c>with</c> safety net),
/// so a property added to <see cref="ProjectConfig"/> without a matching line in the clone body
/// would be silently dropped from the LSP buffer-overlay path. This test sets every settable
/// property to a non-default value via reflection and asserts the clone carries each one over —
/// the same silent-drop insurance the repo requires for <c>SemanticInfo.MergeFrom</c>.
/// If the probe factory throws for a new property type, extend the factory and
/// <c>WithInMemorySources</c> together.
/// </summary>
public class ProjectConfigWithInMemorySourcesTests
{
    [Fact]
    public void WithInMemorySources_CarriesEverySettablePropertyOver()
    {
        var config = new ProjectConfig();
        var properties = typeof(ProjectConfig)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(p => p.CanWrite)
            .ToList();

        // Sanity: the reflection sweep must see the settable surface (22 as of #1099); a sudden
        // drop to near-zero would mean the BindingFlags stopped matching, not that the class shrank.
        Assert.True(properties.Count >= 20,
            $"Expected the settable-property sweep to find the ProjectConfig surface, got {properties.Count}.");

        foreach (var property in properties)
        {
            property.SetValue(config, MakeNonDefaultValue(property, property.GetValue(config)));
        }

        var overlay = new Dictionary<string, string> { ["main.spy"] = "x = 1" };
        var clone = config.WithInMemorySources(overlay);

        foreach (var property in properties)
        {
            if (property.Name == nameof(ProjectConfig.InMemorySources))
            {
                Assert.Same(overlay, property.GetValue(clone));
                continue;
            }

            Assert.True(
                Equals(property.GetValue(config), property.GetValue(clone)),
                $"ProjectConfig.{property.Name} was not carried over by WithInMemorySources — " +
                "add it to the clone body.");
        }
    }

    private static object MakeNonDefaultValue(PropertyInfo property, object? current)
    {
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        if (type == typeof(string))
            return (current as string ?? string.Empty) + "_probe";

        if (type == typeof(bool))
            return !(current as bool? ?? false);

        if (type.IsClass && type.GetConstructor(Type.EmptyTypes) != null)
        {
            // Fresh collection/reference instance: differs by reference from both null and any
            // default-initialized instance, which is what the carried-over-by-reference
            // assertion needs (the clone is intentionally shallow).
            return Activator.CreateInstance(type)!;
        }

        throw new InvalidOperationException(
            $"No probe-value recipe for ProjectConfig.{property.Name} ({property.PropertyType}). " +
            "Extend this factory and verify WithInMemorySources copies the new property.");
    }
}
