using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Project;
using Xunit.Abstractions;

namespace Sharpy.Compiler.Tests.Helpers;

/// <summary>
/// Test helper for multi-file compilation scenarios.
/// Manages temporary directories, project files, and source files for testing.
/// </summary>
public class ProjectCompilationHelper : IDisposable
{
    private readonly string _tempDir;
    private readonly ITestOutputHelper? _output;
    private readonly ICompilerLogger _logger;
    private readonly List<string> _sourceFiles = new();
    private string? _projectFilePath;
    private bool _disposed;

    /// <summary>
    /// Gets the temporary directory path for this test project.
    /// </summary>
    public string TempDirectory => _tempDir;

    /// <summary>
    /// Gets the project directory path (defaults to temp directory).
    /// </summary>
    public string ProjectDirectory { get; private set; }

    /// <summary>
    /// Gets the source directory path (defaults to ProjectDirectory/src).
    /// </summary>
    public string SourceDirectory { get; private set; }

    /// <summary>
    /// Gets the list of source files added to the project.
    /// </summary>
    public IReadOnlyList<string> SourceFiles => _sourceFiles.AsReadOnly();

    /// <summary>
    /// Gets or sets the project configuration options.
    /// </summary>
    public ProjectOptions Options { get; set; }

    public ProjectCompilationHelper(ITestOutputHelper? output = null)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        _output = output;
        _logger = output != null ? new Tests.TestHelpers.OutputTestLogger(output) : NullLogger.Instance;

        Directory.CreateDirectory(_tempDir);
        ProjectDirectory = _tempDir;
        SourceDirectory = Path.Combine(_tempDir, "src");
        Directory.CreateDirectory(SourceDirectory);

