using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;

namespace Sharpy.Lsp.Refactoring;

/// <summary>
/// Context provided to code action providers containing all necessary information
/// for determining applicable code actions.
/// </summary>
/// <param name="DocumentUri">The URI of the document being analyzed.</param>
/// <param name="Range">The user's selection range in the document.</param>
/// <param name="Diagnostics">Diagnostics reported for the document at the requested range.</param>
/// <param name="Analysis">Semantic analysis result for the document, or null if unavailable.</param>
/// <param name="SourceText">The full source text of the document, or null if unavailable.</param>
public sealed record CodeActionProviderContext(
    DocumentUri DocumentUri,
    OmniSharp.Extensions.LanguageServer.Protocol.Models.Range Range,
    Container<Diagnostic> Diagnostics,
    SemanticResult? Analysis,
    string? SourceText);

/// <summary>
/// Interface for extensible code action providers.
/// Each provider is responsible for a specific category of code actions
/// (e.g., quick fixes, refactorings, source actions).
/// </summary>
public interface ICodeActionProvider
{
    /// <summary>
    /// Returns code actions applicable to the given context.
    /// </summary>
    /// <param name="context">The code action context containing document, selection, and analysis info.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of applicable code actions, or an empty list if none apply.</returns>
    Task<IReadOnlyList<CodeAction>> GetCodeActionsAsync(
        CodeActionProviderContext context,
        CancellationToken cancellationToken);
}
