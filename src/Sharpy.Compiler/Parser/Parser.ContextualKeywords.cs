using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.Parser;

/// <summary>
/// The contextual (soft) keywords: identifiers the parser reads as a keyword only at one site
/// (<c>get</c> right after <c>property</c>, <c>when</c> after an <c>except</c> clause's type, …).
/// A backtick-escaped spelling is an identifier at EVERY site (identifiers.md § Literal Names,
/// #2166), so a site tests a token with <see cref="Parser.IsContextualKeyword"/> and a call
/// argument with <see cref="Parser.IsPlaceholder"/> — never with the raw spelling.
/// </summary>
internal static class ContextualKeywords
{
    /// <summary>The partial-application placeholder in a call or operator section, and the wildcard in a pattern.</summary>
    public const string Placeholder = "_";

    /// <summary>Property accessor: <c>property get name(self) -&gt; T:</c>.</summary>
    public const string Get = "get";

    /// <summary>Property accessor: <c>property set name(self, value: T):</c>.</summary>
    public const string Set = "set";

    /// <summary>Property accessor: <c>property init name(self, value: T):</c>.</summary>
    public const string Init = "init";

    /// <summary>Event accessor: <c>event add name(self, handler: H):</c>.</summary>
    public const string Add = "add";

    /// <summary>Event accessor: <c>event remove name(self, handler: H):</c>.</summary>
    public const string Remove = "remove";

    /// <summary>Property observer clause: <c>before_set(value):</c>.</summary>
    public const string BeforeSet = "before_set";

    /// <summary>Property observer clause: <c>after_set(value):</c>.</summary>
    public const string AfterSet = "after_set";

    /// <summary>Exception filter: <c>except E as e when cond:</c>.</summary>
    public const string When = "when";

    /// <summary>Parameter and call-site modifier (<c>x: out T</c>, <c>f(out y)</c>) and covariance (<c>[out T]</c>).</summary>
    public const string Out = "out";

    /// <summary>Parameter and call-site modifier: <c>x: ref T</c>, <c>f(ref y)</c>.</summary>
    public const string Ref = "ref";

    /// <summary>Type-parameter constraint: <c>[T: notnull]</c>.</summary>
    public const string NotNull = "notnull";

    /// <summary>Type-parameter constraint: <c>[T: new()]</c>.</summary>
    public const string New = "new";

    /// <summary>Every contextual keyword above.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Placeholder, Get, Set, Init, Add, Remove, BeforeSet, AfterSet, When, Out, Ref, NotNull, New,
    };
}

/// <summary>
/// Parser partial class: the escape-aware contextual-keyword predicates (#2166)
/// </summary>
public partial class Parser
{
    /// <summary>
    /// Whether <paramref name="token"/> is the contextual keyword <paramref name="keyword"/>: an
    /// unescaped identifier spelled exactly so. A backtick-escaped spelling is an identifier.
    /// </summary>
    private static bool IsContextualKeyword(Token token, string keyword)
        => token.Type == TokenType.Identifier && !token.IsBacktickEscaped && token.Value == keyword;

    /// <summary>
    /// Whether <paramref name="expr"/> is the partial-application placeholder: an unescaped
    /// <c>_</c>. A backtick-escaped <c>`_`</c> is the variable <c>_</c>.
    /// </summary>
    private static bool IsPlaceholder(Expression? expr)
        => expr is Identifier { Name: ContextualKeywords.Placeholder, IsNameBacktickEscaped: false };
}
