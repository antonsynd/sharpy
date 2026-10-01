namespace Sharpy.Compiler.Formatting;

/// <summary>
/// Layout options for <see cref="FormatterService"/>. There is deliberately no indentation option:
/// Sharpy indentation is exactly <see cref="Lexer.Lexer.IndentWidth"/> spaces per level and tabs are
/// not allowed (docs/language_specification/indentation.md), so any other indentation would make
/// the formatted file fail to lex (owner ruling 2026-09-30, P22b).
/// </summary>
public record FormatOptions
{
    public string LineEnding { get; init; } = "\n";
    public int BlankLinesAroundTopLevelDefs { get; init; } = 2;
    public int BlankLinesBetweenClassMembers { get; init; } = 1;
    public bool TrailingNewline { get; init; } = true;

    public static FormatOptions Default { get; } = new();
}
