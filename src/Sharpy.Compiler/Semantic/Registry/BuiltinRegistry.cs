extern alias SharpyRT;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic.Registry;

/// <summary>
/// Registry of builtin types and functions from Sharpy.Core
/// Now uses cached reflection-based discovery for functions.
/// </summary>
[NotThreadSafe(Reason = "Constructor populates non-concurrent state; share only AFTER construction (#1140)")]
internal class BuiltinRegistry
{
    private readonly Dictionary<string, TypeSymbol> _types = new();
    private readonly Dictionary<string, List<FunctionSymbol>> _functions = new();
    private readonly CachedModuleDiscovery _discovery;
    private readonly ClrTypeBridge _clrTypeMapper = new();

    /// <summary>
    /// Deferred interface population work recorded by <see cref="RegisterType"/> and processed
    /// after all builtin types are registered, so that interface definitions can be resolved
    /// against already-registered symbols (e.g., IEnumerable, IEnumerator).
    /// </summary>
    private readonly List<(TypeSymbol Symbol, TypeParameterType[] TypeParams)> _pendingInterfacePopulation = new();

    /// <summary>
    /// Cache of minimal interface TypeSymbols keyed by the open-generic (or non-generic) CLR
    /// interface definition, so all builtin types implementing the same interface share one
    /// definition symbol.
    /// </summary>
    private readonly Dictionary<Type, TypeSymbol> _interfaceSymbols = new();

    /// <summary>
    /// Primitive types to register from PrimitiveCatalog.
    /// This maintains backward compatibility with the original hard-coded type list.
    /// </summary>
    private static readonly HashSet<string> RegisteredPrimitiveNames = new()
    {
        "int", "int32", "long", "int64", "float", "float64", "double", "decimal", "bool", "str"
    };

    /// <summary>
    /// Tagged union constructor names that the type checker handles via expected type inference.
    /// These are not regular functions — the type checker recognizes them based on context.
    /// </summary>
    private static readonly HashSet<string> TaggedUnionConstructors = new()
    {
        "Some", "Ok", "Err"
    };

    public BuiltinRegistry(ICompilerLogger? logger = null)
    {
        _discovery = new CachedModuleDiscovery(null, logger);
        LoadBuiltins();
    }

