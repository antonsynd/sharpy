namespace Sharpy.Compiler.Tests.Properties.Metamorphic.Transforms;

/// <summary>
/// Wraps every call's callee in redundant parentheses: <c>foo(1)</c> → <c>(foo)(1)</c>,
/// <c>obj.method(1)</c> → <c>(obj.method)(1)</c>. Parenthesizing an expression never changes what it
/// denotes, so every wrapped call must compile and behave identically.
///
/// <para>This is the transform that pins the #1147 defect class. The emitter dispatches
/// <c>GenerateCall</c> on the callee's AST shape; before #1147 a <c>Parenthesized</c> wrapper made
/// every proper arm miss, and the fall-through emitted <c>(Foo)(5)</c> — which C# re-parses as a
/// <b>cast</b>, so the accepted Sharpy program produced uncompilable C#. Its sibling
/// <see cref="ParensWrapTransform"/> wraps a print <em>argument</em>, which can never produce a
/// parenthesized callee and therefore never reached that arm.</para>
/// </summary>
internal sealed class ParensWrapCalleeTransform : IAstTransform
{
    public string Name => "ParensWrapCallee";

    /// <summary>
    /// Names that can be followed directly by <c>(</c> without denoting a first-class callable value.
    /// Parenthesizing these is not a semantics-preserving rewrite — there is no value to parenthesize:
    /// <c>super</c> is a keyword form (<c>h = super</c> is a parse error) and <c>Ok</c>/<c>Err</c>/
    /// <c>Some</c>/<c>None</c> are tagged-union constructor syntax (<c>k = Ok</c> is SPY0230,
    /// "'Ok' must be called as a function"). <c>isinstance</c> is deliberately NOT here: it does bind
    /// as a value, so its cells stay in the sweep.
    /// </summary>
    private static readonly HashSet<string> NonCalleeNames = new(StringComparer.Ordinal)
    {
        "if", "elif", "else", "while", "for", "in", "not", "and", "or", "is", "return", "yield",
        "await", "assert", "del", "raise", "lambda", "with", "as", "import", "from", "def", "class",
        "pass", "break", "continue", "global", "nonlocal", "match", "case", "try", "except",
        "finally", "async", "defer", "super", "Ok", "Err", "Some", "None",
    };

    /// <summary>
    /// Statement heads whose parentheses are declaration syntax or pattern syntax rather than a call:
    /// signatures (<c>def</c>, <c>property get raw(self)</c>, type declarations with a base list),
    /// decorators, and <c>case Ok(v):</c> destructuring patterns.
    /// </summary>
    private static readonly string[] NonCallStatementHeads =
    {
        "def ", "async def ", "class ", "property ", "case ", "interface ", "struct ", "enum ",
        "union ", "delegate ", "event ", "before_set(", "after_set(",
    };

    /// <summary>
    /// Block keywords whose header carries a real expression, so a call inside it is a genuine call
    /// site. Every OTHER block header ending in <c>:</c> is declaration or pattern syntax whose
    /// parentheses only look like a call — <c>before_set(new_value):</c>, <c>case Ok(v):</c>,
    /// a property observer, or a future declaration form nobody thought to enumerate here.
    /// </summary>
    private static readonly HashSet<string> ExpressionBlockKeywords = new(StringComparer.Ordinal)
    {
        "if", "elif", "while", "for", "with", "match", "async",
    };

