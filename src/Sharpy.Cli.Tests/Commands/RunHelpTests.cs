using System.CommandLine;
using System.CommandLine.Help;
using FluentAssertions;
using Sharpy.Cli.Commands;
using Xunit;

namespace Sharpy.Cli.Tests.Commands;

/// <summary>
/// <c>run --help</c> has to document the <c>--</c> program-argument separator, because the separator
/// deliberately sits outside the option grammar the help is generated from (#1231, #1215).
/// <para>
/// Assertions are deliberately substring- and structure-level rather than a byte snapshot of the
/// rendered page: System.CommandLine's column widths and wrapping shift across dependency bumps, and
/// a byte snapshot of generated help would fail on every one of them without a defect behind it. The
/// exact placement of the separator inside the usage block is pinned instead on
/// <see cref="RunHelpAction.Decorate"/> with synthetic input, where it is deterministic.
/// </para>
/// </summary>
public class RunHelpTests
{
    [Fact]
    public void RunHelp_UsageLine_ShowsTheSeparator()
    {
        var help = CliTestHarness.Invoke("run --help").StdOut;

        help.Should().Contain(RunHelpAction.UsageSuffix);

        // The suffix belongs to the usage block, not to the section appended after the options, so
        // it must appear before that section's heading.
        help.IndexOf(RunHelpAction.UsageSuffix, StringComparison.Ordinal)
            .Should().BeLessThan(help.IndexOf(RunHelpAction.SectionHeading, StringComparison.Ordinal));
    }

    [Fact]
    public void RunHelp_ShowsTheProgramArgumentsSection()
    {
        var help = CliTestHarness.Invoke("run --help").StdOut;

        help.Should().Contain(RunHelpAction.SectionHeading);
        help.Should().Contain("'--args' is deprecated");
        help.Should().Contain("-- --verbose input.txt");
    }

    /// <summary>
    /// <c>run</c>'s input argument is required, so <c>run --help</c> parses with an error and only
    /// renders help because the help action clears it. The decorating action has to keep forwarding
    /// that (and <c>Terminating</c>) to the action it wraps, or asking for help would start
    /// reporting a missing argument instead.
    /// </summary>
    [Fact]
    public void RunHelp_WithoutTheRequiredInput_StillRendersHelpCleanly()
    {
        var invocation = CliTestHarness.Invoke("run --help");

        invocation.ExitCode.Should().Be(0);
        invocation.StdErr.Should().BeEmpty();
        invocation.StdOut.Should().Contain(RunHelpAction.UsageSuffix);
    }

    /// <summary>
    /// The decoration is scoped to <c>run</c>. Every other command is forwarded to the very action
    /// this one wraps, so its help must come out byte identical to the undecorated render — compared
    /// here against a root whose help action has been reset to a stock <see cref="HelpAction"/>.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("build")]
    [InlineData("compile")]
    [InlineData("project")]
    [InlineData("emit")]
    [InlineData("cache")]
    [InlineData("explain")]
    [InlineData("lsp")]
    [InlineData("repl")]
    [InlineData("format")]
    [InlineData("server")]
    public void OtherCommands_HelpIsByteIdenticalToTheUndecoratedRender(string command)
    {
        var commandLine = command.Length == 0 ? "--help" : $"{command} --help";

        RenderHelp(DecoratedRoot(), commandLine)
            .Should().Be(RenderHelp(UndecoratedRoot(), commandLine));
    }

    [Fact]
    public void RunHelp_IsTheOnlyCommandHelpThatChanges()
    {
        var decorated = RenderHelp(DecoratedRoot(), "run --help");
        var undecorated = RenderHelp(UndecoratedRoot(), "run --help");

        undecorated.Should().NotContain(RunHelpAction.UsageSuffix);
        undecorated.Should().NotContain(RunHelpAction.SectionHeading);
        decorated.Should().NotBe(undecorated);
    }

