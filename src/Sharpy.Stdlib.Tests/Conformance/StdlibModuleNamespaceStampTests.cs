using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sharpy.Stdlib.Tests.Integration;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Stdlib.Tests.Conformance;

/// <summary>
/// #2039 (Decision 28 (g)): a spy-sourced stdlib module is a namespace <c>Sharpy.&lt;Stem&gt;</c>
/// holding its <c>[SharpyModule]</c> members class <c>&lt;Stem&gt;Module</c> and its types as
/// namespace siblings. Discovery maps any un-stamped type in a <c>Sharpy.*</c> namespace to
/// <c>builtins</c> (<c>OverloadIndexBuilder.DeriveModuleNameFromNamespace</c>), so EVERY public
/// sibling must carry <c>[SharpyModuleType]</c> naming the SAME python module as the members class —
/// <c>socket</c>, not the file stem <c>socket_module</c> the compiler stamps
/// (<c>build_tools/regenerate_spy_stdlib.sh</c> rewrites it; the stem-vs-name spelling is #2047).
/// A sibling stamped with the stem, or not at all, is discovered under the wrong module and
/// <c>socket.error</c> stops resolving.
/// </summary>
public class StdlibModuleNamespaceStampTests
{
    private static readonly Assembly StdlibAssembly = typeof(Sharpy.MathModule.MathModuleModule).Assembly;

    /// <summary>Module namespace → (members class, python module), written out, not reflected.</summary>
    private static readonly Dictionary<string, (string MembersClass, string Module)> SpySourcedModules = new()
    {
        ["Sharpy.Textwrap"] = ("TextwrapModule", "textwrap"),
        ["Sharpy.BisectModule"] = ("BisectModuleModule", "bisect"),
        ["Sharpy.Statistics"] = ("StatisticsModule", "statistics"),
        ["Sharpy.Heapq"] = ("HeapqModule", "heapq"),
        ["Sharpy.Itertools"] = ("ItertoolsModule", "itertools"),
        ["Sharpy.Functools"] = ("FunctoolsModule", "functools"),
        ["Sharpy.StringModule"] = ("StringModuleModule", "string"),
        ["Sharpy.FnmatchModule"] = ("FnmatchModuleModule", "fnmatch"),
        ["Sharpy.TempfileModule"] = ("TempfileModuleModule", "tempfile"),
        ["Sharpy.MathModule"] = ("MathModuleModule", "math"),
        ["Sharpy.OsModule"] = ("OsModuleModule", "os"),
        ["Sharpy.OsPathModule"] = ("OsPathModuleModule", "os.path"),
        ["Sharpy.ShutilModule"] = ("ShutilModuleModule", "shutil"),
        ["Sharpy.RandomModule"] = ("RandomModuleModule", "random"),
        ["Sharpy.HashlibModule"] = ("HashlibModuleModule", "hashlib"),
        ["Sharpy.CsvModule"] = ("CsvModuleModule", "csv"),
        ["Sharpy.ReModule"] = ("ReModuleModule", "re"),
        ["Sharpy.SocketModule"] = ("SocketModuleModule", "socket"),
    };

    /// <summary>The generated sibling types (the spy sources' top-level classes), written out.</summary>
    private static readonly string[] ExpectedStampedSiblings =
    {
        "csv.CsvReader", "csv.CsvWriter", "csv.CsvDictReader", "csv.CsvDictWriter",
        "hashlib.HashObject",
        "os.StatResult",
        "re.error", "re.Pattern", "re.MatchResult",
        "socket.error", "socket.timeout", "socket.gaierror", "socket.herror", "socket.socket",
        "tempfile.NamedTemporaryFile", "tempfile.TemporaryDirectory", "tempfile.SpooledTemporaryFile",
    };

    private static IEnumerable<Type> TopLevelPublicTypes(string ns)
        => StdlibAssembly.GetTypes().Where(t =>
            t.IsPublic && t.DeclaringType == null && t.Namespace == ns
            && !t.Name.StartsWith("<", StringComparison.Ordinal));

