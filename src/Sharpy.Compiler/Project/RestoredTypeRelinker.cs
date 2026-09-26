using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Utilities;

namespace Sharpy.Compiler.Project;

/// <summary>
/// The origin a <see cref="UserDefinedType"/> carries on the incremental-cache wire (#2027): the
/// <c>"user"</c> codec writes <c>name@origin</c>, and the post-restore relink pass binds the decoded
/// type's symbol from it. One origin per way a cold build obtains the symbol:
/// <list type="bullet">
/// <item><c>file:&lt;path&gt;</c> — declared in a project source file (the declaring file's
/// normalized path); relinked to the symbol restored from that file's cache entry.</item>
/// <item><c>module:&lt;name&gt;</c> — exported by a discovered stdlib module (<c>datetime.date</c>)
/// or a .NET namespace (<c>system.text.StringBuilder</c>); relinked through the module registry's
/// export of that name.</item>
/// <item><c>clr:&lt;FullName&gt;</c> — any other CLR-backed symbol: the builtins registry
/// (<c>bytes</c>, <c>Template</c>, <c>ValueError</c>) or a CLR type the bridge named. The encoder cannot ask the registry whether a symbol is its own, so the relink asks:
/// the registry's type of that NAME whose CLR type is this one — never a same-named user type
/// (an escape-declared <c>class `bytes`</c> carries a <c>file:</c> origin, #1325).</item>
/// </list>
/// A type with no symbol keeps the origin it was decoded with (a failed relink re-encodes
/// unchanged) and a symbol with none of the three writes the bare name.
/// </summary>
internal static class CachedTypeOrigin
{
    internal const string FilePrefix = "file:";
    internal const string ModulePrefix = "module:";
    internal const string ClrPrefix = "clr:";

    /// <summary>The origin to write for <paramref name="udt"/>, or null for a bare name.</summary>
    internal static string? Of(UserDefinedType udt)
        => udt.Symbol is { } symbol ? Of(symbol) : udt.CacheOrigin;

    /// <summary>
    /// The origin to write for a Sharpy-declared generic's definition, or null for none — a
    /// CLR-defined generic carries its CLR identity instead (<c>ClrOriginTypeName</c>, #1260/#1568).
    /// </summary>
    internal static string? Of(GenericType generic)
        => generic.GenericDefinition is { } definition ? Of(definition) : generic.CacheOrigin;

    /// <summary>Whether an <c>@</c> payload is one of the three origins rather than a CLR name.</summary>
    internal static bool IsCacheOrigin(string payload)
        => payload.StartsWith(FilePrefix, StringComparison.Ordinal)
           || payload.StartsWith(ModulePrefix, StringComparison.Ordinal)
           || payload.StartsWith(ClrPrefix, StringComparison.Ordinal);

    private static string? Of(TypeSymbol symbol)
    {
        if (DeclaringFileOf(symbol) is { Length: > 0 } path)
            return FilePrefix + PathNormalizer.Normalize(path);
        if (symbol.DefiningModule is { Length: > 0 } module)
            return ModulePrefix + module;
        if (symbol.ClrType?.FullName is { Length: > 0 } clrName)
            return ClrPrefix + clrName;
        return null;
    }

    /// <summary>
    /// The project file declaring <paramref name="symbol"/> — its own, else its declaring type's
    /// (a nested type records the file on the outermost declaration).
    /// </summary>
    private static string? DeclaringFileOf(TypeSymbol symbol)
    {
        for (var current = symbol; current != null; current = current.DeclaringType)
        {
            if ((current.DefiningFilePath ?? current.DeclaringFilePath) is { Length: > 0 } path)
                return path;
        }
        return null;
    }

    /// <summary>
    /// The dotted name a type is spelled by in a cached signature: the declaring-type chain plus its
    /// own name (<c>H.C</c> for a <c>class C</c> nested in <c>H</c>) — the key the relink index uses.
    /// </summary>
    internal static string QualifiedName(TypeSymbol symbol)
    {
        var parts = new List<string>();
        for (var current = symbol; current != null; current = current.DeclaringType)
            parts.Add(current.Name);
        parts.Reverse();
        return string.Join(".", parts);
    }
}

