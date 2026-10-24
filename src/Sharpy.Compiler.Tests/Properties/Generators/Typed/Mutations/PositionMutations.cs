using System.Collections.Immutable;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Tests.Properties.Generators.Typed;

/// <summary>
/// The one-fact breaks per AST position (#2324, P63b Phase 1 Task 3). Each break takes the program's
/// <see cref="ProgramFacts"/> and one <see cref="Site"/> and returns the mutated module, or null when it does
/// not apply to that site. <see cref="Mutate"/> is the driver every lane uses: normalize, enumerate the
/// position's sites, filter, and break the first applicable site starting at index
/// <c>StableHash(source) % count</c> — so samples that differ mutate different places, and the same sample
/// always mutates the same one (CsCheck's shrinking and a printed seed stay meaningful). There is no
/// diagnostic code anywhere in this file: the lanes in <c>ErrorInjector.Mutations.cs</c> carry them.
/// </summary>
internal static class PositionMutations
{
    public delegate Module? Break(ProgramFacts facts, Site site);

    /// <summary>The mutation driver: null when no site of <paramref name="position"/> passes
    /// <paramref name="filter"/> and takes <paramref name="breakAt"/>.</summary>
    public static Module? Mutate(Module module, Position position, Func<ProgramFacts, Site, bool> filter, Break breakAt)
    {
        var normalized = AstRewriter.Normalize(module);
        var facts = new ProgramFacts(normalized);
        var sites = MutationSites.Of(facts, position).Where(s => filter(facts, s)).ToList();
        if (sites.Count == 0)
            return null;
        var start = (int)(StableHash(Sharpy.Compiler.Pretty.Unparser.Unparse(normalized)) % (uint)sites.Count);
        for (int k = 0; k < sites.Count; k++)
        {
            if (breakAt(facts, sites[(start + k) % sites.Count]) is { } mutated)
                return mutated;
        }
        return null;
    }

    /// <summary>A whole-module break (a declaration shape the site enumerator does not list) on the
    /// normalized module.</summary>
    public static Module? MutateModule(Module module, Func<ProgramFacts, Module?> breakModule) =>
        breakModule(new ProgramFacts(AstRewriter.Normalize(module)));

    /// <summary>Sites whose family is one of <paramref name="families"/>.</summary>
    public static Func<ProgramFacts, Site, bool> Families(params string[] families) =>
        (_, site) => families.Contains(site.Family);

    /// <summary>FNV-1a over the UTF-16 code units of <paramref name="text"/> — never
    /// <c>string.GetHashCode</c>, which is randomized per process.</summary>
    public static uint StableHash(string text)
    {
        uint hash = 2166136261;
        foreach (var c in text)
        {
            hash ^= c;
            hash *= 16777619;
        }
        return hash;
    }

    // ---- replacement literals (Design Decision 3) ------------------------------------------------

    /// <summary>
    /// A well-formed literal of a type the slot <paramref name="slotType"/> refuses, or null when the facts
    /// cannot name one: int / float / bool → <c>"s"</c>; str → <c>42</c>; a list, a tuple, a user class →
    /// <c>42</c>; <c>T?</c> → the literal its payload refuses. Never a name: an undefined name breaks
    /// "defined", not "typed".
    /// </summary>
    public static Expression? RefusedLiteral(string? slotType, ProgramFacts facts) => slotType switch
    {
        null => null,
        "int" or "float" or "bool" => new StringLiteral { Value = "s" },
        "str" => new IntegerLiteral { Value = "42" },
        "int-convertible" => new ListLiteral { Elements = ImmutableArray.Create<Expression>(new IntegerLiteral { Value = "1" }) },
        var t when t.EndsWith('?') => RefusedLiteral(t[..^1], facts),
        var t when t.StartsWith("list[", StringComparison.Ordinal) || t.StartsWith("tuple[", StringComparison.Ordinal) =>
            new IntegerLiteral { Value = "42" },
        var t when facts.Types.ContainsKey(t) => new IntegerLiteral { Value = "42" },
        _ => null,
    };

    /// <summary>A tuple literal: a fixed tuple has no falsy case, so a truth-tested slot refuses it.</summary>
    public static Expression NoFalsyCase() => new TupleLiteral
    {
        Elements = ImmutableArray.Create<Expression>(new IntegerLiteral { Value = "1" }, new IntegerLiteral { Value = "2" })
    };

