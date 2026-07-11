namespace Sharpy.Compiler.Diagnostics;

/// <summary>
/// DiagnosticExplanations partial: Infrastructure diagnostic entries (SPY0900-SPY0999).
/// These cover compiler-level errors not tied to a specific language phase.
/// </summary>
public static partial class DiagnosticExplanations
{
    private static void AddInfrastructureEntries(Dictionary<string, DiagnosticExplanation> dict)
    {
        // ── Infrastructure errors (SPY0900-SPY0999) ────────────────────

        Add(dict, DiagnosticCodes.Infrastructure.CompilationFailed, "Compilation failed", "Infrastructure",
            "The overall compilation process failed. This is a summary error that accompanies more specific errors from earlier phases.",
            null,
            "Fix the errors reported in earlier phases (lexer, parser, semantic, or code generation).");

        Add(dict, DiagnosticCodes.Infrastructure.CompilationCancelled, "Compilation cancelled", "Infrastructure",
            "The compilation was cancelled, either by user request or by a timeout. No output was produced.",
            null,
            "Re-run the compilation. If it keeps timing out, check for very large files or circular dependencies.");

        Add(dict, DiagnosticCodes.Infrastructure.AssemblyCompilationFailed, "Assembly compilation failed", "Infrastructure",
            "The Roslyn C# compilation of the generated code failed. This means the compiler produced C# code that the .NET compiler could not compile.",
            null,
            "This is likely an internal compiler error. Report it at https://github.com/antonsynd/sharpy/issues with the source file.");

        Add(dict, DiagnosticCodes.Infrastructure.FileReadError, "File read error", "Infrastructure",
            "A source file could not be read from disk. This may be due to missing files, permission issues, or invalid file paths.",
            null,
            "Verify the file exists, the path is correct, and you have read permissions.");

        Add(dict, DiagnosticCodes.Infrastructure.InvariantViolation, "Internal invariant violation", "Infrastructure",
            "An internal compiler invariant was violated. This is a compiler bug — " +
            "the semantic pipeline produced data that fails a post-phase consistency check. " +
            "The compilation may still succeed, but the generated code could be incorrect.",
            null,
            "Report this error at https://github.com/antonsynd/sharpy/issues with the .spy file that triggered it.");

        Add(dict, DiagnosticCodes.Infrastructure.TooManyErrors, "Too many errors", "Infrastructure",
            "The compiler stopped reporting errors because the maximum error limit was reached. " +
            "Additional errors may exist but were suppressed. The reported errors should be fixed first, " +
            "as later errors are often caused by earlier ones.",
            null,
            "Fix the reported errors and re-compile. Use '--max-errors N' to increase the limit if needed.");

        Add(dict, DiagnosticCodes.Infrastructure.ParserLoopStall, "Parser loop stall detected", "Infrastructure",
            "The parser detected that it made no progress in a parsing loop. This is a safety mechanism " +
            "that prevents the parser from hanging on malformed input. The parser forcibly advanced past " +
            "the problematic token to continue parsing. This warning indicates the input may be malformed " +
            "or there is an edge case in the parser that should be reported.",
            null,
            "Check the source code at the indicated location for syntax errors. If the input looks correct, " +
            "report this at https://github.com/antonsynd/sharpy/issues with the source file.");

        Add(dict, DiagnosticCodes.Infrastructure.UnexpectedUnknownType, "Unexpected unknown type", "Infrastructure",
            "Type inference produced an UnknownType for an expression without a corresponding error diagnostic. " +
            "This indicates a gap in the type checker where a type could not be resolved but no user-facing error " +
            "was emitted. This is distinct from error-recovery Unknown types, which are expected when the user " +
            "writes invalid code.",
            null,
            "Report this at https://github.com/antonsynd/sharpy/issues with the .spy file that triggered it.");

        Add(dict, DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError, "Generated C# failed to compile", "Infrastructure",
            "The compiler produced C# code that Roslyn rejected with a raw CSxxxx error. This is always a Sharpy " +
            "compiler bug: a well-typed Sharpy program must lower to valid C#. The original CS id and message are " +
            "preserved inside the diagnostic text, and the location is mapped back to the offending .spy source line " +
            "via the emitted #line directives. This is the last-chance net that keeps a bare CSxxxx code from ever " +
            "reaching the user.",
            null,
            "Report this at https://github.com/antonsynd/sharpy/issues with the .spy file that triggered it, including " +
            "the embedded CS code and message.");

        Add(dict, DiagnosticCodes.Infrastructure.InternalCompilerError, "Internal compiler error", "Infrastructure",
            "An unexpected exception escaped a compilation phase and was caught by the compiler's last-chance handler. " +
            "This is always a Sharpy compiler bug — user code should surface as a normal diagnostic, never as an internal crash. " +
            "The handler writes a minimal-repro crash bundle (source excerpt, compiler version, failing phase and producer, " +
            "exception and stack trace, nearest AST span) to a '.sharpy-crash/<timestamp>/' directory, and the diagnostic " +
            "message points at that bundle.",
            null,
            "Report this at https://github.com/antonsynd/sharpy/issues and attach the crash bundle named in the diagnostic message.");
    }
}
