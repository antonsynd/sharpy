using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Semantic;
using Microsoft.CodeAnalysis.CSharp;

namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// Consolidated compiler invariant assertions.
/// </summary>
/// <remarks>
/// <para>
/// This class consolidates all phase boundary assertions that verify internal
/// consistency of compilation artifacts. Previously these were scattered across
/// <see cref="Compiler"/> (lines 496-638) and called individually. Consolidating
/// them improves maintainability and ensures all invariants are checked consistently.
/// </para>
/// <para>
/// All assertions are always-on (not DEBUG-only) for production safety. When an
/// invariant is violated, it emits a <see cref="DiagnosticCodes.Infrastructure.InvariantViolation"/>
/// warning (SPY0904), not an exception. This allows compilation to continue with
/// diagnostic visibility rather than crashing.
/// </para>
/// <para>
/// Use the <see cref="InvariantSet"/> flags to selectively enable/disable specific
/// invariant categories when needed (e.g., for performance-sensitive scenarios).
/// </para>
/// </remarks>
public static class CompilerInvariants
{
    /// <summary>
    /// Flags indicating which invariant checks to run.
    /// </summary>
    [Flags]
    public enum InvariantSet
    {
        /// <summary>No invariants.</summary>
        None = 0,

        /// <summary>
        /// Check that top-level statements have TextSpan populated.
        /// Run after parsing.
        /// </summary>
        Spans = 1,

        /// <summary>
        /// Check that all symbols have non-empty names.
        /// Run after name resolution.
        /// </summary>
        SymbolNames = 2,

        /// <summary>
        /// Check that no duplicate user-defined type names exist.
        /// Run after name resolution.
        /// </summary>
        TypeUniqueness = 4,

        /// <summary>
        /// Check that all inheritance has been resolved (no dangling unresolved names).
        /// Run after inheritance resolution.
        /// </summary>
        Inheritance = 8,

        /// <summary>
        /// Check for unknown expression types remaining after type checking.
        /// Run after type checking. Skips when user errors exist (error recovery
        /// naturally produces Unknown types). Emits SPY0907 warnings for Unknown
        /// types not marked as error recovery.
        /// </summary>
        UnknownTypes = 16,

        /// <summary>
        /// Check that generated C# parses without syntax errors.
        /// Run after code generation.
        /// </summary>
        GeneratedCSharp = 32,

        /// <summary>All invariants for post-parse phase.</summary>
        PostParse = Spans,

        /// <summary>All invariants for post-name-resolution phase.</summary>
        PostNameResolution = SymbolNames | TypeUniqueness,

        /// <summary>All invariants for post-inheritance-resolution phase.</summary>
        PostInheritance = Inheritance,

        /// <summary>All invariants for post-type-checking phase.</summary>
        PostTypeChecking = UnknownTypes,

        /// <summary>All invariants for post-code-generation phase.</summary>
        PostCodeGen = GeneratedCSharp,

        /// <summary>All invariants.</summary>
        All = Spans | SymbolNames | TypeUniqueness | Inheritance | UnknownTypes | GeneratedCSharp
    }

    /// <summary>
    /// Run all specified invariant assertions.
    /// </summary>
    /// <param name="diagnostics">Diagnostic bag to emit warnings/errors to.</param>
    /// <param name="invariants">Which invariants to check (defaults to All).</param>
    /// <param name="module">Module to check (required for Spans).</param>
    /// <param name="symbolTable">Symbol table to check (required for SymbolNames, TypeUniqueness, Inheritance).</param>
    /// <param name="semanticInfo">Semantic info to check (required for UnknownTypes).</param>
    /// <param name="generatedCSharp">Generated C# code to check (required for GeneratedCSharp).</param>
    public static void Assert(
        DiagnosticBag diagnostics,
        InvariantSet invariants = InvariantSet.All,
        Module? module = null,
        SymbolTable? symbolTable = null,
        SemanticInfo? semanticInfo = null,
        string? generatedCSharp = null)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        if (invariants == InvariantSet.None)
            return;

        if (invariants.HasFlag(InvariantSet.Spans) && module != null)
        {
            AssertStatementsHaveSpans(module, diagnostics);
        }

        if (invariants.HasFlag(InvariantSet.SymbolNames) && symbolTable != null)
        {
            AssertAllSymbolsHaveNames(symbolTable, diagnostics);
        }

        if (invariants.HasFlag(InvariantSet.TypeUniqueness) && symbolTable != null)
        {
            AssertNoDuplicateTypeNames(symbolTable, diagnostics);
        }

        if (invariants.HasFlag(InvariantSet.Inheritance) && symbolTable != null)
        {
            AssertNoUnresolvedInheritance(symbolTable, diagnostics);
        }

        if (invariants.HasFlag(InvariantSet.UnknownTypes) && semanticInfo != null)
        {
            WarnIfUnknownTypes(semanticInfo, diagnostics);
        }

