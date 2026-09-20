using System.Reflection;
using FluentAssertions;
using Sharpy.Compiler.Semantic.Validation;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic.Validation;

/// <summary>
/// Every <see cref="ISemanticValidator"/> owns a DISTINCT <see cref="ISemanticValidator.Order"/>.
///
/// <para><b>Why.</b> <c>ValidationPipeline.AddValidator</c> pins execution sequence by sorting on
/// <c>Order</c> with <c>List&lt;T&gt;.Sort</c>, which is NOT stable. Two validators sharing an Order
/// therefore execute in an order determined by registration and by the sort's internal pivoting —
/// and validators are not mutually independent (several read state an earlier one wrote). A tie is
/// an unpinned execution order wearing a pinned one's clothes, and it also makes
/// <c>ValidatorOrderingPropertyTests.ValidationDiagnostics_AreRegistrationOrderIndependent</c>
/// (which asserts the shuffled pipeline reproduces the canonical NAME sequence) fail only for the
/// shuffles that happen to swap the tied pair.</para>
///
/// <para><b>Anti-vacuity.</b> The scan asserts a literal minimum validator count before it looks for
/// duplicates, so a reflection query that finds nothing cannot pass.</para>
/// </summary>
public class ValidatorOrderUniquenessTests
{
    /// <summary>
    /// A floor on the number of validators the reflection scan must find, anchored to a LITERAL
    /// rather than to the scan's own result. The compiler had 38 ISemanticValidator implementations
    /// when this guard was written; the floor is deliberately below that so adding or retiring one
    /// does not churn the test, while an empty or broken scan still fails.
    /// </summary>
    private const int MinimumValidatorCount = 30;

    private static List<ISemanticValidator> AllValidators() =>
        typeof(ValidationPipeline).Assembly
            .GetTypes()
            .Where(t => typeof(ISemanticValidator).IsAssignableFrom(t)
                        && !t.IsAbstract
                        && !t.IsInterface
                        && t.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic
                                            | BindingFlags.Instance,
                                            binder: null, Type.EmptyTypes, modifiers: null) != null)
            .Select(t => (ISemanticValidator)Activator.CreateInstance(t, nonPublic: true)!)
            .ToList();

    [Fact]
    public void EveryValidatorOrder_IsDistinct()
    {
        var validators = AllValidators();

        validators.Count.Should().BeGreaterThanOrEqualTo(MinimumValidatorCount,
            "the reflection scan must actually find the validators — a scan that finds none would "
            + "satisfy the duplicate check vacuously");

        var duplicates = validators
            .GroupBy(v => v.Order)
            .Where(g => g.Count() > 1)
            .Select(g => $"Order {g.Key}: {string.Join(", ", g.Select(v => v.Name).OrderBy(n => n, StringComparer.Ordinal))}")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        duplicates.Should().BeEmpty(
            "ValidationPipeline sorts by Order with an UNSTABLE sort, so a shared Order leaves "
            + "execution sequence to registration order. Offenders: {0}",
            string.Join(" | ", duplicates));
    }

    /// <summary>
    /// The consumer-facing half: the pipeline the compiler actually runs has a STRICTLY increasing
    /// Order sequence, so its execution order is total and independent of how it was built.
    /// </summary>
    [Fact]
    public void DefaultPipeline_OrdersAreStrictlyIncreasing()
    {
        var orders = ValidationPipelineFactory.CreateDefault()
            .Validators.Select(v => v.Order).ToList();

        orders.Count.Should().BeGreaterThanOrEqualTo(MinimumValidatorCount,
            "the default pipeline registers the standard validator set");

        for (int i = 1; i < orders.Count; i++)
        {
            orders[i].Should().BeGreaterThan(orders[i - 1],
                "the default pipeline's Orders must be strictly increasing — position {0} "
                + "({1}) ties or precedes position {2} ({3})",
                i, orders[i], i - 1, orders[i - 1]);
        }
    }
}
