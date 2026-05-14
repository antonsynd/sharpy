namespace Sharpy.Compiler.Formatting;

public record FormatOptions
{
    public int IndentSize { get; init; } = 4;
    public bool UseTabs { get; init; }
    public string LineEnding { get; init; } = "\n";
    public int BlankLinesAroundTopLevelDefs { get; init; } = 2;
    public int BlankLinesBetweenClassMembers { get; init; } = 1;
    public bool TrailingNewline { get; init; } = true;

    public static FormatOptions Default { get; } = new();
}
