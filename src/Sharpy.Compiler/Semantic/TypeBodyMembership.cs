using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// The kind of type-body member a bare name denotes. The wording of a refusal reads it; the
/// store refusal reads it to keep its own set (fields and properties).
/// </summary>
internal enum TypeBodyMemberKind
{
    Field,
    Property,
    Event,
    Method,
}

/// <summary>
/// A member of a type that a bare name denotes, with the steers that actually compile for it.
/// </summary>
/// <param name="Name">The member's name, as written.</param>
/// <param name="OwnerName">The type whose body declares it.</param>
/// <param name="Kind">Field, property, event or method.</param>
/// <param name="IsConstant">Whether it is a <c>const</c>.</param>
/// <param name="IsStatic">Whether it is <c>@static</c> (or a const, which is also type-level).</param>
/// <param name="ReachableThroughSelf">
/// Whether <c>self.Name</c> compiles here: the member is an instance member of the type
/// <c>self</c> has (its own or an inherited one) and a <c>self</c> is in scope. False in a
/// <c>@static</c> method, in a class-field-initializer lambda, and when the member belongs to
/// an OUTER class of a nested one — the cases where the old steer named something illegal.
/// </param>
/// <param name="IsInherited">
/// Whether <see cref="OwnerName"/> is a BASE of the enclosing type rather than the enclosing
/// type itself. Only the wording reads it: the refusal says "inherited class attribute" so the
/// reader is not sent looking for a declaration in the body in front of them. R-AA (#1803):
/// an inherited member and a property get the same treatment as a member the body declares.
/// </param>
/// <param name="CSharpName">
/// The C# name the member is declared under — the spelling a bare read of it emits where C#
/// resolves it by its simple name (an admitted class-body sibling read, D8). Spelled as the
/// declarations spell it: a field through <c>NameCasing.ResolveField</c> (<c>ProcessField</c>), a
/// property or event through <c>NameCasing.ResolveMethod</c> (the property/event emitters), a method
/// through <c>DunderNameMapping</c> else <c>NameCasing.ResolveMethod</c> (<c>ProcessMethod</c>).
/// </param>
/// <param name="Symbol">The member's symbol, when it has one (a field or a method; a property and
/// an event are standalone records).</param>
/// <param name="Type">The member's declared type — a method's is its function type.</param>
internal sealed record TypeBodyMember(
    string Name,
    string OwnerName,
    TypeBodyMemberKind Kind,
    bool IsConstant,
    bool IsStatic,
    bool ReachableThroughSelf,
    bool IsInherited,
    string CSharpName,
    Symbol? Symbol,
    SemanticType Type)
{
    /// <summary>
    /// <c>Owner.Name</c>, but only for a member C# can reach through the type name. An instance
    /// field named through its type is SPY0290 ("Cannot access instance field via type name"),
    /// so offering it as a fix would send the reader from one refusal to another.
    /// </summary>
    public string? TypeNameAccess => IsConstant || IsStatic ? $"{OwnerName}.{Name}" : null;

    /// <summary><c>self.Name</c>, when that compiles here.</summary>
    public string? SelfAccess => ReachableThroughSelf ? $"self.{Name}" : null;
}

/// <summary>
/// The ONE membership predicate for a bare type-body name (R-AA, #1803; extended by P21h D8,
/// #2384): which member of a type a bare name denotes. The checker's store refusal, its read steer
/// and its class-body sibling read (<c>TypeChecker.ClassScopeRule</c>) ask it, and so does
/// <c>ConstantPositionValidator</c> when it decides that a parameter default's identifier is an
/// instance member the checker already refused — so the two sides cannot disagree about which
/// names are members.
/// </summary>
internal static class TypeBodyMembership
{
    /// <summary>
    /// The bare member name a type-body statement declares, or null. Deliberately partial: a field,
    /// a property half that is not an explicit-interface implementation, an event and a method are
    /// the members a bare sibling read can denote; every other statement kind — a nested type, an
    /// alias, a union case, an explicit-interface property, a `pass` — declares no such member, so it
    /// is never a sibling's declaration (P21h F1).
    /// </summary>
    internal static string? DeclaredMemberName(Node declaration) => declaration switch
    {
        VariableDeclaration v => v.Name,
        PropertyDef { ExplicitInterface: null } p => p.Name,
        EventDef e => e.Name,
        FunctionDef f => f.Name,
        _ => null,
    };

