using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Parser.Ast;

namespace Sharpy.Compiler.CodeGen;

/// <summary>
/// Generates a C# compilation unit from a Sharpy module AST.
/// Implementations are stateful and not thread-safe — create a fresh instance per file.
/// </summary>
public interface ICodeEmitter
{
    CompilationUnitSyntax GenerateCompilationUnit(Module module);
}
