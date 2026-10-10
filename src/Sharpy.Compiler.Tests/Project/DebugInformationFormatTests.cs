using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Text;
using FluentAssertions;
using Sharpy.Compiler.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Project;

/// <summary>
/// #2291: the debug information a build writes does not depend on the host OS — a Debug build
/// writes a PORTABLE PDB that names the <c>.spy</c> sources, on every platform; a Release build
/// writes none.
/// </summary>
/// <remarks>
/// <para>
/// Roslyn picks the debug format from the host when the emit names none: a portable PDB on macOS
/// and Linux, the native Windows PDB on Windows. sharpyc does not ship the native writer
/// (<c>Microsoft.DiaSymReader.Native</c>), so on Windows every Debug build — the README's
/// <c>sharpyc run hello.spy</c> — fell back to the OS's .NET Framework <c>diasymreader.dll</c> and
/// failed with SPY0908 CS0041 "The version of Windows PDB writer is older than required"
/// (reported on the v0.22.0 win-x64 archive). The macOS/Linux suite could not see it: the
/// unnamed default already IS portable there.
/// </para>
/// <para>
/// Every command reaches the one PDB-emitting site through <c>ProjectCompiler</c> →
/// <c>AssemblyCompiler.CompileToAssembly</c> (one construction site, grep @ 2e0ef5601: <c>run</c>,
/// <c>build</c>, <c>compile</c> through <c>SyntheticProject</c>, <c>project</c> directly); the REPL
/// and the generator host emit no debug information. So the cells are configuration {Debug,
/// Release} on the project route, and the host axis is CI's: these tests run on ubuntu in the
/// compiler shards AND on <c>windows-latest</c> in the CI Windows job, where the native-format
/// mutation is what the pre-fix code produced.
/// </para>
/// </remarks>
public class DebugInformationFormatTests
{
    private readonly ITestOutputHelper _output;

    public DebugInformationFormatTests(ITestOutputHelper output) => _output = output;

    private const string Main = "from util import greet\n\ndef main():\n    print(greet(\"World\"))\n";
    private const string Util = "def greet(name: str) -> str:\n    return f\"Hello, {name}!\"\n";

    /// <summary>The ECMA-335 metadata signature a portable PDB opens with ("BSJB").</summary>
    private static readonly byte[] PortablePdbSignature = Encoding.ASCII.GetBytes("BSJB");

    [Fact]
    public void Debug_WritesAPortablePdbNamingTheSpySources()
    {
        using var helper = Build(configuration: null);
        var result = helper.AssertCompilationSucceeded(helper.Compile());

        var pdbPath = Path.ChangeExtension(result.OutputAssemblyPath!, ".pdb");
        File.Exists(pdbPath).Should().BeTrue($"a Debug build writes its PDB beside {result.OutputAssemblyPath}");

        var bytes = File.ReadAllBytes(pdbPath);
        bytes.Take(PortablePdbSignature.Length).Should().Equal(PortablePdbSignature,
            "the PDB is portable on every host — a native Windows PDB opens with \"Microsoft C/C++ MSF 7.00\" "
            + "and needs a writer sharpyc does not ship (#2291)");

        using var provider = MetadataReaderProvider.FromPortablePdbImage(bytes.ToImmutableArray());
        var reader = provider.GetMetadataReader();
        var documents = reader.Documents
            .Select(h => reader.GetString(reader.GetDocument(h).Name))
            .ToList();
        _output.WriteLine("PDB documents:\n  " + string.Join("\n  ", documents));

        // The documents come from the emitted #line directives: what a stack trace's file:line reads.
        documents.Should().Contain(d => Path.GetFileName(d) == "main.spy", "the entry module's #line document");
        documents.Should().Contain(d => Path.GetFileName(d) == "util.spy", "the imported module's #line document");
    }

    [Fact]
    public void Release_WritesNoPdb()
    {
        using var helper = Build(configuration: "Release");
        var result = helper.AssertCompilationSucceeded(helper.Compile());

        // Positive control: the build wrote its assembly where the PDB would sit.
        File.Exists(result.OutputAssemblyPath).Should().BeTrue("a Release build writes its assembly");
        result.OutputAssemblyPath.Should().Contain($"{Path.DirectorySeparatorChar}Release{Path.DirectorySeparatorChar}",
            "the configuration reached the build");
        File.Exists(Path.ChangeExtension(result.OutputAssemblyPath!, ".pdb"))
            .Should().BeFalse("a Release build writes no debug information");
    }

    private ProjectCompilationHelper Build(string? configuration)
    {
        var helper = new ProjectCompilationHelper(_output);
        helper.Options.Configuration = configuration;
        helper.WithRootNamespace("PdbFormat").WithEntryPoint("main.spy");
        helper.AddSourceFile("main.spy", Main);
        helper.AddSourceFile("util.spy", Util);
        helper.CreateProjectFile();
        return helper;
    }
}
