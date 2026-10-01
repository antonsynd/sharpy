using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Pretty;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.PrettyTests;

/// <summary>
/// The parse-level oracle of the formatter sweep (O3: <see cref="AstNormalizer"/> +
/// <see cref="StructuralEqualityComparer"/>) can only see a field the comparer reads and the
/// normalizer carries. P22b measured two silent drops it was blind to — every <c>def</c> in a union
/// body (<c>UnionDef.Body</c>) and the <c>@[attr]</c> spelling (<c>Decorator.IsBracketAttribute</c>)
/// — plus the member-access escape (<c>MemberAccess.IsMemberBacktickEscaped</c>) and two normalizer
/// arms that rebuilt a record with <c>new</c> and lost a flag.
///
/// <para><b>Behavioural, by reflection.</b> For every concrete record in
/// <c>Sharpy.Compiler.Parser.Ast</c> reachable from a <see cref="Node"/> (the node itself, or an
/// auxiliary record one or two levels inside it — a decorator, a keyword argument, a union case and
/// its fields …), and every settable public property of it: two instances that differ ONLY in that
/// property must compare unequal after normalization. A property that compares equal is either a
/// blind spot (raw instances already equal: the comparer never reads it) or a normalizer drop (raw
/// unequal, normalized equal). The exclusion set names every property that is not a syntax fact,
/// each with its reason; an excluded property that DOES discriminate fails too, so the set cannot
/// rot into a hiding place.</para>
/// </summary>
public class StructuralComparerFieldTotalityTests
{
    private readonly ITestOutputHelper _output;

    public StructuralComparerFieldTotalityTests(ITestOutputHelper output) => _output = output;

    private static readonly Assembly AstAssembly = typeof(Node).Assembly;
    private const string AstNamespace = "Sharpy.Compiler.Parser.Ast";

    /// <summary>Property-name suffixes that are source positions, never syntax: the comparer is position-blind by design and the normalizer zeroes them.</summary>
    private static readonly string[] PositionSuffixes =
    {
        "LineStart", "LineEnd", "ColumnStart", "ColumnEnd", "Span", "Line", "Column", "Offset",
        "Positions", "Spans",
    };

    private const string DottedSpelling =
        "the dotted spelling is compared through EscapedSpelling(joined name, parts, escape flags): an EMPTY parts array reads as the "
        + "joined name with no escapes (hand-built nodes, `from . import x`), so a parts-only change with the same unescaped spelling is "
        + "equal by design; a different split or any escape flag is compared (the flags are probed separately)";

    /// <summary>Non-position exclusions: <c>Type.Property</c> (or <c>*.Property</c>) → why it is not compared.</summary>
    private static readonly Dictionary<string, string> Exclusions = new(StringComparer.Ordinal)
    {
        ["*.LeadingTrivia"] = "trivia: comments are compared by the sweep's O4 on the token stream, not by the AST comparer",
        ["*.TrailingTrivia"] = "trivia: as LeadingTrivia",
        ["ImportAlias.NameParts"] = DottedSpelling,
        ["FromImportStatement.ModuleParts"] = DottedSpelling,
        ["TypeAnnotation.NameParts"] = DottedSpelling,
        ["FromImportStatement.ReExportedSymbols"] = "semantic-analysis output (symbols), written after parsing — the Rule-3 smell beside ResolvedModulePath",
        ["FromImportStatement.ResolvedModulePath"] = "semantic-analysis output written onto the node after parsing (a Rule-3 smell, ledgered), not a syntax fact the formatter could drop",
    };

    private sealed record Finding(string Path, string Kind, string Detail);

