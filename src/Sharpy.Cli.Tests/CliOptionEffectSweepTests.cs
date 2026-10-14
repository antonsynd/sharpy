using System.CommandLine;
using System.Reflection;
using FluentAssertions;
using Sharpy.Cli;
using Xunit;

namespace Sharpy.Cli.Tests;

/// <summary>
/// The #2223 contract over the WHOLE grammar: every option every command accepts — its own and the
/// recursive global ones — is honoured, refused for that input kind with a message, or documented as
/// ignored, never silently dropped. <c>compile</c> had this as one table (#2173,
/// <c>CompileOptionEffectTableTests</c>), which by construction could not see the other commands;
/// the registry is now <see cref="CommandOptionEffects"/>, keyed by command path, and this sweep walks
/// the real root (the one <c>Program.Main</c> builds, <see cref="CliRoot"/>) rather than a list of
/// commands, so a new command or option cannot be added without a row.
///
/// <para>Each cell is one (command path, input kind, option). Its row says:</para>
/// <list type="bullet">
/// <item><b>Refused</b> → the command, given the option, exits with its usage code naming the option
/// and writes nothing.</item>
/// <item><b>Ignored</b> → <c>--help</c> says so: an own option's description ends with the generated
/// suffix, a global option is named in the command description's generated sentence; and the
/// globals rule (<see cref="CommandPerforms"/>) does not require it to be honoured.</item>
/// <item><b>Honoured</b> → it is observed: a named evidence test exists (public, not skipped), or —
/// for a global option — the <see cref="GlobalOptionProbes"/> probe passes on this very command.</item>
/// </list>
///
/// <para><b>Ratchet.</b> <c>cli-option-effect-allowlist.txt</c> lists the cells that are wrong today,
/// each citing #2223: a failing cell not listed fails, a listed cell that passes fails ("stale row —
/// delete it"), and a row naming no cell fails. P46 drains it to zero.</para>
/// </summary>
public class CliOptionEffectSweepTests
{
    // ================================================================
    // The cell model
    // ================================================================

    /// <summary>
    /// One cell. <see cref="Column"/> is null for a one-column (or column-less) table, so the key has
    /// no kind suffix; <see cref="Missing"/> marks the "expected option missing" cells of the globals
    /// rule (a command that compiles or analyses declares <c>--reference</c> and <c>--module-path</c>).
    /// </summary>
    internal sealed record Cell(string Path, InputKind? Column, string Option, bool Missing = false)
    {
        public string Key => Missing
            ? $"{Path} {Option} missing"
            : Column is { } kind ? $"{Path}@{(kind == InputKind.SpyFile ? ".spy" : ".spyproj")} {Option}" : $"{Path} {Option}";
    }

    /// <summary>What the sweep needs besides the grammar and the registry (the synthetic controls swap it).</summary>
    internal sealed record SweepContext(
        IReadOnlyDictionary<string, string> Evidence,
        IReadOnlyDictionary<string, string> RefusedSpellings,
        IReadOnlyDictionary<string, string> Invocations,
        Func<string, CliInvocation> Invoke,
        Func<string, InputKind, CommandPerforms, string, string?> RunProbe,
        Assembly EvidenceAssembly);

    static readonly string[] RequiredPathOptions = { "--reference", "--module-path" };

    /// <summary>Every (leaf command, option) pair of <paramref name="root"/>: own options, then the recursive ones.</summary>
    internal static IEnumerable<(Command Command, string Path, Option Option, bool Global)> Pairs(RootCommand root)
    {
        var recursive = root.Options.Where(o => o.Recursive).ToList();
        foreach (var command in CommandOptionEffects.LeafCommands(root))
        {
            var path = CommandOptionEffects.PathOf(command);
            foreach (var option in command.Options)
                yield return (command, path, option, false);
            foreach (var option in recursive)
                yield return (command, path, option, true);
        }
    }

