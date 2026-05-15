extern alias SharpyRT;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Sharpy.Compiler.Logging;
using Sharpy.Compiler.Diagnostics;

namespace Sharpy.Compiler;

/// <summary>
/// Compiles generated C# code into .NET assemblies
/// </summary>
internal class AssemblyCompiler
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

            var diagnostics = new DiagnosticBag();
            foreach (var d in emitResult.Diagnostics.Where(d =>
                         d.Severity == DiagnosticSeverity.Error || d.Severity == DiagnosticSeverity.Warning))
            {
                diagnostics.Add(ToCompilerDiagnostic(d));
            }

            if (!emitResult.Success)
            {
                return new AssemblyCompilationResult
                {
                    Success = false,
                    Diagnostics = diagnostics,
                    Metrics = metrics
                };
            }

            _logger.LogInfo($"Successfully compiled assembly to: {outputPath}");

            // Generate runtime configuration file
            GenerateRuntimeConfig(outputPath, projectConfig);

            // Generate dependencies file
            GenerateDepsFile(outputPath, projectConfig);

            return new AssemblyCompilationResult
            {
                Success = true,
                OutputAssemblyPath = outputPath,
                Diagnostics = diagnostics,
                Metrics = metrics
            };
        }
        catch (Exception ex)
        {
            // Log full exception including stack trace for debugging
            _logger.LogError($"Assembly compilation failed with {ex.GetType().Name}: {ex}", 0, 0);

            // Create a user-facing error message that includes exception type for identification
            var errorMessage = ex is InternalCompilerErrorException ice
                ? $"Internal compiler error in {ice.Component} ({ex.GetType().Name}): {ex.Message}"
                : $"Assembly compilation failed ({ex.GetType().Name}): {ex.Message}";

            var errorDiagnostics = new DiagnosticBag();
            errorDiagnostics.AddError(errorMessage,
                code: DiagnosticCodes.Infrastructure.AssemblyCompilationFailed,
                phase: CompilerPhase.Assembly);
            return new AssemblyCompilationResult
            {
                Success = false,
                Diagnostics = errorDiagnostics,
                Metrics = metrics
            };
        }
    }

    /// <summary>
    /// Get metadata references for compilation
    /// </summary>
    internal static List<MetadataReference> GetDefaultReferences()
    {
        var references = new List<MetadataReference>();
        var coreLibPath = typeof(object).Assembly.Location;
        var coreLibDir = Path.GetDirectoryName(coreLibPath);

        if (!string.IsNullOrEmpty(coreLibDir))
        {
            references.Add(MetadataReference.CreateFromFile(coreLibPath));
            references.Add(MetadataReference.CreateFromFile(Path.Combine(coreLibDir, "System.Runtime.dll")));
            references.Add(MetadataReference.CreateFromFile(Path.Combine(coreLibDir, "System.Collections.dll")));
            references.Add(MetadataReference.CreateFromFile(Path.Combine(coreLibDir, "System.Linq.dll")));
        }

        references.Add(MetadataReference.CreateFromFile(typeof(SharpyRT::Sharpy.Builtins).Assembly.Location));

        return references;
    }

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
        references.Add(MetadataReference.CreateFromFile(typeof(SharpyRT::Sharpy.Builtins).Assembly.Location));

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
    /// Convert a Roslyn diagnostic to a structured CompilerDiagnostic
    /// </summary>
    private CompilerDiagnostic ToCompilerDiagnostic(Diagnostic diagnostic)
    {
        var severity = diagnostic.Severity == DiagnosticSeverity.Error
            ? CompilerDiagnosticSeverity.Error
            : diagnostic.Severity == DiagnosticSeverity.Warning
                ? CompilerDiagnosticSeverity.Warning
                : CompilerDiagnosticSeverity.Info;

        int? line = null;
        int? column = null;
        string? filePath = null;

        var location = diagnostic.Location;
        if (location.IsInSource)
        {
            // Use GetMappedLineSpan to respect #line directives, which map
            // generated C# locations back to original .spy source files.
            var lineSpan = location.GetMappedLineSpan();
            filePath = lineSpan.HasMappedPath ? lineSpan.Path : Path.GetFileName(lineSpan.Path);
            line = lineSpan.StartLinePosition.Line + 1;
            column = lineSpan.StartLinePosition.Character + 1;
        }

        return new CompilerDiagnostic(
            diagnostic.GetMessage(CultureInfo.InvariantCulture),
            severity,
            line,
            column,
            filePath,
            diagnostic.Id,
            CompilerPhase.Assembly);
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
            var sharpyCoreAssembly = typeof(SharpyRT::Sharpy.Builtins).Assembly;
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
internal class AssemblyCompilationResult
{
    public bool Success { get; init; }

    /// <summary>
    /// Structured diagnostics from assembly compilation.
    /// This is the primary way to access errors, warnings, and other diagnostics.
    /// </summary>
    public DiagnosticBag Diagnostics { get; init; } = new();

    public string? OutputAssemblyPath { get; init; }
    public CompilationMetrics? Metrics { get; init; }
}
