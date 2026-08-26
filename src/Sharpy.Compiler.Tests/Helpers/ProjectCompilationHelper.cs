using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Project;
using Xunit.Abstractions;

using Sharpy.TestInfrastructure;

namespace Sharpy.Compiler.Tests.Helpers;

/// <summary>
/// Test helper for multi-file compilation scenarios.
/// Manages temporary directories, project files, and source files for testing.
/// </summary>
public class ProjectCompilationHelper : IDisposable
{
    private readonly string _tempDir;
    private readonly ITestOutputHelper? _output;
    private readonly CapturingCompilerLogger _logger;
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

    /// <summary>
    /// Gets or sets whether to use incremental compilation mode.
    /// </summary>
    public bool Incremental { get; set; }

    /// <summary>
    /// The result of the most recent <see cref="Compile()"/> — including the one
    /// <see cref="CompileAndExecute"/> performs internally, so a test can assert on both the
    /// program's output and the build's incremental behaviour without compiling a third time.
    /// </summary>
    public ProjectCompilationResult? LastCompilationResult { get; private set; }

    /// <summary>
    /// Feature flags to supply on the CLI side (via <see cref="CompilerOptions.Features"/>),
    /// to be merged with the project's &lt;Features&gt;. Mirrors <c>--enable-feature</c>.
    /// </summary>
    public List<string> CliFeatures { get; } = new();

    public ProjectCompilationHelper(ITestOutputHelper? output = null)
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_test_{Guid.NewGuid()}");
        _output = output;
        _logger = new CapturingCompilerLogger(
            output != null ? new TestHelpers.OutputTestLogger(output) : NullLogger.Instance);

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

    private bool _includeRuntimeReferences;

    /// <summary>
    /// Emits &lt;Reference&gt; items for the Sharpy runtime DLLs (Sharpy.Core.dll, Sharpy.Stdlib.dll)
    /// resolved next to the test assembly, so module discovery can resolve `from sharpy.generators
    /// import ...` and other runtime-backed modules in the unit-test harness (the CLI gets these via
    /// its ProjectReference). Mirrors TestProjectScaffold.BuildRuntimeReferences.
    /// </summary>
    public ProjectCompilationHelper WithRuntimeReferences()
    {
        _includeRuntimeReferences = true;
        return this;
    }

    private static string BuildRuntimeReferenceItems()
    {
        // DLL list and resolution shared with the compiler's own scaffold so the two
        // can't drift (#1090).
        var refs = new StringBuilder();
        foreach (var path in TestProjectScaffold.ResolveRuntimeDllPaths())
        {
            refs.AppendLine($"    <Reference Include=\"{Path.GetFileNameWithoutExtension(path)}\">");
            refs.AppendLine($"      <HintPath>{path}</HintPath>");
            refs.AppendLine("    </Reference>");
        }

        return refs.ToString();
    }

    /// <summary>
    /// Adds a Sharpy source file to the project.
    /// Entry point files are automatically wrapped in a main() function if needed.
    /// </summary>
    /// <param name="relativePath">Relative path from source directory (e.g., "main.spy" or "utils/helpers.spy")</param>
    /// <param name="content">Source code content</param>
    /// <param name="isEntryPoint">Whether this file is the entry point (defaults to checking if it matches Options.EntryPoint)</param>
    public ProjectCompilationHelper AddSourceFile(string relativePath, string content, bool? isEntryPoint = null)
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
    /// Updates an existing source file's content.
    /// Useful for testing incremental compilation scenarios.
    /// </summary>
    /// <param name="relativePath">Relative path from source directory (e.g., "main.spy" or "utils/helpers.spy")</param>
    /// <param name="content">New source code content</param>
    public ProjectCompilationHelper UpdateSourceFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(SourceDirectory, relativePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Source file not found: {fullPath}");
        }

