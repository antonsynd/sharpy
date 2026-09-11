using System.Linq;
using System.Reflection;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// Reads the nullable-reference-type annotation a CLR member DECLARES (<c>string?</c>,
/// <c>Dict&lt;string, string&gt;? Proxies</c>) and wraps the member's mapped type in
/// <see cref="NullableType"/> when the declaration says null is a value of it. A reflected
/// <see cref="System.Type"/> carries no NRT information — <c>string?</c> and <c>string</c> are the
/// same <c>typeof(string)</c> — so the annotation must be read from the MEMBER through
/// <see cref="NullabilityInfoContext"/>, at the one point every member-typed reflection site maps
/// through; otherwise a member declared nullable is typed non-nullable and a store of <c>None</c>
/// into it is refused (SPY0229) for a value C# accepts (#1705).
/// </summary>
/// <remarks>
/// <para>
/// The declaration is read as a TREE, not a flag: <c>List&lt;object?&gt;</c> is
/// <c>list[object | None]</c> and <c>IDictionary&lt;string, string?&gt;</c> is
/// <c>dict[str, str | None]</c>. Reading only the top-level state described
/// <c>Yaml.safe_load_all</c> as <c>list[object]</c>, and under list invariance the faithful
/// <c>list[object | None]</c> spelling a caller must write was then refused (#1847). An assembly
/// without NRT annotations reports <see cref="NullabilityState.Unknown"/> at every position, which
/// is treated as non-nullable — exactly the type Sharpy reads today, so unannotated surfaces do not
/// move.
/// </para>
/// <para>
/// A value-typed <c>Nullable&lt;T&gt;</c> is already a distinct <see cref="System.Type"/> and is mapped
/// to <see cref="NullableType"/> by <see cref="ClrTypeBridge.MapClrTypeToSemanticType"/>; it is not
/// wrapped twice here.
/// </para>
/// </remarks>
internal static class ClrDeclaredNullability
{
    // NullabilityInfoContext caches per-member and is not thread-safe; compilations run in
    // parallel under the test host, so one context is shared behind a gate.
    private static readonly NullabilityInfoContext Context = new();
    private static readonly object Gate = new();

    /// <summary>The nullability the property DECLARES, at every position of its type.</summary>
    internal static ClrNullabilityShape Describe(PropertyInfo property)
    {
        if (DeclaredAsGenericParameter(property, m => ((PropertyInfo)m).PropertyType,
                m => ((PropertyInfo)m).CustomAttributes, out var annotated))
            return annotated ? ClrNullabilityShape.TopLevel : ClrNullabilityShape.Oblivious;

        return Shape(Create(() => Context.Create(property)), EitherDirection);
    }

    /// <summary>The nullability the field DECLARES, at every position of its type.</summary>
    internal static ClrNullabilityShape Describe(FieldInfo field)
    {
        if (DeclaredAsGenericParameter(field, m => ((FieldInfo)m).FieldType,
                m => ((FieldInfo)m).CustomAttributes, out var annotated))
            return annotated ? ClrNullabilityShape.TopLevel : ClrNullabilityShape.Oblivious;

        return Shape(Create(() => Context.Create(field)), EitherDirection);
    }

    /// <summary>The nullability the method's RETURN declares, at every position of its type.</summary>
    internal static ClrNullabilityShape DescribeReturn(MethodInfo method)
        => DeclaredAsGenericParameter(method, m => ((MethodInfo)m).ReturnType,
                m => ((MethodInfo)m).ReturnParameter.CustomAttributes, out var annotated)
            ? (annotated ? ClrNullabilityShape.TopLevel : ClrNullabilityShape.Oblivious)
            : Shape(Create(() => Context.Create(method.ReturnParameter)), i => i.ReadState == NullabilityState.Nullable);

    /// <summary>The nullability the parameter declares, at every position of its type.</summary>
    internal static ClrNullabilityShape DescribeArgument(ParameterInfo parameter)
    {
        if (parameter.Member is MethodBase method
            && DeclaredAsGenericParameter(method,
                m => ((MethodBase)m).GetParameters()[parameter.Position].ParameterType,
                m => ((MethodBase)m).GetParameters()[parameter.Position].CustomAttributes,
                out var annotated))
        {
            return annotated ? ClrNullabilityShape.TopLevel : ClrNullabilityShape.Oblivious;
        }

        return Shape(Create(() => Context.Create(parameter)), i => i.WriteState == NullabilityState.Nullable);
    }

    /// <summary>Whether reading the property yields a value declared nullable, or writing one accepts null.</summary>
    internal static bool DeclaresNullable(PropertyInfo property) => Describe(property).Nullable;

