using System.CommandLine;

namespace Sharpy.Cli;

/// <summary>The input kinds a file-taking command can be given; a table has one column per kind it accepts.</summary>
internal enum InputKind
{
    /// <summary>A single <c>.spy</c> source file.</summary>
    SpyFile,

    /// <summary>A <c>.spyproj</c> project file.</summary>
    SpyProject,

    /// <summary>
    /// Any other extension. Never a table column: commands that compile Sharpy source accept it as
    /// source with a warning (<see cref="CliHelpers.ValidateInputFile"/>), as they always have.
    /// </summary>
    Other,
}

/// <summary>What a command does with an option (#2173, #2223).</summary>
internal enum OptionEffect
{
    /// <summary>The option reaches the command and changes its result.</summary>
    Honoured,

    /// <summary>
    /// Giving the option is an error: the command prints why and exits with its usage code before
    /// reading or writing anything (<see cref="CommandOptionEffects.Refuse"/>).
    /// </summary>
    Refused,

    /// <summary>
    /// The option has no effect, and <c>--help</c> says so: an own option's description carries the
    /// generated suffix, a global option is named in the command description's generated sentence
    /// (<see cref="CommandOptionEffects.Document"/>).
    /// </summary>
    Ignored,
}

/// <summary>
/// What a command does with its input. The sweep asserts the globals rule from it (Design
/// Decision 1 of P46): a command that compiles honours all nine global options; one that analyses
/// honours the seven analysis-facing ones; one that lexes or parses honours the logger options and
/// <c>--max-errors</c>; formatting and tooling commands honour the logger where they create one.
/// </summary>
internal enum CommandPerforms
{
    /// <summary>Runs the whole compiler (to C#, an assembly, or a running program).</summary>
    Compiles,

    /// <summary>Runs lexing, parsing and semantic analysis, and reports on the result.</summary>
    Analyses,

    /// <summary>Runs the lexer only.</summary>
    Lexes,

    /// <summary>Runs the lexer and parser only.</summary>
    Parses,

    /// <summary>Formats source text.</summary>
    Formats,

    /// <summary>Runs no compile of a user input (cache, explain, language server, REPL, compile server).</summary>
    Tooling,
}

/// <summary>One row of a command's option table: the effect per input kind and why.</summary>
internal sealed record OptionEffectRow(OptionEffect SpyFile, OptionEffect SpyProject, string Reason)
{
    /// <summary>A row with the same effect for every input kind (every one-column table uses these).</summary>
    internal static OptionEffectRow Uniform(OptionEffect effect, string reason) => new(effect, effect, reason);

    internal OptionEffect For(InputKind kind) => kind == InputKind.SpyProject ? SpyProject : SpyFile;

    internal bool IsUniform => SpyFile == SpyProject;
}

/// <summary>
/// The option table of one command: every option it accepts — its own and the recursive global
/// ones — with what it does for each input kind it accepts (<see cref="Columns"/>; empty for a
/// command that takes no file).
/// </summary>
internal sealed record CommandEffectTable(
    CommandPerforms Performs,
    IReadOnlyList<InputKind> Columns,
    IReadOnlyDictionary<string, OptionEffectRow> Rows,
    int UsageExitCode = CliHelpers.ExitCompileError);

/// <summary>
/// The option-to-effect registry for the whole <c>sharpyc</c> grammar, keyed by command path
/// (<c>"compile"</c>, <c>"emit csharp"</c>, <c>"cache clear"</c>, …): a parsed option is honoured,
/// refused for that input kind with a message, or documented as ignored — never silently dropped
/// (#2173 for <c>compile</c>, #2223 for every other command). <c>--help</c> notes and refusals are
/// generated from it, and <c>CliOptionEffectSweepTests</c> walks the real root grammar to check the
/// registry is total, that every refused cell refuses, every ignored cell is documented, and every
/// honoured cell is observed by a test or a global-option probe.
/// </summary>
internal static class CommandOptionEffects
{
    // ---- Global options, by the effect the globals rule (CommandPerforms) gives them ----

    internal static readonly IReadOnlyList<string> GlobalOptionNames = new[]
    {
        "--log-level", "--log-file", "--metrics-format", "--metrics-output", "--warn-as-error",
        "--nowarn", "--max-errors", "--verbose", "--enable-feature",
    };

