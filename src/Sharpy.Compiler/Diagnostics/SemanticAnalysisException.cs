namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// Thrown when semantic analysis encounters too many errors and aborts early.
/// Used as a control-flow exception by compilation orchestrators (Compiler, ProjectCompiler).
/// </summary>
internal class SemanticAnalysisException : Exception
{
    public SemanticAnalysisException(string message) : base(message) { }
}
