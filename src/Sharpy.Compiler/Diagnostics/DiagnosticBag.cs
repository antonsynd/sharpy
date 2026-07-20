using Sharpy.Compiler.Text;

namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// Severity level for compiler diagnostics.
/// Named to avoid conflict with Microsoft.CodeAnalysis.DiagnosticSeverity.
/// </summary>
public enum CompilerDiagnosticSeverity
{
    Error,
    Warning,
    Info,
    Hint
}

/// <summary>
/// Phase of compilation where the diagnostic originated.
/// </summary>
public enum CompilerPhase
{
    Lexer,
    Parser,
    NameResolution,
    ImportResolution,
    TypeChecking,
    Validation,
    CodeGeneration,
    Assembly,
    Unknown
}

/// <summary>
/// A single diagnostic message with location and severity.
/// Named CompilerDiagnostic to avoid conflict with Microsoft.CodeAnalysis.Diagnostic.
/// </summary>
public record CompilerDiagnostic(
    string Message,
    CompilerDiagnosticSeverity Severity,
    int? Line = null,
    int? Column = null,
    string? FilePath = null,
    string? Code = null,
    CompilerPhase Phase = CompilerPhase.Unknown,
    TextSpan? Span = null,
    IReadOnlyDictionary<string, string>? Data = null
)
{
    public bool IsError => Severity == CompilerDiagnosticSeverity.Error;
    public bool IsWarning => Severity == CompilerDiagnosticSeverity.Warning;
    public bool IsHint => Severity == CompilerDiagnosticSeverity.Hint;

    public override string ToString()
    {
        var prefix = Severity switch
        {
            CompilerDiagnosticSeverity.Error => "error",
            CompilerDiagnosticSeverity.Warning => "warning",
            CompilerDiagnosticSeverity.Info => "info",
            CompilerDiagnosticSeverity.Hint => "hint",
            _ => "diagnostic"
        };

        var location = Line.HasValue && Column.HasValue
            ? $"({Line},{Column})"
            : Line.HasValue
                ? $"({Line})"
                : "";

        var file = !string.IsNullOrEmpty(FilePath) ? $"{FilePath}" : "";
        var code = !string.IsNullOrEmpty(Code) ? $" {Code}:" : ":";
        var span = Span.HasValue ? $" {Span.Value}" : "";

        return $"{file}{location}: {prefix}{code} {Message}{span}";
    }
}

/// <summary>
/// Thread-safe collection of diagnostics.
/// Supports future parallel compilation scenarios.
/// </summary>
public class DiagnosticBag
{
    private readonly List<CompilerDiagnostic> _diagnostics = new();
    private readonly object _lock = new();
    private readonly HashSet<string> _suppressedWarnings;
    private readonly bool _warningsAsErrors;
    private int _errorCount;
    private int _warningCount;
    private int _hintCount;

    /// <summary>
    /// Tracks diagnostics that have already been added, using (Code, Line, Column, Message?, SpanStart, SpanLength) as the key.
    /// This prevents duplicate diagnostics from being shown to the user when multiple validators
    /// catch the same issue, while still allowing distinct diagnostics that share code+line+column
    /// but differ in TextSpan (i.e., different AST nodes on the same line).
    /// </summary>
    private readonly HashSet<(string?, int?, int?, string?, int?, int?)> _seenDiagnostics = new();

    /// <summary>
    /// Tracks identifiers that are root causes of errors.
    /// When an identifier is marked as a root cause (e.g., from a failed import),
    /// subsequent errors about that identifier can be suppressed to avoid cascading noise.
    /// For example, if "from nonexistent import foo" fails, we mark "foo" as a root cause
    /// so that "undefined identifier 'foo'" errors are suppressed.
    /// </summary>
    private readonly HashSet<string> _rootCauseIdentifiers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The <see cref="CompilerDiagnostic.Data"/> key under which a diagnostic's producing
    /// component (the validator or subsystem that reported it) is recorded.
    /// </summary>
    public const string ProducerDataKey = "producer";

