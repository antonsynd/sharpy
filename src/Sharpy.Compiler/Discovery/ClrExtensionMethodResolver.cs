using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// Resolves an extension-method reference written with explicit type arguments —
/// <c>lst.select[str](f)</c> — against the acceptance surface for #1163:
/// <see cref="System.Linq.Enumerable"/> over sequence receivers.
///
/// <para>
/// The no-type-args spelling (<c>lst.first()</c>) used to need nothing from this class: nothing resolved
/// it semantically at all. Semantic analysis merely declined to prove the member absent (the permissive
/// half of #1141, <see cref="ClrTypeHelper.GetExtensionMethodNames"/>), the emitter wrote
/// <c>lst.First()</c> verbatim through the name-only interop channel, and C# overload resolution did
/// the real work because <c>using System.Linq;</c> is always emitted. That works precisely because C#
/// can infer every type argument. Add explicit ones and the same channel emits
/// <c>lst.Select[string](…)</c> — element access on a method group, CS0021 behind SPY0908 — because the
/// written arguments are only PART of the C# type-argument vector: <c>Select&lt;TSource, TResult&gt;</c>
/// needs the element type too, and nothing was computing it.
/// </para>
///
/// <para>
/// It works only while the call is the whole expression, though: with no Sharpy type for what it
/// returns, <c>list(lst.select(f))</c> has nothing to wrap and emits an unparameterized
/// <c>Sharpy.List</c> — CS0305 behind SPY0908 (#1206). So the no-type-args spelling now resolves too,
/// through <see cref="TryResolveFromReceiver"/>, which closes what the receiver determines and reports
/// the rest open for the semantic side to infer from the arguments, and
/// <see cref="TryCompleteFromInferredTypeArguments"/>, which takes the answer back. Where that cannot
/// close the vector, nothing is recorded and the call stays exactly as permissive as it was.
/// </para>
///
/// <para>
/// So this class computes the whole vector. It binds the method's <c>this</c> parameter against the
/// receiver (giving <c>TSource</c> from <c>List&lt;int&gt;</c>), assigns the written arguments to the
/// type parameters the receiver leaves unbound, and returns the closed vector for the emitter to
/// spell. Deriving the binding from the real signature — rather than assuming "receiver arguments come
/// first" — is what makes <c>cast[str]</c> / <c>of_type[str]</c> (whose <c>this</c> parameter is the
/// non-generic <c>IEnumerable</c>, so nothing leads) come out right alongside
/// <c>select[str]</c> / <c>zip[str]</c>.
/// </para>
///
/// <para>
/// Reflection lives here in Discovery, never in the emitter (Critical Rule 2): the TypeChecker
/// materializes the result into the <c>GenericReference</c> fact that code generation reads.
/// Deliberately NOT a general extension-method discovery system (#1163 scope): the surface is
/// <see cref="System.Linq.Enumerable"/>, the one the issue's repro and every LINQ-shaped call need.
/// </para>
/// </summary>
internal static class ClrExtensionMethodResolver
{
    /// <summary>
    /// A resolved extension-method reference: the CLR method name to emit, the complete closed
    /// type-argument vector (receiver-inferred and written arguments, in declaration order), and the
    /// closed <see cref="MethodInfo"/> itself, whose return and parameter types are the signature the
    /// call is checked against once the vector is known (#1195).
    /// </summary>
    internal sealed record Resolution(
        string ClrMethodName, IReadOnlyList<Type> TypeArguments, MethodInfo ClosedMethod);

    /// <summary>The acceptance surface for #1163. Widening this is a deliberate, separate decision.</summary>
    private static readonly Type[] SurfaceTypes = { typeof(System.Linq.Enumerable) };

    /// <summary>
    /// Sharpy member name (and verbatim CLR name) -> the generic extension methods on the surface that
    /// could supply it. Built once; the surface is a fixed set of framework types.
    /// </summary>
    private static readonly Lazy<Dictionary<string, List<MethodInfo>>> _byName = new(BuildIndex);