        Options = new ProjectOptions
        {
            RootNamespace = "TestProject",
            OutputType = "exe",
            TargetFramework = "net10.0"
        };
    }

    /// <summary>
    /// Sets a custom source directory path.
    /// </summary>
    public ProjectCompilationHelper WithSourceDirectory(string relativePath)
    {
        SourceDirectory = Path.Combine(ProjectDirectory, relativePath);
        Directory.CreateDirectory(SourceDirectory);
        return this;
    }

    /// <summary>
    /// Sets the root namespace for the project.
    /// </summary>
    public ProjectCompilationHelper WithRootNamespace(string rootNamespace)
    {
        Options.RootNamespace = rootNamespace;
        return this;
    }

    /// <summary>
    /// Sets the output type (exe or library).
    /// </summary>
    public ProjectCompilationHelper WithOutputType(string outputType)
    {
        Options.OutputType = outputType;
        return this;
    }

    /// <summary>
    /// Sets the entry point file for executable projects.
    /// </summary>
    public ProjectCompilationHelper WithEntryPoint(string entryPoint)
    {
        Options.EntryPoint = entryPoint;
        return this;
    }

    /// <summary>
    /// Adds a Sharpy source file to the project.
    /// </summary>
    /// <param name="relativePath">Relative path from source directory (e.g., "main.spy" or "utils/helpers.spy")</param>
    /// <param name="content">Source code content</param>
    public ProjectCompilationHelper AddSourceFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(SourceDirectory, relativePath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, content);
        _sourceFiles.Add(fullPath);

        return this;
    }

    /// <summary>
    /// Adds multiple source files from a dictionary.
    /// </summary>
    /// <param name="files">Dictionary of relative path to content</param>
    public ProjectCompilationHelper AddSourceFiles(Dictionary<string, string> files)
    {
        foreach (var (path, content) in files)
        {
            AddSourceFile(path, content);
        }
        return this;
    }

    /// <summary>
    /// Creates a package directory with __init__.spy.
    /// </summary>
    /// <param name="packagePath">Relative path to package (e.g., "mypackage" or "utils/helpers")</param>
    /// <param name="initContent">Content for __init__.spy</param>
    public ProjectCompilationHelper AddPackage(string packagePath, string initContent = "")
    {
        var packageDir = Path.Combine(SourceDirectory, packagePath);
        Directory.CreateDirectory(packageDir);

        var initPath = Path.Combine(packageDir, "__init__.spy");
        File.WriteAllText(initPath, initContent);
        _sourceFiles.Add(initPath);

        return this;
    }

    /// <summary>
    /// Adds a source file to a package.
    /// </summary>
    /// <param name="packagePath">Package path (e.g., "mypackage")</param>
    /// <param name="fileName">File name (e.g., "module.spy")</param>
    /// <param name="content">Source code content</param>
    public ProjectCompilationHelper AddPackageFile(string packagePath, string fileName, string content)
    {
        var filePath = Path.Combine(packagePath, fileName);
        return AddSourceFile(filePath, content);
    }

    /// <summary>
    /// Creates a .spyproj project file with the configured options.
    /// </summary>
    public ProjectCompilationHelper CreateProjectFile()
    {
        _projectFilePath = Path.Combine(ProjectDirectory, $"{Options.RootNamespace}.spyproj");

        var sourceFilePattern = Options.SourceFilePattern ?? "src/**/*.spy";

        var projectContent = new StringBuilder();
        projectContent.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
        projectContent.AppendLine("<Project>");
        projectContent.AppendLine("  <PropertyGroup>");
        projectContent.AppendLine($"    <RootNamespace>{Options.RootNamespace}</RootNamespace>");
        projectContent.AppendLine($"    <OutputType>{Options.OutputType}</OutputType>");
        projectContent.AppendLine($"    <TargetFramework>{Options.TargetFramework}</TargetFramework>");

        if (!string.IsNullOrWhiteSpace(Options.AssemblyName))
        {
            projectContent.AppendLine($"    <AssemblyName>{Options.AssemblyName}</AssemblyName>");
        }

        if (!string.IsNullOrWhiteSpace(Options.EntryPoint))
        {
            projectContent.AppendLine($"    <EntryPoint>{Options.EntryPoint}</EntryPoint>");
        }

        projectContent.AppendLine("  </PropertyGroup>");
        projectContent.AppendLine("  <ItemGroup>");
        projectContent.AppendLine($"    <SourceFile Include=\"{sourceFilePattern}\" />");
        projectContent.AppendLine("  </ItemGroup>");
        projectContent.AppendLine("</Project>");

        File.WriteAllText(_projectFilePath, projectContent.ToString());

        return this;
    }

    /// <summary>
    /// Compiles the project and returns the result.
    /// </summary>
    public ProjectCompilationResult Compile()
    {
        if (_projectFilePath == null)
        {
            CreateProjectFile();
        }

        var config = ProjectFileParser.Load(_projectFilePath!);
        var compiler = new Compiler(_logger);

        _output?.WriteLine($"Compiling project: {config.RootNamespace}");
        _output?.WriteLine($"Source files: {string.Join(", ", config.SourceFiles.Select(Path.GetFileName))}");

        var result = compiler.CompileProject(config);

        if (!result.Success)
        {
            _output?.WriteLine("Compilation failed with errors:");
            foreach (var error in result.Errors)
            {
                _output?.WriteLine($"  {error}");
            }
        }
        else
        {
            _output?.WriteLine($"Compilation succeeded: {result.OutputAssemblyPath}");
        }

        return result;
    }

    /// <summary>
    /// Compiles the project and executes it, returning the execution result.
    /// </summary>
    public ExecutionResult CompileAndExecute()
    {
        var compilationResult = Compile();

        if (!compilationResult.Success)
        {
            return new ExecutionResult
            {
                Success = false,
                CompilationErrors = compilationResult.Errors,
                StandardOutput = string.Empty,
                StandardError = string.Empty
            };
        }

        return ExecuteAssembly(compilationResult.OutputAssemblyPath!);
    }

    /// <summary>
    /// Executes a compiled assembly and captures output.
    /// </summary>
    private ExecutionResult ExecuteAssembly(string assemblyPath)
    {
        try
        {
            var assembly = Assembly.LoadFrom(assemblyPath);
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

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

                    var entryPoint = assembly.EntryPoint;
                    if (entryPoint == null)
                    {
                        var moduleTypes = assembly.GetTypes().Where(t => t.Name.Contains("Module")).ToList();
                        if (moduleTypes.Any())
                        {
                            var mainMethod = moduleTypes
                                .Select(t => t.GetMethod("Main", BindingFlags.Public | BindingFlags.Static))
                                .FirstOrDefault(m => m != null);

                            if (mainMethod != null)
                            {
                                mainMethod.Invoke(null, mainMethod.GetParameters().Length == 0
                                    ? null
                                    : new object[] { Array.Empty<string>() });
                            }
                            else
                            {
                                return new ExecutionResult
                                {
                                    Success = false,
                                    CompilationErrors = new List<string> { "No Main entry point found in assembly" },
                                    StandardOutput = string.Empty,
                                    StandardError = string.Empty
                                };
                            }
                        }
                    }
                    else
                    {
                        entryPoint.Invoke(null, entryPoint.GetParameters().Length == 0
                            ? null
                            : new object[] { Array.Empty<string>() });
                    }
                }
                finally
                {
                    Console.SetOut(originalOut);
                    Console.SetError(originalErr);
                }
            }

            var stdoutStr = stdout.ToString();
            var stderrStr = stderr.ToString();

            _output?.WriteLine($"=== EXECUTION OUTPUT ===");
            _output?.WriteLine(stdoutStr);
            if (!string.IsNullOrEmpty(stderrStr))
            {
                _output?.WriteLine($"=== STDERR ===");
                _output?.WriteLine(stderrStr);
            }

            return new ExecutionResult
            {
                Success = true,
                StandardOutput = stdoutStr,
                StandardError = stderrStr,
                CompilationErrors = new List<string>()
            };
        }
        catch (Exception ex)
        {
            _output?.WriteLine($"Execution failed: {ex.Message}");

            return new ExecutionResult
            {
                Success = false,
                CompilationErrors = new List<string> { $"Execution failed: {ex.Message}" },
                StandardOutput = string.Empty,
                StandardError = string.Empty,
                Exception = ex
            };
        }
    }

    /// <summary>
    /// Asserts that the compilation succeeded.
    /// </summary>
    public ProjectCompilationResult AssertCompilationSucceeded(ProjectCompilationResult result)
    {
        if (!result.Success)
        {
            var errorMessage = $"Compilation failed with {result.Errors.Count} error(s):\n" +
                             string.Join("\n", result.Errors);
            throw new Xunit.Sdk.XunitException(errorMessage);
        }
        return result;
    }

    /// <summary>
    /// Asserts that the compilation failed with expected errors.
    /// </summary>
    public ProjectCompilationResult AssertCompilationFailed(ProjectCompilationResult result, string? expectedErrorPattern = null)
    {
        if (result.Success)
        {
            throw new Xunit.Sdk.XunitException("Expected compilation to fail, but it succeeded");
        }

        if (expectedErrorPattern != null && !result.Errors.Any(e => e.Contains(expectedErrorPattern)))
        {
            throw new Xunit.Sdk.XunitException(
                $"Expected error containing '{expectedErrorPattern}', but got:\n" +
                string.Join("\n", result.Errors));
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _output?.WriteLine($"Warning: Failed to cleanup temp directory: {ex.Message}");
        }

        _disposed = true;
    }
}

/// <summary>
/// Configuration options for test projects.
/// </summary>
public class ProjectOptions
{
    public string RootNamespace { get; set; } = "TestProject";
    public string OutputType { get; set; } = "exe";
    public string TargetFramework { get; set; } = "net10.0";
    public string? AssemblyName { get; set; }
    public string? EntryPoint { get; set; }
    public string? SourceFilePattern { get; set; }
}

/// <summary>
/// Result of executing a compiled assembly.
/// </summary>
public class ExecutionResult
{
    public bool Success { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
    public List<string> CompilationErrors { get; init; } = new();
    public Exception? Exception { get; init; }
}
