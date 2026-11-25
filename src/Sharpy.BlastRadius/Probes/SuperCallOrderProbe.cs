using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Validation;
using Sharpy.Compiler.Shared;

namespace Sharpy.BlastRadius.Probes;

/// <summary>
/// R-RO (owner, 2026-11-25), as amended by R-RQ: a derived <c>__init__</c> assigns the fields its own
/// class declares without an initializer BEFORE <c>super().__init__(...)</c>; a <c>var</c> field
/// whose type has a default need not be.
///
/// <para>Today the call must come first (SPY0292), so every program that compiles has the order the
/// rule refuses or has nothing to move. The probe says which, per derived <c>__init__</c>.</para>
///
/// <para>The <c>let</c> / <c>var</c> keywords do not parse on a field yet (#1974), so a field's
/// binding is not in the source. The probe infers the one fact that matters: a field written outside
/// its first assignment in <c>__init__</c> has to be <c>var</c>.</para>
/// </summary>
internal sealed class SuperCallOrderProbe : IRuleProbe
{
    public const string Refused = "refused";
    public const string RefusedIfLet = "refused-if-let";
    public const string Conforms = "conforms";
    public const string OutOfScope = "out-of-scope";
    public const string NotRuled = "not-ruled";

    public const string FixMove = "move";
    public const string FixRestructure = "restructure";
    public const string FixUsesSelf = "uses-self";

    public string Rule => "R-RO";

    public string Title =>
        "A derived __init__ assigns its own fields before super().__init__(...) (Swift order)";

    public string SiteNoun => "__init__ of a class that has a base class";

    public IReadOnlyList<VerdictInfo> Verdicts { get; } = new VerdictInfo[]
    {
        new(Refused,
            "A slot with no default (a field of non-nullable reference type, a reference-type "
            + "auto-property, a get-only or init auto-property) is assigned after the call. The rule "
            + "refuses this constructor however the class is migrated to let / var."),
        new(RefusedIfLet,
            "Only fields whose type has a default (value type, T | None, T?) are assigned after the "
            + "call, and at least one of them is never written again, so it could be declared let. "
            + "Refused if it is; accepted if every such field is declared var."),
        new(Conforms,
            "Nothing the rule asks to be moved."),
        new(OutOfScope,
            "The constructor writes no super().__init__(...) call (it chains to self.__init__, or "
            + "calls neither). R-RO has nothing to order; R-RS decides whether the call is owed."),
        new(NotRuled,
            "A @dataclass with a base class. The owner has not ruled it; `as-if` says what the "
            + "rule's text would give."),
    };

    public IReadOnlyList<AxisInfo> Axes { get; } = new AxisInfo[]
    {
        new("base", "What the class derives from.", new[]
        {
            ("sharpy", "a class written in Sharpy"),
            ("dotnet", "a .NET class, which includes the built-in exception types"),
        }),
        new("fix", "The largest edit any one assignment needs to get ahead of the call.", new[]
        {
            (FixMove, "a top-level `self.f = <value>` whose value reads only parameters, literals, "
                + "module names and own fields assigned before it: cut and paste above the call"),
            (FixRestructure, "the assignment sits under control flow, or its value reads a local "
                + "computed in the constructor: the value has to become one expression or a @static helper"),
            (FixUsesSelf, "the value reads `self` beyond own fields already assigned (an inherited "
                + "member, a method call, `self` passed on): it depends on state the base "
                + "constructor sets, so the constructor needs a redesign"),
        }),
        new("slots", "What kind of member has to move.", new[]
        {
            ("fields", "fields only"),
            ("properties", "auto-properties only"),
            ("both", "fields and auto-properties"),
        }),
        new("why", "For a conforming site, why nothing moves.", new[]
        {
            ("no-own-slots", "the class declares no field or auto-property without an initializer"),
            ("none-after-call", "it declares some, and this constructor assigns none of them after the call"),
            ("var-only", "the ones assigned after the call have a default and are written again, so they are var"),
        }),
        new("call", "For an out-of-scope site, what the constructor chains to.", new[]
        {
            ("self", "self.__init__(...)"),
            ("none", "no chaining call"),
        }),
        new("base-init", "For an out-of-scope site, whether a base class declares an __init__. "
            + "With `call: none` this is R-RS's question: R-RS asks for the call where a base has one.", new[]
        {
            ("declared", "a Sharpy base class, at some depth, declares __init__"),
            ("none", "no base class declares one"),
            ("dotnet", "the chain reaches a .NET class first; R-RS does not rule that case"),
        }),
        new("as-if", "For a not-ruled site, the verdict the rule's text would give.", new[]
        {
            (Refused, ""), (RefusedIfLet, ""), (Conforms, ""), (OutOfScope, ""),
        }),
    };

