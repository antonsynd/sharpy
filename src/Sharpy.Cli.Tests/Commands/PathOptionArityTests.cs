using FluentAssertions;
using Xunit;

namespace Sharpy.Cli.Tests.Commands;

/// <summary>
/// #1215: <c>--reference</c>, <c>--project-reference</c> and <c>--module-path</c> used to set
/// <c>AllowMultipleArgumentsPerToken</c>, so a single occurrence kept consuming following bare
/// tokens and swallowed the positional input path — <c>sharpyc run --module-path src file.spy</c>
/// bound <c>file.spy</c> to <c>--module-path</c> and then failed with "Required argument missing
/// for command: 'run'". Each occurrence now takes exactly one value, in every spelling and either
/// order, on every command that declares the option; more values are spelled as repeated flags.
/// This is the contract #1179 gave <c>--enable-feature</c>, applied to the residual class.
/// </summary>
public class PathOptionArityTests
{
    /// <summary>
    /// Every (command, option) pair in the CLI that declares one of the three path options.
    /// <c>emit csharp</c> is the only one without <c>--project-reference</c>.
    /// </summary>
    private static readonly (string Command, string Option, string Value)[] OptionSites =
    {
        ("build", "--reference", "a.dll"),
        ("build", "--project-reference", "lib.csproj"),
        ("build", "--module-path", "mods"),
        ("compile", "--reference", "a.dll"),
        ("compile", "--project-reference", "lib.csproj"),
        ("compile", "--module-path", "mods"),
        ("run", "--reference", "a.dll"),
        ("run", "--project-reference", "lib.csproj"),
        ("run", "--module-path", "mods"),
        ("emit csharp", "--reference", "a.dll"),
        ("emit csharp", "--module-path", "mods"),
    };

    /// <summary>
    /// The four spellings of a single-valued option against a positional input, in both orders:
    /// <c>--opt=value file</c>, <c>--opt value file</c>, <c>file --opt=value</c>,
    /// <c>file --opt value</c>. Every one of them must leave the positional alone.
    /// </summary>
    public static IEnumerable<object[]> Spellings()
    {
        foreach (var (command, option, value) in OptionSites)
        {
            yield return new object[] { $"{command} {option}={value} main.spy", option, value };
            yield return new object[] { $"{command} {option} {value} main.spy", option, value };
            yield return new object[] { $"{command} main.spy {option}={value}", option, value };
            yield return new object[] { $"{command} main.spy {option} {value}", option, value };
        }
    }

    public static IEnumerable<object[]> Sites()
    {
        foreach (var (command, option, value) in OptionSites)
        {
            yield return new object[] { command, option, value };
        }
    }

    [Theory]
    [MemberData(nameof(Spellings))]
    public void PathOption_TakesOneValue_AndLeavesThePositionalAlone(string commandLine, string option, string value)
    {
        var result = CliTestHarness.Parse(commandLine);

        result.Errors.Should().BeEmpty();
        result.GetValue<string[]>(option).Should().BeEquivalentTo(new[] { value });
        PositionalInputOf(result).Should().Be("main.spy");
    }

    [Theory]
    [MemberData(nameof(Sites))]
    public void PathOption_RepeatedOccurrencesCollect(string command, string option, string value)
    {
        var second = "second_" + value;
        var result = CliTestHarness.Parse($"{command} {option} {value} {option} {second} main.spy");

        result.Errors.Should().BeEmpty();
        result.GetValue<string[]>(option).Should().BeEquivalentTo(new[] { value, second });
        PositionalInputOf(result).Should().Be("main.spy");
    }

    /// <summary>
    /// The #1215 repro, spelled out: the option no longer eats the file that follows its value.
    /// </summary>
    [Fact]
    public void ModulePath_BeforeThePositional_DoesNotSwallowTheInputFile()
    {
        var result = CliTestHarness.Parse("run --module-path src file.spy");

        result.Errors.Should().BeEmpty();
        result.GetValue<string[]>("--module-path").Should().BeEquivalentTo(new[] { "src" });
        result.GetValue<FileInfo>("input")!.Name.Should().Be("file.spy");
    }

    /// <summary>
    /// All three path options at once, each before the positional — the shape that made the
    /// greedy arity impossible to notice one option at a time.
    /// </summary>
    [Fact]
    public void AllThreePathOptions_BeforeThePositional_LeaveItAlone()
    {
        var result = CliTestHarness.Parse(
            "run --reference a.dll --project-reference lib.csproj --module-path mods main.spy");

        result.Errors.Should().BeEmpty();
        result.GetValue<string[]>("--reference").Should().BeEquivalentTo(new[] { "a.dll" });
        result.GetValue<string[]>("--project-reference").Should().BeEquivalentTo(new[] { "lib.csproj" });
        result.GetValue<string[]>("--module-path").Should().BeEquivalentTo(new[] { "mods" });
        result.GetValue<FileInfo>("input")!.Name.Should().Be("main.spy");
    }

    /// <summary>
    /// Reads the token bound to the parsed command's single positional argument, the way
    /// <c>GlobalOptionsTests.EnableFeature_LeavesThePositionalFileAlone</c> does. Reading the
    /// bound token rather than the converted value keeps the assertion honest for commands whose
    /// argument converts to a <see cref="FileInfo"/>.
    /// </summary>
    private static string PositionalInputOf(System.CommandLine.ParseResult result)
    {
        var input = result.CommandResult.Command.Arguments.Single();
        return result.GetResult(input)!.Tokens.Single().Value;
    }
}