    /// <summary>
    /// The <see cref="CompilerDiagnostic.Data"/> key under which a warning's pre-promotion
    /// severity is recorded when <c>warningsAsErrors</c> turns it into an error. Scoped
    /// suppression (<c>@suppress</c>) reads this so a warning that was promoted to an error
    /// under <c>-Werror</c> remains suppressible — mirroring C# <c>#pragma warning disable</c>
    /// under <c>/warnaserror</c>. The value is the original severity's name (always "Warning").
    /// </summary>
    public const string OriginalSeverityDataKey = "originalSeverity";

    // Ambient provenance stamped onto every diagnostic added while a scope is active
    // (see BeginPhaseScope / BeginProducerScope). These are set and cleared on the same
    // thread that drives a compilation phase or validator, so they intentionally sit
    // outside the diagnostic lock: they describe *who is currently adding*, not stored state.
    private CompilerPhase? _activePhase;
    private string? _activeProducer;

    // Crash-context snapshot: the most recently *entered* phase/producer scope, retained
    // across scope disposal (unlike _activePhase/_activeProducer, which are restored on
    // Dispose as the stack unwinds). A last-chance ICE handler runs *outside* the phase
    // scope that threw, so it cannot read the ambient values — it reads these instead to
    // report which phase/producer the compiler had reached when it crashed.
    private CompilerPhase? _lastEnteredPhase;
    private string? _lastEnteredProducer;

    /// <summary>
    /// The most recently entered phase scope, retained across scope disposal for crash
    /// reporting. Approximates "which phase was the compiler in?" at the moment an
    /// exception escaped a phase. Null if no phase scope has been opened.
    /// </summary>
    public CompilerPhase? LastEnteredPhase => _lastEnteredPhase;

    /// <summary>
    /// The most recently entered producer (validator) scope, retained across scope disposal
    /// for crash reporting. Cleared when a bare phase scope is entered, so it is only
    /// non-null while the compiler was inside a validator. Null if none was active.
    /// </summary>
    public string? LastEnteredProducer => _lastEnteredProducer;

    public DiagnosticBag() : this(warningsAsErrors: false, suppressedWarnings: null) { }

