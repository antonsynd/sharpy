using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// The AST-only emitter entry path: a test hand-builds a <see cref="Module"/> and emits it without
/// running semantic analysis. The emitter reads the module layout ONLY from the fact semantic analysis
/// records — it has no fallback (#2102, Rule 2) — so this path records that fact first, through the
/// same authority <c>CodeGenInfoComputer</c> records with (<see cref="ModuleLayout.ForModule"/>), and
/// the names each explicit from-import bound (Phase 4's record), then emits. A test that calls <c>GenerateCompilationUnit</c> on an unanalyzed module instead gets the
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
        // The names each explicit from-import BOUND (ProjectCompiler Phase 4's fact, #2385): with no analysis,
        // what the context's symbols say the name means — the emitter reads only the names.
        foreach (var fromImport in module.Body.OfType<FromImportStatement>().Where(f => !f.ImportAll))
        {
            if (context.SemanticInfo.GetFromImportBindings(fromImport) != null)
                continue;
            context.SemanticInfo.SetFromImportBoundNames(fromImport, fromImport.Names
                .Select(n => n.AsName ?? n.Name)
                .Select(name => context.LookupSymbol(name) is { } bound ? new FromImportBoundName(name, bound) : null)
                .OfType<FromImportBoundName>().ToList());
        }
        return emitter.GenerateCompilationUnit(module);
    }
}
