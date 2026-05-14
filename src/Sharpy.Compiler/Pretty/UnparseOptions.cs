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
}