    public DiagnosticBag(bool warningsAsErrors = false, HashSet<string>? suppressedWarnings = null)
    {
        _warningsAsErrors = warningsAsErrors;
        // Defensive copy: DiagnosticBag claims thread-safety via lock(_lock),
        // so the suppressed set must not be shared with callers.
        _suppressedWarnings = suppressedWarnings != null
            ? new HashSet<string>(suppressedWarnings, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Opens a phase-provenance scope. Every diagnostic added while the returned scope is
    /// alive whose <see cref="CompilerDiagnostic.Phase"/> is still <see cref="CompilerPhase.Unknown"/>
    /// is back-filled with <paramref name="phase"/>. Existing explicit phases are never overwritten.
    /// Dispose the scope (via <c>using</c>) to restore the previous ambient phase.
    /// </summary>
    public IDisposable BeginPhaseScope(CompilerPhase phase)
    {
        var previous = _activePhase;
        _activePhase = phase;
        // Advance the crash-context high-water mark. Entering a new (bare) phase clears the
        // last producer, so a crash in this phase isn't misattributed to a prior validator.
        _lastEnteredPhase = phase;
        _lastEnteredProducer = null;
        return new ScopeRestorer(this, restorePhase: true, previous, restoreProducer: false, null);
    }

    /// <summary>
    /// Opens a validator-provenance scope. Every diagnostic added while the returned scope is
    /// alive is stamped with <c>Data["producer"] = <paramref name="producerName"/></c> (unless it
    /// already carries a producer) and, if its phase is still <see cref="CompilerPhase.Unknown"/>,
    /// with <see cref="CompilerPhase.Validation"/>. Neither an existing producer nor an explicit
    /// phase is overwritten. Dispose the scope to restore the previous ambient state.
    /// </summary>
    public IDisposable BeginProducerScope(string producerName)
    {
        var previousPhase = _activePhase;
        var previousProducer = _activeProducer;
        _activeProducer = producerName;
        _activePhase = CompilerPhase.Validation;
        // Advance the crash-context high-water mark to this validator/phase.
        _lastEnteredPhase = CompilerPhase.Validation;
        _lastEnteredProducer = producerName;
        return new ScopeRestorer(this, restorePhase: true, previousPhase, restoreProducer: true, previousProducer);
    }

    /// <summary>
    /// Applies the ambient phase/producer provenance to a diagnostic, without clobbering any
    /// value the diagnostic already carries. Called at the single Add funnel so every code path
    /// (direct Add*, AddRange, Merge) is stamped uniformly with zero per-call-site edits.
    /// </summary>
    private CompilerDiagnostic ApplyProvenanceStamp(CompilerDiagnostic diagnostic)
    {
        var phase = _activePhase;
        var producer = _activeProducer;

        if (phase is CompilerPhase activePhase && diagnostic.Phase == CompilerPhase.Unknown)
        {
            diagnostic = diagnostic with { Phase = activePhase };
        }

        if (producer != null
            && (diagnostic.Data == null || !diagnostic.Data.ContainsKey(ProducerDataKey)))
        {
            var data = diagnostic.Data == null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(diagnostic.Data);
            data[ProducerDataKey] = producer;
            diagnostic = diagnostic with { Data = data };
        }

        return diagnostic;
    }

    /// <summary>
    /// Records <paramref name="original"/> under <see cref="OriginalSeverityDataKey"/> on a copy
    /// of <paramref name="diagnostic"/> (copy-on-write, mirroring <see cref="ApplyProvenanceStamp"/>).
    /// Used when a warning is promoted to an error so scoped suppression can recover its origin.
    /// </summary>
    private static CompilerDiagnostic StampOriginalSeverity(
        CompilerDiagnostic diagnostic, CompilerDiagnosticSeverity original)
    {
        var data = diagnostic.Data == null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(diagnostic.Data);
        data[OriginalSeverityDataKey] = original.ToString();
        return diagnostic with { Data = data };
    }

    private sealed class ScopeRestorer : IDisposable
    {
        private readonly DiagnosticBag _bag;
        private readonly bool _restorePhase;
        private readonly CompilerPhase? _previousPhase;
        private readonly bool _restoreProducer;
        private readonly string? _previousProducer;
        private bool _disposed;

        public ScopeRestorer(DiagnosticBag bag, bool restorePhase, CompilerPhase? previousPhase,
            bool restoreProducer, string? previousProducer)
        {
            _bag = bag;
            _restorePhase = restorePhase;
            _previousPhase = previousPhase;
            _restoreProducer = restoreProducer;
            _previousProducer = previousProducer;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (_restorePhase)
                _bag._activePhase = _previousPhase;
            if (_restoreProducer)
                _bag._activeProducer = _previousProducer;
        }
    }

    public void Add(CompilerDiagnostic diagnostic)
    {
        // Stamp phase/producer provenance from the active scope (if any) before anything else,
        // so the value ultimately stored — and used for dedup — reflects the producing context.
        diagnostic = ApplyProvenanceStamp(diagnostic);

        // Apply suppression: skip warnings/hints whose code is in the suppressed set.
        // Hints share the same suppression mechanism as warnings.
        if ((diagnostic.IsWarning || diagnostic.IsHint)
            && !string.IsNullOrEmpty(diagnostic.Code)
            && _suppressedWarnings.Contains(diagnostic.Code))
            return;

        // Apply promotion: warnings become errors when WarningsAsErrors is enabled.
        // Hints are NOT promoted — they are advisory diagnostics about behavioral
        // differences from Python/C# and remain hint-severity even under -Werror.
        // The pre-promotion severity is stamped into Data so scoped @suppress can still
        // recognize (and silence) a warning that -Werror turned into an error.
        if (diagnostic.IsWarning && _warningsAsErrors)
        {
            diagnostic = StampOriginalSeverity(diagnostic, CompilerDiagnosticSeverity.Warning)
                with { Severity = CompilerDiagnosticSeverity.Error };
        }

        // Deduplicate by code and location.
        // For diagnostics with codes, we use (Code, Line, Column) as the key.
        // For diagnostics without codes, we include the message to distinguish them.
        var key = GetDeduplicationKey(diagnostic);

        lock (_lock)
        {
            if (!_seenDiagnostics.Add(key))
                return; // Skip duplicate

            _diagnostics.Add(diagnostic);

            if (diagnostic.IsError)
                _errorCount++;
            else if (diagnostic.IsWarning)
                _warningCount++;
            else if (diagnostic.IsHint)
                _hintCount++;
        }
    }

    /// <summary>
    /// Gets a unique key for deduplication purposes.
    /// Diagnostics with codes are deduplicated by (Code, Line, Column, SpanStart, SpanLength).
    /// Diagnostics without codes use the message as part of the key.
    /// Including span information prevents false deduplication when two distinct AST nodes
    /// on the same line produce the same diagnostic code.
    /// </summary>
    private static (string?, int?, int?, string?, int?, int?) GetDeduplicationKey(CompilerDiagnostic diagnostic)
    {
        if (string.IsNullOrEmpty(diagnostic.Code))
        {
            // No code - use message as fallback for uniqueness
            return (null, diagnostic.Line, diagnostic.Column, diagnostic.Message,
                diagnostic.Span?.Start, diagnostic.Span?.Length);
        }
        return (diagnostic.Code, diagnostic.Line, diagnostic.Column, null,
            diagnostic.Span?.Start, diagnostic.Span?.Length);
    }

    public void AddError(string message, int? line = null, int? column = null, string? filePath = null,
        string? code = null, CompilerPhase phase = CompilerPhase.Unknown)
    {
        Add(new CompilerDiagnostic(message, CompilerDiagnosticSeverity.Error, line, column, filePath, code, phase));
    }

    public void AddError(string message, TextSpan? span, int? line = null, int? column = null,
        string? filePath = null, string? code = null, CompilerPhase phase = CompilerPhase.Unknown,
        IReadOnlyDictionary<string, string>? data = null)
    {
        Add(new CompilerDiagnostic(message, CompilerDiagnosticSeverity.Error, line, column, filePath, code, phase, span, data));
    }

    public void AddError(string message, ILocatable locatable, string? filePath = null,
        string? code = null, CompilerPhase phase = CompilerPhase.Unknown)
    {
        Add(new CompilerDiagnostic(message, CompilerDiagnosticSeverity.Error, Span: locatable.Span,
            FilePath: filePath, Code: code, Phase: phase));
    }

    public void AddWarning(string message, int? line = null, int? column = null, string? filePath = null,
        string? code = null, CompilerPhase phase = CompilerPhase.Unknown)
    {
        if (!string.IsNullOrEmpty(code) && _suppressedWarnings.Contains(code))
            return;
        // Emit at Warning severity; Add() centralizes the -Werror promotion and stamps the
        // original severity so scoped @suppress can still silence a promoted warning.
        var severity = CompilerDiagnosticSeverity.Warning;
        Add(new CompilerDiagnostic(message, severity, line, column, filePath, code, phase));
    }

    public void AddWarning(string message, TextSpan? span, int? line = null, int? column = null,
        string? filePath = null, string? code = null, CompilerPhase phase = CompilerPhase.Unknown,
        IReadOnlyDictionary<string, string>? data = null)
    {
        if (!string.IsNullOrEmpty(code) && _suppressedWarnings.Contains(code))
            return;
        // Emit at Warning severity; Add() centralizes the -Werror promotion and stamps the
        // original severity so scoped @suppress can still silence a promoted warning.
        var severity = CompilerDiagnosticSeverity.Warning;
        Add(new CompilerDiagnostic(message, severity, line, column, filePath, code, phase, span, data));
    }

    public void AddWarning(string message, ILocatable locatable, string? filePath = null,
        string? code = null, CompilerPhase phase = CompilerPhase.Unknown)
    {
        if (!string.IsNullOrEmpty(code) && _suppressedWarnings.Contains(code))
            return;
        // Emit at Warning severity; Add() centralizes the -Werror promotion and stamps the
        // original severity so scoped @suppress can still silence a promoted warning.
        var severity = CompilerDiagnosticSeverity.Warning;
        Add(new CompilerDiagnostic(message, severity, Span: locatable.Span,
            FilePath: filePath, Code: code, Phase: phase));
    }

    public void AddInfo(string message, int? line = null, int? column = null, string? filePath = null,
        string? code = null, CompilerPhase phase = CompilerPhase.Unknown)
    {
        Add(new CompilerDiagnostic(message, CompilerDiagnosticSeverity.Info, line, column, filePath, code, phase));
    }

    /// <summary>
    /// Adds a hint-severity diagnostic. Hints are advisory notes about behavioral
    /// differences from Python/C# (e.g., string indexing, struct value semantics).
    /// Hints share the same suppression mechanism as warnings (via the suppressed-warning
    /// set) but are NOT promoted to errors when WarningsAsErrors is enabled.
    /// </summary>
    public void AddHint(string message, int? line = null, int? column = null, string? filePath = null,
        string? code = null, CompilerPhase phase = CompilerPhase.Unknown)
    {
        if (!string.IsNullOrEmpty(code) && _suppressedWarnings.Contains(code))
            return;
        Add(new CompilerDiagnostic(message, CompilerDiagnosticSeverity.Hint, line, column, filePath, code, phase));
    }

    /// <summary>
    /// Adds a hint-severity diagnostic with an associated text span and optional data dictionary.
    /// </summary>
    public void AddHint(string message, TextSpan? span, int? line = null, int? column = null,
        string? filePath = null, string? code = null, CompilerPhase phase = CompilerPhase.Unknown,
        IReadOnlyDictionary<string, string>? data = null)
    {
        if (!string.IsNullOrEmpty(code) && _suppressedWarnings.Contains(code))
            return;
        Add(new CompilerDiagnostic(message, CompilerDiagnosticSeverity.Hint, line, column, filePath, code, phase, span, data));
    }

    /// <summary>
    /// Adds a hint-severity diagnostic anchored to an <see cref="ILocatable"/> AST node.
    /// </summary>
    public void AddHint(string message, ILocatable locatable, string? filePath = null,
        string? code = null, CompilerPhase phase = CompilerPhase.Unknown)
    {
        if (!string.IsNullOrEmpty(code) && _suppressedWarnings.Contains(code))
            return;
        Add(new CompilerDiagnostic(message, CompilerDiagnosticSeverity.Hint, Span: locatable.Span,
            FilePath: filePath, Code: code, Phase: phase));
    }

    public void AddRange(IEnumerable<CompilerDiagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            Add(diagnostic);
        }
    }

    /// <summary>
    /// Merge diagnostics from another bag (useful for aggregating from sub-validators).
    /// Also transfers root cause identifiers from the other bag.
    /// </summary>
    public void Merge(DiagnosticBag other)
    {
        AddRange(other.GetAll());
        // Transfer root cause identifiers
        foreach (var identifier in other.GetRootCauses())
        {
            MarkAsRootCause(identifier);
        }
    }

    public bool HasErrors
    {
        get
        {
            lock (_lock)
            {
                return _errorCount > 0;
            }
        }
    }

    public int ErrorCount
    {
        get
        {
            lock (_lock)
            {
                return _errorCount;
            }
        }
    }

    public int WarningCount
    {
        get
        {
            lock (_lock)
            {
                return _warningCount;
            }
        }
    }

    public int HintCount
    {
        get
        {
            lock (_lock)
            {
                return _hintCount;
            }
        }
    }

    public IReadOnlyList<CompilerDiagnostic> GetAll()
    {
        lock (_lock)
        {
            return _diagnostics.ToList();
        }
    }

    public IReadOnlyList<CompilerDiagnostic> GetErrors()
    {
        lock (_lock)
        {
            return _diagnostics.Where(d => d.IsError).ToList();
        }
    }

    public IReadOnlyList<CompilerDiagnostic> GetWarnings()
    {
        lock (_lock)
        {
            return _diagnostics.Where(d => d.Severity == CompilerDiagnosticSeverity.Warning).ToList();
        }
    }

    public IReadOnlyList<CompilerDiagnostic> GetHints()
    {
        lock (_lock)
        {
            return _diagnostics.Where(d => d.Severity == CompilerDiagnosticSeverity.Hint).ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _diagnostics.Clear();
            _seenDiagnostics.Clear();
            _rootCauseIdentifiers.Clear();
            _errorCount = 0;
            _warningCount = 0;
            _hintCount = 0;
        }
    }

    /// <summary>
    /// Removes every diagnostic for which <paramref name="predicate"/> returns true, keeping the
    /// severity counts and dedup key set consistent. Returns the number removed. Used by scoped
    /// <c>@suppress</c> to drop suppressed diagnostics after all phases have reported (the bag has
    /// no other mutation-after-add path, so this is the single filtering entry point).
    /// </summary>
    public int RemoveWhere(Func<CompilerDiagnostic, bool> predicate)
    {
        lock (_lock)
        {
            var removed = 0;
            for (var i = _diagnostics.Count - 1; i >= 0; i--)
            {
                var diagnostic = _diagnostics[i];
                if (!predicate(diagnostic))
                    continue;

                _diagnostics.RemoveAt(i);
                _seenDiagnostics.Remove(GetDeduplicationKey(diagnostic));
                if (diagnostic.IsError)
                    _errorCount--;
                else if (diagnostic.IsWarning)
                    _warningCount--;
                else if (diagnostic.IsHint)
                    _hintCount--;
                removed++;
            }

            return removed;
        }
    }

    /// <summary>
    /// Adds an error and marks the given identifier as a root cause.
    /// Subsequent errors related to this identifier can be suppressed using <see cref="IsRootCause"/>.
    /// Use this for errors like "module not found" where downstream "undefined identifier" errors
    /// are just noise caused by the original error.
    /// </summary>
    /// <param name="identifier">The identifier that failed to resolve (e.g., module name, imported symbol name)</param>
    /// <param name="message">The error message</param>
    /// <param name="line">Line number of the error</param>
    /// <param name="column">Column number of the error</param>
    /// <param name="filePath">File path where error occurred</param>
    /// <param name="code">Diagnostic code</param>
    /// <param name="phase">Compiler phase where error occurred</param>
    public void AddRootCauseError(string identifier, string message, int? line = null, int? column = null,
        string? filePath = null, string? code = null, CompilerPhase phase = CompilerPhase.Unknown)
    {
        lock (_lock)
        {
            _rootCauseIdentifiers.Add(identifier);
        }
        AddError(message, line, column, filePath, code, phase);
    }

    /// <summary>
    /// Adds an error with text span and marks the given identifier as a root cause.
    /// </summary>
    public void AddRootCauseError(string identifier, string message, TextSpan? span, int? line = null,
        int? column = null, string? filePath = null, string? code = null, CompilerPhase phase = CompilerPhase.Unknown)
    {
        lock (_lock)
        {
            _rootCauseIdentifiers.Add(identifier);
        }
        AddError(message, span, line, column, filePath, code, phase);
    }

    /// <summary>
    /// Checks whether the given identifier is a known root cause of errors.
    /// When true, callers may choose to suppress downstream errors about this identifier
    /// since the user has already been informed of the root cause.
    /// </summary>
    /// <param name="identifier">The identifier to check</param>
    /// <returns>True if this identifier was marked as a root cause via <see cref="AddRootCauseError"/></returns>
    public bool IsRootCause(string identifier)
    {
        lock (_lock)
        {
            return _rootCauseIdentifiers.Contains(identifier);
        }
    }

    /// <summary>
    /// Marks an identifier as a root cause without adding an error.
    /// Use this when the error has already been reported but you want to suppress cascading errors.
    /// </summary>
    /// <param name="identifier">The identifier to mark as a root cause</param>
    public void MarkAsRootCause(string identifier)
    {
        lock (_lock)
        {
            _rootCauseIdentifiers.Add(identifier);
        }
    }

    /// <summary>
    /// Marks multiple identifiers as root causes.
    /// </summary>
    /// <param name="identifiers">The identifiers to mark as root causes</param>
    public void MarkAsRootCauses(IEnumerable<string> identifiers)
    {
        lock (_lock)
        {
            foreach (var id in identifiers)
            {
                _rootCauseIdentifiers.Add(id);
            }
        }
    }

    /// <summary>
    /// Gets all root cause identifiers. Used when merging diagnostic bags.
    /// </summary>
    internal IReadOnlyCollection<string> GetRootCauses()
    {
        lock (_lock)
        {
            return _rootCauseIdentifiers.ToList();
        }
    }

}
