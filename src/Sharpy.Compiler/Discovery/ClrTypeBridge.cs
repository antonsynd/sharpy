using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Shared;
using TypeParameterDef = Sharpy.Compiler.Parser.Ast.TypeParameterDef;
using TypeParameterVariance = Sharpy.Compiler.Parser.Ast.TypeParameterVariance;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// The single owned component for the CLR&lt;-&gt;Sharpy type boundary.
///
/// <para>
/// This facade owns both directions of the boundary:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <b>CLR <see cref="Type"/> -&gt; <see cref="SemanticType"/></b> via
///     <see cref="MapClrTypeToSemanticType"/> — how discovery/semantic analysis reads reflected
///     .NET types back into the Sharpy type system.
///   </description></item>
///   <item><description>
///     <b><see cref="SemanticType"/> name -&gt; C# fully-qualified type name (string)</b> via
///     <see cref="TryGetCSharpTypeName"/> — the seam code generation consumes to emit type names.
///     The bridge deals only in <em>names</em>; Roslyn <c>SyntaxFactory</c> construction stays in CodeGen.
///   </description></item>
/// </list>
///
/// <para>
/// All type special-casing lives in the nested <see cref="SpecialCases"/> registry: the
/// <see cref="PrimitiveCatalog"/> primitives (delegated to, never duplicated), the
/// Sharpy-collection correspondence (Sharpy name &lt;-&gt; C# name &lt;-&gt; CLR generic definition),
/// the <c>"Sharpy"</c>-namespace rule, the <c>NdArray</c> special case, and the
/// <c>KeyValuePair</c>-&gt;tuple mapping. Anything that special-cases a type by identity belongs
/// there, so drift across the compiler is impossible by construction.
/// </para>
///
/// Thread-safe for concurrent use.
/// </summary>
[ThreadSafe]
internal class ClrTypeBridge
{
    private readonly ConcurrentDictionary<Type, SemanticType> _typeCache = new();

    /// <summary>
    /// The single registry of type special cases owned by <see cref="ClrTypeBridge"/>.
    /// Every identity-based special case (primitives, Sharpy collections, the <c>"Sharpy"</c>
    /// namespace rule, NdArray, KeyValuePair) is defined here exactly once.
    /// </summary>
    internal static class SpecialCases
    {
        // ---- Sharpy generic CLR definitions (FullName of the open generic) --------------------
        // Centralizes the FullName string literals the forward mapper matches against, so the
        // CLR-side identity of each Sharpy collection lives in exactly one place.
        internal const string SharpyListFullName = CSharpTypeNames.SharpyList + "`1";
        internal const string SharpyDictFullName = CSharpTypeNames.SharpyDict + "`2";
        internal const string SharpySetFullName = CSharpTypeNames.SharpySet + "`1";
        internal const string SharpyOptionalFullName = CSharpTypeNames.SharpyOptional + "`1";
        internal const string SharpyResultFullName = CSharpTypeNames.SharpyResult + "`2";
        internal const string SharpyNdArrayFullName = CSharpTypeNames.SharpyNdArray + "`1";
        internal const string DictItemsViewFullName = "Sharpy.DictItemsView`2";
        internal const string DictKeyViewFullName = "Sharpy.DictKeyView`2";
        internal const string DictValuesViewFullName = "Sharpy.DictValuesView`2";
        internal const string KeyValuePairFullName = "System.Collections.Generic.KeyValuePair`2";

        /// <summary>
        /// The reserved runtime namespace for Sharpy.Core / stdlib CLR types. Types in this
        /// namespace (or a sub-namespace of it) are Sharpy runtime types, not user code.
        /// </summary>
        internal const string SharpyNamespace = "Sharpy";

        /// <summary>
        /// Reverse direction: Sharpy builtin/collection type name -&gt; fully-qualified C# type name.
        /// This is the single source for the collection correspondence that previously lived in
        /// <c>CSharpTypeNames.FromSharpyName</c> and <c>TypeSyntaxMapper</c>'s static map.
        /// Primitives are <em>not</em> listed here — they are delegated to <see cref="PrimitiveCatalog"/>.
        /// </summary>
        private static readonly ImmutableDictionary<string, string> _collectionCSharpNames =
            new Dictionary<string, string>
            {
                [BuiltinNames.List] = CSharpTypeNames.SharpyList,
                [BuiltinNames.Dict] = CSharpTypeNames.SharpyDict,
                [BuiltinNames.Set] = CSharpTypeNames.SharpySet,
                [BuiltinNames.DefaultDict] = CSharpTypeNames.SharpyDefaultDict,
                ["DefaultDict"] = CSharpTypeNames.SharpyDefaultDict,
                [BuiltinNames.FrozenDict] = CSharpTypeNames.SharpyFrozenDict,
                ["FrozenDict"] = CSharpTypeNames.SharpyFrozenDict,
                [BuiltinNames.Bytes] = CSharpTypeNames.SharpyBytes,
                [BuiltinNames.Template] = CSharpTypeNames.SharpyTemplate,
                [BuiltinNames.Tuple] = "System.ValueTuple",
            }.ToImmutableDictionary();

