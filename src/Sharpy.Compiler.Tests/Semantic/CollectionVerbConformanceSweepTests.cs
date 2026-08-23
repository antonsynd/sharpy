using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Sharpy.Compiler.Shared;
using Xunit;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// #1571 reflective conformance sweep. Enumerates every Python collection verb that
/// Sharpy's builtin list/dict/set expose and guards three invariants:
/// <list type="number">
/// <item>Every UNMAPPED verb that collides with a LINQ extension must have an instance
/// method of the same PascalCase name on a common CLR collection type — otherwise the LINQ
/// extension silently shadows the intended mutation semantics.</item>
/// <item>Every MAPPED verb's target CLR method must exist on <c>ICollection&lt;T&gt;</c>
/// or <c>IList&lt;T&gt;</c> — the mapped spelling is dead code if the target vanishes.</item>
/// <item>Every verb in <c>ClrCollectionVerbMap</c> is also in the known verb set.</item>
/// </list>
/// </summary>
public class CollectionVerbConformanceSweepTests
{
    private static readonly string[] KnownPythonCollectionVerbs =
    {
        "append", "extend", "insert", "remove", "pop", "clear",
        "sort", "reverse", "copy", "count", "index",
    };

    private static readonly HashSet<string> LinqExtensionNames = new(
        typeof(System.Linq.Enumerable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Select(m => m.Name),
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> CollectionAndListMethods = new(
        typeof(ICollection<>)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name)
            .Concat(typeof(IList<>)
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Select(m => m.Name)),
        StringComparer.Ordinal);

    private static readonly HashSet<string> ClrCollectionInstanceMembers = new(
        typeof(System.Collections.Generic.List<>)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name)
            .Concat(typeof(System.Collections.Generic.Dictionary<,>)
                .GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Select(m => m.Name))
            .Concat(typeof(System.Collections.Generic.HashSet<>)
                .GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Select(m => m.Name)),
        StringComparer.Ordinal);

    // Verbs that collide with LINQ extensions but are safe because the #1571 unmapped-verb
    // refusal gate (TypeChecker.Expressions.Access.cs) catches them before LINQ staging
    // can bind. Each entry must explain why mapping is unnecessary.
    private static readonly HashSet<string> SafeUnmappedCollisions = new(StringComparer.Ordinal)
    {
        // index → Enumerable.Index (.NET 10): Python's list.index(x) returns the position of x;
        // LINQ's Index() returns (index, element) tuples. The PascalCase CLR method is IndexOf,
        // not Index, so ToPascalCase("index") → "Index" does not reach the instance method.
        // The #1571 unmapped-verb refusal prevents silent LINQ binding.
        "index",
    };

    [Fact]
    public void UnmappedVerbs_WithLinqCollision_AreCovered()
    {
        var map = NameMangler.ClrCollectionVerbMap;
        var collisions = new List<string>();

        foreach (var verb in KnownPythonCollectionVerbs)
        {
            if (map.ContainsKey(verb))
                continue;

            var pascal = NameMangler.ToPascalCase(verb);
            if (!LinqExtensionNames.Contains(pascal))
                continue;

            if (ClrCollectionInstanceMembers.Contains(pascal))
                continue;

            if (SafeUnmappedCollisions.Contains(verb))
                continue;

            collisions.Add(
                $"'{verb}' (PascalCase: '{pascal}') collides with LINQ Enumerable extension "
                + "and has no CLR collection instance member to take priority — must be mapped "
                + "or added to SafeUnmappedCollisions with justification");
        }

        collisions.Should().BeEmpty(
            "unmapped Python collection verbs that collide with LINQ extensions must have "
            + "a same-named CLR instance member, an explicit mapping, or a documented safety exemption");
    }

    [Fact]
    public void SafeUnmappedCollisions_AreAllJustified()
    {
        foreach (var verb in SafeUnmappedCollisions)
        {
            KnownPythonCollectionVerbs.Should().Contain(verb,
                $"SafeUnmappedCollisions entry '{verb}' must be a known Python collection verb");
            NameMangler.ClrCollectionVerbMap.Should().NotContainKey(verb,
                $"SafeUnmappedCollisions entry '{verb}' should not also be in the verb map");
            LinqExtensionNames.Should().Contain(NameMangler.ToPascalCase(verb),
                $"SafeUnmappedCollisions entry '{verb}' should actually collide with a LINQ extension");
        }
    }

    [Fact]
    public void MappedVerbs_TargetExistingClrMethods()
    {
        var map = NameMangler.ClrCollectionVerbMap;
        var missing = new List<string>();

        foreach (var (verb, clrMethod) in map)
        {
            if (!CollectionAndListMethods.Contains(clrMethod))
                missing.Add($"'{verb}' → '{clrMethod}' not found on ICollection<T> or IList<T>");
        }

        missing.Should().BeEmpty(
            "every mapped CLR collection verb target must exist on ICollection<T> or IList<T>");
    }

    [Fact]
    public void KnownVerbSet_CoversAllMappedVerbs()
    {
        var map = NameMangler.ClrCollectionVerbMap;
        var unmapped = map.Keys.Where(k => !KnownPythonCollectionVerbs.Contains(k)).ToList();

        unmapped.Should().BeEmpty(
            "every verb in ClrCollectionVerbMap should also appear in KnownPythonCollectionVerbs");
    }
}