    /// <summary>The cells of <paramref name="root"/> under <paramref name="tables"/> (rows that exist only; totality is checked apart).</summary>
    internal static IEnumerable<Cell> Cells(RootCommand root, IReadOnlyDictionary<string, CommandEffectTable> tables)
    {
        foreach (var (_, path, option, _) in Pairs(root))
        {
            if (!tables.TryGetValue(path, out var table) || !table.Rows.ContainsKey(option.Name))
                continue;
            if (table.Columns.Count >= 2)
            {
                foreach (var kind in table.Columns)
                    yield return new Cell(path, kind, option.Name);
            }
            else
            {
                yield return new Cell(path, null, option.Name);
            }
        }

        foreach (var command in CommandOptionEffects.LeafCommands(root))
        {
            var path = CommandOptionEffects.PathOf(command);
            if (!tables.TryGetValue(path, out var table)
                || table.Performs is not (CommandPerforms.Compiles or CommandPerforms.Analyses))
            {
                continue;
            }

            foreach (var required in RequiredPathOptions)
            {
                if (command.Options.All(o => o.Name != required))
                    yield return new Cell(path, null, required, Missing: true);
            }
        }
    }

    /// <summary>Totality both ways: every accepted option has a row, every row names an accepted option, every leaf has a table.</summary>
    internal static System.Collections.Generic.List<string> TotalityFailures(RootCommand root, IReadOnlyDictionary<string, CommandEffectTable> tables)
    {
        var failures = new System.Collections.Generic.List<string>();
        var accepted = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var (_, path, option, _) in Pairs(root))
        {
            if (!accepted.TryGetValue(path, out var names))
                accepted[path] = names = new HashSet<string>(StringComparer.Ordinal);
            names.Add(option.Name);
        }

        foreach (var command in CommandOptionEffects.LeafCommands(root))
        {
            var path = CommandOptionEffects.PathOf(command);
            if (!accepted.ContainsKey(path))
                accepted[path] = new HashSet<string>(StringComparer.Ordinal);
        }

        foreach (var (path, names) in accepted)
        {
            if (!tables.TryGetValue(path, out var table))
            {
                failures.Add($"`{path}` has no table in CommandOptionEffects.Tables");
                continue;
            }

            foreach (var name in names.Where(n => !table.Rows.ContainsKey(n)))
                failures.Add($"`{path} {name}` is accepted but has no row: say whether it is honoured, refused or ignored");
            foreach (var name in table.Rows.Keys.Where(n => !names.Contains(n)))
                failures.Add($"`{path}` has a row for {name}, which the command does not accept");
        }

        foreach (var path in tables.Keys.Where(p => !accepted.ContainsKey(p)))
            failures.Add($"table `{path}` names no command of the grammar");

