using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Xunit;

namespace Sharpy.Cli.Tests;

/// <summary>
/// #1494: a diagnostic that carries neither a file nor a position — the unmappable SPY0908 is the
/// shape that motivated this — used to render with NO file name at all, telling the user something
/// had gone wrong without telling them in which program.
/// </summary>
/// <remarks>
/// The fix names the entry file as last-resort CONTEXT and stops there. The cells below pin both
/// halves of that, because only asserting the first half would let a future change "improve" the
/// output into the #1437 defect: attaching the entry file's buffer would let the renderer derive a
/// line/column and draw a snippet from a file the diagnostic was never about. Losing context is
/// acceptable; asserting a location that does not exist is not.
/// </remarks>
public class PositionlessDiagnosticRenderingTests
{
    private const string EntryPath = "/projects/demo/main.spy";

    private static CompilerDiagnostic PositionlessSpy0908() => new(
        Message: "internal error: generated C# failed to compile (CS0103)",
        Severity: CompilerDiagnosticSeverity.Error,
        Code: DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
        Phase: CompilerPhase.CodeGeneration);

    [Fact]
    public void PositionlessDiagnostic_WithFallback_NamesTheEntryFile()
    {
        var output = Render(PositionlessSpy0908(), EntryPath);

        output.Should().Contain(EntryPath,
            "a diagnostic with no file of its own must still name the program that produced it (#1494)");
        output.Should().Contain(DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError);
    }

    [Fact]
    public void PositionlessDiagnostic_WithFallback_AssertsNoLineColumnOrSnippet()
    {
        var output = Render(PositionlessSpy0908(), EntryPath);

        // The renderer's location arrow is `--> path` when there is no position and
        // `--> path:line:column` when there is. The fallback must produce the former.
        output.Should().NotContain($"{EntryPath}:",
            "naming the file must not manufacture a line and column it does not have");
        output.Should().NotContain("|",
            "the snippet gutter must be absent — there is no line to underline");
    }

    [Fact]
    public void PositionlessDiagnostic_WithoutFallback_StillRendersTheMessage()
    {
        // The pre-fix behavior, retained for callers that have no entry file to offer: the header
        // is all there is. This is the falsifiable arm — it fails if the fallback ever fires
        // unconditionally instead of only when a path is supplied.
        var output = Render(PositionlessSpy0908(), fallbackFilePath: null);

        output.Should().Contain("internal error");
        output.Should().NotContain("-->",
            "with nothing to name, the renderer must not invent a location arrow");
    }

    [Fact]
    public void DiagnosticWithItsOwnFile_IgnoresTheFallback()
    {
        var owned = new CompilerDiagnostic(
            Message: "Cannot assign type 'str' to variable of type 'int'",
            Severity: CompilerDiagnosticSeverity.Error,
            Line: 5,
            Column: 5,
            FilePath: "/projects/demo/lib.spy",
            Code: "SPY0220",
            Phase: CompilerPhase.TypeChecking);

        var output = Render(owned, EntryPath);

        output.Should().Contain("/projects/demo/lib.spy:5:5",
            "a diagnostic that knows its file keeps it; the fallback is last-resort only");
        output.Should().NotContain(EntryPath);
    }

    [Fact]
    public void PositionedButFilelessDiagnostic_KeepsItsPosition_AndIsNotPinnedToTheEntryFile()
    {
        // A different defect, deliberately NOT swallowed by this fallback: the diagnostic knows a
        // line but not a file, so its line belongs to some file we cannot name. Attaching it to the
        // entry file would assert a location that may not hold there — the #1437 mistake in
        // miniature. It keeps rendering against the "<source>" placeholder, which is at least
        // honest about not knowing.
        var positioned = new CompilerDiagnostic(
            Message: "some phase knew a line but not a file",
            Severity: CompilerDiagnosticSeverity.Error,
            Line: 12,
            Column: 3,
            Code: "SPY0200",
            Phase: CompilerPhase.TypeChecking);

        var output = Render(positioned, EntryPath);

        output.Should().NotContain(EntryPath,
            "pinning an unattributed position to the entry file would assert something false");
        output.Should().Contain("12:3", "the position it does know is not thrown away");
    }

    // ── harness ──────────────────────────────────────────────────────────────

    private static string Render(CompilerDiagnostic diagnostic, string? fallbackFilePath)
    {
        using var writer = new StringWriter();
        CliHelpers.RenderDiagnosticsFromFiles(new[] { diagnostic }, writer, fallbackFilePath);
        return writer.ToString();
    }
}
