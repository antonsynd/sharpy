extern alias SharpyRT;

using System.Runtime.InteropServices;
using System.Runtime.Loader;
using FluentAssertions;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Discovery;
using Sharpy.Compiler.Semantic.Registry;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The framework a compilation sees is the SHARED FRAMEWORK (the assemblies <c>Microsoft.NETCore.App</c>
/// ships, in the runtime directory CoreLib was loaded from) plus <c>Sharpy.Core</c> — never the host's
/// own application assemblies (#2250, P54 final-verify F3). The contract: a compile is a function of its
/// inputs, so sharpyc, the LSP and a test host — whose trusted-platform lists carry different
/// deps.json packages — must see one framework.
/// </summary>
/// <remarks>
/// <para>At effba7776 the framework set was every trusted-platform assembly NAMED <c>System</c>/<c>System.*</c>/
/// <c>Microsoft.*</c>, so this test host saw Microsoft.CodeAnalysis, Microsoft.Extensions.Primitives,
/// System.Reactive and Microsoft.TestPlatform.* as framework, sharpyc saw System.CommandLine, and the LSP
/// did not. <c>from microsoft.extensions.primitives import StringValues</c> was SPY0300 at base
/// 486c9a88a and compiled, then died with FileNotFoundException at run, at effba7776 (measured, built
/// sharpyc); <c>system.command_line</c> and <c>microsoft.code_analysis.c_sharp</c> compiled then crashed
/// at both (measured: exit 134, FileNotFoundException).</para>
/// <para>Every negative row below is in THIS host's trusted-platform list (asserted, so the cell
/// discriminates a name filter from a directory filter) except System.CommandLine, which is not a
/// dependency of this test project — its discriminating twin is the sharpyc E2E in
/// <c>Sharpy.Cli.Tests.E2E.HostAssemblyVisibilityE2ETests</c>.</para>
/// </remarks>
[Trait("Category", "Conformance")]
public class FrameworkVisibilityTests : IDisposable
{
    /// <summary>Host packages present in this test host's trusted-platform list — each must NOT be framework.</summary>
    private static readonly string[] HostPackagesInThisHost =
    {
        "Microsoft.CodeAnalysis",
        "Microsoft.CodeAnalysis.CSharp",
        "Microsoft.Extensions.Primitives",
        "Microsoft.Extensions.FileSystemGlobbing",
        "System.Reactive",
        "System.Composition.Hosting",
        "Microsoft.TestPlatform.CoreUtilities",
        "xunit.core",
    };

    /// <summary>Shared-framework assemblies — each MUST be framework (positive control).</summary>
    private static readonly string[] SharedFrameworkRows =
    {
        "System.Private.CoreLib",
        "System.Runtime",
        "System.Collections",
        "System.Text.RegularExpressions",
        "Microsoft.Win32.Primitives",
        "netstandard",
    };

    private readonly ITestOutputHelper _output;
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"sharpy_p54fw_{Guid.NewGuid():N}");

    public FrameworkVisibilityTests(ITestOutputHelper output)
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

    private static Dictionary<string, string> TrustedPlatformPaths()
    {
        var byName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in ((AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string) ?? "")
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            byName.TryAdd(Path.GetFileNameWithoutExtension(path), path);
        }
        return byName;
    }

    [Fact]
    public void FrameworkSet_IsTheSharedFrameworkPlusSharpyCore()
    {
        var runtimeDirectory = RuntimeEnvironment.GetRuntimeDirectory();
        var tpa = TrustedPlatformPaths();
        var framework = AssemblyVisibility.FrameworkOnly.Assemblies
            .Select(a => AssemblyVisibility.SafeSimpleName(a) ?? "<unnamed>")
            .ToList();
        _output.WriteLine($"runtime directory: {runtimeDirectory}; framework set: {framework.Count} assemblies; TPA: {tpa.Count}");

        // Discrimination precondition: the host packages ARE trusted-platform here, so a name filter
        // over the trusted-platform list would admit them.
        var missing = HostPackagesInThisHost.Where(n => !tpa.ContainsKey(n)).ToList();
        missing.Should().BeEmpty("INCONCLUSIVE: these host packages are not in this test host's trusted-platform list, so their rows discriminate nothing");

        var failures = new List<string>();

        // Subset: every framework assembly is a shared-framework file, or Sharpy.Core.
        foreach (var name in framework)
        {
            if (name != "Sharpy.Core" && !File.Exists(Path.Combine(runtimeDirectory, name + ".dll")))
                failures.Add($"[subset] '{name}' is in the framework set but is not a shared-framework assembly");
        }

        foreach (var name in SharedFrameworkRows)
        {
            if (!framework.Contains(name))
                failures.Add($"[positive] shared-framework '{name}' is missing from the framework set");
        }
        if (!framework.Contains("Sharpy.Core"))
            failures.Add("[positive] Sharpy.Core is missing from the framework set");

        foreach (var name in HostPackagesInThisHost.Append("System.CommandLine").Append("Sharpy.Compiler"))
        {
            if (framework.Contains(name))
                failures.Add($"[negative] host assembly '{name}' is in the framework set");
            if (AssemblyVisibility.FrameworkOnly.FindAssembly(name) != null)
                failures.Add($"[negative] FrameworkOnly.FindAssembly('{name}') answers the host's copy");
        }

        foreach (var ns in new[] { "Microsoft.CodeAnalysis", "Microsoft.Extensions.Primitives", "System.Reactive.Linq", "System.CommandLine", "Xunit" })
        {
            if (AssemblyVisibility.FrameworkOnly.NamespaceExists(ns))
                failures.Add($"[negative] host namespace '{ns}' is visible with no reference");
        }
        foreach (var ns in new[] { "System.Collections.Generic", "System.Text", "Microsoft.Win32", "Sharpy" })
        {
            if (!AssemblyVisibility.FrameworkOnly.NamespaceExists(ns))
                failures.Add($"[positive] framework namespace '{ns}' is not visible");
        }

        failures.Should().BeEmpty(string.Join("\n", failures));
    }

    /// <summary>
    /// An import of a host package with no reference is refused at import — never a compile that dies
    /// at run with FileNotFoundException — with the SAME refusal an import of a namespace that exists
    /// nowhere gets under that prefix: SPY0300 for a non-<c>system</c> prefix (the base answer for
    /// <c>microsoft.extensions.primitives</c>), SPY0301 for <c>system.*</c>, whose arm maps any name
    /// without an existence check (<c>from system.no_such_namespace import Foo</c> is SPY0301 at base
    /// 486c9a88a, measured — the "control.nowhere" rows). Positive control: a shared-framework namespace
    /// import compiles in the same registry shape.
    /// </summary>
    [Theory]
    [InlineData("extensions_primitives", "from microsoft.extensions.primitives import StringValues\n\ndef main() -> None:\n    s = StringValues(\"abc\")\n    print(s.count)\n", "SPY0300")]
    [InlineData("code_analysis", "from microsoft.code_analysis.c_sharp import CSharpSyntaxTree\n\ndef main() -> None:\n    t = CSharpSyntaxTree.parse_text(\"class C {}\")\n    print(t.length)\n", "SPY0300")]
    [InlineData("control.nowhere_microsoft", "from microsoft.no_such_namespace import Foo\n\ndef main() -> None:\n    print(Foo())\n", "SPY0300")]
    [InlineData("reactive", "from system.reactive.linq import Observable\n\ndef main() -> None:\n    x = Observable.range(1, 2)\n    print(x is not None)\n", "SPY0301")]
    [InlineData("command_line", "from system.command_line import RootCommand\n\ndef main() -> None:\n    r = RootCommand(\"hello\")\n    print(r.description)\n", "SPY0301")]
    [InlineData("control.nowhere_system", "from system.no_such_namespace import Foo\n\ndef main() -> None:\n    print(Foo())\n", "SPY0301")]
    [InlineData("control.system_text", "from system.text import StringBuilder\n\ndef main() -> None:\n    sb = StringBuilder()\n    sb.append(\"x\")\n    print(sb.to_string())\n", null)]
    public void HostPackageImport_WithoutReference_IsRefusedAtImport(string cell, string source, string? refusal)
    {
        var dir = Path.Combine(_dir, cell);
        Directory.CreateDirectory(dir);
        var mainPath = Path.Combine(dir, "main.spy");
        File.WriteAllText(mainPath, source);

        // sharpyc's shape: Sharpy.Core and Sharpy.Stdlib are the default references.
        var defaultReferences = new[] { "Sharpy.Core.dll", "Sharpy.Stdlib.dll" }
            .Select(n => Path.Combine(AppContext.BaseDirectory, n))
            .ToArray();
        var result = new CompilerApi().Compile(source,
            CompilerOptionsFactory.ForCli(references: defaultReferences,
                assemblyName: "Fw_" + cell.Replace('.', '_'),
                outputAssemblyPath: Path.Combine(dir, "out", "main.dll")),
            mainPath);
        var report = string.Join("\n", result.Diagnostics.Select(d => $"  {d.Code} {d.Line}:{d.Column} {d.Message}"));
        _output.WriteLine($"[{cell}] success={result.Success}\n{report}");

        if (refusal == null)
        {
            result.Success.Should().BeTrue($"[{cell}] a shared-framework import compiles (positive control)\n{report}");
            return;
        }

        result.Success.Should().BeFalse($"[{cell}] a host package is not the framework\n{report}");
        var errors = result.Diagnostics.Where(d => d.IsError).ToList();
        errors.Should().Contain(d => d.Code == refusal && d.Line == 1,
            $"[{cell}] refused at the import with {refusal}\n{report}");
        errors.Should().NotContain(d => d.Code == DiagnosticCodes.Infrastructure.GeneratedCodeCompilationError,
            $"[{cell}] refused by the front end, not by the C# compiler\n{report}");
    }

    /// <summary>
    /// A user reference whose simple name is a HOST assembly's loads the user's file into the
    /// registry's reference context — not the host's copy (F3 (d)). Positive control: a reference whose
    /// simple name is Sharpy.Core's keeps the framework copy (the one identity the compiler was built
    /// against).
    /// </summary>
    [Fact]
    public void UserReference_NamedLikeAHostAssembly_IsTheUsersFile()
    {
        var tpa = TrustedPlatformPaths();
        const string hostName = "Microsoft.Extensions.FileSystemGlobbing";
        tpa.Should().ContainKey(hostName, "INCONCLUSIVE: the host assembly must be in this host's trusted-platform list");

        var userCopy = Path.Combine(_dir, "user", hostName + ".dll");
        Directory.CreateDirectory(Path.GetDirectoryName(userCopy)!);
        File.Copy(tpa[hostName], userCopy);

        var coreLocation = typeof(SharpyRT::Sharpy.Builtins).Assembly.Location;
        var coreCopy = Path.Combine(_dir, "core", Path.GetFileName(coreLocation));
        Directory.CreateDirectory(Path.GetDirectoryName(coreCopy)!);
        File.Copy(coreLocation, coreCopy);

        var registry = new ModuleRegistry();
        registry.LoadReference(userCopy).Should().BeTrue(string.Join("\n", registry.Diagnostics.GetAll()));
        registry.LoadReference(coreCopy).Should().BeTrue(string.Join("\n", registry.Diagnostics.GetAll()));

        var user = registry.LoadedAssemblies.Single(a => a.GetName().Name == hostName);
        _output.WriteLine($"user reference bound to: {user.Location} (context {AssemblyLoadContext.GetLoadContext(user)?.Name})");
        Path.GetFullPath(user.Location).Should().Be(Path.GetFullPath(userCopy),
            "a user reference named like a host assembly is the user's file, not the host's copy");
        AssemblyLoadContext.GetLoadContext(user).Should().NotBe(AssemblyLoadContext.Default,
            "it loads into the registry's reference context");
        registry.Visibility.FindAssembly(hostName).Should().BeSameAs(user, "the registry sees its own copy");
        registry.Visibility.OwnAssemblies.Should().Contain(user);

        var core = registry.LoadedAssemblies.Single(a => a.GetName().Name == "Sharpy.Core");
        core.Should().BeSameAs(typeof(SharpyRT::Sharpy.Builtins).Assembly,
            "Sharpy.Core keeps the framework copy (positive control: the framework arm still fires)");
    }
}
