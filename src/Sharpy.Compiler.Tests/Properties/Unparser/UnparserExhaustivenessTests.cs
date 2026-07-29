using System.Reflection;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Pretty;
using Xunit;

namespace Sharpy.Compiler.Tests.Properties.Unparser;

[Trait("Category", "Property")]
[Trait("Category", "RandomProperty")]
public class UnparserExhaustivenessTests
{
    /// <summary>Sentinel thrown by StructuralEqualityComparer's default arm (#1152) — bound to the
    /// production constant so the message can never drift out from under the StartsWith match.</summary>
    private const string NoArmSentinel = StructuralEqualityComparer.NoArmSentinel;

    // NOTE: this fact compares each instance against ITSELF, so Equals short-circuits on the
    // ReferenceEquals(x, y) fast path (StructuralEqualityComparer.cs) and NEVER reaches the switch.
    // It therefore guards the fast path but is VACUOUS for arm coverage — every type "passes"
    // without its arm ever executing. Arm coverage is enforced by
    // AllConcreteNodeTypesHaveComparerArms below, which compares two DISTINCT instances (#1152).
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

    /// <summary>
    /// Mechanical arm-coverage guard: for each concrete Node type build TWO distinct default
    /// instances and compare them, defeating the ReferenceEquals fast path so the switch actually
    /// runs. A missing arm now surfaces as the loud sentinel (StructuralEqualityComparer's default
    /// arm throws — #1152); any other outcome (an NRE from a degenerate <c>null!</c> member, or a
    /// boolean result) proves the arm exists and executed. This is what the identity fact above
    /// could not do.
    /// </summary>
    [Fact]
    public void AllConcreteNodeTypesHaveComparerArms()
    {
        var nodeType = typeof(Node);
        var assembly = nodeType.Assembly;

        var concreteNodeTypes = assembly.GetTypes()
            .Where(t => t.IsSubclassOf(nodeType) && !t.IsAbstract)
            .OrderBy(t => t.Name)
            .ToList();

        Assert.True(concreteNodeTypes.Count > 0, "Should find at least one concrete Node type");

        var comparer = StructuralEqualityComparer.Instance;
        var missingArm = new List<string>();

        foreach (var type in concreteNodeTypes)
        {
            // Two DISTINCT instances so ReferenceEquals(x, y) cannot hide a missing arm.
            var a = CreateDefaultInstance(type);
            var b = CreateDefaultInstance(type);
            if (a == null || b == null)
                continue;

            try
            {
                // Result is irrelevant — we only care whether an arm existed to run.
                _ = comparer.Equals(a, b);
            }
            catch (Exception ex) when (ex.Message.StartsWith(NoArmSentinel, StringComparison.Ordinal))
            {
                missingArm.Add(type.Name);
            }
            catch
            {
                // Any other exception proves the arm exists and executed — coverage satisfied.
            }
        }

        Assert.True(missingArm.Count == 0,
            $"StructuralEqualityComparer has no arm for: {string.Join(", ", missingArm)}. "
            + "Add a switch arm in StructuralEqualityComparer.Equals for each listed node type.");
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
