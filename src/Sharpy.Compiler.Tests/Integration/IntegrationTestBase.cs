using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Sharpy.Compiler.CodeGen;
using Sharpy.Compiler.Lexer;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Parser;
using Sharpy.Compiler.Project;
using Sharpy.Compiler.Semantic;
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
        public string? GeneratedCSharp { get; init; }
        public Exception? Exception { get; init; }
        public bool TimedOut { get; init; }
    }

    /// <summary>
    /// Compiles Sharpy source code to C# and executes it, returning the result.
    /// </summary>
    /// <param name="sharpySource">The Sharpy source code to compile and execute.</param>
    /// <param name="fileName">The file name to use for the source (for error messages).</param>
    /// <param name="executionTimeoutMs">Optional timeout in milliseconds for execution. Default is no timeout (0). Use for tests that may have infinite loops.</param>
    protected ExecutionResult CompileAndExecute(string sharpySource, string fileName = "test.spy", int executionTimeoutMs = 0)
    {
        // Set up assembly resolution for Sharpy.Runtime
        string? runtimePath = null;
        ResolveEventHandler? resolveHandler = null;

        try
        {
            // Phase 1: Lex Sharpy code
            var logger = new OutputTestLogger(Output);
            var lexer = new Sharpy.Compiler.Lexer.Lexer(sharpySource, logger);
            var tokens = lexer.TokenizeAll();

            // Phase 2: Parse Sharpy code
            var parser = new Sharpy.Compiler.Parser.Parser(tokens, logger);
            var module = parser.ParseModule();

            // Phase 3: Semantic analysis
            var builtinRegistry = new BuiltinRegistry();
            var symbolTable = new SymbolTable(builtinRegistry);
            var semanticInfo = new SemanticInfo();

            var nameResolver = new NameResolver(symbolTable, logger);
            nameResolver.ResolveDeclarations(module);
            nameResolver.ResolveInheritance(); // Second pass: resolve inheritance relationships

            if (nameResolver.Errors.Any())
            {
                return new ExecutionResult
                {
                    Success = false,
                    CompilationErrors = nameResolver.Errors.Select(e => e.Message).ToList()
                };
            }

            var typeResolver = new TypeResolver(symbolTable, semanticInfo, logger);
            var typeChecker = new TypeChecker(symbolTable, semanticInfo, typeResolver, logger);
            typeChecker.CheckModule(module);

            if (typeChecker.Errors.Any())
            {
                return new ExecutionResult
                {
                    Success = false,
                    CompilationErrors = typeChecker.Errors.Select(e => e.Message).ToList()
                };
            }

            // Phase 4: Generate C# code
            var codeGenContext = new CodeGenContext(symbolTable, builtinRegistry)
            {
                SourceFilePath = fileName,
                IsEntryPoint = true,  // Integration tests are executable programs
                Logger = logger
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
                    CompilationErrors = codeGenContext.Errors.ToList(),
                    GeneratedCSharp = generatedCSharp
                };
            }

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

                // Detect the current .NET version from the test assembly path
                // Path format: .../bin/Debug/net10.0/...
                var targetFramework = testDir!.Split(Path.DirectorySeparatorChar)
                    .FirstOrDefault(s => s.StartsWith("net") && char.IsDigit(s.Length > 3 ? s[3] : ' '))
                    ?? "net10.0"; // Default to net10.0

                runtimePath = Path.Combine(testDir!, "..", "..", "..", "..", "Sharpy.Core", "bin", "Debug", targetFramework, "Sharpy.Core.dll");
                runtimePath = Path.GetFullPath(runtimePath);

                if (File.Exists(runtimePath))
                {
                    references.Add(MetadataReference.CreateFromFile(runtimePath));
                    Output.WriteLine($"Loaded Sharpy.Core from: {runtimePath}");

                    // Set up assembly resolver for runtime execution
                    resolveHandler = (sender, args) =>
                    {
                        if (args.Name.StartsWith("Sharpy.Core,"))
                        {
                            return Assembly.LoadFrom(runtimePath);
                        }
                        return null;
                    };
                    AppDomain.CurrentDomain.AssemblyResolve += resolveHandler;
                }
                else
                {
                    Output.WriteLine($"Warning: Sharpy.Core not found at: {runtimePath}");
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
                    GeneratedCSharp = generatedCSharp
                };
            }

            // Phase 6: Execute the compiled assembly
            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            bool timedOut = false;
            Exception? executionException = null;

            // Lock console I/O to prevent interference from parallel tests
            lock (TestHelpers.ConsoleLock)
            {
                var originalOut = Console.Out;
                var originalErr = Console.Error;

                try
                {
                    using var outWriter = new StringWriter(stdout);
                    using var errWriter = new StringWriter(stderr);
                    Console.SetOut(outWriter);
                    Console.SetError(errWriter);

                    // Find the entry point
                    var entryPoint = assembly.EntryPoint;
                    MethodInfo? methodToInvoke = null;

                    if (entryPoint == null)
                    {
                        // Try to find a Main method or main function
                        var moduleType = assembly.GetTypes().FirstOrDefault(t => t.Name.Contains("Module"));
                        if (moduleType != null)
                        {
                            methodToInvoke = moduleType.GetMethod("Main", BindingFlags.Public | BindingFlags.Static)
                                          ?? moduleType.GetMethod("main", BindingFlags.Public | BindingFlags.Static);
                        }
                    }
                    else
                    {
                        methodToInvoke = entryPoint;
                    }

                    if (methodToInvoke != null)
                    {
                        // Execute with or without timeout
                        if (executionTimeoutMs > 0)
                        {
                            // Run with timeout using a background thread
                            var cts = new CancellationTokenSource();
                            var executionTask = Task.Run(() =>
                            {
                                methodToInvoke.Invoke(null, methodToInvoke.GetParameters().Length == 0 ? null : new object[] { Array.Empty<string>() });
                            }, cts.Token);

                            try
                            {
                                // Wait for completion or timeout
                                if (!executionTask.Wait(executionTimeoutMs))
                                {
                                    timedOut = true;
                                    cts.Cancel();
                                    // Note: We can't forcibly terminate the thread, but marking as timed out
                                    // allows the test to proceed. The thread will eventually complete or
                                    // be cleaned up when the test process exits.
                                }
                                else if (executionTask.IsFaulted && executionTask.Exception != null)
                                {
                                    executionException = executionTask.Exception.InnerException ?? executionTask.Exception;
                                }
                            }
                            catch (AggregateException ae)
                            {
                                executionException = ae.InnerException ?? ae;
                            }
                        }
                        else
                        {
                            // No timeout - execute directly
                            methodToInvoke.Invoke(null, methodToInvoke.GetParameters().Length == 0 ? null : new object[] { Array.Empty<string>() });
                        }
                    }
                }
                finally
                {
                    Console.SetOut(originalOut);
                    Console.SetError(originalErr);
                }
            }

            if (timedOut)
            {
                return new ExecutionResult
                {
                    Success = false,
                    TimedOut = true,
                    StandardOutput = stdout.ToString(),
                    StandardError = stderr.ToString(),
                    GeneratedCSharp = generatedCSharp,
                    CompilationErrors = new List<string> { $"Execution timed out after {executionTimeoutMs}ms" }
                };
            }

            if (executionException != null)
            {
                throw executionException;
            }

            return new ExecutionResult
            {
                Success = true,
                StandardOutput = stdout.ToString(),
                StandardError = stderr.ToString(),
                GeneratedCSharp = generatedCSharp
            };
        }
        catch (LexerError ex)
        {
            return new ExecutionResult
            {
                Success = false,
                CompilationErrors = new List<string> { $"Lexer error at line {ex.Line}, column {ex.Column}: {ex.Message}" }
            };
        }
        catch (ParserError ex)
        {
            return new ExecutionResult
            {
                Success = false,
                CompilationErrors = new List<string> { $"Parser error at line {ex.Line}, column {ex.Column}: {ex.Message}" }
            };
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
        finally
        {
            // Clean up assembly resolver
            if (resolveHandler != null)
            {
                AppDomain.CurrentDomain.AssemblyResolve -= resolveHandler;
            }
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
        ResolveEventHandler? resolveHandler = null;

        try
        {
            var logger = new OutputTestLogger(Output);

            // Discover all .spy files in the directory
            var sourceFiles = Directory.GetFiles(projectDir, "*.spy", SearchOption.TopDirectoryOnly)
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
                Output.WriteLine($"  - {Path.GetFileName(file)}");
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

            if (!result.Success)
            {
                return new ExecutionResult
                {
                    Success = false,
                    CompilationErrors = result.Errors,
                    GeneratedCSharp = string.Join("\n\n", result.GeneratedCSharpFiles.Select(kvp => $"// {kvp.Key}\n{kvp.Value}"))
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
                var targetFramework = testDir!.Split(Path.DirectorySeparatorChar)
                    .FirstOrDefault(s => s.StartsWith("net") && char.IsDigit(s.Length > 3 ? s[3] : ' '))
                    ?? "net10.0";

                runtimePath = Path.Combine(testDir!, "..", "..", "..", "..", "Sharpy.Core", "bin", "Debug", targetFramework, "Sharpy.Core.dll");
                runtimePath = Path.GetFullPath(runtimePath);

                if (File.Exists(runtimePath))
                {
                    references.Add(MetadataReference.CreateFromFile(runtimePath));
                    Output.WriteLine($"Loaded Sharpy.Core from: {runtimePath}");

                    resolveHandler = (sender, args) =>
                    {
                        if (args.Name.StartsWith("Sharpy.Core,"))
                        {
                            return Assembly.LoadFrom(runtimePath);
                        }
                        return null;
                    };
                    AppDomain.CurrentDomain.AssemblyResolve += resolveHandler;
                }
            }
            catch (Exception ex)
            {
                Output.WriteLine($"Warning: Failed to load Sharpy.Core: {ex.Message}");
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

            // Execute the compiled assembly
            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            bool timedOut = false;
            Exception? executionException = null;

            lock (TestHelpers.ConsoleLock)
            {
                var originalOut = Console.Out;
                var originalErr = Console.Error;

                try
                {
                    using var outWriter = new StringWriter(stdout);
                    using var errWriter = new StringWriter(stderr);
                    Console.SetOut(outWriter);
                    Console.SetError(errWriter);

                    var entryPoint = assembly.EntryPoint;
                    MethodInfo? methodToInvoke = null;

                    if (entryPoint == null)
                    {
                        var moduleType = assembly.GetTypes().FirstOrDefault(t => t.Name.Contains("Module") || t.Name == "Program");
                        if (moduleType != null)
                        {
                            methodToInvoke = moduleType.GetMethod("Main", BindingFlags.Public | BindingFlags.Static)
                                          ?? moduleType.GetMethod("main", BindingFlags.Public | BindingFlags.Static);
                        }
                    }
                    else
                    {
                        methodToInvoke = entryPoint;
                    }

                    if (methodToInvoke != null)
                    {
                        if (executionTimeoutMs > 0)
                        {
                            var cts = new CancellationTokenSource();
                            var executionTask = Task.Run(() =>
                            {
                                methodToInvoke.Invoke(null, methodToInvoke.GetParameters().Length == 0 ? null : new object[] { Array.Empty<string>() });
                            }, cts.Token);

                            try
                            {
                                if (!executionTask.Wait(executionTimeoutMs))
                                {
                                    timedOut = true;
                                    cts.Cancel();
                                }
                                else if (executionTask.IsFaulted && executionTask.Exception != null)
                                {
                                    executionException = executionTask.Exception.InnerException ?? executionTask.Exception;
                                }
                            }
                            catch (AggregateException ae)
                            {
                                executionException = ae.InnerException ?? ae;
                            }
                        }
                        else
                        {
                            methodToInvoke.Invoke(null, methodToInvoke.GetParameters().Length == 0 ? null : new object[] { Array.Empty<string>() });
                        }
                    }
                }
                finally
                {
                    Console.SetOut(originalOut);
                    Console.SetError(originalErr);
                }
            }

            if (timedOut)
            {
                return new ExecutionResult
                {
                    Success = false,
                    TimedOut = true,
                    StandardOutput = stdout.ToString(),
                    StandardError = stderr.ToString(),
                    GeneratedCSharp = string.Join("\n\n", result.GeneratedCSharpFiles.Select(kvp => $"// {kvp.Key}\n{kvp.Value}")),
                    CompilationErrors = new List<string> { $"Execution timed out after {executionTimeoutMs}ms" }
                };
            }

            if (executionException != null)
            {
                throw executionException;
            }

            return new ExecutionResult
            {
                Success = true,
                StandardOutput = stdout.ToString(),
                StandardError = stderr.ToString(),
                GeneratedCSharp = string.Join("\n\n", result.GeneratedCSharpFiles.Select(kvp => $"// {kvp.Key}\n{kvp.Value}"))
            };
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
        finally
        {
            if (resolveHandler != null)
            {
                AppDomain.CurrentDomain.AssemblyResolve -= resolveHandler;
            }
        }
    }
}