    public const string UndefinedName = "undefined_xyz";
    public const string UndefinedSuffix = "_xyz";
    public const string UndefinedType = "Nope_xyz";

    // ---- operand / store / argument breaks -------------------------------------------------------

    /// <summary>The site's expression replaced by a literal its slot type refuses.</summary>
    public static Module? WrongTypedLiteral(ProgramFacts facts, Site site) =>
        RefusedLiteral(site.SlotType, facts) is { } literal ? AstRewriter.Replace(facts.Module, site.Target, literal) : null;

    /// <summary>The site's expression replaced by a str literal — for an operand position whose operator
    /// refuses str (a unary <c>-</c>/<c>+</c>/<c>~</c>); null on a str slot.</summary>
    public static Module? StrOperand(ProgramFacts facts, Site site) =>
        site.SlotType is null or "str" ? null : AstRewriter.Replace(facts.Module, site.Target, new StringLiteral { Value = "s" });

    /// <summary>The site's expression (a truth-tested slot) replaced by a fixed tuple.</summary>
    public static Module? NoFalsyCaseLiteral(ProgramFacts facts, Site site) =>
        AstRewriter.Replace(facts.Module, site.Target, NoFalsyCase());

    /// <summary>A name read replaced by a name nothing defines.</summary>
    public static Module? UndefinedNameAtUse(ProgramFacts facts, Site site) =>
        site.Target is Identifier ? AstRewriter.Replace(facts.Module, site.Target, new Identifier { Name = UndefinedName }) : null;

    /// <summary>A list initializer of a <c>list[T]</c> declaration replaced by a one-element list of a
    /// literal <c>T</c> refuses.</summary>
    public static Module? WrongTypedListElement(ProgramFacts facts, Site site) =>
        site.SlotType is { } slot && slot.StartsWith("list[", StringComparison.Ordinal)
            && RefusedLiteral(slot[5..^1], facts) is { } element
            ? AstRewriter.Replace(facts.Module, site.Target, new ListLiteral { Elements = ImmutableArray.Create(element) })
            : null;

    /// <summary>A numeric-spec f-string hole of an int/float wrapped in <c>str(…)</c>: the hole is a str.</summary>
    public static Module? StrHoleUnderNumericSpec(ProgramFacts facts, Site site) =>
        site.SlotType is "int" or "float" && site.Target is Expression hole
            ? AstRewriter.Replace(facts.Module, site.Target, new FunctionCall
            {
                Function = new Identifier { Name = "str" },
                Arguments = ImmutableArray.Create(hole)
            })
            : null;

    /// <summary>The Optional operand of an identity test replaced by an <c>int</c> binding in scope.</summary>
    public static Module? NonOptionalIdentityOperand(ProgramFacts facts, Site site)
    {
        if (site.SlotType is not { } slot || !slot.EndsWith('?'))
            return null;
        var payload = slot[..^1];
        var name = new[] { "x", "y", "s", "n" }.FirstOrDefault(n => facts.NameType(n, site.Target) == payload);
        return name is null ? null : AstRewriter.Replace(facts.Module, site.Target, new Identifier { Name = name });
    }

    // ---- call breaks ------------------------------------------------------------------------------

    /// <summary>One argument more than the callee takes (a <c>0</c>, so its type is never the fact).</summary>
    public static Module? ArityPlusOne(ProgramFacts facts, Site site) =>
        site.Target is FunctionCall call
            ? AstRewriter.Replace(facts.Module, call, call with { Arguments = call.Arguments.Add(new IntegerLiteral { Value = "0" }) })
            : null;

    /// <summary>The last argument dropped (the others keep their parameters).</summary>
    public static Module? ArityMinusOne(ProgramFacts facts, Site site) =>
        site.Target is FunctionCall { Arguments.Length: > 0 } call
            ? AstRewriter.Replace(facts.Module, call, call with { Arguments = call.Arguments.RemoveAt(call.Arguments.Length - 1) })
            : null;

    // ---- member-name breaks -----------------------------------------------------------------------

