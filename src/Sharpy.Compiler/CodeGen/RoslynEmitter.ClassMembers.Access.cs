using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Access modifiers of type members, read from the symbol facts semantic analysis materialized
/// (#1937). Access is classified ONCE, with the host axis, by
/// <c>MemberClassification.ClassifyAccess</c> — a struct is sealed, so the <c>_name</c>
/// convention's <c>protected</c> is <c>private</c> there — and frozen on <c>Symbol.AccessLevel</c>,
/// <c>PropertySymbol.GetterAccess</c>/<c>SetterAccess</c> and <c>EventSymbol</c>'s three levels.
/// Every class-member emission site reads it through the helpers below. The name-keyed helper they
/// replace had no host axis, which is how a struct's
/// <c>_x</c>, <c>def _m</c>, <c>property get _p</c>, nested <c>class _Inner</c> and
/// <c>event _e</c> all emitted <c>protected</c> (CS0666 behind SPY0908).
///
/// <para>A member of a type whose symbol resolved but whose own symbol is missing is an invariant
/// violation and fails loud — a silent fallback to the name would be the deleted helper again. A
/// member with NO host type symbol is a module-level member (only module-level properties reach
/// the class-member generators that way, <c>GenerateModuleLevelProperty</c>): it takes the
/// host-less module-level rule, <see cref="GetModuleLevelAccessModifier"/>.</para>
/// </summary>
internal partial class RoslynEmitter
{
    /// <summary>The ONE <see cref="AccessLevel"/> → C# access keyword map for a type member.</summary>
    private static SyntaxKind MemberAccessKeyword(AccessLevel level) => level switch
    {
        AccessLevel.Private => SyntaxKind.PrivateKeyword,
        AccessLevel.Protected => SyntaxKind.ProtectedKeyword,
        AccessLevel.Internal => SyntaxKind.InternalKeyword,
        _ => SyntaxKind.PublicKeyword,
    };

    /// <summary>Access of a method, constructor or dunder-backed member: its <c>FunctionSymbol</c>.</summary>
    private SyntaxKind MemberAccessKeyword(FunctionDef func)
    {
        if (_currentTypeSymbol == null)
            return HostlessAccessKeyword(func.Name, func.Decorators);

        var symbol = _context.SemanticInfo?.GetFunctionDeclarationSymbol(func)
            ?? _currentTypeSymbol.Methods.Concat(_currentTypeSymbol.Constructors)
                .FirstOrDefault(m => m.Name == func.Name && m.DeclarationLine == func.LineStart);
        return symbol != null
            ? MemberAccessKeyword(symbol.AccessLevel)
            : MissingMemberAccessSymbol("method", func.Name, func.LineStart, func.ColumnStart);
    }

    /// <summary>
    /// Access of one <c>property</c> declaration: the getter's level for a getter or an
    /// auto-property, the setter's for a <c>set</c>/<c>init</c> accessor — the same split
    /// <c>NameResolver.ResolvePropertyDeclaration</c> records.
    /// </summary>
    private SyntaxKind MemberAccessKeyword(PropertyDef def)
    {
        if (_currentTypeSymbol == null)
            return HostlessAccessKeyword(def.Name, def.Decorators);

        var symbol = _currentTypeSymbol.Properties.FirstOrDefault(p => p.Name == def.Name);
        if (symbol == null)
            return MissingMemberAccessSymbol("property", def.Name, def.LineStart, def.ColumnStart);

        return MemberAccessKeyword(def.Accessor is PropertyAccessor.Set or PropertyAccessor.Init
            ? symbol.SetterAccess
            : symbol.GetterAccess);
    }

    /// <summary>
    /// Access of an event (<paramref name="accessorLevel"/> false: the event-level access) or of one
    /// of its <c>add</c>/<c>remove</c> accessors.
    /// </summary>
    private SyntaxKind MemberAccessKeyword(EventDef def, bool accessorLevel)
    {
        if (_currentTypeSymbol == null)
            return HostlessAccessKeyword(def.Name, def.Decorators);

        var symbol = _currentTypeSymbol.Events.FirstOrDefault(e => e.Name == def.Name);
        if (symbol == null)
            return MissingMemberAccessSymbol("event", def.Name, def.LineStart, def.ColumnStart);

        return MemberAccessKeyword(!accessorLevel ? symbol.AccessLevel : def.Accessor switch
        {
            EventAccessor.Add => symbol.AddAccessLevel,
            EventAccessor.Remove => symbol.RemoveAccessLevel,
            _ => symbol.AccessLevel,
        });
    }

    /// <summary>Access of a field (or <c>const</c>) declared in a type body.</summary>
    private SyntaxKind MemberAccessKeyword(VariableDeclaration field)
    {
        if (_currentTypeSymbol == null)
            return HostlessAccessKeyword(field.Name, field.Decorators);

        var symbol = _currentTypeSymbol.Fields.FirstOrDefault(f => f.Name == field.Name);
        return symbol != null
            ? MemberAccessKeyword(symbol.AccessLevel)
            : MissingMemberAccessSymbol("field", field.Name, field.LineStart, field.ColumnStart);
    }

    /// <summary>Access of a nested type (class, struct, interface, enum, union, delegate).</summary>
    private SyntaxKind NestedTypeAccessKeyword(string name, Statement declaration)
    {
        if (_currentTypeSymbol == null)
            return HostlessAccessKeyword(name, System.Collections.Immutable.ImmutableArray<Decorator>.Empty);

        var symbol = _currentTypeSymbol.NestedTypes.FirstOrDefault(n => n.Name == name);
        return symbol != null
            ? MemberAccessKeyword(symbol.AccessLevel)
            : MissingMemberAccessSymbol("nested type", name, declaration.LineStart, declaration.ColumnStart);
    }

    /// <summary>
    /// The host-less (module-level) rule: an explicit access decorator, else the module-level
    /// underscore convention (<c>_name</c> → <c>internal</c> — a module class is static, and a static
    /// class cannot hold a protected member, CS1057).
    /// </summary>
    private static SyntaxKind HostlessAccessKeyword(string name, IEnumerable<Decorator> decorators)
    {
        SyntaxKind? explicitAccess = null;
        foreach (var decorator in decorators)
        {
            if (decorator.IsBracketAttribute)
                continue;
            explicitAccess = decorator.Name switch
            {
                DecoratorNames.Public => SyntaxKind.PublicKeyword,
                DecoratorNames.Protected => SyntaxKind.ProtectedKeyword,
                DecoratorNames.Private => SyntaxKind.PrivateKeyword,
                DecoratorNames.Internal => SyntaxKind.InternalKeyword,
                _ => explicitAccess,
            };
        }
        return explicitAccess ?? GetModuleLevelAccessModifier(name);
    }

    private SyntaxKind MissingMemberAccessSymbol(string kind, string name, int? line, int? column)
    {
        _context.AddError(
            $"internal: {kind} '{name}' of '{_currentTypeSymbol?.Name}' has no symbol to read its access level from",
            DiagnosticCodes.Infrastructure.InvariantViolation, line, column);
        return SyntaxKind.PublicKeyword;
    }
}
