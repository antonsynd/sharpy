using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Sharpy.Compiler.Semantic.Validation;
using Sharpy.Compiler.Tests.Infrastructure;
using Xunit;

namespace Sharpy.Compiler.Tests.Conformance;

/// <summary>
/// The readers of a constant position's admission table, by a census of the compiler's source: what
/// <see cref="ScopeMeaningDifferentialTests.ConstantReaders"/> is total over (plan-777361's stage 0; the owner's ruling 8 of
/// 2026-11-20, #2423 comment 7042723494). In its own file for the Roslyn namespaces, whose <c>TypeKind</c> is not the compiler's.
/// </summary>
public sealed partial class ScopeMeaningDifferentialTests
{
    /// <summary>The readers of an <c>AdmissionTable</c> value in a set of sources, and the calls whose table cannot be traced.</summary>
    internal sealed record ReaderCensus(IReadOnlyDictionary<string, AdmissionTable> Sites, IReadOnlyList<string> Problems);

    /// <summary>
    /// The call-site census of <c>ConstantDefaultClassifier.IsAdmitted</c>: a syntax scan, so a fact can hand it a snippet. A call
    /// that names its table (<c>AdmissionTable.X</c>) is a reader, keyed <c>File:Method</c>. A call whose table is a parameter of
    /// its method is no reader itself: the readers are that method's callers in the same file, each keyed by ITS method with the
    /// table it passes. Anything else (a table that is neither, a method group, a method nobody calls) is a problem, never a
    /// silent skip.
    /// </summary>
    internal static ReaderCensus ScanClassifierReaders(IEnumerable<(string File, string Text)> sources)
    {
        var sites = new Dictionary<string, AdmissionTable>(StringComparer.Ordinal);
        var problems = new List<string>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);

