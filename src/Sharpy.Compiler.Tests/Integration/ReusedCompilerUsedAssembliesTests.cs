extern alias SharpyRT;

using Sharpy.Compiler.Logging;
using Xunit;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// #2343 — a <see cref="Compiler"/> reused across compilations reports exactly the assemblies THIS
/// compilation used. One <see cref="Compiler"/> holds one module registry for its life; the used set
/// is a per-compilation answer, so each entry point starts by forgetting the previous one's and each
/// result carries a copy taken when its compilation ended.
///
/// <para>Matrix: recorder {Core module, stdlib module function, stdlib module field, stdlib module
/// type, the <c>@lru_cache</c> intrinsic} × entry point {<c>Analyze</c>, <c>Compile</c>,
/// <c>CompileProject</c>}. Each cell compiles an importer, then a program that imports nothing, on
/// the SAME instance: the second answer must equal a fresh instance's answer for that program, and
/// the first result's set must be unchanged by the second compilation. Positive control: the
/// importer's set differs from the fresh plain answer, so the comparison can tell them apart.</para>
/// </summary>
public class ReusedCompilerUsedAssembliesTests : IDisposable
{
    private const string Plain = "def main() -> None:\n    print(1)\n";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"sharpy_2343_{Guid.NewGuid():N}");

    public ReusedCompilerUsedAssembliesTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Best-effort cleanup; an emitted assembly may still be mapped.
        }
    }

    public enum Entry
    {
        Analyze,
        Compile,
        CompileProject,
    }

    public static TheoryData<string, string, Entry> Cells()
    {
        var importers = new (string Recorder, string Source)[]
        {
            ("core module", "import builtins\n\ndef main() -> None:\n    print(builtins.len([1, 2]))\n"),
            ("stdlib module function", "import math\n\ndef main() -> None:\n    print(math.sqrt(4.0))\n"),
            ("stdlib module field", "from math import pi\n\ndef main() -> None:\n    print(pi > 3.0)\n"),
            ("stdlib module type", "from collections import deque\n\ndef main() -> None:\n    d: deque[int] = deque()\n    d.append(1)\n    print(len(d))\n"),
            ("lru_cache intrinsic", "from functools import lru_cache\n\n@lru_cache\ndef f(n: int) -> int:\n    return n * 2\n\ndef main() -> None:\n    print(f(2))\n"),
        };
        var data = new TheoryData<string, string, Entry>();
        foreach (var (recorder, source) in importers)
        {
            foreach (var entry in Enum.GetValues<Entry>())
                data.Add(recorder, source, entry);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cells))]
    public void ReusedCompiler_ReportsOnlyThisCompilationsAssemblies(string recorder, string importer, Entry entry)
    {
        var fresh = Used(NewCompiler("fresh"), Plain, entry, "fresh");

        var reused = NewCompiler("reused");
        var first = Used(reused, importer, entry, "first");
        Assert.True(!first.SetEquals(fresh),
            $"{recorder}/{entry}: positive control — the importer's used set must differ from a plain program's, "
            + $"or the comparison below cannot see a leak; importer [{Show(first)}], plain [{Show(fresh)}]");
        var firstCopy = first.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var second = Used(reused, Plain, entry, "second");

        Assert.True(second.SetEquals(fresh),
            $"{recorder}/{entry}: the reused compiler reported [{Show(second)}] for a program that imports nothing; "
            + $"a fresh compiler reports [{Show(fresh)}] (#2343)");
        Assert.True(first.SetEquals(firstCopy),
            $"{recorder}/{entry}: the first result's used set changed when the instance compiled again: "
            + $"[{Show(firstCopy)}] → [{Show(first)}]");
    }

    private Compiler NewCompiler(string tag)
        => new(CompilerOptionsFactory.ForCli(
            references: References(),
            assemblyName: "Program2343",
            outputAssemblyPath: Path.Combine(_root, tag, "Program2343.dll")), NullLogger.Instance);

    private IReadOnlySet<string> Used(Compiler compiler, string source, Entry entry, string step)
    {
        var path = Path.Combine(_root, $"{step}_{entry}.spy");
        switch (entry)
        {
            case Entry.Analyze:
            {
                var result = compiler.Analyze(source, path);
                Assert.True(result.Success, $"{step}/{entry}: {string.Join("; ", result.Diagnostics.GetAll())}");
                return result.UsedAssemblyPaths;
            }
            case Entry.Compile:
            {
                var result = compiler.Compile(source, path);
                Assert.True(result.Success, $"{step}/{entry}: {string.Join("; ", result.Diagnostics.GetAll())}");
                return result.UsedAssemblyPaths;
            }
            default:
            {
                var config = SyntheticProject.BuildConfig(source, path,
                    CompilerOptionsFactory.ForCli(references: References(), assemblyName: "Program2343",
                        outputAssemblyPath: Path.Combine(_root, $"{step}_project", "Program2343.dll")),
                    NullLogger.Instance);
                var result = compiler.CompileProject(config);
                Assert.True(result.Success, $"{step}/{entry}: {string.Join("; ", result.Diagnostics.GetAll())}");
                return result.UsedAssemblyPaths;
            }
        }
    }

    /// <summary>Sharpy.Core plus the stdlib as the test output carries it (one monolith or the per-module assemblies).</summary>
    private static string[] References()
    {
        var corePath = typeof(SharpyRT::Sharpy.Builtins).Assembly.Location;
        var coreDir = Path.GetDirectoryName(corePath)!;
        var refs = new List<string> { corePath };
        var monolith = Path.Combine(coreDir, "Sharpy.Stdlib.dll");
        if (File.Exists(monolith))
            refs.Add(monolith);
        else
            refs.AddRange(Directory.GetFiles(coreDir, "Sharpy.Stdlib.*.dll").OrderBy(p => p, StringComparer.Ordinal));
        return refs.ToArray();
    }

    private static string Show(IEnumerable<string> paths)
        => string.Join(", ", paths.Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal));
}
