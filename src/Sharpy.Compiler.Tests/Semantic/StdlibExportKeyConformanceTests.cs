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

    [Fact]
    public void EveryFieldKey_IsSharpySpelled()
    {
        var modulesWithFields = new[] { "math", "string", "calendar", "sys", "os", "socket" };
        var violations = new List<string>();

        foreach (var moduleName in modulesWithFields)
        {
            var fields = _discovery.GetModuleFields(moduleName);
            foreach (var (name, _, _, clrName) in fields)
            {
                var expectedSharpyName = NameMangler.ToSharpyName(
                    clrName ?? name, ReverseNameContext.Field);

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