        File.WriteAllText(fullPath, content);
        return this;
    }

    /// <summary>
    /// Removes a source file from the project.
    /// Useful for testing incremental compilation scenarios where a file is deleted.
    /// </summary>
    /// <param name="relativePath">Relative path from source directory (e.g., "lib.spy")</param>
    public ProjectCompilationHelper RemoveSourceFile(string relativePath)
    {
        var fullPath = Path.Combine(SourceDirectory, relativePath);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _sourceFiles.Remove(fullPath);
        }

        return this;
    }

    /// <summary>
    /// Enables incremental compilation mode.
    /// </summary>
    public ProjectCompilationHelper WithIncremental(bool enabled = true)
    {
        Incremental = enabled;
        return this;
    }

    /// <summary>
    /// Clears the incremental compilation cache.
    /// </summary>
    public ProjectCompilationHelper ClearCache()
    {
        var objDir = Path.Combine(ProjectDirectory, "obj", "Debug");
        if (Directory.Exists(objDir))
        {
            var cacheFile = Path.Combine(objDir, ".sharpy-cache");
            var symbolCacheFile = Path.Combine(objDir, ".sharpy-symbols");

            if (File.Exists(cacheFile))
                File.Delete(cacheFile);
            if (File.Exists(symbolCacheFile))
                File.Delete(symbolCacheFile);
        }
        return this;
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

        if (Options.Features.Count > 0)
        {
            projectContent.AppendLine($"    <Features>{string.Join(";", Options.Features)}</Features>");
        }

        if (Options.WarningsAsErrors)
        {
            projectContent.AppendLine("    <WarningsAsErrors>true</WarningsAsErrors>");
        }

        projectContent.AppendLine("  </PropertyGroup>");
        projectContent.AppendLine("  <ItemGroup>");
        projectContent.AppendLine($"    <SourceFile Include=\"{sourceFilePattern}\" />");
        projectContent.AppendLine("  </ItemGroup>");

        if (_includeRuntimeReferences)
        {
            var runtimeReferences = BuildRuntimeReferenceItems();
            if (!string.IsNullOrEmpty(runtimeReferences))
            {
                projectContent.AppendLine("  <ItemGroup>");
                projectContent.Append(runtimeReferences);
                projectContent.AppendLine("  </ItemGroup>");
            }
        }

        projectContent.AppendLine("</Project>");

        File.WriteAllText(_projectFilePath, projectContent.ToString());

        return this;
    }

    /// <summary>
    /// Compiles the project and returns the result.
    /// </summary>
    public ProjectCompilationResult Compile() => Compile(reorderSourceFiles: null);

    /// <summary>
    /// Compiles the project, optionally reordering the loaded source files first.
    /// </summary>
    /// <param name="reorderSourceFiles">
    /// Optional hook applied AFTER <see cref="ProjectFileParser.Load"/> re-globs. It
    /// receives the loaded source-file list (already ordinal-sorted by the loader, #1032)
    /// and must return a permutation of exactly those paths. This is the only way to drive
    /// compilation with a non-sorted order now that the loader sorts, so determinism tests
    /// can prove the compile-entry defensive sort neutralizes any input order (#1036).
    /// </param>
    public ProjectCompilationResult Compile(Func<IReadOnlyList<string>, IReadOnlyList<string>>? reorderSourceFiles)
    {
        if (_projectFilePath == null)
        {
            CreateProjectFile();
        }

        var config = ProjectFileParser.Load(_projectFilePath!);

        if (reorderSourceFiles != null)
        {
            var reordered = reorderSourceFiles(config.SourceFiles.ToList());
            var original = new HashSet<string>(config.SourceFiles, StringComparer.Ordinal);
            if (reordered.Count != config.SourceFiles.Count || !reordered.All(original.Contains))
            {
                throw new ArgumentException(
                    "reorderSourceFiles must return a permutation of the loaded source files.");
            }

            // SourceFiles is init-only but its backing list is mutable; replace contents
            // in place so the reordered set flows into ProjectModel.Units insertion order.
            config.SourceFiles.Clear();
            config.SourceFiles.AddRange(reordered);
        }

        var compilerOptions = new CompilerOptions
        {
            Incremental = Incremental,
            Features = Sharpy.Compiler.Shared.FeatureFlags.None.Enable(CliFeatures)
        };
        var compiler = new Compiler(compilerOptions, _logger);

        // Each Compile() is one build; AssertIncrementalSkipped reads the most recent one.
        _logger.Clear();

        _output?.WriteLine($"Compiling project: {config.RootNamespace} (incremental={Incremental})");
        _output?.WriteLine($"Source files: {string.Join(", ", config.SourceFiles.Select(Path.GetFileName))}");

        var result = compiler.CompileProject(config);
        LastCompilationResult = result;

        if (!result.Success)
        {
            _output?.WriteLine("Compilation failed with errors:");
            foreach (var error in result.Diagnostics.GetErrors().Select(d => d.Message))
            {
                _output?.WriteLine($"  {error}");
            }
        }
        else
        {
            _output?.WriteLine($"Compilation succeeded: {result.OutputAssemblyPath}");
            if (result.Metrics != null)
            {
                _output?.WriteLine($"  Skipped files: {result.Metrics.SkippedFileCount}");
            }
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
                CompilationErrors = compilationResult.Diagnostics.GetErrors().Select(d => d.Message).ToList(),
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
            var errors = result.Diagnostics.GetErrors();
            var errorMessage = $"Compilation failed with {errors.Count} error(s):\n" +
                             string.Join("\n", errors.Select(e => e.ToString()));
            throw new Xunit.Sdk.XunitException(errorMessage);
        }
        return result;
    }

    /// <summary>
    /// Asserts that a build ran in incremental mode and reused the cache for exactly
    /// <paramref name="skippedFiles"/> — i.e. that this was a genuine <b>warm-cache</b> build.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This helper, together with the two-build pattern it is used in — build once to populate
    /// <c>obj/{Configuration}/.sharpy-symbols</c>, make a <b>real content edit</b> to one file
    /// (never a touch/no-op edit, which the SHA-256 hash cache ignores), then build again — is the
    /// warm-cache harness defined by plan-058a93 Phase 5.4 and referenced by #1287 (Batch E).
    /// Use it whenever a fact must hold on a build where a dependency's symbols come from the cache
    /// rather than from a fresh parse: a test that only compiles twice proves nothing unless it also
    /// proves the second build actually skipped the file it claims was cache-served.
    /// </para>
    /// <para>
    /// Two independent channels are checked, because either alone can pass vacuously: the
    /// <c>"Incremental mode: N file(s) to compile, M skipped (unchanged)"</c> line the compiler logs
    /// (captured via <see cref="CapturingCompilerLogger"/>) gives the counts, and
    /// <c>result.Metrics.SkippedFiles</c> gives the identities.
    /// </para>
    /// </remarks>
    /// <param name="result">The result of the warm build (the second <see cref="Compile()"/> call).</param>
    /// <param name="skippedFiles">
    /// File names (e.g. <c>"lib.spy"</c>) expected to have been served from the cache. The count is
    /// asserted too, so passing none asserts that nothing was skipped.
    /// </param>
    public ProjectCompilationResult AssertIncrementalSkipped(
        ProjectCompilationResult result,
        params string[] skippedFiles)
    {
        AssertCompilationSucceeded(result);
        return AssertWarmBuildSkipped(result, skippedFiles);
    }

    /// <summary>
    /// The warm-build proof of <see cref="AssertIncrementalSkipped"/> without the success assertion:
    /// for a build that is EXPECTED to fail — a warm/cold differential whose cold arm refuses and
    /// whose warm arm must refuse identically (#1568) — this is the half that proves the refusal was
    /// measured on cache-served symbols and not on a silent full rebuild.
    /// </summary>
    public ProjectCompilationResult AssertWarmBuildSkipped(
        ProjectCompilationResult result,
        params string[] skippedFiles)
    {
        if (!Incremental)
        {
            throw new Xunit.Sdk.XunitException(
                "AssertIncrementalSkipped requires incremental mode; call WithIncremental() before Compile().");
        }

        var modeLine = _logger.InfoMessages
            .LastOrDefault(m => m.StartsWith("Incremental mode:", StringComparison.Ordinal));

        if (modeLine == null)
        {
            throw new Xunit.Sdk.XunitException(
                "The build did not report an incremental mode line, so no file could have been " +
                "cache-served. Captured info messages:\n  " +
                string.Join("\n  ", _logger.InfoMessages));
        }

        var match = Regex.Match(
            modeLine,
            @"^Incremental mode: (?<compiled>\d+) file\(s\) to compile, (?<skipped>\d+) skipped");
        if (!match.Success)
        {
            throw new Xunit.Sdk.XunitException(
                $"Could not parse the incremental mode line: '{modeLine}'. The helper and " +
                "ProjectCompiler's log format have drifted apart.");
        }

        var reportedSkipped = int.Parse(match.Groups["skipped"].Value);
        if (reportedSkipped != skippedFiles.Length)
        {
            throw new Xunit.Sdk.XunitException(
                $"Expected {skippedFiles.Length} skipped file(s) ({string.Join(", ", skippedFiles)}), " +
                $"but the build reported: '{modeLine}'.");
        }

        var actualSkipped = (result.Metrics?.SkippedFiles ?? Array.Empty<string>())
            .Select(Path.GetFileName)
            .ToList();

        if (actualSkipped.Count != skippedFiles.Length)
        {
            throw new Xunit.Sdk.XunitException(
                $"The mode line reported {reportedSkipped} skipped file(s) but metrics recorded " +
                $"{actualSkipped.Count} ({string.Join(", ", actualSkipped)}).");
        }

        foreach (var expected in skippedFiles)
        {
            if (!actualSkipped.Contains(expected, StringComparer.OrdinalIgnoreCase))
            {
                throw new Xunit.Sdk.XunitException(
                    $"Expected '{expected}' to be served from the incremental cache, but the " +
                    $"skipped set was: {string.Join(", ", actualSkipped)}.");
            }
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

        if (expectedErrorPattern != null)
        {
            var errors = result.Diagnostics.GetErrors();
            if (!errors.Any(e => e.ToString().Contains(expectedErrorPattern)))
            {
                throw new Xunit.Sdk.XunitException(
                    $"Expected error containing '{expectedErrorPattern}', but got:\n" +
                    string.Join("\n", errors.Select(e => e.ToString())));
            }
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

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
/// Wraps another <see cref="ICompilerLogger"/> and records the info-level messages the compiler
/// emits, forwarding them to the inner logger only at the verbosity the inner logger asked for.
/// </summary>
/// <remarks>
/// The incremental skip decision is not on <see cref="ProjectCompilationResult"/>'s public surface
/// beyond the metrics counts — the authoritative "Incremental mode: N file(s) to compile, M skipped
/// (unchanged)" report is logged, so a test that wants to prove a build was warm has to capture the
/// log. Used by <see cref="ProjectCompilationHelper.AssertIncrementalSkipped"/> and by the two-build
/// tests in <c>IncrementalCompilationTests</c>.
/// </remarks>
public sealed class CapturingCompilerLogger : ICompilerLogger
{
    private readonly ICompilerLogger _inner;
    private readonly List<string> _infoMessages = new();

    public CapturingCompilerLogger(ICompilerLogger? inner = null)
    {
        _inner = inner ?? NullLogger.Instance;
    }

    /// <summary>
    /// Info messages logged since construction or the last <see cref="Clear"/>, in order.
    /// </summary>
    public IReadOnlyList<string> InfoMessages => _infoMessages;

    /// <summary>
    /// Drops captured messages so a subsequent build's log can be read on its own.
    /// </summary>
    public void Clear() => _infoMessages.Clear();

    public void LogInfo(string message)
    {
        _infoMessages.Add(message);
        if (_inner.IsEnabled(CompilerLogLevel.Info))
        {
            _inner.LogInfo(message);
        }
    }

    public void LogError(string message, int line, int column) => _inner.LogError(message, line, column);
    public void LogWarning(string message, int line, int column) => _inner.LogWarning(message, line, column);
    public void LogTokenRead(string tokenType, int line, int column, string value)
        => _inner.LogTokenRead(tokenType, line, column, value);
    public void LogIndentChange(int oldLevel, int newLevel) => _inner.LogIndentChange(oldLevel, newLevel);
    public void LogParseEnter(string rule, int tokenPosition) => _inner.LogParseEnter(rule, tokenPosition);
    public void LogParseExit(string rule, bool success) => _inner.LogParseExit(rule, success);
    public void LogMetrics(string metricsOutput) => _inner.LogMetrics(metricsOutput);

    public void LogDebug(string message)
    {
        if (_inner.IsEnabled(CompilerLogLevel.Debug))
        {
            _inner.LogDebug(message);
        }
    }

    public void LogTrace(string message)
    {
        if (_inner.IsEnabled(CompilerLogLevel.Trace))
        {
            _inner.LogTrace(message);
        }
    }

    /// <summary>
    /// Info messages are always produced (that is the point of this logger); everything else
    /// follows the inner logger, so wrapping a <see cref="NullLogger"/> does not turn on
    /// trace-level work.
    /// </summary>
    public bool IsEnabled(CompilerLogLevel level)
        => level <= CompilerLogLevel.Info || _inner.IsEnabled(level);
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

    /// <summary>
    /// Experimental feature flags to emit as a semicolon-separated &lt;Features&gt;
    /// PropertyGroup value in the generated .spyproj.
    /// </summary>
    public List<string> Features { get; set; } = new();

    /// <summary>
    /// When true, emits &lt;WarningsAsErrors&gt;true&lt;/WarningsAsErrors&gt; so warnings are
    /// promoted to errors (used to verify @suppress parity under -Werror, #1024).
    /// </summary>
    public bool WarningsAsErrors { get; set; }
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
