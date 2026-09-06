using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: the consumers of Python's class-scope rule (#1786, R-Y).
/// </summary>
/// <remarks>
/// The rule itself lives in <see cref="Scope.ResolveName"/>: a variable declared in a class or
/// struct body is not bound by its bare name from inside a function-like scope nested in that body.
/// This file holds what the type checker does with the crossing the walk reports — one description
/// of the member, one refusal for every STORE form, one steer for the READ — so a sixth store form
/// cannot arrive with its own wording or, as the walrus and tuple-element forms did, with no
/// refusal at all.
/// </remarks>
internal partial class TypeChecker
{
    /// <summary>
    /// The syntactic form of a bare store, used only to name the write in the refusal.
    /// </summary>
    private enum BareStoreForm
    {
        Plain,
        Augmented,
        Walrus,
        TupleElement,
        CoalesceAssign,
    }

    /// <summary>
    /// A member of an enclosing type that a bare name denotes but cannot reach, with the steers
    /// that actually compile for it.
    /// </summary>
    /// <param name="Name">The member's name, as written.</param>
    /// <param name="OwnerName">The type whose body declares it.</param>
    /// <param name="IsConstant">Whether it is a <c>const</c>.</param>
    /// <param name="IsStatic">Whether it is <c>@static</c> (or a const, which is also type-level).</param>
    /// <param name="ReachableThroughSelf">
    /// Whether <c>self.Name</c> compiles here: the member is an instance member of the type
    /// <c>self</c> has (its own or an inherited one) and a <c>self</c> is in scope. False in a
    /// <c>@static</c> method, in a class-field-initializer lambda, and when the member belongs to
    /// an OUTER class of a nested one — the cases where the old steer named something illegal.
    /// </param>
    /// <param name="DeclaredInACrossedScope">
    /// Whether the scope WALK passed this member's declaration, as opposed to the member being
    /// found on the enclosing type symbol. R-Y refuses a bare store to a name declared in the
    /// enclosing class BODY, which is what a crossing means. An inherited field and a property are
    /// declared elsewhere — another type's body, or on the type symbol only — and a bare store to
    /// one of those names declared a fresh local at BASE and ran; refusing it would turn a working
    /// program red for a rule that was never about it. They still shape the READ diagnostic, which
    /// was already an unresolved name and only gains a steer.
    /// </param>
    private sealed record CrossedClassMember(
        string Name,
        string OwnerName,
        bool IsConstant,
        bool IsStatic,
        bool ReachableThroughSelf,
        bool DeclaredInACrossedScope)
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
    /// Describes the enclosing-type member a bare name denotes, or null when it denotes none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The scope walk answers WHETHER a bare name is a member the reader cannot reach; the owning
    /// TYPE SYMBOL answers what kind of member it is. Both are needed, and neither alone is
    /// enough. The walk sees only what a class body's scope holds, and the symbols the checker
    /// re-declares there carry no <c>@static</c> — so a static field read through the crossing
    /// would be steered to <c>self.count</c>, which does not compile. The type symbol sees
    /// <c>@static</c>, <c>const</c>, INHERITED fields and properties (properties live on the type,
    /// never in the class scope) — but not which of them a bare name at this point could have
    /// reached.
    /// </para>
    /// <para>
    /// Owners are tried innermost first: the enclosing type and its bases (whose instance members
    /// <c>self.</c> reaches), then the outer type whose body the walk crossed — a nested class's
    /// method naming the OUTER class's instance field, where neither <c>self.v</c> nor
    /// <c>Outer.v</c> compiles and the honest answer is the explanation with no steer.
    /// </para>
    /// </remarks>
    private CrossedClassMember? DescribeCrossedClassMember(string name)
    {
        var resolution = _symbolTable.Resolve(name);

        // The name binds. Whether it ALSO crossed a class member is the emitter's problem
        // (SemanticInfo.SetModuleAccessCrossesClassMember), not a diagnostic.
        if (resolution.Bound != null)
            return null;

        bool crossed = resolution.CrossedMember != null;

        for (var type = _currentClass; type != null; type = type.BaseType)
        {
            if (DescribeMemberOf(type, name, SelfIsInScope(), crossed) is { } own)
                return own;
        }

        if (resolution.CrossedMemberOwner is { } ownerName
            && _symbolTable.LookupType(ownerName) is { } ownerType
            && DescribeMemberOf(ownerType, name, reachableThroughSelf: false, crossed) is { } outer)
        {
            return outer;
        }

        // The walk crossed a class-body name no type symbol claims (a synthesized member, or a
        // type whose symbol is not reachable by name from here). Say what happened; offer nothing,
        // because nothing is known to compile.
        return resolution.CrossedMember == null
            ? null
            : new CrossedClassMember(
                name, resolution.CrossedMemberOwner ?? "the enclosing type",
                IsConstant: false, IsStatic: false, ReachableThroughSelf: false,
                DeclaredInACrossedScope: true);
    }