        /// <summary>
        /// Returns the fully-qualified C# type name for a Sharpy collection type name, or
        /// <c>null</c> when the name is not a known collection.
        /// </summary>
        internal static string? TryGetCSharpCollectionName(string sharpyName)
            => _collectionCSharpNames.GetValueOrDefault(sharpyName);

        /// <summary>
        /// Returns true when the CLR namespace is the reserved Sharpy runtime namespace
        /// (<c>"Sharpy"</c>) or a sub-namespace of it. This is the single home of the
        /// <c>"Sharpy"</c>-namespace rule.
        /// </summary>
        internal static bool IsSharpyNamespace(string? ns)
            => ns == SharpyNamespace || (ns != null && ns.StartsWith(SharpyNamespace + ".", StringComparison.Ordinal));
    }

    /// <summary>
    /// Reverse direction: maps a Sharpy builtin/collection type name to its fully-qualified C#
    /// type name (e.g. <c>"int" -&gt; "int"</c>, <c>"list" -&gt; "Sharpy.List"</c>). Primitives are
    /// delegated to <see cref="PrimitiveCatalog"/>; collections come from
    /// <see cref="SpecialCases"/>. Returns <c>null</c> for names with no builtin C# correspondence
    /// (i.e. user-defined types, which CodeGen resolves through the symbol table).
    /// </summary>
    /// <remarks>
    /// This is the seam Phase 3.2 wires code generation onto so that <c>TypeSyntaxMapper</c> and
    /// <c>CSharpTypeNames.FromSharpyName</c> source their name mapping from the bridge instead of
    /// re-declaring it.
    /// </remarks>
    public static string? TryGetCSharpTypeName(string sharpyName)
    {
        var primitive = PrimitiveCatalog.GetByName(sharpyName);
        if (primitive != null)
            return primitive.CSharpName;

        return SpecialCases.TryGetCSharpCollectionName(sharpyName);
    }

    /// <summary>
    /// Map a CLR type to a Sharpy SemanticType.
    /// </summary>
    public SemanticType MapClrTypeToSemanticType(Type clrType)
    {
        // Use GetOrAdd for thread-safe caching
        return _typeCache.GetOrAdd(clrType, MapTypeInternal);
    }

    private SemanticType MapTypeInternal(Type clrType)
    {
        // Handle generic type parameters (e.g., T in List<T>)
        if (clrType.IsGenericParameter)
        {
            return new TypeParameterType { Name = clrType.Name };
        }

        // Check PrimitiveCatalog first for primitive types
        var primitiveInfo = PrimitiveCatalog.GetByClrType(clrType);
        if (primitiveInfo != null)
        {
            // Return the appropriate SemanticType singleton or create BuiltinType
            return primitiveInfo.SharpyName switch
            {
                BuiltinNames.Int => SemanticType.Int,
                BuiltinNames.Long => SemanticType.Long,
                BuiltinNames.Float => SemanticType.Float,       // float -> double (per spec)
                BuiltinNames.Float32 => SemanticType.Float32,   // float32 -> C# float
                BuiltinNames.Float64 or BuiltinNames.Double => SemanticType.Double,
                BuiltinNames.Bool => SemanticType.Bool,
                BuiltinNames.Str or "string" => SemanticType.Str,
                BuiltinNames.Void or BuiltinNames.None => SemanticType.Void,
                BuiltinNames.Object => SemanticType.Object,
                _ => new BuiltinType { Name = primitiveInfo.SharpyName, ClrType = clrType }
            };
        }

        // Handle arrays
        if (clrType.IsArray)
        {
            var elementType = clrType.GetElementType();
            if (elementType == null)
                return SemanticType.Object; // Defensive fallback for safety

            var arrayName = BuiltinNames.Array;
            return new GenericType
            {
                Name = arrayName,
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(elementType)
                }
            };
        }