    [Fact]
    public void EveryAstRecordProperty_DiscriminatesUnderNormalizeAndCompare_OrIsExcludedWithAReason()
    {
        var findings = new List<Finding>();
        var probed = 0;
        foreach (var type in ConcreteNodeTypes())
        {
            Node baseline;
            try
            {
                baseline = (Node)Create(type, seed: 0, depth: 0)!;
            }
            catch (Exception ex)
            {
                findings.Add(new Finding(type.Name, "unconstructible", ex.GetBaseException().Message));
                continue;
            }

            foreach (var (path, key, variant) in Variants(type.Name, baseline, baseline.GetType(), auxDepth: 0, rebuild: v => (Node)v))
            {
                probed++;
                findings.AddRange(Probe(path, key, baseline, variant));
            }
        }

        foreach (var f in findings.OrderBy(f => f.Kind).ThenBy(f => f.Path, StringComparer.Ordinal))
            _output.WriteLine($"FIELDTOT {f.Kind} {f.Path} {f.Detail}");
        _output.WriteLine($"FIELDTOT probed={probed} findings={findings.Count}");

        Assert.True(probed > 300, $"only {probed} properties probed — the universe is not real");
        Assert.True(findings.Count == 0,
            $"{findings.Count} AST record propert(ies) the formatter's structural oracle cannot see:\n  "
            + string.Join("\n  ", findings.Select(f => $"{f.Kind}: {f.Path} {f.Detail}")));
    }

    private static IEnumerable<Finding> Probe(string path, string key, Node baseline, Node? variant)
    {
        if (variant is null)
            return IsExcluded(key, out _) ? Array.Empty<Finding>() : new[] { new Finding(path, "unvaried", "the probe has no second value for this type: teach Vary or exclude it with a reason") };

        bool rawEqual, normEqual;
        try
        {
            rawEqual = StructuralEqualityComparer.Instance.Equals(baseline, variant);
            normEqual = StructuralEqualityComparer.Instance.Equals(
                AstNormalizer.Instance.Visit(baseline), AstNormalizer.Instance.Visit(variant));
        }
        catch (Exception ex)
        {
            return new[] { new Finding(path, "probe-threw", ex.GetBaseException().GetType().Name + ": " + ex.GetBaseException().Message) };
        }

        var excluded = IsExcluded(key, out _);
        if (excluded)
            return normEqual ? Array.Empty<Finding>() : new[] { new Finding(path, "excluded-but-discriminates", "delete its exclusion") };
        if (!normEqual)
            return Array.Empty<Finding>();
        return new[] { new Finding(path, rawEqual ? "comparer-blind" : "normalizer-drops", "") };
    }

    /// <summary>
    /// Every single-property variant of <paramref name="instance"/>, rebuilt into the host node via
    /// <paramref name="rebuild"/>; recurses into auxiliary (non-<see cref="Node"/>) records held by
    /// a property or a one-element array, up to two levels.
    /// </summary>
    private static IEnumerable<(string Path, string Key, Node Variant)> Variants(string prefix, object instance, Type type, int auxDepth, Func<object, Node> rebuild)
    {
        foreach (var prop in SettableProperties(type))
        {
            var path = $"{prefix}.{prop.Name}";
            var current = prop.GetValue(instance);
            var changed = Vary(prop.PropertyType, current, auxDepth);
            if (changed.Ok)
            {
                var copy = Clone(instance);
                prop.SetValue(copy, changed.Value);
                yield return (path, $"{type.Name}.{prop.Name}", rebuild(copy));
            }
            else
            {
                // A property the probe cannot vary is not silently skipped: it surfaces as `unvaried`
                // unless the exclusion set names it.
                yield return (path, $"{type.Name}.{prop.Name}", null!);
            }

            if (auxDepth >= 2)
                continue;

            // Into an auxiliary record: vary each of ITS properties, rebuilt into this one.
            var elementType = ImmutableArrayElement(prop.PropertyType);
            var auxType = elementType ?? Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            if (!IsAuxRecord(auxType) || current == null)
                continue;
            var aux = elementType != null ? FirstElement(current) : current;
            if (aux == null)
                continue;
            foreach (var (subPath, subKey, subVariant) in Variants($"{path}{(elementType != null ? "[]" : "")}", aux, aux.GetType(), auxDepth + 1, v =>
            {
                var copy = Clone(instance);
                prop.SetValue(copy, elementType != null ? MakeArray(elementType, new[] { v }) : v);
                return rebuild(copy);
            }))
            {
                yield return (subPath, subKey, subVariant);
            }
        }
    }

