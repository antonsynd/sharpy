using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Sharpy.Compiler.Logging;

namespace Sharpy.Compiler;

/// <summary>
/// Compiles generated C# code into .NET assemblies
/// </summary>
public class AssemblyCompiler
{
    private readonly ICompilerLogger _logger;

    public AssemblyCompiler(ICompilerLogger? logger = null)
    {
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Compile C# source code to a .NET assembly
    /// </summary>
    public AssemblyCompilationResult CompileToAssembly(
        Dictionary<string, string> csharpSources,
        ProjectConfig projectConfig)
    {
        _logger.LogInfo($"Compiling {csharpSources.Count} C# files to assembly");

        try
        {
            // Parse all C# source files into syntax trees
            var syntaxTrees = new List<SyntaxTree>();
            foreach (var (filePath, sourceCode) in csharpSources)
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode,
                    path: filePath,
                    encoding: System.Text.Encoding.UTF8);
                syntaxTrees.Add(syntaxTree);
            }

            // Gather metadata references
            var references = GetMetadataReferences(projectConfig);

            // Determine output kind
            var outputKind = projectConfig.OutputType.ToLowerInvariant() == "exe"
                ? OutputKind.ConsoleApplication
                : OutputKind.DynamicallyLinkedLibrary;

            // Create compilation
            var assemblyName = projectConfig.AssemblyName ?? projectConfig.RootNamespace;
            var compilation = CSharpCompilation.Create(
                assemblyName,
                syntaxTrees,
                references,
                new CSharpCompilationOptions(outputKind)
                    .WithOptimizationLevel(projectConfig.Configuration == "Release"
                        ? OptimizationLevel.Release
                        : OptimizationLevel.Debug)
                    .WithPlatform(Platform.AnyCpu));

            // Ensure output directory exists
            var outputPath = projectConfig.OutputAssemblyPath;
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Emit assembly to file
            using var assemblyStream = new FileStream(outputPath, FileMode.Create);

            EmitResult emitResult;
            if (projectConfig.Configuration == "Debug")
            {
                // Emit with PDB for debugging
                var pdbPath = Path.ChangeExtension(outputPath, ".pdb");
                using var pdbStream = new FileStream(pdbPath, FileMode.Create);
                emitResult = compilation.Emit(assemblyStream, pdbStream);
            }
            else
            {
                // Release build without debug symbols
                emitResult = compilation.Emit(assemblyStream);
            }

            if (!emitResult.Success)
            {
                var errors = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => FormatDiagnostic(d))
                    .ToList();

                var warnings = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Warning)
                    .Select(d => FormatDiagnostic(d))
                    .ToList();

                return new AssemblyCompilationResult
                {
                    Success = false,
                    Errors = errors,
                    Warnings = warnings
                };
            }

            var allWarnings = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Warning)
                .Select(d => FormatDiagnostic(d))
                .ToList();

            _logger.LogInfo($"Successfully compiled assembly to: {outputPath}");

            return new AssemblyCompilationResult
            {
                Success = true,
                OutputAssemblyPath = outputPath,
                Warnings = allWarnings
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Assembly compilation failed: {ex.Message}", 0, 0);
            return new AssemblyCompilationResult
            {
                Success = false,
                Errors = new List<string> { $"Assembly compilation failed: {ex.Message}" }
            };
        }
    }

    /// <summary>
    /// Get metadata references for compilation
    /// </summary>
    private List<MetadataReference> GetMetadataReferences(ProjectConfig projectConfig)
    {
        var references = new List<MetadataReference>();

        // Add core .NET references
        var coreLibPath = typeof(object).Assembly.Location;
        var coreLibDir = Path.GetDirectoryName(coreLibPath);

        if (!string.IsNullOrEmpty(coreLibDir))
        {
            references.Add(MetadataReference.CreateFromFile(coreLibPath)); // System.Private.CoreLib
            references.Add(MetadataReference.CreateFromFile(Path.Combine(coreLibDir, "System.Runtime.dll")));
            references.Add(MetadataReference.CreateFromFile(Path.Combine(coreLibDir, "System.Console.dll")));
            references.Add(MetadataReference.CreateFromFile(Path.Combine(coreLibDir, "System.Collections.dll")));
            references.Add(MetadataReference.CreateFromFile(Path.Combine(coreLibDir, "System.Linq.dll")));
        }

        // Add Sharpy.Core reference
        references.Add(MetadataReference.CreateFromFile(typeof(Sharpy.Core.Exports).Assembly.Location));

        // Add project-specific references
        foreach (var referencePath in projectConfig.References)
        {
            if (File.Exists(referencePath))
            {
                references.Add(MetadataReference.CreateFromFile(referencePath));
                _logger.LogDebug($"Added reference: {referencePath}");
            }
            else
            {
                _logger.LogWarning($"Reference not found: {referencePath}", 0, 0);
            }
        }

        return references;
    }

    /// <summary>
    /// Format a diagnostic message
    /// </summary>
    private string FormatDiagnostic(Diagnostic diagnostic)
    {
        var location = diagnostic.Location;
        if (location.IsInSource)
        {
            var lineSpan = location.GetLineSpan();
            var fileName = Path.GetFileName(lineSpan.Path);
            var line = lineSpan.StartLinePosition.Line + 1;
            var column = lineSpan.StartLinePosition.Character + 1;
            return $"{fileName}({line},{column}): {diagnostic.Severity.ToString().ToLower()} {diagnostic.Id}: {diagnostic.GetMessage()}";
        }

        return $"{diagnostic.Severity.ToString().ToLower()} {diagnostic.Id}: {diagnostic.GetMessage()}";
    }
}

/// <summary>
/// Result of assembly compilation
/// </summary>
public class AssemblyCompilationResult
{
    public bool Success { get; init; }
    public List<string> Errors { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
    public string? OutputAssemblyPath { get; init; }
}