/// <summary>
/// The post-restore relink pass (#2027): every signature type a restored symbol carries is walked
/// and each <see cref="UserDefinedType"/> decoded without its symbol is rebound through
/// <see cref="UserDefinedType.CacheOrigin"/>, so a warm build types what a cold build types.
/// </summary>
/// <remarks>
/// A pass after decode rather than a decode-time resolver because a file's signatures can name
/// types declared later in the same entry, or in another cached file restored after it; decode is
/// per symbol and cannot see them yet. The name-keyed <c>udt.Symbol ?? Lookup(udt.Name)</c>
/// fallbacks in the checker stay until #2050 retires them.
/// </remarks>
internal sealed class RestoredTypeRelinker
{
    private readonly Func<string, string, TypeSymbol?> _resolve;

    /// <param name="resolve">Binds a decoded (origin, name) to its symbol, or null.</param>
    internal RestoredTypeRelinker(Func<string, string, TypeSymbol?> resolve)
    {
        _resolve = resolve;
    }

    /// <summary>
    /// Rebinds every symbol-less, origin-carrying type inside <paramref name="type"/>: a
    /// <see cref="UserDefinedType"/>'s symbol, and a Sharpy-declared generic's
    /// <see cref="GenericType.GenericDefinition"/> — the member table a constructed <c>Box[int]</c>
    /// is read through, which a cold build attaches at resolution.
    /// </summary>
    internal SemanticType Relink(SemanticType type)
        => SemanticTypeWalker.Rewrite(type, node => node switch
        {
            UserDefinedType { Symbol: null, CacheOrigin: { } origin } udt when _resolve(origin, udt.Name) is { } symbol
                => udt with { Symbol = symbol },
            // A replacement stops the walk, so the arguments are relinked here.
            GenericType { GenericDefinition: null, CacheOrigin: { } origin } generic when _resolve(origin, generic.Name) is { } definition
                => generic with { GenericDefinition = definition, TypeArguments = generic.TypeArguments.Select(Relink).ToList() },
            _ => null,
        });

    /// <summary>Relinks every signature type <paramref name="symbol"/> carries, in place.</summary>
    internal void RelinkSymbol(Symbol symbol)
    {
        switch (symbol)
        {
            case TypeSymbol type:
                RelinkType(type);
                break;
            case FunctionSymbol function:
                RelinkFunction(function);
                break;
            case VariableSymbol variable:
                variable.Type = Relink(variable.Type);
                break;
        }
    }

    private void RelinkType(TypeSymbol type)
    {
        foreach (var field in type.Fields)
            field.Type = Relink(field.Type);
        foreach (var method in type.Methods)
            RelinkFunction(method);
        foreach (var constructor in type.Constructors)
            RelinkFunction(constructor);
        for (var i = 0; i < type.Properties.Count; i++)
        {
            var relinked = Relink(type.Properties[i].Type);
            if (!ReferenceEquals(relinked, type.Properties[i].Type))
                type.Properties[i] = type.Properties[i] with { Type = relinked };
        }
        for (var i = 0; i < type.Events.Count; i++)
        {
            var relinked = Relink(type.Events[i].Type);
            if (!ReferenceEquals(relinked, type.Events[i].Type))
                type.Events[i] = type.Events[i] with { Type = relinked };
        }
        foreach (var nested in type.NestedTypes)
            RelinkType(nested);
        foreach (var unionCase in type.UnionCases)
            RelinkType(unionCase);
    }

    private void RelinkFunction(FunctionSymbol function)
    {
        function.ReturnType = Relink(function.ReturnType);
        for (var i = 0; i < function.Parameters.Count; i++)
        {
            var relinked = Relink(function.Parameters[i].Type);
            if (!ReferenceEquals(relinked, function.Parameters[i].Type))
                function.Parameters[i] = function.Parameters[i] with { Type = relinked };
        }
    }
}
