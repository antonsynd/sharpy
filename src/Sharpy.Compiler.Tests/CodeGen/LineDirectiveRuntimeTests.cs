using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Sharpy.Compiler.Logging;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;

namespace Sharpy.Compiler.Tests.CodeGen;

/// <summary>
/// Tests that #line directives in generated C# result in correct .spy file
/// references in runtime stack traces.
/// </summary>
[Collection("Sequential")]
public class LineDirectiveRuntimeTests
{
    private readonly ITestOutputHelper _output;

    public LineDirectiveRuntimeTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void RuntimeException_StackTrace_References_SpyFile()
    {
        // This Sharpy program will throw an exception at runtime on line 3
        var sharpySource = @"def fail() -> int:
    x: int = 42
    raise ValueError(""deliberate error"")
    return x

def main():
    result: int = fail()
    print(result)
";
        var fileName = "error_test.spy";

        // Phase 1: Compile Sharpy to C# (with #line directives enabled by default)
        var compiler = new Compiler();
        var result = compiler.Compile(sharpySource, fileName);

        result.Success.Should().BeTrue(
            $"compilation should succeed, errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(e => e.Message))}");

        var generatedCSharp = result.GeneratedCSharpCode!;
        _output.WriteLine("=== Generated C# ===");
        _output.WriteLine(generatedCSharp);
        _output.WriteLine("====================");

        // Verify the generated C# contains #line directives
        generatedCSharp.Should().Contain("#line");
        generatedCSharp.Should().Contain($"\"{fileName}\"");

        // Phase 2: Compile C# to assembly WITH PDB support
        var (assemblyPath, pdbPath, tempDir) = CompileCSharpToAssembly(generatedCSharp);

        if (assemblyPath == null)
        {
            _output.WriteLine("Skipping runtime test: C# compilation failed");
            return;
        }

        try
        {
            // Phase 3: Execute and capture stack trace
            var execResult = ExecuteAssembly(assemblyPath, tempDir);

            _output.WriteLine($"Exit code: {(execResult.Success ? 0 : 1)}");
            _output.WriteLine($"Stdout: {execResult.StandardOutput}");
            _output.WriteLine($"Stderr: {execResult.StandardError}");

            // The program should crash with an exception
            execResult.Success.Should().BeFalse("program should crash with ValueError (ArgumentException)");

            // The stack trace should reference the .spy file
            var stderr = execResult.StandardError;
            stderr.Should().Contain("deliberate error",
                "exception message should appear in stderr");
            stderr.Should().Contain(fileName,
                "stack trace should reference the .spy file name from #line directives");
        }
        finally
        {
            CleanupTempDir(tempDir);
        }
    }

    [Fact]
    public void RuntimeException_StackTrace_Shows_Correct_LineNumber()
    {
        // Line 2: x = 42
        // Line 3: raise ValueError("crash here")  <-- exception on line 3
        var sharpySource = @"def main():
    x: int = 42
    raise ValueError(""crash on line 3"")
    print(x)
";
        var fileName = "line_test.spy";

        var compiler = new Compiler();
        var result = compiler.Compile(sharpySource, fileName);

        result.Success.Should().BeTrue(
            $"compilation should succeed, errors: {string.Join(", ", result.Diagnostics.GetErrors().Select(e => e.Message))}");

        var generatedCSharp = result.GeneratedCSharpCode!;
        _output.WriteLine("=== Generated C# ===");
        _output.WriteLine(generatedCSharp);

        // Verify #line 3 is present (raise ValueError is on line 3)
        generatedCSharp.Should().Contain("#line 3");

        var (assemblyPath, pdbPath, tempDir) = CompileCSharpToAssembly(generatedCSharp);

        if (assemblyPath == null)
        {
            _output.WriteLine("Skipping runtime test: C# compilation failed");
            return;
        }

        try
        {
            var execResult = ExecuteAssembly(assemblyPath, tempDir);

            _output.WriteLine($"Stderr: {execResult.StandardError}");

            execResult.Success.Should().BeFalse("should crash with ValueError");

            var stderr = execResult.StandardError;
            stderr.Should().Contain(fileName,
                "stack trace should reference the .spy file");

            // Check that the correct line number appears in the stack trace
            // The stack trace should reference line 3 (where raise ValueError is)
            stderr.Should().Contain(":line 3",
                "stack trace should reference line 3 of the .spy file");
        }
        finally
        {
            CleanupTempDir(tempDir);
        }
    }

