using System;
using System.Collections.Generic;
using System.Reflection;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Discovery;

/// <summary>
/// The public constructor surface of a CLR type, as Sharpy <c>__init__</c> symbols.
///
/// <para>
/// Shared by every registry that builds a <see cref="TypeSymbol"/> from CLR metadata, because a
/// constructor surface that exists for one registry and not another is a silent hole rather than a
/// visible one: C# does not inherit constructors, so the emitter synthesizes forwarders from
/// <see cref="TypeSymbol.Constructors"/>, and a base whose surface was never collected forwards
/// nothing and fails as CS1729 behind SPY0908 (#1367). <c>ModuleRegistry</c> collected it and
/// <c>BuiltinRegistry</c> did not, which is exactly why `class E(Exception): pass` could not be
/// constructed with an argument while a CLR base reached through an import could.
/// </para>
///
/// <para>
/// Reflection lives here in Discovery, never in the emitter (Critical Rule 2).
/// </para>
/// </summary>
internal static class ClrConstructorSurface
{
    /// <summary>
    /// Every public instance constructor of <paramref name="clrType"/>, mapped to an
    /// <c>__init__</c> <see cref="FunctionSymbol"/>. Empty when the type exposes none.
    /// </summary>
    internal static List<FunctionSymbol> Build(Type clrType)
    {
        var result = new List<FunctionSymbol>();
        foreach (var ctor in clrType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
        {
            var symbol = CreateConstructorSymbol(ctor, clrType);
            if (symbol != null)
                result.Add(symbol);
        }

        return result;
    }

    /// <summary>
    /// Create a FunctionSymbol for a .NET constructor, mapped as __init__.
    ///
    /// Note: We DO include 'self' as the first parameter to match Sharpy conventions.
    /// The type checker uses .Skip(1) when building FunctionType from constructors,
    /// so we need the 'self' parameter for the skip to work correctly.
    /// </summary>
    private static FunctionSymbol? CreateConstructorSymbol(ConstructorInfo ctor, Type declaringType)
    {
        var typeMapper = new ClrTypeBridge();
        var parameters = new List<ParameterSymbol>
        {
            // 'self' first (Sharpy convention — skipped by the type checker)
            new ParameterSymbol
            {
                Name = PythonNames.Self,
                Type = new UserDefinedType { Name = declaringType.Name }
            }
        };

        foreach (var param in ctor.GetParameters())
        {
            parameters.Add(new ParameterSymbol
            {
                Name = param.Name ?? $"arg{param.Position}",
                // Parameter position keeps IEnumerable<T> wide (#1450) — see the mapper's own note.
                Type = typeMapper.MapClrParameterTypeToSemanticType(param.ParameterType),
                HasDefault = param.HasDefaultValue
            });
        }

        return new FunctionSymbol
        {
            Name = DunderNames.Init,
            Kind = SymbolKind.Function,
            ReturnType = SemanticType.Void,
            Parameters = parameters,
            AccessLevel = AccessLevel.Public,
            ClrMethod = null  // ConstructorInfo isn't a MethodInfo, leave null
        };
    }
}
