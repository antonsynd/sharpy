using System.Diagnostics;
using System.Globalization;
using Sharpy.BlastRadius.Probes;

namespace Sharpy.BlastRadius;

/// <summary>
/// <c>sharpy-blast</c>: how many existing programs would a proposed language rule touch?
/// See <c>docs/process/blast-radius.md</c>.
/// </summary>
internal static class Program
{
    private const string Usage = """
        usage:
          sharpy-blast list
          sharpy-blast run <rule> [--repo DIR] [--area NAME=PATH]... [--md FILE] [--json FILE] [--examples N]

        run   analyses every program of the corpus and reports, site by site, what the rule would do.
              With no --area, the corpus is every .spy file and specification code block in the
              repository. PATH is a file or a directory, relative to --repo (default: the current
              directory). The Markdown report goes to --md, or to standard output.
        """;

    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            Console.WriteLine(Usage);
            return args.Length == 0 ? 2 : 0;
        }

        switch (args[0])
        {
            case "list":
                foreach (var probe in ProbeRegistry.All)
                    Console.WriteLine($"{probe.Rule}\t{probe.Title}");
                return 0;
            case "run":
                return Run(args.Skip(1).ToArray());
            default:
                Console.Error.WriteLine($"unknown command '{args[0]}'\n{Usage}");
                return 2;
        }
    }

    private static int Run(string[] args)
    {
        string? rule = null, md = null, json = null;
        var repo = Directory.GetCurrentDirectory();
        var areas = new List<CorpusArea>();
        var examples = 5;

        for (var i = 0; i < args.Length; i++)
        {
            string Value() => ++i < args.Length ? args[i] : throw new ArgumentException($"{args[i - 1]} needs a value");
            try
            {
                switch (args[i])
                {
                    case "--repo":
                        repo = Path.GetFullPath(Value());
                        break;
                    case "--md":
                        md = Value();
                        break;
                    case "--json":
                        json = Value();
                        break;
                    case "--examples":
                        examples = int.Parse(Value(), CultureInfo.InvariantCulture);
                        if (examples < 0)
                            throw new ArgumentException("--examples takes a count, zero or more");
                        break;
                    case "--area":
                        var pair = Value().Split('=', 2);
                        if (pair.Length != 2)
                            throw new ArgumentException("--area takes NAME=PATH");
                        areas.Add(new CorpusArea(pair[0], pair[1]));
                        break;
                    default:
                        if (args[i].StartsWith('-') || rule != null)
                            throw new ArgumentException($"unexpected argument '{args[i]}'");
                        rule = args[i];
                        break;
                }
            }
            catch (Exception e) when (e is ArgumentException or FormatException or OverflowException)
            {
                Console.Error.WriteLine($"{e.Message}\n{Usage}");
                return 2;
            }
        }

        if (rule == null || ProbeRegistry.Find(rule) is not { } probeToRun)
        {
            Console.Error.WriteLine(rule == null
                ? $"no rule given\n{Usage}"
                : $"no probe for '{rule}'. Known: {string.Join(", ", ProbeRegistry.All.Select(p => p.Rule))}");
            return 2;
        }

        if (areas.Count == 0)
            areas.AddRange(Corpus.DefaultAreas);

        var runner = new ProbeRunner(probeToRun);
        var outcomes = new List<ItemOutcome>();
        var clock = Stopwatch.StartNew();
        foreach (var area in areas)
        {
            var root = Path.GetFullPath(Path.Combine(repo, area.RelativePath));
            if (!File.Exists(root) && !Directory.Exists(root))
            {
                Console.Error.WriteLine($"area '{area.Name}': nothing at {root}");
                return 2;
            }

            // Analysis is nearly all of the run, and one item does not depend on another.
            var items = Corpus.Enumerate(area.Name, root, repo).ToList();
            var results = new ItemOutcome[items.Count];
            Parallel.For(0, items.Count, i => results[i] = runner.Run(items[i]));
            outcomes.AddRange(results);
            Console.Error.WriteLine($"{area.Name}: {items.Count} programs, {(int)clock.Elapsed.TotalSeconds}s");
        }

        var report = new BlastReport
        {
            Probe = probeToRun,
            Commit = Commit(repo),
            AreaOrder = areas.Select(a => a.Name).Distinct().ToList(),
            Outcomes = outcomes,
        };

        if (json != null)
            File.WriteAllText(json, report.ToJson());
        var markdown = report.ToMarkdown(examples);
        if (md != null)
            File.WriteAllText(md, markdown);
        else
            Console.Write(markdown);
        return 0;
    }

    private static string Commit(string repo)
    {
        try
        {
            var sha = Git(repo, "rev-parse --short HEAD");
            if (sha is null or "")
                return "unknown";
            // The corpus, the compiler and the probe are all in the working tree: a report from a
            // tree with changes is not reproducible at the commit, and says so.
            return Git(repo, "status --porcelain") is "" ? sha : sha + "-dirty";
        }
        catch (Exception)
        {
            return "unknown";
        }
    }

    /// <summary>The trimmed output of a git command, or null when it fails.</summary>
    private static string? Git(string repo, string arguments)
    {
        var info = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
        };
        using var git = Process.Start(info)!;
        var output = git.StandardOutput.ReadToEnd().Trim();
        git.WaitForExit();
        return git.ExitCode == 0 ? output : null;
    }
}
