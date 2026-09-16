using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: the ONE classifier for an expression that denotes a TYPE in
/// member-access receiver position. Every spelling that names a type a static member is reached
/// through — a constructed generic reference (<c>G[int]</c>, <c>G[G[int]]</c>, <c>Outer.Inner[int]</c>,
/// <c>lib.G[int]</c>) and a type alias (<c>type A = G[int]</c> then <c>A.K</c>) — is classified here,
/// records the type it denotes on the receiver node (<see cref="SemanticInfo.SetDenotedType"/>), and
/// is marked a type reference. The emitter reads the recorded denoted type (Critical Rule 2) to spell
/// the receiver of the static member instead of re-deriving it from the AST shape (#1817, #1864).
///
/// The nested-CLR-type chain (<c>Environment.SpecialFolder.Desktop</c>) reaches its type through the
/// resolver's nested-type step (#1864) and is emitted by the ordinary receiver recursion, so it does
/// not need a denoted record; the bare open generic (<c>G.K</c>) is refused SPY0339 at the type-member
/// seam (a generic reference must be constructed), not here.
/// </summary>
internal partial class TypeChecker
{
    /// <summary>
    /// Classifies a member-access receiver that denotes a type, recording the denoted type for the
    /// emitter and normalizing a type-alias receiver to its target. Returns the denoted type when the
    /// receiver denotes one (which the caller uses as the receiver's member-lookup type), or
    /// <c>null</c> when it does not (an ordinary value receiver).
    /// </summary>
    private SemanticType? ClassifyTypeDenotingReceiver(Expression receiver, SemanticType receiverType)
    {
        // A constructed generic reference: G[int], G[G[int]], Outer.Inner[int], lib.G[int]. The
        // GenericReferenceResolver already typed the IndexAccess as the constructed type and recorded
        // a *TypeRef GenericReference; the denoted record lets the emitter spell the closed type
        // (`G<int>`) as the receiver instead of the element access it re-derived before (#1817).
        if (receiver is IndexAccess indexAccess
            && _semanticInfo.GetGenericReference(indexAccess) is
            {
                Kind: GenericReferenceKind.GenericTypeRef
                        or GenericReferenceKind.NestedTypeRef
                        or GenericReferenceKind.ModuleType
            }
            && receiverType is GenericType or UserDefinedType)
        {
            _semanticInfo.MarkTypeReference(receiver);
            _semanticInfo.SetDenotedType(receiver, receiverType);
            return receiverType;
        }

        // A type alias used as a receiver: `type A = G[int]` then `A.K`. The alias identifier resolves
        // to no type reference on its own (it is neither a TypeSymbol nor an IndexAccess), so the
        // member seam never saw the target and the alias was never emitted (CS0103). Normalize the
        // receiver to the alias TARGET so the member resolves against it and the emitter spells the
        // target (#1817, sibling finding (c)).
        if (receiver is Identifier aliasIdentifier
            && _semanticInfo.GetIdentifierSymbol(aliasIdentifier) is TypeAliasSymbol alias
            && alias.TypeAnnotation != null)
        {
            var target = _typeResolver.ResolveTypeAnnotation(alias.TypeAnnotation);
            if (target is GenericType or UserDefinedType or BuiltinType)
            {
                _semanticInfo.SetExpressionType(receiver, target);
                _semanticInfo.MarkTypeReference(receiver);
                _semanticInfo.SetDenotedType(receiver, target);
                return target;
            }
        }

        return null;
    }
}