    public IReadOnlyList<string> Limits { get; } = new[]
    {
        "A write to a field from another file is not seen, so a field counted as possibly `let` may "
            + "in fact have to be `var`: `refused-if-let` is an upper bound.",
        "A base class in another file is resolved only when the file compiles on its own.",
        "The probe does not check that moving an assignment keeps the program's meaning; `move` "
            + "says the edit is mechanical, not that it was tried.",
        "Statements other than own-field assignments stay illegal ahead of the call (R-ML); a "
            + "`restructure` site may need more than the one value rewritten.",
    };

    public IEnumerable<ProbeSite> Probe(ProbeInput input)
    {
        var index = NestedTypeIndex.Build(input.Ast, input.Symbols);
        var stores = StoreCensus.Collect(input.Ast, input.Semantic, index, input.Symbols);

        foreach (var classDef in Descendants(input.Ast).OfType<ClassDef>())
        {
            if (classDef.BaseClasses.Length == 0)
                continue;
            if (index.SymbolFor(classDef, classDef.Name, input.Symbols) is not { BaseType: { } baseType } type)
                continue; // interfaces only, or an unresolved declaration: no base class

            var baseKind = baseType.ClrType != null ? "dotnet" : "sharpy";
            var slots = OwnSlots(classDef, type);
            var constructors = Members(classDef).OfType<FunctionDef>()
                .Where(f => f.Name == DunderNames.Init).ToList();

            if (type.IsDataclass && constructors.Count == 0)
            {
                yield return new ProbeSite
                {
                    Subject = classDef.Name,
                    Line = classDef.LineStart,
                    Verdict = NotRuled,
                    Coords = new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["base"] = baseKind,
                        ["as-if"] = slots.Any(s => !s.HasDefault) ? Refused
                            : slots.Any(s => !stores.WrittenAgain(type, s.Name)) ? RefusedIfLet
                            : Conforms,
                    },
                    Detail = slots.Count == 0
                        ? "generated constructor; no own slot without an initializer"
                        : "generated constructor assigns " + string.Join(", ", slots.Select(s => s.Describe()))
                          + " after the base call",
                };
                continue;
            }

            foreach (var constructor in constructors)
            {
                var site = Judge(classDef, type, constructor, slots, stores, baseKind);
                yield return type.IsDataclass
                    ? site with
                    {
                        Verdict = NotRuled,
                        Coords = new SortedDictionary<string, string>(
                            site.Coords.ToDictionary(kv => kv.Key, kv => kv.Value), StringComparer.Ordinal)
                        {
                            ["as-if"] = site.Verdict,
                        },
                    }
                    : site;
            }
        }
    }

    private static ProbeSite Judge(
        ClassDef classDef, TypeSymbol type, FunctionDef constructor,
        IReadOnlyList<Slot> slots, StoreCensus stores, string baseKind)
    {
        var coords = new SortedDictionary<string, string>(StringComparer.Ordinal) { ["base"] = baseKind };
        ProbeSite Site(string verdict, string detail) => new()
        {
            Subject = $"{classDef.Name}.{DunderNames.Init}",
            Line = constructor.LineStart,
            Verdict = verdict,
            Coords = coords,
            Detail = detail,
        };

        var callIndex = AstHelper.FindConstructorInitializerIndex(constructor.Body);
        if (callIndex < 0)
        {
            coords["call"] = "none";
            coords["base-init"] = BaseInit(type);
            return Site(OutOfScope, "no chaining call");
        }

        AstHelper.TryGetConstructorInitializerCall(constructor.Body[callIndex], out _, out var chainsToBase);
        if (!chainsToBase)
        {
            coords["call"] = "self";
            coords["base-init"] = BaseInit(type);
            return Site(OutOfScope, "chains to self.__init__");
        }

        if (slots.Count == 0)
        {
            coords["why"] = "no-own-slots";
            return Site(Conforms, "");
        }

        // What the constructor assigns after the call, in source order.
        var slotByName = slots.ToDictionary(s => s.Name, StringComparer.Ordinal);
        var locals = LocalNames(constructor);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var movedNames = new HashSet<string>(StringComparer.Ordinal);
        var moved = new List<(Slot Slot, string Fix, bool MustMove)>();

        foreach (var (assignment, topLevel) in AssignmentsAfter(constructor, callIndex))
        {
            foreach (var target in SelfTargets(assignment.Target))
            {
                if (!slotByName.TryGetValue(target, out var slot) || !seen.Add(target))
                    continue;

                var fix = !topLevel ? FixRestructure : ValueFix(assignment.Value, locals, movedNames);
                var mustBeVar = stores.WrittenAgain(type, slot.Name);
                // A slot with a default that has to be `var` may rest on that default until the
                // constructor reaches it (R-RQ): it is not one of the slots the rule moves.
                if (slot.HasDefault && mustBeVar)
                    continue;
                moved.Add((slot, fix, MustMove: !slot.HasDefault));
                movedNames.Add(target);
            }
        }

        if (moved.Count == 0)
        {
            coords["why"] = seen.Count == 0 ? "none-after-call" : "var-only";
            return Site(Conforms, "");
        }

        var verdict = moved.Any(m => m.MustMove) ? Refused : RefusedIfLet;
        // A refused site is costed by the slots that must move; the others ride along if they can.
        var counted = verdict == Refused ? moved.Where(m => m.MustMove).ToList() : moved;
        coords["fix"] = counted.Max(m => FixRank(m.Fix)) switch
        {
            2 => FixUsesSelf,
            1 => FixRestructure,
            _ => FixMove,
        };
        var anyField = counted.Any(m => !m.Slot.IsProperty);
        var anyProperty = counted.Any(m => m.Slot.IsProperty);
        coords["slots"] = anyField && anyProperty ? "both" : anyProperty ? "properties" : "fields";

        return Site(verdict, string.Join("; ", moved.Select(m => $"{m.Slot.Describe()} ({m.Fix})")));
    }

    /// <summary>Whether a Sharpy base class, at any depth, declares an <c>__init__</c>.</summary>
    private static string BaseInit(TypeSymbol type)
    {
        for (var ancestor = type.BaseType; ancestor != null; ancestor = ancestor.BaseType)
        {
            if (ancestor.ClrType != null)
                return "dotnet";
            if (ancestor.Constructors.Count > 0)
                return "declared";
        }

        return "none";
    }

    private static int FixRank(string fix) => fix switch
    {
        FixUsesSelf => 2,
        FixRestructure => 1,
        _ => 0,
    };

    /// <summary>What the value of a top-level assignment needs before it can stand ahead of the call.</summary>
    private static string ValueFix(Expression value, HashSet<string> locals, HashSet<string> movedAhead)
    {
        var rank = 0;
        Visit(value);
        return rank switch { 2 => FixUsesSelf, 1 => FixRestructure, _ => FixMove };

        void Visit(Node node)
        {
            switch (node)
            {
                case SuperExpression:
                    rank = 2;
                    return;
                // `self.f` where f is an own slot already assigned ahead: R-RN allows the read.
                case MemberAccess { Object: Identifier { Name: PythonNames.Self } } member
                    when movedAhead.Contains(member.Member):
                    return;
                case Identifier { Name: PythonNames.Self }:
                    rank = 2;
                    return;
                case Identifier identifier when locals.Contains(identifier.Name):
                    rank = Math.Max(rank, 1);
                    return;
            }

            foreach (var child in node.GetChildNodes())
                Visit(child);
        }
    }

    /// <summary>
    /// Plain assignments after the chaining call, with whether each is a top-level statement of the
    /// constructor body. A nested function, lambda or class runs later (or never), so its stores are
    /// not constructor assignments.
    /// </summary>
    private static IEnumerable<(Assignment Assignment, bool TopLevel)> AssignmentsAfter(
        FunctionDef constructor, int callIndex)
    {
        for (var i = callIndex + 1; i < constructor.Body.Length; i++)
        {
            var statement = Unwrap(constructor.Body[i]);
            if (statement is Assignment { Operator: AssignmentOperator.Assign } top)
            {
                yield return (top, true);
                continue;
            }

            foreach (var nested in DescendantsInSameFunction(statement).OfType<Assignment>())
            {
                if (nested.Operator == AssignmentOperator.Assign)
                    yield return (nested, false);
            }
        }
    }

    /// <summary>The members <c>self.a</c>, <c>self.b</c> a target stores to: one, or the elements of a tuple target.</summary>
    internal static IEnumerable<string> SelfTargets(Expression target)
    {
        switch (target)
        {
            case MemberAccess { Object: Identifier { Name: PythonNames.Self } } member:
                yield return member.Member;
                break;
            case Parenthesized parenthesized:
                foreach (var name in SelfTargets(parenthesized.Expression))
                    yield return name;
                break;
            case TupleLiteral tuple:
                foreach (var element in tuple.Elements)
                {
                    foreach (var name in SelfTargets(element))
                        yield return name;
                }
                break;
            case StarExpression star:
                foreach (var name in SelfTargets(star.Operand))
                    yield return name;
                break;
        }
    }

    /// <summary>Names a constructor binds as locals: targets of assignments, declarations, loops and walruses.</summary>
    private static HashSet<string> LocalNames(FunctionDef constructor)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in constructor.Body.SelectMany(DescendantsInSameFunction))
        {
            switch (node)
            {
                case Assignment assignment:
                    AddNames(assignment.Target);
                    break;
                case VariableDeclaration declaration:
                    names.Add(declaration.Name);
                    break;
                case ForStatement loop:
                    AddNames(loop.Target);
                    break;
                case WalrusExpression walrus:
                    names.Add(walrus.Target);
                    break;
            }
        }

        return names;

        void AddNames(Expression target)
        {
            switch (target)
            {
                case Identifier identifier:
                    names.Add(identifier.Name);
                    break;
                case Parenthesized parenthesized:
                    AddNames(parenthesized.Expression);
                    break;
                case TupleLiteral tuple:
                    foreach (var element in tuple.Elements)
                        AddNames(element);
                    break;
                case StarExpression star:
                    AddNames(star.Operand);
                    break;
            }
        }
    }

    /// <summary>
    /// The instance slots the class itself declares with no initializer: fields, and the
    /// auto-properties that must be given a value (R-RP, R-RU).
    /// </summary>
    private static List<Slot> OwnSlots(ClassDef classDef, TypeSymbol type)
    {
        var slots = new List<Slot>();
        foreach (var member in Members(classDef))
        {
            switch (member)
            {
                case VariableDeclaration { InitialValue: null, IsConst: false, Type: not null } field:
                    if (type.Fields.FirstOrDefault(f => f.Name == field.Name) is { IsStatic: false } symbol
                        && TypeClass.Of(symbol.Type) is { } fieldClass)
                    {
                        slots.Add(new Slot(field.Name, symbol.Type.GetDisplayName(), fieldClass,
                            IsProperty: false, HasDefault: TypeClass.HasDefault(fieldClass)));
                    }
                    break;

                case PropertyDef { IsFunctionStyle: false, DefaultValue: null, ExplicitInterface: null } property
                    when property.Body.Length == 0:
                    if (type.FindOwnProperty(property.Name) is { IsStatic: false, IsAbstract: false } propertySymbol
                        && TypeClass.Of(propertySymbol.Type) is { } propertyClass)
                    {
                        // A get-only or init auto-property takes no zero default (R-RU, SPY0426);
                        // a plain one does when its type has one (R-RP).
                        var readOnly = property.Accessor is PropertyAccessor.Get or PropertyAccessor.Init;
                        var hasDefault = !readOnly && TypeClass.HasDefault(propertyClass);
                        if (!hasDefault)
                        {
                            slots.Add(new Slot(property.Name, propertySymbol.Type.GetDisplayName(),
                                propertyClass, IsProperty: true, HasDefault: false));
                        }
                    }
                    break;
            }
        }

        return slots;
    }

    private static IEnumerable<Statement> Members(ClassDef classDef) => classDef.Body.Select(Unwrap);

    private static Statement Unwrap(Statement statement)
        => statement is DecoratedStatement decorated ? Unwrap(decorated.Statement) : statement;

    internal static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var child in node.GetChildNodes())
        {
            yield return child;
            foreach (var below in Descendants(child))
                yield return below;
        }
    }

    /// <summary>The node and everything under it that runs as part of the same function body.</summary>
    internal static IEnumerable<Node> DescendantsInSameFunction(Node node)
    {
        yield return node;
        if (node is FunctionDef or LambdaExpression or ClassDef or StructDef)
            yield break;
        foreach (var child in node.GetChildNodes())
        {
            if (child is FunctionDef or LambdaExpression or ClassDef or StructDef)
                continue;
            foreach (var below in DescendantsInSameFunction(child))
                yield return below;
        }
    }

    internal sealed record Slot(string Name, string Type, string Class, bool IsProperty, bool HasDefault)
    {
        public string Describe() => $"{(IsProperty ? "property " : "")}{Name}: {Type}";
    }
}

