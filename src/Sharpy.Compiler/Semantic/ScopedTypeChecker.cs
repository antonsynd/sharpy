using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Services;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Provides partial re-analysis for function-body-only changes.
/// When only a function body has changed (no signature, import, or structural changes),
/// this helper re-runs the full semantic pipeline via <see cref="CompilerApi.Analyze"/>.
///
/// This is a conservative implementation: rather than surgically updating SemanticInfo
/// maps (which use ReferenceEqualityComparer for AST nodes), we re-run the full
/// pipeline. The win comes from the caller skipping analysis entirely for NoChange
/// results, and from having already parsed the new AST for BodyOnly results.
/// A future version could reuse the existing SymbolTable to skip name/import resolution.
/// </summary>
public static class ScopedTypeChecker
{
    /// <summary>
    /// Re-checks a module after a function-body-only change.
    /// Re-runs full semantic analysis using <see cref="CompilerApi.Analyze"/> on the new source text.
    /// Falls back to returning null if re-analysis fails, signaling the caller to use full analysis.
    /// </summary>
    /// <param name="api">The compiler API to use for re-analysis.</param>
    /// <param name="newText">The new source text to analyze.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated semantic result, or null if re-analysis should fall back to full pipeline.</returns>
    public static SemanticResult? RecheckFunction(
        CompilerApi api,
        string newText,
        CancellationToken ct = default)
    {
        try
        {
            var result = api.Analyze(newText, ct);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Any failure → fall back to full analysis
            return null;
        }
    }
}