    [Fact]
    public void EverySpySourcedModule_IsANamespaceHoldingItsMembersClass()
    {
        foreach (var (ns, (membersClass, module)) in SpySourcedModules)
        {
            var modules = TopLevelPublicTypes(ns)
                .Select(t => (Type: t, Attr: t.GetCustomAttribute<SharpyModuleAttribute>(inherit: false)))
                .Where(x => x.Attr != null)
                .ToList();
            Assert.True(modules.Count == 1,
                $"{ns}: expected exactly one [SharpyModule] members class, found "
                + string.Join(", ", modules.Select(m => m.Type.Name)));
            Assert.Equal(membersClass, modules[0].Type.Name);
            Assert.Equal(module, modules[0].Attr!.ModuleName);
        }
    }

    [Fact]
    public void EverySiblingType_IsStampedWithItsNamespacesModule()
    {
        var namespacesWithAModule = StdlibAssembly.GetTypes()
            .Where(t => t.DeclaringType == null && t.Namespace is { } n && n.StartsWith("Sharpy.", StringComparison.Ordinal)
                && t.GetCustomAttribute<SharpyModuleAttribute>(inherit: false) != null)
            .Select(t => t.Namespace!)
            .ToHashSet();

        // Positive control: the roster above is what reflection finds — a module moved out of
        // (or into) the layout without the roster is a red, not a silently smaller sweep.
        Assert.Equal(SpySourcedModules.Keys.OrderBy(k => k), namespacesWithAModule.OrderBy(k => k));

        var violations = new System.Collections.Generic.List<string>();
        var stamped = new System.Collections.Generic.List<string>();
        foreach (var ns in namespacesWithAModule)
        {
            var module = SpySourcedModules[ns].Module;
            foreach (var type in TopLevelPublicTypes(ns))
            {
                if (type.GetCustomAttribute<SharpyModuleAttribute>(inherit: false) != null)
                    continue;
                var stamp = type.GetCustomAttribute<SharpyModuleTypeAttribute>(inherit: false);
                if (stamp == null)
                    violations.Add($"{type.FullName}: no [SharpyModuleType] (discovered as builtins)");
                else if (stamp.ModuleName != module)
                    violations.Add($"{type.FullName}: [SharpyModuleType(\"{stamp.ModuleName}\")], its module is '{module}'");
                else
                    stamped.Add(module + "." + (stamp.PythonName ?? type.Name));
            }
        }

        Assert.True(violations.Count == 0, string.Join("\n", violations));

        // Positive control: the sweep found the 17 generated siblings (measured) — an empty or
        // shrunken walk is a red, not a vacuous green.
        Assert.Equal(17, stamped.Count);
        Assert.Equal(ExpectedStampedSiblings.OrderBy(s => s, StringComparer.Ordinal),
            stamped.OrderBy(s => s, StringComparer.Ordinal));
    }
}

/// <summary>
/// The runtime half of <see cref="StdlibModuleNamespaceStampTests"/> (#2039, #2006): a generated
/// stdlib sibling type's module segment is its python module wherever the runtime reads the stamp —
/// the instance fallback (<c>&lt;os.StatResult object&gt;</c>, not <c>&lt;os_module.…&gt;</c>) and
/// <c>type(x)</c> (<c>&lt;class 'socket.gaierror'&gt;</c>, python's spelling, the P11f cell). Only the
/// module segment is asserted for the two fallback lines: python prints <c>os.stat_result(...)</c>
/// and <c>&lt;md5 _hashlib.HASH object @ …&gt;</c> there — those type names are not this contract.
/// </summary>
public class StdlibSiblingTypeModuleNameRuntimeTests : StdlibIntegrationTestBase
{
    public StdlibSiblingTypeModuleNameRuntimeTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void SiblingInstances_NameTheirPythonModule()
    {
        const string program = """
            import hashlib
            import os
            import socket

            def main() -> None:
                print(repr(os.stat(".")))
                print(repr(hashlib.md5("abc")))
                print(type(socket.gaierror("g")))
            """;
        var result = CompileAndExecute(program);
        Assert.True(result.Success, string.Join("; ", result.CompilationErrors) + result.StandardError);

        var lines = result.StandardOutput.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("<os.", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("<hashlib.", lines[1], StringComparison.Ordinal);
        Assert.Equal("<class 'socket.gaierror'>", lines[2]);
    }
}
