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

    /// <summary>
    /// Mechanical arm-coverage guard for <see cref="AstNormalizer"/>, built like
    /// <see cref="AllConcreteNodeTypesHaveComparerArms"/>. The fact it replaces was vacuous twice
    /// over: a default instance already has <c>LineStart == 0</c>, so "the normalizer zeroed the
    /// position" held with no arm at all; and a missing arm returned <see langword="null"/> (the
    /// generic visitor's <c>default!</c>), whose <c>.LineStart</c> threw into a bare
    /// <c>catch</c> that skipped the type. DecoratedStatement, MultiAxisAccess, SubscriptDimension,
    /// QuestionMarkExpression and TypeAnnotation all had no arm and all passed (#1974 P21a gate:
    /// a <c>@suppress</c>-decorated statement in a Formatting fixture crashed the smoke test).
    /// Now: each instance gets a NON-zero position first; a missing arm is the loud
    /// <see cref="AstNormalizer.NoArmSentinel"/> naming the instance's OWN type (a sentinel for a
    /// degenerate <c>null!</c> child names "&lt;null&gt;" and proves the arm ran); a
    /// <see langword="null"/> result is a missing arm; a surviving position is a failure.
    /// </summary>
    [Fact]
    public void AllConcreteNodeTypesHaveAstNormalizerArms()
    {
        var nodeType = typeof(Node);
        var concreteNodeTypes = nodeType.Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(nodeType) && !t.IsAbstract)
            .OrderBy(t => t.Name)
            .ToList();

        // The scan below can only SEE a missing arm through the loud default: without it, the
        // generic visitor's default walks the default instance's children, meets a degenerate
        // null! member and throws an NRE that is indistinguishable from "the arm ran" (measured:
        // with the pre-fix null default restored, this fact stayed green). So the loud default is
        // itself asserted — removing it is red here, not a silent return to vacuity.
        var defaultVisit = typeof(AstNormalizer).GetMethod(nameof(AstNormalizer.DefaultVisit), new[] { typeof(Node) })!;
        Assert.True(defaultVisit.DeclaringType == typeof(AstNormalizer),
            "AstNormalizer must override DefaultVisit to throw AstNormalizer.NoArmSentinel; the generic "
            + "default returns null and hides every missing arm from this guard.");

        var normalizer = AstNormalizer.Instance;
        var missingArm = new List<string>();
        var notZeroed = new List<string>();
        var visited = 0;

        foreach (var type in concreteNodeTypes)
        {
            var instance = CreateDefaultInstance(type);
            if (instance == null)
                continue;

            // Plant a position the arm must erase, so "zeroed" is an observation, not the default.
            typeof(Node).GetProperty(nameof(Node.LineStart))!.SetValue(instance, 7);
            typeof(Node).GetProperty(nameof(Node.ColumnStart))!.SetValue(instance, 3);
            visited++;

            Node? normalized;
            try
            {
                normalized = normalizer.Visit(instance);
            }
            catch (InvalidOperationException ex)
                when (ex.Message == $"{AstNormalizer.NoArmSentinel} {type.Name}")
            {
                missingArm.Add(type.Name);
                continue;
            }
            catch
            {
                // The arm exists and ran into a degenerate null! member of the default instance.
                continue;
            }

            if (normalized is null)
                missingArm.Add($"{type.Name} (returned null)");
            else if (normalized.LineStart != 0 || normalized.ColumnStart != 0)
                notZeroed.Add(type.Name);
        }

        // Positive control: the universe is real, not an empty loop that passes vacuously.
        Assert.True(visited > 50, $"only {visited} node types were constructible — the guard is not measuring anything");
        Assert.True(missingArm.Count == 0,
            $"AstNormalizer has no arm for: {string.Join(", ", missingArm)}. Add a Visit override for each listed node type.");
        Assert.True(notZeroed.Count == 0,
            $"AstNormalizer did not zero positions for: {string.Join(", ", notZeroed)}");
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
