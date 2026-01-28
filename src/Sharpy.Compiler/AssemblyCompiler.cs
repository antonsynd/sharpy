using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Diagnostics;

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
        var metrics = new CompilationMetrics(
            projectName: projectConfig.RootNamespace,
            configuration: projectConfig.Configuration);

        try
        {
            // Parse all C# source files into syntax trees
            metrics.StartPhase("C# Parsing");
            var syntaxTrees = new List<SyntaxTree>();
            foreach (var (filePath, sourceCode) in csharpSources)
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode,
                    path: filePath,
                    encoding: System.Text.Encoding.UTF8);
                syntaxTrees.Add(syntaxTree);
            }
            metrics.EndPhase();

            // Gather metadata references
            metrics.StartPhase("Reference Resolution");
            var references = GetMetadataReferences(projectConfig);
            metrics.EndPhase();

            // Determine output kind
            var outputKind = projectConfig.OutputType.ToLowerInvariant() == "exe"
                ? OutputKind.ConsoleApplication
                : OutputKind.DynamicallyLinkedLibrary;

            // Create compilation
            metrics.StartPhase("Roslyn Compilation");
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
            metrics.EndPhase();

            // Ensure output directory exists
            var outputPath = projectConfig.OutputAssemblyPath;
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Emit assembly to file
            metrics.StartPhase("IL Emission");
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
            metrics.EndPhase();

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
                    Warnings = warnings,
                    Metrics = metrics
                };
            }

            var allWarnings = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Warning)
                .Select(d => FormatDiagnostic(d))
                .ToList();

            _logger.LogInfo($"Successfully compiled assembly to: {outputPath}");

            // Generate runtime configuration file
            GenerateRuntimeConfig(outputPath, projectConfig);

            // Generate dependencies file
            GenerateDepsFile(outputPath, projectConfig);

            return new AssemblyCompilationResult
            {
                Success = true,
                OutputAssemblyPath = outputPath,
                Warnings = allWarnings,
                Metrics = metrics
            };
        }
        catch (Exception ex)
        {
            _logger.LogError($"Assembly compilation failed: {ex.Message}", 0, 0);
            return new AssemblyCompilationResult
            {
                Success = false,
                Errors = new List<string> { $"Assembly compilation failed: {ex.Message}" },
                Metrics = metrics
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

        // Add netstandard reference (required because Sharpy.Core targets netstandard2.1/2.0)
        try
        {
            var netstandardAssembly = System.Reflection.Assembly.Load("netstandard");
            references.Add(MetadataReference.CreateFromFile(netstandardAssembly.Location));
        }
        catch
        {
            // netstandard may not be directly loadable in all runtimes
            // Try to find it in the runtime directory
            if (!string.IsNullOrEmpty(coreLibDir))
            {
                var netstandardPath = Path.Combine(coreLibDir, "netstandard.dll");
                if (File.Exists(netstandardPath))
                {
                    references.Add(MetadataReference.CreateFromFile(netstandardPath));
                }
            }
        }

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

    /// <summary>
    /// Generate a .runtimeconfig.json file for the assembly
    /// </summary>
    private void GenerateRuntimeConfig(string assemblyPath, ProjectConfig projectConfig)
    {
        try
        {
            var runtimeConfigPath = Path.ChangeExtension(assemblyPath, ".runtimeconfig.json");

            // Get the current runtime version
            var runtimeVersion = Environment.Version;
            var frameworkVersion = $"{runtimeVersion.Major}.{runtimeVersion.Minor}.{runtimeVersion.Build}";

            // Create runtime config JSON
            var runtimeConfig = $$"""
{
  "runtimeOptions": {
    "tfm": "{{projectConfig.TargetFramework}}",
    "framework": {
      "name": "Microsoft.NETCore.App",
      "version": "{{frameworkVersion}}"
    },
    "configProperties": {
      "System.Reflection.Metadata.MetadataUpdater.IsSupported": false
    }
  }
}
""";

            File.WriteAllText(runtimeConfigPath, runtimeConfig);
            _logger.LogDebug($"Generated runtime config: {runtimeConfigPath}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to generate runtime config: {ex.Message}", 0, 0);
        }
    }

    /// <summary>
    /// Generate a .deps.json file for the assembly
    /// </summary>
    private void GenerateDepsFile(string assemblyPath, ProjectConfig projectConfig)
    {
        try
        {
            var depsPath = Path.ChangeExtension(assemblyPath, ".deps.json");
            var assemblyName = projectConfig.AssemblyName ?? projectConfig.RootNamespace;

            // Get Sharpy.Core assembly info
            var sharpyCoreAssembly = typeof(Sharpy.Core.Exports).Assembly;
            var sharpyCoreLocation = sharpyCoreAssembly.Location;
            var sharpyCoreName = sharpyCoreAssembly.GetName();
            var sharpyCoreVersion = sharpyCoreName.Version?.ToString() ?? "1.0.0";

            // Get the current runtime version
            var runtimeVersion = Environment.Version;
            var frameworkVersion = $"{runtimeVersion.Major}.{runtimeVersion.Minor}.{runtimeVersion.Build}";

            // Create deps.json content
            var depsJson = $$"""
{
  "runtimeTarget": {
    "name": ".NETCoreApp,Version=v{{runtimeVersion.Major}}.{{runtimeVersion.Minor}}",
    "signature": ""
  },
  "compilationOptions": {},
  "targets": {
    ".NETCoreApp,Version=v{{runtimeVersion.Major}}.{{runtimeVersion.Minor}}": {
      "{{assemblyName}}/1.0.0": {
        "dependencies": {
          "Sharpy.Core": "{{sharpyCoreVersion}}"
        },
        "runtime": {
          "{{Path.GetFileName(assemblyPath)}}": {}
        }
      },
      "Sharpy.Core/{{sharpyCoreVersion}}": {
        "runtime": {
          "{{Path.GetFileName(sharpyCoreLocation)}}": {
            "assemblyVersion": "{{sharpyCoreVersion}}",
            "fileVersion": "{{sharpyCoreVersion}}"
          }
        }
      }
    }
  },
  "libraries": {
    "{{assemblyName}}/1.0.0": {
      "type": "project",
      "serviceable": false,
      "sha512": ""
    },
    "Sharpy.Core/{{sharpyCoreVersion}}": {
      "type": "reference",
      "serviceable": false,
      "sha512": ""
    }
  }
}
""";

            File.WriteAllText(depsPath, depsJson);
            _logger.LogDebug($"Generated deps file: {depsPath}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to generate deps file: {ex.Message}", 0, 0);
        }
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
    public CompilationMetrics? Metrics { get; init; }
}