    /// <summary>
    /// The ONE anchor rule for a type-body member's position (P21h F1, P75): the first unwrapped
    /// statement of <paramref name="body"/> that declares <paramref name="name"/>
    /// (<see cref="DeclaredMemberName"/>), or null. A split property is emitted as ONE C# property
    /// at its FIRST half, so a default carried by a later half runs where the first half stands — the
    /// checker's order rule (<c>TypeChecker.IsDeclaredLater</c>) and
    /// <c>StaticInitializerOrderValidator</c> both anchor a member here (P21h verify C2).
    /// </summary>
    internal static Node? FirstDeclarationOf(IEnumerable<Statement> body, string? name)
    {
        if (name == null)
            return null;
        foreach (var statement in body)
        {
            var declaration = statement.UnwrapDecorated();
            if (DeclaredMemberName(declaration) == name)
                return declaration;
        }

        return null;
    }

    /// <summary>
    /// Describes <paramref name="name"/> as a field, property, event or method of
    /// <paramref name="type"/> itself (not its bases), or null when that type declares no such
    /// member.
    /// </summary>
    internal static TypeBodyMember? DescribeMemberOf(
        TypeSymbol type, string name, bool reachableThroughSelf, bool isInherited)
    {
        foreach (var field in type.Fields)
        {
            if (field.Name != name)
                continue;
            bool typeLevel = field.IsStatic || field.IsConstant;
            return new TypeBodyMember(
                name, type.Name, TypeBodyMemberKind.Field, field.IsConstant, typeLevel,
                ReachableThroughSelf: !typeLevel && reachableThroughSelf,
                IsInherited: isInherited,
                NameCasing.ResolveField(field.Name, field.IsNameBacktickEscaped),
                field, field.Type);
        }

        // An explicit-interface property is not in the type's own namespace (R-JH, #2390).
        if (type.FindOwnProperty(name) is { } property)
        {
            return new TypeBodyMember(
                name, type.Name, TypeBodyMemberKind.Property, IsConstant: false, property.IsStatic,
                ReachableThroughSelf: !property.IsStatic && reachableThroughSelf,
                IsInherited: isInherited,
                NameCasing.ResolveMethod(property.Name, property.IsNameBacktickEscaped),
                Symbol: null, property.Type);
        }

        foreach (var evt in type.Events)
        {
            if (evt.Name != name)
                continue;
            return new TypeBodyMember(
                name, type.Name, TypeBodyMemberKind.Event, IsConstant: false, evt.IsStatic,
                ReachableThroughSelf: !evt.IsStatic && reachableThroughSelf,
                IsInherited: isInherited,
                NameCasing.ResolveMethod(evt.Name, evt.IsNameBacktickEscaped),
                Symbol: null, evt.Type);
        }

        foreach (var method in type.Methods)
        {
            if (method.Name != name)
                continue;
            return new TypeBodyMember(
                name, type.Name, TypeBodyMemberKind.Method, IsConstant: false, method.IsStatic,
                ReachableThroughSelf: !method.IsStatic && reachableThroughSelf,
                IsInherited: isInherited,
                DunderNameMapping.ResolveCSharpName(method.Name)
                    ?? NameCasing.ResolveMethod(method.Name, method.IsNameBacktickEscaped),
                method, FunctionType.FromParameters(method.Parameters, method.ReturnType));
        }

        return null;
    }

    /// <summary>
    /// Describes <paramref name="name"/> as a member of <paramref name="type"/> or of one of its
    /// bases, innermost first, or null when none declares it.
    /// </summary>
    internal static TypeBodyMember? FindMember(TypeSymbol? type, string name, bool reachableThroughSelf)
    {
        for (var owner = type; owner != null; owner = owner.BaseType)
        {
            if (DescribeMemberOf(owner, name, reachableThroughSelf,
                    isInherited: !ReferenceEquals(owner, type)) is { } member)
            {
                return member;
            }
        }

        return null;
    }
}
