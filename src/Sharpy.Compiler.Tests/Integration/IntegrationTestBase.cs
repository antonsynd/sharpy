using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Diagnostics;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;
using Sharpy.Compiler.Semantic.Registry;
using Xunit.Abstractions;
using static Sharpy.Compiler.Tests.TestHelpers;

namespace Sharpy.Compiler.Tests.Integration;

/// <summary>
/// Base class for end-to-end integration tests that compile Sharpy code to C# and execute it.
/// </summary>
public abstract class IntegrationTestBase
{
    protected readonly ITestOutputHelper Output;

    protected IntegrationTestBase(ITestOutputHelper output)
    {
        Output = output;
    }

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
    /// Compiles Sharpy source code to C# and executes it, returning the result.
    /// </summary>
    /// <param name="sharpySource">The Sharpy source code to compile and execute.</param>
    /// <param name="fileName">The file name to use for the source (for error messages).</param>
    /// <param name="executionTimeoutMs">Optional timeout in milliseconds for execution. Default is no timeout (0). Use for tests that may have infinite loops.</param>
    protected ExecutionResult CompileAndExecute(string sharpySource, string fileName = "test.spy", int executionTimeoutMs = 0)
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

            var nameResolver = new NameResolver(symbolTable, logger, semanticBinding);
            nameResolver.ResolveDeclarations(module);

            // Phase 3a: Resolve imports to register .NET types before inheritance resolution
            var importResolver = new ImportResolver(logger, moduleRegistry);
            importResolver.SetSemanticBinding(semanticBinding);
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
                SemanticBinding = semanticBinding
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

            // Collect warnings from type checking and validation pipeline
            var compilationWarnings = typeChecker.Diagnostics.GetWarnings().Select(w => w.Message).ToList();

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

