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
        internal const string SharpyFrozenSetFullName = CSharpTypeNames.SharpyFrozenSet + "`1";
        internal const string SharpyFrozenDictFullName = CSharpTypeNames.SharpyFrozenDict + "`2";
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
        /// The Sharpy collection <em>wrapper</em> types constructed as <c>new Sharpy.X&lt;T&gt;(...)</c>
        /// — the exact set the emitter's collection-constructor lowering recognizes (this absorbed the
        /// former <c>CSharpTypeNames.FromSharpyName</c>). Deliberately excludes <c>bytes</c>,
        /// <c>tuple</c>, and template, which have bespoke construction paths.
        /// </summary>
        private static readonly ImmutableDictionary<string, string> _wrapperCollectionCSharpNames =
            new Dictionary<string, string>
            {
                [BuiltinNames.List] = CSharpTypeNames.SharpyList,
                [BuiltinNames.Dict] = CSharpTypeNames.SharpyDict,
                [BuiltinNames.Set] = CSharpTypeNames.SharpySet,
                [BuiltinNames.DefaultDict] = CSharpTypeNames.SharpyDefaultDict,
                ["DefaultDict"] = CSharpTypeNames.SharpyDefaultDict,
                [BuiltinNames.FrozenDict] = CSharpTypeNames.SharpyFrozenDict,
                ["FrozenDict"] = CSharpTypeNames.SharpyFrozenDict,
                [BuiltinNames.FrozenSet] = CSharpTypeNames.SharpyFrozenSet,
                ["FrozenSet"] = CSharpTypeNames.SharpyFrozenSet,
            }.ToImmutableDictionary();

        /// <summary>
        /// Reverse direction: Sharpy builtin/collection type name -&gt; fully-qualified C# type name.
        /// The single source for the collection correspondence that previously lived in
        /// <c>CSharpTypeNames.FromSharpyName</c> and <c>TypeSyntaxMapper</c>'s static map. This is the
        /// superset of <see cref="_wrapperCollectionCSharpNames"/> plus the collection-shaped builtins
        /// that appear in type annotations (<c>bytes</c>, template, <c>tuple</c>); it is derived from
        /// the wrapper set so no name literal is written twice.
        /// Primitives are <em>not</em> listed here — they are delegated to <see cref="PrimitiveCatalog"/>.
        /// </summary>
        private static readonly ImmutableDictionary<string, string> _collectionCSharpNames =
            _wrapperCollectionCSharpNames
                .Add(BuiltinNames.Bytes, CSharpTypeNames.SharpyBytes)
                .Add(BuiltinNames.Template, CSharpTypeNames.SharpyTemplate)
                .Add(BuiltinNames.Tuple, "System.ValueTuple");

        /// <summary>
        /// Returns the fully-qualified C# type name for a Sharpy collection type name (as used in a
        /// type annotation), or <c>null</c> when the name is not a known collection.
        /// </summary>
        internal static string? TryGetCSharpCollectionName(string sharpyName)
            => _collectionCSharpNames.GetValueOrDefault(sharpyName);

        /// <summary>
        /// Returns the fully-qualified C# type name for a Sharpy collection <em>wrapper</em>
        /// constructed via <c>new Sharpy.X&lt;T&gt;(...)</c> (list/dict/set/defaultdict/frozendict),
        /// or <c>null</c> otherwise. This is the set the emitter's collection-constructor lowering
        /// keys off (formerly <c>CSharpTypeNames.FromSharpyName</c>).
        /// </summary>
        internal static string? TryGetWrapperCollectionName(string sharpyName)
            => _wrapperCollectionCSharpNames.GetValueOrDefault(sharpyName);

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
    /// <remarks>
    /// The overload index cache key now includes compiler identity
    /// (<c>AssemblyIdentity.CompilerVersion</c>), so a rebuilt compiler automatically
    /// invalidates every index (#1313). Manual <c>CurrentCacheFormatVersion</c> bumps are
    /// still needed for *shape* changes (new/removed serialized fields), but mapping changes
    /// here are covered structurally and no longer require a bump.
    /// </remarks>
    public SemanticType MapClrTypeToSemanticType(Type clrType)
    {
        // Use GetOrAdd for thread-safe caching
        return _typeCache.GetOrAdd(clrType, MapTypeInternal);
    }

    /// <summary>
    /// Maps a reflected type appearing in PARAMETER position, where an <c>IEnumerable&lt;T&gt;</c>
    /// keeps its own identity instead of collapsing onto <c>list[T]</c> (#1450).
    /// </summary>
    /// <remarks>
    /// <para>Position matters because the two directions want opposite things. A RETURNED
    /// <c>IEnumerable&lt;T&gt;</c> becomes <c>list[T]</c> so the value is an ordinary Sharpy
    /// collection with Sharpy methods (#1354's owner decision governs that direction). A PARAMETER
    /// must stay as WIDE as .NET declared it: collapsing it narrowed the accepted set from "any
    /// <c>IEnumerable&lt;int&gt;</c>" to "a Sharpy list", so a synthesized forwarder emitted
    /// <c>IntList(Sharpy.List&lt;int&gt;)</c>, a CLR <c>List&lt;int&gt;</c> argument matched no
    /// overload, C# fell back to the <c>int</c> one, and the result was CS1503 behind SPY0908.
    /// Axiom 1 decides: .NET says this position takes any sequence.</para>
    ///
    /// <para>Sharpy callers lose nothing — <c>Sharpy.List&lt;T&gt;</c> implements
    /// <c>IEnumerable&lt;T&gt;</c>, so a list literal binds exactly as before. The identity is
    /// carried the way <c>IOrderedEnumerable&lt;T&gt;</c> carries its own (a <see cref="GenericType"/>
    /// with the CLR <c>GenericDefinition</c>), so <c>TryGetClrType</c> reconstructs the real
    /// interface and the emitter writes the .NET type rather than a Sharpy wrapper.</para>
    /// </remarks>
    public SemanticType MapClrParameterTypeToSemanticType(Type clrType)
    {
        if (clrType.IsGenericType
            && !clrType.IsGenericTypeDefinition
            && IsGenericTypeDefinition(clrType.GetGenericTypeDefinition(), typeof(IEnumerable<>)))
        {
            var genericDef = clrType.GetGenericTypeDefinition();
            return new GenericType
            {
                Name = ClrNameHelper.StripArity(genericDef.Name),
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(clrType.GetGenericArguments()[0])
                },
                GenericDefinition = GetOrCreateClrDefinitionSymbol(genericDef)
            };
        }

        return MapClrTypeToSemanticType(clrType);
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
                BuiltinNames.Int32 => SemanticType.Int,
                BuiltinNames.Int64 => SemanticType.Long,
                BuiltinNames.Float32 => SemanticType.Float32,
                BuiltinNames.Float64 => SemanticType.Double,
                BuiltinNames.Bool => SemanticType.Bool,
                BuiltinNames.Str => SemanticType.Str,
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
                return new UnmappedClrType { ClrTypeName = clrType.FullName ?? clrType.Name, ClrType = clrType };

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

        // Non-generic CLR class/struct/delegate: map to UserDefinedType with CLR type info
        if (clrType.IsClass || clrType.IsValueType)
        {
            var isDelegate = IsClrDelegateType(clrType);
            var symbol = new TypeSymbol
            {
                Name = clrType.Name,
                Kind = SymbolKind.Type,
                TypeKind = isDelegate ? TypeKind.Delegate
                    : clrType.IsValueType ? TypeKind.Struct
                    : TypeKind.Class,
                ClrType = clrType,
                Interfaces = BuildInterfaceList(clrType)
            };

            if (isDelegate)
            {
                var invoke = SynthesizeDelegateInvoke(clrType);
                if (invoke != null)
                    symbol.Methods.Add(invoke);
            }

            return new UserDefinedType
            {
                Name = clrType.Name,
                Symbol = symbol
            };
        }

        return new UnmappedClrType { ClrTypeName = clrType.FullName ?? clrType.Name, ClrType = clrType };
    }

    /// <summary>
    /// Maps a closed CLR generic to its Sharpy form, stamping the result with the CLR generic
    /// definition it came from (<see cref="GenericType.ClrOriginTypeName"/>).
    ///
    /// <para>
    /// The stamp is applied HERE, to whatever <see cref="MapGenericTypeCore"/> returns, rather than
    /// inside each arm. Eight arms currently collapse a CLR type onto a Sharpy collection name —
    /// <c>List/IList/ICollection/Sharpy.List</c>, <c>Dictionary/IDictionary/Sharpy.Dict</c>,
    /// <c>HashSet/ISet/Sharpy.Set</c>, <c>Sharpy.FrozenSet</c>, <c>Sharpy.FrozenDict</c>,
    /// <c>IReadOnlyList/IReadOnlyCollection</c>, <c>IReadOnlyDictionary</c> and <c>IEnumerable</c> —
    /// and stamping one and not its siblings is the
    /// parallel-site defect this provenance exists to close (#1145, #1260). Wrapping the whole switch
    /// makes the sweep exhaustive by construction: a future arm is stamped whether or not its author
    /// knows provenance exists.
    /// </para>
    ///
    /// <para>
    /// The count above is descriptive, not load-bearing — the wrapper is what guarantees coverage, and
    /// the arm SET is tracked by probing this method rather than by any hand-written list (see
    /// <c>ClrBridgeArmProbe</c> in the tests). Three arms were added without updating the prose that
    /// claimed to enumerate them, which is exactly why nothing downstream is allowed to depend on it.
    /// </para>
    /// </summary>
    private SemanticType MapGenericType(Type clrType)
    {
        var mapped = MapGenericTypeCore(clrType);

        // Only GenericType can carry provenance, and only a name-collapsing mapping needs it: the
        // arms that produce a distinct shape (TupleType, FunctionType, TaskType, Optional, Result)
        // lose no CLR identity, and `object` means the bridge already gave up.
        return mapped is GenericType generic
            ? generic with { ClrOriginTypeName = clrType.GetGenericTypeDefinition().FullName }
            : mapped;
    }

    private SemanticType MapGenericTypeCore(Type clrType)
    {
        var genericDef = clrType.GetGenericTypeDefinition();
        var typeArgs = clrType.GetGenericArguments();

        // Sharpy.List<T> — the Sharpy wrapper IS list.
        if (genericDef.FullName == SpecialCases.SharpyListFullName)
        {
            return new GenericType
            {
                Name = BuiltinNames.List,
                TypeArguments = new List<SemanticType> { MapClrTypeToSemanticType(typeArgs[0]) }
            };
        }

        // Concrete SCG List<T> — honest identity (#1517).
        if (IsGenericTypeDefinition(genericDef, typeof(List<>)))
        {
            return new GenericType
            {
                Name = ClrNameHelper.StripArity(genericDef.Name),
                TypeArguments = new List<SemanticType> { MapClrTypeToSemanticType(typeArgs[0]) },
                GenericDefinition = GetOrCreateClrDefinitionSymbol(genericDef)
            };
        }

        // IList<T>, ICollection<T> — interfaces stay assimilated (Design Decision 1, #1517).
        if (IsGenericTypeDefinition(genericDef, typeof(IList<>)) ||
            IsGenericTypeDefinition(genericDef, typeof(ICollection<>)))
        {
            return new GenericType
            {
                Name = BuiltinNames.List,
                TypeArguments = new List<SemanticType> { MapClrTypeToSemanticType(typeArgs[0]) }
            };
        }

        // Sharpy.Dict<K, V> — the Sharpy wrapper IS dict.
        if (genericDef.FullName == SpecialCases.SharpyDictFullName)
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

        // Concrete Dictionary<K,V> — honest identity (#1517).
        if (IsGenericTypeDefinition(genericDef, typeof(Dictionary<,>)))
        {
            return new GenericType
            {
                Name = ClrNameHelper.StripArity(genericDef.Name),
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0]),
                    MapClrTypeToSemanticType(typeArgs[1])
                },
                GenericDefinition = GetOrCreateClrDefinitionSymbol(genericDef)
            };
        }

        // IDictionary<K,V> — interface stays assimilated.
        if (IsGenericTypeDefinition(genericDef, typeof(IDictionary<,>)))
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

        // Sharpy.Set<T> — the Sharpy wrapper IS set.
        if (genericDef.FullName == SpecialCases.SharpySetFullName)
        {
            return new GenericType
            {
                Name = BuiltinNames.Set,
                TypeArguments = new List<SemanticType> { MapClrTypeToSemanticType(typeArgs[0]) }
            };
        }

        // Concrete HashSet<T> — honest identity (#1517).
        if (IsGenericTypeDefinition(genericDef, typeof(HashSet<>)))
        {
            return new GenericType
            {
                Name = ClrNameHelper.StripArity(genericDef.Name),
                TypeArguments = new List<SemanticType> { MapClrTypeToSemanticType(typeArgs[0]) },
                GenericDefinition = GetOrCreateClrDefinitionSymbol(genericDef)
            };
        }

        // ISet<T> — interface stays assimilated.
        if (IsGenericTypeDefinition(genericDef, typeof(ISet<>)))
        {
            return new GenericType
            {
                Name = BuiltinNames.Set,
                TypeArguments = new List<SemanticType> { MapClrTypeToSemanticType(typeArgs[0]) }
            };
        }

        // Sharpy.FrozenSet<T>
        if (genericDef.FullName == SpecialCases.SharpyFrozenSetFullName)
        {
            return new GenericType
            {
                Name = BuiltinNames.FrozenSet,
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0])
                }
            };
        }

        // Sharpy.FrozenDict<K, V> (#1310)
        if (genericDef.FullName == SpecialCases.SharpyFrozenDictFullName)
        {
            return new GenericType
            {
                Name = BuiltinNames.FrozenDict,
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0]),
                    MapClrTypeToSemanticType(typeArgs[1])
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

        // IOrderedEnumerable<T> — kept as ITSELF, not collapsed onto list[T] (#1390).
        // Must precede IEnumerable because IOrderedEnumerable extends IEnumerable.
        //
        // The #1332 arm mapped it to list[T] like every other sequence. That is the one collapse the
        // Sharpy wrapper cannot honour: `Sharpy.List<T>` implements IEnumerable/IList/ICollection, so
        // materializing any of those into it loses nothing an extension method could want — but it is
        // NOT an IOrderedEnumerable<T>, and `ThenBy`/`ThenByDescending` accept nothing else. Calling the
        // ordered sequence a list therefore made `xs = lst.order_by(f)` bind a Sharpy list (#1251) and
        // left `xs.then_by(g)` with no receiver any `ThenBy` overload accepts — CS0411 behind SPY0908,
        // against ParallelEnumerable's overload, which is merely the candidate C# names when none fit.
        //
        // Carrying the CLR definition (the IComparer/IEqualityComparer and Sharpy-interface precedents
        // below and above) keeps the identity: TryGetClrType reconstructs IOrderedEnumerable<T> from
        // GenericDefinition, so `then_by` binds on a slotted receiver exactly as it does mid-chain, and
        // the materialization ring leaves the value alone because its name is not a collection's.
        if (IsGenericTypeDefinition(genericDef, typeof(IOrderedEnumerable<>)))
        {
            return new GenericType
            {
                Name = ClrNameHelper.StripArity(genericDef.Name),
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0])
                },
                GenericDefinition = GetOrCreateClrDefinitionSymbol(genericDef)
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

        // The dict views (Sharpy.DictKeyView / DictItemsView / DictValuesView).
        //
        // GenericDefinition is what makes the EXPRESSION path (`d.keys()`, mapped here) agree with
        // the ANNOTATION path (`x: DictKeyView[str, int]`, built by TypeResolver, which sets it from
        // the resolved symbol). Everything downstream that reconstructs the closed CLR type —
        // GetClrType -> ClrTypeHelper.TryConstructClosedGeneric, and so operator resolution — reads
        // GenericDefinition and nothing else. Omitting it here is why `d.keys() | e.keys()` was
        // refused (SPY0222) while the annotation-typed spelling of the same value was accepted:
        // one type value carried the CLR identity and the other didn't (#1496).
        //
        // Same shape as the NdArray and Sharpy-interface arms; the standing agreement sweep
        // (AnnotationExpressionPathAgreementConformanceTests) is what keeps the two paths honest.
        var viewName = genericDef.FullName switch
        {
            SpecialCases.DictItemsViewFullName => BuiltinNames.DictItemsView,
            SpecialCases.DictKeyViewFullName => BuiltinNames.DictKeyView,
            SpecialCases.DictValuesViewFullName => BuiltinNames.DictValuesView,
            _ => null
        };
        if (viewName != null)
        {
            return new GenericType
            {
                Name = viewName,
                TypeArguments = new List<SemanticType>
                {
                    MapClrTypeToSemanticType(typeArgs[0]),
                    MapClrTypeToSemanticType(typeArgs[1])
                },
                GenericDefinition = GetOrCreateClrDefinitionSymbol(genericDef)
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

        // A generic interface Sharpy.Core owns, with no arm of its own above. Mapping it to `object`
        // is a silent degradation: nothing fails until a user calls a member on the result, which is
        // how #1210 surfaced with FrozenSet. `IReverseEnumerable<T>` — the `__reversed__` protocol
        // contract — was the one such interface left, and it kept the CoreGenericTypeMapping guard's
        // last allowlist entry alive (#1242).
        //
        // Scoped to Sharpy.Core's own interfaces rather than made a general fallback: mapping every
        // unknown generic to a synthesized symbol would quietly change how a large set of BCL types
        // are typed, which is a much larger decision than this one. GenericDefinition carries the CLR
        // definition, so the closed type stays constructible for operator and overload resolution —
        // the same shape the NdArray arm above uses.
        if (genericDef is { IsInterface: true, Namespace: "Sharpy" })
        {
            return new GenericType
            {
                Name = ClrNameHelper.StripArity(genericDef.Name),
                TypeArguments = typeArgs.Select(MapClrTypeToSemanticType).ToList(),
                GenericDefinition = GetOrCreateClrDefinitionSymbol(genericDef)
            };
        }

        return new UnmappedClrType
        {
            ClrTypeName = clrType.GetGenericTypeDefinition().FullName ?? clrType.Name,
            ClrType = clrType
        };
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
    private static bool IsClrDelegateType(Type clrType)
        => typeof(MulticastDelegate).IsAssignableFrom(clrType)
           && clrType != typeof(Delegate)
           && clrType != typeof(MulticastDelegate);

    private FunctionSymbol? SynthesizeDelegateInvoke(Type clrType)
    {
        var invokeMethod = clrType.GetMethod("Invoke");
        if (invokeMethod == null)
            return null;

        var parameters = new List<ParameterSymbol>();
        foreach (var param in invokeMethod.GetParameters())
        {
            parameters.Add(new ParameterSymbol
            {
                Name = param.Name ?? $"arg{param.Position}",
                Type = MapClrParameterTypeToSemanticType(param.ParameterType),
                HasDefault = param.HasDefaultValue
            });
        }

        return new FunctionSymbol
        {
            Name = "Invoke",
            Kind = SymbolKind.Function,
            ReturnType = invokeMethod.ReturnType == typeof(void)
                ? SemanticType.Void
                : MapClrTypeToSemanticType(invokeMethod.ReturnType),
            Parameters = parameters,
            AccessLevel = AccessLevel.Public,
            ClrMethod = invokeMethod
        };
    }

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
                        Definition = GetOrCreateClrDefinitionSymbol(iface)
                    });
                    continue;
                }

                var resolvedArgs = iface.GetGenericArguments()
                    .Select(arg => MapInterfaceArgument(arg, clrType))
                    .ToImmutableArray();

                result.Add(new InterfaceReference
                {
                    Definition = GetOrCreateClrDefinitionSymbol(iface.GetGenericTypeDefinition()),
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
    /// Finds or creates the TypeSymbol for a CLR type definition, cached per CLR definition so all
    /// users share one symbol. Type parameters follow the discovery naming convention (T0, T1, ...)
    /// with CLR variance preserved; <see cref="TypeSymbol.TypeKind"/> is read off the CLR type
    /// rather than assumed, so the same helper serves interfaces and the concrete definitions
    /// (the dict views) whose arms also need to carry a <c>GenericDefinition</c>.
    /// </summary>
    private TypeSymbol GetOrCreateClrDefinitionSymbol(Type clrDef)
    {
        return _interfaceSymbolCache.GetOrAdd(clrDef, static def =>
        {
            var clrArgs = def.IsGenericTypeDefinition
                ? def.GetGenericArguments()
                : Type.EmptyTypes;
            var typeParams = clrArgs
                .Select((arg, i) => new TypeParameterDef { Name = $"T{i}", Variance = GetClrVariance(arg) })
                .ToList();

            var isDelegate = IsClrDelegateType(def);
            var sym = new TypeSymbol
            {
                Name = ClrNameHelper.StripArity(def.Name),
                Kind = SymbolKind.Type,
                TypeKind = isDelegate ? TypeKind.Delegate
                    : def.IsInterface ? TypeKind.Interface
                    : def.IsEnum ? TypeKind.Enum
                    : def.IsValueType ? TypeKind.Struct
                    : TypeKind.Class,
                ClrType = def,
                TypeParameters = typeParams,
                AccessLevel = AccessLevel.Public
            };

            if (isDelegate)
            {
                var invokeMethod = def.GetMethod("Invoke");
                if (invokeMethod != null)
                {
                    var bridge = new ClrTypeBridge();
                    var invokeParams = invokeMethod.GetParameters().Select(p => new ParameterSymbol
                    {
                        Name = p.Name ?? $"arg{p.Position}",
                        Type = bridge.MapClrParameterTypeToSemanticType(p.ParameterType),
                        HasDefault = p.HasDefaultValue
                    }).ToList();

                    sym.Methods.Add(new FunctionSymbol
                    {
                        Name = "Invoke",
                        Kind = SymbolKind.Function,
                        ReturnType = invokeMethod.ReturnType == typeof(void)
                            ? SemanticType.Void
                            : bridge.MapClrTypeToSemanticType(invokeMethod.ReturnType),
                        Parameters = invokeParams,
                        AccessLevel = AccessLevel.Public,
                        ClrMethod = invokeMethod
                    });
                }
            }

            return sym;
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
