using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Diagnostics;
using Sharpy.TestInfrastructure.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// P54 (#2250) cell 5, the in-process twin of the compile-server request sequence: a compile is a
/// pure function of its inputs, so two compiles in ONE process that reference two different files
/// with the SAME assembly simple name each see the file they were given. Two
/// <c>P54IsoTypes.dll</c> are built into two temp directories — copy A declares <c>Isoapp.Alpha</c>,
/// copy B declares <c>Isoapp.Beta</c> — and two <see cref="CompilerApi"/> instances ("registry A",
/// "registry B") compile against them in a fixed order.
/// </summary>
/// <remarks>
/// The simple name <c>P54IsoTypes</c> is used by no other test, so nothing else in the test host
/// loads it first and the run order below is the whole history of the default load context for that
/// name: A, then B. Cells (in run order):
/// <list type="bullet">
/// <item><c>A/Alpha</c> — registry A imports its own member: compiles (positive control).</item>
/// <item><c>B/Beta</c> — registry B imports its own member: must compile against B's file.</item>
/// <item><c>A/Beta</c> — registry A, after B loaded its copy, imports B's member: refused by a
/// diagnostic (a reference another registry loaded is invisible; the plan's inert-fix control).</item>
/// <item><c>B/Alpha</c> — registry B imports A's member: refused by a diagnostic (B never sees A's
/// copy; the "compiled against the first copy" discriminator).</item>
/// </list>
/// A cell red at base is a literal <see cref="KnownRed"/> row holding the measured base fingerprint:
/// the test asserts the defect still reproduces, so the Phase 2 cure fails the row as stale.
/// </remarks>
public class RegistryIsolationTests : IDisposable
{
    private const string SharedSimpleName = "P54IsoTypes";

    private readonly ITestOutputHelper _output;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"sharpy_p54iso_{Guid.NewGuid():N}");

    /// <summary>
    /// Deterministic base reproductions, keyed by cell id; the value is the observed fingerprint at
    /// base (see <see cref="Fingerprint"/>). Each row drains in Phase 2 task 2 (#2250).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> KnownRed = new Dictionary<string, string>
    {
        // #2250: B's load of a second P54IsoTypes.dll cannot enter the default load context (the
        // registry's SPY0306 arm, which MissingReferenceDiagnostics filters out — plan cell 5,
        // fa7e8aa63), so B's semantic pass sees A's copy and refuses its own member — a valid
        // program refused. Reproduced 1/1 @ 486c9a88a.
        ["B/Beta"] = "refused | SPY0200,SPY0301",
        // #2250: B's semantic pass resolves A's Alpha from A's loaded copy; the Roslyn step compiles
        // against B's file, which has no Alpha — an ICE instead of a refusal. Reproduced 1/1 @ 486c9a88a.
        ["B/Alpha"] = "refused | SPY0908(CS0234)",
    };

    public RegistryIsolationTests(ITestOutputHelper output)
    {
        _output = output;
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        { Directory.Delete(_dir, recursive: true); }
        catch { /* best effort */ }
    }

    private string BuildCopy(string label, string className, int value)
    {
        var directory = Path.Combine(_dir, label);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, SharedSimpleName + ".dll");
        var compilation = CSharpCompilation.Create(
            SharedSimpleName,
            new[] { CSharpSyntaxTree.ParseText(
                $"namespace Isoapp {{ public class {className} {{ public static int Value() => {value}; }} }}") },
            IntegrationTestBase.GetSharedReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var emit = compilation.Emit(path);
        emit.Success.Should().BeTrue(string.Join("\n", emit.Diagnostics));
        return path;
    }

    [Fact]
    public void TwoRegistries_SameNamedAssemblyFromTwoPaths_EachSeesItsOwn()
    {
        var dlls = new Dictionary<string, string>
        {
            ["A"] = BuildCopy("a", "Alpha", 1),
            ["B"] = BuildCopy("b", "Beta", 2),
        };
        var registries = new Dictionary<string, CompilerApi>
        {
            ["A"] = new CompilerApi(),
            ["B"] = new CompilerApi(),
        };

        var cells = new (string Id, string Registry, string Import, bool Visible)[]
        {
            ("A/Alpha", "A", "Alpha", true),
            ("B/Beta", "B", "Beta", true),
            ("A/Beta", "A", "Beta", false),
            ("B/Alpha", "B", "Alpha", false),
        };

        var failures = new List<string>();
        foreach (var cell in cells)
        {
            var own = dlls[cell.Registry];
            var dir = Path.Combine(_dir, "run_" + cell.Id.Replace('/', '_'));
            Directory.CreateDirectory(dir);
            var mainPath = Path.Combine(dir, "main.spy");
            var source = $"from isoapp import {cell.Import}\n\ndef main() -> None:\n    print({cell.Import}.value())\n";
            File.WriteAllText(mainPath, source);

            var result = registries[cell.Registry].Compile(source,
                CompilerOptionsFactory.ForCli(references: new[] { own },
                    assemblyName: "IsoRun_" + cell.Id.Replace('/', '_'),
                    outputAssemblyPath: Path.Combine(dir, "out", "main.dll")),
                mainPath);

            var fingerprint = Fingerprint(result);
            var report = string.Join("\n", result.Diagnostics.Select(d => $"  {d.Code} {d.Line}:{d.Column} {d.Message}"));
            _output.WriteLine($"[{cell.Id}] {fingerprint}\n{report}");

            if (KnownRed.TryGetValue(cell.Id, out var recorded))
            {
                if (fingerprint != recorded)
                    failures.Add($"[{cell.Id}] KnownRed row is stale — drain it (#2250): recorded '{recorded}', observed '{fingerprint}'\n{report}");
                continue;
            }

            var errors = result.Diagnostics.Where(d => d.IsError).ToList();
            if (cell.Visible)
            {
                // The Roslyn step compiles against THIS registry's file only, so success means the
                // member resolved from that file. (UsedAssemblyPaths is not read: the CLR-namespace
                // import arms do not record it — #2313's cell, not this one.)
                if (!result.Success || errors.Count > 0)
                    failures.Add($"[{cell.Id}] must compile against its own reference; observed '{fingerprint}'\n{report}");
            }
            else if (result.Success
                || errors.Count == 0
                || errors.Any(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError))
            {
                failures.Add($"[{cell.Id}] must be refused by a diagnostic (the member is in the OTHER registry's copy); observed '{fingerprint}'\n{report}");
            }
        }

        failures.Should().BeEmpty(string.Join("\n\n", failures));
    }

    /// <summary>
    /// <c>ok|refused | sorted distinct diagnostic codes</c>, a SPY0908 spelled with the C# error it
    /// wraps — a literal string so a KnownRed row holds the base observation verbatim.
    /// </summary>
    private static string Fingerprint(CompileResult result)
    {
        var codes = result.Diagnostics
            .Select(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError
                ? $"{d.Code}({Regex.Match(d.Message, @"\bCS\d{4}\b").Value})"
                : d.Code)
            .Distinct()
            .OrderBy(c => c, StringComparer.Ordinal);
        var joined = string.Join(",", codes);
        return $"{(result.Success ? "ok" : "refused")} | {(joined.Length == 0 ? "-" : joined)}";
    }
}