    // ================================================================
    // Instance construction
    // ================================================================

    private static IEnumerable<Type> ConcreteNodeTypes()
        => AstAssembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Node)) && !t.IsAbstract && t.Namespace == AstNamespace)
            .OrderBy(t => t.Name, StringComparer.Ordinal);

    private static bool IsAuxRecord(Type t)
        => t.Namespace == AstNamespace && t.IsClass && !typeof(Node).IsAssignableFrom(t) && t.GetMethod("<Clone>$") != null;

    private static IEnumerable<PropertyInfo> SettableProperties(Type t)
        => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.SetMethod is { IsPublic: true } && p.GetIndexParameters().Length == 0)
            .OrderBy(p => p.Name, StringComparer.Ordinal);

    private static object Clone(object record)
        => record.GetType().GetMethod("<Clone>$")!.Invoke(record, null)!;

    /// <summary>A populated instance: strings, flags, numbers and enums seeded, children built (shallowly below depth 3), positions and trivia left unset.</summary>
    private static object? Create(Type type, int seed, int depth)
    {
        if (type.IsAbstract)
        {
            var concrete = ConcreteSubtypes(type);
            if (concrete.Count == 0)
                return null;
            return Create(concrete[Math.Min(seed, concrete.Count - 1)], seed, depth);
        }

        object instance;
        var parameterless = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes);
        if (parameterless != null)
        {
            instance = parameterless.Invoke(null);
        }
        else
        {
            var ctor = type.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
            instance = ctor.Invoke(ctor.GetParameters().Select(p => Value(p.ParameterType, seed, depth + 1)).ToArray());
        }

        foreach (var prop in SettableProperties(type))
        {
            if (IsPosition(prop.Name) || prop.Name is "LeadingTrivia" or "TrailingTrivia")
                continue;
            prop.SetValue(instance, Value(prop.PropertyType, seed, depth + 1));
        }

        return instance;
    }

    private static object? Value(Type type, int seed, int depth)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying != null)
            return Value(underlying, seed, depth);
        if (type == typeof(string))
            return seed == 0 ? "a" : "b";
        if (type == typeof(bool))
            return seed != 0;
        if (type == typeof(int))
            return seed + 1;
        if (type == typeof(long))
            return (long)(seed + 1);
        if (type == typeof(double))
            return seed + 1.5;
        if (type == typeof(char))
            return seed == 0 ? 'a' : 'b';
        if (type.IsEnum)
        {
            var values = Enum.GetValues(type);
            return values.GetValue(Math.Min(seed, values.Length - 1));
        }

        var element = ImmutableArrayElement(type);
        if (element != null)
        {
            var item = depth < 3 ? Value(element, seed, depth) : null;
            return MakeArray(element, item == null ? Array.Empty<object>() : new[] { item });
        }

        if (type.Namespace == AstNamespace && type.IsClass)
            return depth < 4 ? Create(type, seed, depth) : (type.IsAbstract ? Create(type, seed, 9) : MinimalInstance(type));
        if (type.IsValueType)
            return Activator.CreateInstance(type);
        return null;
    }

    private static object? MinimalInstance(Type type)
    {
        var parameterless = type.GetConstructor(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, Type.EmptyTypes);
        return parameterless?.Invoke(null);
    }

    /// <summary>A different value of the same type, or <c>Ok = false</c> when the type has only one value.</summary>
    private static (bool Ok, object? Value) Vary(Type type, object? current, int depth)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (current == null)
        {
            var made = Value(underlying, 0, depth + 1);
            return (made != null, made);
        }

        if (underlying == typeof(string))
            return (true, (string)current + "x");
        if (underlying == typeof(bool))
            return (true, !(bool)current);
        if (underlying == typeof(int))
            return (true, (int)current + 1);
        if (underlying == typeof(long))
            return (true, (long)current + 1);
        if (underlying == typeof(double))
            return (true, (double)current + 1);
        if (underlying == typeof(char))
            return (true, (char)((char)current + 1));
        if (underlying.IsEnum)
        {
            var values = Enum.GetValues(underlying).Cast<object>().ToList();
            if (values.Count < 2)
                return (false, null);
            return (true, values[(values.IndexOf(current) + 1) % values.Count]);
        }

        var element = ImmutableArrayElement(underlying);
        if (element == typeof(bool))
        {
            // A parallel escape-flag array: a missing entry reads as false by contract, so emptying
            // [false] changes nothing — flip the first flag instead.
            var flags = ((IEnumerable)current).Cast<object>().Select(f => (object)(bool)f).ToList();
            if (flags.Count == 0)
                return (true, MakeArray(element, new object[] { true }));
            flags[0] = !(bool)flags[0];
            return (true, MakeArray(element, flags));
        }

        if (element != null)
        {
            var items = ((IEnumerable)current).Cast<object>().ToList();
            if (items.Count > 0)
                return (true, MakeArray(element, Array.Empty<object>()));
            var item = Value(element, 0, depth + 1);
            return (item != null, item == null ? null : MakeArray(element, new[] { item }));
        }

        if (underlying.Namespace == AstNamespace && underlying.IsClass)
        {
            // A different concrete type when the slot is abstract; otherwise the other seed.
            var declared = underlying.IsAbstract ? underlying : current.GetType();
            if (declared.IsAbstract)
            {
                var others = ConcreteSubtypes(declared).Where(t => t != current.GetType()).ToList();
                foreach (var other in others)
                {
                    try
                    {
                        var made = Create(other, 0, depth + 1);
                        if (made != null)
                            return (true, made);
                    }
                    catch
                    {
                        // Try the next concrete type.
                    }
                }

                return (false, null);
            }

            return (true, Create(declared, 1, depth + 1));
        }

        return (false, null);
    }

    private static List<Type> ConcreteSubtypes(Type abstractType)
        => AstAssembly.GetTypes()
            .Where(t => abstractType.IsAssignableFrom(t) && !t.IsAbstract && t.Namespace == AstNamespace)
            .OrderBy(t => t.GetProperties().Length)
            .ThenBy(t => t.Name, StringComparer.Ordinal)
            .ToList();

    private static Type? ImmutableArrayElement(Type t)
        => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(ImmutableArray<>) ? t.GetGenericArguments()[0] : null;

    private static object? FirstElement(object array)
        => ((IEnumerable)array).Cast<object?>().FirstOrDefault();

    private static readonly MethodInfo CreateArray = typeof(ImmutableArray).GetMethods()
        .Single(m => m.Name == nameof(ImmutableArray.Create) && m.GetParameters() is [{ ParameterType.IsArray: true }]);

    private static object MakeArray(Type element, IReadOnlyList<object> items)
    {
        var array = Array.CreateInstance(element, items.Count);
        for (var i = 0; i < items.Count; i++)
            array.SetValue(items[i], i);
        return CreateArray.MakeGenericMethod(element).Invoke(null, new object[] { array })!;
    }

    private static bool IsPosition(string name) => PositionSuffixes.Any(s => name.EndsWith(s, StringComparison.Ordinal));

    /// <summary><paramref name="key"/> is <c>DeclaringRecord.Property</c>.</summary>
    private static bool IsExcluded(string key, out string reason)
    {
        var property = key[(key.IndexOf('.') + 1)..];
        var owner = key[..key.IndexOf('.')];
        if (IsPosition(property))
        {
            reason = "source position";
            return true;
        }

        return Exclusions.TryGetValue($"{owner}.{property}", out reason!)
            || Exclusions.TryGetValue($"*.{property}", out reason!);
    }
}
