using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Semantic;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Discovery;

/// <summary>
/// A CLR declaration's nullability is a TREE, not a flag: <c>List&lt;object?&gt;</c> is
/// <c>list[object | None]</c> and <c>IDictionary&lt;string, string?&gt;</c> is
/// <c>dict[str, str | None]</c>. Reading only the top-level state described
/// <c>Yaml.safe_load_all</c> (declared <c>List&lt;object?&gt;</c>) as <c>list[object]</c>, and under
/// list invariance the faithful <c>list[object | None]</c> spelling a caller must write was refused
/// in both directions (#1847).
///
/// <para>
/// The matrix is nullable-argument POSITION (list element, dict value, dict key, nested generic,
/// array element, tuple item, top level over a nested one) × ROUTE (property, field, method return,
/// method parameter, overload-index signature) × DIRECTION (read / write). Every cell is a PAIR:
/// an annotated member and its un-annotated twin, because "is nullable here" passes vacuously if
/// the projection wraps everything, and "is not nullable there" passes vacuously if it wraps
/// nothing. Both halves of every pair must answer, or the harness proves nothing.
/// </para>
///
/// <para>
/// Assertions are STRUCTURAL (is the node at this path a <see cref="NullableType"/>) rather than on
/// display names: the display of a bridge-mapped collection is a separate decision, and a harness
/// that pins it would go red for a spelling change that is not the subject here.
/// </para>
/// </summary>
public class ClrNestedNullabilityMatrixTests
{
    private readonly ITestOutputHelper _output;

    public ClrNestedNullabilityMatrixTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// The surface under test. Declared HERE, in a nullable-enabled compilation unit, so every cell
    /// names an annotation this repository controls: a BCL member's annotations can change with the
    /// framework, and a matrix whose expectations live in someone else's assembly reports their
    /// churn as our regression.
    /// </summary>
    private sealed class Surface
    {
        // ── list element ──
        public List<string?> ListOfNullable { get; set; } = new();
        public List<string> ListOfNonNullable { get; set; } = new();

        // ── dict value / dict key ──
        public Dictionary<string, string?> DictNullableValue { get; set; } = new();
        public Dictionary<string, string> DictNonNullableValue { get; set; } = new();
        // IDictionary rather than Dictionary: `Dictionary<TKey, TValue>` constrains TKey to
        // `notnull`, so the nullable-KEY position cannot be spelled on it at all.
        public IDictionary<string?, string> DictNullableKey { get; set; } = default!;

        // ── nested generic (two levels down) ──
        public List<Dictionary<string, string?>> ListOfDictNullableValue { get; set; } = new();
        public List<Dictionary<string, string>> ListOfDictNonNullableValue { get; set; } = new();

        // ── array element ──
        public string?[] ArrayOfNullable { get; set; } = Array.Empty<string?>();
        public string[] ArrayOfNonNullable { get; set; } = Array.Empty<string>();

        // ── tuple item ──
        public (string?, string) TupleFirstNullable { get; set; }
        public (string, string) TupleNeitherNullable { get; set; }

        // ── top level AND nested, independently ──
        public List<string?>? NullableListOfNullable { get; set; }

        // ── fields (the same positions on the field route) ──
        public List<string?> ListOfNullableField = new();
        public List<string> ListOfNonNullableField = new();

        // ── returns (read direction) ──
        public List<string?> ReturnsListOfNullable() => new();
        public List<string> ReturnsListOfNonNullable() => new();
        public Dictionary<string, string?> ReturnsDictNullableValue() => new();

        // ── parameters (write direction) ──
        public void AcceptsListOfNullable(List<string?> arg) => _ = arg;
        public void AcceptsListOfNonNullable(List<string> arg) => _ = arg;
        public void AcceptsDictNullableValue(Dictionary<string, string?> arg) => _ = arg;
        public void AcceptsArrayOfNullable(string?[] arg) => _ = arg;
    }

    private enum Route { Property, Field, Return, Parameter }

    private enum Direction { Read, Write }

    /// <param name="Path">
    /// The nested position, as type-argument indices from the mapped root. Empty means the root.
    /// </param>
    private sealed record Cell(
        string Label, string Position, Route Route, Direction Direction,
        string Member, int[] Path, bool ExpectNullable);

