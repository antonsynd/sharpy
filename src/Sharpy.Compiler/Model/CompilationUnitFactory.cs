using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Logging;
using System.Collections.Immutable;

namespace Sharpy.Compiler.Model;

/// <summary>
/// Factory methods for creating and populating CompilationUnits.
/// </summary>
public static class CompilationUnitFactory
{
    /// <summary>
    /// Computes the module path from a file path and project root.
    /// </summary>
    /// <param name="filePath">Full path to the source file.</param>
    /// <param name="projectRoot">Path to the project root directory.</param>
    /// <returns>Dotted module path (e.g., "mypackage.mymodule").</returns>
    public static string ComputeModulePath(string filePath, string projectRoot)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(projectRoot);

        var relativePath = Path.GetRelativePath(projectRoot, filePath);
        var withoutExtension = Path.ChangeExtension(relativePath, null);

        // Replace directory separators with dots
        var modulePath = withoutExtension
            .Replace(Path.DirectorySeparatorChar, '.')
            .Replace(Path.AltDirectorySeparatorChar, '.');

        // Remove leading dots if present
        while (modulePath.StartsWith('.'))
        {
            modulePath = modulePath.Substring(1);
        }

        return modulePath;
    }

    /// <summary>
    /// Creates a CompilationUnit from a source file.
    /// </summary>
    /// <param name="filePath">Full path to the source file.</param>
    /// <param name="projectRoot">Path to the project root directory.</param>
    /// <returns>A new CompilationUnit with source text loaded.</returns>
    public static CompilationUnit CreateFromFile(string filePath, string projectRoot)
    {
        var sourceText = File.ReadAllText(filePath);
        var modulePath = ComputeModulePath(filePath, projectRoot);
        return new CompilationUnit(filePath, modulePath, sourceText);
    }

    /// <summary>
    /// Performs lexical analysis on a CompilationUnit.
    /// </summary>
    /// <param name="unit">The CompilationUnit to tokenize.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <returns>True if lexing succeeded, false if there were errors.</returns>
    public static bool Lex(CompilationUnit unit, ICompilerLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(unit);

        try
        {
            var lexer = new Lexer.Lexer(unit.SourceText, logger ?? NullLogger.Instance);
            var tokens = lexer.TokenizeAll();
            unit.Tokens = tokens;
            unit.Phase = CompilationPhase.Lexed;
            return true;
        }
        catch (LexerError ex)
        {
            unit.Diagnostics.AddError(ex.Message, ex.Line, ex.Column, unit.FilePath);
            unit.Phase = CompilationPhase.Failed;
            return false;
        }
    }

    /// <summary>
    /// Performs parsing on a CompilationUnit.
    /// Requires Lex() to have been called first.
    /// </summary>
    /// <param name="unit">The CompilationUnit to parse.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <returns>True if parsing succeeded, false if there were errors.</returns>
    public static bool Parse(CompilationUnit unit, ICompilerLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(unit);

        if (unit.Tokens == null)
        {
            throw new InvalidOperationException("Cannot parse without tokens. Call Lex() first.");
        }

        try
        {
            var parser = new Parser.Parser(unit.Tokens.ToList(), logger ?? NullLogger.Instance);
            var ast = parser.ParseModule();
            unit.Ast = ast;

            // Extract import statements from AST
            var imports = new List<ImportStatement>();
            var fromImports = new List<FromImportStatement>();

            foreach (var statement in ast.Body)
            {
                if (statement is ImportStatement import)
                {
                    imports.Add(import);
                }
                else if (statement is FromImportStatement fromImport)
                {
                    fromImports.Add(fromImport);
                }
            }

            unit.Imports = imports;
            unit.FromImports = fromImports;
            unit.Phase = CompilationPhase.Parsed;
            return true;
        }
        catch (ParserError ex)
        {
            unit.Diagnostics.AddError(ex.Message, ex.Line, ex.Column, unit.FilePath);
            unit.Phase = CompilationPhase.Failed;
            return false;
        }
    }

    /// <summary>
    /// Performs lexing and parsing in one call.
    /// </summary>
    /// <param name="unit">The CompilationUnit to process.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <returns>True if both lexing and parsing succeeded.</returns>
    public static bool LexAndParse(CompilationUnit unit, ICompilerLogger? logger = null)
    {
        return Lex(unit, logger) && Parse(unit, logger);
    }

    /// <summary>
    /// Sets the direct dependencies for a CompilationUnit.
    /// </summary>
    /// <param name="unit">The CompilationUnit to update.</param>
    /// <param name="dependencies">The file paths this unit depends on.</param>
    public static void SetDependencies(CompilationUnit unit, IEnumerable<string> dependencies)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(dependencies);

        unit.DirectDependencies = dependencies.ToImmutableHashSet();
    }
}
