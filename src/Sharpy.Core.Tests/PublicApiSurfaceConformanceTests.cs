using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace Sharpy.Core.Tests;

public class PublicApiSurfaceConformanceTests
{
    private static readonly HashSet<Type> BannedGenericDefinitions = new()
    {
        typeof(System.Collections.Generic.List<>),
        typeof(ReadOnlyCollection<>),
        typeof(IList<>),
        typeof(Dictionary<,>),
        typeof(ICollection<>),
    };

    private static readonly HashSet<string> Allowlist = new(StringComparer.Ordinal)
    {
        "IDeepCopyable.DeepCopy",
        "Dict`2.ToDictionary",
        "List`1.ToList",
        "Operator.Not",
        "Operator.Truth",
        "StringExtensions.Maketrans",
        "StringExtensions.Translate",
    };

    private static readonly HashSet<string> AllowlistedTypeNames = new(StringComparer.Ordinal)
    {
        // Generators namespace is compiler infrastructure, not user-facing Sharpy API.
        "ClassInfo", "DecoratorInfo", "FunctionInfo", "MethodInfo",
        "GeneratorContext", "GeneratorOutput", "ParameterInfo",
    };

    [Fact]
    public void PublicApi_DoesNotExpose_RawDotNetCollections()
    {
        var assembly = typeof(Sharpy.List<>).Assembly;
        var violations = new List<string>();

        foreach (var type in assembly.GetExportedTypes())
        {
            if (AllowlistedTypeNames.Contains(type.Name))
                continue;

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                var key = $"{type.Name}.{prop.Name}";
                if (Allowlist.Contains(key))
                    continue;

                if (IsBannedCollectionType(prop.PropertyType))
                    violations.Add($"Property {type.FullName}.{prop.Name} returns banned type {FormatType(prop.PropertyType)}");
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName)
                    continue;

                var key = $"{type.Name}.{method.Name}";
                if (Allowlist.Contains(key))
                    continue;

                if (IsBannedCollectionType(method.ReturnType))
                    violations.Add($"Method {type.FullName}.{method.Name} returns banned type {FormatType(method.ReturnType)}");

                foreach (var param in method.GetParameters())
                {
                    if (IsBannedCollectionType(param.ParameterType))
                        violations.Add($"Method {type.FullName}.{method.Name} parameter '{param.Name}' uses banned type {FormatType(param.ParameterType)}");
                }
            }
        }

        violations.Should().BeEmpty(
            "public API must use Sharpy collection types, not raw .NET collections");
    }

    private static bool IsBannedCollectionType(Type type)
    {
        if (!type.IsGenericType)
            return false;

        var def = type.GetGenericTypeDefinition();
        return BannedGenericDefinitions.Contains(def);
    }

    private static string FormatType(Type type)
    {
        if (!type.IsGenericType)
            return type.Name;

        var baseName = type.Name[..type.Name.IndexOf('`')];
        var args = string.Join(", ", type.GetGenericArguments().Select(a => a.Name));
        return $"{baseName}<{args}>";
    }
}
