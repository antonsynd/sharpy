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

        bool hasAbstractDecorator = def.Decorators.Any(d => d.Name == DecoratorNames.Abstract);
        bool hasEllipsisBody = AstHelper.IsEllipsisStubBody(def.Body);
        bool isInterfaceAbstract = ownerKind == TypeKind.Interface
            && AstHelper.IsAbstractStubBody(def.Body);
        bool isAbstract = hasAbstractDecorator
            || (ownerIsAbstract && hasEllipsisBody)
            || isInterfaceAbstract;

        bool isVirtual = def.Decorators.Any(d => d.Name == DecoratorNames.Virtual);
        bool isOverride = def.Decorators.Any(d => d.Name == DecoratorNames.Override)
            || ProtocolRegistry.IsObjectOverrideDunder(def.Name);

        return new Result(access, explicitAccess, isStatic, isAbstract, isVirtual, isOverride);
    }

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
