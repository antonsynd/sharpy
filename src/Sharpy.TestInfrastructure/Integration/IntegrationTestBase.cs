using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Sharpy.Compiler;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit.Abstractions;
using static Sharpy.TestInfrastructure.TestHelpers;

namespace Sharpy.TestInfrastructure.Integration;

/// <summary>
/// Base class for end-to-end integration tests that compile Sharpy code to C# and execute it.
/// </summary>
public abstract class IntegrationTestBase
{
    protected readonly ITestOutputHelper Output;

    private static readonly Lazy<(IReadOnlyList<MetadataReference> References, string? RuntimePath)> SharedReferences =
        new(BuildSharedReferences);

    private static readonly Lazy<CSharpCompilation> SharedBaseCompilation =
        new(() => CSharpCompilation.Create(
            "SharpyTestAssembly",
            Array.Empty<SyntaxTree>(),
            SharedReferences.Value.References,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication)));

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
        };

        string? runtimePath = null;
        var testAssemblyPath = Assembly.GetExecutingAssembly().Location;
        var testDir = Path.GetDirectoryName(testAssemblyPath);
        var possibleFrameworks = new[] { "net10.0", "netstandard2.1", "netstandard2.0" };

        foreach (var targetFramework in possibleFrameworks)
        {
            var candidate = Path.GetFullPath(Path.Combine(testDir!, "..", "..", "..", "..", "Sharpy.Core", "bin", "Debug", targetFramework, "Sharpy.Core.dll"));
            if (File.Exists(candidate))
            {
                references.Add(MetadataReference.CreateFromFile(candidate));
                runtimePath = candidate;

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
                break;
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
    protected ExecutionResult CompileAndExecuteWithGC(string sharpySource, string fileName = "test.spy", int executionTimeoutMs = 0)
    {
        var result = CompileAndExecute(sharpySource, fileName, executionTimeoutMs);
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
    protected ExecutionResult CompileAndExecute(string sharpySource, string fileName = "test.spy", int executionTimeoutMs = 0)
    {
        try
        {
            return CompileAndExecuteCore(sharpySource, fileName, executionTimeoutMs);
        }
        finally
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
        }
    }

    private ExecutionResult CompileAndExecuteCore(string sharpySource, string fileName, int executionTimeoutMs)
    {
        // Track path to Sharpy.Core for copying to temp execution directory
        string? runtimePath = null;

        try
        {
            // Phase 1: Lex Sharpy code
            var logger = new OutputTestLogger(Output);
            var lexer = new Sharpy.Compiler.Lexer.Lexer(sharpySource, logger);
            var tokens = lexer.TokenizeAll();

            // Check for lexer errors collected via DiagnosticBag
            if (lexer.Diagnostics.HasErrors)
            {
                return new ExecutionResult
                {
                    Success = false,
                    CompilationErrors = lexer.Diagnostics.GetErrors().Select(d => d.Message).ToList(),
                    RawDiagnostics = lexer.Diagnostics.GetAll().ToList()
                };
            }

            // Phase 2: Parse Sharpy code
            var parser = new Sharpy.Compiler.Parser.Parser(tokens, logger);
            var module = parser.ParseModule();

            // Check for parser errors collected via DiagnosticBag
            if (parser.Diagnostics.HasErrors)
            {
                return new ExecutionResult
                {
                    Success = false,
                    CompilationErrors = parser.Diagnostics.GetErrors().Select(d => d.Message).ToList(),
                    RawDiagnostics = parser.Diagnostics.GetAll().ToList()
                };
            }

            // Phase 3: Semantic analysis
            var builtinRegistry = new BuiltinRegistry();
            var symbolTable = new SymbolTable(builtinRegistry);
            var semanticInfo = new SemanticInfo();
            var semanticBinding = new SemanticBinding();
            var moduleRegistry = new ModuleRegistry(logger);

            moduleRegistry.LoadReference(SharpyCoreReference.Location);
            foreach (var additionalPath in GetAdditionalReferenceAssemblyPaths())
                moduleRegistry.LoadReference(additionalPath);

            var nameResolver = new NameResolver(symbolTable, logger, semanticBinding);
            nameResolver.ResolveDeclarations(module);

            // Phase 3a: Resolve imports to register .NET types before inheritance resolution
            var importResolver = new ImportResolver(logger, moduleRegistry,
                semanticBinding: semanticBinding);
            importResolver.ResolveAllImports(module, symbolTable, null);

            nameResolver.ResolveInheritance(); // Second pass: resolve inheritance relationships

            // Materialize inheritance onto Symbol properties and freeze
            semanticBinding.MaterializeInheritance();
            semanticBinding.FreezeInheritance();

            if (nameResolver.Diagnostics.HasErrors)
            {
                return new ExecutionResult
                {
                    Success = false,
                    CompilationErrors = nameResolver.Diagnostics.GetErrors().Select(d => d.Message).ToList(),
                    RawDiagnostics = nameResolver.Diagnostics.GetAll().ToList()
                };
            }

            // Collect import errors but continue to type checking so users see all errors
            var allErrors = new List<string>();
            if (importResolver.Diagnostics.HasErrors)
            {
                allErrors.AddRange(importResolver.Diagnostics.GetErrors().Select(d => d.Message));
            }

            var typeResolver = new TypeResolver(symbolTable, semanticInfo, logger);
            var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, logger)
            {
                SemanticBinding = semanticBinding,
                CurrentFilePath = fileName
            };
            // Integration tests are executable programs, so they're entry points
            try
            {
                typeChecker.CheckModule(module, computeCodeGenInfo: true, isEntryPoint: true);
            }
            catch (SemanticAnalysisException)
            {
                // MaxErrors exceeded — collect whatever diagnostics exist
            }

            // Materialize CodeGenInfo and VariableType onto Symbol properties and freeze
            semanticBinding.MaterializeCodeGenInfo();
            semanticBinding.MaterializeVariableTypes();
            semanticBinding.FreezeVariableTypes();
            semanticBinding.FreezeCodeGenInfo();

            // Collect warnings, hints, and info notes from all phases.
            // Hints are advisory diagnostics (e.g., transition hints SPY0470+) and Info diagnostics
            // (e.g., SPY1010 functools.partial placeholder hint) share suppression with warnings;
            // we surface them via the same channel so .warning fixtures can verify behavioral notes.
            var compilationWarnings = typeChecker.Diagnostics.GetAll()
                .Where(d => d.Severity == CompilerDiagnosticSeverity.Warning
                         || d.Severity == CompilerDiagnosticSeverity.Hint
                         || d.Severity == CompilerDiagnosticSeverity.Info)
                .Select(d => d.Message)
                .ToList();
            compilationWarnings.AddRange(
                parser.Diagnostics.GetAll()
                    .Where(d => d.Severity == CompilerDiagnosticSeverity.Warning
                             || d.Severity == CompilerDiagnosticSeverity.Hint
                             || d.Severity == CompilerDiagnosticSeverity.Info)
                    .Select(d => d.Message));

            if (typeChecker.Diagnostics.HasErrors)
            {
                allErrors.AddRange(typeChecker.Diagnostics.GetErrors().Select(e => e.Message));
            }

            if (allErrors.Count > 0)
            {
                var rawDiags = new List<CompilerDiagnostic>();
                rawDiags.AddRange(importResolver.Diagnostics.GetAll());
                rawDiags.AddRange(typeChecker.Diagnostics.GetAll());
                return new ExecutionResult
                {
                    Success = false,
                    CompilationErrors = allErrors,
                    CompilationWarnings = compilationWarnings,
                    RawDiagnostics = rawDiags
                };
            }

            // Phase 4: Generate C# code
            var codeGenContext = new CodeGenContext(symbolTable, builtinRegistry)
            {
                SourceFilePath = fileName,
                IsEntryPoint = true,  // Integration tests are executable programs
                Logger = logger,
                SemanticInfo = semanticInfo,
                SemanticBinding = semanticBinding
            };
            var emitter = new RoslynEmitter(codeGenContext);
            var compilationUnit = emitter.GenerateCompilationUnit(module);
            var generatedCSharp = compilationUnit.ToFullString();

            Output.WriteLine("=== Generated C# ===");
            Output.WriteLine(generatedCSharp);
            Output.WriteLine("====================");

            // Check for code generation errors
            if (codeGenContext.HasErrors)
            {
                return new ExecutionResult
                {
                    Success = false,
                    CompilationErrors = codeGenContext.Diagnostics.GetErrors().Select(d => d.Message).ToList(),
                    GeneratedCSharp = generatedCSharp,
                    RawDiagnostics = codeGenContext.Diagnostics.GetAll().ToList()
                };
            }

            // Collect codegen warnings and info diagnostics (e.g., SPY1001 implicit interface synthesis)
            compilationWarnings.AddRange(
                codeGenContext.Diagnostics.GetAll()
                    .Where(d => d.Severity == CompilerDiagnosticSeverity.Warning || d.Severity == CompilerDiagnosticSeverity.Info)
                    .Select(d => d.Message));

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
                    CopyTransitiveDependencies(testBinDir, tempDir);
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
    protected ExecutionResult CompileAndExecuteProject(string projectDir, string entryPointFile, int executionTimeoutMs = 0)
    {
        try
        {
            return CompileAndExecuteProjectCore(projectDir, entryPointFile, executionTimeoutMs);
        }
        finally
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
        }
    }

    private ExecutionResult CompileAndExecuteProjectCore(string projectDir, string entryPointFile, int executionTimeoutMs)
    {
        string? runtimePath = null;

        try
        {
            var logger = new OutputTestLogger(Output);

            // Discover all .spy files in the directory (including subdirectories for packages)
            var sourceFiles = Directory.GetFiles(projectDir, "*.spy", SearchOption.AllDirectories)
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
                TargetFramework = "net10.0"
            };

            // Compile the project
            var projectCompiler = new ProjectCompiler(logger);
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
                    GeneratedCSharp = string.Join("\n\n", result.GeneratedCSharpFiles.Select(kvp => $"// {kvp.Key}\n{kvp.Value}")),
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

            // Parse and compile the generated C#
            var syntaxTrees = result.GeneratedCSharpFiles.Values
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
                    GeneratedCSharp = string.Join("\n\n", result.GeneratedCSharpFiles.Select(kvp => $"// {kvp.Key}\n{kvp.Value}"))
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
                    CopyTransitiveDependencies(testBinDir, tempDir);
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
                        GeneratedCSharp = string.Join("\n\n", result.GeneratedCSharpFiles.Select(kvp => $"// {kvp.Key}\n{kvp.Value}")),
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
                        GeneratedCSharp = string.Join("\n\n", result.GeneratedCSharpFiles.Select(kvp => $"// {kvp.Key}\n{kvp.Value}")),
                        CompilationErrors = new List<string> { $"Process exited with code {process.ExitCode}: {stderr}" }
                    };
                }

                return new ExecutionResult
                {
                    Success = true,
                    StandardOutput = stdout.ToString(),
                    StandardError = stderr.ToString(),
                    GeneratedCSharp = string.Join("\n\n", result.GeneratedCSharpFiles.Select(kvp => $"// {kvp.Key}\n{kvp.Value}")),
                    CompilationWarnings = projectWarnings
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
    /// Copies transitive dependencies of Sharpy.Core (e.g., Microsoft.Data.Sqlite and its
    /// native SQLite libraries) from the test project's output directory to the temp execution
    /// directory. Skips assemblies already present and framework assemblies.
    /// Also copies native libraries (e.g., libe_sqlite3.dylib) directly to the temp directory
    /// so the .NET runtime can find them without a .deps.json probing configuration.
    /// </summary>
    private static void CopyTransitiveDependencies(string testBinDir, string destDir)
    {
        // Managed DLLs that are transitive dependencies of Sharpy.Core but not part of the
        // .NET runtime. These must be present next to the compiled test assembly.
        string[] transitiveDeps = new[]
        {
            "Microsoft.Data.Sqlite.dll",
            "SQLitePCLRaw.batteries_v2.dll",
            "SQLitePCLRaw.core.dll",
            "SQLitePCLRaw.provider.e_sqlite3.dll",
            // Math.NET Numerics — required by numpy.linalg / numpy.fft submodules.
            "MathNet.Numerics.dll",
        };

        foreach (var dllName in transitiveDeps)
        {
            var srcPath = Path.Combine(testBinDir, dllName);
            if (File.Exists(srcPath))
            {
                var destPath = Path.Combine(destDir, dllName);
                if (!File.Exists(destPath))
                    File.Copy(srcPath, destPath);
            }
        }

        // Copy native runtime libraries directly into the temp directory.
        // Without a .deps.json, the runtime won't probe the runtimes/ subdirectory,
        // so we find the platform-specific native library and place it at the root.
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
}
