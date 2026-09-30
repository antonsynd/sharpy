using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// The AST-only emitter entry path: a test hand-builds a <see cref="Module"/> and emits it without
/// running semantic analysis. The emitter reads the module layout ONLY from the fact semantic analysis
/// records — it has no fallback (#2102, Rule 2) — so this path records that fact first, through the
/// same authority <c>CodeGenInfoComputer</c> records with (<see cref="ModuleLayout.ForModule"/>), then
/// emits. A test that calls <c>GenerateCompilationUnit</c> on an unanalyzed module instead gets the
/// emitter's internal error, which is the guarded behaviour, not a test bug to paper over.
/// </summary>
internal static class AstOnlyEmission
{
    /// <param name="importLayouts">
    /// The layout of each hand-built from-import's source module, stated by the test the way it states
    /// the import's other resolution facts (<c>ResolvedModulePath</c>, <c>ReExportedSymbols</c>).
    /// </param>
    internal static CompilationUnitSyntax GenerateRecordedCompilationUnit(
        this RoslynEmitter emitter, Module module, params (FromImportStatement Import, ModuleLayout Layout)[] importLayouts)
    {
        var context = emitter.Context;
        context.SemanticInfo ??= new SemanticInfo();
        if (context.SemanticInfo.GetModuleLayout(module) == null)
            context.SemanticInfo.SetModuleLayout(
                module, ModuleLayout.ForModule(module, context.ProjectRootPath, context.SourceFilePath));
        foreach (var (import, layout) in importLayouts)
            context.SemanticInfo.SetModuleLayout(import, layout);
        return emitter.GenerateCompilationUnit(module);
    }
}
