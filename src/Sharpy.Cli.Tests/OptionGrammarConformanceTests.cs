using System.CommandLine;
using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests;

/// <summary>
/// A whole-grammar guard, not a per-option one (#1215, D5).
///
/// <para>
/// <c>AllowMultipleArgumentsPerToken</c> lets a single option occurrence keep consuming the bare
/// tokens that follow it, so it eats whatever comes next — including the command's positional input
/// path. #1179 fixed exactly one option (<c>--enable-feature</c>) and the other thirteen sat
/// untouched for two rounds until #1215; the class only closes when a new greedy option cannot be
/// added without something failing. So this walks every command in the real root — the same root
/// <c>Program.Main</c> builds — rather than the four files those fixes happened to touch.
/// </para>
///
/// <para>
/// There is no allowlist. <c>--args</c> was the one option with a plausible claim to greediness,
/// and it lost it when program arguments moved to the <c>--</c> separator, so nothing needs an
/// exemption. If a future option genuinely does, the exemption belongs here with a reason and a
/// removal plan, not scattered at the registration site.
/// </para>
/// </summary>
public class OptionGrammarConformanceTests
{
    [Fact]
    public void NoOption_AllowsMultipleArgumentsPerToken()
    {
        var greedy = AllOptions()
            .Where(entry => entry.Option.AllowMultipleArgumentsPerToken)
            .Select(entry => $"{entry.CommandPath} {entry.Option.Name}")
            .ToArray();

        greedy.Should().BeEmpty(
            "an option that allows multiple arguments per token swallows the tokens after it, "
            + "including the positional input path (#1179, #1215). Spell repetition as a repeated "
            + "flag instead, and say \"(repeatable)\" in the description");
    }

    /// <summary>
    /// The guard above is a negative assertion, so it would also pass if the walk found nothing.
    /// This pins that the walk actually reaches both per-command options and the recursive global
    /// ones, on a subcommand nested two levels deep (<c>emit csharp</c>).
    /// </summary>
    [Fact]
    public void TheWalk_ReachesEveryLevelOfTheGrammar()
    {
        var all = AllOptions().ToArray();

        all.Should().HaveCountGreaterThan(20);
        all.Should().Contain(e => e.CommandPath == "sharpyc run" && e.Option.Name == "--module-path");
        all.Should().Contain(e => e.CommandPath == "sharpyc emit csharp" && e.Option.Name == "--reference");
        all.Should().Contain(e => e.Option.Name == "--enable-feature");
    }

    private static IEnumerable<(string CommandPath, Option Option)> AllOptions()
    {
        var (root, _, _) = CliTestHarness.BuildRoot();
        return Walk(root, "sharpyc");

        static IEnumerable<(string CommandPath, Option Option)> Walk(Command command, string path)
        {
            foreach (var option in command.Options)
            {
                yield return (path, option);
            }

            foreach (var subcommand in command.Subcommands)
            {
                foreach (var entry in Walk(subcommand, $"{path} {subcommand.Name}"))
                {
                    yield return entry;
                }
            }
        }
    }
}
