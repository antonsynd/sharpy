using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// Shared CLR type inspection helpers used by semantic analysis, code generation,
/// and validators.
/// </summary>
internal static class ClrTypeHelper
{
    private static readonly ConcurrentDictionary<Type, bool> _paramsIndexerCache = new();

    // Caches (CLR type, Sharpy member name) -> original CLR method name.
    private static readonly ConcurrentDictionary<(Type, string), string?> _clrMethodNameCache = new();

    // Caches (CLR type, Sharpy member name) -> original CLR property name.
    private static readonly ConcurrentDictionary<(Type, string), string?> _clrPropertyNameCache = new();

    // Caches (CLR type, CLR generic definition full name) -> that definition within the type's
    // supertype closure, for provenance-scoped assignability (#1260).
    private static readonly ConcurrentDictionary<(Type, string), Type?> _originDefinitionCache = new();

    /// <summary>
    /// Resolves the original CLR method name on <paramref name="clrType"/> whose reverse-mangled
    /// Sharpy form equals <paramref name="memberName"/> (e.g. <c>is_os_platform</c> ->
    /// <c>IsOSPlatform</c>), preserving acronym casing for directly-imported .NET types whose
    /// methods are not eagerly discovered (#705). Returns <c>null</c> when there is no unambiguous
    /// match. Lives here (not in the emitter) so code generation performs no reflection (#974); the
    /// TypeChecker materializes the result into <c>SemanticInfo</c> for the emitter to read.
    /// </summary>
    internal static string? ResolveClrMethodName(Type clrType, string memberName)
    {
        if (_clrMethodNameCache.TryGetValue((clrType, memberName), out var cached))
            return cached;

        string? resolved = null;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        foreach (var method in clrType.GetMethods(flags))
        {
            // A CLR name already written verbatim (PascalCase) should be left untouched;
            // only match when the Sharpy (reverse-mangled) form equals the written name.
            if (NameMangler.ToSharpyName(method.Name, ReverseNameContext.Method) == memberName)
            {
                if (resolved != null && resolved != method.Name)
                {
                    // Ambiguous (multiple distinct CLR names map to this Sharpy name) — bail out.
                    resolved = null;
                    break;
                }
                resolved = method.Name;
            }
        }

        _clrMethodNameCache[(clrType, memberName)] = resolved;
        return resolved;
    }

    // Caches (CLR type, Sharpy member name) -> matching generic instance method definitions.
    private static readonly ConcurrentDictionary<(Type, string), ImmutableArray<MethodInfo>> _genericInstanceMethodCache = new();

    /// <summary>
    /// Returns the public instance generic-method <em>definitions</em> on <paramref name="clrType"/>
    /// whose reverse-mangled Sharpy name equals <paramref name="memberName"/> (same matching rule as
    /// <see cref="ResolveClrMethodName"/>). Used by the TypeChecker to resolve an explicit-type-argument
    /// reference <c>recv.method[T]</c> on a raw BCL type whose members were never eagerly discovered into
    /// its <see cref="TypeSymbol"/> (#1136). Reflection lives here (Discovery), never in the emitter
    /// (#974); the caller materializes the pick into a <see cref="FunctionSymbol"/> the emitter reads.
    /// Only generic-method <em>definitions</em> are returned (<c>IsGenericMethodDefinition</c>), since the
    /// reference supplies its own explicit type arguments; empty when none match.
    /// </summary>
    internal static ImmutableArray<MethodInfo> ResolveGenericInstanceMethods(Type clrType, string memberName)
    {
        if (_genericInstanceMethodCache.TryGetValue((clrType, memberName), out var cached))
            return cached;

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        var matches = clrType.GetMethods(flags)
            .Where(m => m.IsGenericMethodDefinition
                && NameMangler.ToSharpyName(m.Name, ReverseNameContext.Method) == memberName)
            .ToImmutableArray();

        _genericInstanceMethodCache[(clrType, memberName)] = matches;
        return matches;
    }