        // Handle nullable value types
        var underlyingNullable = Nullable.GetUnderlyingType(clrType);
        if (underlyingNullable != null)
        {
            return new NullableType
            {
                UnderlyingType = MapClrTypeToSemanticType(underlyingNullable)
            };
        }

        // Handle Sharpy.Core types that extend Iterator<T> (like RangeIterator)
        // These are iterable types that yield elements of a specific type
        var iteratorElementType = ClrTypeHelper.GetIteratorElementType(clrType);
        if (iteratorElementType != null)
        {
            return new BuiltinType
            {
                Name = clrType.Name,
                ClrType = clrType
            };
        }

        // Handle non-generic Task
        if (clrType == typeof(System.Threading.Tasks.Task))
            return new TaskType { ResultType = null };

        // Handle generic types
        if (clrType.IsGenericType)
        {
            return MapGenericType(clrType);
        }

        // Handle enums
        if (clrType.IsEnum)
        {
            return SemanticType.Int;
        }

        // Non-generic CLR class/struct: map to UserDefinedType with CLR type info
        if (clrType.IsClass || clrType.IsValueType)
        {
            var symbol = new TypeSymbol
            {
                Name = clrType.Name,
                Kind = SymbolKind.Type,
                TypeKind = clrType.IsValueType ? TypeKind.Struct : TypeKind.Class,
                ClrType = clrType,
                Interfaces = BuildInterfaceList(clrType)
            };

            return new UserDefinedType
            {
                Name = clrType.Name,
                Symbol = symbol
            };
        }

