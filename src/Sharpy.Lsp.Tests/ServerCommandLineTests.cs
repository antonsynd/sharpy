using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Sharpy.Lsp.Tests;

/// <summary>
/// Precedence and failure handling for the server's log-level resolution (#1225). These run
/// against the resolution function rather than a spawned server, which is why it is a function.
/// </summary>
public class ServerCommandLineTests
{
    private static (ServerLogging Logging, string Warnings) Resolve(string[] args, string? environmentValue)
    {
        using var warnings = new StringWriter();
        var logging = ServerCommandLine.ResolveLogging(args, environmentValue, warnings);
        return (logging, warnings.ToString());
    }

    [Fact]
    public void Unconfigured_server_keeps_the_previous_default()
    {
        var (logging, warnings) = Resolve(Array.Empty<string>(), environmentValue: null);

        logging.Level.Should().Be(LogLevel.Information,
            "a server nobody configured must behave exactly as it did before #1225");
        logging.IsConfigured.Should().BeFalse(
            "with nothing configured the server attaches no log destination, so the per-keystroke "
            + "instrumentation stays free when unobserved (#1140)");
        warnings.Should().BeEmpty();
    }

    [Fact]
    public void Flag_sets_the_level()
    {
        var (logging, warnings) = Resolve(new[] { "--log-level", "Debug" }, environmentValue: null);

        logging.Level.Should().Be(LogLevel.Debug);
        logging.IsConfigured.Should().BeTrue();
        warnings.Should().BeEmpty();
    }

    [Fact]
    public void Flag_accepts_the_equals_form()
    {
        var (logging, _) = Resolve(new[] { "--log-level=Trace" }, environmentValue: null);

        logging.Level.Should().Be(LogLevel.Trace);
    }

    [Fact]
    public void Flag_is_case_insensitive()
    {
        var (logging, warnings) = Resolve(new[] { "--log-level", "debug" }, environmentValue: null);

        logging.Level.Should().Be(LogLevel.Debug);
        warnings.Should().BeEmpty();
    }

    [Fact]
    public void Environment_variable_sets_the_level_when_no_flag_is_passed()
    {
        var (logging, warnings) = Resolve(Array.Empty<string>(), "Warning");

        logging.Level.Should().Be(LogLevel.Warning,
            "the environment variable exists for editors whose LSP client cannot pass server args");
        logging.IsConfigured.Should().BeTrue();
        warnings.Should().BeEmpty();
    }

    [Fact]
    public void Flag_wins_over_the_environment_variable()
    {
        var (logging, warnings) = Resolve(new[] { "--log-level", "Debug" }, "Critical");

        logging.Level.Should().Be(LogLevel.Debug);
        warnings.Should().BeEmpty();
    }

    [Fact]
    public void Blank_environment_variable_is_treated_as_unset()
    {
        var (logging, warnings) = Resolve(Array.Empty<string>(), "   ");

        logging.Level.Should().Be(LogLevel.Information);
        logging.IsConfigured.Should().BeFalse();
        warnings.Should().BeEmpty("an empty variable is how a shell spells 'not set', not a mistake");
    }

    [Fact]
    public void Unusable_flag_value_falls_back_with_exactly_one_warning()
    {
        var (logging, warnings) = Resolve(new[] { "--log-level", "chatty" }, environmentValue: null);

        logging.Level.Should().Be(LogLevel.Information,
            "a broken editor configuration must not stop the server from starting");
        logging.IsConfigured.Should().BeTrue(
            "a typo is still someone asking for logs, so they get them — at the fallback level, "
            + "with the warning saying why");
        warnings.Should().Contain("chatty").And.Contain("Information");
        warnings.Trim().Split('\n').Should().HaveCount(1, "the warning is reported once, not per read");
    }

    [Fact]
    public void Unusable_environment_value_falls_back_with_exactly_one_warning()
    {
        var (logging, warnings) = Resolve(Array.Empty<string>(), "loud");

        logging.Level.Should().Be(LogLevel.Information);
        warnings.Should().Contain(ServerCommandLine.EnvironmentVariable).And.Contain("loud");
        warnings.Trim().Split('\n').Should().HaveCount(1);
    }

    [Fact]
    public void Flag_without_a_value_is_reported_rather_than_falling_through_to_the_environment()
    {
        var (logging, warnings) = Resolve(new[] { "--log-level" }, "Debug");

        logging.Level.Should().Be(LogLevel.Information);
        warnings.Should().NotBeEmpty(
            "the caller asked explicitly and got it wrong; silently using the environment would hide that");
    }

    [Fact]
    public void Numeric_levels_are_rejected()
    {
        var (logging, warnings) = Resolve(new[] { "--log-level", "3" }, environmentValue: null);

        logging.Level.Should().Be(LogLevel.Information,
            "'3' is far more likely to be a mistake than a request for Warning");
        warnings.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Help_is_requested_by_the_usual_spellings(string arg)
    {
        ServerCommandLine.IsHelpRequested(new[] { arg }).Should().BeTrue();
    }

    [Fact]
    public void Help_is_not_requested_by_ordinary_arguments()
    {
        ServerCommandLine.IsHelpRequested(new[] { "--log-level", "Debug" }).Should().BeFalse();
    }

    [Fact]
    public void Usage_text_documents_both_mechanisms_and_the_empty_breakdown()
    {
        var usage = ServerCommandLine.UsageText;

        usage.Should().Contain(ServerCommandLine.LogLevelFlag)
            .And.Contain(ServerCommandLine.EnvironmentVariable);
        usage.Should().Contain("fast path",
            "a reader who does not know that an absent stage line means 'served without a compiler "
            + "call' will read the empty breakdown as missing measurement (#1140)");
    }
}