/// <summary>The four kinds of type the initialization rulings tell apart (R-RQ).</summary>
internal static class TypeClass
{
    public const string ValueType = "value-type";
    public const string Nullable = "nullable";
    public const string Optional = "optional";
    public const string ReferenceType = "reference-type";

    /// <summary>Null when the type did not resolve: the probe then leaves the slot out.</summary>
    public static string? Of(SemanticType type) => type switch
    {
        UnknownType => null,
        OptionalType => Optional,
        NullableType => Nullable,
        // A bare type parameter has no default the rulings name: T may be a reference type.
        TypeParameterType => ReferenceType,
        { IsValueType: true } => ValueType,
        _ => ReferenceType,
    };

    /// <summary>A value type rests on zero, <c>T | None</c> on <c>None</c>, <c>T?</c> on <c>None()</c>.</summary>
    public static bool HasDefault(string typeClass) => typeClass != ReferenceType;
}

/// <summary>
/// Every store to a member in one file, so a probe can tell a field that is assigned once in
/// <c>__init__</c> (it could be <c>let</c>) from one that is written again (it has to be <c>var</c>).
/// </summary>
internal sealed class StoreCensus
{
    private readonly HashSet<(TypeSymbol Owner, string Member)> _writtenAgain = new();

    public bool WrittenAgain(TypeSymbol owner, string member) => _writtenAgain.Contains((owner, member));