    private static IEnumerable<Cell> Cells()
    {
        // ── list element ──
        yield return new Cell("list-element.property.nullable", "list element",
            Route.Property, Direction.Read, nameof(Surface.ListOfNullable), new[] { 0 }, true);
        yield return new Cell("list-element.property.twin", "list element",
            Route.Property, Direction.Read, nameof(Surface.ListOfNonNullable), new[] { 0 }, false);
        yield return new Cell("list-element.property.root-not-nullable", "list element",
            Route.Property, Direction.Read, nameof(Surface.ListOfNullable), Array.Empty<int>(), false);

        yield return new Cell("list-element.field.nullable", "list element",
            Route.Field, Direction.Read, nameof(Surface.ListOfNullableField), new[] { 0 }, true);
        yield return new Cell("list-element.field.twin", "list element",
            Route.Field, Direction.Read, nameof(Surface.ListOfNonNullableField), new[] { 0 }, false);

        yield return new Cell("list-element.return.nullable", "list element",
            Route.Return, Direction.Read, nameof(Surface.ReturnsListOfNullable), new[] { 0 }, true);
        yield return new Cell("list-element.return.twin", "list element",
            Route.Return, Direction.Read, nameof(Surface.ReturnsListOfNonNullable), new[] { 0 }, false);

        yield return new Cell("list-element.parameter.nullable", "list element",
            Route.Parameter, Direction.Write, nameof(Surface.AcceptsListOfNullable), new[] { 0 }, true);
        yield return new Cell("list-element.parameter.twin", "list element",
            Route.Parameter, Direction.Write, nameof(Surface.AcceptsListOfNonNullable), new[] { 0 }, false);

        // ── dict value ──
        yield return new Cell("dict-value.property.nullable", "dict value",
            Route.Property, Direction.Read, nameof(Surface.DictNullableValue), new[] { 1 }, true);
        yield return new Cell("dict-value.property.twin", "dict value",
            Route.Property, Direction.Read, nameof(Surface.DictNonNullableValue), new[] { 1 }, false);
        yield return new Cell("dict-value.property.key-untouched", "dict key",
            Route.Property, Direction.Read, nameof(Surface.DictNullableValue), new[] { 0 }, false);

        yield return new Cell("dict-value.return.nullable", "dict value",
            Route.Return, Direction.Read, nameof(Surface.ReturnsDictNullableValue), new[] { 1 }, true);
        yield return new Cell("dict-value.parameter.nullable", "dict value",
            Route.Parameter, Direction.Write, nameof(Surface.AcceptsDictNullableValue), new[] { 1 }, true);

        // ── dict KEY, which is a different argument index from the value ──
        yield return new Cell("dict-key.property.nullable", "dict key",
            Route.Property, Direction.Read, nameof(Surface.DictNullableKey), new[] { 0 }, true);
        yield return new Cell("dict-key.property.value-untouched", "dict value",
            Route.Property, Direction.Read, nameof(Surface.DictNullableKey), new[] { 1 }, false);

        // ── nested generic, two levels down ──
        yield return new Cell("nested-generic.property.nullable", "nested generic",
            Route.Property, Direction.Read, nameof(Surface.ListOfDictNullableValue), new[] { 0, 1 }, true);
        yield return new Cell("nested-generic.property.twin", "nested generic",
            Route.Property, Direction.Read, nameof(Surface.ListOfDictNonNullableValue), new[] { 0, 1 }, false);
        yield return new Cell("nested-generic.property.inner-key-untouched", "nested generic",
            Route.Property, Direction.Read, nameof(Surface.ListOfDictNullableValue), new[] { 0, 0 }, false);

        // ── array element ──
        yield return new Cell("array-element.property.nullable", "array element",
            Route.Property, Direction.Read, nameof(Surface.ArrayOfNullable), new[] { 0 }, true);
        yield return new Cell("array-element.property.twin", "array element",
            Route.Property, Direction.Read, nameof(Surface.ArrayOfNonNullable), new[] { 0 }, false);
        yield return new Cell("array-element.parameter.nullable", "array element",
            Route.Parameter, Direction.Write, nameof(Surface.AcceptsArrayOfNullable), new[] { 0 }, true);

        // ── tuple item ──
        yield return new Cell("tuple-item.property.nullable", "tuple item",
            Route.Property, Direction.Read, nameof(Surface.TupleFirstNullable), new[] { 0 }, true);
        yield return new Cell("tuple-item.property.second-untouched", "tuple item",
            Route.Property, Direction.Read, nameof(Surface.TupleFirstNullable), new[] { 1 }, false);
        yield return new Cell("tuple-item.property.twin", "tuple item",
            Route.Property, Direction.Read, nameof(Surface.TupleNeitherNullable), new[] { 0 }, false);

        // ── top level AND nested are independent states, not one flag ──
        yield return new Cell("top-and-nested.property.root", "top level over nested",
            Route.Property, Direction.Read, nameof(Surface.NullableListOfNullable), Array.Empty<int>(), true);
        yield return new Cell("top-and-nested.property.element", "top level over nested",
            Route.Property, Direction.Read, nameof(Surface.NullableListOfNullable), new[] { 0 }, true);
    }

