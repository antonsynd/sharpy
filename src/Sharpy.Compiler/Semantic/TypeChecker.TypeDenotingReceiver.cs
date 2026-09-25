using System.Linq;
using Sharpy.Compiler.Diagnostics;
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
            var target = _typeResolver.ResolveTypeAnnotation(alias.TypeAnnotation, AnnotationPosition.Value);
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

    /// <summary>
    /// A member of a NESTED enum reached through its declaring-type chain (<c>H.C.red</c>) is a static
    /// constant of the enum the chain denotes (#2037). The chain stays untyped as a value (a nested
    /// access that is itself a qualifier is left untyped, and <c>H.C.red</c>'s own type is #2038), so
    /// nothing reached the emitter but the AST: it re-mangled the member name from the USE site and
    /// disagreed with the declaration (an escaped member, or a camelCase int member, was CS0117).
    /// Record the denoted enum on the qualifier and the member resolution on the access, so the
    /// emitter spells the receiver from the enum symbol and the member from the member symbol's
    /// materialized <c>CSharpName</c> — the one spelling every enum reference reads. A chain under a
    /// generic owner is left alone: the denoted type would spell the OPEN owner (CS0305, #1941).
    /// </summary>
    private void RecordNestedEnumMemberReference(MemberAccess memberAccess, SemanticType objectType)
    {
        if (memberAccess.Object is not MemberAccess qualifier
            || _semanticInfo.GetDenotedType(qualifier) != null
            || DenotedTypeSymbolOf(qualifier, objectType) is not ({ TypeKind: TypeKind.Enum } enumSymbol, _)
            || enumSymbol.Fields.FirstOrDefault(f => f.Name == memberAccess.Member) is not { } member)
            return;

        for (var owner = enumSymbol.DeclaringType; owner != null; owner = owner.DeclaringType)
        {
            if (owner.IsGeneric)
                return;
        }

        _semanticInfo.SetDenotedType(qualifier, new UserDefinedType { Name = enumSymbol.Name, Symbol = enumSymbol });
        _semanticInfo.SetMemberAccessResolution(memberAccess, enumSymbol, member);
    }

    /// <summary>
    /// Whether <paramref name="receiver"/> DENOTES a type rather than holding a value. The ONE
    /// predicate: a bare name bound to a <see cref="TypeSymbol"/> (<c>H</c>, <c>G</c>, an imported or
    /// module-qualified type), or any node
    /// <see cref="SemanticInfo.MarkTypeReference"/> recorded — which
    /// <see cref="ClassifyTypeDenotingReceiver"/> does for the constructed generic (<c>G[int]</c>) and
    /// the type alias (<c>A</c> of <c>type A = G[int]</c>), and the nested-type arms do for
    /// <c>Outer.Inner</c> and <c>Environment.SpecialFolder</c>. Both the CLR receiver-kind split
    /// (<c>ClrReceiverKindOf</c>) and the instance-member refusal
    /// (<see cref="TryRefuseInstanceMemberViaTypeName"/>) ask it here, so no spelling can be a type
    /// name for one question and a value for the other (#1817, Decision 3).
    /// </summary>
    private bool ReceiverDenotesType(Expression receiver)
        => (receiver is Identifier id && _semanticInfo.GetIdentifierSymbol(id) is TypeSymbol)
           || _semanticInfo.IsTypeReference(receiver);

    /// <summary>
    /// The Sharpy-declared <see cref="TypeSymbol"/> a type-denoting receiver names, together with the
    /// spelling to quote back in a diagnostic, or null when the receiver is not a type name or names a
    /// CLR-backed type. Every spelling resolves through the SAME two questions — the receiver's
    /// recorded type (a <see cref="UserDefinedType"/> for a plain or nested name, a
    /// <see cref="GenericType"/> for a constructed reference or its alias) and, for a bare name, the
    /// symbol the identifier is bound to.
    /// </summary>
    private (TypeSymbol Symbol, string Name)? DenotedTypeSymbolOf(Expression receiver, SemanticType receiverType)
    {
        // A NESTED type under another type-denoting receiver — `Outer.Inner`, `G[int].Inner`,
        // `Outer.Mid.Inner` — answered structurally, by asking the same question of the segment to its
        // left. The member-typing arms deliberately leave such a chain untyped when it is itself a
        // qualifier (the emitter has no carrier for the closed owner of a nested type under a
        // constructed generic, so typing it spells the OPEN `G.Inner`, CS0305 — #1941), and that
        // omission must not decide whether `G[int].Inner.inst` is refused (#1817, Decision 3).
        if (receiver is MemberAccess nestedAccess && !nestedAccess.IsNullConditional
            && DenotedTypeSymbolOf(
                   nestedAccess.Object, _semanticInfo.GetExpressionType(nestedAccess.Object) ?? SemanticType.Unknown)
               is var (outerSymbol, _)
            && outerSymbol.NestedTypes.FirstOrDefault(n => n.Name == nestedAccess.Member) is { } nestedSymbol)
            return (nestedSymbol, nestedSymbol.Name);

        if (!ReceiverDenotesType(receiver))
            return null;

        var symbol = receiverType switch
        {
            UserDefinedType udt => udt.Symbol ?? _symbolTable.LookupType(udt.Name),
            GenericType generic => GenericDefinitionOf(generic),
            _ => null,
        };

        // A bare name is its own answer when the recorded type is not one of the two shapes above —
        // `H.K` types the identifier as a FunctionType (the constructor reference), not a UDT (#432).
        if (symbol == null && receiver is Identifier id
            && _semanticInfo.GetIdentifierSymbol(id) is TypeSymbol identifierSymbol)
            symbol = identifierSymbol;

        if (symbol == null)
            return null;

        // The spelling the reader wrote: the bare name for an identifier, the constructed display name
        // (`G[int]`) for every other type-denoting receiver.
        var name = receiver is Identifier bare && _semanticInfo.GetIdentifierSymbol(bare) is TypeSymbol
            ? bare.Name
            : receiverType is UnknownType ? symbol.Name : receiverType.GetDisplayName();

        return (symbol, name);
    }

    /// <summary>
    /// The ONE refusal for an INSTANCE member named through a receiver that denotes a TYPE (SPY0290).
    /// Reached from <c>CheckMemberAccessCore</c> before any member-typing arm, so every spelling of the
    /// receiver gets the same verdict: the bare non-generic name (<c>H.inst</c>), the bare generic name
    /// (<c>G.inst</c>), the constructed reference (<c>G[int].inst</c>), a nested type under one
    /// (<c>G[int].Inner.inst</c>) and a type alias for one (<c>type A = G[int]</c> then <c>A.inst</c>).
    /// Before this, only the two bare spellings were refused; the constructed, nested and alias
    /// spellings type the instance member and reach Roslyn as CS0120/CS1503 behind SPY0908 — a
    /// compiler-bug report for `@static`-forgetting (#1817, Decision 3 sibling cell).
    ///
    /// <para>The refusal covers all three instance member kinds (field, property, method) for the same
    /// reason C# does: none of them binds without a receiver instance. A <c>const</c>, a
    /// <c>@static</c> member, an enum case, a union case and a nested type are NOT instance members and
    /// fall through untouched. CLR-backed type symbols are excluded — the static/instance mix-up on a
    /// reflected surface is decided by <c>ClrMemberTypeResolver.ExistsOnOppositeHalf</c>, which
    /// deliberately declines rather than refuses (#1940).</para>
    /// </summary>
    private bool TryRefuseInstanceMemberViaTypeName(MemberAccess memberAccess, SemanticType receiverType)
    {
        if (memberAccess.Member.Length == 0)
            return false;

        if (DenotedTypeSymbolOf(memberAccess.Object, receiverType) is not var (typeSymbol, typeName))
            return false;

        // Enum cases and union cases are named through the type by construction; a CLR surface is the
        // reflected resolver's business.
        if (typeSymbol.ClrType != null
            || typeSymbol.TypeKind is not (TypeKind.Class or TypeKind.Struct or TypeKind.Interface))
            return false;

        var member = memberAccess.Member;

        // A name with ANY static declaration in the hierarchy binds statically — an instance overload
        // sitting beside it is not what the reference names.
        if (EnumerateTypeAndHierarchy(typeSymbol).Any(t =>
                t.Fields.Any(f => f.Name == member && (f.IsStatic || f.IsConstant))
                || t.Properties.Any(p => p.Name == member && p.IsStatic)
                || t.Methods.Any(m => m.Name == member && m.IsStatic)
                || t.NestedTypes.Any(n => n.Name == member)))
            return false;

        var kind = FindFieldInHierarchy(typeSymbol, member).Field != null ? "field"
            : FindPropertyInHierarchy(typeSymbol, member).Property != null ? "property"
            : FindMethodInHierarchy(typeSymbol, member).Method != null ? "method"
            : null;

        if (kind == null)
            return false;

        AddError(
            $"Cannot access instance {kind} '{member}' via type name '{typeName}'. " +
            "Mark it as @static or use an instance.",
            memberAccess.LineStart, memberAccess.ColumnStart,
            code: DiagnosticCodes.Semantic.InstanceFieldViaTypeName,
            span: memberAccess.Span);
        MarkExpressionAsErrorRecovery(memberAccess,
            ErrorRecoveryReason.AlreadyReported("an instance member named through a type (SPY0290)"));
        return true;
    }
}
