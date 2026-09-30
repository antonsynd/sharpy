using System.Reflection;
using Xunit;

namespace Sharpy.Stdlib.Tests.Conformance;

/// <summary>
/// The optional-parameter spelling sweep (#2054). Contract (CLAUDE.md › Core &amp; Stdlib
/// Conventions): an optional parameter on the public Sharpy.Core / Sharpy.Stdlib surface is
/// nullable with a null default — <c>T? x = null</c> — never a non-nullable parameter defaulted
/// with <c>default!</c>, never <c>Optional&lt;T&gt;</c>, and never an unconstrained type parameter.
///
/// <para>
/// Three rules, read from metadata by reflection over every exported type of both assemblies:
/// (1) a parameter whose default is null (what <c>default!</c> compiles to for a reference type
/// or type parameter) while its nullability annotation says NOT NULL — the <c>!</c> silenced
/// exactly the warning that names the lie; (2) an optional parameter typed <c>Optional&lt;T&gt;</c>;
/// (3) an optional parameter typed by a type parameter that is not constrained to a reference or
/// a value type. Rule 3 exists because <c>V? x = default</c> on an unconstrained V looks nullable
/// but is not: for a value-type V it is plain V defaulting to 0/False, so "absent" and "0" are the
/// same argument — <c>OrderedDict[str, int]().get("x")</c> returned 0 where python returns None.
/// The cure is <c>Dict&lt;K, V&gt;</c>'s shape: a valueless overload returning <c>Optional&lt;V&gt;</c>
/// plus an overload whose V is required. The cells this closed: <c>Dict.Fromkeys</c> and
/// <c>FrozenDict.Get</c> (Core), <c>OrderedDict.Get</c>, <c>ChainMap.Get</c>, <c>DefaultDict.Get</c>,
/// and <c>csv.dict_reader</c>/<c>CsvDictReader(fieldnames: Optional[list[str]])</c>.
/// </para>
///
/// <para>
/// The positive control runs the same scan over synthetic violators declared in this file (one
/// cell per rule and per declaration shape) and asserts each is flagged, and over compliant twins
/// asserting none is — so an empty result on the real assemblies cannot be a scan that sees nothing.
/// The exemption roster is empty; a row needs an issue and a reason, and drains on fix.
/// </para>
/// </summary>
public class OptionalParameterSpellingSweepTests
{
    private static readonly Assembly[] SweptAssemblies =
    {
        typeof(ISized).Assembly,     // Sharpy.Core
        typeof(Deque<>).Assembly,    // Sharpy.Stdlib
    };

    /// <summary>Exempt members ("Type.Method(param)") → the issue that tracks them. Empty.</summary>
    private static readonly Dictionary<string, string> Exemptions = new();

    internal static System.Collections.Generic.List<string> Violations(IEnumerable<Type> types)
    {
        var violations = new System.Collections.Generic.List<string>();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var type in types)
        {
            var members = type.GetMethods(flags).Cast<MethodBase>().Concat(type.GetConstructors(flags));
            foreach (var member in members)
            {
                foreach (var parameter in member.GetParameters())
                {
                    var key = $"{type.FullName}.{member.Name}({parameter.Name})";
                    if (Exemptions.ContainsKey(key))
                    {
                        continue;
                    }

                    if (parameter.IsOptional && IsOptionalOfT(parameter.ParameterType))
                    {
                        violations.Add($"{key}: optional parameter typed Optional<T> — spell it `T? = null`");
                        continue;
                    }

                    if (parameter.IsOptional && IsUnconstrainedTypeParameter(parameter.ParameterType))
                    {
                        violations.Add($"{key}: optional parameter typed by unconstrained type parameter {parameter.ParameterType.Name} — `{parameter.ParameterType.Name}?` is not nullable for a value type; split into a valueless overload (Optional<T> return, as Dict.Get) and one with the value required");
                        continue;
                    }

                    if (HasNullDefault(parameter) && CanHoldNull(parameter.ParameterType)
                        && TopLevelNullableFlag(parameter) == NotAnnotated)
                    {
                        violations.Add($"{key}: non-nullable parameter defaulted to null (`default!`) — spell it `T? = null`");
                    }
                }
            }
        }