        // Fallback to object for unknown types
        return SemanticType.Object;
    }

    private SemanticType MapGenericType(Type clrType)
    {
        var genericDef = clrType.GetGenericTypeDefinition();
        var typeArgs = clrType.GetGenericArguments();

        // Sharpy.List<T>, List<T>, or IList<T>
        if (genericDef.FullName == SpecialCases.SharpyListFullName ||
            IsGenericTypeDefinition(genericDef, typeof(List<>)) ||
            IsGenericTypeDefinition(genericDef, typeof(IList<>)))
        {
            return new GenericType
            {
                Name = BuiltinNames.List,
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0])
                }
            };
        }

        // Sharpy.Dict<K, V>, Dictionary<K, V>, or IDictionary<K, V>
        if (genericDef.FullName == SpecialCases.SharpyDictFullName ||
            IsGenericTypeDefinition(genericDef, typeof(Dictionary<,>)) ||
            IsGenericTypeDefinition(genericDef, typeof(IDictionary<,>)))
        {
            return new GenericType
            {
                Name = BuiltinNames.Dict,
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0]),
                    MapClrTypeToSemanticType(typeArgs[1])
                }
            };
        }

        // Sharpy.Set<T>, HashSet<T>, or ISet<T>
        if (genericDef.FullName == SpecialCases.SharpySetFullName ||
            IsGenericTypeDefinition(genericDef, typeof(HashSet<>)) ||
            IsGenericTypeDefinition(genericDef, typeof(ISet<>)))
        {
            return new GenericType
            {
                Name = BuiltinNames.Set,
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0])
                }
            };
        }

        // IReadOnlyList<T> or IReadOnlyCollection<T>
        if (IsGenericTypeDefinition(genericDef, typeof(IReadOnlyList<>)) ||
            IsGenericTypeDefinition(genericDef, typeof(IReadOnlyCollection<>)))
        {
            return new GenericType
            {
                Name = BuiltinNames.List,
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0])
                }
            };
        }

        // IReadOnlyDictionary<K, V>
        if (IsGenericTypeDefinition(genericDef, typeof(IReadOnlyDictionary<,>)))
        {
            return new GenericType
            {
                Name = BuiltinNames.Dict,
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0]),
                    MapClrTypeToSemanticType(typeArgs[1])
                }
            };
        }

        // IEnumerable<T>
        if (IsGenericTypeDefinition(genericDef, typeof(IEnumerable<>)))
        {
            return new GenericType
            {
                // Note: IEnumerable<T> is mapped to list for simplicity, losing lazy evaluation semantics.
                // This is a known limitation - iterables are treated as eager lists in the type system.
                Name = BuiltinNames.List,
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0])
                }
            };
        }

        // Task<T>
        if (IsGenericTypeDefinition(genericDef, typeof(System.Threading.Tasks.Task<>)))
        {
            return new TaskType
            {
                ResultType = MapClrTypeToSemanticType(typeArgs[0])
            };
        }

        // Sharpy.Optional<T>
        if (genericDef.FullName == SpecialCases.SharpyOptionalFullName)
        {
            return new OptionalType
            {
                UnderlyingType = MapClrTypeToSemanticType(typeArgs[0])
            };
        }

        // Sharpy.Result<T, E>
        if (genericDef.FullName == SpecialCases.SharpyResultFullName)
        {
            return new ResultType
            {
                OkType = MapClrTypeToSemanticType(typeArgs[0]),
                ErrorType = MapClrTypeToSemanticType(typeArgs[1])
            };
        }

        // Sharpy.DictItemsView<K, V>
        if (genericDef.FullName == SpecialCases.DictItemsViewFullName)
        {
            return new GenericType
            {
                Name = BuiltinNames.DictItemsView,
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0]),
                    MapClrTypeToSemanticType(typeArgs[1])
                }
            };
        }

        // Sharpy.DictKeyView<K, V>
        if (genericDef.FullName == SpecialCases.DictKeyViewFullName)
        {
            return new GenericType
            {
                Name = BuiltinNames.DictKeyView,
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0]),
                    MapClrTypeToSemanticType(typeArgs[1])
                }
            };
        }

        // Sharpy.DictValuesView<K, V>
        if (genericDef.FullName == SpecialCases.DictValuesViewFullName)
        {
            return new GenericType
            {
                Name = BuiltinNames.DictValuesView,
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0]),
                    MapClrTypeToSemanticType(typeArgs[1])
                }
            };
        }

        // KeyValuePair<K, V> -> tuple[K, V]
        if (genericDef.FullName == SpecialCases.KeyValuePairFullName)
        {
            return new TupleType
            {
                ElementTypes = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0]),
                    MapClrTypeToSemanticType(typeArgs[1])
                }
            };
        }

        // IComparer<T> — preserve as CLR interface type
        if (IsGenericTypeDefinition(genericDef, typeof(IComparer<>)))
        {
            return new GenericType
            {
                Name = "IComparer",
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0])
                }
            };
        }

        // IEqualityComparer<T> — preserve as CLR interface type
        if (IsGenericTypeDefinition(genericDef, typeof(IEqualityComparer<>)))
        {
            return new GenericType
            {
                Name = "IEqualityComparer",
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0])
                }
            };
        }

        // Tuple types
        if (genericDef.FullName?.StartsWith("System.Tuple") == true ||
            genericDef.FullName?.StartsWith("System.ValueTuple") == true)
        {
            return new TupleType
            {
                ElementTypes = typeArgs
                    .Select(MapClrTypeToSemanticType)
                    .ToList()
            };
        }

        // Func<T1,...,TResult> -> FunctionType
        if (genericDef.FullName?.StartsWith("System.Func`") == true)
        {
            // Last type arg is the return type, rest are parameter types
            var paramTypes = typeArgs.Take(typeArgs.Length - 1)
                .Select(MapClrTypeToSemanticType)
                .ToList();
            var returnType = MapClrTypeToSemanticType(typeArgs[typeArgs.Length - 1]);
            return new FunctionType
            {
                ParameterTypes = paramTypes,
                ReturnType = returnType
            };
        }

        // Action<T1,...> -> FunctionType with void return
        if (genericDef.FullName?.StartsWith("System.Action`") == true)
        {
            var paramTypes = typeArgs
                .Select(MapClrTypeToSemanticType)
                .ToList();
            return new FunctionType
            {
                ParameterTypes = paramTypes,
                ReturnType = SemanticType.Void
            };
        }

        // NdArray<T> — first-class stdlib generic backed by CLR; needs GenericDefinition
        // so GetClrType can construct closed generics for operator resolution (#968, #971).
        if (genericDef.FullName == SpecialCases.SharpyNdArrayFullName)
        {
            var defSymbol = new TypeSymbol
            {
                Name = "NdArray",
                Kind = SymbolKind.Type,
                TypeKind = TypeKind.Class,
                ClrType = genericDef
            };
            return new GenericType
            {
                Name = "NdArray",
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0])
                },
                GenericDefinition = defSymbol
            };
        }

        // Unknown generic type - fallback to object
        return SemanticType.Object;
    }

    private bool IsGenericTypeDefinition(Type type, Type genericTypeDef)
    {
        return type == genericTypeDef;
    }

    /// <summary>
    /// Cache of minimal interface TypeSymbols keyed by the open-generic (or non-generic)
    /// CLR interface definition, so all mapped types implementing the same interface share
    /// one definition symbol.
    /// </summary>
    private readonly ConcurrentDictionary<Type, TypeSymbol> _interfaceSymbolCache = new();

    /// <summary>
    /// Types whose interface lists are currently being built on this thread. Guards against
    /// infinite recursion through self-referential interface arguments
    /// (e.g., <c>Foo : IEquatable&lt;Foo&gt;</c>) and indirect cycles between types.
    /// </summary>
    [ThreadStatic]
    private static HashSet<Type>? _interfaceListInProgress;

    /// <summary>
    /// Builds the interface list for a CLR type with resolved type arguments (#827).
    /// All public CLR interfaces are enumerated; generic interfaces carry
    /// ResolvedTypeArguments. Non-generic duplicates of generic forms (IEnumerable vs
    /// IEnumerable&lt;T&gt;) are filtered out.
    /// </summary>
    private List<InterfaceReference> BuildInterfaceList(Type clrType)
    {
        var result = new List<InterfaceReference>();

        _interfaceListInProgress ??= new HashSet<Type>();
        if (!_interfaceListInProgress.Add(clrType))
            return result; // Re-entrant on the same type — break the cycle

        try
        {
            var allInterfaces = clrType.GetInterfaces()
                .Where(IsPublicInterface)
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

                    result.Add(new InterfaceReference
                    {
                        Definition = GetOrCreateInterfaceSymbol(iface)
                    });
                    continue;
                }

                var resolvedArgs = iface.GetGenericArguments()
                    .Select(arg => MapInterfaceArgument(arg, clrType))
                    .ToImmutableArray();

                result.Add(new InterfaceReference
                {
                    Definition = GetOrCreateInterfaceSymbol(iface.GetGenericTypeDefinition()),
                    ResolvedTypeArguments = resolvedArgs
                });
            }

            return result;
        }
        finally
        {
            _interfaceListInProgress.Remove(clrType);
        }
    }

    /// <summary>
    /// Maps a CLR interface type argument to a SemanticType. Self-referential arguments
    /// (e.g., <c>Foo</c> in <c>Foo : IEquatable&lt;Foo&gt;</c>) and types whose interface
    /// lists are currently being built map to a shallow UserDefinedType to avoid recursing
    /// back through <see cref="MapTypeInternal"/>.
    /// </summary>
    private SemanticType MapInterfaceArgument(Type arg, Type declaringType)
    {
        if (arg == declaringType || (_interfaceListInProgress?.Contains(arg) == true))
            return new UserDefinedType { Name = arg.Name };

        return MapClrTypeToSemanticType(arg);
    }

    /// <summary>
    /// Finds or creates the TypeSymbol for an interface definition, cached per CLR
    /// definition so all implementers share one symbol. Type parameters follow the
    /// discovery naming convention (T0, T1, ...) with CLR variance preserved.
    /// </summary>
    private TypeSymbol GetOrCreateInterfaceSymbol(Type interfaceDef)
    {
        return _interfaceSymbolCache.GetOrAdd(interfaceDef, static def =>
        {
            var clrArgs = def.IsGenericTypeDefinition
                ? def.GetGenericArguments()
                : Type.EmptyTypes;
            var typeParams = clrArgs
                .Select((arg, i) => new TypeParameterDef { Name = $"T{i}", Variance = GetClrVariance(arg) })
                .ToList();

            return new TypeSymbol
            {
                Name = ClrNameHelper.StripArity(def.Name),
                Kind = SymbolKind.Type,
                TypeKind = TypeKind.Interface,
                ClrType = def,
                TypeParameters = typeParams,
                AccessLevel = AccessLevel.Public
            };
        });
    }

    /// <summary>
    /// Returns true when the interface (or its generic definition) is publicly visible.
    /// </summary>
    internal static bool IsPublicInterface(Type iface)
    {
        var def = iface.IsGenericType ? iface.GetGenericTypeDefinition() : iface;
        return def.IsVisible;
    }

    /// <summary>
    /// Reads the declared CLR variance (out/in) of a generic parameter.
    /// </summary>
    internal static TypeParameterVariance GetClrVariance(Type genericParameter)
    {
        return (genericParameter.GenericParameterAttributes & GenericParameterAttributes.VarianceMask) switch
        {
            GenericParameterAttributes.Covariant => TypeParameterVariance.Covariant,
            GenericParameterAttributes.Contravariant => TypeParameterVariance.Contravariant,
            _ => TypeParameterVariance.None
        };
    }
}