    /// <summary>A member name no type defines.</summary>
    public static Module? UndefinedMember(ProgramFacts facts, Site site) =>
        site.Target is MemberAccess access
            ? AstRewriter.Replace(facts.Module, access, new MemberAccess { Object = access.Object, Member = access.Member + UndefinedSuffix })
            : null;

    /// <summary>An operator a user type implements, called by its dunder name: <c>a + b</c> → <c>a.__add__(b)</c>.</summary>
    public static Module? DunderByName(ProgramFacts facts, Site site) => site.Target switch
    {
        BinaryOp b when ProgramFacts.Dunder(b.Operator) is { } dunder => AstRewriter.Replace(facts.Module, b, new FunctionCall
        {
            Function = new MemberAccess { Object = b.Left, Member = dunder },
            Arguments = ImmutableArray.Create(b.Right)
        }),
        UnaryOp u when ProgramFacts.Dunder(u.Operator) is { } dunder => AstRewriter.Replace(facts.Module, u, new FunctionCall
        {
            Function = new MemberAccess { Object = u.Operand, Member = dunder },
            Arguments = ImmutableArray<Expression>.Empty
        }),
        _ => null,
    };

    // ---- annotation breaks ------------------------------------------------------------------------

    /// <summary>An annotation naming a type nothing declares.</summary>
    public static Module? UndefinedTypeAtAnnotation(ProgramFacts facts, Site site) =>
        site.Target is TypeAnnotation ? AstRewriter.Replace(facts.Module, site.Target, new TypeAnnotation { Name = UndefinedType }) : null;

    /// <summary>One type argument more than the generic takes (a copy of its first).</summary>
    public static Module? TypeArgumentArityPlusOne(ProgramFacts facts, Site site) =>
        site.Target is TypeAnnotation { TypeArguments.Length: > 0 } annotation
            ? AstRewriter.Replace(facts.Module, annotation, annotation with { TypeArguments = annotation.TypeArguments.Add(annotation.TypeArguments[0]) })
            : null;

    /// <summary>A scalar declaration's annotation swapped int ↔ str, the initializer kept.</summary>
    public static Module? ChangedScalarAnnotation(ProgramFacts facts, Site site) =>
        site.SlotType is "int" or "str"
            ? AstRewriter.Replace(facts.Module, site.Target, new TypeAnnotation { Name = site.SlotType == "int" ? "str" : "int" })
            : null;

    // ---- return-value breaks ----------------------------------------------------------------------

    public static Module? WrongTypedReturnValue(ProgramFacts facts, Site site) =>
        site.Target is ReturnStatement { Value: not null } r && RefusedLiteral(site.SlotType, facts) is { } literal
            ? AstRewriter.Replace(facts.Module, r, r with { Value = literal })
            : null;

    public static Module? DroppedReturnValue(ProgramFacts facts, Site site) =>
        site.Target is ReturnStatement { Value: not null } r && site.SlotType is not null and not "None"
            ? AstRewriter.Replace(facts.Module, r, r with { Value = null })
            : null;

    /// <summary>A def with an implicit <c>None</c> return gains <c>return 1</c>.</summary>
    public static Module? AddedReturnValue(ProgramFacts facts, Site site) =>
        site.Target is FunctionDef def
            ? AstRewriter.Replace(facts.Module, def, def with { Body = def.Body.Add(new ReturnStatement { Value = new IntegerLiteral { Value = "1" } }) })
            : null;

    public static Module? WrongTypedYield(ProgramFacts facts, Site site) =>
        site.Target is YieldStatement { IsFrom: false } y && RefusedLiteral(site.SlotType, facts) is { } literal
            ? AstRewriter.Replace(facts.Module, y, y with { Value = literal })
            : null;

    public static Module? YieldFromNonIterable(ProgramFacts facts, Site site) =>
        site.Target is YieldStatement { IsFrom: true } y
            ? AstRewriter.Replace(facts.Module, y, y with { Value = new IntegerLiteral { Value = "5" } })
            : null;

    // ---- pattern breaks ---------------------------------------------------------------------------

    public static Module? LiteralPatternOfOtherType(ProgramFacts facts, Site site) =>
        site.Target is LiteralPattern && RefusedLiteral(site.SlotType, facts) is { } literal
            ? AstRewriter.Replace(facts.Module, site.Target, new LiteralPattern { Literal = literal })
            : null;