    /// <summary>Whether reading the field yields a value declared nullable, or writing one accepts null.</summary>
    internal static bool DeclaresNullable(FieldInfo field) => Describe(field).Nullable;

    /// <summary>Whether the method's return value is declared nullable.</summary>
    internal static bool DeclaresNullableReturn(MethodInfo method) => DescribeReturn(method).Nullable;

    /// <summary>Whether the parameter accepts a null argument by declaration.</summary>
    internal static bool DeclaresNullableArgument(ParameterInfo parameter) => DescribeArgument(parameter).Nullable;

    /// <summary>
    /// A member-typed position reads nullable when EITHER direction declares it so — a property
    /// whose getter can return null, or whose setter accepts null, has null among its values.
    /// </summary>
    private static bool EitherDirection(NullabilityInfo info)
        => info.ReadState == NullabilityState.Nullable || info.WriteState == NullabilityState.Nullable;

    /// <summary>
    /// Builds the declared-nullability tree from a <see cref="NullabilityInfo"/>. The TOP-LEVEL state
    /// is direction-sensitive (a return reads, a parameter writes), so it is supplied by the caller;
    /// every NESTED position takes its state from the single annotation the declaration carries there,
    /// which both directions share.
    /// </summary>
    private static ClrNullabilityShape Shape(NullabilityInfo? info, System.Func<NullabilityInfo, bool> topLevel)
    {
        if (info == null)
            return ClrNullabilityShape.Oblivious;

        return ClrNullabilityShape.Create(topLevel(info), NestedShapes(info));
    }

    private static ClrNullabilityShape[] NestedShapes(NullabilityInfo info)
    {
        // An array's element and a generic's type arguments are the same kind of nested position;
        // NullabilityInfo exposes them through different members, so both are read here rather than
        // at each call site.
        var children = info.ElementType is { } element
            ? new[] { element }
            : info.GenericTypeArguments;

        if (children.Length == 0)
            return System.Array.Empty<ClrNullabilityShape>();

        var shapes = new ClrNullabilityShape[children.Length];
        for (var i = 0; i < children.Length; i++)
            shapes[i] = ClrNullabilityShape.Create(EitherDirection(children[i]), NestedShapes(children[i]));

        return shapes;
    }

    /// <summary>
    /// Whether the parameter explicitly declares it does NOT accept null (NRT-annotated non-nullable).
    /// Oblivious/un-annotated parameters return false — they accept <c>None</c> conservatively.
    /// </summary>
    internal static bool DeclaresNonNullableArgument(ParameterInfo parameter)
    {
        if (parameter.Member is MethodBase method
            && DeclaredAsGenericParameter(method,
                m => ((MethodBase)m).GetParameters()[parameter.Position].ParameterType,
                m => ((MethodBase)m).GetParameters()[parameter.Position].CustomAttributes,
                out _))
            return false;
        var info = Create(() => Context.Create(parameter));
        return info?.WriteState == NullabilityState.NotNull;
    }

