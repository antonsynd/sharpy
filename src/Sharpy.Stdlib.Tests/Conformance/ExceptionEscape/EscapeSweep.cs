using SCG = System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace Sharpy.Stdlib.Tests.Conformance.ExceptionEscape;

/// <summary>One swept member: what it can be called with, or why it is not called.</summary>
internal sealed record SweptMember(string Key, MethodBase Method, Type DeclaringType, bool ReadsClock);

/// <summary>A CLR exception that escaped a member, with the first arguments that produced it.</summary>
internal sealed record Leak(string Key, string Example);

/// <summary>The result of sweeping one set of types.</summary>
internal sealed class SweepResult
{
    public SortedSet<string> Swept { get; } = new(StringComparer.Ordinal);

    /// <summary>Every member the reflection over the module's types enumerated, swept or not.</summary>
    public SortedSet<string> Enumerated { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, string> Excluded { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, string> Unswept { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, Leak> Leaks { get; } = new(StringComparer.Ordinal);
    public SortedSet<string> Accepted { get; } = new(StringComparer.Ordinal);
    public SCG.List<string> HarnessErrors { get; } = new();

    /// <summary>Members over <c>CartesianCap</c> combinations: how many combinations were never tried.</summary>
    public SortedDictionary<string, double> Untried { get; } = new(StringComparer.Ordinal);

    /// <summary>Members that read the clock or host time zone: called, but nothing they return is pooled.</summary>
    public SortedDictionary<string, string> ClockReaders { get; } = new(StringComparer.Ordinal);
    public long Invocations { get; set; }
    public TimeSpan Elapsed { get; set; }
}

/// <summary>
/// The behavioural exception-escape sweep: every public member of a set of Sharpy types is invoked with boundary
/// arguments (<see cref="BoundaryArguments"/>), and every exception that escapes is classified by
/// <see cref="AcceptedExceptions"/>: a Python exception (accepted) or a bare CLR one (a leak, keyed
/// <c>prefix:Type.Member(ParamTypes):Exception.FullName</c>). Members the <see cref="SideEffectRule"/> marks are
/// excluded, never invoked. A returned iterator is drained (up to <see cref="IterationCap"/> items), so an exception
/// raised on its first <c>MoveNext</c> is an escape of the member that returned it.
/// </summary>
internal static class EscapeSweep
{
    internal const int CartesianCap = 4096;
    private const int Rounds = 3;
    private const int IterationCap = 256;

    /// <summary>The types a Stdlib module exposes: its <c>[SharpyModule]</c> class and every <c>[SharpyModuleType]</c> type.</summary>
    public static IReadOnlyList<Type> ModuleTypes(Assembly assembly, string module)
        => assembly.GetExportedTypes()
            .Where(t => t.GetCustomAttribute<SharpyModuleAttribute>()?.ModuleName == module
                        || t.GetCustomAttribute<SharpyModuleTypeAttribute>()?.ModuleName == module)
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToArray();

    public static SweepResult Run(string prefix, IReadOnlyList<Type> types)
    {
        var clock = Stopwatch.StartNew();
        var result = new SweepResult();
        var pool = new InstancePool();
        var nullability = new NullabilityInfoContext();
        var members = new SCG.List<SweptMember>();

        foreach (var type in types)
        {
            if (type.IsGenericTypeDefinition)
            {
                result.Unswept[$"{prefix}:{type.Name}"] = "generic type definition: no type argument generator";
                continue;
            }
            // Reading a static field runs the type's static constructor, so it is judged by the rule first.
            var initializer = type.TypeInitializer is { } cctor ? SideEffectRule.Walk(cctor, types) : null;
            if (initializer is { SideEffect: not null } or { Unfollowable: not null })
            {
                result.Excluded[$"{prefix}:{type.Name}..cctor"] = initializer.SideEffect ?? "cannot follow: " + initializer.Unfollowable;
                continue;
            }
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (field.GetValue(null) is { } value && SideEffectRule.IsSharpyAssembly(value.GetType().Assembly))
                    pool.Add(value);
            }
            foreach (var (method, body) in Members(type))
            {
                var key = $"{prefix}:{type.Name}.{Display(method)}";
                result.Enumerated.Add(key);
                var reach = SideEffectRule.Walk(body, types);
                if (reach.SideEffect is not null || reach.Unfollowable is not null)
                {
                    result.Excluded[key] = reach.SideEffect ?? "cannot follow: " + reach.Unfollowable;
                    continue;
                }
                if (reach.ReadsClock is not null)
                    result.ClockReaders[key] = reach.ReadsClock;
                if (method is MethodInfo { IsGenericMethodDefinition: true })
                {
                    result.Unswept[key] = "generic method: no type argument generator";
                    continue;
                }
                if (method.GetParameters().Any(p => p.ParameterType.IsByRef || p.ParameterType.IsPointer))
                {
                    result.Unswept[key] = "ref, out or pointer parameter";
                    continue;
                }
                members.Add(new SweptMember(key, method, type, reach.ReadsClock is not null));
            }
        }

        var reached = new HashSet<string>(StringComparer.Ordinal);
        for (int round = 0; round < Rounds; round++)
        {
            var before = types.Sum(pool.Count);
            foreach (var member in members)
            {
                var candidates = Candidates(member, pool, nullability, out var missing);
                if (candidates is null)
                {
                    result.Unswept[member.Key] = missing!;
                    continue;
                }
                result.Unswept.Remove(member.Key);
                reached.Add(member.Key);
                foreach (var args in Combinations(member, candidates, pool, result))
                    Invoke(member, args, pool, result);
            }
            if (types.Sum(pool.Count) == before && round > 0)
                break;
        }

        foreach (var key in reached)
            result.Swept.Add(key);
        result.Elapsed = clock.Elapsed;
        return result;
    }

    /// <summary>
    /// Public constructors, public declared methods (accessors and operators included) and the methods of Sharpy
    /// interfaces and <see cref="IFormattable"/> the type implements explicitly: the method invoked, and the body
    /// the side-effect rule walks (for an explicit implementation, the implementing method).
    /// </summary>
    private static IEnumerable<(MethodBase Invoke, MethodBase Body)> Members(Type type)
    {
        if (!type.IsAbstract && !type.IsInterface)
        {
            foreach (var ctor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
                yield return (ctor, ctor);
        }
        if (type.IsInterface)
            yield break;
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                     .Where(m => !m.IsAbstract)
                     .OrderBy(m => m.ToString(), StringComparer.Ordinal))
            yield return (method, method);
        foreach (var iface in type.GetInterfaces().OrderBy(i => i.FullName, StringComparer.Ordinal))
        {
            if (!(SideEffectRule.IsSharpyAssembly(iface.Assembly) || iface == typeof(IFormattable)))
                continue;
            var map = type.GetInterfaceMap(iface);
            for (int i = 0; i < map.TargetMethods.Length; i++)
            {
                if (!map.TargetMethods[i].IsPublic && map.TargetMethods[i].DeclaringType == type)
                    yield return (map.InterfaceMethods[i], map.TargetMethods[i]);
            }
        }
    }

    private static string Display(MethodBase method)
    {
        var name = method.DeclaringType is { IsInterface: true } iface ? iface.Name + "." + method.Name : method.Name;
        return name + "(" + string.Join(",", method.GetParameters().Select(p => TypeName(p.ParameterType))) + ")";
    }

    private static string TypeName(Type t)
        => Nullable.GetUnderlyingType(t) is { } u ? TypeName(u) + "?"
            : t.IsGenericType ? t.Name[..t.Name.IndexOf('`')] + "<" + string.Join(",", t.GetGenericArguments().Select(TypeName)) + ">"
            : t.Name;

    /// <summary>The candidates per argument position (position 0 is the receiver of an instance method), or null with the reason.</summary>
    private static IReadOnlyList<object?>[]? Candidates(SweptMember member, InstancePool pool, NullabilityInfoContext nullability, out string? missing)
    {
        missing = null;
        var lists = new SCG.List<IReadOnlyList<object?>>();
        if (!member.Method.IsStatic && member.Method is not ConstructorInfo)
        {
            var receivers = pool.For(member.DeclaringType);
            if (receivers.Count == 0)
            {
                missing = $"no instance of {member.DeclaringType.Name} was produced to call it on";
                return null;
            }
            lists.Add(receivers);
        }
        foreach (var p in member.Method.GetParameters())
        {
            var values = BoundaryArguments.For(p.ParameterType, BoundaryArguments.AdmitsNull(p, nullability), pool);
            if (values is null || values.Count == 0)
            {
                missing = values is null
                    ? $"no generator for parameter `{p.Name}` of type {TypeName(p.ParameterType)}"
                    : $"no instance of {TypeName(p.ParameterType)} for parameter `{p.Name}`";
                return null;
            }
            lists.Add(values);
        }
        return lists.ToArray();
    }

    /// <summary>
    /// Every combination when their number is at most <see cref="CartesianCap"/>; otherwise one-factor-at-a-time from
    /// the nominal tuple, then the two corners where every position takes its least and its greatest accepted value
    /// (accepted: the one-factor call did not throw).
    /// </summary>
    private static IEnumerable<object?[]> Combinations(SweptMember member, IReadOnlyList<object?>[] candidates, InstancePool pool, SweepResult result)
    {
        double product = 1;
        foreach (var c in candidates)
            product *= c.Count;
        if (product <= CartesianCap)
        {
            var index = new int[candidates.Length];
            while (true)
            {
                yield return index.Select((k, i) => candidates[i][k]).ToArray();
                int pos = candidates.Length - 1;
                while (pos >= 0 && ++index[pos] == candidates[pos].Count)
                    index[pos--] = 0;
                if (pos < 0)
                    yield break;
            }
        }

        var accepted = candidates.Select(_ => new SCG.List<object?>()).ToArray();
        var nominal = candidates.Select(c => c[0]).ToArray();
        for (int i = 0; i < candidates.Length; i++)
        {
            foreach (var value in candidates[i])
            {
                var args = (object?[])nominal.Clone();
                args[i] = value;
                if (Invoke(member, args, pool, result))
                    accepted[i].Add(value);
            }
        }
        double tried = candidates.Sum(c => c.Count) + 2;
        result.Untried[member.Key] = Math.Max(result.Untried.GetValueOrDefault(member.Key), product - tried);
        yield return accepted.Select((a, i) => Extreme(a, nominal[i], least: true)).ToArray();
        yield return accepted.Select((a, i) => Extreme(a, nominal[i], least: false)).ToArray();
    }

    private static object? Extreme(SCG.List<object?> accepted, object? nominal, bool least)
    {
        var numeric = accepted.Where(v => v is int or long or double or float).ToList();
        if (numeric.Count == 0)
            return nominal;
        var ordered = numeric.OrderBy(v => Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture)).ToList();
        return least ? ordered[0] : ordered[^1];
    }

    /// <summary>Call <paramref name="member"/>; true when it returned. A returned Sharpy object joins the pool.</summary>
    private static bool Invoke(SweptMember member, object?[] args, InstancePool pool, SweepResult result)
    {
        result.Invocations++;
        object? value;
        try
        {
            if (member.Method is ConstructorInfo ctor)
            {
                value = ctor.Invoke(args);
            }
            else if (member.Method.IsStatic)
            {
                value = member.Method.Invoke(null, args);
            }
            else
            {
                value = member.Method.Invoke(args[0], args.Skip(1).ToArray());
            }
        }
        catch (TargetInvocationException tie) when (tie.InnerException is { } inner)
        {
            Record(member.Key, inner, member, args, result);
            return false;
        }
        catch (Exception e) when (e is ArgumentException or TargetParameterCountException or MemberAccessException or TargetException)
        {
            result.HarnessErrors.Add($"{member.Key}: the harness passed bad arguments {Render(member, args)}: {e.Message}");
            return false;
        }

        if (value is System.Collections.IEnumerable sequence and not string)
        {
            try
            {
                int n = 0;
                foreach (var _ in sequence)
                {
                    if (++n >= IterationCap)
                        break;
                }
            }
            catch (Exception e)
            {
                Record(member.Key + "#iterate", e, member, args, result);
                return false;
            }
        }

        if (!member.ReadsClock && value is not null && SideEffectRule.IsSharpyAssembly(value.GetType().Assembly) && value is not Exception)
            pool.Add(value);
        return true;
    }

    private static void Record(string memberKey, Exception escaped, SweptMember member, object?[] args, SweepResult result)
    {
        var type = escaped.GetType();
        if (AcceptedExceptions.IsPythonException(type))
        {
            result.Accepted.Add(type.Name);
            return;
        }
        var key = memberKey + ":" + type.FullName;
        if (!result.Leaks.ContainsKey(key))
            result.Leaks[key] = new Leak(key, Render(member, args) + " -> " + escaped.Message);
    }

    private static string Render(SweptMember member, object?[] args)
        => member.Method.Name + "(" + string.Join(", ", args.Select(RenderValue)) + ")";

    private static string RenderValue(object? v)
    {
        string text;
        try
        {
            text = v switch
            {
                null => "null",
                string s => "\"" + (s.Length > 24 ? s[..24] + $"...({s.Length} chars)" : s) + "\"",
                _ => v.ToString() ?? v.GetType().Name,
            };
        }
        catch (Exception)
        {
            text = "<" + v!.GetType().Name + ">";
        }
        return text;
    }
}

/// <summary>
/// The exceptions a Sharpy program can name in <c>except</c>, derived by reflection, never a hand list: every public
/// exception type of the Core assembly and its subclasses (a Stdlib exception deriving from <c>ValueError</c>, ...),
/// every exception type a Stdlib module declares (<c>[SharpyModuleType]</c>), and <see cref="System.Exception"/>
/// itself, which is Sharpy's <c>Exception</c> (exception_handling.md, "Exception Type Hierarchy").
/// </summary>
internal static class AcceptedExceptions
{
    public static readonly IReadOnlyList<Type> CoreTypes =
        typeof(KeyError).Assembly.GetExportedTypes()
            .Where(t => typeof(Exception).IsAssignableFrom(t) && t != typeof(Exception))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToArray();

    public static bool IsPythonException(Type type)
    {
        if (type == typeof(Exception))
            return true;
        if (CoreTypes.Any(core => core.IsAssignableFrom(type)))
            return true;
        return SideEffectRule.IsSharpyAssembly(type.Assembly) && type.GetCustomAttribute<SharpyModuleTypeAttribute>() is not null;
    }
}
