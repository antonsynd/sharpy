extern alias SharpyRT;
using System.Runtime.InteropServices;
using System.Text;

namespace Sharpy.Cli;

/// <summary>
/// Publishes a self-contained executable (no .NET runtime required) from an already
/// compiled Sharpy assembly. Creates a temporary wrapper project that references the
/// compiled assembly plus its runtime dependencies and shells out to
/// <c>dotnet publish</c>. Shared by the <c>run</c> and <c>compile</c> commands —
/// it writes the published artifact but does NOT execute it.
/// </summary>
internal static class SelfContainedPublisher
{
    /// <summary>
    /// Publishes a self-contained executable for the current runtime into
    /// <paramref name="outputDir"/>.
    /// </summary>
    /// <param name="compiledAssemblyPath">Path to the already-compiled Sharpy assembly.</param>
    /// <param name="assemblyName">
    /// The published artifact's file identity — the Sharpy source file's base name. Used for the
    /// wrapper project file name, the <c>&lt;AssemblyName&gt;</c>, and the published executable path.
    /// These are file names, so they take the raw stem verbatim.
    /// </param>
    /// <param name="entryTypeName">
    /// The C# IDENTIFIER of the generated module class that carries <c>Main()</c> — the emitter's
    /// mangled name (e.g. <c>ScProbe</c> for <c>sc_probe.spy</c>, <c>Program</c> for an entry
    /// <c>main.spy</c>), NOT the raw stem. The wrapper's <c>&lt;entryTypeName&gt;.Main()</c> call must
    /// name the type the compiled assembly actually declares; interpolating the raw stem wrote
    /// <c>sc_probe.Main()</c> against class <c>ScProbe</c> — CS0103, so EVERY publish failed (#1483).
    /// </param>
    /// <param name="outputDir">Directory to publish the self-contained executable into.</param>
    /// <param name="usedAssemblyPaths">Stdlib assemblies referenced by the program.</param>
    /// <returns>The path to the published executable, or <c>null</c> if publishing failed.</returns>
    internal static string? Publish(
        string compiledAssemblyPath,
        string assemblyName,
        string entryTypeName,
        string outputDir,
        IReadOnlySet<string> usedAssemblyPaths)
    {
        var rid = RuntimeInformation.RuntimeIdentifier;
        var sharpyCorePath = typeof(SharpyRT::Sharpy.Builtins).Assembly.Location;
        var cliDir = Path.GetDirectoryName(sharpyCorePath)!;

        Directory.CreateDirectory(outputDir);

        var tempProjDir = Path.Combine(Path.GetTempPath(), $"sharpy_proj_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempProjDir);

        try
        {
            var csprojPath = Path.Combine(tempProjDir, $"{assemblyName}.csproj");

            var stdlibRefs = new StringBuilder();
            foreach (var assemblyPath in usedAssemblyPaths)
            {
                var fileName = Path.GetFileName(assemblyPath);
                if (fileName.Equals("Sharpy.Core.dll", StringComparison.OrdinalIgnoreCase))
                    continue;
                var fullPath = Path.Combine(cliDir, fileName);
                if (File.Exists(fullPath))
                {
                    var includeName = Path.GetFileNameWithoutExtension(fileName);
                    stdlibRefs.AppendLine($@"    <Reference Include=""{includeName}"">
      <HintPath>{fullPath}</HintPath>
    </Reference>");
                }
            }

            // The program assembly is COPIED beside the wrapper under a fixed, distinct file name —
            // never <Reference>d — and reflection-loaded at run time (see BuildEntryPointSource). Two
            // things forced this over a direct `{entryTypeName}.Main()` call, and together they are why
            // every --self-contained publish failed (#1483):
            //   * The wrapper is a ref-assembly SDK project, but the program was compiled against the
            //     RUNTIME (implementation) assemblies — its metadata references System.Private.CoreLib
            //     directly. A compile-time reference + direct call is therefore CS0012 ("the type
            //     'Object' is defined in an assembly that is not referenced"). Copy-and-reflect keeps
            //     the wrapper's compile free of the program's types; the self-contained runtime pack
            //     supplies System.Private.CoreLib at run time.
            //   * For `compile -o {stem}.dll` the program's assembly identity IS {stem}, which would
            //     equal the wrapper's <AssemblyName> — the same-named managed dll would overwrite the
            //     program in the output and load in its place. The distinct copy name plus a dedicated
            //     AssemblyLoadContext (its simple name may still equal the wrapper's) keeps them apart.
            // The wrapper still references Sharpy.Core and the used stdlib assemblies so they are
            // published beside the program for its runtime dependency resolution.
            const string programCopyFileName = "__sharpy_program.dll";
            File.Copy(compiledAssemblyPath, Path.Combine(tempProjDir, programCopyFileName), overwrite: true);

            var csprojContent = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <AssemblyName>{assemblyName}</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <None Include=""{programCopyFileName}"">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <Reference Include=""Sharpy.Core"">
      <HintPath>{sharpyCorePath}</HintPath>
    </Reference>
{stdlibRefs}  </ItemGroup>
</Project>";

            File.WriteAllText(csprojPath, csprojContent);
            var entryPointSource = BuildEntryPointSource(programCopyFileName, entryTypeName);
            File.WriteAllText(Path.Combine(tempProjDir, "Program.cs"), entryPointSource);

            Console.WriteLine($"Publishing self-contained executable for {rid}...");
            var publishInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                ArgumentList =
                {
                    "publish",
                    csprojPath,
                    "--self-contained",
                    "-r", rid,
                    "-o", outputDir,
                    "--nologo",
                    "-v", "q"
                },
                UseShellExecute = false,
                RedirectStandardError = true
            };

            var publishProcess = System.Diagnostics.Process.Start(publishInfo);
            if (publishProcess != null)
            {
                var stderr = publishProcess.StandardError.ReadToEnd();
                publishProcess.WaitForExit();

                if (publishProcess.ExitCode != 0)
                {
                    Console.Error.WriteLine("Self-contained publish failed:");
                    Console.Error.WriteLine(stderr);
                    return null;
                }
            }

            var publishedExe = Path.Combine(outputDir, assemblyName);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                publishedExe += ".exe";

            if (!File.Exists(publishedExe))
            {
                Console.Error.WriteLine($"Published executable not found: {publishedExe}");
                return null;
            }

            return publishedExe;
        }
        finally
        {
            try
            { Directory.Delete(tempProjDir, recursive: true); }
            catch { }
        }
    }

    /// <summary>
    /// The wrapper's <c>Program.cs</c>: loads the compiled program assembly (copied beside the
    /// wrapper as <paramref name="programAssemblyFileName"/>) into its OWN AssemblyLoadContext and
    /// invokes its <c>Main</c> by reflection (#1483). <paramref name="entryTypeName"/> is the
    /// emitter's mangled module class (e.g. <c>ScProbe</c>, <c>Program</c>) — the reflected type
    /// name, never the raw file stem, which is the CS0103 the fix closes. A dedicated load context is
    /// used because the program's assembly identity may equal this wrapper's (e.g.
    /// <c>compile -o sc_probe.dll</c>), and loading a same-named assembly into the Default context
    /// would hand back the wrapper instead. Kept a pure function of its inputs so the entry-name
    /// mangling is testable without shelling out to <c>dotnet publish</c>.
    /// </summary>
    internal static string BuildEntryPointSource(string programAssemblyFileName, string entryTypeName) =>
        "// Auto-generated entry point (#1483): reflection-load the compiled program into its own\n"
        + "// AssemblyLoadContext (its simple name may equal this wrapper's) and invoke Main.\n"
        + "var __sharpyProgramContext = new System.Runtime.Loader.AssemblyLoadContext(\"sharpy-program\");\n"
        + "var __sharpyProgram = __sharpyProgramContext.LoadFromAssemblyPath(\n"
        + $"    System.IO.Path.Combine(System.AppContext.BaseDirectory, \"{programAssemblyFileName}\"));\n"
        + $"__sharpyProgram.GetType(\"{entryTypeName}\")!.GetMethod(\"Main\")!.Invoke(null, null);\n";
}