    private static readonly ConcurrentDictionary<Type, Type[]> _ancestorCache = new();

    private static Dictionary<string, List<MethodInfo>> BuildIndex()
    {
        var index = new Dictionary<string, List<MethodInfo>>(StringComparer.Ordinal);

        foreach (var surface in SurfaceTypes)
        {
            foreach (var method in surface.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                // Only a generic extension method can be written with explicit type arguments; a
                // non-generic one (Enumerable.Sum(this IEnumerable<int>)) has no vector to compute.
                if (!method.IsGenericMethodDefinition)
                    continue;
                if (!method.IsDefined(typeof(ExtensionAttribute), inherit: false))
                    continue;
                if (method.GetParameters().Length == 0)
                    continue;

                Register(index, method.Name, method);
                var sharpyName = NameMangler.ToSharpyName(method.Name, ReverseNameContext.Method);
                if (!string.Equals(sharpyName, method.Name, StringComparison.Ordinal))
                    Register(index, sharpyName, method);
            }
        }

        return index;
    }

    private static void Register(Dictionary<string, List<MethodInfo>> index, string name, MethodInfo method)
    {
        if (!index.TryGetValue(name, out var list))
        {
            list = new List<MethodInfo>();
            index[name] = list;
        }
        list.Add(method);
    }

    /// <summary>
    /// Whether <paramref name="memberName"/> names a generic extension method on the acceptance
    /// surface. Distinguishes "this is one of ours but the type arguments could not be computed"
    /// (a deliberate diagnostic) from "nothing here supplies that name" (left to the existing
    /// permissive interop channel and the #1141 absence proof).
    /// </summary>
    internal static bool IsOnAcceptanceSurface(string memberName)
        => _byName.Value.ContainsKey(memberName);

    /// <summary>
    /// Resolves <paramref name="memberName"/> on <paramref name="receiverType"/> with the written
    /// <paramref name="explicitTypeArgs"/>, returning the CLR method name and the complete closed
    /// type-argument vector — or <c>null</c> when no single candidate accounts for the written
    /// arguments (nothing by that name binds the receiver, the counts do not add up, a written
    /// argument contradicts what the receiver determines, a constraint is violated, or two candidates
    /// disagree on the vector).
    /// </summary>
    internal static Resolution? TryResolveWithExplicitTypeArguments(
        Type receiverType, string memberName, IReadOnlyList<Type> explicitTypeArgs)
    {
        if (explicitTypeArgs.Count == 0 || !_byName.Value.TryGetValue(memberName, out var candidates))
            return null;

        Resolution? resolved = null;
        foreach (var candidate in candidates)
        {
            var closed = TryCloseCandidate(candidate, receiverType, explicitTypeArgs);
            if (closed == null)
                continue;

            var next = new Resolution(candidate.Name, closed.GetGenericArguments(), closed);
            if (resolved == null)
            {
                resolved = next;
                continue;
            }

            // Same name and same vector is not ambiguity — Enumerable ships several same-arity
            // overloads (Select's plain and index-taking selectors) that close identically and let C#
            // pick by argument shape. A genuine disagreement is un-computable.
            if (!SameResolution(resolved, next))
                return null;
        }

        return resolved;
    }

    private static bool SameResolution(Resolution a, Resolution b)
        => string.Equals(a.ClrMethodName, b.ClrMethodName, StringComparison.Ordinal)
           && a.TypeArguments.Count == b.TypeArguments.Count
           && !a.TypeArguments.Where((t, i) => t != b.TypeArguments[i]).Any();

    /// <summary>
    /// What the caller WROTE at an argument position, before any of it is type-checked: a lambda with a
    /// known parameter count, or anything else. That is enough to pick between <c>Enumerable</c>'s
    /// same-arity overloads — <c>Select</c>'s plain and index-taking selectors differ only in the
    /// selector's arity — without checking an argument the staged path has not reached yet (#1206).
    /// </summary>
    internal readonly record struct ExtensionArgumentShape(bool IsLambda, int LambdaParameterCount)
    {
        /// <summary>Anything that is not a written lambda, including a variable holding one.</summary>
        internal static ExtensionArgumentShape Value => new(false, 0);

        internal static ExtensionArgumentShape Lambda(int parameterCount) => new(true, parameterCount);
    }

