using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Shared;

/// <summary>
/// The three spellings of a class-pattern head. A head names a type and tests the subject against
/// it; whichever spelling it was written in cannot change what it matches (owner ruling Q1, #1670).
/// </summary>
internal enum PatternHeadKind
{
    /// <summary><c>case C():</c> — a bare type test with no sub-patterns.</summary>
    Type,

    /// <summary><c>case C(a, b):</c> — positional deconstruction.</summary>
    Positional,

    /// <summary><c>case C(f=v):</c> — keyword (property) field patterns.</summary>
    Property,
}

/// <summary>
/// The ONE classifier for class-pattern heads (Design Decision 8, plan-6ca898 P13). The parser makes
/// <c>C()</c>, <c>C(v)</c> and <c>C(f=v)</c> three node kinds
/// (<see cref="TypePattern"/>, <see cref="PositionalPattern"/>, <see cref="PropertyPattern"/>); six
/// recognizers used to re-switch on the node kind with different arm sets, which is exactly how the
/// property form fell through exhaustiveness (SPY0463 on a covered case, #1890) and subsumption
/// (CS8120 behind SPY0908). Every consumer outside the parser, checker, emitter and pretty-printers
/// switches on <see cref="PatternHead"/> instead — this is the single switch on the three head kinds
/// (guarded by <c>DispatchSiteInventoryTests</c>).
/// </summary>
internal readonly record struct PatternHead
{
    /// <summary>Which of the three head spellings this is.</summary>
    public PatternHeadKind Kind { get; init; }

    /// <summary>
    /// The type the head names (<c>C</c> in every spelling). Never null for a parser-constructed head.
    /// </summary>
    public TypeAnnotation? Type { get; init; }

    /// <summary>
    /// The head's sub-patterns: positional elements for <see cref="PatternHeadKind.Positional"/>, the
    /// field VALUE patterns for <see cref="PatternHeadKind.Property"/> (the D4 fix — a property field
    /// pattern is a sub-pattern like a positional element), empty for <see cref="PatternHeadKind.Type"/>.
    /// </summary>
    public IReadOnlyList<Pattern> SubPatterns { get; init; }

    /// <summary>
    /// The PATTERN node the checker keyed its facts on (pattern type, coverage, union case). For a
    /// head wrapped in an <see cref="AsPattern"/> this is the inner head node, since the checker lodges
    /// on the inner node — a fact lodged on the <c>as</c> wrapper would not be found by the consumers.
    /// </summary>
    public Pattern Lodge { get; init; }

    /// <summary>
    /// Classifies <paramref name="pattern"/> as a class-pattern head, unwrapping a leading
    /// <see cref="AsPattern"/> (its capture does not change the head). Returns false for every pattern
    /// that is not a head (wildcard, binding, literal, member-access, tuple, list, relational, …).
    /// </summary>
    public static bool TryGet(Pattern pattern, out PatternHead head)
    {
        switch (pattern)
        {
            case AsPattern asPattern:
                return TryGet(asPattern.Inner, out head);

            case TypePattern typePattern:
                head = new PatternHead
                {
                    Kind = PatternHeadKind.Type,
                    Type = typePattern.Type,
                    SubPatterns = System.Array.Empty<Pattern>(),
                    Lodge = typePattern,
                };
                return true;

            case PositionalPattern positionalPattern:
                head = new PatternHead
                {
                    Kind = PatternHeadKind.Positional,
                    Type = positionalPattern.Type,
                    SubPatterns = positionalPattern.Elements,
                    Lodge = positionalPattern,
                };
                return true;

            case PropertyPattern propertyPattern:
                head = new PatternHead
                {
                    Kind = PatternHeadKind.Property,
                    Type = propertyPattern.Type,
                    SubPatterns = propertyPattern.Fields.Select(f => f.Pattern).ToArray(),
                    Lodge = propertyPattern,
                };
                return true;

            default:
                head = default;
                return false;
        }
    }
}