        static string? Rightmost(ExpressionSyntax e) => e switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            MemberAccessExpressionSyntax m => m.Name.Identifier.ValueText,
            _ => null,
        };
        static AdmissionTable? TableOf(ExpressionSyntax e)
            => e is MemberAccessExpressionSyntax m && Rightmost(m.Expression) == nameof(AdmissionTable) && Enum.TryParse<AdmissionTable>(m.Name.Identifier.ValueText, out var table) ? table : null;
        static MethodDeclarationSyntax? MethodOf(SyntaxNode node) => node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        static string Where(string file, SyntaxNode node) => $"{file}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";

        void Add(string file, SyntaxNode at, AdmissionTable table)
        {
            var key = $"{file}:{MethodOf(at)?.Identifier.ValueText ?? "<no method>"}";
            int ordinal = seen[key] = seen.GetValueOrDefault(key) + 1;
            sites[ordinal == 1 ? key : $"{key}#{ordinal}"] = table;
        }

        foreach (var (file, text) in sources.OrderBy(s => s.File, StringComparer.Ordinal))
        {
            var root = CSharpSyntaxTree.ParseText(text).GetRoot();
            foreach (var name in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
                .Where(m => m.Name.Identifier.ValueText == nameof(ConstantDefaultClassifier.IsAdmitted) && Rightmost(m.Expression) == nameof(ConstantDefaultClassifier)))
            {
                if (name.Parent is not InvocationExpressionSyntax call || call.Expression != name || call.ArgumentList.Arguments.Count != 2)
                {
                    problems.Add($"{Where(file, name)}: `{name.Parent}` uses the classifier's admission as something other than a call with a kind and a table");
                    continue;
                }
                var tableArgument = call.ArgumentList.Arguments[1].Expression;
                if (TableOf(tableArgument) is { } named)
                {
                    Add(file, call, named);
                    continue;
                }
                var method = MethodOf(call);
                int index = method == null || tableArgument is not IdentifierNameSyntax parameter
                    ? -1
                    : method.ParameterList.Parameters.ToList().FindIndex(p => p.Identifier.ValueText == parameter.Identifier.ValueText);
                if (index < 0)
                {
                    problems.Add($"{Where(file, call)}: `{call}` takes its table from neither an AdmissionTable value nor a parameter of its method");
                    continue;
                }
                var callers = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Where(c => Rightmost(c.Expression) == method!.Identifier.ValueText && c.ArgumentList.Arguments.Count > index).ToList();
                if (callers.Count == 0)
                    problems.Add($"{Where(file, call)}: {method!.Identifier.ValueText} takes its table as a parameter, and nothing in {file} calls it");
                foreach (var caller in callers)
                {
                    if (TableOf(caller.ArgumentList.Arguments[index].Expression) is { } passed)
                        Add(file, caller, passed);
                    else
                        problems.Add($"{Where(file, caller)}: `{caller}` passes {method!.Identifier.ValueText} a table that is no AdmissionTable value");
                }
            }
        }
        return new ReaderCensus(sites, problems);
    }

    /// <summary>The census over the compiler's own source.</summary>
    private static readonly Lazy<ReaderCensus> ClassifierReaders = new(() =>
    {
        var dir = Path.Combine(DispatchSiteScan.FindRepoRoot(), "src", "Sharpy.Compiler");
        return ScanClassifierReaders(Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(f => (Path.GetFileName(f), File.ReadAllText(f))));
    });

    // ------------------------------------------------------------------------------------------- //
    // The compiler in a child process: for a behaviour that ends the process
    // ------------------------------------------------------------------------------------------- //

    /// <summary>How a child process that compiled one source ended: its exit code, what it wrote, whether the compiler
    /// answered (compiled or refused), and whether the process died of a stack overflow.</summary>
    internal sealed record ChildCompile(int ExitCode, string Stdout, string Stderr)
    {
        public bool Compiled => ExitCode == 0 && Stdout.Contains(ChildCompiledMark, StringComparison.Ordinal);
        public bool Answered => Compiled || Stdout.Contains(ChildRefusedMark, StringComparison.Ordinal);
        public bool Overflowed => ExitCode != 0 && !Answered && Stderr.Contains("Stack overflow", StringComparison.OrdinalIgnoreCase);

        public override string ToString()
            => $"exit {ExitCode}, stdout '{Stdout.Trim()}', stderr '{Stderr.TrimStart().Split('\n', 2)[0].Trim()}'";
    }

    private const string ChildCompiledMark = "CHILD-COMPILED";
    private const string ChildRefusedMark = "CHILD-REFUSED";

    /// <summary>The child's whole program: compile the file it is handed with the compiler's public API and say how it went.</summary>
    private const string ChildDriverSource =
        "public static class ScopeMeaningChild\n"
        + "{\n"
        + "    public static int Main(string[] args)\n"
        + "    {\n"
        + "        var api = new Sharpy.Compiler.CompilerApi(null, new[] { args[1] });\n"
        + "        var result = api.Compile(System.IO.File.ReadAllText(args[0]));\n"
        + "        System.Console.WriteLine(result.Success ? \"" + ChildCompiledMark + "\" : \"" + ChildRefusedMark + "\");\n"
        + "        return result.Success ? 0 : 1;\n"
        + "    }\n"
        + "}\n";

    /// <summary>
    /// Compiles <paramref name="source"/> in a process of its own and reports how the process ended. The child is a driver
    /// compiled here against the compiler this test loaded, written beside it (the test's output directory), so <c>dotnet exec</c>
    /// resolves the compiler and everything it references from the driver's own directory; nothing depends on a built CLI.
    /// </summary>
    internal static ChildCompile CompileInChildProcess(string source)
    {
        var compilerAssembly = typeof(Sharpy.Compiler.CompilerApi).Assembly.Location;
        var directory = Path.GetDirectoryName(compilerAssembly)!;
        var name = $"scope-meaning-child-{Environment.ProcessId}-{Guid.NewGuid():N}";
        var driver = Path.Combine(directory, name + ".dll");
        var runtimeConfig = Path.Combine(directory, name + ".runtimeconfig.json");
        var sourceFile = Path.Combine(Path.GetTempPath(), name + ".spy");
        try
        {
            var runtime = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
            var references = new[] { typeof(object).Assembly.Location, Path.Combine(runtime, "System.Runtime.dll"), Path.Combine(runtime, "System.Console.dll"), compilerAssembly }
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));
            var compilation = CSharpCompilation.Create(name, new[] { CSharpSyntaxTree.ParseText(ChildDriverSource) }, references, new CSharpCompilationOptions(OutputKind.ConsoleApplication));
            var emitted = compilation.Emit(driver);
            Assert.True(emitted.Success, "the child's driver does not compile: " + string.Join("; ", emitted.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
            File.WriteAllText(runtimeConfig, "{ \"runtimeOptions\": { \"tfm\": \"net" + Environment.Version.Major + ".0\", \"framework\": { \"name\": \"Microsoft.NETCore.App\", \"version\": \"" + Environment.Version.Major + ".0.0\" } } }");
            File.WriteAllText(sourceFile, source);

            var start = new System.Diagnostics.ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = directory,
            };
            start.ArgumentList.Add("exec");
            start.ArgumentList.Add(driver);
            start.ArgumentList.Add(sourceFile);
            start.ArgumentList.Add(Sharpy.TestInfrastructure.SharpyCoreReference.Location);
            using var process = System.Diagnostics.Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(120_000))
            {
                process.Kill(entireProcessTree: true);
                return new ChildCompile(-1, stdout.Result, "timed out after 120 s");
            }
            process.WaitForExit();
            return new ChildCompile(process.ExitCode, stdout.Result, stderr.Result);
        }
        finally
        {
            foreach (var file in new[] { driver, runtimeConfig, sourceFile })
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                }
            }
        }
    }

    /// <summary>The child's own control: it compiles a program that compiles and refuses one that does not, so a death is the
    /// compiler's and not the child's.</summary>
    [Fact]
    public void CompileInChildProcess_AnswersForAProgramThatCompiles_AndForOneThatDoesNot()
    {
        var compiled = CompileInChildProcess("def main() -> None:\n    print(1)\n");
        Assert.True(compiled.Compiled && !compiled.Overflowed, compiled.ToString());
        var refused = CompileInChildProcess("def main() -> None:\n    print(nosuch)\n");
        Assert.True(refused is { Compiled: false, Answered: true, Overflowed: false }, refused.ToString());
    }

    /// <summary>The census's own control: it sees a call that names its table, follows a table that is a parameter to each
    /// caller, numbers a method's second site, and reports a call it cannot trace. An absence ("no reader without a host") is
    /// only as good as this.</summary>
    [Fact]
    public void ConstantReaders_TheCensusSeesACallSite_AndFollowsATableParameter()
    {
        const string source =
            "class V {\n"
            + "  void A(S s) { Judge(s, AdmissionTable.ParameterDefault); }\n"
            + "  void B(S s) { Judge(s, Validation.AdmissionTable.ConstInitializer); }\n"
            + "  void Judge(S s, AdmissionTable table) { if (!ConstantDefaultClassifier.IsAdmitted(K(s), table)) return; }\n"
            + "  void C() { var a = ConstantDefaultClassifier.IsAdmitted(k, AdmissionTable.DecoratorArgument); var b = Validation.ConstantDefaultClassifier.IsAdmitted(k, AdmissionTable.DecoratorArgument); }\n"
            + "  void D(AdmissionTable[] ts) { ConstantDefaultClassifier.IsAdmitted(k, ts[0]); }\n"
            + "  void E() { System.Func<EmittableConstantKind, AdmissionTable, bool> f = ConstantDefaultClassifier.IsAdmitted; }\n"
            + "}\n";
        var census = ScanClassifierReaders(new[] { ("V.cs", source) });
        Assert.Equal(
            new[] { "V.cs:A=ParameterDefault", "V.cs:B=ConstInitializer", "V.cs:C#2=DecoratorArgument", "V.cs:C=DecoratorArgument" },
            census.Sites.Select(s => $"{s.Key}={s.Value}").OrderBy(s => s, StringComparer.Ordinal));
        Assert.Equal(2, census.Problems.Count);
        Assert.Contains(census.Problems, p => p.StartsWith("V.cs:6:", StringComparison.Ordinal) && p.Contains("neither an AdmissionTable value nor a parameter", StringComparison.Ordinal));
        Assert.Contains(census.Problems, p => p.StartsWith("V.cs:7:", StringComparison.Ordinal) && p.Contains("something other than a call", StringComparison.Ordinal));

        // The compiler's own readers: every one traced, none a problem.
        Assert.Empty(ClassifierReaders.Value.Problems);
        Assert.NotEmpty(ClassifierReaders.Value.Sites);
    }
}
