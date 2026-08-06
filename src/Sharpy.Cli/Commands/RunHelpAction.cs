using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.Text;

namespace Sharpy.Cli.Commands;

/// <summary>
/// Renders <c>sharpyc run --help</c> with the <c>--</c> program-argument separator documented
/// (#1231). #1215 made <c>--</c> the way to pass a program its arguments and deprecated the
/// multi-token <c>--args</c> spelling, but the generated help still described only the option
/// grammar — so the readers most likely to need the new spelling were shown a page that never
/// mentioned it.
/// <para>
/// This is a <b>display-only</b> decoration. The separator is split off the raw argument vector
/// before parsing (<see cref="CliHelpers.SplitAtDoubleDash"/>), and it stays that way: nothing here
/// adds unmatched-token tolerance or a trailing variadic argument, either of which would stop
/// <c>run prog.spy --does-not-exist</c> from being a parse error (#1215).
/// </para>
/// <para>
/// System.CommandLine 2.0.10 keeps <c>HelpBuilder</c> and <c>HelpContext</c> internal, so the
/// section/layout customization hooks are unreachable from here. Instead this action wraps the
/// <see cref="HelpOption"/>'s own <see cref="HelpAction"/>: every command other than <c>run</c> is
/// forwarded to it untouched — the same instance on the same code path, so their help is byte
/// identical by construction — and only <c>run</c>'s render is captured and decorated.
/// </para>
/// </summary>
internal sealed class RunHelpAction : SynchronousCommandLineAction
{
    /// <summary>The indent System.CommandLine's help builder puts in front of section content.</summary>
    private const string Indent = "  ";

    /// <summary>Appended to the usage line so the separator is visible where the grammar is stated.</summary>
    internal const string UsageSuffix = "[-- <program-args>...]";

    /// <summary>Heading of the section appended after the generated help.</summary>
    internal const string SectionHeading = "Program arguments:";

    private readonly Command _runCommand;
    private readonly string _usagePrefix;
    private readonly SynchronousCommandLineAction _inner;

    private RunHelpAction(Command runCommand, string usagePrefix, SynchronousCommandLineAction inner)
    {
        _runCommand = runCommand;
        _usagePrefix = usagePrefix;
        _inner = inner;
    }

    /// <summary>
    /// Wraps the help action of <paramref name="root"/>'s recursive <see cref="HelpOption"/> — the
    /// one every subcommand's <c>--help</c> resolves to — so <paramref name="runCommand"/>'s help
    /// gains the separator documentation and no other command's changes. A no-op if the option or
    /// its action is not the shape we expect, or if it is already wrapped.
    /// </summary>
    internal static void Install(Command root, Command runCommand)
    {
        var helpOption = root.Options.OfType<HelpOption>().FirstOrDefault();
        if (helpOption?.Action is not SynchronousCommandLineAction inner || inner is RunHelpAction)
        {
            return;
        }

        helpOption.Action = new RunHelpAction(runCommand, $"{root.Name} {runCommand.Name}", inner);
    }

    /// <inheritdoc />
    public override bool Terminating => _inner.Terminating;

    /// <inheritdoc />
    public override bool ClearsParseErrors => _inner.ClearsParseErrors;

    /// <inheritdoc />
    public override int Invoke(ParseResult parseResult)
    {
        if (!ReferenceEquals(parseResult.CommandResult.Command, _runCommand))
        {
            return _inner.Invoke(parseResult);
        }

        // Capture the default render by pointing the invocation's output at a buffer, then restore
        // it before writing — the writer is shared with every other action, so it must come back
        // even if help rendering throws.
        var configuration = parseResult.InvocationConfiguration;
        var output = configuration.Output;
        using var buffer = new StringWriter { NewLine = output.NewLine };
        int exitCode;
        try
        {
            configuration.Output = buffer;
            exitCode = _inner.Invoke(parseResult);
        }
        finally
        {
            configuration.Output = output;
        }

        output.Write(Decorate(buffer.ToString(), _usagePrefix, output.NewLine));
        return exitCode;
    }

    /// <summary>
    /// Adds the separator to the usage line and appends the "Program arguments" section.
    /// <paramref name="usagePrefix"/> is the command path the usage line starts with
    /// (<c>sharpyc run</c>), which is how the line is located without depending on the localized
    /// "Usage:" heading. A usage line long enough to wrap spans several indented lines, so the
    /// suffix goes on the last of them; if the line cannot be found the section is still appended.
    /// </summary>
    internal static string Decorate(string help, string usagePrefix, string newLine)
    {
        var lines = help.Split(newLine).ToList();
        var usageLine = lines.FindIndex(line => line.StartsWith(Indent + usagePrefix, StringComparison.Ordinal));
        if (usageLine >= 0)
        {
            while (usageLine + 1 < lines.Count && lines[usageLine + 1].StartsWith(Indent, StringComparison.Ordinal))
            {
                usageLine++;
            }

            lines[usageLine] += " " + UsageSuffix;
        }

        var decorated = string.Join(newLine, lines);
        if (!decorated.EndsWith(newLine, StringComparison.Ordinal))
        {
            decorated += newLine;
        }

        return decorated + ProgramArgumentsSection(usagePrefix, newLine);
    }

    private static string ProgramArgumentsSection(string usagePrefix, string newLine)
    {
        var section = new StringBuilder();
        section.Append(SectionHeading).Append(newLine);
        section.Append("  Tokens after a bare '--' are the argument vector of the program being run, not").Append(newLine);
        section.Append("  sharpyc's own command line. They are split off before parsing, so they never").Append(newLine);
        section.Append("  reach the option grammar:").Append(newLine);
        section.Append(newLine);
        section.Append("    ").Append(usagePrefix).Append(" app.spy -- --verbose input.txt").Append(newLine);
        section.Append(newLine);
        section.Append("  '--args' is deprecated. It takes one value per occurrence, so write").Append(newLine);
        section.Append("  '-- a b c' rather than '--args a b c'.").Append(newLine);
        section.Append(newLine);
        return section.ToString();
    }
}