        return failures;
    }

    /// <summary>Checks one cell. Returns null when the cell keeps the contract, else why not.</summary>
    internal static string? Check(Cell cell, RootCommand root, IReadOnlyDictionary<string, CommandEffectTable> tables, SweepContext context)
    {
        var table = tables[cell.Path];
        if (cell.Missing)
        {
            return $"`{cell.Path}` {DescribePerforms(table.Performs)}, so it must take {cell.Option} (the globals rule): an input "
                + "that imports a referenced library or a module on a search path cannot be handled without it";
        }

        var command = CommandOptionEffects.LeafCommands(root).Single(c => CommandOptionEffects.PathOf(c) == cell.Path);
        var row = table.Rows[cell.Option];
        var kind = cell.Column ?? (table.Columns.Count > 0 ? table.Columns[0] : InputKind.SpyFile);
        var effect = row.For(kind);
        var isGlobal = command.Options.All(o => o.Name != cell.Option);

        switch (effect)
        {
            case OptionEffect.Ignored:
                {
                    var ruleBreak = RuleBreak(table.Performs, cell.Option, isGlobal);
                    if (ruleBreak != null)
                        return ruleBreak;

                    if (isGlobal)
                    {
                        var description = command.Description ?? string.Empty;
                        var sentence = CommandOptionEffects.GlobalsSentence(table);
                        return sentence.Length > 0 && description.EndsWith(sentence, StringComparison.Ordinal)
                            && sentence.Contains(cell.Option, StringComparison.Ordinal)
                            ? null
                            : $"{cell.Option} is ignored by `{cell.Path}`, but the command's --help description does not name it "
                              + "among the global options without effect";
                    }

                    var option = command.Options.Single(o => o.Name == cell.Option);
                    var suffix = CommandOptionEffects.DescriptionSuffix(table, row);
                    return suffix.Length > 0 && (option.Description ?? string.Empty).EndsWith(suffix, StringComparison.Ordinal)
                        ? null
                        : $"{cell.Option} is ignored by `{cell.Path}`, but its --help description does not say so";
                }

            case OptionEffect.Refused:
                return CheckRefusal(cell, table, kind, context);

            default:
                return CheckHonoured(cell, table, kind, isGlobal, context);
        }
    }

    static string DescribePerforms(CommandPerforms performs) => performs switch
    {
        CommandPerforms.Compiles => "compiles",
        CommandPerforms.Analyses => "analyses its input",
        CommandPerforms.Lexes => "lexes its input",
        CommandPerforms.Parses => "parses its input",
        _ => performs.ToString(),
    };

    /// <summary>The globals rule (Design Decision 1 of P46): which globals a command may not ignore.</summary>
    static string? RuleBreak(CommandPerforms performs, string option, bool isGlobal)
    {
        if (!isGlobal || option == "--help")
            return null;

        IReadOnlyList<string> required = performs switch
        {
            CommandPerforms.Compiles => CommandOptionEffects.GlobalOptionNames,
            CommandPerforms.Analyses => CommandOptionEffects.AnalysisGlobals,
            CommandPerforms.Lexes or CommandPerforms.Parses => CommandOptionEffects.LoggerGlobals.Append("--max-errors").ToList(),
            _ => Array.Empty<string>(),
        };
        return required.Contains(option)
            ? $"a command that {DescribePerforms(performs)} honours {option} (the globals rule); documenting it as ignored does not satisfy the contract"
            : null;
    }

    static string? CheckRefusal(Cell cell, CommandEffectTable table, InputKind kind, SweepContext context)
    {
        if (!context.RefusedSpellings.TryGetValue(cell.Key, out var spelling))
            return $"refused cell `{cell.Key}` has no command-line spelling in RefusedSpellings; add one so the refusal is exercised";
        if (!context.Invocations.TryGetValue(cell.Path, out var template))
            return $"refused cell `{cell.Key}`: `{cell.Path}` has no invocation template";

        using var input = new GlobalOptionProbes.ProbeInput("def main():\n    print(\"plain\")\n", kind);
        var line = template.Replace("{input}", input.Input).Replace("{out}", input.Out) + " " + spelling;
        var invocation = context.Invoke(line);
        var problems = new System.Collections.Generic.List<string>();
        if (invocation.ExitCode != table.UsageExitCode)
            problems.Add($"exit {invocation.ExitCode}, expected the usage code {table.UsageExitCode}");
        if (!invocation.StdErr.Contains(cell.Option, StringComparison.Ordinal)
            || !invocation.StdErr.Contains("not supported", StringComparison.Ordinal))
        {
            problems.Add($"stderr does not say {cell.Option} is not supported");
        }

        if (Directory.Exists(input.Out))
            problems.Add("the output directory was written");

        return problems.Count == 0
            ? null
            : $"refused cell `{cell.Key}` (`{spelling}`): {string.Join("; ", problems)}\n--- stdout ---\n{invocation.StdOut}\n--- stderr ---\n{invocation.StdErr}";
    }

    static string? CheckHonoured(Cell cell, CommandEffectTable table, InputKind kind, bool isGlobal, SweepContext context)
    {
        var evidence = EvidenceFor(cell, context.Evidence);
        if (evidence != null)
            return ResolveEvidence(evidence, context.EvidenceAssembly);

        if (isGlobal && GlobalOptionProbes.ProbedOptions.Contains(cell.Option)
            && (cell.Option == "--help" || context.Invocations.ContainsKey(cell.Path)))
        {
            return context.RunProbe(cell.Path, kind, table.Performs, cell.Option);
        }

        return $"`{cell.Key}` is honoured but nothing observes it: name an evidence test (Evidence) that shows the option's effect";
    }

    static string? EvidenceFor(Cell cell, IReadOnlyDictionary<string, string> evidence) =>
        evidence.TryGetValue(cell.Key, out var exact) ? exact
        : evidence.TryGetValue($"{cell.Path} {cell.Option}", out var any) ? any
        : null;

    /// <summary>An evidence name resolves to a public, non-skipped xUnit test method of the test assembly.</summary>
    internal static string? ResolveEvidence(string evidence, Assembly assembly)
    {
        var dot = evidence.LastIndexOf('.');
        var type = dot > 0 ? assembly.GetType(evidence[..dot]) : null;
        var method = type?.GetMethod(evidence[(dot + 1)..], BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        var fact = method?.GetCustomAttribute<FactAttribute>(inherit: true);
        if (fact == null)
            return $"evidence `{evidence}` is not a public [Fact]/[Theory] method of {assembly.GetName().Name}";
        return fact.Skip != null ? $"evidence `{evidence}` is skipped ({fact.Skip})" : null;
    }

    // ================================================================
    // The real grammar's context
    // ================================================================

    /// <summary>
    /// The test that observes each honoured own option (and each honoured global on a command with
    /// no probe invocation). Key: <c>"{path} {option}"</c>, or <c>"{path}@.spy|@.spyproj {option}"</c>
    /// to name a test for one input kind only.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> Evidence = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["build --type"] = "Sharpy.Cli.Tests.Commands.OptionEffectEvidenceTests.Build_TypeLibrary_CompilesAMainlessFile",
        ["build --output"] = "Sharpy.Cli.Tests.Commands.BuildCommandTests.Build_ValidSource_ReturnsExitCode0",
        ["build --reference"] = "Sharpy.Cli.Tests.Commands.CompileOptionEffectTableTests.AReferenceThatResolvesNowhere_IsSpy0305",
        ["build --module-path"] = "Sharpy.Cli.Tests.Commands.CompileOptionEffectTableTests.AModulePathThatDoesNotExist_IsAnError",
        ["build --server"] = "Sharpy.Cli.Tests.E2E.CompileServerE2ETests.Client_FallsBackToInProcess_WhenNoServerIsRunning",

        ["compile@.spy --output"] = "Sharpy.Cli.Tests.Commands.CompileCommandTests.Compile_ValidSource_ProducesDll",
        ["compile@.spyproj --output"] = "Sharpy.Cli.Tests.Commands.GeneratedCSharpLayoutTests.CompileProject_OutputOption_PlacesTheAssemblyAndItsCSharp",
        ["compile --configuration"] = "Sharpy.Cli.Tests.Commands.OptionEffectEvidenceTests.Compile_Configuration_SelectsTheProjectOutputDirectory",
        ["compile --type"] = "Sharpy.Cli.Tests.Commands.OptionEffectEvidenceTests.Compile_TypeLibrary_CompilesAMainlessFile",
        ["compile@.spy --reference"] = "Sharpy.Cli.Tests.Commands.CompileOptionEffectTableTests.AReferenceThatResolvesNowhere_IsSpy0305",
        ["compile@.spyproj --reference"] = "Sharpy.Cli.Tests.Commands.CompileOptionEffectTableTests.Reference_IsHonouredForAProject",
        ["compile --module-path"] = "Sharpy.Cli.Tests.Commands.CompileOptionEffectTableTests.ModulePath_ResolvesABareReferenceName",
        ["compile --self-contained"] = "Sharpy.Cli.Tests.E2E.DotnetHostPrerequisiteTests.WithoutDotnet_RefusesBeforeDoingAnyWork",
        ["compile --no-deps"] = "Sharpy.Cli.Tests.Commands.CompileCommandTests.Compile_NoDeps_SkipsCopyingRuntimeDependencies",
        ["compile --incremental"] = "Sharpy.Cli.Tests.Commands.OptionEffectEvidenceTests.CompileProject_Incremental_WritesTheIncrementalCache",
        ["compile --clean"] = "Sharpy.Cli.Tests.Commands.OptionEffectEvidenceTests.CompileProject_Clean_DeletesBinAndObjFirst",
        ["compile --emit-csharp"] = "Sharpy.Cli.Tests.Commands.CompileCommandTests.Compile_EmitCSharp_WritesCSharpFileAlongsideDll",

        ["run --output"] = "Sharpy.Cli.Tests.Commands.OptionEffectEvidenceTests.Run_Output_WritesTheAssemblyThere",
        ["run --reference"] = "Sharpy.Cli.Tests.Commands.CompileOptionEffectTableTests.AReferenceThatResolvesNowhere_IsSpy0305",
        ["run --module-path"] = "Sharpy.Cli.Tests.E2E.ModuleSearchPathCliTests.ModulePathOption_ReachesImportResolution",
        ["run --args"] = "Sharpy.Cli.Tests.Commands.RunCommandTests.Run_RepeatedArgsOption_ReachesTheProgram",
        ["run --namespace"] = "Sharpy.Cli.Tests.Commands.OptionEffectEvidenceTests.Run_Namespace_WrapsTheGeneratedCode",
        ["run --self-contained"] = "Sharpy.Cli.Tests.E2E.SelfContainedDeploymentTests.SnakeCaseStem_PublishesAndRuns",
        ["run --server"] = "Sharpy.Cli.Tests.Commands.OptionEffectEvidenceTests.Run_Server_FallsBackToInProcessWhenNoServerRuns",

        ["project --configuration"] = "Sharpy.Cli.Tests.Commands.OptionEffectEvidenceTests.Project_Configuration_SelectsTheOutputDirectory",
        ["project --clean"] = "Sharpy.Cli.Tests.Commands.OptionEffectEvidenceTests.Project_Clean_DeletesBinAndObjFirst",
        ["project --emit-cs-to"] = "Sharpy.Cli.Tests.Commands.ProjectCommandTests.EmitCsTo_FlatModule_WritesAtProjectRelativePath",
        ["project --incremental"] = "Sharpy.Cli.Tests.Commands.OptionEffectEvidenceTests.Project_Incremental_WritesTheIncrementalCache",

        ["emit csharp --output"] = "Sharpy.Cli.Tests.Commands.EmitCommandTests.CSharp_WritesGeneratedFile",
        ["emit csharp --reference"] = "Sharpy.Cli.Tests.Commands.CompileOptionEffectTableTests.AReferenceThatResolvesNowhere_IsSpy0305",
        ["emit csharp --module-path"] = "Sharpy.Cli.Tests.E2E.ModuleSearchPathCliTests.ModulePathOption_ReachesImportResolution",
        ["emit csharp --show-line-directives"] = "Sharpy.Cli.Tests.Commands.OptionEffectEvidenceTests.EmitCSharp_ShowLineDirectives_KeepsLineDirectives",
        ["emit csharp --type"] = "Sharpy.Cli.Tests.Commands.OptionEffectEvidenceTests.EmitCSharp_TypeExe_RequiresAnEntryPoint",
        ["emit csharp --namespace"] = "Sharpy.Cli.Tests.Commands.OptionEffectEvidenceTests.EmitCSharp_Namespace_WrapsTheGeneratedCode",

        ["emit diagnostics --format"] = "Sharpy.Cli.Tests.Commands.EmitCommandTests.Diagnostics_JsonFormat_ProducesJsonArray",
        ["emit diagnostics --include-codegen"] = "Sharpy.Cli.Tests.Commands.OptionEffectEvidenceTests.EmitDiagnostics_IncludeCodegen_RunsTheCompilePipeline",
        ["emit hover --line"] = "Sharpy.Cli.Tests.Commands.OptionEffectEvidenceTests.EmitHover_LineAndCol_SelectTheHoveredSymbol",
        ["emit hover --col"] = "Sharpy.Cli.Tests.Commands.OptionEffectEvidenceTests.EmitHover_LineAndCol_SelectTheHoveredSymbol",

        ["cache clear --cache-dir"] = "Sharpy.Cli.Tests.Commands.CacheCommandTests.Clear_CacheDirIsAFile_ReturnsExitCode1",
        ["cache info --cache-dir"] = "Sharpy.Cli.Tests.Commands.CacheCommandTests.Info_CacheDirIsAFile_ReturnsExitCode1",
        ["explain --list"] = "Sharpy.Cli.Tests.Commands.ExplainCommandTests.List_PrintsDocumentedCodes",

        ["format --check"] = "Sharpy.Cli.Tests.Commands.FormatCommandTests.Check_OnFormattedFile_ReportsClean",
        ["format --diff"] = "Sharpy.Cli.Tests.Commands.FormatCommandTests.CheckAndDiff_Combined_IsUsageError",
        ["format --output"] = "Sharpy.Cli.Tests.Commands.FormatCommandTests.OutputOption_RejectedForDirectoryInput",
        ["format --indent"] = "Sharpy.Cli.Tests.Commands.FormatCommandTests.NonFourSpaceIndentation_IsUsageError_AndLeavesTheFileUntouched",

        ["lsp --log-level"] = "Sharpy.Cli.Tests.Commands.LspCommandTests.ServerArgs_PresentLevel_BecomesTheServersFlag",

        ["repl --log-level"] = "Sharpy.Cli.Tests.Commands.OptionEffectEvidenceTests.Repl_LogLevel_EnablesTheLogger",
        ["repl --log-file"] = "Sharpy.Cli.Tests.Commands.OptionEffectEvidenceTests.Repl_LogFile_ReceivesTheLog",
        ["repl --verbose"] = "Sharpy.Cli.Tests.Commands.OptionEffectEvidenceTests.Repl_Verbose_RaisesTheLogLevel",
        ["repl --enable-feature"] = "Sharpy.Cli.Tests.Commands.OptionEffectEvidenceTests.Repl_EnableFeature_AcceptsGatedSyntax",

        ["server --pipe"] = "Sharpy.Cli.Tests.E2E.CompileServerE2ETests.TwoSequentialCompiles_ThroughOneServerProcess_SecondReportsWarmPhaseTimes",
        ["server --idle-timeout"] = "Sharpy.Cli.Tests.Commands.OptionEffectEvidenceTests.Server_IdleTimeout_ExitsAfterIdleSeconds",
        ["server --log-level"] = "Sharpy.Cli.Tests.Commands.OptionEffectEvidenceTests.Server_LogLevel_EnablesTheLogger",
        ["server --log-file"] = "Sharpy.Cli.Tests.Commands.OptionEffectEvidenceTests.Server_LogFile_ReceivesTheLog",
        ["server --verbose"] = "Sharpy.Cli.Tests.Commands.OptionEffectEvidenceTests.Server_Verbose_RaisesTheLogLevel",
    };

    /// <summary>A command-line spelling per refused cell; a new refused cell must add one.</summary>
    internal static readonly IReadOnlyDictionary<string, string> RefusedSpellings = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["build --project-reference"] = "--project-reference lib.csproj",
        ["run --project-reference"] = "--project-reference lib.csproj",
        ["compile@.spy --project-reference"] = "--project-reference lib.csproj",
        ["compile@.spyproj --project-reference"] = "--project-reference lib.csproj",
        ["compile@.spyproj --self-contained"] = "--self-contained",
        ["format --tabs"] = "--tabs",
    };

    static SweepContext RealContext() => new(
        Evidence,
        RefusedSpellings,
        GlobalOptionProbes.Invocations,
        CliTestHarness.Invoke,
        GlobalOptionProbes.Run,
        typeof(CliOptionEffectSweepTests).Assembly);

    // ================================================================
    // The allowlist ratchet
    // ================================================================

    const string AllowlistFileName = "cli-option-effect-allowlist.txt";
    const string Tracker = "#2223";

    internal static string? FindAllowlistPath()
    {
        var current = AppContext.BaseDirectory;
        while (current != null)
        {
            var dir = Path.Combine(current, "src", "Sharpy.Cli.Tests");
            if (Directory.Exists(dir))
            {
                var path = Path.Combine(dir, AllowlistFileName);
                return File.Exists(path) ? path : null;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        return null;
    }

    /// <summary>The allowlist rows: <c>cell key  # #2223 reason</c>.</summary>
    internal static IReadOnlyDictionary<string, string> ReadAllowlist()
    {
        var path = FindAllowlistPath()
            ?? throw new InvalidOperationException($"{AllowlistFileName} is missing beside the tests. Its presence arms the ratchet.");
        var rows = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var split = line.IndexOf(" # ", StringComparison.Ordinal);
            if (split < 0)
                throw new InvalidOperationException($"{AllowlistFileName}: '{line}' must read `cell key  # {Tracker} reason`.");
            var key = line[..split].Trim();
            var comment = line[(split + 3)..].Trim();
            if (!comment.StartsWith(Tracker, StringComparison.Ordinal))
                throw new InvalidOperationException($"{AllowlistFileName}: '{line}' must cite {Tracker} first.");
            if (!rows.TryAdd(key, comment))
                throw new InvalidOperationException($"{AllowlistFileName}: '{key}' is listed twice.");
        }

        return rows;
    }

    // ================================================================
    // The sweep over the real grammar
    // ================================================================

    [Fact]
    public void TheRegistry_IsTotal_OverEveryCommandOfTheRealGrammar()
    {
        var (root, _, _) = CliTestHarness.BuildRoot();

        TotalityFailures(root, CommandOptionEffects.Tables).Should().BeEmpty(
            "every option every command accepts needs a row in CommandOptionEffects saying whether it is honoured, "
            + "refused or ignored, and every row must name an accepted option (#2223)");
    }

    /// <summary>
    /// The totality check and the cell theory are only as strong as the walk. This pins that it reaches
    /// every leaf command — two levels deep included — and that the number of (command, option) pairs is
    /// at least the literal sum declared below, counted by hand from the grammar, not from the walk.
    /// </summary>
    [Fact]
    public void TheWalk_ReachesEveryCommandAndOption()
    {
        var (root, _, _) = CliTestHarness.BuildRoot();
        var pairs = Pairs(root).ToList();
        var paths = pairs.Select(p => p.Path).Distinct().ToList();

        paths.Should().BeEquivalentTo(new[]
        {
            "build", "compile", "run", "project", "emit tokens", "emit ast", "emit csharp", "emit parse",
            "emit diagnostics", "emit hover", "cache clear", "cache info", "explain", "lsp", "repl", "format", "server",
        });
        // Own options: build 6, compile 11, run 8, project 4, emit csharp 6, emit diagnostics 2,
        // emit hover 2, cache clear 1, cache info 1, explain 1, lsp 1, format 5, server 2 = 50;
        // plus 9 globals and --help on each of the 17 leaves = 170.
        pairs.Should().HaveCountGreaterThanOrEqualTo(220);
        pairs.Should().Contain(p => p.Path == "emit csharp" && p.Option.Name == "--reference" && !p.Global);
        pairs.Should().Contain(p => p.Path == "cache clear" && p.Option.Name == "--log-file" && p.Global);
    }

    public static TheoryData<string> CellKeys()
    {
        var (root, _, _) = CliTestHarness.BuildRoot();
        var data = new TheoryData<string>();
        foreach (var cell in Cells(root, CommandOptionEffects.Tables))
            data.Add(cell.Key);
        return data;
    }

    [Theory]
    [MemberData(nameof(CellKeys))]
    public void EveryCell_IsHonouredRefusedOrDocumented(string key)
    {
        var (root, _, _) = CliTestHarness.BuildRoot();
        var cell = Cells(root, CommandOptionEffects.Tables).Single(c => c.Key == key);
        var failure = Check(cell, root, CommandOptionEffects.Tables, RealContext());
        var allowlist = ReadAllowlist();

        if (allowlist.TryGetValue(key, out var reason))
        {
            failure.Should().NotBeNull(
                $"`{key}` is allowlisted ({reason}) but now keeps the contract — stale row: delete it from {AllowlistFileName}");
            return;
        }

        failure.Should().BeNull($"`{key}` breaks the #2223 contract (not allowlisted)");
    }

    [Fact]
    public void EveryAllowlistRow_NamesACellOfTheGrammar()
    {
        var (root, _, _) = CliTestHarness.BuildRoot();
        var keys = Cells(root, CommandOptionEffects.Tables).Select(c => c.Key).ToHashSet(StringComparer.Ordinal);

        ReadAllowlist().Keys.Where(k => !keys.Contains(k)).Should().BeEmpty(
            $"every {AllowlistFileName} row must name a cell the sweep enumerates");
    }

    [Fact]
    public void EveryEvidenceAndSpellingKey_NamesACellOfTheGrammar()
    {
        var (root, _, _) = CliTestHarness.BuildRoot();
        var cells = Cells(root, CommandOptionEffects.Tables).ToList();
        var keys = cells.Select(c => c.Key)
            .Concat(cells.Select(c => $"{c.Path} {c.Option}"))
            .ToHashSet(StringComparer.Ordinal);

        Evidence.Keys.Concat(RefusedSpellings.Keys).Where(k => !keys.Contains(k)).Should().BeEmpty(
            "an evidence or spelling entry must name a cell, or it silently covers nothing");
    }

    // ================================================================
    // Positive controls: a synthetic grammar with one broken cell of each kind
    // ================================================================

    /// <summary>
    /// A test-only root with a <c>probe</c> command whose action does nothing, and a table that is
    /// wrong in every way the sweep must catch: <c>--dropped</c> has no row, <c>--quiet</c> is
    /// ignored but undocumented, <c>--nope</c> is refused but the action never refuses, and
    /// <c>--honest</c> is honoured with evidence that names no test.
    /// </summary>
    static (RootCommand Root, Dictionary<string, CommandEffectTable> Tables, SweepContext Context) SyntheticGrammar()
    {
        var root = new RootCommand("synthetic");
        var probe = new Command("probe", "a command for the sweep's positive controls");
        foreach (var name in new[] { "--dropped", "--quiet", "--nope", "--honest" })
            probe.Options.Add(new Option<bool>(name) { Description = name });
        probe.SetAction(_ => 0);
        root.Subcommands.Add(probe);

        var rows = new Dictionary<string, OptionEffectRow>(StringComparer.Ordinal)
        {
            ["--quiet"] = OptionEffectRow.Uniform(OptionEffect.Ignored, "has no effect"),
            ["--nope"] = OptionEffectRow.Uniform(OptionEffect.Refused, "is refused"),
            ["--honest"] = OptionEffectRow.Uniform(OptionEffect.Honoured, "is honoured"),
            ["--help"] = OptionEffectRow.Uniform(OptionEffect.Honoured, "help"),
        };
        var tables = new Dictionary<string, CommandEffectTable>(StringComparer.Ordinal)
        {
            ["probe"] = new(CommandPerforms.Tooling, Array.Empty<InputKind>(), rows),
        };

        CliInvocation Invoke(string line)
        {
            var parse = root.Parse(System.CommandLine.Parsing.CommandLineParser.SplitCommandLine(line).ToArray());
            var exit = 0;
#pragma warning disable CS0618 // matches production Program.cs usage
            var (output, error) = CliTestHarness.CaptureConsole(() => exit = parse.Invoke());
#pragma warning restore CS0618
            return new CliInvocation(exit, output, error, parse);
        }

        var context = new SweepContext(
            new Dictionary<string, string> { ["probe --honest"] = "Nope.Nope", ["probe --help"] = "Nope.Help" },
            new Dictionary<string, string> { ["probe --nope"] = "--nope" },
            new Dictionary<string, string> { ["probe"] = "probe" },
            Invoke,
            (_, _, _, _) => null,
            typeof(CliOptionEffectSweepTests).Assembly);
        return (root, tables, context);
    }

    [Fact]
    public void TheSweep_ReportsASyntheticDroppedOption()
    {
        var (root, tables, _) = SyntheticGrammar();

        TotalityFailures(root, tables).Should().Contain(f => f.Contains("`probe --dropped` is accepted but has no row"));
    }

    [Fact]
    public void TheSweep_ReportsAnIgnoredOptionWhoseHelpIsSilent()
    {
        var (root, tables, context) = SyntheticGrammar();

        Check(new Cell("probe", null, "--quiet"), root, tables, context).Should().Contain("does not say so");

        // Control: once documented the way CommandOptionEffects.Document documents it, the cell passes.
        var quiet = root.Subcommands.Single().Options.Single(o => o.Name == "--quiet");
        quiet.Description += CommandOptionEffects.DescriptionSuffix(tables["probe"], tables["probe"].Rows["--quiet"]);
        Check(new Cell("probe", null, "--quiet"), root, tables, context).Should().BeNull();
    }

    [Fact]
    public void TheSweep_ReportsARefusedCellWhoseCommandDoesNotRefuse()
    {
        var (root, tables, context) = SyntheticGrammar();

        Check(new Cell("probe", null, "--nope"), root, tables, context).Should()
            .Contain("expected the usage code").And.Contain("not supported");
    }

    [Fact]
    public void TheSweep_ReportsHonouredEvidenceThatNamesNoTest()
    {
        var (root, tables, context) = SyntheticGrammar();

        Check(new Cell("probe", null, "--honest"), root, tables, context).Should().Contain("`Nope.Nope` is not a public");

        // Control: a real test method resolves.
        ResolveEvidence($"{typeof(CliOptionEffectSweepTests).FullName}.{nameof(TheSweep_ReportsASyntheticDroppedOption)}",
            typeof(CliOptionEffectSweepTests).Assembly).Should().BeNull();
    }

    [Fact]
    public void TheSweep_ReportsAnIgnoredGlobalOnACompilingCommand()
    {
        // The globals rule: a compiling command may not document a global as ignored to pass.
        RuleBreak(CommandPerforms.Compiles, "--warn-as-error", isGlobal: true).Should().Contain("honours --warn-as-error");
        RuleBreak(CommandPerforms.Analyses, "--nowarn", isGlobal: true).Should().NotBeNull();
        RuleBreak(CommandPerforms.Lexes, "--max-errors", isGlobal: true).Should().NotBeNull();

        // Controls: what the rule leaves free.
        RuleBreak(CommandPerforms.Analyses, "--metrics-format", isGlobal: true).Should().BeNull();
        RuleBreak(CommandPerforms.Lexes, "--warn-as-error", isGlobal: true).Should().BeNull();
        RuleBreak(CommandPerforms.Tooling, "--log-level", isGlobal: true).Should().BeNull();
    }
}
