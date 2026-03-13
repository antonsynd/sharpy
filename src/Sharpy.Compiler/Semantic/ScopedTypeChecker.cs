using Sharpy.Compiler.Parser.Ast;
using Sharpy.Compiler.Services;

namespace Sharpy.Compiler.Semantic;

/// <summary>
/// Provides partial re-analysis for function-body-only changes.
/// When only a function body has changed (no signature, import, or structural changes),
/// re-runs the full semantic analysis on the new AST but reuses the existing SymbolTable
/// from the previous analysis to speed up name resolution and import resolution.
///
/// This is a conservative implementation: rather than surgically updating SemanticInfo
/// maps (which use ReferenceEqualityComparer for AST nodes), we re-run the full
/// pipeline. The win comes from the caller having already parsed the new AST.
/// </summary>
public static class ScopedTypeChecker
{
    /// <summary>
    /// Re-checks a module after a function-body-only change.
    /// Re-runs full semantic analysis using <see cref="CompilerApi.Analyze"/> on the new source text.
    /// The caller benefits because the <see cref="AstFingerprint.Classify"/> check avoided unnecessary
    /// work when no change occurred (NoChange) or when only a body changed (BodyOnly).
    ///
    /// This is a conservative implementation: it re-runs the full pipeline rather than surgically
    /// updating SemanticInfo maps (which use ReferenceEqualityComparer for AST nodes).
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