    /// <summary>The logger globals every command that creates a compiler logger honours.</summary>
    internal static readonly IReadOnlyList<string> LoggerGlobals = new[] { "--log-level", "--log-file", "--verbose" };

    /// <summary>The globals an analysis (no assembly, no metrics) honours: the logger, error limit, warnings and features.</summary>
    internal static readonly IReadOnlyList<string> AnalysisGlobals = new[]
    {
        "--log-level", "--log-file", "--verbose", "--max-errors", "--warn-as-error", "--nowarn", "--enable-feature",
    };

    internal static readonly IReadOnlyList<string> MetricsGlobals = new[] { "--metrics-format", "--metrics-output" };

    static readonly IReadOnlyDictionary<string, string> HonouredGlobalReasons = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["--log-level"] = "the compiler logger's level",
        ["--log-file"] = "the compiler log goes to this file (at the --log-level/--verbose level)",
        ["--metrics-format"] = "compilation metrics are printed",
        ["--metrics-output"] = "compilation metrics are written to this file",
        ["--warn-as-error"] = "warnings fail the command",
        ["--nowarn"] = "the codes are suppressed",
        ["--max-errors"] = "error reporting stops after this many",
        ["--verbose"] = "raises the log level to Info and shows diagnostic provenance",
        ["--enable-feature"] = "the experimental feature is enabled",
    };

    static OptionEffectRow H(string reason) => OptionEffectRow.Uniform(OptionEffect.Honoured, reason);

    static OptionEffectRow I(string reason) => OptionEffectRow.Uniform(OptionEffect.Ignored, reason);

    static OptionEffectRow R(string reason) => OptionEffectRow.Uniform(OptionEffect.Refused, reason);

    const string HelpReason = "prints help instead of running the command";

    /// <summary>
    /// Builds a one-column (or column-less) table: the command's own rows, then every global as
    /// honoured unless <paramref name="ignoredGlobals"/> names it with the reason it has no effect.
    /// </summary>
    static CommandEffectTable Table(
        CommandPerforms performs,
        InputKind[] columns,
        IReadOnlyDictionary<string, OptionEffectRow> own,
        IReadOnlyDictionary<string, string>? ignoredGlobals = null,
        IReadOnlyDictionary<string, string>? honouredGlobalReasons = null,
        int usageExitCode = CliHelpers.ExitCompileError)
    {
        var rows = new Dictionary<string, OptionEffectRow>(own, StringComparer.Ordinal);
        foreach (var global in GlobalOptionNames)
        {
            rows[global] = ignoredGlobals != null && ignoredGlobals.TryGetValue(global, out var why)
                ? I(why)
                : H(honouredGlobalReasons != null && honouredGlobalReasons.TryGetValue(global, out var reason)
                    ? reason
                    : HonouredGlobalReasons[global]);
        }

        rows["--help"] = H(HelpReason);
        return new CommandEffectTable(performs, columns, rows, usageExitCode);
    }

    /// <summary>Every global ignored for one reason (commands that create no compiler logger).</summary>
    static IReadOnlyDictionary<string, string> AllGlobalsIgnored(string reason) =>
        GlobalOptionNames.ToDictionary(g => g, _ => reason, StringComparer.Ordinal);

    static IReadOnlyDictionary<string, string> Ignore(string reason, params string[] globals) =>
        globals.ToDictionary(g => g, _ => reason, StringComparer.Ordinal);

    static IReadOnlyDictionary<string, string> Merge(params IReadOnlyDictionary<string, string>[] parts)
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in parts)
        {
            foreach (var (key, value) in part)
                merged[key] = value;
        }

        return merged;
    }

    const string LexOnlyReason = "this command stops before semantic analysis, so no warnings, feature gates or compilation metrics arise";

    /// <summary>
    /// The <c>compile</c> table, moved here verbatim from <c>CompileCommand</c> (#2173): one column
    /// per input kind.
    /// </summary>
    static CommandEffectTable CompileTable()
    {
        var rows = new Dictionary<string, OptionEffectRow>(StringComparer.Ordinal)
        {
            ["--output"] = new(OptionEffect.Honoured, OptionEffect.Honoured,
                "the assembly path; for a project it overrides bin/{Configuration}/{TFM} (#2159)"),
            ["--configuration"] = new(OptionEffect.Honoured, OptionEffect.Honoured,
                "the build configuration (default Release)"),
            ["--type"] = new(OptionEffect.Honoured, OptionEffect.Ignored,
                "a project's output type is its <OutputType>"),
            ["--reference"] = new(OptionEffect.Honoured, OptionEffect.Honoured,
                "for a project, added to the .spyproj's <Reference> items; a reference that cannot be found is SPY0305"),
            ["--project-reference"] = new(OptionEffect.Refused, OptionEffect.Refused,
                CliHelpers.ProjectReferenceUnsupportedReason),
            ["--module-path"] = new(OptionEffect.Honoured, OptionEffect.Honoured,
                "for a project, added to the .spyproj's <ModulePath> items; a directory that does not exist is an error"),
            ["--self-contained"] = new(OptionEffect.Honoured, OptionEffect.Refused,
                "self-contained publishing of a .spyproj is not supported yet"),
            ["--no-deps"] = new(OptionEffect.Honoured, OptionEffect.Honoured,
                "runtime dependencies are not copied beside the output"),
            ["--incremental"] = new(OptionEffect.Ignored, OptionEffect.Honoured,
                "the incremental cache lives in a project's obj/"),
            ["--clean"] = new(OptionEffect.Ignored, OptionEffect.Honoured,
                "only a project has bin/ and obj/ directories to delete"),
            ["--emit-csharp"] = new(OptionEffect.Honoured, OptionEffect.Honoured,
                "generated C# is mirrored beside the output (#2159)"),
        };

        // Recursive global options (GlobalOptions): every one reaches both paths.
        var table = Table(CommandPerforms.Compiles, new[] { InputKind.SpyFile, InputKind.SpyProject }, rows,
            honouredGlobalReasons: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["--warn-as-error"] = "warnings fail the compile (OR-ed with a project's <WarningsAsErrors>)",
                ["--nowarn"] = "the codes are suppressed (unioned with a project's <NoWarn>)",
                ["--enable-feature"] = "the experimental feature is enabled (unioned with a project's <Features>)",
            });
        var withHelp = new Dictionary<string, OptionEffectRow>(table.Rows, StringComparer.Ordinal)
        {
            ["--help"] = H("prints help instead of compiling"),
        };
        return table with { Rows = withHelp };
    }

    /// <summary>The registry: one table per leaf command path of the real root grammar.</summary>
    internal static readonly IReadOnlyDictionary<string, CommandEffectTable> Tables =
        new Dictionary<string, CommandEffectTable>(StringComparer.Ordinal)
        {
            ["build"] = Table(CommandPerforms.Compiles, new[] { InputKind.SpyFile },
                new Dictionary<string, OptionEffectRow>(StringComparer.Ordinal)
                {
                    ["--type"] = H("exe or library"),
                    ["--output"] = H("the assembly path"),
                    ["--reference"] = H("a reference that cannot be found is SPY0305"),
                    ["--project-reference"] = R(CliHelpers.ProjectReferenceUnsupportedReason),
                    ["--module-path"] = H("a directory that does not exist is an error"),
                    ["--server"] = H("compiles through a running 'sharpyc server'"),
                }),

            ["compile"] = CompileTable(),

            ["run"] = Table(CommandPerforms.Compiles, new[] { InputKind.SpyFile },
                new Dictionary<string, OptionEffectRow>(StringComparer.Ordinal)
                {
                    ["--output"] = H("the assembly path (temporary if not given)"),
                    ["--reference"] = H("a reference that cannot be found is SPY0305"),
                    ["--project-reference"] = R(CliHelpers.ProjectReferenceUnsupportedReason),
                    ["--module-path"] = H("a directory that does not exist is an error"),
                    ["--args"] = H("deprecated spelling of the program arguments after '--'"),
                    ["--namespace"] = H("the generated code is wrapped in this namespace"),
                    ["--self-contained"] = H("the program is published self-contained and run"),
                    ["--server"] = H("compiles through a running 'sharpyc server'"),
                }),

            ["project"] = Table(CommandPerforms.Compiles, new[] { InputKind.SpyProject },
                new Dictionary<string, OptionEffectRow>(StringComparer.Ordinal)
                {
                    ["--configuration"] = H("the build configuration (default Debug)"),
                    ["--clean"] = H("bin/ and obj/ are deleted first"),
                    ["--emit-cs-to"] = H("generated C# is written to this directory"),
                    ["--incremental"] = H("unchanged files are not recompiled"),
                    ["--reference"] = H("added to the .spyproj's <Reference> items; a reference that cannot be found is SPY0305"),
                    ["--module-path"] = H("added to the .spyproj's <ModulePath> items; a directory that does not exist is an error"),
                }),

            ["emit tokens"] = Table(CommandPerforms.Lexes, new[] { InputKind.SpyFile },
                new Dictionary<string, OptionEffectRow>(StringComparer.Ordinal),
                ignoredGlobals: Ignore(LexOnlyReason,
                    "--metrics-format", "--metrics-output", "--warn-as-error", "--nowarn", "--enable-feature")),

            ["emit ast"] = Table(CommandPerforms.Parses, new[] { InputKind.SpyFile },
                new Dictionary<string, OptionEffectRow>(StringComparer.Ordinal),
                ignoredGlobals: Ignore(LexOnlyReason,
                    "--metrics-format", "--metrics-output", "--warn-as-error", "--nowarn", "--enable-feature")),

            ["emit parse"] = Table(CommandPerforms.Parses, new[] { InputKind.SpyFile },
                new Dictionary<string, OptionEffectRow>(StringComparer.Ordinal),
                ignoredGlobals: Ignore(LexOnlyReason,
                    "--metrics-format", "--metrics-output", "--warn-as-error", "--nowarn", "--enable-feature")),

            ["emit csharp"] = Table(CommandPerforms.Compiles, new[] { InputKind.SpyFile },
                new Dictionary<string, OptionEffectRow>(StringComparer.Ordinal)
                {
                    ["--output"] = H("the C# file path"),
                    ["--reference"] = H("a reference that cannot be found is SPY0305"),
                    ["--module-path"] = H("a directory that does not exist is an error"),
                    ["--show-line-directives"] = H("#line directives are kept"),
                    ["--type"] = H("exe or library"),
                    ["--namespace"] = H("the generated code is wrapped in this namespace"),
                }),

            ["emit diagnostics"] = Table(CommandPerforms.Analyses, new[] { InputKind.SpyFile },
                new Dictionary<string, OptionEffectRow>(StringComparer.Ordinal)
                {
                    ["--format"] = H("text or json"),
                    ["--include-codegen"] = H("code generation diagnostics are included"),
                    ["--reference"] = H("a reference that cannot be found is SPY0305"),
                    ["--module-path"] = H("a directory that does not exist is an error"),
                },
                ignoredGlobals: Ignore("emit diagnostics reports diagnostics, not compilation metrics; build, compile, run and emit csharp report them",
                    "--metrics-format", "--metrics-output")),

            ["emit hover"] = Table(CommandPerforms.Analyses, new[] { InputKind.SpyFile },
                new Dictionary<string, OptionEffectRow>(StringComparer.Ordinal)
                {
                    ["--line"] = H("the hovered line"),
                    ["--col"] = H("the hovered column"),
                    ["--reference"] = H("a reference that cannot be found is SPY0305"),
                    ["--module-path"] = H("a directory that does not exist is an error"),
                },
                ignoredGlobals: Ignore("hover analyses the file but compiles nothing, so it has no compilation metrics",
                    "--metrics-format", "--metrics-output")),

            ["cache clear"] = Table(CommandPerforms.Tooling, Array.Empty<InputKind>(),
                new Dictionary<string, OptionEffectRow>(StringComparer.Ordinal)
                {
                    ["--cache-dir"] = H("the cache directory to clear"),
                },
                ignoredGlobals: AllGlobalsIgnored("cache clear deletes cache files; it runs no compiler and creates no logger")),

            ["cache info"] = Table(CommandPerforms.Tooling, Array.Empty<InputKind>(),
                new Dictionary<string, OptionEffectRow>(StringComparer.Ordinal)
                {
                    ["--cache-dir"] = H("the cache directory to describe"),
                },
                ignoredGlobals: AllGlobalsIgnored("cache info reads cache files; it runs no compiler and creates no logger")),

            ["explain"] = Table(CommandPerforms.Tooling, Array.Empty<InputKind>(),
                new Dictionary<string, OptionEffectRow>(StringComparer.Ordinal)
                {
                    ["--list"] = H("every documented code is listed"),
                },
                ignoredGlobals: AllGlobalsIgnored("explain prints documentation; it runs no compiler and creates no logger")),

            ["lsp"] = Table(CommandPerforms.Tooling, Array.Empty<InputKind>(),
                new Dictionary<string, OptionEffectRow>(StringComparer.Ordinal)
                {
                    ["--stdio"] = I("stdio is the only transport; the flag is accepted for editor compatibility"),
                },
                ignoredGlobals: Ignore(
                    "the language server owns its logging and analysis settings; only --log-level is forwarded to it (#1225)",
                    "--log-file", "--metrics-format", "--metrics-output", "--warn-as-error", "--nowarn",
                    "--max-errors", "--verbose", "--enable-feature"),
                honouredGlobalReasons: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["--log-level"] = "forwarded to the language server's logger (#1225)",
                }),

            ["repl"] = Table(CommandPerforms.Tooling, Array.Empty<InputKind>(),
                new Dictionary<string, OptionEffectRow>(StringComparer.Ordinal),
                ignoredGlobals: Merge(
                    Ignore("the REPL compiles snippets in memory and reports no compilation metrics",
                        "--metrics-format", "--metrics-output"),
                    Ignore("the REPL session takes only a logger and features; each snippet's diagnostics are shown as they arise",
                        "--warn-as-error", "--nowarn", "--max-errors"))),

            ["format"] = Table(CommandPerforms.Formats, new[] { InputKind.SpyFile },
                new Dictionary<string, OptionEffectRow>(StringComparer.Ordinal)
                {
                    ["--check"] = H("exits 1 if a file would change; writes nothing"),
                    ["--diff"] = H("prints a unified diff; writes nothing"),
                    ["--output"] = H("the formatted text goes to this file"),
                    ["--indent"] = H("validated: only 4 is accepted, any other width is a usage error"),
                    ["--tabs"] = R("Sharpy does not allow tabs for indentation; it is exactly 4 spaces per level (docs/language_specification/indentation.md)"),
                },
                ignoredGlobals: AllGlobalsIgnored("the formatter runs no compiler and creates no logger"),
                usageExitCode: 2),

            ["server"] = Table(CommandPerforms.Tooling, Array.Empty<InputKind>(),
                new Dictionary<string, OptionEffectRow>(StringComparer.Ordinal)
                {
                    ["--pipe"] = H("the named pipe the server listens on"),
                    ["--idle-timeout"] = H("the server exits after this many idle seconds"),
                },
                ignoredGlobals: Ignore("each compile request carries its own compiler options from the client",
                    "--metrics-format", "--metrics-output", "--warn-as-error", "--nowarn", "--max-errors",
                    "--enable-feature"),
                honouredGlobalReasons: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["--verbose"] = "raises the server's log level to Info",
                }),
        };

    /// <summary>The leaf command path of <paramref name="command"/> (<c>"emit csharp"</c>), without the root name.</summary>
    internal static string PathOf(Command command)
    {
        var names = new System.Collections.Generic.List<string>();
        for (Command? current = command; current is not null and not RootCommand; current = current.Parents.OfType<Command>().FirstOrDefault())
        {
            names.Add(current.Name);
        }

        names.Reverse();
        return string.Join(' ', names);
    }

    static string EffectWord(OptionEffect effect) => effect == OptionEffect.Refused ? "refused" : "ignored";

    static string KindName(InputKind kind) => kind == InputKind.SpyFile ? ".spy" : ".spyproj";

    /// <summary>
    /// The <c>--help</c> suffix that documents a row's non-honoured cells, e.g.
    /// <c>(ignored for .spyproj: …)</c>. Empty when the option is honoured for every input kind.
    /// </summary>
    internal static string DescriptionSuffix(CommandEffectTable table, OptionEffectRow row)
    {
        var notes = new System.Collections.Generic.List<string>();
        if (table.Columns.Count < 2)
        {
            var effect = table.Columns.Count == 1 ? row.For(table.Columns[0]) : row.SpyFile;
            if (effect != OptionEffect.Honoured)
                notes.Add($"{EffectWord(effect)}: {row.Reason}");
        }
        else if (row.IsUniform && row.SpyFile != OptionEffect.Honoured)
        {
            notes.Add($"{EffectWord(row.SpyFile)} for {string.Join(" and ", table.Columns.Select(KindName))}: {row.Reason}");
        }
        else
        {
            foreach (var kind in table.Columns)
            {
                var effect = row.For(kind);
                if (effect != OptionEffect.Honoured)
                    notes.Add($"{EffectWord(effect)} for {KindName(kind)}: {row.Reason}");
            }
        }

        return notes.Count == 0 ? string.Empty : $" ({string.Join("; ", notes)})";
    }

    /// <summary>
    /// The sentence appended to a command's description naming the global options that have no
    /// effect on it (their <c>--help</c> text is shared with every command, so the note goes on the
    /// command). Empty when every global is honoured.
    /// </summary>
    internal static string GlobalsSentence(CommandEffectTable table)
    {
        var ignored = GlobalOptionNames
            .Where(g => table.Rows.TryGetValue(g, out var row) && row.IsUniform && row.SpyFile == OptionEffect.Ignored)
            .ToList();
        return ignored.Count == 0 ? string.Empty : $" Global options without effect here: {string.Join(", ", ignored)}.";
    }

    /// <summary>
    /// Documents every non-honoured cell in <c>--help</c>: own options get the
    /// <see cref="DescriptionSuffix"/>, and the command description gets the
    /// <see cref="GlobalsSentence"/>. Called once on the real root after every command is configured,
    /// so a new command cannot skip it.
    /// </summary>
    internal static void Document(RootCommand root)
    {
        foreach (var command in LeafCommands(root))
        {
            if (!Tables.TryGetValue(PathOf(command), out var table))
                continue;

            foreach (var option in command.Options)
            {
                if (table.Rows.TryGetValue(option.Name, out var row))
                    option.Description += DescriptionSuffix(table, row);
            }

            command.Description += GlobalsSentence(table);
        }
    }

    /// <summary>The commands of <paramref name="root"/> that have an action (leaves), depth first.</summary>
    internal static IEnumerable<Command> LeafCommands(Command root)
    {
        foreach (var sub in root.Subcommands)
        {
            if (sub.Subcommands.Count == 0)
            {
                yield return sub;
                continue;
            }

            foreach (var leaf in LeafCommands(sub))
                yield return leaf;
        }
    }

    /// <summary>
    /// Refuses, before anything is read or written, every option the user actually gave whose cell
    /// in <paramref name="command"/>'s table is <see cref="OptionEffect.Refused"/> for
    /// <paramref name="kind"/> (the first column when null). Returns false (after printing one error
    /// per refused option) when the command must exit with the table's usage code.
    /// </summary>
    internal static bool Refuse(ParseResult parseResult, Command command, InputKind? kind = null)
    {
        if (!Tables.TryGetValue(PathOf(command), out var table))
            return true;

        var column = kind ?? (table.Columns.Count > 0 ? table.Columns[0] : InputKind.SpyFile);
        var refused = false;
        foreach (var option in command.Options)
        {
            if (!table.Rows.TryGetValue(option.Name, out var row) || row.For(column) != OptionEffect.Refused)
                continue;
            if (parseResult.GetResult(option) is not { Implicit: false })
                continue;

            if (table.Columns.Count >= 2)
            {
                var inputKind = column == InputKind.SpyFile ? "a .spy file" : "a .spyproj project";
                Console.Error.WriteLine($"Error: {option.Name} is not supported when compiling {inputKind}: {row.Reason}.");
            }
            else
            {
                Console.Error.WriteLine($"Error: {option.Name} is not supported: {row.Reason}.");
            }

            refused = true;
        }

        return !refused;
    }
}
