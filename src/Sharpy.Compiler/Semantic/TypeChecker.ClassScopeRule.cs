using System.Diagnostics;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Shared;
using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// TypeChecker partial class: the consumers of Python's class-scope rule (#1786, R-Y; #1803, R-AA).
/// </summary>
/// <remarks>
/// The rule itself lives in <see cref="Scope.ResolveName"/>: a variable declared in a class or
/// struct body is not bound by its bare name from inside a function-like scope nested in that body.
/// This file holds what the type checker does with the crossing the walk reports — one description
/// of the member, one refusal for every STORE form, one steer for the READ — so a sixth store form
/// cannot arrive with its own wording or, as the walrus and tuple-element forms did, with no
/// refusal at all. R-AA (#1803) made the description the ONE membership predicate for both sides:
/// a name is a member of the enclosing type whether its body declares it or a base's does, and
/// whether it is a field or a property, so the store refusal and the read steer cannot disagree
/// about which names are members. The predicate lives in <see cref="TypeBodyMembership"/>, which
/// <c>ConstantPositionValidator</c> asks too.
/// <para>
/// P21h D8 (R-JJ, #2384) adds the class-body READ from a position that is not a method body: a
/// field initializer, a property default, a <c>const</c> initializer, a parameter default. One
/// classifier (<see cref="ClassifyClassBodyReadPosition"/>), one admission table
/// (<see cref="ClassBodyReadAdmits"/>, the measured C# rule) and one steer per position
/// (<see cref="DescribeClassBodySiblingRefusal"/>), reached from <c>CheckIdentifier</c> through
/// <see cref="TryResolveClassBodySiblingRead"/>.
/// </para>
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
    /// Describes the enclosing-type member a bare name denotes, or null when it denotes none. This
    /// is the ONE membership predicate: the store refusal and the read steer both ask it, so the
    /// set of names one side treats as members is the set the other side does (R-AA, #1803).
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
    private TypeBodyMember? DescribeCrossedClassMember(string name)
    {
        var resolution = _symbolTable.Resolve(name);

        // The name binds. Whether it ALSO crossed a class member is the emitter's problem
        // (SemanticInfo.SetModuleAccessCrossesClassMember), not a diagnostic.
        if (resolution.Bound != null)
            return null;

        if (TypeBodyMembership.FindMember(_currentClass, name, SelfIsInScope()) is { } own)
            return own;

        if (resolution.CrossedMemberOwner is { } ownerName
            && _symbolTable.LookupType(ownerName) is { } ownerType
            && TypeBodyMembership.DescribeMemberOf(ownerType, name, reachableThroughSelf: false, isInherited: false)
                is { } outer)
        {
            return outer;
        }

        // The walk crossed a class-body name no type symbol claims (a synthesized member, or a
        // type whose symbol is not reachable by name from here). Say what happened; offer nothing,
        // because nothing is known to compile.
        return resolution.CrossedMember == null
            ? null
            : new TypeBodyMember(
                name, resolution.CrossedMemberOwner ?? "the enclosing type", TypeBodyMemberKind.Field,
                IsConstant: false, IsStatic: false, ReachableThroughSelf: false,
                IsInherited: false, CSharpName: name, Symbol: resolution.CrossedMember,
                Type: SemanticType.Unknown);
    }

    /// <summary>
    /// Whether a <c>self</c> parameter is bound in the current scope chain. Asked of the chain
    /// rather than of a "is this a static method" flag so every host answers for itself — a lambda
    /// inside an instance method has one, a lambda in a class-field initializer does not.
    /// </summary>
    private bool SelfIsInScope()
        => _symbolTable.Lookup(PythonNames.Self, searchParents: true) is VariableSymbol;

    /// <summary>
    /// Records the module-shadow crossing for <paramref name="node"/> when the bare
    /// <paramref name="name"/> reaches a MODULE-level variable past a class or struct body that
    /// declares the same name (#1786, R-Y).
    /// </summary>
    /// <remarks>
    /// The binding is correct — Python resolves the module variable too, and Sharpy's
    /// write-through rule (variable_scoping.md, Write-Through Assignment) makes a bare store reach
    /// that same module slot. The EMISSION is not: a bare name in the generated C# method body
    /// binds the field, so a read printed the field's value and a store wrote the field's storage.
    /// ONE recording point for every position — the read, all four store forms — because the
    /// emitter needs the same qualification at each of them and a position that forgets to record
    /// is silently wrong rather than loud.
    /// </remarks>
    private void RecordModuleAccessCrossingClassMember(string name, Expression node)
    {
        if (_symbolTable.Resolve(name) is
            { Bound: VariableSymbol, CrossedMember: not null, DeclaringScope: { } declaringScope }
            && SymbolTable.ClassifyScope(declaringScope.Name) == SymbolTable.ScopeKind.Module)
        {
            _semanticInfo.SetModuleAccessCrossesClassMember(node);
        }
    }

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
    /// The C# position a bare class-body read lands in (P21h D8, R-JJ): what C# resolves there is
    /// what Sharpy resolves there (<see cref="ClassBodyReadAdmits"/>).
    /// </summary>
    private enum ClassBodyReadPosition
    {
        /// <summary>A method, accessor, observer or lambda body: no sibling by bare name (#1786, R-Y).</summary>
        MethodBody,

        /// <summary>A plain class's instance field initializer, or an instance property's default in
        /// any host — a C# instance initializer (CS0236 for an instance member).</summary>
        InstanceInitializer,

        /// <summary>A <c>@static</c> field's initializer or a <c>@static</c> property's default — a C#
        /// static initializer.</summary>
        StaticInitializer,

        /// <summary>A <c>const</c> member's initializer. One that is not a compile-time constant is
        /// emitted <c>static readonly</c> (<c>ConstEligibility</c>), so it is a C# static initializer
        /// too; one that is, reads only constants anyway.</summary>
        ConstInitializer,

        /// <summary>A method's parameter default, or an instance FIELD's initializer in a struct or a
        /// <c>@dataclass</c> whose constructor is synthesized — that initializer is the synthesized
        /// constructor's parameter default (CS1736 for anything but a constant).</summary>
        ParameterDefault,
    }

    /// <summary>The declaring slot a refusal names; the position alone does not say whether an
    /// instance initializer is a field's or a property's.</summary>
    private enum ClassBodyReadSlot
    {
        Body,
        Field,
        Property,
        Const,
        Parameter,
        SynthesizedConstructorField,
    }

    /// <summary>
    /// The declaration a class-body read sits in: its classified position, its slot, and its name
    /// (the field, property or parameter a steer assigns).
    /// </summary>
    private readonly record struct ClassBodyReader(ClassBodyReadPosition Position, ClassBodyReadSlot Slot, string Name);

    private static readonly ClassBodyReader MethodBodyReader =
        new(ClassBodyReadPosition.MethodBody, ClassBodyReadSlot.Body, string.Empty);

    /// <summary>
    /// The class-body declaration whose initializer, default or parameter default is being checked,
    /// set around exactly that check: a field's or const's initializer (<c>CheckVariableDeclaration</c>,
    /// under its <c>initializer:</c> scope), a property's default (<c>CheckClassProperty</c>) and an
    /// eager parameter default (<c>RegisterFunctionParameters</c>). A property default and a
    /// parameter default are checked under the CLASS scope — no <c>initializer:</c> scope is pushed
    /// for either — so this is the ONLY input that tells them apart from any other class-body
    /// expression.
    /// </summary>
    private ClassBodyReader? _initializerOwner;

    /// <summary>
    /// The reader of a field or const initializer in the current type body. The host route is the
    /// one fact the position does not carry by itself: an instance field of a struct or a
    /// <c>@dataclass</c> with no explicit <c>__init__</c> is emitted as the synthesized
    /// constructor's PARAMETER DEFAULT (<c>StructConstructorRoster</c>,
    /// <c>IsSynthesizedConstructorField</c>), so it reads what a parameter default reads; the same
    /// predicate <c>ConstantPositionValidator</c> validates that slot by. Everything else classifies
    /// by position alone.
    /// </summary>
    private ClassBodyReader FieldInitializerReader(VariableDeclaration varDecl, TypeSymbol owner)
    {
        if (varDecl.IsConst)
            return new(ClassBodyReadPosition.ConstInitializer, ClassBodyReadSlot.Const, varDecl.Name);
        if (!MemberClassification.IsInstanceField(varDecl))
            return new(ClassBodyReadPosition.StaticInitializer, ClassBodyReadSlot.Field, varDecl.Name);

        // An explicit __init__ suppresses both syntheses; then the field keeps its own initializer.
        // NameResolver puts a declared __init__ in Methods; DataclassSynthesis adds its synthesized
        // one to Constructors only.
        bool synthesizedConstructorOwnsDefault = (owner.TypeKind == TypeKind.Struct || owner.IsDataclass)
            && !owner.Methods.Any(m => m.Name == DunderNames.Init);
        return synthesizedConstructorOwnsDefault
            ? new(ClassBodyReadPosition.ParameterDefault, ClassBodyReadSlot.SynthesizedConstructorField, varDecl.Name)
            : new(ClassBodyReadPosition.InstanceInitializer, ClassBodyReadSlot.Field, varDecl.Name);
    }

    /// <summary>The reader of a class property's default: instance or <c>@static</c> by the
    /// declaration's own decorator, a C# property initializer in every host (#1938).</summary>
    private static ClassBodyReader PropertyDefaultReader(PropertyDef propDef)
        => new(propDef.Decorators.Any(d => d.Name == DecoratorNames.Static)
                ? ClassBodyReadPosition.StaticInitializer
                : ClassBodyReadPosition.InstanceInitializer,
            ClassBodyReadSlot.Property, propDef.Name);

    /// <summary>The reader of an eager parameter default.</summary>
    private static ClassBodyReader ParameterDefaultReader(Parameter param)
        => new(ClassBodyReadPosition.ParameterDefault, ClassBodyReadSlot.Parameter, param.Name);

    /// <summary>
    /// The ONE position classifier (D8): the innermost scope that is not a block decides. A
    /// function-like scope is a method body (a lambda inside an initializer included — it is a
    /// body of its own); an <c>initializer:</c> scope, or the type body itself, is the declaration
    /// <see cref="_initializerOwner"/> names. Null for any other class-body expression (a decorator
    /// argument) and outside a type.
    /// </summary>
    private ClassBodyReader? ClassifyClassBodyReadPosition()
    {
        for (var scope = _symbolTable.CurrentScope; scope != null; scope = scope.Parent)
        {
            switch (SymbolTable.ClassifyScope(scope.Name))
            {
                case SymbolTable.ScopeKind.Block:
                case SymbolTable.ScopeKind.Transparent:
                    continue;
                case SymbolTable.ScopeKind.FunctionLike:
                    return MethodBodyReader;
                case SymbolTable.ScopeKind.InitializerHost:
                    Debug.Assert(_currentClass == null || _initializerOwner != null,
                        "an initializer: scope inside a type body is pushed only by CheckVariableDeclaration, "
                        + "which names its owner — a new push site must name one too");
                    return _initializerOwner;
                case SymbolTable.ScopeKind.TypeBody:
                    return _initializerOwner;
                default:
                    return null;
            }
        }

        return null;
    }

    /// <summary>
    /// The ONE admission table (D8): the measured C# rule. An instance, static or const initializer
    /// resolves a <c>const</c> and a <c>@static</c> field, property or method; a parameter default
    /// only a <c>const</c> (CS1736 for the rest); a method body nothing (#1786).
    /// </summary>
    private static bool ClassBodyReadAdmits(ClassBodyReadPosition position, TypeBodyMember member)
        => position switch
        {
            ClassBodyReadPosition.InstanceInitializer
                or ClassBodyReadPosition.StaticInitializer
                or ClassBodyReadPosition.ConstInitializer =>
                member.IsConstant || (member.IsStatic && member.Kind != TypeBodyMemberKind.Event),
            ClassBodyReadPosition.ParameterDefault => member.IsConstant,
            _ => false,
        };

    /// <summary>
    /// Resolves a bare read of a class-body sibling by the position it sits in (P21h D8, R-JJ), or
    /// returns null when the read is not one — outside a type, a name nothing in the type declares,
    /// or a name a scope INSIDE the position binds (a comprehension variable, a lambda parameter)
    /// or one outside the type body binds (a module variable keeps its own meaning).
    /// <list type="bullet">
    /// <item>A method body: today's crossing steer (#1786, R-Y), now for every member kind.</item>
    /// <item>A sibling the position admits: the read BINDS — typed from the member, the field or
    /// method symbol recorded on the identifier.</item>
    /// <item>A <c>@static</c> sibling in a parameter default binds too, as C# binds it before CS1736:
    /// the constant-position rule (SPY0401, <c>ConstantPositionValidator</c>) is its one verdict.</item>
    /// <item>Any other sibling: ONE SPY0200 whose steer names the position, marked as error
    /// recovery so nothing downstream reports the read again.</item>
    /// </list>
    /// </summary>
    private SemanticType? TryResolveClassBodySiblingRead(Identifier id, Symbol? bound)
    {
        if (ClassifyClassBodyReadPosition() is not { } reader)
            return null;

        // The crossing needs no enclosing-type symbol of its own: the walk names the type whose
        // body it crossed (DescribeCrossedClassMember).
        if (reader.Position == ClassBodyReadPosition.MethodBody)
        {
            if (bound != null || DescribeBareClassMemberRead(id.Name) is not { } crossedMemberTail)
                return null;

            AddError(
                $"Undefined identifier '{id.Name}'" + crossedMemberTail,
                id.LineStart, id.ColumnStart,
                code: DiagnosticCodes.Semantic.UndefinedVariable,
                span: id.Span);
            return SemanticType.Unknown;
        }

        if (_currentClass == null)
            return null;

        if (bound != null
            && !(_symbolTable.Resolve(id.Name) is { DeclaringScope: { } declaringScope } resolution
                && ReferenceEquals(resolution.Bound, bound)
                && SymbolTable.ClassifyScope(declaringScope.Name) == SymbolTable.ScopeKind.TypeBody))
        {
            return null;
        }

        if (TypeBodyMembership.FindMember(_currentClass, id.Name, reachableThroughSelf: true) is not { } member)
            return null;

        bool resolves = ClassBodyReadAdmits(reader.Position, member)
            || (reader.Position == ClassBodyReadPosition.ParameterDefault
                && member.IsStatic && member.Kind != TypeBodyMemberKind.Event);
        if (!resolves)
        {
            AddError(DescribeClassBodySiblingRefusal(reader, member),
                id.LineStart, id.ColumnStart,
                code: DiagnosticCodes.Semantic.UndefinedVariable,
                span: id.Span);
            MarkExpressionAsErrorRecovery(id,
                ErrorRecoveryReason.AlreadyReported(
                    "a class-body sibling the read's position cannot reach (SPY0200, R-JJ)"));
            return SemanticType.Unknown;
        }

        SemanticType type;
        switch (member.Symbol)
        {
            case VariableSymbol field:
                // The binding the type body made, when it made one (the symbol every other read of
                // the field records); the type's own field symbol for a forward read.
                var fieldSymbol = bound as VariableSymbol ?? field;
                _semanticInfo.SetIdentifierSymbol(id, fieldSymbol);
                type = GetVariableType(fieldSymbol);
                break;
            case FunctionSymbol method:
                _semanticInfo.SetIdentifierSymbol(id, method);
                type = member.Type;
                break;
            default:
                type = member.Type;
                break;
        }

        if (type is LiteralStringType)
            _semanticInfo.SetLiteralDerived(id);
        return type;
    }

    /// <summary>
    /// The SPY0200 text for a sibling the read's position refuses: the position's phrase, what the
    /// member is, what the position may read, and — for an instance member — where the read
    /// belongs instead. Each position names ONE phrase ("field initializer", "property default",
    /// "'@static' initializer", "'const' initializer", "parameter default") and no other's.
    /// </summary>
    private string DescribeClassBodySiblingRefusal(ClassBodyReader reader, TypeBodyMember member)
    {
        var name = member.Name;
        var owner = member.OwnerName;
        var level = member.IsStatic ? "'@static'" : "instance";
        var noun = member.Kind switch
        {
            TypeBodyMemberKind.Property => "property",
            TypeBodyMemberKind.Event => "event",
            TypeBodyMemberKind.Method => "method",
            _ => "attribute",
        };
        var what = $"'{name}' is {(member.IsStatic ? "a" : "an")} {level} {noun} of '{owner}'";
        var reachable = $"a 'const' or '@static' member of '{owner}'";
        var selfRead = member.IsStatic ? null
            : member.Kind == TypeBodyMemberKind.Method ? $"self.{name}()" : $"self.{name}";

        string Steer(string clause) => selfRead == null ? string.Empty : clause;

        return reader.Position switch
        {
            ClassBodyReadPosition.InstanceInitializer when reader.Slot == ClassBodyReadSlot.Property =>
                $"Undefined identifier '{name}' in a property default. {what} — a property default runs before "
                + $"any instance exists, so it may read only {reachable}"
                + Steer($". Assign '{reader.Name}' in '__init__' with 'self.{reader.Name} = {selfRead}'"),
            ClassBodyReadPosition.InstanceInitializer =>
                $"Undefined identifier '{name}' in a field initializer. {what} — a field initializer runs before "
                + $"any instance exists, so it may read only {reachable}"
                + Steer($". Initialize '{reader.Name}' in '__init__' with 'self.{reader.Name} = {selfRead}'"),
            ClassBodyReadPosition.StaticInitializer =>
                $"Undefined identifier '{name}' in a '@static' initializer. {what} — a '@static' initializer runs "
                + $"once for the type, with no instance, so it may read only {reachable}",
            ClassBodyReadPosition.ConstInitializer =>
                $"Undefined identifier '{name}' in a 'const' initializer. {what} — a 'const' initializer that is not "
                + $"a compile-time constant runs as a 'static readonly' initializer, with no instance, so it may "
                + $"read only {reachable}",
            _ when reader.Slot == ClassBodyReadSlot.SynthesizedConstructorField =>
                $"Undefined identifier '{name}' in a parameter default. {what} — the default of instance field "
                + $"'{reader.Name}' in {(_currentClass?.TypeKind == TypeKind.Struct ? "struct" : "dataclass")} "
                + $"'{_currentClass?.Name}' is its synthesized constructor's parameter default, which must be a "
                + $"compile-time constant (a 'const' of '{owner}')"
                + Steer($". Assign 'self.{reader.Name} = {selfRead}' in "
                    + $"'{(_currentClass?.TypeKind == TypeKind.Struct ? DunderNames.Init : DunderNames.PostInit)}' instead"),
            _ =>
                $"Undefined identifier '{name}' in a parameter default. {what} — a parameter default must be a "
                + $"compile-time constant (a 'const' of '{owner}')"
                + Steer($". Default '{reader.Name}' to None and read '{selfRead}' in the body"),
        };
    }

    /// <summary>
    /// Refuses a bare STORE whose target names an enclosing type's member (R-Y, R-AA), and reports
    /// true when it did. Called from every store form — plain, augmented, <c>??=</c>, walrus and
    /// tuple element — before the target is otherwise resolved, so the write is refused by name
    /// rather than being reported as an unresolved READ (augmented, <c>??=</c>) or silently
    /// declaring a fresh local (walrus, tuple element). Every member the description names is
    /// refused: own or inherited, field or property (#1803).
    /// </summary>
    /// <param name="bindingNode">
    /// The node consumers ask "did this store declare a local?" of — the target identifier for
    /// the plain and tuple-element forms, the walrus expression itself. The refused store is
    /// recorded there as <see cref="TargetBindingKind.Rebinds"/>: it names a MEMBER, never a fresh
    /// local, so the unused-variable validator must not add "'v' is assigned but never used" under
    /// the refusal (it did, at fa0c9e39a, for every store form that would have declared one), the
    /// outline must not list it as a declaration, and no consumer has to know the rule exists.
    /// </param>
    private bool TryRefuseBareClassAttributeStore(
        string targetName, Node bindingNode, BareStoreForm form, int line, int column, TextSpan? span)
    {
        // The store set stays fields and properties (#1803; lead ruling (a), 2026-10-28). The ONE
        // membership predicate also names methods and events now (P21h D8, for the read steer), and
        // this refusal filters on the member's KIND: a method or event is not storage, so a bare store
        // of its name in a method body declares a local, as it did before D8 and as C# and Python
        // both do.
        if (DescribeCrossedClassMember(targetName) is not
            { Kind: TypeBodyMemberKind.Field or TypeBodyMemberKind.Property } member)
            return false;

        _semanticInfo.SetTargetBinding(bindingNode, new TargetBinding(TargetBindingKind.Rebinds));

        var write = form switch
        {
            BareStoreForm.Plain => "Cannot assign to",
            BareStoreForm.Augmented => "Cannot augment-assign to",
            BareStoreForm.Walrus => "Cannot bind with ':=' to",
            BareStoreForm.TupleElement => "Cannot unpack into",
            BareStoreForm.CoalesceAssign => "Cannot assign with '??=' to",
            _ => "Cannot assign to",
        };

        var kind = (member.IsInherited ? "inherited " : string.Empty)
            + (member.IsConstant ? "class constant" : "class attribute");
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

        // `let` is the explicit new-local spelling (#1974); the annotated shadow stays second until
        // Stage 2 retires it as a declaration trigger.
        steers.Add($"'let {targetName} = ...' to declare a new local");
        steers.Add($"'{targetName}: <type> = ...' to declare a shadowing local");
        message += $". Use {string.Join(", or ", steers)}";

        AddError(message, line, column,
            code: DiagnosticCodes.SemanticOverflow.ClassAttributeBareStore, span: span);
        return true;
    }
}
