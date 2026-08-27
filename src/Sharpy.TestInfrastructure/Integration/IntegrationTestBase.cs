using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Sharpy.Compiler;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Discovery.Caching;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Sharpy.Compiler.Services;
using Sharpy.Compiler.Shared;
using Xunit.Abstractions;
using static Sharpy.TestInfrastructure.TestHelpers;

namespace Sharpy.TestInfrastructure.Integration;

/// <summary>
/// Base class for end-to-end integration tests that compile Sharpy code to C# and execute it.
/// </summary>
public abstract class IntegrationTestBase
{
    protected readonly ITestOutputHelper Output;

    private static readonly Lazy<string> SharedTestCacheDir = new(() =>
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sharpy-test-cache-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        { try { Directory.Delete(dir, recursive: true); } catch { } };
        return dir;
    });

    private static readonly Lazy<(IReadOnlyList<MetadataReference> References, string? RuntimePath)> SharedReferences =
        new(BuildSharedReferences);

    private static readonly Lazy<CSharpCompilation> SharedBaseCompilation =
        new(() => CSharpCompilation.Create(
            "SharpyTestAssembly",
            Array.Empty<SyntaxTree>(),
            SharedReferences.Value.References,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication)));

    /// <summary>
    /// Exposes the shared metadata-reference set used to compile generated C# to IL.
    /// Property/regression tests that emit generated C# through Roslyn outside this
    /// base class must reuse this exact set — otherwise a missing reference surfaces as
    /// a spurious CS error and masquerades as a code-generation leak.
    /// </summary>
    public static IReadOnlyList<MetadataReference> GetSharedReferences() => SharedReferences.Value.References;

    private static (IReadOnlyList<MetadataReference> References, string? RuntimePath) BuildSharedReferences()
    {
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
            // Xunit references — needed for fixtures that use the @test decorator,
            // which emits [Xunit.FactAttribute] and Xunit.Assert.* calls.
            MetadataReference.CreateFromFile(typeof(Xunit.FactAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Xunit.Assert).Assembly.Location),
            // Additional collection assemblies referenced by Xunit.Assert overloads
            // (e.g. Contains/DoesNotContain accept IDictionary, IReadOnlyDictionary, ImmutableHashSet, etc.).
            MetadataReference.CreateFromFile(Assembly.Load("System.Collections.Concurrent").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.ObjectModel").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Collections.Immutable").Location),
            // System.Text.RegularExpressions is needed for overload resolution of
            // Xunit.Assert.Matches (the Regex overload's signature must be loadable),
            // emitted by assert_regex and assert_raises(..., match=...). Real net10.0
            // test projects reference it implicitly via the framework.
            MetadataReference.CreateFromFile(Assembly.Load("System.Text.RegularExpressions").Location),
            // System.Threading.Tasks (the facade assembly, v4.0.0.0) is needed for async
            // @test.fixture classes that implement Xunit.IAsyncLifetime — xunit's interface
            // metadata references Task through this facade, so it must be referenced even
            // though Task's implementation lives in System.Private.CoreLib. Real net10.0
            // test projects reference it implicitly.
            MetadataReference.CreateFromFile(Assembly.Load("System.Threading.Tasks").Location),
            // System.Runtime.Numerics is needed for fixtures whose generated C# surfaces
            // System.Numerics.BigInteger (e.g. fractions.Fraction.numerator/denominator).
            // Real net10.0 test projects reference it implicitly via the framework.
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime.Numerics").Location),
        };

        string? runtimePath = null;
        var testAssemblyPath = Assembly.GetExecutingAssembly().Location;
        var testDir = Path.GetDirectoryName(testAssemblyPath)!;

        // Probe order: test output directory first (MSBuild copies project references there),
        // then navigate to the Core project's bin directory with both Debug and Release configs.
        var coreDllPath = FindAssembly(testDir, "Sharpy.Core", "Sharpy.Core.dll");
        if (coreDllPath != null)
        {
            references.Add(MetadataReference.CreateFromFile(coreDllPath));
            runtimePath = coreDllPath;

            try
            {
                var netstandardAssembly = Assembly.Load("netstandard");
                references.Add(MetadataReference.CreateFromFile(netstandardAssembly.Location));
            }
            catch
            {
                var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
                var netstandardPath = Path.Combine(runtimeDir!, "netstandard.dll");
                if (File.Exists(netstandardPath))
                    references.Add(MetadataReference.CreateFromFile(netstandardPath));
            }
        }

        return (references, runtimePath);
    }

    protected IntegrationTestBase(ITestOutputHelper output)
    {
        Output = output;
    }

    protected virtual IEnumerable<string> GetAdditionalReferenceAssemblyPaths()
        => Enumerable.Empty<string>();

    /// <summary>
    /// Result of compiling and executing Sharpy code.
    /// </summary>
    protected class ExecutionResult
    {
        public bool Success { get; init; }
        public string StandardOutput { get; init; } = string.Empty;
        public string StandardError { get; init; } = string.Empty;
        public List<string> CompilationErrors { get; init; } = new();
        public List<string> CompilationWarnings { get; init; } = new();
        public string? GeneratedCSharp { get; init; }
        public Exception? Exception { get; init; }
        public bool TimedOut { get; init; }

        /// <summary>
        /// Raw CompilerDiagnostic objects from Sharpy compilation phases.
        /// Used for verifying diagnostic locations (line/column/span) in error tests.
        /// May be empty for errors originating from the C# compilation or execution phases.
        /// </summary>
        public List<CompilerDiagnostic> RawDiagnostics { get; init; } = new();
    }

    /// <summary>
    /// Compiles and executes, then forces a gen-2 GC to release Roslyn compilation state.
    /// Use in tight loops (property tests) to prevent memory buildup.
    /// </summary>
    protected ExecutionResult CompileAndExecuteWithGC(string sharpySource, string fileName = "test.spy", int executionTimeoutMs = 0, FeatureFlags? features = null)
    {
        var result = CompileAndExecute(sharpySource, fileName, executionTimeoutMs, features);
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        return result;
    }

    /// <summary>
    /// Compiles Sharpy source code to C# and executes it, returning the result.
    /// Forces GC after each call to prevent memory buildup from Roslyn compilation state.
    /// </summary>
    /// <param name="sharpySource">The Sharpy source code to compile and execute.</param>
    /// <param name="fileName">The file name to use for the source (for error messages).</param>
    /// <param name="executionTimeoutMs">Optional timeout in milliseconds for execution. Default is no timeout (0). Use for tests that may have infinite loops.</param>
    /// <param name="features">Optional experimental feature flags to enable for this compile (e.g. from a fixture's <c>.features</c> sidecar). Defaults to none.</param>
    protected ExecutionResult CompileAndExecute(string sharpySource, string fileName = "test.spy", int executionTimeoutMs = 0, FeatureFlags? features = null)
    {
        try
        {
            return CompileAndExecuteCore(sharpySource, fileName, executionTimeoutMs, features ?? FeatureFlags.None);
        }
        finally
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
        }
    }

    private ExecutionResult CompileAndExecuteCore(string sharpySource, string fileName, int executionTimeoutMs, FeatureFlags features)
    {
        // Track path to Sharpy.Core for copying to temp execution directory
        string? runtimePath = null;

        try
        {
            // Phases 1-4 (Lexer -> Parser -> Semantic -> Validation -> CodeGen) run
            // through the production pipeline (#1038). The source is written to a per-test
            // temp file so CompilerApi.Compile detects an on-disk entry and compiles it as
            // a synthetic project-of-one-file through ProjectCompiler — the exact path that
            // `sharpyc run/build <file>` uses. The harness no longer hand-wires the phases,
            // hardcodes IsEntryPoint, or threads no FeatureFlags.
            var logger = new OutputTestLogger(Output);

            // Preserve the fixture's file name (inside a unique temp dir) so single-file
            // module naming and any #line directives match a real single-file compile of
            // this fixture rather than a random temp stem.
            var safeName = Path.GetFileName(fileName);
            if (string.IsNullOrEmpty(safeName))
                safeName = "test.spy";
            if (!safeName.EndsWith(".spy", StringComparison.Ordinal))
                safeName += ".spy";

            var tempSourceDir = Path.Combine(Path.GetTempPath(), $"sharpy_src_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempSourceDir);
            var sourceFilePath = Path.Combine(tempSourceDir, safeName);

            CompileResult compileResult;
            try
            {
                File.WriteAllText(sourceFilePath, sharpySource);

                var defaultReferences = new List<string> { SharpyCoreReference.Location };
                defaultReferences.AddRange(GetAdditionalReferenceAssemblyPaths());

                var api = new CompilerApi(logger, defaultReferences.ToArray());
                // Exempt from the CompilerOptionsFactory seam (#1144) by design: this is a
                // baseline constructor. The harness must be able to state an arbitrary options
                // shape independent of what any product entry surface currently decides —
                // routing it through a per-surface factory method would make the fixture suite
                // agree with the surfaces by construction and stop detecting their drift.
                var options = new CompilerOptions
                {
                    // Integration tests are executable programs, so the synthetic project's
                    // single source file is its entry point.
                    OutputType = "exe",
                    // This harness references Xunit (see the metadata references above), so it IS a
                    // test host: a `@test` function's asserts keep the Xunit lowering here, which is
                    // what holds the `unittest/*.expected.cs` snapshots byte-identical while the
                    // same source compiles framework-free under `sharpyc run` (#1495).
                    TargetsTestHost = true,
                    Features = features
                };

                compileResult = api.Compile(sharpySource, options, sourceFilePath);
            }
            finally
            {
                // The compiler has read everything it needs off disk by now; drop the
                // temp source so it does not accumulate across ~9,600 tests.
                try
                {
                    if (Directory.Exists(tempSourceDir))
                        Directory.Delete(tempSourceDir, recursive: true);
                }
                catch
                {
                    // Best-effort cleanup.
                }
            }

            var rawDiagnostics = compileResult.Diagnostics.ToList();
            var compilationErrors = rawDiagnostics
                .Where(d => d.Severity == CompilerDiagnosticSeverity.Error)
                .Select(d => d.Message)
                .ToList();

            // Collect warnings, hints, and info notes from all phases.
            // Hints are advisory diagnostics (e.g., transition hints SPY0470+) and Info
            // diagnostics (e.g., SPY1001 implicit interface synthesis, SPY1010 functools
            // placeholder hint) share suppression with warnings; we surface them via the
            // same channel so .warning fixtures can verify behavioral notes.
            var compilationWarnings = rawDiagnostics
                .Where(d => d.Severity == CompilerDiagnosticSeverity.Warning
                         || d.Severity == CompilerDiagnosticSeverity.Hint
                         || d.Severity == CompilerDiagnosticSeverity.Info)
                .Select(d => d.Message)
                .ToList();

            var generatedCSharp = compileResult.GeneratedCSharp;

            Output.WriteLine("=== Generated C# ===");
            Output.WriteLine(generatedCSharp ?? "(no C# generated)");
            Output.WriteLine("====================");

            if (!compileResult.Success || generatedCSharp == null)
            {
                return new ExecutionResult
                {
                    Success = false,
                    CompilationErrors = compilationErrors,
                    CompilationWarnings = compilationWarnings,
                    GeneratedCSharp = generatedCSharp,
                    RawDiagnostics = rawDiagnostics
                };
            }

            // Phase 5: Compile C# to assembly
            var syntaxTree = CSharpSyntaxTree.ParseText(generatedCSharp);

            runtimePath = SharedReferences.Value.RuntimePath;

            var compilation = SharedBaseCompilation.Value.AddSyntaxTrees(syntaxTree);
            var additionalPaths = GetAdditionalReferenceAssemblyPaths().ToList();
            if (additionalPaths.Count > 0)
            {
                compilation = compilation.AddReferences(
                    additionalPaths.Where(File.Exists).Select(p => MetadataReference.CreateFromFile(p)));
            }

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            if (!emitResult.Success)
            {
                var errors = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString())
                    .ToList();

                return new ExecutionResult
                {
                    Success = false,
                    CompilationErrors = errors,
                    CompilationWarnings = compilationWarnings,
                    GeneratedCSharp = generatedCSharp
                };
            }

            // Phase 6: Execute the compiled assembly
            // Write to a temp file and execute as a separate process to avoid
            // reflection/interpreted mode issues on some platforms (.NET 10 on Linux x64)
            var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            var tempAssemblyPath = Path.Combine(tempDir, "SharpyTestAssembly.dll");

            try
            {
                ms.Seek(0, SeekOrigin.Begin);
                using (var fileStream = File.Create(tempAssemblyPath))
                {
                    ms.CopyTo(fileStream);
                }

                // Copy runtime dependencies
                if (runtimePath != null && File.Exists(runtimePath))
                {
                    var runtimeDest = Path.Combine(tempDir, "Sharpy.Core.dll");
                    File.Copy(runtimePath, runtimeDest, overwrite: true);

                    var testBinDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
                    CopyRuntimeClosure(testBinDir, tempDir,
                        new[] { runtimeDest }.Concat(additionalPaths));
                }

                foreach (var additionalPath in additionalPaths.Where(File.Exists))
                {
                    var destPath = Path.Combine(tempDir, Path.GetFileName(additionalPath));
                    if (!File.Exists(destPath))
                        File.Copy(additionalPath, destPath);
                }

                // Create a runtimeconfig.json for the assembly
                var runtimeConfigPath = Path.Combine(tempDir, "SharpyTestAssembly.runtimeconfig.json");
                var runtimeConfig = @"{
  ""runtimeOptions"": {
    ""tfm"": ""net10.0"",
    ""framework"": {
      ""name"": ""Microsoft.NETCore.App"",
      ""version"": ""10.0.0""
    }
  }
}";
                File.WriteAllText(runtimeConfigPath, runtimeConfig);

                // Execute the assembly as a separate process
                var startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"exec \"{tempAssemblyPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = tempDir
                };

                using var process = new Process { StartInfo = startInfo };
                var stdout = new StringBuilder();
                var stderr = new StringBuilder();
                bool timedOut = false;

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        stdout.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        stderr.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var timeout = executionTimeoutMs > 0 ? executionTimeoutMs : 30000; // Default 30s timeout
                if (!process.WaitForExit(timeout))
                {
                    timedOut = true;
                    try
                    { process.Kill(entireProcessTree: true); }
                    catch { }
                }

                // Ensure async output handlers complete
                process.WaitForExit();

                if (timedOut)
                {
                    return new ExecutionResult
                    {
                        Success = false,
                        TimedOut = true,
                        StandardOutput = stdout.ToString(),
                        StandardError = stderr.ToString(),
                        GeneratedCSharp = generatedCSharp,
                        CompilationErrors = new List<string> { $"Execution timed out after {timeout}ms" }
                    };
                }

                if (process.ExitCode != 0)
                {
                    return new ExecutionResult
                    {
                        Success = false,
                        StandardOutput = stdout.ToString(),
                        StandardError = stderr.ToString(),
                        GeneratedCSharp = generatedCSharp,
                        CompilationErrors = new List<string> { $"Process exited with code {process.ExitCode}: {stderr}" }
                    };
                }

                return new ExecutionResult
                {
                    Success = true,
                    StandardOutput = stdout.ToString(),
                    StandardError = stderr.ToString(),
                    GeneratedCSharp = generatedCSharp,
                    CompilationWarnings = compilationWarnings
                };
            }
            finally
            {
                // Clean up temp directory
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
        catch (TargetInvocationException ex)
        {
            var errorMessage = ex.InnerException != null
                ? $"Unexpected error during execution: {ex.InnerException.Message}\nStack Trace: {ex.InnerException.StackTrace}"
                : $"Unexpected error during execution: {ex.Message}";

            return new ExecutionResult
            {
                Success = false,
                Exception = ex,
                CompilationErrors = new List<string> { errorMessage }
            };
        }
        catch (InvalidOperationException ex)
        {
            return new ExecutionResult
            {
                Success = false,
                Exception = ex,
                CompilationErrors = new List<string> { $"Invalid operation: {ex.Message}" }
            };
        }
        catch (FileNotFoundException ex)
        {
            return new ExecutionResult
            {
                Success = false,
                Exception = ex,
                CompilationErrors = new List<string> { $"File not found: {ex.Message}" }
            };
        }
        catch (TypeLoadException ex)
        {
            return new ExecutionResult
            {
                Success = false,
                Exception = ex,
                CompilationErrors = new List<string> { $"Type load error: {ex.Message}" }
            };
        }
        // Generic catch as final fallback for any unexpected exceptions during compilation/execution
        // This is intentional as test infrastructure needs to handle arbitrary code gracefully
        catch (Exception ex)
        {
            var errorMessage = $"Unexpected error: {ex.Message}";
            if (ex.InnerException != null)
            {
                errorMessage += $"\nInner Exception: {ex.InnerException.Message}";
                errorMessage += $"\nStack Trace: {ex.InnerException.StackTrace}";
            }

            return new ExecutionResult
            {
                Success = false,
                Exception = ex,
                CompilationErrors = new List<string> { errorMessage }
            };
        }
    }

    /// <summary>
    /// Compiles a multi-file Sharpy project and executes it.
    /// </summary>
    /// <param name="projectDir">Directory containing the Sharpy source files.</param>
    /// <param name="entryPointFile">The main entry point file (e.g., "main.spy").</param>
    /// <param name="executionTimeoutMs">Optional timeout in milliseconds for execution.</param>
    /// <param name="features">Optional experimental feature flags enabled compilation-wide for the
    /// whole project (e.g. from a fixture's <c>.features</c> sidecar). Defaults to none.</param>
    protected ExecutionResult CompileAndExecuteProject(string projectDir, string entryPointFile, int executionTimeoutMs = 0, FeatureFlags? features = null)
    {
        try
        {
            return CompileAndExecuteProjectCore(projectDir, entryPointFile, executionTimeoutMs, features ?? FeatureFlags.None);
        }
        finally
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
        }
    }

    private ExecutionResult CompileAndExecuteProjectCore(string projectDir, string entryPointFile, int executionTimeoutMs, FeatureFlags features)
    {
        try
        {
            var logger = new OutputTestLogger(Output);

            // Discover all .spy files in the directory (including subdirectories for packages).
            // Directory.GetFiles order is filesystem-dependent; sort ordinally so the test
            // harness compiles in the same deterministic order as ProjectFileParser.Load (#1032).
            var sourceFiles = Directory.GetFiles(projectDir, "*.spy", SearchOption.AllDirectories)
                .Where(f => !Compiler.Diagnostics.CrashBundleWriter.IsNonSourceSegment(
                    Path.GetRelativePath(projectDir, f)))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();

            if (sourceFiles.Count == 0)
            {
                return new ExecutionResult
                {
                    Success = false,
                    CompilationErrors = new List<string> { $"No .spy files found in {projectDir}" }
                };
            }

            Output.WriteLine($"Found {sourceFiles.Count} source files:");
            foreach (var file in sourceFiles)
            {
                var relativePath = Path.GetRelativePath(projectDir, file);
                Output.WriteLine($"  - {relativePath}");
            }

            // Create a project config for the test
            var projectConfig = new ProjectConfig
            {
                ProjectDirectory = projectDir,
                ProjectFilePath = Path.Combine(projectDir, "test.spyproj"),
                RootNamespace = "Sharpy.Test",
                OutputType = "exe",
                EntryPoint = entryPointFile,
                SourceFiles = sourceFiles,
                Configuration = "Debug",
                TargetFramework = "net10.0",
                CrashRoot = Path.GetTempPath(),
            };

            // Set up module registry with an isolated test cache so the fixture suite never
            // reads or writes the developer's ~/.sharpy/cache/overload-index (#1313).
            var moduleRegistry = new ModuleRegistry(logger, new OverloadIndexCache(SharedTestCacheDir.Value));
            moduleRegistry.LoadReference(SharpyCoreReference.Location);
            foreach (var additionalPath in GetAdditionalReferenceAssemblyPaths())
                moduleRegistry.LoadReference(additionalPath);

            // Compile the project. Experimental features (from a fixture's `.features`
            // sidecar) are enabled compilation-wide via the ProjectCompiler, matching how
            // `<Features>` in a .spyproj gates the whole project.
            var projectCompiler = new ProjectCompiler(logger, moduleRegistry,
                ProjectCompilerOptions.Default with { Features = features });
            var result = projectCompiler.Compile(projectConfig);

            // Collect warnings and hints from the project compilation. Hints are
            // surfaced alongside warnings (see semantic-phase comment above) so that
            // fixture .warning files can assert advisory transition diagnostics.
            var projectWarnings = result.Diagnostics.GetAll()
                .Where(d => d.Severity == CompilerDiagnosticSeverity.Warning
                         || d.Severity == CompilerDiagnosticSeverity.Hint)
                .Select(d => d.Message)
                .ToList();

            if (!result.Success)
            {
                return new ExecutionResult
                {
                    Success = false,
                    CompilationErrors = result.Diagnostics.GetErrors().Select(d => d.Message).ToList(),
                    CompilationWarnings = projectWarnings,
                    GeneratedCSharp = FormatGeneratedProjectCSharp(result.GeneratedCSharpFiles),
                    RawDiagnostics = result.Diagnostics.GetAll().ToList()
                };
            }

            // Log generated C#
            Output.WriteLine("=== Generated C# ===");
            foreach (var (fileName, code) in result.GeneratedCSharpFiles)
            {
                Output.WriteLine($"// {fileName}");
                Output.WriteLine(code);
                Output.WriteLine("---");
            }
            Output.WriteLine("====================");

            return EmitAndRunProjectAssembly(
                result.GeneratedCSharpFiles.Values.ToList(),
                FormatGeneratedProjectCSharp(result.GeneratedCSharpFiles),
                projectWarnings,
                executionTimeoutMs);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Unexpected error: {ex.Message}";
            if (ex.InnerException != null)
            {
                errorMessage += $"\nInner Exception: {ex.InnerException.Message}";
                errorMessage += $"\nStack Trace: {ex.InnerException.StackTrace}";
            }

            return new ExecutionResult
            {
                Success = false,
                Exception = ex,
                CompilationErrors = new List<string> { errorMessage }
            };
        }
    }

    /// <summary>
    /// Compiles and executes a Sharpy program by its <em>entry file path</em> — the seam
    /// <c>sharpyc run &lt;file&gt;</c> uses (<see cref="CompilerApi.CompileFile"/>), which discovers
    /// the transitive closure of local <c>.spy</c> imports itself instead of being handed a source
    /// list. No <c>RootNamespace</c> is supplied, so the namespace the compiler picks for a
    /// multi-file closure is exercised as the CLI would experience it (#1171).
    /// </summary>
    /// <param name="entryFilePath">Full path to the entry <c>.spy</c> file on disk. Sibling
    /// modules are read from disk relative to it, so the file must not be copied out of its
    /// directory.</param>
    /// <param name="executionTimeoutMs">Optional timeout in milliseconds for execution.</param>
    /// <param name="features">Optional experimental feature flags enabled compilation-wide.</param>
    protected ExecutionResult CompileAndExecuteEntryFile(
        string entryFilePath, int executionTimeoutMs = 0, FeatureFlags? features = null)
    {
        try
        {
            return CompileAndExecuteEntryFileCore(entryFilePath, executionTimeoutMs, features ?? FeatureFlags.None);
        }
        finally
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
        }
    }

    private ExecutionResult CompileAndExecuteEntryFileCore(
        string entryFilePath, int executionTimeoutMs, FeatureFlags features)
    {
        try
        {
            var logger = new OutputTestLogger(Output);

            var defaultReferences = new List<string> { SharpyCoreReference.Location };
            defaultReferences.AddRange(GetAdditionalReferenceAssemblyPaths());

            var api = new CompilerApi(logger, defaultReferences.ToArray());
            // Exempt from the CompilerOptionsFactory seam (#1144) for the same reason as
            // CompileAndExecuteCore: this arm must state its own options shape so it keeps
            // detecting drift in whatever the CLI surfaces decide. Deliberately no Namespace —
            // the entry-file compile is what has to choose a workable one (#1171).
            var options = new CompilerOptions
            {
                OutputType = "exe",
                // See the note on the single-file path above (#1495).
                TargetsTestHost = true,
                Features = features
            };

            var result = api.CompileFile(entryFilePath, options);

            var rawDiagnostics = result.Diagnostics.ToList();
            var compilationErrors = rawDiagnostics
                .Where(d => d.Severity == CompilerDiagnosticSeverity.Error)
                .Select(d => d.Message)
                .ToList();

            // Bucket severities exactly as CompileAndExecuteProjectCore does (Warning + Hint) so a
            // `.warning` sidecar behaves identically through both arms; a difference between the
            // arms is then a compiler difference, never a harness one.
            var compilationWarnings = rawDiagnostics
                .Where(d => d.Severity == CompilerDiagnosticSeverity.Warning
                         || d.Severity == CompilerDiagnosticSeverity.Hint)
                .Select(d => d.Message)
                .ToList();

            var generatedReport = FormatGeneratedProjectCSharp(result.GeneratedCSharpFiles);

            Output.WriteLine("=== Generated C# ===");
            Output.WriteLine(generatedReport);
            Output.WriteLine("====================");

            if (!result.Success || result.GeneratedCSharpFiles.Count == 0)
            {
                return new ExecutionResult
                {
                    Success = false,
                    CompilationErrors = compilationErrors,
                    CompilationWarnings = compilationWarnings,
                    GeneratedCSharp = result.GeneratedCSharpFiles.Count > 0 ? generatedReport : null,
                    RawDiagnostics = rawDiagnostics
                };
            }

            return EmitAndRunProjectAssembly(
                result.GeneratedCSharpFiles.Values.ToList(),
                generatedReport,
                compilationWarnings,
                executionTimeoutMs);
        }
        catch (Exception ex)
        {
            var errorMessage = $"Unexpected error: {ex.Message}";
            if (ex.InnerException != null)
            {
                errorMessage += $"\nInner Exception: {ex.InnerException.Message}";
                errorMessage += $"\nStack Trace: {ex.InnerException.StackTrace}";
            }

            return new ExecutionResult
            {
                Success = false,
                Exception = ex,
                CompilationErrors = new List<string> { errorMessage }
            };
        }
    }

    /// <summary>
    /// Renders a project's per-file generated C# into the single string the
    /// <see cref="ExecutionResult.GeneratedCSharp"/> contract carries, ordered by file name so the
    /// report is stable across filesystem enumeration order.
    /// </summary>
    private static string FormatGeneratedProjectCSharp(IReadOnlyDictionary<string, string> generatedFiles)
        => string.Join("\n\n", generatedFiles.OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .Select(kvp => $"// {kvp.Key}\n{kvp.Value}"));

    /// <summary>
    /// Compiles generated C# for a whole project to an assembly and runs it out-of-process.
    /// Shared by the project-path harness (<see cref="CompileAndExecuteProject"/>) and the
    /// entry-file-path harness (<see cref="CompileAndExecuteEntryFile"/>) so both arms deploy and
    /// execute through identical code — a behavioral difference between them is then attributable
    /// to the compiler, not to the harness (#1171).
    /// </summary>
    private ExecutionResult EmitAndRunProjectAssembly(
        IReadOnlyList<string> csharpSources,
        string generatedCSharpReport,
        List<string> compilationWarnings,
        int executionTimeoutMs)
    {
        string? runtimePath;

        try
        {
            // Parse and compile the generated C#
            var syntaxTrees = csharpSources
                .Select(code => CSharpSyntaxTree.ParseText(code))
                .ToList();

            var (projectReferences, projectRuntimePath) = SharedReferences.Value;
            runtimePath = projectRuntimePath;

            var allReferences = projectReferences.ToList();
            var additionalPaths = GetAdditionalReferenceAssemblyPaths().ToList();
            allReferences.AddRange(
                additionalPaths.Where(File.Exists).Select(p => MetadataReference.CreateFromFile(p)));

            var compilation = CSharpCompilation.Create(
                "SharpyTestProject",
                syntaxTrees,
                allReferences,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication));

            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            if (!emitResult.Success)
            {
                var errors = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.ToString())
                    .ToList();

                return new ExecutionResult
                {
                    Success = false,
                    CompilationErrors = errors,
                    GeneratedCSharp = generatedCSharpReport
                };
            }

            // Execute the compiled assembly via external process to avoid
            // reflection/interpreted mode issues on some platforms
            var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            var tempAssemblyPath = Path.Combine(tempDir, "SharpyTestProject.dll");

            try
            {
                ms.Seek(0, SeekOrigin.Begin);
                using (var fileStream = File.Create(tempAssemblyPath))
                {
                    ms.CopyTo(fileStream);
                }

                // Copy runtime dependencies
                if (runtimePath != null && File.Exists(runtimePath))
                {
                    var runtimeDest = Path.Combine(tempDir, "Sharpy.Core.dll");
                    File.Copy(runtimePath, runtimeDest, overwrite: true);

                    var testBinDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
                    CopyRuntimeClosure(testBinDir, tempDir,
                        new[] { runtimeDest }.Concat(additionalPaths));
                }

                foreach (var additionalPath in additionalPaths.Where(File.Exists))
                {
                    var destPath = Path.Combine(tempDir, Path.GetFileName(additionalPath));
                    if (!File.Exists(destPath))
                        File.Copy(additionalPath, destPath);
                }

                // Create a runtimeconfig.json for the assembly
                var runtimeConfigPath = Path.Combine(tempDir, "SharpyTestProject.runtimeconfig.json");
                var runtimeConfig = @"{
  ""runtimeOptions"": {
    ""tfm"": ""net10.0"",
    ""framework"": {
      ""name"": ""Microsoft.NETCore.App"",
      ""version"": ""10.0.0""
    }
  }
}";
                File.WriteAllText(runtimeConfigPath, runtimeConfig);

                // Execute the assembly as a separate process
                var startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"exec \"{tempAssemblyPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = tempDir
                };

                using var process = new Process { StartInfo = startInfo };
                var stdout = new StringBuilder();
                var stderr = new StringBuilder();
                bool timedOut = false;

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        stdout.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        stderr.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                var timeout = executionTimeoutMs > 0 ? executionTimeoutMs : 30000; // Default 30s timeout
                if (!process.WaitForExit(timeout))
                {
                    timedOut = true;
                    try
                    { process.Kill(entireProcessTree: true); }
                    catch { }
                }

                // Ensure async output handlers complete
                process.WaitForExit();

                if (timedOut)
                {
                    return new ExecutionResult
                    {
                        Success = false,
                        TimedOut = true,
                        StandardOutput = stdout.ToString(),
                        StandardError = stderr.ToString(),
                        GeneratedCSharp = generatedCSharpReport,
                        CompilationErrors = new List<string> { $"Execution timed out after {timeout}ms" }
                    };
                }

                if (process.ExitCode != 0)
                {
                    return new ExecutionResult
                    {
                        Success = false,
                        StandardOutput = stdout.ToString(),
                        StandardError = stderr.ToString(),
                        GeneratedCSharp = generatedCSharpReport,
                        CompilationErrors = new List<string> { $"Process exited with code {process.ExitCode}: {stderr}" }
                    };
                }

                return new ExecutionResult
                {
                    Success = true,
                    StandardOutput = stdout.ToString(),
                    StandardError = stderr.ToString(),
                    GeneratedCSharp = generatedCSharpReport,
                    CompilationWarnings = compilationWarnings
                };
            }
            finally
            {
                // Clean up temp directory
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
        catch (Exception ex)
        {
            var errorMessage = $"Unexpected error: {ex.Message}";
            if (ex.InnerException != null)
            {
                errorMessage += $"\nInner Exception: {ex.InnerException.Message}";
                errorMessage += $"\nStack Trace: {ex.InnerException.StackTrace}";
            }

            return new ExecutionResult
            {
                Success = false,
                Exception = ex,
                CompilationErrors = new List<string> { errorMessage }
            };
        }
    }

    /// <summary>
    /// Copies the runtime closure of the fixture's references into the temp execution directory,
    /// flat. There is no .deps.json, so the host resolves managed assemblies and native libraries
    /// from the app directory alone — which is why everything lands beside the assembly.
    ///
    /// <para>
    /// This used to be a hand-maintained list of six DLL names, and it drifted: YamlDotNet was
    /// simply never added, so no yaml fixture could ever run and the gap was invisible until
    /// someone tried (#1300). The closure is now computed by the same
    /// <see cref="RuntimeClosureResolver"/> the CLI uses — a mechanical walk of assembly
    /// references plus the companion pass that finds runtime-registration assemblies like
    /// SQLitePCLRaw's provider bundle — so a new NuGet-backed stdlib module needs no edit here.
    /// </para>
    /// </summary>
    private static void CopyRuntimeClosure(
        string testBinDir, string destDir, IEnumerable<string> referencePaths)
    {
        var seeds = referencePaths.Where(File.Exists).ToList();
        if (seeds.Count == 0)
            return;

        var closure = RuntimeClosureResolver.Resolve(seeds);

        foreach (var assetPath in closure.ManagedAssemblies.Concat(closure.NativeAssets))
        {
            var destPath = Path.Combine(destDir, Path.GetFileName(assetPath));
            if (!File.Exists(destPath))
                File.Copy(assetPath, destPath);
        }

        // The closure walks assembly REFERENCES; a native asset reachable only through the test
        // project's own runtimes/ layout (not through a closure member's) is still needed.
        CopyNativeLibraries(testBinDir, destDir);
    }

    /// <summary>
    /// Copies platform-specific native libraries from the runtimes/ subdirectory to the
    /// destination directory root, where the .NET runtime can find them via P/Invoke.
    /// </summary>
    private static void CopyNativeLibraries(string testBinDir, string destDir)
    {
        var runtimesDir = Path.Combine(testBinDir, "runtimes");
        if (!Directory.Exists(runtimesDir))
            return;

        // Determine the platform-specific runtime identifier
        string rid;
        if (OperatingSystem.IsMacOS())
            rid = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture ==
                  System.Runtime.InteropServices.Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        else if (OperatingSystem.IsLinux())
            rid = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture ==
                  System.Runtime.InteropServices.Architecture.Arm64 ? "linux-arm64" : "linux-x64";
        else
            rid = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture ==
                  System.Runtime.InteropServices.Architecture.Arm64 ? "win-arm64" : "win-x64";

        var nativeDir = Path.Combine(runtimesDir, rid, "native");
        if (!Directory.Exists(nativeDir))
            return;

        foreach (var file in Directory.GetFiles(nativeDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            if (!File.Exists(destFile))
                File.Copy(file, destFile);
        }
    }

    /// <summary>
    /// Finds an assembly DLL by checking: (1) the test output directory (sibling DLL),
    /// then (2) the project's bin directory under both Debug and Release configurations.
    /// Configuration-agnostic so tests work under both Debug and Release builds.
    /// </summary>
    protected static string? FindAssembly(string testOutputDir, string projectName, string dllName)
    {
        // 1. Check test output directory (MSBuild copies ProjectReference outputs here)
        var siblingPath = Path.Combine(testOutputDir, dllName);
        if (File.Exists(siblingPath))
            return siblingPath;

        // 2. Navigate to the project's bin directory and probe Debug/Release with multiple TFMs
        var possibleFrameworks = new[] { "net10.0", "netstandard2.1", "netstandard2.0" };
        var possibleConfigs = new[] { "Debug", "Release" };

        foreach (var config in possibleConfigs)
        {
            foreach (var tfm in possibleFrameworks)
            {
                var candidate = Path.GetFullPath(Path.Combine(
                    testOutputDir, "..", "..", "..", "..", projectName, "bin", config, tfm, dllName));
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }
}