    /// <summary>
    /// Describes <paramref name="name"/> as a field or property of <paramref name="type"/>, or
    /// null when that type declares no such member.
    /// </summary>
    private static CrossedClassMember? DescribeMemberOf(
        TypeSymbol type, string name, bool reachableThroughSelf, bool declaredInACrossedScope)
    {
        foreach (var field in type.Fields)
        {
            if (field.Name != name)
                continue;
            bool typeLevel = field.IsStatic || field.IsConstant;
            return new CrossedClassMember(
                name, type.Name, field.IsConstant, typeLevel,
                ReachableThroughSelf: !typeLevel && reachableThroughSelf,
                DeclaredInACrossedScope: declaredInACrossedScope);
        }

        foreach (var property in type.Properties)
        {
            if (property.Name != name)
                continue;
            return new CrossedClassMember(
                name, type.Name, IsConstant: false, property.IsStatic,
                ReachableThroughSelf: !property.IsStatic && reachableThroughSelf,
                // A property is never IN a scope, so the walk cannot have crossed it.
                DeclaredInACrossedScope: false);
        }

        return null;
    }

    /// <summary>
    /// Whether a <c>self</c> parameter is bound in the current scope chain. Asked of the chain
    /// rather than of a "is this a static method" flag so every host answers for itself — a lambda
    /// inside an instance method has one, a lambda in a class-field initializer does not.
    /// </summary>
    private bool SelfIsInScope()
        => _symbolTable.Lookup(PythonNames.Self, searchParents: true) is VariableSymbol;

    /// <summary>
    /// The tail of the SPY0200 message for a bare READ that names an enclosing type's member, or
    /// null when the name names none. Never offers a spelling that does not compile: a nested
    /// class's method reading the OUTER class's instance field gets the explanation and no steer,
    /// because neither <c>self.v</c> nor <c>Outer.v</c> would work there.
    /// </summary>
    private string? DescribeBareClassMemberRead(string name)
    {
        if (DescribeCrossedClassMember(name) is not { } member)
            return null;

        var explanation =
            $". '{name}' is a class attribute of '{member.OwnerName}' — class-body names are not "
            + "visible by bare name inside methods";

        var steers = new List<string>();
        if (member.SelfAccess != null)
            steers.Add($"'{member.SelfAccess}'");
        if (member.TypeNameAccess != null)
            steers.Add($"'{member.TypeNameAccess}'");

        return steers.Count == 0
            ? explanation
            : explanation + $". Use {string.Join(" or ", steers)} to access it";
    }

    /// <summary>
    /// Refuses a bare STORE whose target names an enclosing type's member (R-Y), and reports true
    /// when it did. Called from every store form — plain, augmented, <c>??=</c>, walrus and tuple
    /// element — before the target is otherwise resolved, so the write is refused by name rather
    /// than being reported as an unresolved READ (augmented, <c>??=</c>) or silently declaring a
    /// fresh local (walrus, tuple element).
    /// </summary>
    private bool TryRefuseBareClassAttributeStore(
        string targetName, BareStoreForm form, int line, int column, TextSpan? span)
    {
        if (DescribeCrossedClassMember(targetName) is not { } member)
            return false;

        // Only a name the walk crossed is a store R-Y refuses: an inherited field or a property
        // name declared a fresh local at BASE and ran, and this rule does not turn a working
        // program red. The read arm keeps the improved message for both.
        if (!member.DeclaredInACrossedScope)
            return false;

        var write = form switch
        {
            BareStoreForm.Plain => "Cannot assign to",
            BareStoreForm.Augmented => "Cannot augment-assign to",
            BareStoreForm.Walrus => "Cannot bind with ':=' to",
            BareStoreForm.TupleElement => "Cannot unpack into",
            BareStoreForm.CoalesceAssign => "Cannot assign with '??=' to",
            _ => "Cannot assign to",
        };

        var kind = member.IsConstant ? "class constant" : "class attribute";
        var message =
            $"{write} {kind} '{targetName}' by bare name — class-body names are not visible "
            + "inside methods";

        var steers = new List<string>();

        // A const cannot be assigned through ANY spelling, so the member steers are omitted rather
        // than pointing at 'C.K = ...', which is CS0131. Saying it is a constant keeps the
        // assign-to-a-constant meaning the plain refusal used to carry (SPY0225).
        if (member.IsConstant)
        {
            message += $"; '{member.OwnerName}.{targetName}' is a constant and cannot be assigned";
        }
        else
        {
            if (member.SelfAccess != null)
                steers.Add($"'{member.SelfAccess} = ...' for the instance attribute");
            if (member.TypeNameAccess != null)
                steers.Add($"'{member.TypeNameAccess} = ...' for the class attribute");
        }

        steers.Add($"'{targetName}: <type> = ...' to declare a shadowing local");
        message += $". Use {string.Join(", or ", steers)}";

        AddError(message, line, column,
            code: DiagnosticCodes.SemanticOverflow.ClassAttributeBareStore, span: span);
        return true;
    }
}
