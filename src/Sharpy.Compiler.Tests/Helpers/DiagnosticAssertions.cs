using Sharpy.Compiler.Diagnostics;
using Xunit;

namespace Sharpy.Compiler.Tests.Helpers;

/// <summary>
/// Extension methods on <see cref="DiagnosticBag"/> for test assertions.
/// </summary>
public static class DiagnosticAssertions
{
    /// <summary>
    /// Assert that the bag contains no errors. Shows error messages on failure.
    /// </summary>
    public static void ShouldHaveNoErrors(this DiagnosticBag bag)
    {
        var errors = bag.GetErrors();
        if (errors.Count > 0)
        {
            var messages = string.Join("\n", errors.Select(e => e.ToString()));
            Assert.Fail($"Expected no errors, but found {errors.Count}:\n{messages}");
        }
    }

    /// <summary>
    /// Assert that the bag contains no warnings. Shows warning messages on failure.
    /// </summary>
    public static void ShouldHaveNoWarnings(this DiagnosticBag bag)
    {
        var warnings = bag.GetWarnings();
        if (warnings.Count > 0)
        {
            var messages = string.Join("\n", warnings.Select(w => w.ToString()));
            Assert.Fail($"Expected no warnings, but found {warnings.Count}:\n{messages}");
        }
    }

    /// <summary>
    /// Assert that the bag contains at least one error whose message contains the given substring.
    /// </summary>
    public static void ShouldHaveError(this DiagnosticBag bag, string messageSubstring)
    {
        var errors = bag.GetErrors();
        if (!errors.Any(e => e.Message.Contains(messageSubstring, StringComparison.OrdinalIgnoreCase)))
        {
            var messages = errors.Count > 0
                ? string.Join("\n", errors.Select(e => e.ToString()))
                : "(none)";
            Assert.Fail($"Expected an error containing '{messageSubstring}', but errors were:\n{messages}");
        }
    }

    /// <summary>
    /// Assert that the bag contains at least one error with the specified diagnostic code.
    /// </summary>
    public static void ShouldHaveErrorWithCode(this DiagnosticBag bag, string code)
    {
        var errors = bag.GetErrors();
        if (!errors.Any(e => e.Code == code))
        {
            var messages = errors.Count > 0
                ? string.Join("\n", errors.Select(e => $"[{e.Code ?? "no code"}] {e.ToString()}"))
                : "(none)";
            Assert.Fail($"Expected an error with code '{code}', but errors were:\n{messages}");
        }
    }

    /// <summary>
    /// Assert that the bag contains at least one warning whose message contains the given substring.
    /// </summary>
    public static void ShouldHaveWarning(this DiagnosticBag bag, string messageSubstring)
    {
        var warnings = bag.GetWarnings();
        if (!warnings.Any(w => w.Message.Contains(messageSubstring, StringComparison.OrdinalIgnoreCase)))
        {
            var messages = warnings.Count > 0
                ? string.Join("\n", warnings.Select(w => w.ToString()))
                : "(none)";
            Assert.Fail($"Expected a warning containing '{messageSubstring}', but warnings were:\n{messages}");
        }
    }
}
