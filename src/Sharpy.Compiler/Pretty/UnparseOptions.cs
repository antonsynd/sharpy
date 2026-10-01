namespace Sharpy.Compiler.Pretty;

public record FormatOptions
{
    public int BlankLinesAroundTopLevelDefs { get; init; } = 2;
    public int BlankLinesBetweenClassMembers { get; init; } = 1;
    public bool TrailingNewline { get; init; } = true;

    public static FormatOptions Default { get; } = new();
}

public record UnparseOptions
{
    public string IndentString { get; init; } = "    ";
    public string LineEnding { get; init; } = "\n";
    public bool Canonical { get; init; } = true;
    public bool PreserveTrivia { get; init; }
    public FormatOptions? Formatting { get; init; }

    /// <summary>
    /// The source text the unparsed module was parsed from. With <see cref="SourceTokens"/> it
    /// drives the trivia cursor (P22b, #2077): every comment is written at its anchor, and a header
    /// with a comment inside its brackets is written as its source slice.
    /// </summary>
    public string? SourceText { get; init; }

    /// <summary>
    /// The trivia-preserving token stream <see cref="SourceText"/> lexed to (the stream the module
    /// was parsed from). The cursor reads the comments and blank lines from its trivia, and from the
    /// same stream the facts its anchors need: where code lines are (a body's end), where literals
    /// are (a verbatim header's re-indent never shifts a line inside a string) and where tokens
    /// really end. Null: comments come from the statements' own trivia when
    /// <see cref="PreserveTrivia"/> is set, else none are written.
    /// </summary>
    public IReadOnlyList<Lexer.Token>? SourceTokens { get; init; }
}