            // Get references to required assemblies
            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
            };

            // Try to add Sharpy.Core reference if available
            try
            {
                // Find Sharpy.Core.dll in the build output directory
                var testAssemblyPath = Assembly.GetExecutingAssembly().Location;
                var testDir = Path.GetDirectoryName(testAssemblyPath);

                // Try to find Sharpy.Core.dll in multiple possible locations
                // Sharpy.Core targets netstandard2.1 and netstandard2.0
                var possibleFrameworks = new[] { "netstandard2.1", "netstandard2.0" };
                bool found = false;

                foreach (var targetFramework in possibleFrameworks)
                {
                    runtimePath = Path.Combine(testDir!, "..", "..", "..", "..", "Sharpy.Core", "bin", "Debug", targetFramework, "Sharpy.Core.dll");
                    runtimePath = Path.GetFullPath(runtimePath);

                    if (File.Exists(runtimePath))
                    {
                        references.Add(MetadataReference.CreateFromFile(runtimePath));
                        Output.WriteLine($"Loaded Sharpy.Core from: {runtimePath}");

                        // Add netstandard reference for netstandard libraries
                        try
                        {
                            var netstandardAssembly = Assembly.Load("netstandard");
                            references.Add(MetadataReference.CreateFromFile(netstandardAssembly.Location));
                        }
                        catch
                        {
                            // Fallback: try to find netstandard.dll in runtime directory
                            var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location);
                            var netstandardPath = Path.Combine(runtimeDir!, "netstandard.dll");
                            if (File.Exists(netstandardPath))
                            {
                                references.Add(MetadataReference.CreateFromFile(netstandardPath));
                            }
                        }

                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    Output.WriteLine($"Warning: Sharpy.Core not found in any expected location");
                }
            }
            catch (FileNotFoundException ex)
            {
                // Sharpy.Core not available, continue without it
                Output.WriteLine($"Warning: Failed to load Sharpy.Core: {ex.Message}");
            }
            catch (DirectoryNotFoundException ex)
            {
                Output.WriteLine($"Warning: Failed to load Sharpy.Core: {ex.Message}");
            }
            catch (BadImageFormatException ex)
            {
                Output.WriteLine($"Warning: Failed to load Sharpy.Core: {ex.Message}");
            }

            var compilation = CSharpCompilation.Create(
                "SharpyTestAssembly",
                new[] { syntaxTree },
                references,
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

            // Collect warnings from the project compilation
            var projectWarnings = result.Diagnostics.GetWarnings().Select(d => d.Message).ToList();

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

            // Get references to required assemblies
            var references = new List<MetadataReference>
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
            };

            // Try to add Sharpy.Core reference
            try
            {
                var testAssemblyPath = Assembly.GetExecutingAssembly().Location;
                var testDir = Path.GetDirectoryName(testAssemblyPath);

                // Sharpy.Core now targets netstandard2.1/2.0, not net10.0
                var possibleFrameworks = new[] { "netstandard2.1", "netstandard2.0" };
                foreach (var framework in possibleFrameworks)
                {
                    runtimePath = Path.Combine(testDir!, "..", "..", "..", "..", "Sharpy.Core", "bin", "Debug", framework, "Sharpy.Core.dll");
                    runtimePath = Path.GetFullPath(runtimePath);

                    if (File.Exists(runtimePath))
                    {
                        references.Add(MetadataReference.CreateFromFile(runtimePath));
                        Output.WriteLine($"Loaded Sharpy.Core from: {runtimePath}");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Output.WriteLine($"Warning: Failed to load Sharpy.Core: {ex.Message}");
            }

            // Add netstandard reference (required for Sharpy.Core which targets netstandard)
            try
            {
                var netstandardAssembly = Assembly.Load("netstandard");
                references.Add(MetadataReference.CreateFromFile(netstandardAssembly.Location));
            }
            catch
            {
                // Fallback: try to find in runtime directory
                var coreLibPath = typeof(object).Assembly.Location;
                var coreLibDir = Path.GetDirectoryName(coreLibPath);
                if (!string.IsNullOrEmpty(coreLibDir))
                {
                    var netstandardPath = Path.Combine(coreLibDir, "netstandard.dll");
                    if (File.Exists(netstandardPath))
                    {
                        references.Add(MetadataReference.CreateFromFile(netstandardPath));
                    }
                }
            }

            var compilation = CSharpCompilation.Create(
                "SharpyTestProject",
                syntaxTrees,
                references,
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
    /// Resolves imports in a module and registers imported symbols in the symbol table.
    /// This is needed before inheritance resolution so that .NET base classes can be found.
    /// </summary>
    private void ResolveImports(Sharpy.Compiler.Parser.Ast.Module module, ImportResolver importResolver, SymbolTable symbolTable)
    {
        foreach (var statement in module.Body)
        {
            if (statement is Sharpy.Compiler.Parser.Ast.FromImportStatement fromImport)
            {
                var moduleInfo = importResolver.ResolveFromImport(fromImport);
                if (moduleInfo != null)
                {
                    // Register imported symbols in the symbol table
                    if (fromImport.ImportAll)
                    {
                        foreach (var (name, symbol) in moduleInfo.ExportedSymbols)
                        {
                            symbolTable.TryDefine(symbol);
                        }
                    }
                    else
                    {
                        foreach (var importAlias in fromImport.Names)
                        {
                            var symbolName = importAlias.AsName ?? importAlias.Name;
                            if (moduleInfo.ExportedSymbols.TryGetValue(importAlias.Name, out var symbol))
                            {
                                // If aliased, create a new symbol with the alias name
                                if (importAlias.AsName != null && symbol is TypeSymbol typeSymbol)
                                {
                                    symbol = typeSymbol with { Name = importAlias.AsName };
                                }
                                symbolTable.TryDefine(symbol);
                            }
                        }
                    }
                }
            }
            else if (statement is Sharpy.Compiler.Parser.Ast.ImportStatement import)
            {
                var modules = importResolver.ResolveImport(import);
                foreach (var moduleInfo in modules)
                {
                    if (moduleInfo == null)
                        continue;
                    // For regular imports, the module itself is the symbol
                    var moduleSymbol = new ModuleSymbol
                    {
                        Name = moduleInfo.Path,
                        Kind = Sharpy.Compiler.Semantic.SymbolKind.Module,
                        FilePath = moduleInfo.Path,
                        Exports = new Dictionary<string, Symbol>(moduleInfo.ExportedSymbols)
                    };
                    symbolTable.TryDefine(moduleSymbol);
                }
            }
        }
    }
}
