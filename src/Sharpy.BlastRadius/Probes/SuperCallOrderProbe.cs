using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
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

    public const string CallOmitted = "omitted";
    public const string CallChained = "chained-to-own-init";

    /// <summary>The edit one assignment needs, cheapest first.</summary>
    private enum Fix { Move, Restructure, UsesSelf }

    private static readonly string[] FixNames = { FixMove, FixRestructure, FixUsesSelf };

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
        // `call` and its values are the ones the rulings' own axis list uses (axes.yaml,
        // super-call-presence).
        new("call", "For an out-of-scope site, what the constructor writes in place of the call.", new[]
        {
            (CallOmitted, "no chaining call"),
            (CallChained, "self.__init__(...)"),
        }),
        new("base-has-init", "For an out-of-scope site, whether a base class declares an __init__. "
            + "With `call: omitted` this is R-RS's question: R-RS asks for the call where a base has one.", new[]
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
        "The arms of an `if` or a `match` count as one assignment of a field. A `try` body and its "
            + "handlers do not: a field assigned in both is taken to be `var`. No ruling says yet how "
            + "`let` treats that shape.",
        "A base class in another file is resolved only when the file compiles on its own.",
        "The probe does not check that moving an assignment keeps the program's meaning; `move` "
            + "says the edit is mechanical, not that it was tried.",
        "Statements other than own-field assignments stay illegal ahead of the call (R-ML); a "
            + "`restructure` site may need more than the one value rewritten.",
    };

    public IEnumerable<ProbeSite> Probe(ProbeInput input)
    {
        var stores = StoreCensus.Collect(input);

        foreach (var classDef in SyntaxFacts.Descendants(input.Ast).OfType<ClassDef>())
        {
            if (classDef.BaseClasses.Length == 0)
                continue;
            if (input.Types.SymbolFor(classDef, classDef.Name, input.Symbols) is not { BaseType: { } baseType } type)
                continue; // interfaces only, or an unresolved declaration: no base class

            var baseKind = baseType.ClrType != null ? "dotnet" : "sharpy";
            var slots = OwnSlots.Of(classDef, type);
            var constructors = SyntaxFacts.Members(classDef).OfType<FunctionDef>()
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
                yield return Judge(classDef, type, constructor, slots, stores, baseKind);
        }
    }

    private static ProbeSite Judge(
        ClassDef classDef, TypeSymbol type, FunctionDef constructor,
        IReadOnlyList<Slot> slots, StoreCensus stores, string baseKind)
    {
        var coords = new SortedDictionary<string, string>(StringComparer.Ordinal) { ["base"] = baseKind };
        ProbeSite Site(string verdict, string detail)
        {
            // The owner has not ruled a @dataclass with a base: say what the rule's text would give.
            if (type.IsDataclass)
            {
                coords["as-if"] = verdict;
                verdict = NotRuled;
            }

            return new()
            {
                Subject = $"{classDef.Name}.{DunderNames.Init}",
                Line = constructor.LineStart,
                Verdict = verdict,
                Coords = coords,
                Detail = detail,
            };
        }

        var callIndex = AstHelper.FindConstructorInitializerIndex(constructor.Body);
        if (callIndex < 0)
        {
            coords["call"] = CallOmitted;
            coords["base-has-init"] = BaseHasInit(type);
            return Site(OutOfScope, "no chaining call");
        }

        AstHelper.TryGetConstructorInitializerCall(constructor.Body[callIndex], out _, out var chainsToBase);
        if (!chainsToBase)
        {
            coords["call"] = CallChained;
            coords["base-has-init"] = BaseHasInit(type);
            return Site(OutOfScope, "chains to self.__init__");
        }

        if (slots.Count == 0)
        {
            coords["why"] = "no-own-slots";
            return Site(Conforms, "");
        }

        // What the constructor assigns after the call, in source order.
        var slotByName = slots.ToDictionary(s => s.Name, StringComparer.Ordinal);
        var locals = SyntaxFacts.LocalNames(constructor);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var moved = new List<(Slot Slot, Fix Fix)>();
        var fixOf = new Dictionary<string, Fix>(StringComparer.Ordinal);

        foreach (var (assignment, topLevel) in AssignmentsAfter(constructor, callIndex))
        {
            foreach (var target in SyntaxFacts.TargetLeaves(assignment.Target).OfType<MemberAccess>())
            {
                if (target.Object is not Identifier { Name: PythonNames.Self }
                    || !slotByName.TryGetValue(target.Member, out var slot) || !seen.Add(slot.Name))
                    continue;

                // A slot with a default that has to be `var` may rest on that default until the
                // constructor reaches it (R-RQ): it is not one of the slots the rule moves.
                if (slot.HasDefault && stores.WrittenAgain(type, slot.Name))
                    continue;
                var fix = topLevel ? ValueFix(assignment.Value, locals, fixOf) : Fix.Restructure;
                moved.Add((slot, fix));
                fixOf[slot.Name] = fix;
            }
        }

        if (moved.Count == 0)
        {
            coords["why"] = seen.Count == 0 ? "none-after-call" : "var-only";
            return Site(Conforms, "");
        }

        // A refused site is costed by the slots that must move; the others ride along if they can.
        var mustMove = moved.Where(m => !m.Slot.HasDefault).ToList();
        var counted = mustMove.Count > 0 ? mustMove : moved;
        coords["fix"] = FixNames[(int)counted.Max(m => m.Fix)];
        var anyField = counted.Any(m => !m.Slot.IsProperty);
        var anyProperty = counted.Any(m => m.Slot.IsProperty);
        coords["slots"] = anyField && anyProperty ? "both" : anyProperty ? "properties" : "fields";

        return Site(mustMove.Count > 0 ? Refused : RefusedIfLet,
            string.Join("; ", moved.Select(m => $"{m.Slot.Describe()} ({FixNames[(int)m.Fix]})")));
    }

    /// <summary>Whether a Sharpy base class, at any depth, declares an <c>__init__</c>.</summary>
    private static string BaseHasInit(TypeSymbol type)
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

    /// <summary>What the value of a top-level assignment needs before it can stand ahead of the call.</summary>
    private static Fix ValueFix(Expression value, HashSet<string> locals, Dictionary<string, Fix> movedAhead)
    {
        var fix = Fix.Move;
        Visit(value);
        return fix;

        void Visit(Node node)
        {
            switch (node)
            {
                case SuperExpression:
                    fix = Fix.UsesSelf;
                    return;
                // `self.f` where f is an own slot that moves ahead as well: R-RN allows the read,
                // and this value can stand ahead of the call only as easily as f's own can.
                case MemberAccess { Object: Identifier { Name: PythonNames.Self } } member
                    when movedAhead.TryGetValue(member.Member, out var earlier):
                    if (fix < earlier)
                        fix = earlier;
                    return;
                case Identifier { Name: PythonNames.Self }:
                    fix = Fix.UsesSelf;
                    return;
                case Identifier identifier when locals.Contains(identifier.Name):
                    if (fix < Fix.Restructure)
                        fix = Fix.Restructure;
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
        foreach (var statement in constructor.Body.Skip(callIndex + 1))
        {
            foreach (var assignment in SyntaxFacts.InSameFunction(statement).OfType<Assignment>())
            {
                if (assignment.Operator == AssignmentOperator.Assign)
                    yield return (assignment, ReferenceEquals(assignment, statement));
            }
        }
    }
}
