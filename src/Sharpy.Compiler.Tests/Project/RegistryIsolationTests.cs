extern alias SharpyRT;

using System.Runtime.Loader;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Semantic.Registry;
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
[Collection("LoadContextRetention")]
public class RegistryIsolationTests : IDisposable
{
    private const string SharedSimpleName = "P54IsoTypes";

    private readonly ITestOutputHelper _output;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"sharpy_p54iso_{Guid.NewGuid():N}");

    /// <summary>
    /// Deterministic base reproductions, keyed by cell id; the value is the observed fingerprint at
    /// base (see <see cref="Fingerprint"/>). Empty since P54 Phase 2 task 2 (#2250): each registry
    /// loads its references into its own load context, so B/Beta (was "refused | SPY0200,SPY0301" —
    /// B's second P54IsoTypes.dll could not enter the default context and B saw A's copy) and
    /// B/Alpha (was "refused | SPY0908(CS0234)" — B resolved A's Alpha from A's copy) reported stale
    /// and drained.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> KnownRed = new Dictionary<string, string>
    {
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

    /// <summary>
    /// One identity for Sharpy.Core (P54 Design Decision 2): a referenced library loads into the
    /// registry's own collectible context, but the Sharpy.Core it was built against is the compiler's
    /// own copy — <c>Load(AssemblyName)</c> defers to the default context for a name the registry did
    /// not load — so a <c>Sharpy.List&lt;int&gt;</c> in the library's signature IS the compiler's
    /// <c>typeof(Sharpy.List&lt;&gt;)</c>. Positive control: the library itself is NOT the default
    /// context's (it is the registry's own copy).
    /// </summary>
    [Fact]
    public void ReferencedLibrary_SharesTheCompilersSharpyCore()
    {
        var path = Path.Combine(_dir, "corelib", "P54CoreIdentity.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var compilation = CSharpCompilation.Create(
            "P54CoreIdentity",
            new[] { CSharpSyntaxTree.ParseText(
                "namespace CoreIdentity { public static class Maker { public static Sharpy.List<int> Make() => new Sharpy.List<int>(); } }") },
            IntegrationTestBase.GetSharedReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var emit = compilation.Emit(path);
        emit.Success.Should().BeTrue(string.Join("\n", emit.Diagnostics));

        var registry = new ModuleRegistry();
        registry.LoadReference(path).Should().BeTrue(string.Join("\n", registry.Diagnostics.GetAll()));
        registry.LoadReference(typeof(SharpyRT::Sharpy.Builtins).Assembly.Location).Should().BeTrue();

        var maker = registry.Visibility.ResolveType("CoreIdentity.Maker");
        maker.Should().NotBeNull("the registry sees its own reference");
        AssemblyLoadContext.GetLoadContext(maker!.Assembly).Should().NotBeSameAs(AssemblyLoadContext.Default,
            "positive control: the library is the registry's own copy, not the process's");
        maker.Assembly.IsCollectible.Should().BeTrue("the registry's context is collectible");

        var returned = maker.GetMethod("Make")!.ReturnType;
        returned.GetGenericTypeDefinition().Should().BeSameAs(typeof(SharpyRT::Sharpy.List<>),
            "the library's Sharpy.Core is the compiler's — one identity");
        registry.LoadedAssemblies.Single(a => a.GetName().Name == "Sharpy.Core")
            .Should().BeSameAs(typeof(SharpyRT::Sharpy.Builtins).Assembly,
                "a Sharpy.Core reference is the framework copy, never a second load");
        registry.ReferenceDiagnostics().Should().BeEmpty();
    }

    /// <summary>
    /// A compile server (or any long-lived host) must not retain one load context per request (P54
    /// Design Decision 3): after 50 compiles in one process, each with its own reference, and a GC,
    /// the contexts holding that reference are bounded. The process-wide Type-keyed caches cache only
    /// process-stable types, so nothing pins a request's context once its registry is unreachable.
    /// In-process twin of the compile server (one CompilerApi per request, as the server's
    /// per-request compile builds a fresh registry).
    /// </summary>
    [Fact]
    public void CompileServer_FiftyRequests_LoadContextsCollect()
    {
        const int Requests = 50;
        const int Ceiling = 2;
        // Instance members and a property, so each compile reaches the process-wide Type-keyed caches
        // (CLR method / property name resolution, member-name surfaces) with the reference's types.
        const string simpleName = "P54Retention";
        var dll = Path.Combine(_dir, "retention", simpleName + ".dll");
        Directory.CreateDirectory(Path.GetDirectoryName(dll)!);
        var library = CSharpCompilation.Create(
            simpleName,
            new[] { CSharpSyntaxTree.ParseText(
                "namespace Retainapp { public class Counter { public int GetValue() => 1; " +
                "public int Count => 2; public static Counter Make() => new Counter(); } }") },
            IntegrationTestBase.GetSharedReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var emitted = library.Emit(dll);
        emitted.Success.Should().BeTrue(string.Join("\n", emitted.Diagnostics));

        // 50 DISTINCT reference paths: each request's reference set is new, so each gets its own
        // context and its own share entry — the shape a server sees when a library is rebuilt.
        var copies = Enumerable.Range(0, Requests + 1).Select(i =>
        {
            var copy = Path.Combine(_dir, "retention", "copy" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), simpleName + ".dll");
            Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            File.Copy(dll, copy);
            return copy;
        }).ToList();

        for (var i = 0; i < Requests; i++)
            CompileOnce(copies[i], i);

        // Deterministic half: no process-wide static cache in the compiler holds a key from a
        // request's reference (a Type, an Assembly, or a tuple carrying one) — such a key would pin
        // that request's context for the life of the process.
        var pinned = StaticCacheKeysFrom(simpleName);
        _output.WriteLine($"static cache keys from {simpleName}: {pinned.Count} [{string.Join(", ", pinned.Take(5))}]");
        pinned.Should().BeEmpty("a static Type-keyed cache must not hold a per-request type (#2250 Design Decision 3)");

        var alive = int.MaxValue;
        for (var attempt = 0; attempt < 20 && alive > Ceiling; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            alive = AssemblyLoadContext.All.Count(c => c != AssemblyLoadContext.Default
                && c.Assemblies.Any(a => a.GetName().Name == simpleName));
        }

        _output.WriteLine($"{alive} of {Requests} request load contexts still alive after GC (ceiling {Ceiling})");
        alive.Should().BeLessThanOrEqualTo(Ceiling,
            "a request's load context must be collectible once its compile is done (#2250 Design Decision 3)");

        // The process-wide share must not grow with dead entries: one more request (a new reference
        // set) replaces / prunes every dead entry, so the share holds only what is alive.
        var entriesBefore = ReferenceLoadContextShareCount();
        CompileOnce(copies[Requests], Requests);
        var entriesAfter = ReferenceLoadContextShareCount();
        _output.WriteLine($"share entries: {entriesBefore} after {Requests} requests + GC, {entriesAfter} after one more request (ceiling {ShareCeiling})");
        entriesAfter.Should().BeLessThanOrEqualTo(ShareCeiling,
            "dead share entries are pruned when a new context is created (#2250)");
    }

    /// <summary>This probe's share entries after a prune: the one new context plus at most one survivor.</summary>
    private const int ShareCeiling = 2;

    private static int ReferenceLoadContextShareCount()
        => Sharpy.Compiler.Discovery.ReferenceLoadContext.SharedKeys.Count(k => k.Contains("P54Retention", StringComparison.Ordinal));

    /// <summary>
    /// The keys of every static <c>ConcurrentDictionary</c> in the compiler assembly that are (or
    /// carry, in a value tuple) a <see cref="Type"/> or <see cref="System.Reflection.Assembly"/> from the
    /// assembly named <paramref name="simpleName"/>.
    /// </summary>
    private static List<string> StaticCacheKeysFrom(string simpleName)
    {
        bool FromIt(object? o) => o switch
        {
            Type t => t.Assembly.GetName().Name == simpleName
                || (t.IsConstructedGenericType && t.GetGenericArguments().Any(FromIt)),
            System.Reflection.Assembly a => a.GetName().Name == simpleName,
            System.Runtime.CompilerServices.ITuple tuple => Enumerable.Range(0, tuple.Length).Any(i => FromIt(tuple[i])),
            _ => false,
        };

        var found = new List<string>();
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
        foreach (var type in typeof(CompilerApi).Assembly.GetTypes())
        {
            if (type.IsGenericTypeDefinition)
                continue;
            foreach (var field in type.GetFields(flags))
            {
                if (!field.FieldType.IsGenericType
                    || field.FieldType.GetGenericTypeDefinition() != typeof(System.Collections.Concurrent.ConcurrentDictionary<,>))
                    continue;
                if (field.GetValue(null) is not System.Collections.IDictionary cache)
                    continue;
                foreach (var key in cache.Keys)
                {
                    if (FromIt(key))
                        found.Add($"{type.Name}.{field.Name}");
                }
            }
        }
        return found;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void CompileOnce(string dll, int i)
    {
        var dir = Path.Combine(_dir, "retention_run", i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(dir);
        var mainPath = Path.Combine(dir, "main.spy");
        var source = "from retainapp import Counter\n\ndef main() -> None:\n    c = Counter.make()\n"
            + "    print(c.get_value())\n    print(c.count)\n";
        File.WriteAllText(mainPath, source);
        var result = new CompilerApi().Compile(source,
            CompilerOptionsFactory.ForCli(references: new[] { dll },
                assemblyName: "Retention" + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                outputAssemblyPath: Path.Combine(dir, "out", "main.dll")),
            mainPath);
        result.Success.Should().BeTrue(string.Join("\n", result.Diagnostics.Select(d => $"{d.Code} {d.Message}")));
    }
}
