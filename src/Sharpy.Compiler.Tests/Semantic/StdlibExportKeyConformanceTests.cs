using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Shared;
using Sharpy.TestInfrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Semantic;

/// <summary>
/// Conformance guard for #1540: every stdlib module field/property export key must be
/// the Sharpy spelling (snake_case), not the CLR PascalCase. Before the fix, discovery
/// stored export keys verbatim from CLR reflection, so <c>math.Pi</c> was the key and
/// completion showed "Pi" instead of "pi". The re-keying in
/// <see cref="OverloadIndexBuilder"/> applies <see cref="NameMangler.ToSharpyName"/> with
/// <see cref="ReverseNameContext.Field"/>, and this guard verifies the result matches
/// what the checker would resolve (no PascalCase fallback needed).
/// </summary>
public class StdlibExportKeyConformanceTests : System.IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testCacheDir;
    private readonly CachedModuleDiscovery _discovery;

    public StdlibExportKeyConformanceTests(ITestOutputHelper output)
    {
        _output = output;
        _testCacheDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "sharpy-test-cache", System.Guid.NewGuid().ToString());
        var cache = new OverloadIndexCache(_testCacheDir);
        _discovery = new CachedModuleDiscovery(cache);
        _discovery.LoadAssembly(SharpyCoreReference.Assembly);
        _discovery.LoadAssembly(SharpyStdlibReference.Assembly);
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(_testCacheDir))
        {
            try
            { System.IO.Directory.Delete(_testCacheDir, recursive: true); }
            catch { /* ignore cleanup errors */ }
        }
    }

    /// <summary>
    /// Modules that carried fields when this guard was written. The dynamic enumeration below
    /// must cover at least these — otherwise a discovery regression that loads nothing would
    /// make every per-module loop pass vacuously.
    /// </summary>
    private static readonly string[] SentinelModules =
        { "math", "string", "calendar", "sys", "os", "socket" };

    private List<string> LoadedModules()
    {
        var modules = _discovery.GetLoadedModules().ToList();
        modules.Should().Contain(SentinelModules,
            "the dynamic module enumeration must cover at least the modules this guard "
            + "originally pinned by hand — an empty or partial registry would pass vacuously");
        return modules;
    }

    /// <summary>
    /// The one documented exemption from field-key snake_casing, mirrored from
    /// <c>OverloadIndexBuilder</c> (fields discovery): when the module also exports a TYPE with
    /// the field's CLR name, the collision is deliberate and the field keeps the CLR spelling —
    /// e.g. <c>sqlite3.Row</c> is both a type and the value assigned to <c>row_factory</c>, and
    /// Python users spell both with the capital R (documented in ModuleExports.cs). The exemption
    /// is keyed on the module's type-name set, an independent fact — a regression that stores
    /// <c>math.Pi</c> as a key is not excused by it unless math also grew a type named Pi.
    /// </summary>
    private System.Collections.Generic.HashSet<string> ModuleTypeNames(string moduleName)
        => _discovery.GetModuleTypes(moduleName)
            .Select(t => t.Name)
            .ToHashSet(System.StringComparer.Ordinal);

    [Fact]
    public void EveryFieldKey_IsSharpySpelled()
    {
        var violations = new List<string>();

        foreach (var moduleName in LoadedModules())
        {
            var typeNames = ModuleTypeNames(moduleName);
            var fields = _discovery.GetModuleFields(moduleName);
            foreach (var (name, _, _, clrName) in fields)
            {
                var expectedSharpyName = typeNames.Contains(clrName ?? name)
                    ? clrName ?? name
                    : NameMangler.ToSharpyName(clrName ?? name, ReverseNameContext.Field);

                if (name != expectedSharpyName)
                {
                    violations.Add(
                        $"{moduleName}.{name} — expected key '{expectedSharpyName}' "
                        + $"(CLR: '{clrName ?? name}')");
                }

                if (clrName != null && clrName == name &&
                    clrName != expectedSharpyName)
                {
                    violations.Add(
                        $"{moduleName}.{name} — ClrName equals key but differs from "
                        + $"expected Sharpy spelling '{expectedSharpyName}'");
                }

                _output.WriteLine($"{moduleName}.{name} (CLR: {clrName ?? "<same>"})");
            }
        }

        violations.Should().BeEmpty(
            "every field export key must be the Sharpy spelling, not the CLR PascalCase. "
            + "The re-keying in OverloadIndexBuilder applies ToSharpyName with "
            + "ReverseNameContext.Field (#1540).\nViolations:\n  "
            + string.Join("\n  ", violations));
    }

    /// <summary>
    /// The checker-facing invariant. <c>CheckMemberAccessCore</c> resolves a module member by RAW
    /// <c>Exports</c> lookup first and only tries a <see cref="NameMangler.ToPascalCase"/> fallback
    /// for .NET modules when that misses (TypeChecker.Expressions.Access.cs). For a stdlib key the
    /// fallback must never be the resolving route: the Sharpy spelling derived from the CLR name
    /// must itself be a key (raw lookup succeeds), and no field key may coexist with its PascalCase
    /// twin in the same module's export universe (a dual spelling means the CLR name leaked into
    /// Exports alongside the Sharpy one — the pre-#1540 state). The key universe here is exactly
    /// the union <c>TryResolveNetModule</c> copies into <c>ModuleInfo.ExportedSymbols</c>, which
    /// becomes the <c>ModuleSymbol.Exports</c> dictionary the checker reads: functions, types,
    /// then fields.
    /// </summary>
    [Fact]
    public void EveryFieldExport_ResolvesViaRawLookup_NeverViaPascalCaseFallback()
    {
        var violations = new List<string>();
        var pascalDifferingKeys = 0;

        foreach (var moduleName in LoadedModules())
        {
            var typeNames = ModuleTypeNames(moduleName);
            var exportKeys = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var function in _discovery.GetModuleFunctions(moduleName))
                exportKeys.Add(function.Name);
            foreach (var type in _discovery.GetModuleTypes(moduleName))
                exportKeys.Add(type.Name);
            foreach (var (fieldName, _, _, _) in _discovery.GetModuleFields(moduleName))
                exportKeys.Add(fieldName);

            foreach (var (fieldName, _, _, clrName) in _discovery.GetModuleFields(moduleName))
            {
                var sharpySpelling = typeNames.Contains(clrName ?? fieldName)
                    ? clrName ?? fieldName
                    : NameMangler.ToSharpyName(clrName ?? fieldName, ReverseNameContext.Field);

                // The raw lookup the checker performs first must succeed for the spelling a
                // Sharpy user writes. Before #1540 this failed ('pi' was not a key; only 'Pi'
                // was) and the PascalCase fallback silently became the resolving route.
                if (!exportKeys.Contains(sharpySpelling))
                {
                    violations.Add(
                        $"{moduleName}.{sharpySpelling} — raw Exports lookup would MISS; "
                        + $"only the fallback could resolve it (keys have '{fieldName}')");
                }

                var pascalTwin = NameMangler.ToPascalCase(sharpySpelling);
                if (pascalTwin != sharpySpelling)
                {
                    pascalDifferingKeys++;

                    if (exportKeys.Contains(pascalTwin))
                    {
                        violations.Add(
                            $"{moduleName}.{sharpySpelling} — PascalCase twin '{pascalTwin}' is "
                            + "ALSO an export key (dual spelling: the CLR name leaked into Exports)");
                    }
                }
            }
        }

        pascalDifferingKeys.Should().BeGreaterThan(0,
            "the fallback arm must actually be exercised — math.pi alone differs from 'Pi', "
            + "so zero here means the enumeration went vacuous, not that the invariant holds");

        violations.Should().BeEmpty(
            "the PascalCase fallback in CheckMemberAccessCore must never be the resolving route "
            + "for a stdlib field key (#1540).\nViolations:\n  "
            + string.Join("\n  ", violations));
    }

    [Fact]
    public void MathModule_HasSnakeCaseConstants()
    {
        var fields = _discovery.GetModuleFields("math");
        var keys = fields.Select(f => f.Name).ToList();

        keys.Should().Contain("pi", "math.pi must be the Sharpy key, not math.Pi");
        keys.Should().Contain("e", "math.e must be the Sharpy key, not math.E");
        keys.Should().Contain("inf", "math.inf must be the Sharpy key, not math.Inf");
        keys.Should().Contain("nan", "math.nan must be the Sharpy key, not math.Nan");
        keys.Should().NotContain("Pi", "PascalCase 'Pi' must not be an export key");
        keys.Should().NotContain("E", "PascalCase 'E' must not be an export key");
    }

    [Fact]
    public void StringModule_HasSnakeCaseConstants()
    {
        var fields = _discovery.GetModuleFields("string");
        var keys = fields.Select(f => f.Name).ToList();

        keys.Should().Contain("ascii_lowercase",
            "string.ascii_lowercase must be the Sharpy key, not string.AsciiLowercase");
        keys.Should().NotContain("AsciiLowercase",
            "PascalCase 'AsciiLowercase' must not be an export key");
    }

    [Fact]
    public void ClrName_IsPreserved()
    {
        var fields = _discovery.GetModuleFields("math");
        var pi = fields.FirstOrDefault(f => f.Name == "pi");

        pi.ClrName.Should().Be("Pi",
            "the CLR name must be preserved for code generation to emit the correct member access");
    }
}