    /// <summary><c>case 1 | 2</c> → <c>case (1 as bound_xyz) | 2</c>: one alternative binds a name the other does not.</summary>
    public static Module? OrPatternBindingMismatch(ProgramFacts facts, Site site) =>
        site.Target is OrPattern { Alternatives.Length: >= 2 } alternatives
            ? AstRewriter.Replace(facts.Module, alternatives, alternatives with
            {
                Alternatives = alternatives.Alternatives.SetItem(0,
                    new AsPattern { Inner = alternatives.Alternatives[0], Name = new Identifier { Name = "bound_xyz" } })
            })
            : null;

    /// <summary><c>case [a, b] and whole: …</c> → <c>case [a, b] and a: &lt;the wildcard arm's body&gt;</c>: a
    /// capture bound on both sides. The arm borrows the trailing wildcard arm's body, which reads no
    /// capture — keeping the original body read the rebound name at another type (SPY0372 + SPY0222,
    /// measured @ 98180fcd9 with <c>(whole as a)</c>).</summary>
    public static Module? AndPatternDuplicateCapture(ProgramFacts facts, Site site)
    {
        if (site.Target is not AndPattern conjunction || site.Parent is not MatchStatement match)
            return null;
        var first = AstRewriter.Descendants(conjunction.Left).OfType<BindingPattern>().FirstOrDefault();
        var index = match.Cases.IndexOf(match.Cases.FirstOrDefault(c => ReferenceEquals(c.Pattern, conjunction))!);
        if (first is null || index < 0 || CaptureFreeFallback(match) is not { } fallback)
            return null;
        var arm = match.Cases[index] with
        {
            Pattern = conjunction with { Right = new BindingPattern { Name = new Identifier { Name = first.Name.Name } } },
            Body = fallback,
        };
        return AstRewriter.Replace(facts.Module, match, match with { Cases = match.Cases.SetItem(index, arm) });
    }

    /// <summary>The body of the match's trailing wildcard arm when it reads no name a pattern binds.</summary>
    private static System.Collections.Immutable.ImmutableArray<Statement>? CaptureFreeFallback(MatchStatement match) =>
        match.Cases.Length >= 2 && match.Cases[^1] is { Pattern: WildcardPattern, Guard: null } last && !MutationSites.ArmReadsCapture(last)
            ? last.Body
            : null;

    /// <summary>A tuple pattern one element longer than its subject, in an arm that reads no capture (a
    /// read of a capture the refused pattern never bound is a second fact — measured @ 98180fcd9).</summary>
    public static Module? TuplePatternExtraWildcard(ProgramFacts facts, Site site)
    {
        if (site.Target is not TuplePattern tuple || site.Parent is not MatchStatement match)
            return null;
        var arm = match.Cases.FirstOrDefault(c => AstRewriter.Descendants(c.Pattern).Any(n => ReferenceEquals(n, tuple)));
        if (arm is null || MutationSites.ArmReadsCapture(arm))
            return null;
        return AstRewriter.Replace(facts.Module, tuple, tuple with { Elements = tuple.Elements.Add(new WildcardPattern()) });
    }

    /// <summary>The trailing wildcard arm moved first: every later arm is unreachable.</summary>
    public static Module? IrrefutableArmFirst(ProgramFacts facts, Site site) =>
        site.Target is MatchStatement { Cases.Length: >= 2 } match && match.Cases[^1].Pattern is WildcardPattern
            ? AstRewriter.Replace(facts.Module, match, match with { Cases = match.Cases.RemoveAt(match.Cases.Length - 1).Insert(0, match.Cases[^1]) })
            : null;

    /// <summary>A top-level list pattern gains a leading literal its element type refuses; every capture
    /// stays bound.</summary>
    public static Module? ListPatternElementOfOtherType(ProgramFacts facts, Site site) =>
        site.Target is ListPattern list && site.SlotType is { } slot && slot.StartsWith("list[", StringComparison.Ordinal)
            && RefusedLiteral(slot[5..^1], facts) is { } literal
            ? AstRewriter.Replace(facts.Module, list, list with { Elements = list.Elements.Insert(0, new LiteralPattern { Literal = literal }) })
            : null;
}