    /// <summary>
    /// Whether the member's type, as DECLARED on its generic type or method definition, is or contains
    /// a generic parameter (<c>T Peek()</c> on <c>Stack&lt;T&gt;</c>). Such a member's nullability is
    /// the type ARGUMENT's annotation, which a runtime-constructed <see cref="System.Type"/> does not
    /// carry — <see cref="NullabilityInfoContext"/> then answers <see cref="NullabilityState.Nullable"/>
    /// for an unconstrained <c>T</c>, and <c>Stack[Queue[int]].peek()</c> came back <c>Queue[int]?</c>.
    /// The reflected type is the only honest answer there, so the annotation is not consulted.
    /// </summary>
    /// <param name="attributesOf">
    /// The custom attributes of the DECLARING position (a property, a field, a method's return
    /// parameter, a parameter) — where C# records the declared annotation as a
    /// <c>NullableAttribute</c> byte.
    /// </param>
    /// <param name="annotatedNullable">
    /// True when the declared type is a type parameter written <c>T?</c> rather than a bare
    /// <c>T</c>. <see cref="NullabilityInfoContext"/> cannot tell them apart on a generic definition
    /// — it answers <see cref="NullabilityState.Nullable"/> for an unconstrained <c>T</c> either way
    /// (measured, <c>ClrNullabilityConsumerTotalityTests</c>) — so the suppression above swallowed
    /// the ANNOTATED case too and <c>List[str].find(...)</c> came back <c>str</c> although
    /// <c>List&lt;T&gt;.Find</c> returns <c>T?</c> (#1828). The metadata does distinguish them, and
    /// it is read here rather than inferred.
    /// </param>
    private static bool DeclaredAsGenericParameter(
        MemberInfo member,
        System.Func<MemberInfo, Type> declaredTypeOf,
        System.Func<MemberInfo, System.Collections.Generic.IEnumerable<CustomAttributeData>> attributesOf,
        out bool annotatedNullable)
    {
        annotatedNullable = false;

        try
        {
            // The member's type AS REFLECTED HERE is itself a free type parameter. That is the shape
            // discovery and the overload index see, because they reflect over the generic type
            // DEFINITION (`List<T>.Pop` returns `T`, never `string`) and over generic method
            // definitions. `NullabilityInfoContext` answers Nullable for an unconstrained `T`, so
            // without this arm every bare-`T` member is typed nullable: `list[str].pop(0)` came back
            // `str | None` and `list.append`'s `T` slot became `Box[int] | None`, which broke the
            // union constructor's inference (#1705 B1). The constructed-declaring-type arm below
            // cannot answer this case — `IsConstructedGenericType` is false for a definition.
            if (declaredTypeOf(member).ContainsGenericParameters)
            {
                annotatedNullable = IsAnnotatedTypeParameter(declaredTypeOf(member), member, attributesOf);
                return true;
            }

            if (member is MethodInfo { IsGenericMethod: true, IsGenericMethodDefinition: false } constructedMethod
                && declaredTypeOf(constructedMethod.GetGenericMethodDefinition()).ContainsGenericParameters)
            {
                var openMethod = constructedMethod.GetGenericMethodDefinition();
                annotatedNullable = IsAnnotatedTypeParameter(declaredTypeOf(openMethod), openMethod, attributesOf);
                return true;
            }

            if (member.DeclaringType is { IsConstructedGenericType: true } constructed)
            {
                var definition = constructed.GetGenericTypeDefinition();
                var declared = definition
                    .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .FirstOrDefault(m => m.MetadataToken == member.MetadataToken && m.Module == member.Module);

                if (declared == null || !declaredTypeOf(declared).ContainsGenericParameters)
                    return false;

                annotatedNullable = IsAnnotatedTypeParameter(declaredTypeOf(declared), declared, attributesOf);
                return true;
            }
        }
        catch (System.Exception ex) when (ex is System.NotSupportedException or System.InvalidOperationException
                                              or System.ArgumentException)
        {
            // A member reflection cannot map back to its definition keeps the reflected type.
            return true;
        }

        return false;
    }

    /// <summary>
    /// Whether a member whose declared type IS a bare type parameter was written <c>T?</c>.
    ///
    /// <para>C# records the declared annotation of each position as a byte on
    /// <c>System.Runtime.CompilerServices.NullableAttribute</c> — 0 oblivious, 1 not annotated,
    /// 2 annotated — defaulting to the enclosing <c>NullableContextAttribute</c>. The attribute is
    /// EMBEDDED per assembly, so it is matched by full name; its type identity differs between
    /// assemblies and a <c>typeof</c> comparison would answer false for every reference this
    /// compiler reads.</para>
    ///
    /// <para>Only a position that is EXACTLY a type parameter is answered here. A declared
    /// <c>Queue&lt;T&gt;</c> or <c>List&lt;T&gt;[]</c> spends its first byte on the outer type, and
    /// reading that byte as the type parameter's would attach the wrong annotation to the wrong
    /// position — those keep the suppression they have.</para>
    /// </summary>
    private static bool IsAnnotatedTypeParameter(
        Type declaredType,
        MemberInfo member,
        System.Func<MemberInfo, System.Collections.Generic.IEnumerable<CustomAttributeData>> attributesOf)
    {
        if (!declaredType.IsGenericParameter)
            return false;

        try
        {
            return (ExplicitNullableFlag(attributesOf(member)) ?? NullableContextFlag(member)) == AnnotatedFlag;
        }
        catch (System.Exception ex) when (ex is System.NotSupportedException or System.InvalidOperationException
                                              or System.ArgumentException or System.TypeLoadException
                                              or System.IO.FileNotFoundException)
        {
            // Metadata this reflection context cannot read keeps the bare reading, which is the
            // behaviour every declaration had before the annotation was consulted at all.
            return false;
        }
    }

    private const byte AnnotatedFlag = 2;
    private const string NullableAttributeName = "System.Runtime.CompilerServices.NullableAttribute";
    private const string NullableContextAttributeName = "System.Runtime.CompilerServices.NullableContextAttribute";

