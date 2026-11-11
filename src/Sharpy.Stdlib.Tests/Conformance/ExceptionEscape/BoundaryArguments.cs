using SCG = System.Collections.Generic;
using System.Numerics;
using System.Reflection;

namespace Sharpy.Stdlib.Tests.Conformance.ExceptionEscape;

/// <summary>
/// The candidate arguments of the sweep, per parameter type. Each list's FIRST value is the nominal one (the value
/// a parameter holds while another parameter varies). Primitive pools hold the type's limits and the boundaries the
/// Stdlib's domains share (calendar and clock fields, microsecond and year limits); string pools hold empty,
/// garbage, format-shaped and very long text. A Sharpy type's pool is the set of its instances the sweep itself
/// produced (<see cref="InstancePool"/>).
/// </summary>
internal static class BoundaryArguments
{
    public static readonly object[] Ints =
    {
        1, 0, -1, 2, 12, 13, 23, 24, 31, 59, 60, 999_999, 1_000_000, 9_999, 10_000, int.MinValue, int.MaxValue,
    };

    public static readonly object[] Longs =
    {
        1L, 0L, -1L, 9_999L, 10_000L, 999_999_999L, 1_000_000_000L, (long)int.MaxValue + 1, long.MinValue, long.MaxValue,
    };

    public static readonly object[] Doubles =
    {
        1.0, 0.0, -0.0, -1.0, 0.5, 1e-300, double.Epsilon, 1e308, -1e308, double.MaxValue, double.NaN,
        double.PositiveInfinity, double.NegativeInfinity,
    };

    public static readonly object[] Floats =
    {
        1.0f, 0.0f, -1.0f, float.MaxValue, float.NaN, float.PositiveInfinity, float.NegativeInfinity,
    };

    public static readonly object[] Strings =
    {
        "a", "", " ", "not-a-date", "2020-01-01", "2020-13-01", "20200101", "2020-01-01T12:30:45.123456+05:30",
        "12:30:45", "%", "%Y-%m-%d %H:%M:%S", "%Q", "%%", "-1", "nan", "\0", "\uD800", "é", new string('x', 100_000),
    };

    public static readonly object[] Chars = { 'a', '\0', '%', '\uD800' };

    public static readonly object[] BigIntegers =
    {
        BigInteger.One, BigInteger.Zero, BigInteger.MinusOne, BigInteger.Pow(2, 64), -BigInteger.Pow(2, 64), BigInteger.Pow(10, 100),
    };

    /// <summary>
    /// The candidates for <paramref name="type"/>, or null when the sweep has no generator for it (the member is
    /// then reported as unswept, never silently skipped). <paramref name="nullable"/> adds <c>null</c> after the
    /// nominal value.
    /// </summary>
    public static IReadOnlyList<object?>? For(Type type, bool nullable, InstancePool pool)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
            return WithNull(For(underlying, false, pool));

        IReadOnlyList<object?>? values = type switch
        {
            _ when type == typeof(int) => Ints,
            _ when type == typeof(long) => Longs,
            _ when type == typeof(double) => Doubles,
            _ when type == typeof(float) => Floats,
            _ when type == typeof(bool) => new object?[] { true, false },
            _ when type == typeof(string) => Strings,
            _ when type == typeof(char) => Chars,
            _ when type == typeof(BigInteger) => BigIntegers,
            _ when type == typeof(Type) => new object?[] { typeof(object) },
            _ when type == typeof(IFormatProvider) => new object?[] { System.Globalization.CultureInfo.InvariantCulture, null },
            _ when type.IsEnum => Enum.GetValues(type).Cast<object?>().Append(Enum.ToObject(type, -1)).ToArray(),
            _ when type == typeof(object) => new object?[] { "a", 1 }.Concat(pool.AllInstances().Take(8)).ToArray(),
            _ when SideEffectRule.IsSharpyAssembly(type.Assembly) && !type.IsGenericTypeDefinition => pool.For(type),
            _ => null,
        };
        if (values is null)
            return null;
        return nullable ? WithNull(values) : values;
    }

    private static IReadOnlyList<object?>? WithNull(IReadOnlyList<object?>? values)
        => values is null ? null : values.Take(1).Append(null).Concat(values.Skip(1)).ToArray();

    /// <summary>Whether <paramref name="p"/> admits null: a nullable value type or a nullable-annotated reference.</summary>
    public static bool AdmitsNull(ParameterInfo p, NullabilityInfoContext context)
    {
        if (p.ParameterType.IsValueType)
            return Nullable.GetUnderlyingType(p.ParameterType) is not null;
        return context.Create(p).WriteState == NullabilityState.Nullable;
    }
}

/// <summary>
/// Instances of Sharpy types the sweep produced, by exact type. An interface or abstract parameter draws from
/// every pooled type assignable to it. Each pool is thinned to a fixed spread: the first instance (nominal), the
/// least and greatest when the type orders itself, and evenly spaced others.
/// </summary>
internal sealed class InstancePool
{
    private const int PerType = 10;
    private readonly Dictionary<Type, SCG.List<object>> _byType = new();

    public bool Add(object instance)
    {
        var type = instance.GetType();
        if (!_byType.TryGetValue(type, out var list))
            _byType[type] = list = new SCG.List<object>();
        if (list.Any(x => SafeEquals(x, instance)))
            return false;
        list.Add(instance);
        return true;
    }

    public int Count(Type type) => _byType.Where(kv => type.IsAssignableFrom(kv.Key)).Sum(kv => kv.Value.Count);

    public IEnumerable<object> AllInstances() => _byType.OrderBy(kv => kv.Key.FullName, StringComparer.Ordinal).SelectMany(kv => Spread(kv.Value).Take(2));

    public IReadOnlyList<object?> For(Type type)
        => _byType.Where(kv => type.IsAssignableFrom(kv.Key))
            .OrderBy(kv => kv.Key.FullName, StringComparer.Ordinal)
            .SelectMany(kv => Spread(kv.Value))
            .Cast<object?>()
            .ToArray();

    private static IEnumerable<object> Spread(SCG.List<object> all)
    {
        if (all.Count <= PerType)
            return all;
        var picked = new SCG.List<object> { all[0] };
        var compare = Comparer(all[0].GetType());
        if (compare is not null)
        {
            try
            {
                var sorted = all.ToList();
                sorted.Sort(compare);
                picked.Add(sorted[0]);
                picked.Add(sorted[^1]);
            }
            catch (InvalidOperationException)
            {
                // a comparison threw: keep the unsorted spread
            }
        }
        for (int i = 1; picked.Count < PerType && i < PerType; i++)
            picked.Add(all[i * (all.Count - 1) / (PerType - 1)]);
        return picked.Distinct(ReferenceEqualityComparer.Instance).Cast<object>();
    }

    /// <summary>The type's own ordering (<c>IComparable&lt;T&gt;</c>, else <c>IComparable</c>), or null.</summary>
    private static Comparison<object>? Comparer(Type type)
    {
        var generic = typeof(IComparable<>).MakeGenericType(type);
        if (generic.IsAssignableFrom(type))
        {
            var method = generic.GetMethod("CompareTo")!;
            return (a, b) => (int)method.Invoke(a, new[] { b })!;
        }
        if (typeof(IComparable).IsAssignableFrom(type))
            return (a, b) => ((IComparable)a).CompareTo(b);
        return null;
    }

    private static bool SafeEquals(object a, object b)
    {
        try
        {
            return a.Equals(b);
        }
        catch (Exception)
        {
            return ReferenceEquals(a, b);
        }
    }
}