    /// <summary>
    /// Installing twice must not stack decorations — the guard is what keeps a second
    /// <c>Configure</c> on the same root from doubling the section.
    /// </summary>
    [Fact]
    public void Install_IsIdempotent()
    {
        var (root, _, run) = CliTestHarness.BuildRoot();
        var once = RenderHelp(root, "run --help");

        RunHelpAction.Install(root, run);

        RenderHelp(root, "run --help").Should().Be(once);
    }

    // ---- Decoration semantics, pinned on synthetic input ----

    [Fact]
    public void Decorate_AppendsTheSuffixToTheUsageLine()
    {
        var help = Lines(
            "Description:",
            "  Compile and execute a Sharpy source file",
            "",
            "Usage:",
            "  sharpyc run <input> [options]",
            "",
            "Options:",
            "  -?, -h, --help  Show help and usage information",
            "",
            "");

        var decorated = RunHelpAction.Decorate(help, "sharpyc run", "\n");

        decorated.Should().Contain("  sharpyc run <input> [options] [-- <program-args>...]\n");
        decorated.Should().Contain(RunHelpAction.SectionHeading);
    }

    /// <summary>
    /// A narrow terminal wraps the usage text across several indented lines. The separator has to
    /// land on the last of them, not the first, or it reads as though it belonged mid-usage.
    /// </summary>
    [Fact]
    public void Decorate_WrappedUsage_SuffixesTheLastLineOfTheBlock()
    {
        var help = Lines(
            "Usage:",
            "  sharpyc run <input>",
            "  [options]",
            "",
            "Options:",
            "",
            "");

        var decorated = RunHelpAction.Decorate(help, "sharpyc run", "\n");

        decorated.Should().Contain("  sharpyc run <input>\n");
        decorated.Should().Contain("  [options] [-- <program-args>...]\n");
        decorated.Should().NotContain("  sharpyc run <input> [-- <program-args>...]");
    }

    /// <summary>
    /// The usage line is located by the command path, which sidesteps the localized "Usage:"
    /// heading. If it still cannot be found the section is appended anyway — a page missing the
    /// suffix is a smaller failure than a page missing the explanation entirely.
    /// </summary>
    [Fact]
    public void Decorate_WithoutAMatchingUsageLine_StillAppendsTheSection()
    {
        var help = Lines("Options:", "  -h  Show help", "", "");

        var decorated = RunHelpAction.Decorate(help, "sharpyc run", "\n");

        decorated.Should().NotContain(RunHelpAction.UsageSuffix);
        decorated.Should().Contain(RunHelpAction.SectionHeading);
    }

    [Fact]
    public void Decorate_PreservesTheWritersNewline()
    {
        var help = Lines("Usage:", "  sharpyc run <input> [options]", "", "").Replace("\n", "\r\n");

        var decorated = RunHelpAction.Decorate(help, "sharpyc run", "\r\n");

        decorated.Should().Contain("  sharpyc run <input> [options] [-- <program-args>...]\r\n");
        decorated.Split("\r\n").Should().AllSatisfy(line => line.Should().NotContain("\n").And.NotContain("\r"));
    }

    private static string Lines(params string[] lines) => string.Join("\n", lines);

    private static RootCommand DecoratedRoot() => CliTestHarness.BuildRoot().Root;

    /// <summary>A root whose help renders exactly as System.CommandLine generates it.</summary>
    private static RootCommand UndecoratedRoot()
    {
        var root = CliTestHarness.BuildRoot().Root;
        root.Options.OfType<HelpOption>().Single().Action = new HelpAction();
        return root;
    }

    private static string RenderHelp(RootCommand root, string commandLine)
    {
        var parseResult = root.Parse(commandLine);
        var (stdOut, _) = CliTestHarness.CaptureConsole(() =>
        {
#pragma warning disable CS0618 // Matches production Program.cs and CliTestHarness usage.
            parseResult.Invoke();
#pragma warning restore CS0618
        });
        return stdOut;
    }
}
