using FluentAssertions;
using Microsoft.CodeAnalysis;
using Sharpy.Compiler;
using Sharpy.Compiler.Diagnostics;
using Xunit;

namespace Sharpy.Compiler.Tests.Diagnostics;

/// <summary>
/// #1482: <c>GetMetadataReferences</c> degraded silently. If every trusted-platform-assembly entry
/// failed its guards, compilation proceeded with a near-empty reference set and the user got a wall
/// of <c>CS0518: Predefined type 'System.String' is not defined or imported</c> — which the SPY0908
/// net relabelled "This is a Sharpy compiler bug — please report it".
/// </summary>
/// <remarks>
/// <para>
/// Measured cost of one occurrence: 181 test failures and roughly two hours establishing that the
/// compiler was fine. The trigger was transient and never reproduced (identical re-run: 0 failures;
/// handle exhaustion and incomplete build output both eliminated on-thread), which is exactly why
/// the MISREPORTING — not the trigger — is the defect. The post-condition makes the next occurrence
/// name itself on first contact.
/// </para>
/// <para>
/// <see cref="ReferenceAcquisitionDiagnostic_DoesNotBlameTheCompiler"/> is the cell this issue
/// exists for. The others could all pass while the message still sent the user to the issue tracker.
/// </para>
/// </remarks>
public class ReferenceSetPostConditionTests
{
    /// <summary>A reference set that really does define System.Object — the host's own corlib.</summary>
    private static IReadOnlyList<MetadataReference> HealthyReferences() =>
        new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };

    [Fact]
    public void HealthySet_IsAccepted_Silently()
    {
        var diagnostic = AssemblyCompiler.ValidateReferenceSet(
            HealthyReferences(),
            new AssemblyCompiler.TpaCensus(ListWasPresent: true, Seen: 200, Skipped: 0, SkipReasons: Array.Empty<string>()));

        diagnostic.Should().BeNull("a set that defines System.Object is usable and must not be flagged");
    }

    [Fact]
    public void EmptySet_WhenTheTpaListWasAbsent_SaysSo()
    {
        var diagnostic = AssemblyCompiler.ValidateReferenceSet(
            Array.Empty<MetadataReference>(),
            AssemblyCompiler.TpaCensus.None);

        diagnostic.Should().NotBeNull();
        diagnostic!.Code.Should().Be(DiagnosticCodes.Infrastructure.ReferenceAcquisitionFailed);
        diagnostic.Message.Should().Contain("TRUSTED_PLATFORM_ASSEMBLIES was empty",
            "an absent list is a different fault from a list whose entries all failed — only the "
            + "first ever reached the manual fallback (#1482)");
    }

    [Fact]
    public void NonEmptyTpa_WhereNothingSurvived_ReportsTheCountsAndTheFirstReasons()
    {
        var diagnostic = AssemblyCompiler.ValidateReferenceSet(
            Array.Empty<MetadataReference>(),
            new AssemblyCompiler.TpaCensus(
                ListWasPresent: true,
                Seen: 187,
                Skipped: 187,
                SkipReasons: new[] { "System.Private.CoreLib.dll: not found or not readable" }));

        diagnostic.Should().NotBeNull();
        diagnostic!.Message.Should().Contain("187");
        diagnostic.Message.Should().Contain("System.Private.CoreLib.dll",
            "the first skip reasons are what turn a two-hour investigation into a one-line answer");
        diagnostic.Message.Should().NotContain("TRUSTED_PLATFORM_ASSEMBLIES was empty",
            "the list was present — reporting it as empty would send the user down the wrong path");
    }

    [Fact]
    public void CorlibLessButNonEmptySet_IsRejected()
    {
        // The set is not empty — it holds a real assembly — but nothing in it defines System.Object.
        // A count-based check would pass here; the post-condition asks Roslyn the question CS0518
        // answers, so it does not.
        var sharpyCoreOnly = new[]
        {
            MetadataReference.CreateFromFile(typeof(CompilerApi).Assembly.Location)
        };

        var diagnostic = AssemblyCompiler.ValidateReferenceSet(
            sharpyCoreOnly,
            new AssemblyCompiler.TpaCensus(ListWasPresent: true, Seen: 187, Skipped: 186, SkipReasons: Array.Empty<string>()));

        diagnostic.Should().NotBeNull("a non-empty set without a corlib still cannot compile anything");
        diagnostic!.Message.Should().Contain("References acquired: 1",
            "the count is reported so 'nothing at all' and 'nearly nothing' are distinguishable");
    }

    [Fact]
    public void ReferenceAcquisitionDiagnostic_DoesNotBlameTheCompiler()
    {
        var diagnostic = AssemblyCompiler.ValidateReferenceSet(
            Array.Empty<MetadataReference>(),
            new AssemblyCompiler.TpaCensus(ListWasPresent: true, Seen: 187, Skipped: 187, SkipReasons: Array.Empty<string>()));

        diagnostic.Should().NotBeNull();
        diagnostic!.Message.Should().NotContain("Sharpy compiler bug",
            "misattributing an environment fault to a compiler bug is the defect #1482 exists to kill");
        diagnostic.Message.Should().NotContain("please report it");
        diagnostic.Message.Should().NotContain("github.com/antonsynd/sharpy/issues");
        diagnostic.Message.Should().Contain(".NET runtime installation",
            "the message must point at the thing that is actually broken");
    }

    [Fact]
    public void ReferenceAcquisitionCode_HasAnExplanation()
    {
        // Every active diagnostic needs a DiagnosticExplanations entry (`sharpyc explain SPY0910`).
        var explanation = DiagnosticExplanations.Get(
            DiagnosticCodes.Infrastructure.ReferenceAcquisitionFailed);

        explanation.Should().NotBeNull();
        explanation!.Description.Should().NotBeNullOrWhiteSpace();
        explanation.Fix.Should().NotBeNullOrWhiteSpace();
    }
}