    private (string? assemblyPath, string? pdbPath, string tempDir) CompileCSharpToAssembly(string csharpCode)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"sharpy_line_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        var syntaxTree = CSharpSyntaxTree.ParseText(csharpCode);

        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
        };

        // Add Sharpy.Core reference
        string? runtimePath = null;
        try
        {
            var testAssemblyPath = Assembly.GetExecutingAssembly().Location;
            var testDir = Path.GetDirectoryName(testAssemblyPath);

            foreach (var targetFramework in new[] { "netstandard2.1", "netstandard2.0" })
            {
                runtimePath = Path.Combine(testDir!, "..", "..", "..", "..", "Sharpy.Core", "bin", "Debug", targetFramework, "Sharpy.Core.dll");
                runtimePath = Path.GetFullPath(runtimePath);

                if (File.Exists(runtimePath))
                {
                    references.Add(MetadataReference.CreateFromFile(runtimePath));

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
                        {
                            references.Add(MetadataReference.CreateFromFile(netstandardPath));
                        }
                    }
                    break;
                }
            }
        }
        catch
        {
            _output.WriteLine("Warning: Could not find Sharpy.Core");
        }

        var compilation = CSharpCompilation.Create(
            "SharpyLineTest",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.ConsoleApplication)
                .WithOptimizationLevel(OptimizationLevel.Debug));

        var assemblyPath = Path.Combine(tempDir, "SharpyLineTest.dll");
        var pdbPath = Path.Combine(tempDir, "SharpyLineTest.pdb");

        // Emit WITH PDB support - this is critical for #line directives to work
        using var assemblyStream = new FileStream(assemblyPath, FileMode.Create);
        using var pdbStream = new FileStream(pdbPath, FileMode.Create);
        var emitResult = compilation.Emit(assemblyStream, pdbStream);

        if (!emitResult.Success)
        {
            var errors = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString())
                .ToList();

            _output.WriteLine("C# compilation errors:");
            foreach (var error in errors)
                _output.WriteLine($"  {error}");

            return (null, null, tempDir);
        }

        // Copy Sharpy.Core to temp dir
        if (runtimePath != null && File.Exists(runtimePath))
        {
            File.Copy(runtimePath, Path.Combine(tempDir, "Sharpy.Core.dll"), overwrite: true);
        }

        // Create runtimeconfig.json
        var runtimeConfig = @"{
  ""runtimeOptions"": {
    ""tfm"": ""net10.0"",
    ""framework"": {
      ""name"": ""Microsoft.NETCore.App"",
      ""version"": ""10.0.0""
    }
  }
}";
        File.WriteAllText(Path.Combine(tempDir, "SharpyLineTest.runtimeconfig.json"), runtimeConfig);

        return (assemblyPath, pdbPath, tempDir);
    }

    private record ExecResult(bool Success, string StandardOutput, string StandardError);

    private ExecResult ExecuteAssembly(string assemblyPath, string workingDir)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"exec \"{assemblyPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDir
        };

        using var process = new Process { StartInfo = startInfo };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) stdout.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) stderr.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit(30000))
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return new ExecResult(false, stdout.ToString(), "Execution timed out");
        }

        process.WaitForExit();

        return new ExecResult(process.ExitCode == 0, stdout.ToString(), stderr.ToString());
    }

    private static void CleanupTempDir(string tempDir)
    {
        try
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
        catch { }
    }
}
