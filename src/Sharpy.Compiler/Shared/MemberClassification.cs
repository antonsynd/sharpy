using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// Single classification authority for method access, staticness, abstractness, virtualness,
/// and overrideness. Both <c>NameResolver.ResolveMethodDeclaration</c> and
/// <c>ModuleLoader.ExtractMethodSymbol</c> consume this so the same rules apply to same-file
/// and imported members (#1267).
/// </summary>
internal static class MemberClassification
{
    public readonly record struct Result(
        AccessLevel Access,
        AccessLevel? ExplicitAccess,
        bool IsStatic,
        bool IsAbstract,
        bool IsVirtual,
        bool IsOverride);

    /// <summary>
    /// Classifies a method's access, staticness, abstractness, virtualness, and overrideness.
    /// The formula is keyed to the owning type's kind and abstractness.
    /// </summary>
    public static Result Classify(FunctionDef def, TypeKind ownerKind, bool ownerIsAbstract)
    {
        var (access, explicitAccess) = ClassifyAccess(def.Name, def.Decorators, ownerKind);

        bool hasSelf = def.Parameters.Any(p =>
            string.Equals(p.Name, PythonNames.Self, StringComparison.OrdinalIgnoreCase));
        bool isStatic = def.Decorators.Any(d => d.Name == DecoratorNames.Static) || !hasSelf;

        bool isAbstract = IsAbstractMember(
            def.Decorators, def.Body, canBeImplicitStub: true, ownerKind, ownerIsAbstract);

        bool isVirtual = def.Decorators.Any(d => d.Name == DecoratorNames.Virtual);
        bool isOverride = def.Decorators.Any(d => d.Name == DecoratorNames.Override)
            || ProtocolRegistry.IsObjectOverrideDunder(def.Name);

        return new Result(access, explicitAccess, isStatic, isAbstract, isVirtual, isOverride);
    }

    /// <summary>A field's access, staticness and finality — the <see cref="Result"/> of a field.</summary>
    public readonly record struct FieldResult(
        AccessLevel Access,
        AccessLevel? ExplicitAccess,
        bool IsStatic,
        bool IsFinal);

    /// <summary>
    /// Classifies a field declaration, for the same reason <see cref="Classify(FunctionDef, TypeKind, bool)"/>
    /// exists for a method: <c>NameResolver.ResolveFieldDeclaration</c> and
    /// <c>ModuleLoader.ExtractFields</c> both answer this question, and when they answered it
    /// separately the extraction knew nothing of the <c>@private</c>/<c>@public</c> decorators — an
    /// explicitly-private imported field read as convention-public, and the decorator that set the
    /// level was not recorded at all (#1441). The owner kind is the host axis
    /// <see cref="ClassifyAccess"/> needs (#1937).
    /// </summary>
    public static FieldResult ClassifyField(VariableDeclaration def, TypeKind ownerKind)
    {
        var (access, explicitAccess) = ClassifyAccess(def.Name, def.Decorators, ownerKind);

        return new FieldResult(
            access,
            explicitAccess,
            def.Decorators.Any(d => d.Name == DecoratorNames.Static),
            def.Decorators.Any(d => d.Name == DecoratorNames.Final));
    }

    /// <summary>
    /// Whether <paramref name="def"/> declares an INSTANCE field — the roster every synthesized
    /// member of a struct or dataclass is built from.
    ///
    /// <para>A <c>const</c> and a <c>@static</c> field are class-level storage, not per-instance
    /// state: neither is a constructor parameter, neither is assigned in a constructor body, and
    /// neither participates in the "non-default field cannot follow a defaulted one" ordering rule
    /// — a <c>const</c> always has an initializer, so reading it as a defaulted INSTANCE field made
    /// every instance field after it SPY0435 (#1794). One predicate, because the struct validator,
    /// the dataclass field vector and the synthesized-constructor roster were three copies of it
    /// and only one of them knew about <c>const</c>.</para>
    ///
    /// <para>CodeGen must NOT call this: the emitter reads the same fact materialized on the field
    /// symbol (<c>VariableSymbol.IsStatic</c>, <c>CodeGenInfo.IsConstant</c>), per Critical
    /// Rule 2.</para>
    /// </summary>
    public static bool IsInstanceField(VariableDeclaration def)
        => !def.IsConst && !def.Decorators.Any(d => d.Name == DecoratorNames.Static);

    /// <summary>
    /// Whether a member carries the Sharpy <c>@abstract</c> DECORATOR.
    /// </summary>
    /// <remarks>
    /// A bracket attribute — <c>@[abstract]</c> — is a CLR attribute pass-through, not the decorator,
    /// and is deliberately excluded (#1373). Without that exclusion <c>@[abstract] class Weird</c> made
    /// the class symbol abstract, the implicit-stub rule then classified its <c>...</c>-bodied members
    /// abstract, and emission produced abstract members inside a NON-abstract C# class — CS0513 behind
    /// SPY0908. The emitter's own formulas always excluded it; this is the seam that did not.
    /// </remarks>
    public static bool HasAbstractDecorator(IEnumerable<Decorator> decorators)
        => decorators.Any(d => !d.IsBracketAttribute && d.Name == DecoratorNames.Abstract);