        return violations;
    }

    private const byte NotAnnotated = 1;

    /// <summary>
    /// The compiler's own nullable annotation for the parameter's top-level type: the first byte of
    /// its <c>[Nullable]</c>, else the nearest <c>[NullableContext]</c> on the member or an
    /// enclosing type (0 oblivious, 1 not annotated, 2 annotated). Read raw rather than through
    /// <see cref="NullabilityInfoContext"/>, which reports an unannotated unconstrained <c>T</c> as
    /// nullable — the positive control's <c>T value = default!</c> cell went unflagged through it.
    /// </summary>
    private static byte TopLevelNullableFlag(ParameterInfo parameter)
    {
        var own = parameter.GetCustomAttributesData()
            .FirstOrDefault(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute");
        if (own != null)
        {
            var arg = own.ConstructorArguments[0];
            if (arg.Value is byte single)
            {
                return single;
            }

            if (arg.Value is System.Collections.ObjectModel.ReadOnlyCollection<CustomAttributeTypedArgument> bytes && bytes.Count > 0)
            {
                return (byte)bytes[0].Value!;
            }
        }

        for (MemberInfo? scope = parameter.Member; scope != null; scope = scope.DeclaringType)
        {
            var context = scope.GetCustomAttributesData()
                .FirstOrDefault(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.NullableContextAttribute");
            if (context != null)
            {
                return (byte)context.ConstructorArguments[0].Value!;
            }
        }

        return 0;
    }

    // A type parameter the compiler cannot make nullable uniformly: no `class` constraint, no
    // `struct` constraint, and no class-type constraint (which would imply a reference type).
    private static bool IsUnconstrainedTypeParameter(Type type)
    {
        if (!type.IsGenericParameter)
        {
            return false;
        }

        var attributes = type.GenericParameterAttributes;
        if ((attributes & (GenericParameterAttributes.ReferenceTypeConstraint | GenericParameterAttributes.NotNullableValueTypeConstraint)) != 0)
        {
            return false;
        }

        return !type.GetGenericParameterConstraints().Any(c => c.IsClass && c != typeof(object) && c != typeof(ValueType));
    }

    private static bool IsOptionalOfT(Type type)
        => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Optional<>);

    private static bool HasNullDefault(ParameterInfo parameter)
    {
        if (!parameter.HasDefaultValue)
        {
            return false;
        }

        return parameter.DefaultValue is null;
    }

    // A reference type, or a type parameter not constrained to a struct — the types `default!`
    // turns into a null the annotation denies. A value type's default is a real value (`int x = 0`).
    private static bool CanHoldNull(Type type)
    {
        if (type.IsByRef)
        {
            type = type.GetElementType()!;
        }

        if (type.IsGenericParameter)
        {
            return (type.GenericParameterAttributes & GenericParameterAttributes.NotNullableValueTypeConstraint) == 0;
        }

        return !type.IsValueType;
    }

    [Fact]
    public void PublicSurface_OptionalParameters_AreNullable_NeverDefaultBangOrOptional()
    {
        var types = SweptAssemblies.SelectMany(a => a.GetExportedTypes()).ToList();
        Assert.True(types.Count > 100, $"sweep saw only {types.Count} exported types");

        var violations = Violations(types);
        Assert.True(violations.Count == 0,
            "optional parameters must be nullable `= null` (#2054):\n  " + string.Join("\n  ", violations));
    }

    [Fact]
    public void Exemptions_AreEmpty()
    {
        Assert.Empty(Exemptions);
    }

    // ── Positive control: synthetic declarations, one per rule × shape. ──

    public static class Violators
    {
        // rule 1: a non-nullable reference slot defaulted to null
        public static void ReferenceDefaultBang(string s = default!) { }

        public static void ClassConstrainedDefaultBang<T>(T value = default!) where T : class { }

        // rule 2: Optional<T> as an optional parameter
        public static void OptionalParameter(Optional<int> o = default) { }

        public static void OptionalReferenceParameter(Optional<string> o = default) { }

        // rule 3: an unconstrained type parameter as an optional parameter, annotated or not
        public static void GenericDefaultBang<T>(T value = default!) { }

        public static void UnconstrainedNullableGeneric<T>(T? value = default) { }
    }

    public sealed class GenericViolator<TValue>
    {
        public GenericViolator(TValue seed = default!) { }

        // The #2054 cell exactly: OrderedDict.Get(K key, V? @default = default).
        public TValue? Get(string key, TValue? fallback = default) => fallback;
    }

    public static class Compliant
    {
        public static void NullableReference(string? s = null) { }

        public static void ClassConstrainedNullable<T>(T? value = null) where T : class { }

        public static void StructConstrainedNullable<T>(T? value = null) where T : struct { }

        public static void ValueDefault(int start = 0, bool flag = false) { }

        public static void NullableValue(int? stop = null) { }

        public static void Required(string s, Optional<int> o) { }
    }

    // Dict<K, V>.Get's shape: a valueless overload and a required-value overload.
    public sealed class GenericCompliant<TValue>
    {
        public Optional<TValue> Get(string key) => Optional<TValue>.None;

        public TValue Get(string key, TValue fallback) => fallback;
    }

    [Fact]
    public void Control_EveryViolatorShape_IsFlagged()
    {
        var flagged = Violations(new[] { typeof(Violators), typeof(GenericViolator<>) });
        Assert.True(flagged.Count == 8, "expected 8 flagged, got:\n" + string.Join("\n", flagged));
        Assert.Contains(flagged, v => v.Contains("ReferenceDefaultBang(s)") && v.Contains("default!"));
        Assert.Contains(flagged, v => v.Contains("ClassConstrainedDefaultBang(value)") && v.Contains("default!"));
        Assert.Contains(flagged, v => v.Contains("OptionalParameter(o)") && v.Contains("Optional<T>"));
        Assert.Contains(flagged, v => v.Contains("OptionalReferenceParameter(o)") && v.Contains("Optional<T>"));
        Assert.Contains(flagged, v => v.Contains("GenericDefaultBang(value)") && v.Contains("unconstrained"));
        Assert.Contains(flagged, v => v.Contains("UnconstrainedNullableGeneric(value)") && v.Contains("unconstrained"));
        Assert.Contains(flagged, v => v.Contains(".ctor(seed)") && v.Contains("unconstrained"));
        Assert.Contains(flagged, v => v.Contains("Get(fallback)") && v.Contains("unconstrained"));
    }

    [Fact]
    public void Control_CompliantShapes_AreNotFlagged()
    {
        var flagged = Violations(new[] { typeof(Compliant), typeof(GenericCompliant<>) });
        Assert.True(flagged.Count == 0, string.Join("\n", flagged));
    }
}