    /// <summary>
    /// A candidate closed as far as the RECEIVER determines it, with every type parameter the receiver
    /// leaves unbound still open (#1206). This is the state
    /// <see cref="TryResolveWithExplicitTypeArguments"/> has no representation for: it goes straight to a
    /// closed <see cref="MethodInfo"/> and hard-fails on any residual type parameter, because the written
    /// <c>[...]</c> supplied the rest. With nothing written, the rest comes from the ARGUMENTS, which are
    /// checked later and on the semantic side — so this record is what the two halves meet on.
    ///
    /// <para>
    /// <see cref="ParameterTypes"/> and <see cref="ReturnType"/> carry the receiver's bindings substituted
    /// in and the open parameters left as CLR generic parameters, so
    /// <see cref="ClrTypeBridge.MapClrTypeToSemanticType"/> renders them as
    /// <c>TypeParameterType</c> (never <c>Unknown</c>, never <c>object</c>) — the shape the deferral pass
    /// and <c>CheckLambda</c>'s guard both require.
    /// </para>
    /// </summary>
    internal sealed record PartialResolution(
        string ClrMethodName,
        MethodInfo OpenMethod,
        IReadOnlyList<Type> ParameterTypes,
        Type ReturnType,
        IReadOnlyList<string> OpenTypeParameterNames,
        IReadOnlyDictionary<Type, Type> ReceiverBindings);

    /// <summary>
    /// Resolves <paramref name="memberName"/> on <paramref name="receiverType"/> with NO written type
    /// arguments, binding only what the receiver determines and reporting the rest as open — or
    /// <c>null</c> when no single candidate accounts for the written argument shapes (nothing by that
    /// name binds the receiver, the counts do not add up, a written lambda's arity fits no candidate, or
    /// two candidates disagree on the resulting signature).
    ///
    /// <para>
    /// Binding is deliberately RECEIVER-ONLY. Type parameters that an argument determines
    /// (<c>Join</c>'s <c>TInner</c> from its <c>IEnumerable&lt;TInner&gt;</c> argument, <c>Aggregate</c>'s
    /// <c>TAccumulate</c> from its seed) are closed on the semantic side by
    /// <c>GenericTypeInferenceService.UnifyTypes</c>, which already runs over these formals in the
    /// deferred-lambda pass. Unifying a second time here, against CLR types, would be a parallel
    /// implementation of the same rule (#1145) — so this returns them open and
    /// <see cref="TryCompleteFromInferredTypeArguments"/> takes them back once inference has run.
    /// </para>
    ///
    /// <para>
    /// This says nothing about precedence: an instance member of the same name beats every extension
    /// method, and proving that is the CALLER's job (#1206 D3a). This entry point closes
    /// <c>reverse</c>/<c>append</c>/<c>count</c>/<c>contains</c> as readily as <c>select</c>.
    /// </para>
    /// </summary>
    internal static PartialResolution? TryResolveFromReceiver(
        Type receiverType, string memberName, IReadOnlyList<ExtensionArgumentShape> argumentShapes)
    {
        var all = TryResolveAllFromReceiver(receiverType, memberName, argumentShapes);
        return all.Count == 1 ? all[0] : null;
    }

    internal static IReadOnlyList<PartialResolution> TryResolveAllFromReceiver(
        Type receiverType, string memberName, IReadOnlyList<ExtensionArgumentShape> argumentShapes)
    {
        if (!_byName.Value.TryGetValue(memberName, out var candidates))
            return Array.Empty<PartialResolution>();

        var resolved = new List<PartialResolution>();
        foreach (var candidate in candidates)
        {
            var next = TryBindCandidateFromReceiver(candidate, receiverType, argumentShapes);
            if (next == null)
                continue;

            // Same name, same substituted signature and same open set is not ambiguity — it is two
            // overloads that became indistinguishable once the receiver bound them. Deduplicate.
            if (resolved.Count > 0 && resolved.Any(r => SamePartialResolution(r, next)))
                continue;

            resolved.Add(next);
        }

        return resolved;
    }

