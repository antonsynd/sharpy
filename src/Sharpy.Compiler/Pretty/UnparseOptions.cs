namespace Sharpy.Compiler.Pretty;

public record UnparseOptions
{
    public string IndentString { get; init; } = "    ";
    public string LineEnding { get; init; } = "\n";
    public bool Canonical { get; init; } = true;
    public bool PreserveTrivia { get; init; }
}
