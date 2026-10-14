using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Text;
using Sharpy.Compiler.Utilities;

namespace Sharpy.Lsp;

/// <summary>
/// Decides which route owns a file's squiggles, and publishes through
/// <see cref="DiagnosticPublisher"/> (#2242). Two routes:
/// <list type="bullet">
/// <item><b>Project route</b> — <see cref="LanguageService.ProjectFilesAnalyzed"/>: after every
/// completed project analysis (index, open/change reanalysis, watched-file change, reload, feature
/// change, and a render-only republish) every project file gets its per-file result — the build's
/// diagnostics for that file, <c>∅</c> when it has none. It is the ONLY publisher of a project
/// file once the project has been analyzed (<see cref="LanguageService.IsProjectFile"/>).</item>
/// <item><b>Single-file route</b> — <see cref="PublishSingleFile"/>: an open document's own
/// analysis, for a file the project does not own (no project, a file outside the source set, or
/// any file before the first indexing completes — that file gets a single-file publication, then
/// the index's).</item>
/// </list>
/// </summary>
/// <remarks>
/// <para><b>Foreign partitions.</b> The single-file door analyzes an open document with its import
/// closure, so its result can hold an imported sibling's diagnostics, which the publisher
/// partitions onto the sibling's URI. Such a partition is published only for a file no one else
/// owns — neither a project file (the project route owns it) nor an open document (its own
/// analysis owns it). Each source document's partitions are remembered: one that disappears from
/// the document's next result, or goes with the document's close, is replaced by another open
/// document's partition for the same file, else cleared with <c>∅</c>.</para>
/// <para><b>Leaving the project.</b> A file the last project publication covered that the current
/// one does not (removed from the <c>.spyproj</c>, or the <c>.spyproj</c> itself deleted) is
/// cleared — or, when it is open, republished from its own single-file analysis.</para>
/// <para>Every decision and send happens under one lock, so a single-file publication that
/// checked ownership before the project took the file over is sent before the project's.</para>
/// </remarks>
internal sealed class DiagnosticRoutes : IDisposable
{
    // Pseudo source document for project-bag entries stamped with a file outside the source set
    // (an extracted module's own errors — measured absent @ 8be4576fd, published here if one ever
    // appears so it is never dropped).
    private const string ProjectSourceKey = "<project>";

    private readonly LanguageService _languageService;
    private readonly SharpyWorkspace _workspace;
    private readonly DiagnosticPublisher _publisher;
    private readonly ILogger<DiagnosticRoutes> _logger;
    private readonly object _gate = new();

    // Project route: key → URI of every file the last project publication covered.
    private Dictionary<string, string> _projectPublished = new(StringComparer.Ordinal);

    // Foreign partitions: source document key → (foreign file key → the publication for it).
    private readonly Dictionary<string, Dictionary<string, PublishDiagnosticsParams>> _foreignBySource =
        new(StringComparer.Ordinal);

    private int _disposed;

    public DiagnosticRoutes(
        LanguageService languageService,
        SharpyWorkspace workspace,
        DiagnosticPublisher publisher,
        ILogger<DiagnosticRoutes> logger)
    {
        _languageService = languageService;
        _workspace = workspace;
        _publisher = publisher;
        _logger = logger;
        _languageService.ProjectFilesAnalyzed += OnProjectFilesAnalyzedAsync;
    }

    /// <summary>
    /// The single-file route: publishes an open document's own analysis — unless the project owns
    /// the document, or it closed while the analysis ran.
    /// </summary>
    /// <param name="uri">The analyzed document.</param>
    /// <param name="result">Its single-file analysis (import closure included).</param>
    /// <param name="sourceText">The text the analysis ran on.</param>
    public void PublishSingleFile(string uri, SemanticResult result, SourceText? sourceText)
    {
        lock (_gate)
        {
            // The project's per-file result is this file's publication (D2 a of #2242); the
            // single-file analysis still runs for completion's last-good tree (#1360).
            if (_languageService.IsProjectFile(uri))
                return;

            if (!OpenDocumentKeys().Contains(Key(uri)))
                return;

            var publications = DiagnosticPublisher.BuildPublications(
                uri, result, sourceText, DiagnosticPublisher.ReadSourceTextFromDisk, _publisher.Configuration);

            _publisher.Send(publications[0]);
            ReplaceForeignPartitions(Key(uri), publications.Skip(1));
        }
    }

    /// <summary>
    /// <c>textDocument/didClose</c>, after the workspace dropped the document. A project file keeps
    /// the project's publication (the build still owns it); another file's squiggles go — replaced
    /// by an open document's partition for it, if one holds it.
    /// </summary>
    public void OnDocumentClosed(string uri)
    {
        lock (_gate)
        {
            var key = Key(uri);
            ReplaceForeignPartitions(key, Enumerable.Empty<PublishDiagnosticsParams>());

            if (_languageService.IsProjectFile(uri))
                return;

            _publisher.Send(PartitionHeldByAnotherSource(key, exceptSource: key) ?? Empty(uri));
        }
    }