    private static byte? ExplicitNullableFlag(System.Collections.Generic.IEnumerable<CustomAttributeData> attributes)
    {
        foreach (var attribute in attributes)
        {
            if (attribute.AttributeType.FullName != NullableAttributeName
                || attribute.ConstructorArguments.Count != 1)
            {
                continue;
            }

            var argument = attribute.ConstructorArguments[0];
            if (argument.Value is byte single)
                return single;

            // The array form: byte 0 describes the whole type, which for a bare type parameter is
            // the only position there is.
            if (argument.Value is System.Collections.Generic.IReadOnlyList<CustomAttributeTypedArgument> { Count: > 0 } flags
                && flags[0].Value is byte first)
            {
                return first;
            }
        }

        return null;
    }

    /// <summary>
    /// The default annotation byte in force at <paramref name="member"/>, from the nearest enclosing
    /// <c>NullableContextAttribute</c> (the method, then its declaring types, then the module).
    /// </summary>
    private static byte? NullableContextFlag(MemberInfo member)
    {
        for (MemberInfo? scope = member; scope != null; scope = scope.DeclaringType)
        {
            foreach (var attribute in scope.CustomAttributes)
            {
                if (attribute.AttributeType.FullName == NullableContextAttributeName
                    && attribute.ConstructorArguments.Count == 1
                    && attribute.ConstructorArguments[0].Value is byte flag)
                {
                    return flag;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// The member's mapped type with its declared nullability applied: wrapped in
    /// <see cref="NullableType"/> when <paramref name="declaredNullable"/> and the mapping is not
    /// already nullable or unknown; otherwise <paramref name="mapped"/> itself.
    /// </summary>
    internal static SemanticType Apply(SemanticType mapped, bool declaredNullable)
        => declaredNullable ? Wrap(mapped) : mapped;

    /// <summary>
    /// The member's mapped type with its declared nullability applied at EVERY position — the top
    /// level and each nested type argument, array element and tuple item. This is the one place a
    /// CLR declaration's nullability is projected onto a Sharpy type, so every route (member type,
    /// parameter, return, extension) that maps through it gets the nested positions for free.
    /// </summary>
    /// <remarks>
    /// A nested position is applied only when the mapped node's arity MATCHES the declaration's:
    /// the bridge deliberately reshapes some CLR types (<c>Func&lt;…&gt;</c> into a
    /// <see cref="FunctionType"/>, <c>Result</c>/<c>Optional</c> into their own records), and
    /// pairing positions across a reshape would attach an annotation to the wrong slot. Where the
    /// shapes do not line up the subtree keeps exactly the type it has today.
    /// </remarks>
    internal static SemanticType Apply(SemanticType mapped, ClrNullabilityShape shape)
    {
        var applied = ApplyToNested(mapped, shape.Arguments);
        return shape.Nullable ? Wrap(applied) : applied;
    }

    /// <remarks>
    /// A VALUE type is never wrapped: an NRT annotation applies to reference positions, and `T?`
    /// with `T` instantiated as `int` is plain `int` in C# — `default(int)`, not `Nullable<int>`.
    /// A CLR member genuinely declared `int?` is `Nullable&lt;int&gt;`, a distinct
    /// <see cref="System.Type"/> the bridge already maps to <see cref="NullableType"/>, so it is
    /// excluded by the first clause rather than by this one.
    /// </remarks>
    private static SemanticType Wrap(SemanticType mapped)
        => mapped is not NullableType and not UnknownType and not OptionalType and not VoidType
            && !mapped.IsValueType
            ? new NullableType { UnderlyingType = mapped }
            : mapped;

    private static SemanticType ApplyToNested(SemanticType mapped, ClrNullabilityShape[] arguments)
    {
        if (arguments.Length == 0)
            return mapped;

        switch (mapped)
        {
            case GenericType generic when generic.TypeArguments.Count == arguments.Length:
                return generic with { TypeArguments = Zip(generic.TypeArguments, arguments) };

            case TupleType tuple when tuple.ElementTypes.Count == arguments.Length:
                return tuple with { ElementTypes = Zip(tuple.ElementTypes, arguments) };

            // `Nullable<T>` is already a NullableType here; its single CLR type argument describes
            // the payload, which can itself be a generic with nested annotations.
            case NullableType nullable when arguments.Length == 1:
                return nullable with { UnderlyingType = Apply(nullable.UnderlyingType, arguments[0]) };

            case TaskType { ResultType: { } result } task when arguments.Length == 1:
                return task with { ResultType = Apply(result, arguments[0]) };

            default:
                return mapped;
        }
    }

    /// <summary>
    /// The same projection in the ENCODING the discovery cache uses
    /// (<see cref="Caching.TypeSignature.NullableReferenceSentinel"/>). The overload index and the
    /// builtin registry describe CLR members as <see cref="Caching.TypeSignature"/> rather than
    /// <see cref="SemanticType"/>; routing both representations through this one class is what keeps
    /// a member's nullability the SAME whichever route types it (#1847).
    /// </summary>
    internal static Caching.TypeSignature Apply(Caching.TypeSignature mapped, ClrNullabilityShape shape)
    {
        var applied = shape.Arguments.Length > 0 && mapped.TypeArguments.Count == shape.Arguments.Length
            ? new Caching.TypeSignature
            {
                Name = mapped.Name,
                IsGeneric = mapped.IsGeneric,
                IsGenericParameter = mapped.IsGenericParameter,
                GenericParameterPosition = mapped.GenericParameterPosition,
                IsMethodLevelTypeParam = mapped.IsMethodLevelTypeParam,
                ClrTypeName = mapped.ClrTypeName,
                TypeArguments = ZipSignatures(mapped.TypeArguments, shape.Arguments)
            }
            : mapped;

        return shape.Nullable ? WrapSignature(applied) : applied;
    }

    private static Caching.TypeSignature WrapSignature(Caching.TypeSignature inner)
        => inner.Name == Caching.TypeSignature.NullableSentinel
            || inner.Name == Caching.TypeSignature.NullableReferenceSentinel
            ? inner
            : new Caching.TypeSignature
            {
                Name = Caching.TypeSignature.NullableReferenceSentinel,
                IsGeneric = true,
                TypeArguments = new System.Collections.Generic.List<Caching.TypeSignature> { inner }
            };

    private static System.Collections.Generic.List<Caching.TypeSignature> ZipSignatures(
        System.Collections.Generic.List<Caching.TypeSignature> mapped,
        ClrNullabilityShape[] shapes)
    {
        var applied = new System.Collections.Generic.List<Caching.TypeSignature>(mapped.Count);
        for (var i = 0; i < mapped.Count; i++)
            applied.Add(Apply(mapped[i], shapes[i]));
        return applied;
    }

    private static System.Collections.Generic.List<SemanticType> Zip(
        System.Collections.Generic.List<SemanticType> mapped,
        ClrNullabilityShape[] shapes)
    {
        var applied = new System.Collections.Generic.List<SemanticType>(mapped.Count);
        for (var i = 0; i < mapped.Count; i++)
            applied.Add(Apply(mapped[i], shapes[i]));
        return applied;
    }

    private static NullabilityInfo? Create(System.Func<NullabilityInfo> create)
    {
        try
        {
            lock (Gate)
            {
                return create();
            }
        }
        catch (System.Exception ex) when (ex is System.NotSupportedException or System.ArgumentException
                                              or System.TypeLoadException or System.IO.FileNotFoundException)
        {
            // A member the context cannot describe (a by-ref-like or open generic shape) keeps the
            // type it has today rather than failing the whole resolution.
            return null;
        }
    }
}

/// <summary>
/// The nullability a CLR declaration states at every position of one type: the top level plus one
/// child per nested position (generic type argument or array element), in declaration order.
/// </summary>
/// <remarks>
/// A reflected <see cref="System.Type"/> carries no NRT information at ANY depth, so the tree must
/// come from the member. <see cref="Oblivious"/> — nothing nullable anywhere — is what an
/// un-annotated assembly and a declined member both produce, which is exactly the type Sharpy read
/// before nullability was consulted at all.
/// </remarks>
internal sealed class ClrNullabilityShape
{
    private static readonly ClrNullabilityShape[] NoArguments = System.Array.Empty<ClrNullabilityShape>();

    /// <summary>Nothing nullable at any position.</summary>
    internal static readonly ClrNullabilityShape Oblivious = new(false, NoArguments);

    /// <summary>Nullable at the top level, with no nested positions.</summary>
    internal static readonly ClrNullabilityShape TopLevel = new(true, NoArguments);

    private ClrNullabilityShape(bool nullable, ClrNullabilityShape[] arguments)
    {
        Nullable = nullable;
        Arguments = arguments;
    }

    internal static ClrNullabilityShape Create(bool nullable, ClrNullabilityShape[] arguments)
        => arguments.Length == 0
            ? (nullable ? TopLevel : Oblivious)
            : new ClrNullabilityShape(nullable, arguments);

    /// <summary>Whether null is a value of the type at THIS position.</summary>
    internal bool Nullable { get; }

    /// <summary>One shape per nested position, in declaration order. Empty when there are none.</summary>
    internal ClrNullabilityShape[] Arguments { get; }
}