    public string Apply(string source)
    {
        var masked = MaskedSource.Of(source);
        var lines = masked.Lines.ToArray();

        for (int i = 0; i < lines.Length; i++)
        {
            if (!masked.StartsStatement(i) || masked.DepthAfterLine(i) != 0)
                continue;

            var codeLine = masked.MaskedLines[i];
            var head = codeLine.TrimStart();
            if (head.StartsWith('@') || NonCallStatementHeads.Any(h => head.StartsWith(h, StringComparison.Ordinal)))
                continue;
            // A backtick-escaped identifier (`System.Console`.WriteLine) is one name containing dots;
            // the chain scan cannot see its boundaries, so leave such lines alone.
            if (codeLine.Contains('`', StringComparison.Ordinal))
                continue;
            if (codeLine.TrimEnd().EndsWith(':') && !ExpressionBlockKeywords.Contains(FirstWord(head)))
                continue;

            var insertions = new List<(int Index, string Text)>();
            for (int j = 0; j < codeLine.Length; j++)
            {
                if (codeLine[j] != '(')
                    continue;

                var chainStart = CalleeChainStart(codeLine, j);
                if (chainStart < 0)
                    continue;

                var chain = codeLine[chainStart..j];
                if (NonCalleeNames.Contains(chain))
                    continue;

                var close = ParenBalance.MatchingClose(codeLine, j);
                if (close < 0)
                    continue;

                if (IsZeroArgMemberCall(chain, codeLine, j, close))
                    continue;

                insertions.Add((chainStart, "("));
                insertions.Add((j, ")"));
            }

            if (insertions.Count == 0)
                continue;

            var line = lines[i];
            var builder = new System.Text.StringBuilder(line.Length + insertions.Count);
            var previous = 0;
            foreach (var (index, text) in insertions.OrderBy(x => x.Index))
            {
                builder.Append(line, previous, index - previous).Append(text);
                previous = index;
            }
            builder.Append(line, previous, line.Length - previous);
            lines[i] = builder.ToString();
        }

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Whether <c>recv.member()</c> takes no arguments — the one call shape where parenthesizing
    /// the callee is not semantics-preserving, so the transform must leave it alone.
    /// </summary>
    /// <remarks>
    /// A CLR-discovered member can be a property whose zero-argument call Sharpy collapses onto
    /// the property read: <c>Counter[str].keys</c> is a <c>list[str]</c> property, and
    /// <c>c.keys()</c> works because the TypeChecker leaves a <em>call callee</em> unresolved for
    /// the emitter to collapse. Parenthesizing takes the member out of callee position, so
    /// <c>(c.keys)()</c> means "call this list" — correctly SPY0201. The rewrite changed which
    /// entity the source denotes, which is the one thing a metamorphic transform may not do; the
    /// property and the method are spelled identically, so no text-level rule can tell them apart.
    /// Calls WITH arguments are unaffected (a property holding a delegate invokes the same either
    /// way), and bare-name calls have no receiver to read a property from — the #1147 shape this
    /// transform exists for stays covered.
    /// </remarks>
    private static bool IsZeroArgMemberCall(string chain, string maskedLine, int openIndex, int closeIndex)
    {
        if (!chain.Contains('.', StringComparison.Ordinal))
            return false;

        for (int k = openIndex + 1; k < closeIndex; k++)
        {
            if (!char.IsWhiteSpace(maskedLine[k]))
                return false;
        }

        return true;
    }

    private static string FirstWord(string head)
    {
        var end = 0;
        while (end < head.Length && ParenBalance.IsIdentifierChar(head[end]))
            end++;
        return head[..end];
    }

    /// <summary>
    /// Start index of the identifier chain (<c>name</c>, <c>a.b.c</c>, <c>self.method</c>) directly
    /// preceding the <c>(</c> at <paramref name="openIndex"/>, or -1 when the callee is not a plain
    /// chain — an already-parenthesized expression, a subscript (<c>f[int](…)</c>), a call result
    /// (<c>f()(…)</c>), or a grouping paren with no callee at all.
    /// </summary>
    private static int CalleeChainStart(string maskedLine, int openIndex)
    {
        var i = openIndex - 1;
        if (i < 0 || !ParenBalance.IsIdentifierChar(maskedLine[i]))
            return -1;

        while (i >= 0 && (ParenBalance.IsIdentifierChar(maskedLine[i]) || maskedLine[i] == '.'))
            i--;

        var start = i + 1;
        // A chain must begin with an identifier start character, and must not be the tail of a
        // subscript/call (`f[0].g(` and `f().g(` keep their receiver unwrapped — those callees are
        // not plain chains). A leading '.' means the receiver is a literal or another expression:
        // `" ".join(xs)` masks to `   .join(xs)`, and wrapping that yields the nonsense `(.join)(xs)`.
        if (start >= openIndex || char.IsDigit(maskedLine[start]) || maskedLine[start] == '.')
            return -1;
        if (i >= 0 && (maskedLine[i] == ')' || maskedLine[i] == ']'))
            return -1;
        return start;
    }
}
