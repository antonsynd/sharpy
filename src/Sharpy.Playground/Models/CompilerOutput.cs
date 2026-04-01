namespace Sharpy.Playground.Models;

public sealed record CompilerOutput(
    string CSharp,
    string Ast,
    string Tokens,
    string Diagnostics,
    bool Success);
