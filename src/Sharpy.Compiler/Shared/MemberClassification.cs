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
        var access = AccessLevelConventions.FromName(def.Name);
        var explicitAccess = GetExplicitAccessLevel(def.Decorators);
        if (explicitAccess != null)
            access = explicitAccess.Value;

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
    /// Extracts the explicit access level from access modifier decorators, if any.
    /// </summary>
    public static AccessLevel? GetExplicitAccessLevel(IEnumerable<Decorator> decorators)
    {
        AccessLevel? result = null;
        foreach (var decorator in decorators)
        {
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
