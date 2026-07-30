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
/// The no-type-args spelling (<c>lst.first()</c>) needs nothing from this class: nothing resolves it
/// semantically at all. Semantic analysis merely declines to prove the member absent (the permissive
/// half of #1141, <see cref="ClrTypeHelper.GetExtensionMethodNames"/>), the emitter writes
/// <c>lst.First()</c> verbatim through the name-only interop channel, and C# overload resolution does
/// the real work because <c>using System.Linq;</c> is always emitted. That works precisely because C#
/// can infer every type argument. Add explicit ones and the same channel emits
/// <c>lst.Select[string](…)</c> — element access on a method group, CS0021 behind SPY0908 — because the
/// written arguments are only PART of the C# type-argument vector: <c>Select&lt;TSource, TResult&gt;</c>
/// needs the element type too, and nothing was computing it.
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