    /// <summary>
    /// Completes a <see cref="PartialResolution"/> once inference has supplied a CLR type for every open
    /// type parameter, returning the same <see cref="Resolution"/> shape the explicit path produces — or
    /// null when an open parameter is still missing, still open, or the closed vector violates a
    /// constraint.
    /// </summary>
    internal static Resolution? TryCompleteFromInferredTypeArguments(
        PartialResolution partial, IReadOnlyDictionary<string, Type> inferredOpenTypeArguments)
    {
        var typeParams = partial.OpenMethod.GetGenericArguments();
        var vector = new Type[typeParams.Length];
        for (int i = 0; i < typeParams.Length; i++)
        {
            if (partial.ReceiverBindings.TryGetValue(typeParams[i], out var fromReceiver))
            {
                vector[i] = fromReceiver;
                continue;
            }

            if (!inferredOpenTypeArguments.TryGetValue(typeParams[i].Name, out var inferred)
                || inferred == null || inferred.ContainsGenericParameters)
            {
                return null;
            }
            vector[i] = inferred;
        }

        try
        {
            // Rejects constraint violations up front, exactly as the explicit path does, so a vector that
            // cannot exist never reaches the emitter as valid-looking C#.
            var closed = partial.OpenMethod.MakeGenericMethod(vector);
            return new Resolution(partial.ClrMethodName, closed.GetGenericArguments(), closed);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static PartialResolution? TryBindCandidateFromReceiver(
        MethodInfo candidate, Type receiverType, IReadOnlyList<ExtensionArgumentShape> argumentShapes)
    {
        var parameters = candidate.GetParameters();
        // The receiver is not in the argument list, so the written arguments face parameters 1..n.
        if (parameters.Length - 1 != argumentShapes.Count)
            return null;

        var bindings = new Dictionary<Type, Type>();
        if (!TryBindThisParameter(parameters[0].ParameterType, receiverType, bindings))
            return null;

        var formals = new Type[argumentShapes.Count];
        for (int i = 0; i < argumentShapes.Count; i++)
        {
            var formal = SubstituteBindings(parameters[i + 1].ParameterType, bindings);
            if (!ShapeFits(argumentShapes[i], formal))
                return null;
            formals[i] = formal;
        }

        var open = candidate.GetGenericArguments()
            .Where(tp => !bindings.ContainsKey(tp))
            .Select(tp => tp.Name)
            .ToList();

        return new PartialResolution(
            candidate.Name,
            candidate,
            formals,
            SubstituteBindings(candidate.ReturnType, bindings),
            open,
            bindings);
    }

    private static bool SamePartialResolution(PartialResolution a, PartialResolution b)
        => string.Equals(a.ClrMethodName, b.ClrMethodName, StringComparison.Ordinal)
           && a.ReturnType == b.ReturnType
           && a.ParameterTypes.Count == b.ParameterTypes.Count
           && !a.ParameterTypes.Where((t, i) => t != b.ParameterTypes[i]).Any()
           && a.OpenTypeParameterNames.Count == b.OpenTypeParameterNames.Count
           && !a.OpenTypeParameterNames
                .Where((n, i) => !string.Equals(n, b.OpenTypeParameterNames[i], StringComparison.Ordinal))
                .Any();

    /// <summary>
    /// Whether a written argument's shape can face <paramref name="formal"/>. Only a written lambda
    /// constrains anything, and only by arity: a lambda of N parameters needs a delegate formal of N
    /// parameters. Anything else is accepted, so a variable holding a function still matches — and when
    /// that leaves two overloads standing, <see cref="SamePartialResolution"/> declines rather than guesses.
    /// </summary>
    private static bool ShapeFits(ExtensionArgumentShape shape, Type formal)
        => !shape.IsLambda
           || DelegateParameterCount(formal) is int arity && arity == shape.LambdaParameterCount;

    private static int? DelegateParameterCount(Type formal)
    {
        if (formal == typeof(Action))
            return 0;
        if (!formal.IsGenericType)
            return null;

        var name = formal.GetGenericTypeDefinition().FullName ?? string.Empty;
        if (name.StartsWith("System.Linq.Expressions.Expression`", StringComparison.Ordinal))
            return DelegateParameterCount(formal.GetGenericArguments()[0]);
        if (name.StartsWith("System.Func`", StringComparison.Ordinal))
            return formal.GetGenericArguments().Length - 1;
        if (name.StartsWith("System.Action`", StringComparison.Ordinal))
            return formal.GetGenericArguments().Length;
        return null;
    }

    /// <summary>
    /// Rewrites <paramref name="type"/> with <paramref name="bindings"/> applied, leaving unbound type
    /// parameters in place. <c>MakeGenericType</c> accepts a generic parameter as an argument, so
    /// <c>Func&lt;TSource, TResult&gt;</c> with <c>TSource = int</c> becomes the open constructed
    /// <c>Func&lt;int, TResult&gt;</c> rather than failing or collapsing.
    /// </summary>
    private static Type SubstituteBindings(Type type, IReadOnlyDictionary<Type, Type> bindings)
    {
        if (type.IsGenericParameter)
            return bindings.TryGetValue(type, out var bound) ? bound : type;

        if (type.IsByRef)
            return SubstituteBindings(type.GetElementType()!, bindings).MakeByRefType();

        if (type.IsArray)
        {
            var element = SubstituteBindings(type.GetElementType()!, bindings);
            return type.GetArrayRank() == 1 ? element.MakeArrayType() : element.MakeArrayType(type.GetArrayRank());
        }

        if (!type.IsGenericType)
            return type;

        var arguments = type.GetGenericArguments();
        var substituted = new Type[arguments.Length];
        bool changed = false;
        for (int i = 0; i < arguments.Length; i++)
        {
            substituted[i] = SubstituteBindings(arguments[i], bindings);
            changed |= substituted[i] != arguments[i];
        }

        return changed ? type.GetGenericTypeDefinition().MakeGenericType(substituted) : type;
    }

    /// <summary>
    /// Closes one candidate: binds its type parameters from the receiver, assigns the written
    /// arguments to whatever the receiver left unbound (or, when the written arguments cover every
    /// type parameter, positionally with the receiver binding as a consistency check), and verifies
    /// the result against the method's constraints. Returns the CLOSED method — its generic arguments
    /// are the vector, its return and parameter types the signature the call is checked against
    /// (#1195) — or null when the candidate cannot account for the written arguments.
    /// </summary>
    private static MethodInfo? TryCloseCandidate(
        MethodInfo candidate, Type receiverType, IReadOnlyList<Type> explicitTypeArgs)
    {
        var typeParams = candidate.GetGenericArguments();
        var bindings = new Dictionary<Type, Type>();
        if (!TryBindThisParameter(candidate.GetParameters()[0].ParameterType, receiverType, bindings))
            return null;

        var unbound = typeParams.Where(tp => !bindings.ContainsKey(tp)).ToList();
        if (unbound.Count == explicitTypeArgs.Count)
        {
            for (int i = 0; i < unbound.Count; i++)
                bindings[unbound[i]] = explicitTypeArgs[i];
        }
        else if (typeParams.Length == explicitTypeArgs.Count)
        {
            // Every type parameter written out, including the ones the receiver determines — legal C#,
            // but only if the written arguments AGREE with the receiver.
            for (int i = 0; i < typeParams.Length; i++)
            {
                if (bindings.TryGetValue(typeParams[i], out var fromReceiver)
                    && fromReceiver != explicitTypeArgs[i])
                {
                    return null;
                }
                bindings[typeParams[i]] = explicitTypeArgs[i];
            }
        }
        else
        {
            return null;
        }

        var vector = new Type[typeParams.Length];
        for (int i = 0; i < typeParams.Length; i++)
        {
            if (!bindings.TryGetValue(typeParams[i], out var bound))
                return null;
            vector[i] = bound;
        }

        try
        {
            // Rejects constraint violations (and any residual open type parameter) up front, so a
            // vector that cannot exist never reaches the emitter as valid-looking C#.
            return candidate.MakeGenericMethod(vector);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Binds the type parameters appearing in an extension method's <c>this</c> parameter against the
    /// receiver: <c>IEnumerable&lt;TSource&gt;</c> against <c>List&lt;int&gt;</c> binds
    /// <c>TSource = int</c>. A non-generic <c>this</c> parameter (<c>Cast</c>/<c>OfType</c>'s
    /// <c>IEnumerable</c>) binds nothing and still succeeds. Returns false when the receiver does not
    /// reach the parameter's generic definition, or reaches it more than one way (a type implementing
    /// both <c>IEnumerable&lt;int&gt;</c> and <c>IEnumerable&lt;str&gt;</c> determines nothing).
    /// </summary>
    private static bool TryBindThisParameter(Type thisParameter, Type receiverType, Dictionary<Type, Type> bindings)
    {
        if (thisParameter.IsGenericParameter)
        {
            bindings[thisParameter] = receiverType;
            return true;
        }

        // An ARRAY `this` parameter binds only against an array receiver. .NET 10 added
        // Enumerable.Reverse<TSource>(this TSource[]) beside the IEnumerable<TSource> one; without this
        // arm an array parameter falls into the non-generic early-out below, "succeeds" having bound
        // nothing, and leaves TSource open for a List<int> receiver it can never accept — which made
        // `lst.reverse[str]()` resolve to Reverse<string> and `lst.reverse()` ambiguous.
        if (thisParameter.IsArray)
        {
            if (!receiverType.IsArray || receiverType.GetArrayRank() != thisParameter.GetArrayRank())
                return false;

            var parameterElement = thisParameter.GetElementType()!;
            var receiverElement = receiverType.GetElementType()!;
            if (!parameterElement.IsGenericParameter)
                return parameterElement == receiverElement;

            if (bindings.TryGetValue(parameterElement, out var boundElement) && boundElement != receiverElement)
                return false;
            bindings[parameterElement] = receiverElement;
            return true;
        }

        if (!thisParameter.IsGenericType)
            return true;

        var definition = thisParameter.GetGenericTypeDefinition();
        var closed = FindUniqueClosedAncestor(receiverType, definition);
        if (closed == null)
            return false;

        var parameterArgs = thisParameter.GetGenericArguments();
        var receiverArgs = closed.GetGenericArguments();
        if (parameterArgs.Length != receiverArgs.Length)
            return false;

        for (int i = 0; i < parameterArgs.Length; i++)
        {
            if (parameterArgs[i].IsGenericParameter)
            {
                if (bindings.TryGetValue(parameterArgs[i], out var existing) && existing != receiverArgs[i])
                    return false;
                bindings[parameterArgs[i]] = receiverArgs[i];
            }
            else if (parameterArgs[i] != receiverArgs[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The single closed instantiation of <paramref name="definition"/> that <paramref name="type"/>
    /// is or implements, or null when there is none or more than one.
    /// </summary>
    private static Type? FindUniqueClosedAncestor(Type type, Type definition)
    {
        Type? found = null;
        foreach (var candidate in Ancestors(type))
        {
            if (!candidate.IsGenericType || candidate.GetGenericTypeDefinition() != definition)
                continue;
            if (found != null && found != candidate)
                return null;
            found = candidate;
        }
        return found;
    }

    private static Type[] Ancestors(Type type)
    {
        return _ancestorCache.GetOrAdd(type, static t =>
        {
            var all = new List<Type>();
            for (var current = t; current != null; current = current.BaseType)
                all.Add(current);
            try
            {
                all.AddRange(t.GetInterfaces());
            }
            catch (Exception ex) when (ex is System.Reflection.ReflectionTypeLoadException or TypeLoadException)
            {
                // Interfaces that cannot be loaded contribute nothing.
            }
            return all.ToArray();
        });
    }
}
