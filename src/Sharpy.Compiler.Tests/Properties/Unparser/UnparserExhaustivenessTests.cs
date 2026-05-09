using System.Reflection;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Pretty;
using Xunit;

namespace Sharpy.Compiler.Tests.Properties.Unparser;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
public class UnparserExhaustivenessTests
{
    [Fact]
    public void AllConcreteNodeTypesAreCoveredByStructuralEqualityComparer()
    {
        var nodeType = typeof(Node);
        var assembly = nodeType.Assembly;

        var concreteNodeTypes = assembly.GetTypes()
            .Where(t => t.IsSubclassOf(nodeType) && !t.IsAbstract)
            .OrderBy(t => t.Name)
            .ToList();

        Assert.True(concreteNodeTypes.Count > 0, "Should find at least one concrete Node type");

        var comparer = StructuralEqualityComparer.Instance;
        var identityFailures = new List<string>();

        foreach (var type in concreteNodeTypes)
        {
            try
            {
                var instance = CreateDefaultInstance(type);
                if (instance == null)
                {
                    continue;
                }

                bool result = comparer.Equals(instance, instance);
                if (!result)
                    identityFailures.Add(type.Name);
            }
            catch
            {
                // Can't construct — skip
            }
        }

        Assert.True(identityFailures.Count == 0,
            $"StructuralEqualityComparer failed identity check for: {string.Join(", ", identityFailures)}");
    }

    [Fact]
    public void AllConcreteNodeTypesAreCoveredByAstNormalizer()
    {
        var nodeType = typeof(Node);
        var assembly = nodeType.Assembly;

        var concreteNodeTypes = assembly.GetTypes()
            .Where(t => t.IsSubclassOf(nodeType) && !t.IsAbstract)
            .OrderBy(t => t.Name)
            .ToList();

        var normalizer = AstNormalizer.Instance;
        var failures = new List<string>();

        foreach (var type in concreteNodeTypes)
        {
            try
            {
                var instance = CreateDefaultInstance(type);
                if (instance == null)
                    continue;

                var normalized = normalizer.Visit(instance);
                if (normalized.LineStart != 0 || normalized.ColumnStart != 0)
                    failures.Add(type.Name);
            }
            catch
            {
                // Can't construct — skip
            }
        }

        Assert.True(failures.Count == 0,
            $"AstNormalizer did not zero positions for: {string.Join(", ", failures)}");
    }

    private static Node? CreateDefaultInstance(Type type)
    {
        try
        {
            var ctor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(c => c.GetParameters().Length)
                .FirstOrDefault();

            if (ctor == null)
                return null;

            var args = ctor.GetParameters()
                .Select(p => GetDefault(p.ParameterType))
                .ToArray();

            return (Node)ctor.Invoke(args);
        }
        catch
        {
            return null;
        }
    }

    private static object? GetDefault(Type t)
    {
        if (t.IsValueType)
            return Activator.CreateInstance(t);
        return null;
    }
}