    // Caches CLR type -> every name a member reference on it could legitimately denote: each public
    // member's verbatim CLR name plus its reverse-mangled Sharpy form. A null value memoizes a
    // reflection failure (inconclusive), which callers must treat as "cannot prove absence".
    private static readonly ConcurrentDictionary<Type, FrozenSet<string>?> _memberNameSurfaceCache = new();

    /// <summary>
    /// Returns every name a Sharpy member reference on <paramref name="clrType"/> could denote — each
    /// public member's verbatim CLR name and its reverse-mangled Sharpy form (<c>ConvertAll</c> and
    /// <c>convert_all</c>), across methods, properties, fields, events, and nested types, including
    /// inherited and interface members. Returns <c>null</c> when reflection is inconclusive (type-load
    /// failure), which the caller must treat as "member might exist".
    ///
    /// <para>
    /// This is the affirmative half of the #1141 absence proof: a member name absent from this surface
    /// (and from the extension-method surface, see <see cref="GetExtensionMethodNames"/>) provably does
    /// not exist on the receiver, so the TypeChecker may reject it instead of letting the name-only
    /// interop channel emit it and leak a CS1061. Reflection lives here (Discovery), never in the
    /// emitter (#974). The set doubles as the candidate pool for a "did you mean" suggestion.
    /// </para>
    /// </summary>
    internal static IReadOnlySet<string>? GetMemberNameSurface(Type clrType)
    {
        return _memberNameSurfaceCache.GetOrAdd(clrType, static t =>
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                CollectMemberNames(t, names);
                // A receiver typed as an interface sees its inherited interface members, which
                // GetMembers does not flatten; for class receivers this adds nothing new.
                foreach (var iface in t.GetInterfaces())
                    CollectMemberNames(iface, names);
            }
            catch (Exception ex) when (ex is ReflectionTypeLoadException or TypeLoadException
                                          or FileNotFoundException or NotSupportedException)
            {
                return null;
            }

            return names.ToFrozenSet(StringComparer.Ordinal);
        });
    }

    private static void CollectMemberNames(Type type, HashSet<string> names)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance
            | BindingFlags.Static | BindingFlags.FlattenHierarchy;
        foreach (var member in type.GetMembers(flags))
        {
            names.Add(member.Name);
            var context = member is MethodBase ? ReverseNameContext.Method : ReverseNameContext.Property;
            names.Add(NameMangler.ToSharpyName(member.Name, context));
        }
    }

    // Caches assembly -> the verbatim and reverse-mangled names of every extension method it declares.
    private static readonly ConcurrentDictionary<Assembly, FrozenSet<string>> _extensionMethodNameCache = new();

    /// <summary>
    /// Returns the verbatim and reverse-mangled names of every extension method declared in
    /// <paramref name="assembly"/>. The receiver-compatibility question is deliberately not asked:
    /// this is the permissive half of the #1141 absence proof — a name that any reachable extension
    /// method could supply is never claimed absent, so the name-only interop channel keeps its
    /// permissiveness (Anton's rule, #1141) and only names nothing can supply are rejected. Assemblies
    /// that cannot be inspected contribute no names.
    /// </summary>
    internal static IReadOnlySet<string> GetExtensionMethodNames(Assembly assembly)
    {
        return _extensionMethodNameCache.GetOrAdd(assembly, static asm =>
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            Type[] types;
            try
            {
                types = asm.GetExportedTypes();
            }
            catch (Exception ex) when (ex is ReflectionTypeLoadException or TypeLoadException
                                          or FileNotFoundException or NotSupportedException)
            {
                return names.ToFrozenSet(StringComparer.Ordinal);
            }

            foreach (var type in types)
            {
                // Extension methods live only in non-generic static classes, which the compiler marks
                // with [Extension] at the class level — a cheap filter before enumerating methods.
                if (!type.IsSealed || !type.IsAbstract || type.IsGenericTypeDefinition)
                    continue;
                if (!type.IsDefined(typeof(ExtensionAttribute), inherit: false))
                    continue;

                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (!method.IsDefined(typeof(ExtensionAttribute), inherit: false))
                        continue;
                    names.Add(method.Name);
                    names.Add(NameMangler.ToSharpyName(method.Name, ReverseNameContext.Method));
                }
            }

            return names.ToFrozenSet(StringComparer.Ordinal);
        });
    }

    /// <summary>
    /// Resolves the original CLR property name on <paramref name="clrType"/> whose reverse-mangled
    /// Sharpy form equals <paramref name="memberName"/>, mirroring <see cref="ResolveClrMethodName"/>
    /// for the property/field emission path. This lets a verbatim (backtick-escaped) spy-stdlib
    /// property such as socket's lowercase <c>type</c> survive to code generation instead of being
    /// forward-mangled to <c>Type</c> (CS1061, #1093). Indexers (which are never reached by member
    /// name) are skipped. Returns <c>null</c> when there is no unambiguous match. Reflection lives
    /// here (Discovery), never in the emitter (#974); the TypeChecker materializes the result into
    /// <c>SemanticInfo</c> for the emitter to read.
    /// </summary>
    internal static string? ResolveClrPropertyName(Type clrType, string memberName)
    {
        if (_clrPropertyNameCache.TryGetValue((clrType, memberName), out var cached))
            return cached;

        string? resolved = null;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance;
        foreach (var prop in clrType.GetProperties(flags))
        {
            // Indexers carry index parameters and are never accessed by member name.
            if (prop.GetIndexParameters().Length > 0)
                continue;

            // A CLR name already written verbatim should be left untouched; only match when the
            // Sharpy (reverse-mangled) form equals the written name.
            if (NameMangler.ToSharpyName(prop.Name, ReverseNameContext.Property) == memberName)
            {
                if (resolved != null && resolved != prop.Name)
                {
                    // Ambiguous (multiple distinct CLR names map to this Sharpy name) — bail out.
                    resolved = null;
                    break;
                }
                resolved = prop.Name;
            }
        }

        _clrPropertyNameCache[(clrType, memberName)] = resolved;
        return resolved;
    }

    /// <summary>
    /// Returns true when the CLR type backing <paramref name="type"/> exposes an indexer whose last
    /// parameter is a C# <c>params</c> array (e.g. numpy's <c>NdArray</c>), so a tuple index
    /// <c>a[1, 2]</c> can be spread into separate element-access arguments (#956). Used by the
    /// TypeChecker to materialize <c>IndexAccessLowering.ParamsSpread</c>; the emitter never reflects.
    /// </summary>
    internal static bool HasParamsIndexer(SemanticType? type)
    {
        var clrType = type switch
        {
            UserDefinedType udt => udt.Symbol?.ClrType,
            BuiltinType bt => bt.ClrType,
            GenericType gt => TryConstructClosedGeneric(gt, t => t.ClrType ?? typeof(object)),
            _ => null
        };

        if (clrType == null)
            return false;

        if (_paramsIndexerCache.TryGetValue(clrType, out var cached))
            return cached;

        var result = false;
        foreach (var prop in clrType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var indexParams = prop.GetIndexParameters();
            if (indexParams.Length > 0 &&
                indexParams[indexParams.Length - 1].GetCustomAttribute<ParamArrayAttribute>() != null)
            {
                result = true;
                break;
            }
        }

        _paramsIndexerCache.TryAdd(clrType, result);
        return result;
    }

    // Caches (CLR type, Sharpy member name, arity) -> whether an overridable member matches.
    private static readonly ConcurrentDictionary<(Type, string, int), bool> _overridableMemberCache = new();

    /// <summary>
    /// Returns true when <paramref name="clrType"/> exposes an abstract or virtual (non-sealed)
    /// instance method whose reverse-mangled Sharpy name equals <paramref name="sharpyMethodName"/>
    /// and whose parameter count equals <paramref name="arity"/> (#1122). Used by the TypeChecker to
    /// decide whether a <c>.spy</c> method overrides a CLR-base member for directly-imported base
    /// classes (e.g. <c>SourceGenerator</c>) whose members are not eagerly discovered into the
    /// bridged <see cref="TypeSymbol"/>. Reflection lives here (Discovery), never in the emitter
    /// (#974); the decision is materialized onto <c>CodeGenInfo</c> for code generation to read.
    /// Members declared on <see cref="object"/> are excluded — those (ToString/Equals/GetHashCode)
    /// are handled by the dunder-override path.
    /// </summary>
    internal static bool HasOverridableClrMember(Type clrType, string sharpyMethodName, int arity)
    {
        var key = (clrType, sharpyMethodName, arity);
        if (_overridableMemberCache.TryGetValue(key, out var cached))
            return cached;

        var result = false;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        foreach (var method in clrType.GetMethods(flags))
        {
            // object.ToString/Equals/GetHashCode are handled by the dunder-override path.
            if (method.DeclaringType == typeof(object))
                continue;
            // Overridable only when abstract or virtual (IsVirtual covers both) and not sealed.
            if (!method.IsVirtual || method.IsFinal)
                continue;
            if (method.GetParameters().Length != arity)
                continue;
            if (NameMangler.ToSharpyName(method.Name, ReverseNameContext.Method) == sharpyMethodName)
            {
                result = true;
                break;
            }
        }

        _overridableMemberCache[key] = result;
        return result;
    }

    // Caches a stored CLR type name -> the resolved CLR type, so every channel that records a
    // CLR identity as text scans loaded assemblies at most once per name. Null is cached too:
    // an unresolvable name stays unresolvable.
    private static readonly ConcurrentDictionary<string, Type?> _originNameDefinitionCache = new();

    /// <summary>
    /// Resolves a CLR type by the FullName a cache channel recorded — <c>Type.GetType</c> first,
    /// then a loaded-assembly scan, null-cached per name. The SHARED origin resolver for every
    /// serializer channel that stores a CLR identity as text (the #1538 builtin channel, the
    /// #1534 UnmappedClrType channel) and for <see cref="ResolveOriginDefinition"/>'s
    /// generic-definition lookup, which layers its arity gates on top.
    /// </summary>
    internal static Type? ResolveClrTypeByStoredName(string fullName)
        => _originNameDefinitionCache.GetOrAdd(fullName, static name =>
        {
            var direct = Type.GetType(name);
            if (direct != null)
                return direct;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var found = assembly.GetType(name);
                if (found != null)
                    return found;
            }
            return null;
        });

    // Caches a bare Sharpy display name + arity -> the resolved CLR generic definition (or null),
    // so the last-resort scan below runs at most once per spelling instead of on every negative
    // protocol probe.
    private static readonly ConcurrentDictionary<string, Type?> _displayNameDefinitionCache = new();

    /// <summary>
    /// Last-resort resolution of a CLR generic definition from a bare Sharpy DISPLAY name (no
    /// <see cref="GenericType.ClrOriginTypeName"/>, no symbol) — the shape a return type takes
    /// after losing its identity through the overload-index → builtin-substitution chain (#1517).
    /// Probes System.Collections.Generic directly, then scans loaded assemblies for an arity
    /// match on the StripArity'd simple name. Cached per name+arity, nulls included.
    /// </summary>
    internal static Type? ResolveGenericDefinitionByDisplayName(string displayName, int arity)
        => _displayNameDefinitionCache.GetOrAdd($"{displayName}`{arity}", _ =>
        {
            var candidateName = displayName.Contains('`', StringComparison.Ordinal)
                ? displayName
                : displayName + "`" + arity;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var found = assembly.GetType("System.Collections.Generic." + candidateName)
                        ?? assembly.GetTypes().FirstOrDefault(t =>
                            t.IsGenericTypeDefinition
                            && Shared.ClrNameHelper.StripArity(t.Name) == displayName
                            && t.GetGenericArguments().Length == arity);
                    if (found != null)
                        return found;
                }
                catch (ReflectionTypeLoadException)
                {
                    // A partially-loadable assembly cannot veto the scan — skip it.
                }
            }
            return null;
        });

    internal static Type? TryConstructClosedGeneric(GenericType generic, Func<SemanticType, Type?> resolveClrType)
    {
        // Provenance has two channels and a GenericType may carry either one. TypeResolver's
        // ANNOTATION path sets GenericDefinition from the resolved symbol; the discovery path that
        // types an EXPRESSION (a discovered method's return type, e.g. `d.keys()`) sets
        // ClrOriginTypeName instead, because Sharpy.Core's own types are not in the module-symbol
        // table it looks GenericDefinition up in. Reading only the first channel meant the same
        // runtime type resolved to a CLR type when it was spelled in an annotation and to nothing
        // when it came from an expression — so CLR operator resolution accepted `x | y` and refused
        // `d.keys() | e.keys()` (#1496). Both channels answer the same question, so both are read
        // here, at the single funnel every CLR operator/member resolution already goes through.
        var openDef = generic.GenericDefinition?.ClrType ?? ResolveOriginDefinition(generic);
        if (openDef == null || !openDef.IsGenericTypeDefinition)
            return openDef;

        var clrArgs = new Type[generic.TypeArguments.Count];
        for (int i = 0; i < generic.TypeArguments.Count; i++)
        {
            var arg = resolveClrType(generic.TypeArguments[i]);
            if (arg == null)
                return openDef;
            clrArgs[i] = arg;
        }

        try
        {
            return openDef.MakeGenericType(clrArgs);
        }
        catch (ArgumentException)
        {
            return openDef;
        }
    }

    /// <summary>
    /// The open generic definition a <see cref="GenericType"/> records in
    /// <see cref="GenericType.ClrOriginTypeName"/>, or null when it records none or the name no
    /// longer resolves. Arity is checked here so a name that resolves to a differently-shaped
    /// definition is rejected rather than blowing up inside <c>MakeGenericType</c>.
    /// </summary>
    internal static Type? ResolveOriginDefinition(GenericType generic)
    {
        if (generic.ClrOriginTypeName is not { Length: > 0 } originName)
            return null;

        var resolved = ResolveClrTypeByStoredName(originName);

        if (resolved is not { IsGenericTypeDefinition: true })
            return null;

        return resolved.GetGenericArguments().Length == generic.TypeArguments.Count
            ? resolved
            : null;
    }

    /// <summary>
    /// Whether <paramref name="sourceClr"/> satisfies a formal that the bridge mapped from a CLR
    /// generic — answered by .NET itself, so the accepted set is exactly the set of calls C# binds.
    ///
    /// <para>
    /// The question this exists to answer: a CLR <c>IEnumerable&lt;int&gt;</c> parameter is SPELLED
    /// <c>list[int]</c> after <see cref="ClrTypeBridge"/> maps it, and comparing that spelling against a
    /// CLR <c>List&lt;int&gt;</c> actual by semantic identity refuses a call .NET binds natively
    /// (#1260). Rather than teach assignability a table of CLR sequence relationships — a second,
    /// weaker copy of .NET's rules, and the #1145 hazard — the formal remembers the CLR definition it
    /// came from and reflection answers.
    /// </para>
    ///
    /// <para>
    /// The open definition is recovered from <paramref name="sourceClr"/>'s own supertype closure
    /// rather than resolved from the stored name, so no assembly loading or name lookup is involved and
    /// a provenance string that survived a symbol-cache round-trip needs nothing rehydrated. A source
    /// that does not implement the origin at all simply has no matching definition, which is the
    /// correct <c>false</c>.
    /// </para>
    ///
    /// <para>
    /// Returns false for a formal with no provenance — every type written in Sharpy source. That is the
    /// scope guard: a user's <c>def f(xs: list[int])</c> stays strict, so no hidden copy or aliasing
    /// surprise can be introduced by this rule.
    /// </para>
    /// </summary>
    /// <param name="formal">The mapped formal; only its provenance and type arguments are read.</param>
    /// <param name="sourceClr">The actual argument's resolved CLR type.</param>
    /// <param name="resolveClrType">
    /// The caller's <c>SemanticType</c> -> CLR resolution (the TypeChecker's <c>TryGetClrType</c>), so
    /// this helper performs reflection but no semantic resolution.
    /// </param>
    internal static bool ClrOriginIsSatisfiedBy(
        GenericType formal, Type sourceClr, Func<SemanticType, Type?> resolveClrType)
    {
        if (formal.ClrOriginTypeName is not { Length: > 0 } originName)
            return false;

        var openDefinition = FindGenericDefinitionInClosure(sourceClr, originName);
        if (openDefinition == null
            || openDefinition.GetGenericArguments().Length != formal.TypeArguments.Count)
        {
            return false;
        }

        var clrArgs = new Type[formal.TypeArguments.Count];
        for (int i = 0; i < clrArgs.Length; i++)
        {
            var arg = resolveClrType(formal.TypeArguments[i]);
            if (arg == null)
                return false;
            clrArgs[i] = arg;
        }

        try
        {
            // IsAssignableFrom, not argument-by-argument equality, so declared CLR variance is honored:
            // List<string> satisfies IEnumerable<object> because IEnumerable<out T> says it does.
            return openDefinition.MakeGenericType(clrArgs).IsAssignableFrom(sourceClr);
        }
        catch (ArgumentException)
        {
            // Constraint violation — the origin cannot be constructed at these arguments, so nothing
            // could bind it either.
            return false;
        }
    }

    /// <summary>
    /// The open generic definition named <paramref name="definitionFullName"/> within
    /// <paramref name="type"/>'s own supertype closure: the type itself, its base chain, and every
    /// interface it implements. Null when the type has no such supertype.
    ///
    /// <para>
    /// Cached because the caller sits on assignability's failure path, which the checker takes often;
    /// the closure of a CLR type never changes, so the answer is stable for the process.
    /// </para>
    /// </summary>
    private static Type? FindGenericDefinitionInClosure(Type type, string definitionFullName)
        => _originDefinitionCache.GetOrAdd(
            (type, definitionFullName),
            key => FindGenericDefinitionInClosureUncached(key.Item1, key.Item2));

    private static Type? FindGenericDefinitionInClosureUncached(Type type, string definitionFullName)
    {
        foreach (var supertype in SupertypeClosureOf(type))
        {
            if (supertype.IsGenericType
                && DefinitionIsOrigin(supertype.GetGenericTypeDefinition(), definitionFullName))
            {
                return supertype.GetGenericTypeDefinition();
            }
        }

        return null;
    }

    /// <summary>
    /// A type's own supertype closure: the type itself, its base chain, then every interface it
    /// implements (including inherited ones — <see cref="Type.GetInterfaces"/> flattens). This is the
    /// ONE enumeration of "what a CLR type can be seen as": <see cref="FindGenericDefinitionInClosure"/>
    /// walks it for assignability, and <c>GenericTypeInferenceService.TryUnifyViaClrReflection</c> walks
    /// it (over an open definition) to bind type arguments through a provenance-carrying formal. Keeping
    /// both consumers on this method is the #1145 discipline — a second hand-rolled walk is how the two
    /// answers drift apart.
    /// <para>The type itself is yielded FIRST because a formal can name the actual's own definition
    /// rather than one of its interfaces (a CLR parameter typed <c>List&lt;T&gt;</c> against a
    /// <c>List[int]</c> actual).</para>
    /// </summary>
    internal static IEnumerable<Type> SupertypeClosureOf(Type type)
    {
        for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            yield return current;

        foreach (var implemented in type.GetInterfaces())
            yield return implemented;
    }

    /// <summary>
    /// Whether <paramref name="definition"/> is the CLR definition a provenance stamp names. The rule —
    /// full-name equality on the OPEN definition — is deliberately the only spelling of this comparison
    /// (#1145): <see cref="FindGenericDefinitionInClosure"/> and the unification path both resolve a
    /// provenance string through here, so what satisfies one satisfies the other.
    /// </summary>
    internal static bool DefinitionIsOrigin(Type definition, string originFullName)
        => definition.FullName == originFullName;

    /// <summary>
    /// Gets the element type if the given type is <c>Sharpy.Iterator&lt;T&gt;</c>
    /// or extends <c>Sharpy.Iterator&lt;T&gt;</c>. Returns <c>null</c> otherwise.
    /// </summary>
    public static Type? GetIteratorElementType(Type clrType)
    {
        var currentType = clrType;
        while (currentType != null)
        {
            if (currentType.IsGenericType &&
                currentType.GetGenericTypeDefinition().FullName == "Sharpy.Iterator`1")
            {
                return currentType.GetGenericArguments()[0];
            }
            currentType = currentType.BaseType;
        }
        return null;
    }
}