    /// <summary>
    /// The ONE abstractness rule, for every member kind (#1374). A member is abstract when it carries
    /// the <c>@abstract</c> decorator, or when it is a stub its owner makes implicitly abstract: an
    /// ellipsis body in an abstract class, or an abstract stub body in an interface.
    /// </summary>
    /// <param name="canBeImplicitStub">
    /// Whether the member can carry a body at all, and so whether the implicit arms can apply. Always
    /// true for a method. For a property or event this is <c>IsFunctionStyle</c>: an AUTO-event
    /// (<c>event on_click: Handler</c>) and an auto-property have no body to be a stub, so only the
    /// decorator can make them abstract — which is the one place the member kinds genuinely differ.
    /// </param>
    public static bool IsAbstractMember(
        IEnumerable<Decorator> decorators,
        ImmutableArray<Statement> body,
        bool canBeImplicitStub,
        TypeKind ownerKind,
        bool ownerIsAbstract)
    {
        if (HasAbstractDecorator(decorators))
            return true;

        if (!canBeImplicitStub)
            return false;

        return (ownerIsAbstract && AstHelper.IsEllipsisStubBody(body))
            || (ownerKind == TypeKind.Interface && AstHelper.IsAbstractStubBody(body));
    }

    /// <summary>Abstractness of a property, through <see cref="IsAbstractMember"/> (#1374).</summary>
    public static bool IsAbstract(PropertyDef def, TypeKind ownerKind, bool ownerIsAbstract)
        => IsAbstractMember(def.Decorators, def.Body, def.IsFunctionStyle, ownerKind, ownerIsAbstract);

    /// <summary>Abstractness of an event, through <see cref="IsAbstractMember"/> (#1374).</summary>
    public static bool IsAbstract(EventDef def, TypeKind ownerKind, bool ownerIsAbstract)
        => IsAbstractMember(def.Decorators, def.Body, def.IsFunctionStyle, ownerKind, ownerIsAbstract);

    /// <summary>
    /// The ONE access rule for a member of a type — method, field, property accessor, event
    /// accessor, nested type (#1937). The explicit access decorator wins; otherwise the underscore
    /// convention (<see cref="AccessLevelConventions.FromName"/>) decides. Then the HOST axis: a
    /// struct is sealed, so <c>protected</c> has no meaning there and is <c>private</c> — C# refuses
    /// a protected member in a struct outright (CS0666). The emitter reads the resulting
    /// <c>Symbol.AccessLevel</c> (and <c>PropertySymbol.GetterAccess</c>/<c>SetterAccess</c>,
    /// <c>EventSymbol</c>'s levels) instead of re-deriving access from the name, which is how
    /// <c>_x</c> in a struct emitted <c>protected</c> at four sites that never looked at the host.
    ///
    /// <para><c>ExplicitAccess</c> is the decorator as WRITTEN, unmapped: an explicit
    /// <c>@protected</c> on a struct member is refused by <c>DecoratorValidator</c> (SPY0415, beside
    /// <c>@virtual</c>), and <c>AccessValidator</c> reads the written level for its message. Mapping
    /// the effective level anyway keeps the symbol emit-legal for error recovery.</para>
    /// </summary>
    public static (AccessLevel Access, AccessLevel? ExplicitAccess) ClassifyAccess(
        string name, IEnumerable<Decorator> decorators, TypeKind ownerKind)
    {
        var explicitAccess = GetExplicitAccessLevel(decorators);
        var access = explicitAccess ?? AccessLevelConventions.FromName(name);
        if (ownerKind == TypeKind.Struct && access == AccessLevel.Protected)
            access = AccessLevel.Private;
        return (access, explicitAccess);
    }

    /// <summary>
    /// Extracts the explicit access level from access modifier decorators, if any. The last one
    /// wins (two access decorators are refused by <c>DecoratorValidator</c>, SPY0430). A
    /// bracket attribute is a CLR attribute pass-through, not the decorator (the #1373 rule
    /// <see cref="HasAbstractDecorator"/> states).
    /// </summary>
    public static AccessLevel? GetExplicitAccessLevel(IEnumerable<Decorator> decorators)
    {
        AccessLevel? result = null;
        foreach (var decorator in decorators)
        {
            if (decorator.IsBracketAttribute)
                continue;
            var level = decorator.Name switch
            {
                DecoratorNames.Public => AccessLevel.Public,
                DecoratorNames.Protected => AccessLevel.Protected,
                DecoratorNames.Private => AccessLevel.Private,
                DecoratorNames.Internal => AccessLevel.Internal,
                _ => (AccessLevel?)null
            };
            if (level != null)
            {
                result = level;
            }
        }
        return result;
    }
}
