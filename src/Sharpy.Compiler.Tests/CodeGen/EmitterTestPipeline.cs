using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// The one front-end pipeline CodeGen unit tests run before handing a module to the emitter:
/// name resolution → type checking with CodeGenInfo computation → the materialization boundary
/// (<see cref="SemanticBinding.MaterializeCodeGenInfo"/> / <see cref="SemanticBinding.MaterializeVariableTypes"/>)
/// → a <see cref="CodeGenContext"/> that carries both the <see cref="SemanticBinding"/> and the
/// <see cref="SemanticInfo"/>. This is the same sequence <c>FileCompilationPipeline</c> runs.
/// <para>
/// Why one helper: the emitter reads only materialized facts (CLAUDE.md Rule 2) and throws on an
/// absent one — "No CodeGenInfo for local", "No StatementLowering recorded", "No power lowering
/// recorded". Five test classes carried their own copy of this pipeline that stopped one step
/// short of materialization (no <see cref="SemanticBinding"/>, no
/// <c>MaterializeCodeGenInfo</c>, or no <c>SemanticInfo</c> on the context) and passed only
/// while the emitter still re-derived those facts itself; they went red the moment the
/// fallbacks were deleted (plan-c6ae1b verification, 25 reds @ 3bc6bc2a7). A hand-rolled
/// pipeline that skips materialization is a class defect, guarded by
/// <see cref="EmitterTestPipelineConformanceTests"/>.
/// </para>
/// </summary>
internal static class EmitterTestPipeline
{
    /// <summary>Everything a test may want to inspect after analysis.</summary>
    internal sealed record Analysis(
        Module Module,
        RoslynEmitter Emitter,
        TypeChecker TypeChecker,
        SymbolTable SymbolTable,
        SemanticInfo SemanticInfo,
        SemanticBinding SemanticBinding);

    /// <summary>Lexes and parses Sharpy source into a module (no diagnostics filtering).</summary>
    internal static Module Parse(string source)
    {
        var lexer = new global::Sharpy.Compiler.Lexer.Lexer(source, NullLogger.Instance);
        var parser = new global::Sharpy.Compiler.Parser.Parser(lexer.TokenizeAll(), NullLogger.Instance);
        return parser.ParseModule();
    }

    /// <summary>
    /// Runs the full front end on an already-parsed module and returns an emitter whose context
    /// carries the materialized facts.
    /// </summary>
    internal static Analysis Analyze(Module module, bool isEntryPoint = false, string? sourceFilePath = null)
    {
        var builtins = new BuiltinRegistry();
        var symbolTable = new SymbolTable(builtins);
        var semanticInfo = new SemanticInfo();
        var semanticBinding = new SemanticBinding();
        var logger = NullLogger.Instance;

        var nameResolver = new NameResolver(symbolTable, logger, semanticBinding);
        nameResolver.ResolveDeclarations(module);
        nameResolver.ResolveInheritance();
        semanticBinding.MaterializeInheritance();

        var typeResolver = new TypeResolver(symbolTable, semanticInfo, logger);
        var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, logger)
        {
            SemanticBinding = semanticBinding
        };
        typeChecker.CheckModule(module, computeCodeGenInfo: true, isEntryPoint: isEntryPoint);

        // The materialization boundary — after this the emitter may read Symbol.CodeGenInfo.
        semanticBinding.MaterializeCodeGenInfo();
        semanticBinding.MaterializeVariableTypes();

        var context = new CodeGenContext(symbolTable, builtins)
        {
            SourceFilePath = sourceFilePath,
            IsEntryPoint = isEntryPoint,
            SemanticBinding = semanticBinding,
            SemanticInfo = semanticInfo
        };
        return new Analysis(module, new RoslynEmitter(context), typeChecker, symbolTable, semanticInfo, semanticBinding);
    }

    /// <summary>Parses, analyzes and emits the compilation unit for <paramref name="source"/>.</summary>
    /// <param name="requireNoErrors">
    /// When true, asserts the type checker reported no errors before emitting — for tests whose
    /// subject is the emitted shape of a well-typed program. Leave false for tests that emit in
    /// the presence of diagnostics on purpose.
    /// </param>
    internal static CompilationUnitSyntax EmitCompilationUnit(string source, bool isEntryPoint = false, bool requireNoErrors = false, string? sourceFilePath = null)
    {
        var analysis = Analyze(Parse(source), isEntryPoint, sourceFilePath);
        if (requireNoErrors)
            analysis.TypeChecker.Diagnostics.GetErrors().Should().BeEmpty("Sharpy source should have no type errors");
        return analysis.Emitter.GenerateCompilationUnit(analysis.Module);
    }

    /// <summary>Parses, analyzes and emits <paramref name="source"/> as normalized C# text.</summary>
    internal static string CompileToCSharp(string source, bool isEntryPoint = false, bool requireNoErrors = false, string? sourceFilePath = null)
        => EmitCompilationUnit(source, isEntryPoint, requireNoErrors, sourceFilePath).NormalizeWhitespace().ToFullString();
}
