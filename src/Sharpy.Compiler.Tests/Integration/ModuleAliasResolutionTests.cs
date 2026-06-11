extern alias SharpyStdlib;

using System.Reflection;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Logging;
using Sharpy.TestInfrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Drift-proof guard for #891: every <c>[SharpyModule]</c> class in Sharpy.Stdlib must be
/// resolvable to its real C# class name through discovery — the same data the emitter uses
/// when emitting <c>using &lt;alias&gt; = global::Sharpy.&lt;ClassName&gt;;</c>. This catches the
/// Module-suffix mismatch (e.g. <c>email</c> → <c>EmailModule</c>, not <c>Email</c>) for every
/// current and future module without enumerating them by hand.
/// </summary>
public class ModuleAliasResolutionTests
{
    private readonly ITestOutputHelper _output;
    private readonly ICompilerLogger _logger;

    public ModuleAliasResolutionTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = new TestHelpers.OutputTestLogger(output);
    }

    /// <summary>
    /// Maps each <c>[SharpyModule]("name")</c> attribute value to the declaring type.
    /// </summary>
    private static Dictionary<string, Type> GetSharpyModuleTypes(Assembly assembly)
    {
        var result = new Dictionary<string, Type>();
        foreach (var type in assembly.GetExportedTypes())
        {
            var attr = type.GetCustomAttributes()
                .FirstOrDefault(a => a.GetType().Name == "SharpyModuleAttribute");
            if (attr != null)
            {
                var moduleName = (string)attr.GetType()
                    .GetProperty("ModuleName")!
                    .GetValue(attr)!;
                result[moduleName] = type;
            }
        }

        return result;
    }

    /// <summary>
    /// Builds a discovery instance backed by a throwaway cache directory so the index is
    /// always rebuilt fresh from reflection (no dependency on any on-disk cache state).
    /// </summary>
    private CachedModuleDiscovery CreateFreshDiscovery()
    {
        var tempCacheDir = Path.Combine(Path.GetTempPath(),
            "sharpy-alias-test-" + Guid.NewGuid().ToString("N"));
        var cache = new OverloadIndexCache(tempCacheDir, _logger);
        return new CachedModuleDiscovery(cache, _logger);
    }

    [Fact]
    public void EveryDiscoveredModule_ResolvesToItsRealClassName()
    {
        var stdlibAssembly = typeof(SharpyStdlib::Sharpy.Textwrap).Assembly;
        var moduleTypes = GetSharpyModuleTypes(stdlibAssembly);
        Assert.NotEmpty(moduleTypes);

        var discovery = CreateFreshDiscovery();
        discovery.LoadAssembly(stdlibAssembly);

        var mismatches = new List<string>();
        var resolvedCount = 0;

        foreach (var (moduleName, type) in moduleTypes.OrderBy(kv => kv.Key))
        {
            var resolved = discovery.GetModuleCSharpClassName(moduleName);

            // Modules with no module-level functions or const fields are not registered in
            // the overload index (the emitter falls back to PascalCase for those). Only
            // assert correctness for modules that discovery actually surfaces.
            if (resolved == null)
            {
                _output.WriteLine($"  (skipped, not in overload index) [{moduleName}] -> {type.Name}");
                continue;
            }

            resolvedCount++;
            _output.WriteLine($"  [{moduleName}] -> {resolved} (actual: {type.Name})");

            if (!string.Equals(resolved, type.Name, StringComparison.Ordinal))
                mismatches.Add($"module '{moduleName}': discovered '{resolved}' but real class is '{type.Name}'");
        }

        Assert.True(resolvedCount > 0, "Expected at least one discovered module with a resolvable class name");
        Assert.True(mismatches.Count == 0,
            "Module alias class names drifted from their real [SharpyModule] classes:\n  " +
            string.Join("\n  ", mismatches));
    }

    [Theory]
    [InlineData("email", "EmailModule")]
    [InlineData("ipaddress", "IpaddressModule")]
    public void ModuleSuffixedClasses_ResolveWithSuffix(string moduleName, string expectedClassName)
    {
        // Regression guard for the specific #891 repro: 'email'/'ipaddress' previously
        // resolved to 'Sharpy.Email'/'Sharpy.Ipaddress' (CS0234) instead of the real
        // 'EmailModule'/'IpaddressModule' classes.
        var stdlibAssembly = typeof(SharpyStdlib::Sharpy.Textwrap).Assembly;
        var discovery = CreateFreshDiscovery();
        discovery.LoadAssembly(stdlibAssembly);

        var resolved = discovery.GetModuleCSharpClassName(moduleName);
        Assert.Equal(expectedClassName, resolved);
    }
}