    [Fact]
    [Trait("Category", "Conformance")]
    public void NestedNullability_SurvivesEveryPositionRouteAndDirection()
    {
        var bridge = new ClrTypeBridge();
        var cells = Cells().ToList();

        Assert.Equal(cells.Count, cells.Select(c => c.Label).Distinct().Count());

        // The pair rule, enforced rather than trusted: a POSITION whose cells all expect nullable
        // (or all expect non-nullable) cannot tell a working projection from one that wraps
        // everything (or nothing).
        foreach (var position in cells.GroupBy(c => c.Position))
        {
            Assert.True(position.Any(c => c.ExpectNullable),
                $"position '{position.Key}' has no nullable cell — nothing to detect");
            Assert.True(position.Any(c => !c.ExpectNullable),
                $"position '{position.Key}' has no non-nullable control — a projection that wraps "
                + "every position would pass it");
        }

        var failures = new List<string>();

        foreach (var cell in cells)
        {
            SemanticType mapped;
            try
            {
                mapped = Map(bridge, cell);
            }
            catch (Exception ex)
            {
                failures.Add($"{cell.Label}: crashed mapping — {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            var node = Navigate(mapped, cell.Path);
            if (node == null)
            {
                failures.Add($"{cell.Label}: no node at path [{string.Join(",", cell.Path)}] of "
                    + $"{mapped.GetDisplayName()} ({mapped.GetType().Name})");
                continue;
            }

            var isNullable = node is NullableType;
            if (isNullable != cell.ExpectNullable)
            {
                failures.Add($"{cell.Label} ({cell.Route}/{cell.Direction}): expected "
                    + $"{(cell.ExpectNullable ? "nullable" : "NOT nullable")} at "
                    + $"[{string.Join(",", cell.Path)}], got {node.GetDisplayName()} "
                    + $"in {mapped.GetDisplayName()}");
            }
        }

        _output.WriteLine($"Nested-nullability cells: {cells.Count}  Failures: {failures.Count}");
        foreach (var f in failures)
            _output.WriteLine("  " + f);

        Assert.True(failures.Count == 0,
            $"nested CLR nullability (#1847): {failures.Count} of {cells.Count} cells failed.\n"
            + string.Join("\n", failures.Select(f => "  " + f)));
    }

    /// <summary>
    /// The overload index describes the same members in its own encoding
    /// (<see cref="TypeSignature.NullableReferenceSentinel"/>). A nested annotation that survives on
    /// the <see cref="SemanticType"/> route and not on this one is the same defect one route over,
    /// so the sentinel tree is asserted at the same positions.
    /// </summary>
    [Fact]
    [Trait("Category", "Conformance")]
    public void NestedNullability_SurvivesTheOverloadIndexEncoding()
    {
        var builder = new OverloadIndexBuilder();

        var nullableElement = builder.CreateTypeSignature(Property(nameof(Surface.ListOfNullable)));
        Assert.Single(nullableElement.TypeArguments);
        Assert.Equal(TypeSignature.NullableReferenceSentinel, nullableElement.TypeArguments[0].Name);
        Assert.NotEqual(TypeSignature.NullableReferenceSentinel, nullableElement.Name);

        // The un-annotated twin: without it, a builder that wrapped every argument would pass above.
        var plainElement = builder.CreateTypeSignature(Property(nameof(Surface.ListOfNonNullable)));
        Assert.Single(plainElement.TypeArguments);
        Assert.NotEqual(TypeSignature.NullableReferenceSentinel, plainElement.TypeArguments[0].Name);

        // Write direction, and a two-argument shape where only the VALUE is annotated.
        var parameter = builder.CreateParameterTypeSignature(
            typeof(Surface).GetMethod(nameof(Surface.AcceptsDictNullableValue))!.GetParameters()[0]);
        Assert.Equal(2, parameter.TypeArguments.Count);
        Assert.NotEqual(TypeSignature.NullableReferenceSentinel, parameter.TypeArguments[0].Name);
        Assert.Equal(TypeSignature.NullableReferenceSentinel, parameter.TypeArguments[1].Name);
    }

    private static PropertyInfo Property(string name)
        => typeof(Surface).GetProperty(name)!;

    private static SemanticType Map(ClrTypeBridge bridge, Cell cell) => cell.Route switch
    {
        Route.Property => bridge.MapPropertyType(typeof(Surface).GetProperty(cell.Member)!),
        Route.Field => bridge.MapFieldType(typeof(Surface).GetField(cell.Member)!),
        Route.Return => bridge.MapReturnType(typeof(Surface).GetMethod(cell.Member)!),
        Route.Parameter => bridge.MapParameterType(
            typeof(Surface).GetMethod(cell.Member)!.GetParameters()[0]),
        _ => throw new InvalidOperationException($"unhandled route {cell.Route}")
    };

    /// <summary>
    /// The node at <paramref name="path"/>, following type-argument indices. A
    /// <see cref="NullableType"/> encountered ALONG the way is transparent — the top-level
    /// annotation of <c>List&lt;string?&gt;?</c> sits above the list, and the element is still the
    /// list's argument 0.
    /// </summary>
    private static SemanticType? Navigate(SemanticType type, IReadOnlyList<int> path)
    {
        var node = type;
        foreach (var index in path)
        {
            if (node is NullableType nullable)
                node = nullable.UnderlyingType;

            node = node switch
            {
                GenericType generic when index < generic.TypeArguments.Count => generic.TypeArguments[index],
                TupleType tuple when index < tuple.ElementTypes.Count => tuple.ElementTypes[index],
                _ => null
            };

            if (node == null)
                return null;
        }

        return node;
    }
}