    private async Task OnProjectFilesAnalyzedAsync(IReadOnlyList<string> uris)
    {
        var republishFromWorkspace = new System.Collections.Generic.List<string>();

        lock (_gate)
        {
            var current = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var uri in uris)
            {
                var key = Key(uri);
                current[key] = uri;

                // A document that is now a project file publishes no single-file result any more;
                // the partitions its last one showed would otherwise stand forever.
                ReplaceForeignPartitions(key, Enumerable.Empty<PublishDiagnosticsParams>());

                var result = _languageService.GetProjectPublication(uri);
                if (result == null)
                {
                    _publisher.Send(Empty(uri));
                    continue;
                }

                // The per-file projection holds only this file's diagnostics, so this is one
                // notification; real ranges against the buffer or the disk text.
                foreach (var publication in DiagnosticPublisher.BuildPublications(
                    uri, result, _languageService.GetSourceText(uri), _languageService.GetSourceText, _publisher.Configuration))
                {
                    _publisher.Send(publication);
                }
            }

            ReplaceForeignPartitions(ProjectSourceKey, OutsideSourceSetPublications(current));

            foreach (var (key, uri) in _projectPublished)
            {
                if (current.ContainsKey(key))
                    continue;

                if (OpenDocumentKeys().Contains(key))
                    republishFromWorkspace.Add(uri);
                else
                    _publisher.Send(PartitionHeldByAnotherSource(key, exceptSource: null) ?? Empty(uri));
            }

            _projectPublished = current;
        }

        // A file that left the project while open: its own analysis owns it again.
        foreach (var uri in republishFromWorkspace)
        {
            var openUri = _workspace.GetAllDocumentUris().FirstOrDefault(u => Key(u) == Key(uri)) ?? uri;
            var analysis = await _workspace.GetAnalysisAsync(openUri).ConfigureAwait(false);
            if (analysis != null)
                PublishSingleFile(openUri, analysis, _workspace.GetSourceText(openUri));
        }
    }

    /// <summary>
    /// Project-bag diagnostics stamped with a file that is not a project file — no per-file
    /// projection holds them — one publication per such file.
    /// </summary>
    private System.Collections.Generic.List<PublishDiagnosticsParams> OutsideSourceSetPublications(
        IReadOnlyDictionary<string, string> projectFiles)
    {
        var publications = new System.Collections.Generic.List<PublishDiagnosticsParams>();
        var bag = _languageService.ProjectAnalysis?.Diagnostics;
        if (bag == null)
            return publications;

        var byFile = new Dictionary<string, (string Path, System.Collections.Generic.List<CompilerDiagnostic> Diagnostics)>(
            StringComparer.Ordinal);
        foreach (var diagnostic in bag.GetAll())
        {
            var path = diagnostic.FilePath;
            if (string.IsNullOrEmpty(path) || DiagnosticPublisher.IsGeneratedFilePath(path))
                continue;

            var localPath = LanguageService.UriToFilePath(path);
            if (localPath == null)
                continue;

            var key = PathNormalizer.Normalize(localPath);
            if (projectFiles.ContainsKey(key))
                continue;

            if (!byFile.TryGetValue(key, out var group))
            {
                group = (localPath, new System.Collections.Generic.List<CompilerDiagnostic>());
                byFile[key] = group;
            }

            group.Diagnostics.Add(diagnostic);
        }

        foreach (var (path, diagnostics) in byFile.Values)
        {
            var uri = LanguageService.FilePathToUri(path);
            publications.Add(new PublishDiagnosticsParams
            {
                Uri = DocumentUri.From(uri),
                Diagnostics = new Container<Diagnostic>(DiagnosticPublisher.ConvertDiagnostics(
                    diagnostics, DiagnosticPublisher.ReadSourceTextFromDisk(uri), _publisher.Configuration)),
            });
        }

        return publications;
    }

    /// <summary>
    /// Makes <paramref name="partitions"/> the foreign partitions <paramref name="sourceKey"/>
    /// shows: each is sent if no one owns its file; a file the source showed before and does not
    /// now is handed to another source's partition for it, else cleared. Caller holds the lock.
    /// </summary>
    private void ReplaceForeignPartitions(string sourceKey, IEnumerable<PublishDiagnosticsParams> partitions)
    {
        var next = new Dictionary<string, PublishDiagnosticsParams>(StringComparer.Ordinal);
        foreach (var partition in partitions)
            next[Key(partition.Uri.ToString())] = partition;

        _foreignBySource.TryGetValue(sourceKey, out var previous);
        if (next.Count == 0)
            _foreignBySource.Remove(sourceKey);
        else
            _foreignBySource[sourceKey] = next;

        if (previous == null && next.Count == 0)
            return;

        var openKeys = OpenDocumentKeys();
        foreach (var (key, partition) in next)
        {
            if (IsUnowned(key, partition.Uri.ToString(), openKeys))
                _publisher.Send(partition);
        }

        if (previous == null)
            return;

        foreach (var (key, partition) in previous)
        {
            if (next.ContainsKey(key))
                continue;

            var uri = partition.Uri.ToString();
            if (IsUnowned(key, uri, openKeys))
                _publisher.Send(PartitionHeldByAnotherSource(key, exceptSource: null) ?? Empty(uri));
        }
    }

    private bool IsUnowned(string key, string uri, HashSet<string> openKeys)
        => !openKeys.Contains(key) && !_languageService.IsProjectFile(uri);

    private PublishDiagnosticsParams? PartitionHeldByAnotherSource(string key, string? exceptSource)
    {
        foreach (var (source, partitions) in _foreignBySource)
        {
            if (source == exceptSource)
                continue;

            if (partitions.TryGetValue(key, out var partition))
                return partition;
        }

        return null;
    }

    private HashSet<string> OpenDocumentKeys()
        => _workspace.GetAllDocumentUris().Select(Key).ToHashSet(StringComparer.Ordinal);

    private static string Key(string uri)
    {
        var path = LanguageService.UriToFilePath(uri);
        return path != null ? PathNormalizer.Normalize(path) : uri;
    }

    private static PublishDiagnosticsParams Empty(string uri) => new()
    {
        Uri = DocumentUri.From(uri),
        Diagnostics = new Container<Diagnostic>(),
    };

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _languageService.ProjectFilesAnalyzed -= OnProjectFilesAnalyzedAsync;
    }
}