        if (invariants.HasFlag(InvariantSet.GeneratedCSharp) && generatedCSharp != null)
        {
            AssertGeneratedCSharpParses(generatedCSharp, diagnostics);
        }
    }

    /// <summary>
    /// Run post-parse invariants.
    /// </summary>
    public static void AssertPostParse(Module module, DiagnosticBag diagnostics)
    {
        Assert(diagnostics, InvariantSet.PostParse, module: module);
    }

    /// <summary>
    /// Run post-name-resolution invariants.
    /// </summary>
    public static void AssertPostNameResolution(SymbolTable symbolTable, DiagnosticBag diagnostics)
    {
        Assert(diagnostics, InvariantSet.PostNameResolution, symbolTable: symbolTable);
    }

    /// <summary>
    /// Run post-inheritance-resolution invariants.
    /// </summary>
    public static void AssertPostInheritance(SymbolTable symbolTable, DiagnosticBag diagnostics)
    {
        Assert(diagnostics, InvariantSet.PostInheritance, symbolTable: symbolTable);
    }

    /// <summary>
    /// Run post-type-checking invariants.
    /// </summary>
    public static void AssertPostTypeChecking(SemanticInfo semanticInfo, DiagnosticBag diagnostics)
    {
        Assert(diagnostics, InvariantSet.PostTypeChecking, semanticInfo: semanticInfo);
    }

    /// <summary>
    /// Run post-code-generation invariants.
    /// </summary>
    public static void AssertPostCodeGen(string generatedCSharp, DiagnosticBag diagnostics)
    {
        Assert(diagnostics, InvariantSet.PostCodeGen, generatedCSharp: generatedCSharp);
    }

    // ----- Individual Invariant Assertions -----

    /// <summary>
    /// Verify top-level statements have TextSpan populated.
    /// Emits SPY0904 if any statement is missing its span.
    /// </summary>
    /// <remarks>
    /// Import statements are exempt because they're processed before codegen
    /// and may not have spans populated.
    /// </remarks>
    internal static void AssertStatementsHaveSpans(Module module, DiagnosticBag diagnostics)
    {
        foreach (var stmt in module.Body)
        {
            // Import statements may not have spans (they're processed before codegen)
            if (stmt is ImportStatement or FromImportStatement)
                continue;

            if (!stmt.Span.HasValue)
            {
                diagnostics.AddWarning(
                    $"Internal invariant violation: statement {stmt.GetType().Name} at line {stmt.LineStart} is missing TextSpan. This is a compiler bug — please report it.",
                    stmt.LineStart, stmt.ColumnStart, code: DiagnosticCodes.Infrastructure.InvariantViolation,
                    phase: CompilerPhase.Unknown);
            }
        }
    }

    /// <summary>
    /// Verify all symbols in the global scope have non-empty names.
    /// Emits SPY0904 for any symbol with a null/empty name.
    /// </summary>
    internal static void AssertAllSymbolsHaveNames(SymbolTable symbolTable, DiagnosticBag diagnostics)
    {
        foreach (var symbol in symbolTable.GlobalScope.GetAllSymbols())
        {
            if (string.IsNullOrEmpty(symbol.Name))
            {
                diagnostics.AddWarning(
                    $"Internal invariant violation: symbol with kind {symbol.Kind} has null/empty name. This is a compiler bug — please report it.",
                    code: DiagnosticCodes.Infrastructure.InvariantViolation,
                    phase: CompilerPhase.NameResolution);
            }
        }
    }

    /// <summary>
    /// Verify no duplicate type definitions exist in the symbol table.
    /// NameResolver should have emitted errors for duplicates, but this checks
    /// the resulting symbol table is clean.
    /// </summary>
    /// <remarks>
    /// CLR types (from ModuleRegistry) are skipped because multiple modules
    /// can legitimately re-export the same CLR type.
    /// </remarks>
    internal static void AssertNoDuplicateTypeNames(SymbolTable symbolTable, DiagnosticBag diagnostics)
    {
        var typeNames = new HashSet<string>();
        foreach (var symbol in symbolTable.GlobalScope.GetAllSymbols().OfType<TypeSymbol>())
        {
            // Skip CLR types - multiple modules can legitimately re-export the same CLR type
            if (symbol.ClrType != null)
                continue;

            if (!typeNames.Add(symbol.Name))
            {
                diagnostics.AddWarning(
                    $"Internal invariant violation: duplicate type definition '{symbol.Name}' in symbol table after name resolution. This is a compiler bug — please report it.",
                    code: DiagnosticCodes.Infrastructure.InvariantViolation,
                    phase: CompilerPhase.NameResolution);
            }
        }
    }

    /// <summary>
    /// Verify all UnresolvedBaseName/UnresolvedInterfaceNames have been resolved
    /// after inheritance resolution. A dangling unresolved name means the inheritance
    /// resolver failed to find or match a type.
    /// </summary>
    /// <remarks>
    /// CLR types (from ModuleRegistry) are skipped because they don't go through
    /// the Sharpy inheritance resolution pipeline.
    /// </remarks>
    internal static void AssertNoUnresolvedInheritance(SymbolTable symbolTable, DiagnosticBag diagnostics)
    {
        foreach (var symbol in symbolTable.GlobalScope.GetAllSymbols().OfType<TypeSymbol>())
        {
            // Skip CLR types - they don't go through our resolution pipeline
            if (symbol.ClrType != null)
                continue;

            // If UnresolvedBaseName is set but BaseType is still null, resolution failed
            if (symbol.UnresolvedBaseName != null && symbol.BaseType == null)
            {
                diagnostics.AddWarning(
                    $"Internal invariant violation: type '{symbol.Name}' has UnresolvedBaseName '{symbol.UnresolvedBaseName}' but BaseType is null after inheritance resolution. This is a compiler bug — please report it.",
                    code: DiagnosticCodes.Infrastructure.InvariantViolation,
                    phase: CompilerPhase.NameResolution);
            }

            // If UnresolvedInterfaceNames has entries but Interfaces count doesn't match
            if (symbol.UnresolvedInterfaceNames.Count > 0 && symbol.Interfaces.Count < symbol.UnresolvedInterfaceNames.Count)
            {
                diagnostics.AddWarning(
                    $"Internal invariant violation: type '{symbol.Name}' has {symbol.UnresolvedInterfaceNames.Count} unresolved interface names but only {symbol.Interfaces.Count} resolved interfaces after inheritance resolution. This is a compiler bug — please report it.",
                    code: DiagnosticCodes.Infrastructure.InvariantViolation,
                    phase: CompilerPhase.NameResolution);
            }
        }
    }

    /// <summary>
    /// Check for unexpected unknown expression types after type checking.
    /// Unknown types from error recovery (user errors with diagnostics already emitted) are expected.
    /// Unknown types that appear without a corresponding diagnostic indicate a compiler bug.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <see cref="DiagnosticBag.HasErrors"/> is true, all Unknown types are considered
    /// acceptable because error recovery naturally produces them.
    /// </para>
    /// <para>
    /// When there are no errors, <see cref="SemanticInfo.GetUnexpectedUnknownExpressions"/>
    /// is used to find Unknown types that were NOT marked as error recovery. These are
    /// reported as errors (SPY0907) because they indicate type inference gaps.
    /// </para>
    /// </remarks>
    internal static void WarnIfUnknownTypes(SemanticInfo semanticInfo, DiagnosticBag diagnostics)
    {
        // When there are user errors, Unknown types are expected from error recovery — skip the check
        if (diagnostics.HasErrors)
            return;

        var unexpectedUnknowns = semanticInfo.GetUnexpectedUnknownExpressions();
        if (unexpectedUnknowns.Count > 0)
        {
            foreach (var expr in unexpectedUnknowns)
            {
                var nodeName = expr switch
                {
                    Parser.Ast.Identifier id => id.Name,
                    Parser.Ast.MemberAccess ma => $"{ma.Member}",
                    Parser.Ast.FunctionCall fc when fc.Function is Parser.Ast.Identifier fid => $"{fid.Name}()",
                    _ => expr.GetType().Name
                };

                // Note: This is emitted as a Warning rather than Error (as the spec suggests)
                // because there are known false positives from GenericType member access
                // (e.g., list[T].append, Box[T].get) where the type checker returns Unknown
                // without an error — the codegen resolves these through CLR member discovery.
                // When those type checker gaps are fixed, upgrade this to AddError.
                diagnostics.AddWarning(
                    $"Internal: type inference produced UnknownType for '{nodeName}' without a corresponding error diagnostic. This is a compiler bug.",
                    expr.Span,
                    expr.LineStart,
                    expr.ColumnStart,
                    code: DiagnosticCodes.Infrastructure.UnexpectedUnknownType,
                    phase: CompilerPhase.TypeChecking);
            }
        }
    }

    /// <summary>
    /// Verify generated C# code parses without syntax errors.
    /// This catches codegen bugs that produce malformed C#.
    /// Always-on (not DEBUG-only) because invalid generated C# in Release builds
    /// would produce cryptic Roslyn compilation errors instead of a clear
    /// "internal compiler error" diagnostic.
    /// </summary>
    internal static void AssertGeneratedCSharpParses(string csharpCode, DiagnosticBag diagnostics)
    {
        var tree = CSharpSyntaxTree.ParseText(csharpCode);
        var parseDiagnostics = tree.GetDiagnostics()
            .Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .ToList();
        if (parseDiagnostics.Count > 0)
        {
            var details = string.Join("; ", parseDiagnostics.Take(3).Select(d => d.GetMessage()));
            diagnostics.AddError(
                $"Internal error: generated C# contains {parseDiagnostics.Count} syntax error(s): {details}. This is a compiler bug -- please report it.",
                code: DiagnosticCodes.CodeGen.InternalGeneratedCSharpParseError,
                phase: CompilerPhase.CodeGeneration);
        }
    }
}