    public static StoreCensus Collect(
        Module module, SemanticInfo semantic, NestedTypeIndex index, SymbolTable symbols)
    {
        var census = new StoreCensus();
        Walk(module, enclosing: null, assigned: null, inLoop: false);
        return census;

        // `assigned` is non-null while walking the straight-line body of an __init__: the fields a
        // plain `self.f = …` has already assigned on the path to this node. The first such store on
        // a path is the assignment a `let` field allows; any other store means the field is `var`.
        void Walk(Node node, TypeSymbol? enclosing, HashSet<(TypeSymbol, string)>? assigned, bool inLoop)
        {
            switch (node)
            {
                case ClassDef classDef:
                    enclosing = index.SymbolFor(classDef, classDef.Name, symbols);
                    assigned = null;
                    break;
                case StructDef:
                    enclosing = null;
                    assigned = null;
                    break;
                case FunctionDef function:
                    // A def nested in __init__ runs later, or more than once: not the first assignment.
                    assigned = assigned == null && function.Name == DunderNames.Init ? new() : null;
                    inLoop = false;
                    break;
                case LambdaExpression:
                    assigned = null;
                    break;
                case WhileStatement or ForStatement:
                    if (node is ForStatement loop)
                    {
                        foreach (var member in MemberTargets(loop.Target))
                            Record(member, plain: false, enclosing, assigned, inLoop);
                    }
                    inLoop = true;
                    break;
                case Assignment assignment:
                    foreach (var member in MemberTargets(assignment.Target))
                        Record(member, assignment.Operator == AssignmentOperator.Assign, enclosing, assigned, inLoop);
                    break;

                // Branches are alternatives: a field assigned once on each is assigned once.
                case IfStatement branch when assigned != null:
                    Walk(branch.Test, enclosing, assigned, inLoop);
                    var arms = new List<IEnumerable<Node>> { branch.ThenBody };
                    arms.AddRange(branch.ElifClauses.Select(e => e.Body.Cast<Node>().Prepend(e.Test)));
                    arms.Add(branch.ElseBody);
                    WalkAlternatives(arms, enclosing, assigned, inLoop);
                    return;
                case MatchStatement match when assigned != null:
                    Walk(match.Scrutinee, enclosing, assigned, inLoop);
                    WalkAlternatives(match.Cases.Select(c => c.Body.Cast<Node>()), enclosing, assigned, inLoop);
                    return;
            }

            foreach (var child in node.GetChildNodes())
                Walk(child, enclosing, assigned, inLoop);
        }

        void WalkAlternatives(IEnumerable<IEnumerable<Node>> arms, TypeSymbol? enclosing,
            HashSet<(TypeSymbol, string)> assigned, bool inLoop)
        {
            var after = new HashSet<(TypeSymbol, string)>(assigned);
            foreach (var arm in arms)
            {
                var onThisArm = new HashSet<(TypeSymbol, string)>(assigned);
                foreach (var node in arm)
                    Walk(node, enclosing, onThisArm, inLoop);
                after.UnionWith(onThisArm);
            }

            assigned.UnionWith(after);
        }

        void Record(MemberAccess member, bool plain, TypeSymbol? enclosing,
            HashSet<(TypeSymbol, string)>? assigned, bool inLoop)
        {
            var isSelf = member.Object is Identifier { Name: PythonNames.Self };
            // The checker's own resolution names the declaring type; a `self.f` store it did not
            // record belongs to the class the store is written in.
            var owner = semantic.GetMemberAccessResolution(member)?.Owner ?? (isSelf ? enclosing : null);
            if (owner == null)
                return;
            if (plain && isSelf && assigned != null && !inLoop && assigned.Add((owner, member.Member)))
                return;
            census._writtenAgain.Add((owner, member.Member));
        }
    }

    private static IEnumerable<MemberAccess> MemberTargets(Expression target)
    {
        switch (target)
        {
            case MemberAccess member:
                yield return member;
                break;
            case Parenthesized parenthesized:
                foreach (var member in MemberTargets(parenthesized.Expression))
                    yield return member;
                break;
            case TupleLiteral tuple:
                foreach (var element in tuple.Elements)
                {
                    foreach (var member in MemberTargets(element))
                        yield return member;
                }
                break;
            case StarExpression star:
                foreach (var member in MemberTargets(star.Operand))
                    yield return member;
                break;
        }
    }
}