    private void LoadBuiltins()
    {
        // Load Sharpy.Core assembly first so discovery data is available for RegisterType
        var sharpyCoreAssembly = typeof(SharpyRT::Sharpy.Builtins).Assembly;
        _discovery.LoadAssembly(sharpyCoreAssembly);

        // Register primitives from PrimitiveCatalog using the defined set of names
        foreach (var (name, info) in PrimitiveCatalog.GetAllPrimitives())
        {
            if (!RegisteredPrimitiveNames.Contains(name))
                continue;
            // Skip void - it's registered separately below
            if (info.ClrType == typeof(void))
                continue;

            var kind = info.ClrType.IsValueType ? TypeKind.Struct : TypeKind.Class;
            RegisterType(info.SharpyName, info.ClrType, kind);
        }

        // Collections (generic) - use Sharpy.Core wrapper types
        RegisterType("list", typeof(SharpyRT::Sharpy.List<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);
        // dict was left on System.Collections.Generic.Dictionary<,> when 616a07c9b moved its
        // siblings to the wrappers. The CLR type recorded here is what operator resolution
        // reflects over, and Dictionary declares no operators, so every `dict op dict` had to be
        // served by the single-candidate shortcut in TypeInferenceService.FindBestOverload; a
        // second `|` overload took that shortcut away and `dict | dict` stopped resolving (#1361).
        RegisterType("dict", typeof(SharpyRT::Sharpy.Dict<,>), TypeKind.Class, isGeneric: true, typeParamCount: 2);
        RegisterType("set", typeof(SharpyRT::Sharpy.Set<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);
        RegisterType(BuiltinNames.FrozenDict, typeof(SharpyRT::Sharpy.FrozenDict<,>), TypeKind.Class, isGeneric: true, typeParamCount: 2);
        RegisterType(BuiltinNames.FrozenSet, typeof(SharpyRT::Sharpy.FrozenSet<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);

        // Bytes (non-generic) - immutable byte sequence
        RegisterType("bytes", typeof(SharpyRT::Sharpy.Bytes), TypeKind.Struct);

        // complex: the Python spelling. `Complex` already resolved through the Sharpy-namespace
        // discovery rule — this is not a rename, it registers the lowercase name a Python reader
        // actually writes, and both spellings resolve afterwards (#1362). Every other builtin is
        // lowercase; Complex was the outlier.
        RegisterType(BuiltinNames.Complex, typeof(SharpyRT::Sharpy.Complex), TypeKind.Struct);

        // Tuple: registered for OperatorValidator/ProtocolValidator metadata lookup.
        // typeParamCount=1 is nominal — real tuple arity is tracked by TupleType.ElementTypes,
        // not by this TypeSymbol's TypeParameters. CLR type is System.ValueTuple (non-generic sentinel).
        RegisterType(BuiltinNames.Tuple, typeof(System.ValueTuple), TypeKind.Struct, isGeneric: true, typeParamCount: 1);

        // Dict view types (returned by dict.items(), .keys(), .values()).
        // These named their real CLR types only after #1346: they were registered against
        // typeof(object) placeholders, which PopulateClrInterfaces skips, so none of them carried a
        // CLR interface list. Naming the real type is what gives them one.
        RegisterType(BuiltinNames.DictItemsView, typeof(SharpyRT::Sharpy.DictItemsView<,>), TypeKind.Class, isGeneric: true, typeParamCount: 2);
        RegisterType(BuiltinNames.DictKeyView, typeof(SharpyRT::Sharpy.DictKeyView<,>), TypeKind.Class, isGeneric: true, typeParamCount: 2);
        RegisterType(BuiltinNames.DictValuesView, typeof(SharpyRT::Sharpy.DictValuesView<,>), TypeKind.Class, isGeneric: true, typeParamCount: 2);

        // Iterator/iterable types (used by generators and reversed()). Sharpy.Iterator<T> is
        // ABSTRACT, which is the fact that makes a reference to it non-constructible (#1346) —
        // recorded here rather than asserted in prose, so NonConstructibleTypeNameOf reads it.
        RegisterType(BuiltinNames.Iterator, typeof(SharpyRT::Sharpy.Iterator<>), TypeKind.Class, isGeneric: true, typeParamCount: 1);
        RegisterType(BuiltinNames.IEnumerable, typeof(System.Collections.IEnumerable), TypeKind.Interface, isGeneric: true, typeParamCount: 1,
            varianceSource: typeof(IEnumerable<>));
        RegisterType(BuiltinNames.IEnumerator, typeof(System.Collections.IEnumerator), TypeKind.Interface, isGeneric: true, typeParamCount: 1,
            varianceSource: typeof(IEnumerator<>));

        // Result and Optional (for semantic-time method/property resolution)
        RegisterType("Result", typeof(SharpyRT::Sharpy.Result<,>), TypeKind.Struct, isGeneric: true, typeParamCount: 2);
        RegisterType("Optional", typeof(SharpyRT::Sharpy.Optional<>), TypeKind.Struct, isGeneric: true, typeParamCount: 1);

        // Template (PEP 750) — t"..." literal type
        RegisterType(BuiltinNames.Template, typeof(SharpyRT::Sharpy.Template), TypeKind.Class);

        // Special
        RegisterType("object", typeof(object), TypeKind.Class);
        RegisterType("None", typeof(void), TypeKind.Struct); // void for return type

        // Load builtin functions using reflection-based discovery
        LoadBuiltinFunctions();

        // Generic builtins (reversed, sorted, min, max) are now auto-discovered via
        // reflection instead of manual RegisterGenericBuiltin() calls. Type inference
        // for their return types is handled by TypeChecker special cases.

        // Auto-discover and register public types from Sharpy.Core (exceptions, etc.)
        LoadBuiltinTypes();

        // Register System.Exception as a base type for catch clauses
        if (!_types.ContainsKey("Exception"))
        {
            RegisterType("Exception", typeof(System.Exception), TypeKind.Class);
        }

        // Wire up BaseType for discovered exception types whose CLR base is outside the
        // discovery index (e.g., TypeError -> System.Exception, IOError -> IOException).
        // Types with Sharpy-defined intermediate bases (ZeroDivisionError -> ArithmeticError)
        // were already wired during discovery (#1596).
        WireExceptionBaseTypes();

        // Populate CLR interface information on registered TypeSymbols. Deferred until all
        // types are registered so interface definitions resolve to registered symbols (#827).
        PopulateClrInterfaces();
    }

    private void LoadBuiltinTypes()
    {
        var discoveredTypes = _discovery.GetModuleTypes("builtins");
        foreach (var typeSymbol in discoveredTypes)
        {
            // Skip types already registered (primitives, collections, etc.)
            if (_types.ContainsKey(typeSymbol.Name))
                continue;

            // Collect the constructor surface, as RegisterType and ModuleRegistry both already do.
            //
            // Without this, `Constructors.Count == 0` meant two different things depending on which
            // path built the symbol: "no public instance constructor" for a registered type, and
            // "never populated" for a discovered one — indistinguishable at every read site. Every
            // Sharpy.Core exception sits on this path, so `ValueError` reported an empty surface
            // while declaring two public constructors, and #1346 was the bill for it: that arm now
            // asks the CLR type by reflection precisely because this list could not be trusted.
            // The reflection stays (it is authoritative, and it is what handles the value-type edge
            // where an implicit parameterless constructor is not enumerated) — it now AGREES with
            // the field instead of contradicting it (#1473).
            //
            // Guarded on emptiness because discovery hands back cached TypeSymbol instances for
            // reference identity, so this can see the same symbol twice.
            if (typeSymbol.ClrType is { } clrType && typeSymbol.Constructors.Count == 0)
                typeSymbol.Constructors.AddRange(Discovery.ClrConstructorSurface.Build(clrType));

            _types[typeSymbol.Name] = typeSymbol;
        }
    }

    private void WireExceptionBaseTypes()
    {
        if (!_types.TryGetValue("Exception", out var exceptionSymbol))
            return;

        foreach (var typeSymbol in _types.Values)
        {
            if (typeSymbol.BaseType != null
                || typeSymbol.ClrType == null
                || typeSymbol.ClrType == typeof(System.Exception)
                || !BuiltinExceptionSurface.IsBuiltinExceptionType(typeSymbol.ClrType))
                continue;

            typeSymbol.BaseType = exceptionSymbol;
        }
    }

    private void LoadBuiltinFunctions()
    {
        // Get all functions from the "builtins" module (assembly already loaded in LoadBuiltins)
        var builtinFunctions = _discovery.GetModuleFunctions("builtins");

        // Register them in our internal dictionary
        // Note: This is called during construction, so no concurrent access is expected here
        foreach (var function in builtinFunctions)
        {
            // Skip generic functions whose name collides with a registered type constructor.
            // This specifically prevents CLR-discovered generic overloads like Builtins.List<T>(),
            // Builtins.Bool<T>(), Builtins.Int<T>() from shadowing the type constructors
            // registered by RegisterTypeConstructor(). User-defined types cannot collide here
            // because _types only contains compiler-registered builtin type names.
            if (function.IsGeneric && _types.ContainsKey(function.Name))
                continue;

            // Same rule, one step further: CPython spells its collection names as single words
            // (frozenset, defaultdict, frozendict), while reverse-mangling a PascalCase CLR name
            // inserts underscores. Builtins.FrozenSet<T> therefore arrived as `frozen_set`, which
            // does not collide by name with the registered `frozenset` — so both spellings resolved
            // and the discovered one returned an un-mapped FrozenSet<T> that degraded to `object`
            // (#1210). A discovered generic whose name differs from a registered type only by
            // underscores IS that type's constructor; the registered type wins.
            if (function.IsGeneric
                && _types.ContainsKey(function.Name.Replace("_", string.Empty, StringComparison.Ordinal)))
                continue;

            if (!_functions.ContainsKey(function.Name))
            {
                _functions[function.Name] = new List<FunctionSymbol>();
            }
            _functions[function.Name].Add(function);
        }
    }

    private void RegisterType(string sharpyName, Type clrType, TypeKind kind, bool isGeneric = false, int typeParamCount = 0, Type? varianceSource = null)
    {
        // Build shared TypeParameterType instances for generic types so all methods
        // reference the same objects (required for consistent name-based substitution).
        var sharedTypeParams = isGeneric
            ? Enumerable.Range(0, typeParamCount)
                .Select(i => new TypeParameterType { Name = $"T{i}" })
                .ToArray()
            : Array.Empty<TypeParameterType>();

        // Discover methods, operators, and protocols from Sharpy.Core via CLR reflection.
        var discovered = _discovery.GetTypeByName(sharpyName, sharedTypeParams);

        // Reuse TypeParameters from the discovered skeleton when available, so the
        // TypeParameterDef instances on the final TypeSymbol originate from discovery
        // rather than being created redundantly here.
        var typeParams = discovered is { IsGeneric: true }
            ? discovered.TypeParameters
            : (isGeneric
                ? Enumerable.Range(0, typeParamCount)
                    .Select(i => new TypeParameterDef { Name = $"T{i}" })
                    .ToList()
                : new List<TypeParameterDef>());

        // Apply CLR-declared variance (out/in) from the registered CLR type or an explicit
        // variance source (used when the registered ClrType is a non-generic placeholder,
        // e.g., IEnumerable registered as System.Collections.IEnumerable) (#827).
        typeParams = ApplyClrVariance(typeParams, varianceSource ?? clrType);

        // list and set are covariant in their element type by language design: Sharpy
        // treats list[Dog] as assignable to list[Animal] even though Sharpy.List<T> is
        // invariant in C#. This expresses per-parameter what the removed
        // TypeSymbol.IsCovariant flag previously declared for the whole type (#827).
        if (sharpyName is BuiltinNames.List or BuiltinNames.Set)
        {
            typeParams = typeParams
                .Select(tp => tp with { Variance = TypeParameterVariance.Covariant })
                .ToList();
        }

        var methods = discovered?.Methods ?? new List<FunctionSymbol>();
        var operatorMethods = discovered?.OperatorMethods ?? new Dictionary<string, List<FunctionSymbol>>();
        var protocolMethods = discovered?.ProtocolMethods ?? new Dictionary<string, List<FunctionSymbol>>();
        var properties = discovered?.Properties ?? new List<PropertySymbol>();

        // For types not discoverable from Sharpy.Core, provide inline definitions.
        ApplyNonDiscoverableDefinitions(sharpyName, ref methods, ref operatorMethods, ref protocolMethods);

        var typeSymbol = new TypeSymbol
        {
            Name = sharpyName,
            Kind = SymbolKind.Type,
            TypeKind = kind,
            ClrType = clrType,
            TypeParameters = typeParams,
            AccessLevel = AccessLevel.Public,
            // Read from the CLR type rather than declared per-registration, the same way the
            // discovery path does. `Sharpy.Iterator<T>` is abstract, so a reference to it has no
            // construction to denote and NonConstructibleTypeNameOf refuses it (SPY0346) instead of
            // reporting the weaker "no constructor reference form" (#1346). Unreachable while the
            // registration named a typeof(object) placeholder, which is not abstract.
            IsAbstract = clrType.IsAbstract && !clrType.IsInterface,
            Methods = methods,
            OperatorMethods = operatorMethods,
            ProtocolMethods = protocolMethods,
            Properties = properties,
            // The base's constructor surface, which ModuleRegistry has always collected and this
            // registry did not. C# inherits no constructors, so the emitter synthesizes forwarders
            // from this list: without it `class E(Exception): pass` emitted no forwarder at all and
            // `E('boom')` — the way a user exception is written in Python — died as CS1729 behind
            // SPY0908 (#1367). Both registries now build it through one helper, because a surface
            // that exists for CLR types reached by import and not for builtin-registered ones is the
            // same defect with a different entry point.
            Constructors = Discovery.ClrConstructorSurface.Build(clrType),
        };

        PopulateMethodOverloads(typeSymbol);

        // bytes.fromhex is registered manually (ApplyNonDiscoverableDefinitions) before this
        // TypeSymbol exists, so its bytes return type carries no Symbol at that point; patch
        // the freshly built symbol in so the RESULT of bytes.fromhex(...) has members (#1347).
        if (sharpyName == BuiltinNames.Bytes)
        {
            foreach (var method in typeSymbol.Methods)
            {
                if (method.Name == "fromhex"
                    && method.ReturnType is UserDefinedType { Symbol: null } bytesReturn)
                {
                    method.ReturnType = bytesReturn with { Symbol = typeSymbol };
                }
            }
        }

        _types[sharpyName] = typeSymbol;

        // Defer interface population until all builtin types are registered, so that
        // interface definitions (e.g., IEnumerable) resolve to the registered symbols.
        _pendingInterfacePopulation.Add((typeSymbol, sharedTypeParams));
    }

    /// <summary>
    /// Applies CLR-declared variance (out/in) to type parameter definitions by reading
    /// <see cref="System.Reflection.GenericParameterAttributes"/> from the generic type
    /// definition's parameters (#827). Non-generic sources leave the definitions unchanged.
    /// </summary>
    private static List<TypeParameterDef> ApplyClrVariance(List<TypeParameterDef> typeParams, Type? clrSource)
    {
        if (typeParams.Count == 0 || clrSource is not { IsGenericTypeDefinition: true })
            return typeParams;

        var clrArgs = clrSource.GetGenericArguments();
        if (clrArgs.Length != typeParams.Count)
            return typeParams;

        var result = new List<TypeParameterDef>(typeParams.Count);
        for (int i = 0; i < typeParams.Count; i++)
        {
            result.Add(typeParams[i] with { Variance = ClrTypeBridge.GetClrVariance(clrArgs[i]) });
        }
        return result;
    }

    /// <summary>
    /// Processes all deferred interface population work recorded by <see cref="RegisterType"/>.
    /// Populates <see cref="TypeSymbol.Interfaces"/> from CLR reflection so that
    /// TypeHierarchyService and generic inference can walk interface hierarchies of
    /// builtin types (#827).
    /// </summary>
    private void PopulateClrInterfaces()
    {
        foreach (var (typeSymbol, typeParams) in _pendingInterfacePopulation)
        {
            PopulateClrInterfaces(typeSymbol, typeParams);
        }
        _pendingInterfacePopulation.Clear();
    }

    private void PopulateClrInterfaces(TypeSymbol typeSymbol, TypeParameterType[] sharedTypeParams)
    {
        var clrType = typeSymbol.ClrType;

        // Skip placeholder registrations (typeof(object)/typeof(void)) and interface symbols
        // themselves — only concrete builtin classes/structs get interface lists here.
        if (clrType == null || clrType == typeof(object) || clrType == typeof(void))
            return;
        if (typeSymbol.TypeKind == TypeKind.Interface)
            return;
        if (typeSymbol.Interfaces.Count > 0)
            return;

        var allInterfaces = clrType.GetInterfaces()
            .Where(ClrTypeBridge.IsPublicInterface)
            .ToList();

        // When both generic and non-generic forms share a stripped name (IEnumerable vs
        // IEnumerable<T>), keep only the generic form — the non-generic form carries no
        // additional information and would create an ambiguous duplicate reference.
        var genericNames = new HashSet<string>(
            allInterfaces
                .Where(i => i.IsGenericType)
                .Select(i => ClrNameHelper.StripArity(i.GetGenericTypeDefinition().Name)));

        foreach (var iface in allInterfaces)
        {
            if (!iface.IsGenericType)
            {
                if (genericNames.Contains(iface.Name))
                    continue;

                typeSymbol.Interfaces.Add(new InterfaceReference
                {
                    Definition = GetOrCreateInterfaceSymbol(iface)
                });
                continue;
            }

            // Map the CLR interface's type arguments. Generic parameters of the implementing
            // type are mapped positionally to the shared TypeParameterType instances so that
            // list[T0] is recorded as implementing IEnumerable[T0], etc.
            var resolvedArgs = new List<SemanticType>();
            var resolvable = true;
            foreach (var arg in iface.GetGenericArguments())
            {
                var mapped = MapClrInterfaceArgument(arg, sharedTypeParams);
                if (mapped == null)
                {
                    resolvable = false;
                    break;
                }
                resolvedArgs.Add(mapped);
            }

            if (!resolvable)
                continue;

            typeSymbol.Interfaces.Add(new InterfaceReference
            {
                Definition = GetOrCreateInterfaceSymbol(iface.GetGenericTypeDefinition()),
                ResolvedTypeArguments = resolvedArgs.ToImmutableArray()
            });
        }
    }

    /// <summary>
    /// Maps a CLR interface type argument to a SemanticType. Generic parameters of the
    /// implementing type map positionally to <paramref name="sharedTypeParams"/> (T0, T1, ...)
    /// so name-based substitution lines up with the TypeSymbol's TypeParameters.
    /// Returns null when the argument cannot be represented (interface is then skipped).
    /// </summary>
    private SemanticType? MapClrInterfaceArgument(Type arg, TypeParameterType[] sharedTypeParams)
    {
        if (arg.IsGenericParameter)
        {
            var position = arg.GenericParameterPosition;
            return position < sharedTypeParams.Length ? sharedTypeParams[position] : null;
        }

        if (arg.IsGenericType)
        {
            var mappedArgs = new List<SemanticType>();
            foreach (var nested in arg.GetGenericArguments())
            {
                var mapped = MapClrInterfaceArgument(nested, sharedTypeParams);
                if (mapped == null)
                    return null;
                mappedArgs.Add(mapped);
            }

            var def = arg.GetGenericTypeDefinition();

            // KeyValuePair<K, V> -> tuple[K, V] (matches ClrTypeBridge's mapping)
            if (def == typeof(KeyValuePair<,>))
                return new TupleType { ElementTypes = mappedArgs };

            return new GenericType
            {
                Name = ClrNameHelper.StripArity(def.Name),
                TypeArguments = mappedArgs
            };
        }

        // Unrepresentable open-generic leftovers (e.g., arrays of generic parameters).
        if (arg.ContainsGenericParameters)
            return null;

        // Concrete leaf type (e.g., int in IEquatable<int>) — reuse the standard CLR mapping.
        return _clrTypeMapper.MapClrTypeToSemanticType(arg);
    }

    /// <summary>
    /// Finds or creates the TypeSymbol for an interface definition. Prefers symbols already
    /// registered in <see cref="_types"/> (e.g., IEnumerable, IEnumerator); otherwise creates
    /// a minimal interface TypeSymbol, cached per CLR definition so all implementers share it.
    /// </summary>
    private TypeSymbol GetOrCreateInterfaceSymbol(Type interfaceDef)
    {
        var name = ClrNameHelper.StripArity(interfaceDef.Name);

        if (_types.TryGetValue(name, out var registered) && registered.TypeKind == TypeKind.Interface)
            return registered;

        if (_interfaceSymbols.TryGetValue(interfaceDef, out var cached))
            return cached;

        var clrArgs = interfaceDef.IsGenericTypeDefinition
            ? interfaceDef.GetGenericArguments()
            : Type.EmptyTypes;
        var typeParams = clrArgs
            .Select((arg, i) => new TypeParameterDef { Name = $"T{i}", Variance = ClrTypeBridge.GetClrVariance(arg) })
            .ToList();

        var symbol = new TypeSymbol
        {
            Name = name,
            Kind = SymbolKind.Type,
            TypeKind = TypeKind.Interface,
            ClrType = interfaceDef,
            TypeParameters = typeParams,
            AccessLevel = AccessLevel.Public
        };
        _interfaceSymbols[interfaceDef] = symbol;
        return symbol;
    }

    /// <summary>
    /// Populates MethodOverloads on a TypeSymbol for methods that share the same name.
    /// </summary>
    private static void PopulateMethodOverloads(TypeSymbol typeSymbol)
    {
        var overloadGroups = typeSymbol.Methods
            .GroupBy(m => m.Name)
            .Where(g => g.Count() > 1);

        foreach (var group in overloadGroups)
        {
            typeSymbol.MethodOverloads[group.Key] = group.ToList();
        }
    }

    /// <summary>
    /// Provides methods, operators, and protocols for types that cannot be discovered
    /// <summary>
    /// Provides inline definitions for types whose methods, operators, or protocols cannot
    /// be discovered from Sharpy.Core via CLR reflection. Each case here is permanent by design:
    /// <list type="bullet">
    /// <item><description>
    /// <b>str</b>: Maps to <c>System.String</c>. Python-compatible string methods live as
    /// extension methods in <c>Sharpy.StringExtensions</c>, not on <c>System.String</c> itself.
    /// Discovery cannot find extension methods on the target type, so they are reflected here
    /// and registered as instance methods.
    /// </description></item>
    /// <item><description>
    /// <b>tuple</b>: Maps to <c>System.ValueTuple</c>, whose operators (<c>==</c>, <c>+</c>, <c>*</c>)
    /// are compiler-synthesized by Roslyn/CLR, not present as discoverable CLR methods.
    /// Protocols (<c>__len__</c>, <c>__iter__</c>, <c>__getitem__</c>) similarly have no CLR surface.
    /// </description></item>
    /// <item><description>
    /// <b>Iterator/IEnumerable/IEnumerator</b>: <c>Iterator</c> names its real CLR type
    /// (<c>Sharpy.Iterator&lt;T&gt;</c>) since #1346, so it carries a CLR interface list and its
    /// abstractness — the two things the old <c>typeof(object)</c> placeholder suppressed.
    /// <c>IEnumerable</c>/<c>IEnumerator</c> are still registered against the NON-GENERIC
    /// <c>System.Collections</c> forms with the generic ones supplied as a variance source, so
    /// their type-parameter variance is read from the generic definition.
    /// </description></item>
    /// <item><description>
    /// <b>int.parse / float.parse</b>: Live on separate utility classes (<c>IntParse</c>,
    /// <c>DoubleParse</c>) in Sharpy.Core, not on <c>System.Int32</c> / <c>System.Double</c>.
    /// Discovery operates on the actual CLR type surface, so these cross-type helpers
    /// must be registered manually as static methods on the Sharpy <c>int</c>/<c>float</c> types.
    /// </description></item>
    /// </list>
    /// </summary>
    private void ApplyNonDiscoverableDefinitions(
        string typeName,
        ref List<FunctionSymbol> methods,
        ref Dictionary<string, List<FunctionSymbol>> operatorMethods,
        ref Dictionary<string, List<FunctionSymbol>> protocolMethods)
    {
        switch (typeName)
        {
            case BuiltinNames.Str:
                DiscoverStringExtensionMethods(ref methods);
                operatorMethods = MakeDunderDict(DunderNames.Add, DunderNames.Mul, DunderNames.Eq, DunderNames.Ne);
                protocolMethods = MakeDunderDict(DunderNames.Len, DunderNames.Iter, DunderNames.GetItem, DunderNames.Contains);
                break;

            case BuiltinNames.Tuple:
                operatorMethods = MakeDunderDict(DunderNames.Add, DunderNames.Mul, DunderNames.Eq, DunderNames.Ne);
                protocolMethods = MakeDunderDict(DunderNames.Len, DunderNames.Iter, DunderNames.GetItem);
                break;

            case BuiltinNames.FrozenDict:
                // Read-only mapping: supports __len__, __iter__, __getitem__, __contains__ but NOT __setitem__
                protocolMethods = MakeDunderDict(DunderNames.Len, DunderNames.Iter, DunderNames.GetItem, DunderNames.Contains);
                break;

            case BuiltinNames.FrozenSet:
                // Read-only set: __len__, __iter__, __contains__ — and NOT __getitem__, since a set
                // is not subscriptable (this is where it differs from its frozendict sibling).
                protocolMethods = MakeDunderDict(DunderNames.Len, DunderNames.Iter, DunderNames.Contains);
                break;

            case BuiltinNames.Iterator or BuiltinNames.IEnumerable or BuiltinNames.IEnumerator:
                protocolMethods = MakeDunderDict(DunderNames.Iter);
                break;

            case BuiltinNames.Int:
                if (methods.Count == 0)
                    methods = new List<FunctionSymbol> { MakeParseMethod(SemanticType.Int) };
                else
                    methods.Add(MakeParseMethod(SemanticType.Int));
                break;

            case BuiltinNames.Float:
                if (methods.Count == 0)
                    methods = new List<FunctionSymbol> { MakeParseMethod(SemanticType.Float) };
                else
                    methods.Add(MakeParseMethod(SemanticType.Float));
                break;

            case BuiltinNames.Bytes:
                // bytes.fromhex lives in the standalone BytesFromhex class (the IntParse
                // design, #1347) so the Builtins.Bytes overload set never shadows the TYPE in
                // static-member position. CLR reflection therefore cannot surface it off the
                // struct, and it is registered manually like int.parse/float.parse above —
                // which is also what gives the call real argument-type checking instead of a
                // CS1503 leak behind SPY0908.
                methods.Add(MakeFromhexMethod());
                break;
        }
    }

    /// <summary>
    /// Discovers extension methods on <c>System.String</c> from <c>Sharpy.StringExtensions</c>
    /// and adds them as instance method FunctionSymbols. The <c>this string</c> first parameter
    /// is stripped since the TypeChecker sees these as instance methods on <c>str</c>.
    /// </summary>
    private void DiscoverStringExtensionMethods(ref List<FunctionSymbol> methods)
    {
        var sharpyCoreAssembly = typeof(SharpyRT::Sharpy.Builtins).Assembly;
        var extensionType = sharpyCoreAssembly.GetType("Sharpy.StringExtensions");
        if (extensionType == null)
            return;

        var extensionMethods = extensionType
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.IsDefined(typeof(ExtensionAttribute), false))
            .Where(m =>
            {
                var parameters = m.GetParameters();
                return parameters.Length > 0 && parameters[0].ParameterType == typeof(string);
            })
            .ToList();

        if (methods.Count == 0)
            methods = new List<FunctionSymbol>();

        foreach (var method in extensionMethods)
        {
            try
            {
                // Build a FunctionSignature via the discovery infrastructure, then strip
                // the first parameter (the `this string` extension target).
                var signature = BuildExtensionMethodSignature(method);
                var expanded = OverloadExpander.Expand(signature, "StringExtensions");
                foreach (var overloadSig in expanded)
                {
                    methods.Add(_discovery.ConvertToFunctionSymbol(overloadSig, "str", sharedTypeParams: null));
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
            {
                // Skip methods that can't be mapped (same pattern as OverloadIndexBuilder)
            }
        }
    }

    /// <summary>
    /// Builds a <see cref="FunctionSignature"/> from a CLR extension method, stripping the
    /// <c>this</c> parameter so it appears as an instance method.
    /// </summary>
    private static FunctionSignature BuildExtensionMethodSignature(MethodInfo method)
    {
        var typeMapper = new ClrTypeBridge();
        var parameters = method.GetParameters();

        var signature = new FunctionSignature
        {
            Name = NameMangler.ToSharpyName(method.Name, ReverseNameContext.Method),
            ReturnType = CreateTypeSignatureFromClr(method.ReturnType, typeMapper),
        };

        // Skip the first parameter (the `this string` extension target)
        for (int i = 1; i < parameters.Length; i++)
        {
            var param = parameters[i];
            signature.Parameters.Add(new ParameterSignature
            {
                Name = param.Name ?? "arg",
                Type = CreateTypeSignatureFromClr(param.ParameterType, typeMapper),
                HasDefault = param.HasDefaultValue,
                DefaultValue = param.HasDefaultValue ? ConvertDefaultValue(param.DefaultValue) : null,
                IsVariadic = param.GetCustomAttribute<ParamArrayAttribute>() != null,
            });
        }

        return signature;
    }

    /// <summary>
    /// Creates a <see cref="TypeSignature"/> from a CLR type for extension method discovery.
    /// Handles primitives, generic types, and generic parameters.
    /// </summary>
    private static TypeSignature CreateTypeSignatureFromClr(Type clrType, ClrTypeBridge typeMapper)
    {
        if (clrType.IsGenericParameter)
        {
            return new TypeSignature
            {
                Name = clrType.Name,
                IsGenericParameter = true,
                GenericParameterPosition = clrType.GenericParameterPosition,
                IsMethodLevelTypeParam = clrType.DeclaringMethod != null,
                ClrTypeName = string.Empty
            };
        }

        var semanticType = typeMapper.MapClrTypeToSemanticType(clrType);

        var signature = new TypeSignature
        {
            Name = semanticType.GetDisplayName(),
            ClrTypeName = clrType.AssemblyQualifiedName ?? clrType.FullName ?? clrType.Name
        };

        if (clrType.IsGenericType)
        {
            var clrTypeArgs = clrType.GetGenericArguments();

            if (semanticType is GenericType)
            {
                signature.IsGeneric = true;
                signature.TypeArguments = clrTypeArgs
                    .Select(t => CreateTypeSignatureFromClr(t, typeMapper))
                    .ToList();
            }
        }
        else if (clrType.IsArray && semanticType is GenericType)
        {
            // Arrays map to list[T] — preserve the element type so variadic extraction
            // (GetVariadicElementType) can recover T for `params T[]` parameters.
            var elementType = clrType.GetElementType();
            if (elementType != null)
            {
                signature.IsGeneric = true;
                signature.TypeArguments = new List<TypeSignature>
                {
                    CreateTypeSignatureFromClr(elementType, typeMapper)
                };
            }
        }

        return signature;
    }

    private static string? ConvertDefaultValue(object? value)
    {
        if (value == null || value == DBNull.Value)
            return null;

        return value switch
        {
            string s => $"\"{s}\"",
            char c => $"'{c}'",
            bool b => b.ToString().ToLowerInvariant(),
            int or long or short or byte or sbyte or uint or ulong or ushort => value.ToString(),
            float f => f.ToString("G9", System.Globalization.CultureInfo.InvariantCulture),
            double d => d.ToString("G17", System.Globalization.CultureInfo.InvariantCulture),
            _ => null
        };
    }

    private static readonly UserDefinedType ValueErrorType = new() { Name = "ValueError" };

    /// <summary>
    /// The <c>bytes.fromhex</c> static: <c>fromhex(string) -> bytes</c>, implemented by
    /// <c>Sharpy.BytesFromhex.Fromhex</c> (#1347). CPython-matching semantics: returns bytes
    /// and raises ValueError on malformed input, so the return type is plain bytes rather
    /// than a Result. The bytes <see cref="TypeSymbol"/> is patched onto the return type by
    /// <c>RegisterType</c> once that symbol exists.
    /// </summary>
    private static FunctionSymbol MakeFromhexMethod()
    {
        return new FunctionSymbol
        {
            Name = "fromhex",
            Kind = SymbolKind.Function,
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol { Name = "string", Type = SemanticType.Str }
            },
            ReturnType = new UserDefinedType { Name = BuiltinNames.Bytes },
            AccessLevel = AccessLevel.Public,
            IsStatic = true,
        };
    }

    private static FunctionSymbol MakeParseMethod(SemanticType resultOkType)
    {
        return new FunctionSymbol
        {
            Name = "parse",
            Kind = SymbolKind.Function,
            Parameters = new List<ParameterSymbol>
            {
                new ParameterSymbol { Name = "s", Type = SemanticType.Str }
            },
            ReturnType = new ResultType { OkType = resultOkType, ErrorType = ValueErrorType },
            AccessLevel = AccessLevel.Public,
            IsStatic = true,
        };
    }

    /// <summary>
    /// Creates a dictionary of dunder names, each with a single placeholder FunctionSymbol.
    /// Used for operator and protocol stubs where validators only check key presence.
    /// </summary>
    private static Dictionary<string, List<FunctionSymbol>> MakeDunderDict(params string[] dunderNames)
    {
        var dict = new Dictionary<string, List<FunctionSymbol>>();
        foreach (var name in dunderNames)
        {
            dict[name] = new List<FunctionSymbol>
            {
                new FunctionSymbol
                {
                    Name = name,
                    Kind = SymbolKind.Function,
                    AccessLevel = AccessLevel.Public,
                }
            };
        }
        return dict;
    }

    public TypeSymbol? GetType(string name) => _types.GetValueOrDefault(name);

    /// <summary>
    /// Every builtin type registered by <c>RegisterType</c>, keyed by Sharpy name. Exposed so the
    /// registration-parity conformance guard can enumerate this site and diff it against the other
    /// two a registered type has to appear in (#1253, umbrella #1145).
    /// </summary>
    internal IReadOnlyDictionary<string, TypeSymbol> RegisteredTypes => _types;

    /// <summary>
    /// Every builtin FUNCTION name registered here. Exposed for the same reason
    /// <see cref="RegisteredTypes"/> is: a conformance sweep has to enumerate the class it guards
    /// from the registry itself, or the "all builtins" it claims to cover quietly becomes "the
    /// builtins someone listed once" (#1322).
    /// </summary>
    internal IReadOnlyCollection<string> RegisteredFunctionNames => _functions.Keys;

    /// <summary>
    /// Returns the first function symbol with the given name.
    /// For functions with multiple overloads, use GetFunctionOverloads instead.
    /// </summary>
    public FunctionSymbol? GetFunction(string name) => _functions.GetValueOrDefault(name)?.FirstOrDefault();

    /// <summary>
    /// Returns all function overloads with the given name, or null if no function with that name exists.
    /// </summary>
    public List<FunctionSymbol>? GetFunctionOverloads(string name) => _functions.GetValueOrDefault(name);

    /// <summary>
    /// Returns true if the name is a tagged union constructor (Some, Ok, Err).
    /// These are handled by the type checker via expected type inference, not as regular functions.
    /// </summary>
    public bool IsTaggedUnionConstructor(string name) => TaggedUnionConstructors.Contains(name);

    public IEnumerable<(string Name, TypeSymbol Type)> GetAllTypes() => _types.Select(kv => (kv.Key, kv.Value));
    public IEnumerable<(string Name, FunctionSymbol Function)> GetAllFunctions() =>
        _functions.SelectMany(kv => kv.Value.Select(f => (kv.Key, f)));

    /// <summary>
    /// True when <paramref name="name"/> is the bare spelling of something the language already
    /// defines: a registered builtin type, a registered builtin function, or a primitive.
    /// </summary>
    /// <remarks>
    /// The single authority behind the shadowing rule (SPY0212). It is deliberately a QUERY OVER THE
    /// REGISTRIES rather than a hand-maintained list, so a builtin added later is covered without
    /// anyone remembering to update this — the failure mode of a literal list is that it silently
    /// stops matching the language.
    /// <para>Types and functions are unioned on purpose. The behavioural split they have today is
    /// accidental (shadowing a builtin type was silent, shadowing a builtin function produced an
    /// incidental protocol or arity error), and a rule stated over only one of the two kinds leaks
    /// at the boundary — which is exactly how <c>`len`</c> stayed correct while <c>`int`</c> broke.</para>
    /// </remarks>
    public bool IsReservedBuiltinName(string name) =>
        _types.ContainsKey(name)
        || _functions.ContainsKey(name)
        || PrimitiveCatalog.IsPrimitive(name);

    /// <summary>
    /// True when <paramref name="name"/> is the bare spelling of a builtin <em>type</em> — the
    /// narrower half of <see cref="IsReservedBuiltinName"/>, and the one the SPY0212 refusal is
    /// stated over.
    /// </summary>
    /// <remarks>
    /// A type declaration enters the TYPE namespace, which is the namespace annotations resolve
    /// through; that is the only collision Sharpy cannot leave unresolved, so it is the only one
    /// refused. Shadowing a builtin <em>function</em> name is a value-namespace event and draws the
    /// SPY0483 warning instead.
    /// </remarks>
    public bool IsReservedBuiltinTypeName(string name) =>
        _types.ContainsKey(name) || PrimitiveCatalog.IsPrimitive(name);

    /// <summary>
    /// True when <paramref name="symbol"/> IS one of the registry's own symbols, as opposed to a
    /// user symbol that merely spells the same name.
    /// </summary>
    /// <remarks>
    /// Identity, not name — the same discipline <c>ConstructorReferenceOf</c> uses. Builtins are
    /// seeded into the global scope, so <c>SymbolTable.Lookup("len")</c> answers with the registry's
    /// own <c>FunctionSymbol</c> when nothing shadows it and with the user's when something does.
    /// Only reference identity separates those two, and both the TypeChecker's callee classifier and
    /// the emitter's builtin-call arm need the same answer: when they disagreed, a bare
    /// <c>def len</c> was type-checked as the builtin and emitted as the user's function (#1241).
    /// </remarks>
    public bool IsBuiltinSymbol(Symbol symbol)
    {
        // An aliased import binds under its own spelling but IS the registry's symbol; the lookups
        // below are keyed by the builtin's name, so ask about what it dispatches as (#1383).
        if (symbol.BuiltinAliasOf is { } aliased)
            symbol = aliased;

        if (_types.TryGetValue(symbol.Name, out var type) && ReferenceEquals(type, symbol))
            return true;

        return _functions.TryGetValue(symbol.Name, out var overloads)
            && overloads.Any(f => ReferenceEquals(f, symbol));
    }

    #region CLR Type Fallback

    private readonly System.Collections.Concurrent.ConcurrentDictionary<(string, int), TypeSymbol?> _clrTypeCache = new();

    /// <summary>
    /// Attempts to resolve a type name as a .NET type from well-known namespaces.
    /// Used as a fallback when a type is not found in the symbol table.
    /// Results are cached for performance. The arity parameter selects the right
    /// member from a multi-arity group (e.g. Action vs Action`1 vs Action`2).
    /// </summary>
    public TypeSymbol? TryResolveClrType(string name, int arity = 0)
    {
        return _clrTypeCache.GetOrAdd((name, arity), static key =>
        {
            var (n, a) = key;
            var clrType = TryFindClrType(n, a);
            if (clrType == null)
                return null;

            var isDelegate = typeof(MulticastDelegate).IsAssignableFrom(clrType)
                && clrType != typeof(Delegate) && clrType != typeof(MulticastDelegate);
            var kind = clrType.IsInterface ? TypeKind.Interface
                : clrType.IsEnum ? TypeKind.Enum
                : clrType.IsValueType ? TypeKind.Struct
                : isDelegate ? TypeKind.Delegate
                : TypeKind.Class;
            var sym = new TypeSymbol
            {
                Name = n,
                Kind = SymbolKind.Type,
                TypeKind = kind,
                ClrType = clrType,
                AccessLevel = AccessLevel.Public,
                IsAbstract = clrType.IsAbstract && !clrType.IsInterface
            };

            // #1613: populate TypeParameters so IsGeneric is true and the arity check fires
            if (clrType.IsGenericTypeDefinition)
            {
                foreach (var tp in clrType.GetGenericArguments())
                {
                    sym.TypeParameters.Add(new Parser.Ast.TypeParameterDef { Name = tp.Name });
                }
            }

            if (isDelegate)
            {
                var invoke = new ClrTypeBridge().SynthesizeDelegateInvoke(clrType);
                if (invoke != null)
                    sym.Methods.Add(invoke);
            }

            return sym;
        });
    }

    // TODO(#1625): namespace priority is broken for arity > 0 — Type.GetType can't see
    // Sharpy.Core.dll, so Sharpy.List`1 loses to System.Collections.Generic.List`1.
    private static Type? TryFindClrType(string name, int arity)
    {
        // #1613: for arity > 0, probe `Name`N (e.g. Action`1, Func`3)
        var clrName = arity > 0 ? $"{name}`{arity}" : name;

        string[] namespaces =
        {
            "Sharpy",
            "System",
            "System.Collections.Generic",
            "System.IO",
            "System.Text",
            "System.IO.Compression",
            "System.Net",
            "System.Net.Sockets",
            "System.Net.Http",
            "System.Numerics",
            "System.Threading",
            "System.Threading.Tasks",
            "System.Text.RegularExpressions",
            "System.Security.Cryptography",
            "System.Diagnostics",
            "System.Linq"
        };

        foreach (var ns in namespaces)
        {
            var fullName = $"{ns}.{clrName}";
            var type = Type.GetType(fullName);
            if (type != null)
                return type;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var ns in namespaces)
            {
                var type = assembly.GetType($"{ns}.{clrName}");
                if (type != null)
                    return type;
            }
        }

        return null;
    }

    #endregion
}
